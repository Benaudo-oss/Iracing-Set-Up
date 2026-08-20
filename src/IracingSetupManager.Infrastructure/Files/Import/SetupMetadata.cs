using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed record SetupMetadata(
    string Provider,
    string Category,
    string Car,
    string Track,
    string? TrackConfiguration,
    string? Season,
    string SetupType,
    int? Week = null,
    SetupWeekKind WeekKind = SetupWeekKind.Unknown)
{
    public SetupWeekKind EffectiveWeekKind => SetupWeekPresentation.EffectiveKind(Week, WeekKind);
}
