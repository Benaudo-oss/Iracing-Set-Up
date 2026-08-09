namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed record DetectedImportFile(
    string FullPath,
    ImportFolderKind SourceKind,
    string? Provider);

