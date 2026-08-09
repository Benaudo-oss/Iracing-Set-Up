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
            ["acuransxevo22gt3"] = ("Acura NSX GT3 Evo 22", "GT3"),
            ["NSX"] = ("Acura NSX GT3 Evo 22", "GT3"),
            ["amvantageevogt3"] = ("Aston Martin Vantage GT3 Evo", "GT3"),
            ["audir8gt3"] = ("Audi R8 LMS GT3", "GT3"),
            ["audir8lmsevo2gt3"] = ("Audi R8 LMS Evo II GT3", "GT3"),
            ["bmwm4gt3"] = ("BMW M4 GT3", "GT3"),
            ["bmwz4gt3"] = ("BMW Z4 GT3", "GT3"),
            ["chevyvettez06rgt3"] = ("Chevrolet Corvette Z06 GT3.R", "GT3"),
            ["ferrari296gt3"] = ("Ferrari 296 GT3", "GT3"),
            ["ferrari488gt3"] = ("Ferrari 488 GT3", "GT3"),
            ["ferrarievogt3"] = ("Ferrari 488 GT3 Evo", "GT3"),
            ["fordgtgt3"] = ("Ford GT GT3", "GT3"),
            ["fordmustanggt3"] = ("Ford Mustang GT3", "GT3"),
            ["lamborghinievogt3"] = ("Lamborghini Huracán GT3 Evo", "GT3"),
            ["mclaren720sgt3"] = ("McLaren 720S GT3", "GT3"),
            ["mclarenmp4"] = ("McLaren MP4-12C GT3", "GT3"),
            ["mercedesamgevogt3"] = ("Mercedes-AMG GT3 Evo", "GT3"),
            ["mercedesamggt3"] = ("Mercedes-AMG GT3", "GT3"),
            ["porsche911rgt3"] = ("Porsche 911 GT3 R", "GT3"),
            ["porsche992rgt3"] = ("Porsche 911 GT3 R (992)", "GT3"),
            ["720SGT3"] = ("McLaren 720S GT3", "GT3"),
            ["M4GT3"] = ("BMW M4 GT3", "GT3"),

            ["amvantagegt4"] = ("Aston Martin Vantage GT4", "GT4"),
            ["bmwm4evogt4"] = ("BMW M4 GT4 Evo", "GT4"),
            ["bmwm4gt4"] = ("BMW M4 GT4", "GT4"),
            ["fordmustanggt4"] = ("Ford Mustang GT4", "GT4"),
            ["mclaren570sgt4"] = ("McLaren 570S GT4", "GT4"),
            ["mercedesamggt4"] = ("Mercedes-AMG GT4", "GT4"),
            ["porsche718gt4"] = ("Porsche 718 Cayman GT4 Clubsport", "GT4"),

            ["bmwm8gte"] = ("BMW M8 GTE", "GTE"),
            ["c8rvettegte"] = ("Chevrolet Corvette C8.R GTE", "GTE"),
            ["ferrari488gte"] = ("Ferrari 488 GTE", "GTE"),
            ["fordgt2017"] = ("Ford GT GTE", "GTE"),
            ["porsche991rsr"] = ("Porsche 911 RSR", "GTE"),

            ["dallarap217"] = ("Dallara P217", "LMP2"),
            ["hpdarx01c"] = ("HPD ARX-01c", "LMP2"),

            ["acuraarx06gtp"] = ("Acura ARX-06", "GTP"),
            ["bmwlmdh"] = ("BMW M Hybrid V8", "GTP"),
            ["cadillacvseriesrgtp"] = ("Cadillac V-Series.R", "GTP"),
            ["ferrari499p"] = ("Ferrari 499P", "GTP"),
            ["nissangtpzxt"] = ("Nissan GTP ZX-T", "GTP"),
            ["porsche963gtp"] = ("Porsche 963", "GTP"),
            ["ARX06"] = ("Acura ARX-06", "GTP"),
            ["BMWGTP"] = ("BMW M Hybrid V8", "GTP"),

            ["porsche911cup"] = ("Porsche 911 GT3 Cup", "PCUP"),
            ["porsche992cup"] = ("Porsche 911 GT3 Cup (992)", "PCUP"),
            ["porsche9922cup"] = ("Porsche 911 GT3 Cup (992) Gen 2", "PCUP")
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
