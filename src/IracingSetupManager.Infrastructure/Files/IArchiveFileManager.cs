namespace IracingSetupManager.Infrastructure.Files;

public interface IArchiveFileManager
{
    Task<string> CopyWithoutOverwriteAsync(
        string sourcePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default);
}

