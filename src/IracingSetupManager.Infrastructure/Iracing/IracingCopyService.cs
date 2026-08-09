using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Iracing;

public enum IracingConflictChoice
{
    None,
    Skip,
    KeepBoth
}

public sealed record IracingCopyPlanItem(
    Guid SetupId,
    string OriginalFileName,
    string Car,
    string SourcePath,
    string DestinationPath,
    bool HasConflict,
    IracingConflictChoice ConflictChoice = IracingConflictChoice.None);

public sealed record IracingCopyResult(int Copied, int Skipped);

public sealed class IracingCopyService(ISetupDbContextFactory contextFactory)
{
    public static string? DetectSetupsFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var detected = Path.Combine(documents, "iRacing", "setups");
        return Directory.Exists(detected) ? detected : null;
    }

    public async Task<IReadOnlyList<IracingCopyPlanItem>> CreatePlanAsync(
        IReadOnlyCollection<Guid> setupIds,
        string iracingSetupsFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iracingSetupsFolder);
        var root = Path.GetFullPath(iracingSetupsFolder);
        var ids = setupIds.Distinct().ToArray();

        await using var context = contextFactory.Create();
        var setups = await context.Setups.AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.Status == SetupStatus.Valide)
            .OrderBy(item => item.Car)
            .ThenBy(item => item.OriginalFileName)
            .ToListAsync(cancellationToken);

        return setups.Select(setup =>
        {
            var destination = Path.Combine(root, SanitizeSegment(setup.Car), setup.OriginalFileName);
            return new IracingCopyPlanItem(
                setup.Id,
                setup.OriginalFileName,
                setup.Car,
                setup.ArchivePath,
                destination,
                File.Exists(destination));
        }).ToList();
    }

    public async Task<IracingCopyResult> ExecuteAsync(
        IReadOnlyCollection<IracingCopyPlanItem> plan,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            throw new InvalidOperationException("La copie doit être confirmée après affichage de l’aperçu.");
        }

        if (plan.Any(item => item.HasConflict && item.ConflictChoice == IracingConflictChoice.None))
        {
            throw new InvalidOperationException("Chaque conflit doit être résolu avant la copie.");
        }

        var planIds = plan.Select(item => item.SetupId).Distinct().ToArray();
        await using (var context = contextFactory.Create())
        {
            var validatedIds = await context.Setups.AsNoTracking()
                .Where(item => planIds.Contains(item.Id) && item.Status == SetupStatus.Valide)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            if (validatedIds.Count != planIds.Length)
            {
                throw new InvalidOperationException("La copie contient un setup qui n’est plus validé.");
            }
        }

        var copied = 0;
        var skipped = 0;
        foreach (var item in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.HasConflict && item.ConflictChoice == IracingConflictChoice.Skip)
            {
                skipped++;
                continue;
            }

            if (!File.Exists(item.SourcePath))
            {
                throw new FileNotFoundException("Le fichier original est absent de l’archive.", item.SourcePath);
            }

            var destination = item.HasConflict
                ? FindAvailablePath(item.DestinationPath)
                : item.DestinationPath;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(item.SourcePath, destination, overwrite: false);
            copied++;
        }

        return new IracingCopyResult(copied, skipped);
    }

    private static string FindAvailablePath(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizeSegment(string value)
    {
        var result = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? "Voiture à identifier" : result;
    }
}
