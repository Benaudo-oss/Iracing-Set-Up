using System.IO.Compression;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class SecureZipExtractor(
    int maximumEntries = 10_000,
    long maximumUncompressedBytes = 2L * 1024 * 1024 * 1024)
{
    public async Task<IReadOnlyList<string>> ExtractAsync(
        string zipPath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        var extractedFiles = new List<string>();

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > maximumEntries)
        {
            throw new InvalidDataException("L'archive contient trop de fichiers.");
        }

        long totalSize = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalSize = checked(totalSize + entry.Length);
            if (totalSize > maximumUncompressedBytes)
            {
                throw new InvalidDataException("L'archive dépasse la taille maximale autorisée.");
            }

            if (IsSymbolicLink(entry))
            {
                throw new InvalidDataException("Les liens symboliques ne sont pas autorisés dans une archive.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!IsSameOrChildOf(destinationPath, destinationRoot))
            {
                throw new InvalidDataException("L'archive tente d'écrire en dehors du dossier d'extraction.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var input = entry.Open();
            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await input.CopyToAsync(output, cancellationToken);
            extractedFiles.Add(destinationPath);
        }

        return extractedFiles;
    }

    private static bool IsSameOrChildOf(string candidate, string parent) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsSymbolicLink(ZipArchiveEntry entry) =>
        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;
}

