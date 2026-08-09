using System.Text.RegularExpressions;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed partial class SetupMetadataAnalyzer
{
    private const string Unknown = "À identifier";

    private static readonly string[] Categories = ["GT4", "GT3", "GTE", "LMP2", "GTP", "PCUP"];
    private static readonly string[] SetupTypes =
        ["Endurance", "Aggressive", "Qualifying", "Quali", "Sprint", "Race", "Wet", "Safe"];
    private static readonly IReadOnlyDictionary<string, (string Car, string Category)> Cars =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["720SGT3"] = ("McLaren 720S GT3", "GT3"),
            ["M4GT3"] = ("BMW M4 GT3", "GT3"),
            ["ARX06"] = ("Acura ARX-06", "GTP"),
            ["BMWGTP"] = ("BMW M Hybrid V8", "GTP")
        };
    private static readonly IReadOnlyDictionary<string, string> Tracks =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LeMans"] = "Le Mans",
            ["Fuji"] = "Fuji",
            ["Monza"] = "Monza"
        };

    public SetupMetadata Analyze(string filePath, SetupMetadata? defaults = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var tokens = Tokenize(filePath);

        var provider = FindProvider(tokens) ?? defaults?.Provider ?? Unknown;
        var carMatch = tokens.Select(token => Cars.GetValueOrDefault(token)).FirstOrDefault(match => match.Car is not null);
        var category = carMatch.Category ?? FindKnown(tokens, Categories) ?? defaults?.Category ?? Unknown;
        var setupType = FindSetupType(tokens) ?? defaults?.SetupType ?? Unknown;
        var car = carMatch.Car ?? EmptyAsNull(defaults?.Car) ?? Unknown;
        var track = tokens.Select(token => Tracks.GetValueOrDefault(token)).FirstOrDefault(value => value is not null)
            ?? EmptyAsNull(defaults?.Track) ?? Unknown;
        var seasonMatch = tokens.Select(token => SeasonRegex().Match(token))
            .FirstOrDefault(match => match.Success);
        var season = seasonMatch is not null
            ? $"{NormalizeYear(seasonMatch.Groups["year"].Value)} S{seasonMatch.Groups["season"].Value}"
            : defaults?.Season;

        return new SetupMetadata(
            provider,
            category,
            car,
            track,
            defaults?.TrackConfiguration,
            season,
            setupType.Equals("Quali", StringComparison.OrdinalIgnoreCase) ? "Qualifying" : setupType);
    }

    private static IReadOnlyList<string> Tokenize(string path) =>
        path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '_', '-', '.', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? FindProvider(IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            if (token.Contains("HYMO", StringComparison.OrdinalIgnoreCase))
            {
                return "HYMO";
            }

            if (token.Equals("GO", StringComparison.OrdinalIgnoreCase) ||
                token.Contains("GOSETUPS", StringComparison.OrdinalIgnoreCase))
            {
                return "GO Setups";
            }

            if (token.Equals("GNG", StringComparison.OrdinalIgnoreCase) ||
                token.Contains("GRIDANDGO", StringComparison.OrdinalIgnoreCase))
            {
                return "Grid & Go";
            }

            if (token.Equals("VRS", StringComparison.OrdinalIgnoreCase) ||
                token.Contains("VIRTUALRACINGSCHOOL", StringComparison.OrdinalIgnoreCase))
            {
                return "VRS";
            }
        }

        return null;
    }

    private static string? FindKnown(IEnumerable<string> tokens, IEnumerable<string> knownValues) =>
        knownValues.FirstOrDefault(known =>
            tokens.Any(token => token.Equals(known, StringComparison.OrdinalIgnoreCase)));

    private static string? FindSetupType(IReadOnlyList<string> tokens)
    {
        if (tokens.Any(token => token.Equals("WR", StringComparison.OrdinalIgnoreCase)))
            return "Wet Race";

        var isRace = tokens.Any(token => RaceRegex().IsMatch(token));
        if (isRace && tokens.Any(token => token.Equals("Safe", StringComparison.OrdinalIgnoreCase)))
            return "Race Safe";

        var version = tokens.Select(token => VersionRegex().Match(token)).FirstOrDefault(match => match.Success);
        if (isRace && version is not null)
            return $"Race V{version.Groups["version"].Value}";

        if (isRace)
            return "Race";

        var known = FindKnown(tokens, SetupTypes);
        return known?.Equals("Quali", StringComparison.OrdinalIgnoreCase) == true ? "Qualifying" : known;
    }

    private static string? EmptyAsNull(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals(Unknown, StringComparison.OrdinalIgnoreCase) ? null : value;

    private static string NormalizeYear(string year) => year.Length == 2 ? $"20{year}" : year;

    [GeneratedRegex(@"(?<year>(?:20)?\d{2})[ _-]*S(?<season>[1-4])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonRegex();

    [GeneratedRegex(@"^R\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RaceRegex();

    [GeneratedRegex(@"^V(?<version>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();
}
