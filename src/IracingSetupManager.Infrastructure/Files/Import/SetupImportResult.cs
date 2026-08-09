namespace IracingSetupManager.Infrastructure.Files.Import;

public enum SetupImportOutcome
{
    Imported,
    Duplicate,
    Unsupported
}

public sealed record SetupImportResult(
    string SourcePath,
    SetupImportOutcome Outcome,
    string? ArchivePath = null,
    string? Sha256 = null);

