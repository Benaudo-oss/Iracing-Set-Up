using System.Text.RegularExpressions;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed partial class SetupMetadataAnalyzer
{
    private const string Unknown = "À identifier";

    private static readonly string[] Categories = ["GT4", "GT3", "GTE", "LMP2", "GTP", "PCUP"];
    private static readonly string[] SetupTypes =
        ["Endurance", "Aggressive", "Qualifying", "Quali", "Sprint", "Race", "Wet", "Safe"];

    public SetupMetadata Analyze(string filePath, SetupMetadata? defaults = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var tokens = Tokenize(filePath);

        var provider = FindProvider(tokens) ?? defaults?.Provider ?? Unknown;
        var category = FindKnown(tokens, Categories) ?? defaults?.Category ?? Unknown;
        var setupType = FindKnown(tokens, SetupTypes) ?? defaults?.SetupType ?? Unknown;
        var seasonMatch = tokens.Select(token => SeasonRegex().Match(token))
            .FirstOrDefault(match => match.Success);
        var season = seasonMatch is not null
            ? $"{seasonMatch.Groups["year"].Value} S{seasonMatch.Groups["season"].Value}"
            : defaults?.Season;

        return new SetupMetadata(
            provider,
            category,
            defaults?.Car ?? Unknown,
            defaults?.Track ?? Unknown,
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
        }

        return null;
    }

    private static string? FindKnown(IEnumerable<string> tokens, IEnumerable<string> knownValues) =>
        knownValues.FirstOrDefault(known =>
            tokens.Any(token => token.Equals(known, StringComparison.OrdinalIgnoreCase)));

    [GeneratedRegex(@"(?<year>20\d{2})[ _-]*S(?<season>[1-4])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonRegex();
}
