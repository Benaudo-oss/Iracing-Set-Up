namespace IracingSetupManager.Infrastructure.Files;

public sealed class ArchiveFileManager(Sha256Calculator sha256Calculator) : IArchiveFileManager
{
    public async Task<string> CopyWithoutOverwriteAsync(
        string sourcePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var sourceFullPath = SecurePath.GetFullPath(sourcePath);
        var destinationRoot = SecurePath.GetFullPath(destinationDirectory);
        var originalFileName = Path.GetFileName(sourceFullPath);
        Directory.CreateDirectory(destinationRoot);
        var destinationPath = SecurePath.EnsureChildOf(Path.Combine(destinationRoot, originalFileName), destinationRoot);

        if (File.Exists(destinationPath))
        {
            var sourceHash = await sha256Calculator.CalculateAsync(sourceFullPath, cancellationToken);
            var existingHash = await sha256Calculator.CalculateAsync(destinationPath, cancellationToken);
            if (sourceHash.Equals(existingHash, StringComparison.OrdinalIgnoreCase))
            {
                return destinationPath;
            }

            var conflictDirectory = SecurePath.EnsureChildOf(Path.Combine(destinationRoot, "Conflits", sourceHash[..12]), destinationRoot);
            Directory.CreateDirectory(conflictDirectory);
            destinationPath = Path.Combine(conflictDirectory, originalFileName);

            if (File.Exists(destinationPath))
            {
                var conflictHash = await sha256Calculator.CalculateAsync(destinationPath, cancellationToken);
                if (sourceHash.Equals(conflictHash, StringComparison.OrdinalIgnoreCase))
                {
                    return destinationPath;
                }

                throw new IOException("Un conflit de nom et d'empreinte n'a pas pu être résolu sans écrasement.");
            }
        }

        await using var source = new FileStream(
            sourceFullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous);
        await source.CopyToAsync(destination, cancellationToken);
        return destinationPath;
    }
}
