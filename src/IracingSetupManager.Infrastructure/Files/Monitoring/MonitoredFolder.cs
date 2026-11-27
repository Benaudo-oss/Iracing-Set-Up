namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed record MonitoredFolder(
    string Path,
    ImportFolderKind Kind,
    string? Provider = null)
{
    public bool IncludeTelemetry { get; init; }
    public string? TelemetryDestination { get; init; }
}

