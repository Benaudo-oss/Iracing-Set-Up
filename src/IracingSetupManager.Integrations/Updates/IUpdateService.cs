namespace IracingSetupManager.Integrations.Updates;

public interface IUpdateService
{
    Task<UpdateAvailability> CheckAsync(
        Version installedVersion,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateAvailability(
    bool IsAvailable,
    Version InstalledVersion,
    Version? AvailableVersion,
    Uri? DownloadUri,
    string? Sha256);

