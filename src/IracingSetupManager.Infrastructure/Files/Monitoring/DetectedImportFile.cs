namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed record DetectedImportFile(
    string FullPath,
    ImportFolderKind SourceKind,
    string? Provider,
    long? Length = null,
    long? LastWriteTimeUtcTicks = null)
{
    public MonitoredFileSnapshot? Snapshot =>
        Length is not null && LastWriteTimeUtcTicks is not null
            ? new MonitoredFileSnapshot(FullPath, Length.Value, LastWriteTimeUtcTicks.Value)
            : null;
}
