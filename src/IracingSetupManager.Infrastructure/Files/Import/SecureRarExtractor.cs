using SharpCompress.Archives;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class SecureRarExtractor(
    int maximumEntries = 10_000,
    long maximumUncompressedBytes = 2L * 1024 * 1024 * 1024,
    long maximumEntryBytes = 256L * 1024 * 1024,
    int maximumCompressionRatio = 200,
    IAtomicFileOperations? atomicFileOperations = null)
{
    private readonly IAtomicFileOperations atomicFiles = atomicFileOperations ?? new AtomicFileOperations();

    public bool ContainsSetup(string rarPath, CancellationToken cancellationToken = default)
    {
        using var archive = ArchiveFactory.OpenArchive(SecurePath.GetFullPath(rarPath));
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Key) &&
                Path.GetExtension(entry.Key).Equals(".sto", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<string>> ExtractAsync(
        string rarPath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        var source = SecurePath.GetFullPath(rarPath);
        var destinationRoot = SecurePath.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);

        using var archive = ArchiveFactory.OpenArchive(source);
        var archiveEntries = archive.Entries.ToList();
        long totalSize = 0;
        var entries = new List<(IArchiveEntry Entry, string Destination)>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archiveEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Key) ||
                !Path.GetExtension(entry.Key).Equals(".sto", StringComparison.OrdinalIgnoreCase)) continue;
            if (entries.Count >= maximumEntries) throw new InvalidDataException("L’archive contient trop de setups.");
            SecurePath.ValidateArchiveEntry(entry.Key);

            totalSize = checked(totalSize + entry.Size);
            if (totalSize > maximumUncompressedBytes) throw new InvalidDataException("L’archive dépasse la taille maximale autorisée.");
            if (entry.Size > maximumEntryBytes) throw new InvalidDataException("Un fichier de l’archive dépasse la taille maximale autorisée.");
            if (entry.Size > 0 && (entry.CompressedSize == 0 || entry.Size / Math.Max(1, entry.CompressedSize) > maximumCompressionRatio))
                throw new InvalidDataException("Le taux de compression de l’archive est anormalement élevé.");

            var destination = SecurePath.EnsureChildOf(Path.Combine(destinationRoot, entry.Key), destinationRoot);
            if (!destinations.Add(destination)) throw new InvalidDataException("L’archive contient plusieurs entrées vers le même fichier.");
            entries.Add((entry, destination));
        }

        var extracted = new List<string>();
        foreach (var (entry, destination) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await atomicFiles.WriteAsync(destination, async (output, token) =>
            {
                await using var input = entry.OpenEntryStream();
                await input.CopyToAsync(output, token);
            }, cancellationToken);
            extracted.Add(destination);
        }

        return extracted;
    }
}
