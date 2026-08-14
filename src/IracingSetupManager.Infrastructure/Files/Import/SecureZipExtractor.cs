using System.IO.Compression;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class SecureZipExtractor(
    int maximumEntries = 10_000,
    long maximumUncompressedBytes = 2L * 1024 * 1024 * 1024,
    long maximumEntryBytes = 256L * 1024 * 1024,
    int maximumCompressionRatio = 200)
{
    public bool ContainsSetup(string zipPath, CancellationToken cancellationToken = default)
    {
        using var archive = ZipFile.OpenRead(SecurePath.GetFullPath(zipPath));
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(entry.Name) &&
                Path.GetExtension(entry.Name).Equals(".sto", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<string>> ExtractAsync(string zipPath, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        var source = SecurePath.GetFullPath(zipPath);
        var destinationRoot = SecurePath.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);

        using var archive = ZipFile.OpenRead(source);
        if (archive.Entries.Count > maximumEntries) throw new InvalidDataException("L'archive contient trop de fichiers.");

        long totalSize = 0;
        var entries = new List<(ZipArchiveEntry Entry, string Destination)>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalSize = checked(totalSize + entry.Length);
            if (totalSize > maximumUncompressedBytes) throw new InvalidDataException("L'archive dépasse la taille maximale autorisée.");
            if (entry.Length > maximumEntryBytes) throw new InvalidDataException("Un fichier de l'archive dépasse la taille maximale autorisée.");
            if (entry.Length > 0 && (entry.CompressedLength == 0 || entry.Length / Math.Max(1, entry.CompressedLength) > maximumCompressionRatio))
                throw new InvalidDataException("Le taux de compression de l'archive est anormalement élevé.");
            if (IsSymbolicLink(entry)) throw new InvalidDataException("Les liens symboliques ne sont pas autorisés dans une archive.");

            SecurePath.ValidateArchiveEntry(entry.FullName);
            var destination = SecurePath.EnsureChildOf(Path.Combine(destinationRoot, entry.FullName), destinationRoot);
            if (!destinations.Add(destination)) throw new InvalidDataException("L'archive contient plusieurs entrées vers le même fichier.");
            entries.Add((entry, destination));
        }

        var extracted = new List<string>();
        foreach (var (entry, destination) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await input.CopyToAsync(output, cancellationToken);
            extracted.Add(destination);
        }
        return extracted;
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry) => ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;
}
