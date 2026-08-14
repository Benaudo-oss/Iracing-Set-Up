namespace IracingSetupManager.Integrations.Updates;

public interface IUpdateService
{
    Task<UpdateAvailability> CheckAsync(Version installedVersion, CancellationToken cancellationToken = default);
    Task<DownloadedUpdate> DownloadAndVerifyAsync(
        UpdateAvailability update,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateAvailability(
    bool IsAvailable,
    Version InstalledVersion,
    Version? AvailableVersion,
    Uri? DownloadUri,
    Uri? Sha256Uri,
    string? AssetName,
    string? ReleaseNotes);

public sealed record DownloadedUpdate(Version Version, string InstallerPath, string Sha256);

public sealed record UpdateDownloadProgress(long BytesReceived, long? TotalBytes, string Stage)
{
    public double? Percentage => TotalBytes is > 0 ? BytesReceived * 100d / TotalBytes.Value : null;
}
