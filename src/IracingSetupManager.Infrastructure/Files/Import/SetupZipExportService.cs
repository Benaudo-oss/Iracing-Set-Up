using System.IO.Compression;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class SetupZipExportService(ISetupDbContextFactory contextFactory)
{
    public const int MaximumBatchSize = 500;

    public static string? GetBlockReason(SetupEntity setup)
    {
        if (string.IsNullOrWhiteSpace(setup.Car) || setup.Car.Trim().Equals("À identifier", StringComparison.OrdinalIgnoreCase))
            return "Voiture à identifier";
        if (!Path.GetExtension(setup.ArchivePath).Equals(".sto", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(setup.ArchivePath)) return "Fichier .sto indisponible";
        return null;
    }

    public async Task<bool> ExportAsync(IReadOnlyCollection<Guid> setupIds, string destination,
        CancellationToken cancellationToken = default)
    {
        if (setupIds.Count is < 1 or > MaximumBatchSize) throw new ArgumentOutOfRangeException(nameof(setupIds));
        if (!string.Equals(Path.GetExtension(destination), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Le fichier de destination doit être un ZIP.", nameof(destination));
        var ids = setupIds.Distinct().ToArray();
        await using var context = contextFactory.Create();
        var setups = await context.Setups.Where(setup => ids.Contains(setup.Id)).ToListAsync(cancellationToken);
        if (setups.Count != ids.Length || setups.Any(setup => GetBlockReason(setup) is not null))
            throw new InvalidOperationException("La sélection a changé ou contient un setup non exportable.");

        await new AtomicFileOperations().WriteAsync(destination, async (output, token) =>
        {
            using var outer = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            var carNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var car in setups.GroupBy(setup => setup.Car.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                using var entry = outer.CreateEntry(UniqueName(car.Key, ".zip", carNames), CompressionLevel.NoCompression).Open();
                using var inner = new ZipArchive(entry, ZipArchiveMode.Create, leaveOpen: true);
                var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var setup in car.OrderBy(setup => setup.OriginalFileName, StringComparer.OrdinalIgnoreCase).ThenBy(setup => setup.Id))
                {
                    token.ThrowIfCancellationRequested();
                    await using var source = File.OpenRead(setup.ArchivePath);
                    await using var target = inner.CreateEntry(
                        UniqueName(Path.GetFileNameWithoutExtension(setup.OriginalFileName), ".sto", filenames),
                        CompressionLevel.NoCompression).Open();
                    await source.CopyToAsync(target, token);
                }
            }
        }, cancellationToken);

        try
        {
            var exportedAt = DateTimeOffset.UtcNow;
            foreach (var setup in setups) setup.LastExportedAtUtc = exportedAt;
            await context.SaveChangesAsync(CancellationToken.None);
            return true;
        }
        catch (DbUpdateException) { return false; }
    }

    private static string UniqueName(string name, string extension, HashSet<string> used)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var stem = new string(name.Select(character =>
            invalid.Contains(character) || character is '/' or '\\' or ':' || char.IsControl(character) ? '_' : character).ToArray()).Trim(' ', '.');
        if (stem.Length == 0) stem = "Setup";
        // Also rejects Windows device names when the ZIP is extracted.
        if (System.Text.RegularExpressions.Regex.IsMatch(stem,
            @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)) stem = "_" + stem;
        var candidate = stem + extension;
        for (var suffix = 2; !used.Add(candidate); suffix++) candidate = $"{stem} ({suffix}){extension}";
        return candidate;
    }
}
