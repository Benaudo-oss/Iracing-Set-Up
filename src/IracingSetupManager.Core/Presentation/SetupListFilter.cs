namespace IracingSetupManager.Core.Presentation;

public sealed record SetupListItem(
    string FileName,
    string Provider,
    string Category,
    string Car,
    string Track,
    string? TrackConfiguration,
    string? Season,
    string SetupType,
    string Status,
    string Week = "Week inconnue");

public sealed record SetupFilterCriteria(
    string? Search = null,
    string? Provider = null,
    string? Category = null,
    string? Car = null,
    string? Track = null,
    string? Week = null,
    string? Status = null);

public static class SetupListFilter
{
    public static bool Matches(SetupListItem item, SetupFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(criteria);
        return MatchesSelection(item.Provider, criteria.Provider) &&
               MatchesSelection(item.Category, criteria.Category) &&
               MatchesSelection(item.Car, criteria.Car) &&
               MatchesSelection(item.Track, criteria.Track) &&
               MatchesSelection(item.Week, criteria.Week) &&
               MatchesSelection(item.Status, criteria.Status) &&
               MatchesSearch(item, criteria.Search);
    }

    public static IReadOnlyList<string> Options(IEnumerable<string?> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    private static bool MatchesSearch(SetupListItem item, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        return new[]
        {
            item.FileName, item.Provider, item.Category, item.Car, item.Track,
            item.TrackConfiguration, item.Season, item.Week, item.SetupType, item.Status
        }.Any(value => value?.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) == true);
    }

    private static bool MatchesSelection(string value, string? selected) =>
        string.IsNullOrWhiteSpace(selected) || value.Equals(selected, StringComparison.OrdinalIgnoreCase);
}
