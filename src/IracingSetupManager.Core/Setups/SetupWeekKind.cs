namespace IracingSetupManager.Core.Setups;

public enum SetupWeekKind
{
    Unknown,
    Numeric,
    Nec,
    NoWeek
}

public static class SetupWeekPresentation
{
    public static string Display(int? week, SetupWeekKind kind) => EffectiveKind(week, kind) switch
    {
        SetupWeekKind.Numeric when week is >= 1 and <= 13 => $"Week {week:00}",
        SetupWeekKind.Nec => "Week NEC",
        SetupWeekKind.NoWeek => "Sans Week",
        _ => "Week inconnue"
    };

    public static SetupWeekKind EffectiveKind(int? week, SetupWeekKind kind) =>
        week is >= 1 and <= 13 ? SetupWeekKind.Numeric : kind;
}
