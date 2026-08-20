namespace IracingSetupManager.Infrastructure.Files.Import;

public enum SetupImportOutcome
{
    Imported,
    Duplicate,
    Filtered,
    Unsupported
}

public sealed record SetupImportResult(
    string SourcePath,
    SetupImportOutcome Outcome,
    string? ArchivePath = null,
    string? Sha256 = null,
    int? Week = null,
    IracingSetupManager.Core.Setups.SetupWeekKind WeekKind = IracingSetupManager.Core.Setups.SetupWeekKind.Unknown)
{
    public string WeekDisplay => IracingSetupManager.Core.Setups.SetupWeekPresentation.Display(Week, WeekKind);
}
