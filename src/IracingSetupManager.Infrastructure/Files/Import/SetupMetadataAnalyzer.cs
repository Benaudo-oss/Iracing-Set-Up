using System.Text.RegularExpressions;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed partial class SetupMetadataAnalyzer(TrackCatalogService? trackCatalog = null)
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
            ["corvettez06gt3"] = ("Chevrolet Corvette Z06 GT3.R", "GT3"),
            ["corvettez06rgt3"] = ("Chevrolet Corvette Z06 GT3.R", "GT3"),
            ["z06gt3"] = ("Chevrolet Corvette Z06 GT3.R", "GT3"),
            ["z06rgt3"] = ("Chevrolet Corvette Z06 GT3.R", "GT3"),
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
            ["M8"] = ("BMW M8 GTE", "GTE"),
            ["c8rvettegte"] = ("Chevrolet Corvette C8.R GTE", "GTE"),
            ["corvettec8r"] = ("Chevrolet Corvette C8.R GTE", "GTE"),
            ["corvettec8rgte"] = ("Chevrolet Corvette C8.R GTE", "GTE"),
            ["c8rgte"] = ("Chevrolet Corvette C8.R GTE", "GTE"),
            ["c8r"] = ("Chevrolet Corvette C8.R GTE", "GTE"),
            ["c6r"] = ("Chevrolet Corvette C6.R", "GT1"),
            ["corvettec6r"] = ("Chevrolet Corvette C6.R", "GT1"),
            ["c7vettedp"] = ("Chevrolet Corvette C7 Daytona Prototype", "DP"),
            ["corvettec7dp"] = ("Chevrolet Corvette C7 Daytona Prototype", "DP"),
            ["ferrari488gte"] = ("Ferrari 488 GTE", "GTE"),
            ["fordgt2017"] = ("Ford GT GTE", "GTE"),
            ["porsche991rsr"] = ("Porsche 911 RSR", "GTE"),

            ["dallarap217"] = ("Dallara P217", "LMP2"),
            ["hpdarx01c"] = ("HPD ARX-01c", "LMP2"),

            ["acuraarx06gtp"] = ("Acura ARX-06", "GTP"),
            ["bmwlmdh"] = ("BMW M Hybrid V8", "GTP"),
            ["cadillacvseriesrgtp"] = ("Cadillac V-Series.R", "GTP"),
            ["Caddy"] = ("Cadillac V-Series.R", "GTP"),
            ["ferrari499p"] = ("Ferrari 499P", "GTP"),
            ["nissangtpzxt"] = ("Nissan GTP ZX-T", "GTP"),
            ["porsche963gtp"] = ("Porsche 963", "GTP"),
            ["ARX06"] = ("Acura ARX-06", "GTP"),
            ["BMWGTP"] = ("BMW M Hybrid V8", "GTP"),

            ["porsche911cup"] = ("Porsche 911 GT3 Cup", "PCUP"),
            ["porsche992cup"] = ("Porsche 911 GT3 Cup (992)", "PCUP"),
            ["992Cup"] = ("Porsche 911 GT3 Cup (992)", "PCUP"),
            ["porsche9922cup"] = ("Porsche 911 GT3 Cup (992) Gen 2", "PCUP"),
            ["992.2Cup"] = ("Porsche 911 GT3 Cup (992) Gen 2", "PCUP"),
            ["9922Cup"] = ("Porsche 911 GT3 Cup (992) Gen 2", "PCUP"),
            ["PC992.2"] = ("Porsche 911 GT3 Cup (992) Gen 2", "PCUP"),
            ["PCUP"] = ("Porsche 911 GT3 Cup (992) Gen 2", "PCUP")
        };

    public static string? ResolveIracingFolderName(string car, IEnumerable<string> availableFolderNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(car);
        ArgumentNullException.ThrowIfNull(availableFolderNames);

        var folders = availableFolderNames.ToList();
        var aliases = Cars
            .Where(item => item.Value.Car.Equals(car, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Key)
            .ToList();
        foreach (var alias in aliases)
        {
            var existing = folders.FirstOrDefault(folder => folder.Equals(alias, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) return existing;
        }

        return aliases.FirstOrDefault(alias => alias.All(character => char.IsLetterOrDigit(character)));
    }

    private static readonly IReadOnlyDictionary<string, string> Tracks =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LeMans"] = "Le Mans",
            ["Fuji"] = "Fuji",
            ["Monza"] = "Monza",
            ["RoAmerica"] = "Road America",
            ["RdAmerica"] = "Road America",
            ["RAmerica"] = "Road America",
            ["RoadAmerica"] = "Road America",
            ["RoAtlanta"] = "Road Atlanta",
            ["RdAtlanta"] = "Road Atlanta",
            ["RAtlanta"] = "Road Atlanta",
            ["RoadAtlanta"] = "Road Atlanta",
            ["RBRing"] = "Red Bull Ring",
            ["RBR"] = "Red Bull Ring",
            ["RedBullRing"] = "Red Bull Ring",
            ["Spielberg"] = "Red Bull Ring",
            ["A1Ring"] = "Red Bull Ring",
            ["Detroit"] = "Detroit Belle Isle",
            ["BelleIsle"] = "Detroit Belle Isle",
            ["Donington"] = "Donington Park",
            ["Donnington"] = "Donington Park",
            ["DoningtonNTL"] = "Donington Park",
            ["DonningtonNTL"] = "Donington Park",
            ["Thruxton"] = "Thruxton Circuit",
            ["Zandvoort"] = "Zandvoort",
            ["Suzuka"] = "Suzuka",
            ["Nurburgring"] = "Nürburgring",
            ["Nuerburgring"] = "Nürburgring",
            ["Nurb"] = "Nürburgring",
            ["Nuerb"] = "Nürburgring",
            ["NurbCombined"] = "Nürburgring Combined",
            ["NuerbCombined"] = "Nürburgring Combined",
            ["NurbConbined"] = "Nürburgring Combined",
            ["NuerbConbined"] = "Nürburgring Combined",
            ["Nordschleife"] = "Nürburgring Nordschleife",
            ["Nords"] = "Nürburgring Nordschleife",
            ["COTA"] = "Circuit of the Americas",
            ["CircuitOfTheAmericas"] = "Circuit of the Americas",
            ["Interlagos"] = "Interlagos",
            ["Montreal"] = "Circuit Gilles-Villeneuve",
            ["GillesVilleneuve"] = "Circuit Gilles-Villeneuve",
            ["LongBeach"] = "Long Beach",
            ["BrandsHatch"] = "Brands Hatch",
            ["PhillipIsland"] = "Phillip Island",
            ["MidOhio"] = "Mid-Ohio",
            ["Sonoma"] = "Sonoma Raceway",
            ["Indianapolis"] = "Indianapolis",
            ["Indy"] = "Indianapolis",
            ["Hungaroring"] = "Hungaroring",
            ["Jerez"] = "Jerez",
            ["Aragon"] = "MotorLand Aragón",
            ["Mugello"] = "Mugello",
            ["Portimao"] = "Algarve International Circuit",
            ["Algarve"] = "Algarve International Circuit",
            ["Zolder"] = "Zolder",
            ["Knockhill"] = "Knockhill",
            ["Navarra"] = "Navarra",
            ["Sachsenring"] = "Sachsenring",
            ["WillowSprings"] = "Willow Springs",
            ["Winton"] = "Winton",
            ["Sandown"] = "Sandown",
            ["Barber"] = "Barber Motorsports Park",
            ["Barcelona"] = "Circuit de Barcelona-Catalunya",
            ["Catalunya"] = "Circuit de Barcelona-Catalunya",
            ["Catalonia"] = "Circuit de Barcelona-Catalunya",
            ["Bathurst"] = "Mount Panorama Circuit",
            ["MountPanorama"] = "Mount Panorama Circuit",
            ["MtPanorama"] = "Mount Panorama Circuit",
            ["Cadwell"] = "Cadwell Park",
            ["CadwellPark"] = "Cadwell Park",
            ["Chicago"] = "Chicago Street Course",
            ["ChicagoStreet"] = "Chicago Street Course",
            ["Daytona"] = "Daytona International Speedway",
            ["Hockenheim"] = "Hockenheimring",
            ["Hockenheimring"] = "Hockenheimring",
            ["Imola"] = "Autodromo Internazionale Enzo e Dino Ferrari",
            ["LagunaSeca"] = "WeatherTech Raceway Laguna Seca",
            ["Laguna"] = "WeatherTech Raceway Laguna Seca",
            ["Ledenon"] = "Circuit de Lédenon",
            ["Ledénon"] = "Circuit de Lédenon",
            ["LimeRock"] = "Lime Rock Park",
            ["MagnyCours"] = "Circuit de Nevers Magny-Cours",
            ["Miami"] = "Miami International Autodrome",
            ["Misano"] = "Misano World Circuit Marco Simoncelli",
            ["Motegi"] = "Mobility Resort Motegi",
            ["TwinRing"] = "Mobility Resort Motegi",
            ["Okayama"] = "Okayama International Circuit",
            ["Oran"] = "Oran Park Raceway",
            ["OranPark"] = "Oran Park Raceway",
            ["Oschersleben"] = "Motorsport Arena Oschersleben",
            ["Oulton"] = "Oulton Park Circuit",
            ["OultonPark"] = "Oulton Park Circuit",
            ["Rudskogen"] = "Rudskogen Motorsenter",
            ["Sebring"] = "Sebring International Raceway",
            ["Silverstone"] = "Silverstone Circuit",
            ["Snetterton"] = "Snetterton Circuit",
            ["Spa"] = "Spa-Francorchamps",
            ["SpaFrancorchamps"] = "Spa-Francorchamps",
            ["SummitPoint"] = "Summit Point Motorsports Park",
            ["Summit"] = "Summit Point Motorsports Park",
            ["Tsukuba"] = "Tsukuba Circuit",
            ["VIR"] = "Virginia International Raceway",
            ["Virginia"] = "Virginia International Raceway",
            ["Willow"] = "Willow Springs",
            ["Zhejiang"] = "Zhejiang International Circuit",
            ["Glen"] = "Watkins Glen",
            ["WatkinsGlen"] = "Watkins Glen",
            ["Mexico"] = "Mexique",
            ["StPete"] = "Saint-Pétersbourg",
            ["Adelaide"] = "Adelaide",
            ["Mosport"] = "Canadian Tire Motorsport Park"
        };

    public SetupMetadata Analyze(string filePath, SetupMetadata? defaults = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var tokens = Tokenize(filePath);

        var provider = FindProvider(tokens) ?? defaults?.Provider ?? Unknown;
        var carMatch = FindCar(filePath, tokens);
        var category = carMatch.Category ?? FindKnown(tokens, Categories) ?? defaults?.Category ?? Unknown;
        var setupType = FindSetupType(tokens) ?? defaults?.SetupType ?? Unknown;
        var car = carMatch.Car ?? EmptyAsNull(defaults?.Car) ?? Unknown;
        var catalogTrack = trackCatalog?.Find(filePath);
        var track = FindTrack(filePath, tokens)
            ?? catalogTrack?.TrackName ?? EmptyAsNull(defaults?.Track) ?? Unknown;
        var seasonMatch = tokens.Select(token => SeasonRegex().Match(token))
            .FirstOrDefault(match => match.Success);
        var season = seasonMatch is not null
            ? $"{NormalizeYear(seasonMatch.Groups["year"].Value)} S{seasonMatch.Groups["season"].Value}"
            : defaults?.Season;

        var trackConfiguration = FindTrackConfiguration(filePath, tokens, track)
            ?? catalogTrack?.Configuration ?? defaults?.TrackConfiguration;

        return new SetupMetadata(
            provider,
            category,
            car,
            track,
            trackConfiguration,
            season,
            setupType.Equals("Quali", StringComparison.OrdinalIgnoreCase) ? "Qualifying" : setupType);
    }

    private static IReadOnlyList<string> Tokenize(string path) =>
        path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '_', '-', '.', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static (string? Car, string? Category) FindCar(string filePath, IReadOnlyList<string> tokens)
    {
        var normalizedName = NormalizeAlias(Path.GetFileNameWithoutExtension(filePath));
        var aliasMatch = Cars
            .Select(item => new { Alias = NormalizeAlias(item.Key), item.Value })
            .Where(item => item.Alias.Length >= 4 && normalizedName.Contains(item.Alias, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Alias.Length)
            .FirstOrDefault();
        if (aliasMatch is not null) return aliasMatch.Value;

        var exact = tokens.Select(token => Cars.GetValueOrDefault(token)).FirstOrDefault(match => match.Car is not null);
        return exact.Car is null ? (null, null) : exact;
    }

    private static string? FindTrack(string filePath, IReadOnlyList<string> tokens)
    {
        var exact = tokens.Select(token => Tracks.GetValueOrDefault(token)).FirstOrDefault(value => value is not null);
        if (exact is not null) return exact;

        var normalizedName = NormalizeAlias(Path.GetFileNameWithoutExtension(filePath));
        var embedded = Tracks
            .Select(item => new { Alias = NormalizeAlias(item.Key), item.Value })
            .Where(item => item.Alias.Length >= 4 && normalizedName.Contains(item.Alias, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Alias.Length)
            .Select(item => item.Value)
            .FirstOrDefault();
        if (embedded is not null) return embedded;

        return tokens
            .Select(token => new { Token = NormalizeAlias(token), Match = FindClosestTrackAlias(token) })
            .Where(item => item.Match is not null)
            .OrderByDescending(item => item.Token.Length)
            .Select(item => item.Match)
            .FirstOrDefault();
    }

    private static string? FindClosestTrackAlias(string token)
    {
        var normalizedToken = NormalizeAlias(token);
        if (normalizedToken.Length < 6) return null;

        return Tracks
            .Select(item => new
            {
                Alias = NormalizeAlias(item.Key),
                item.Value,
                Distance = EditDistance(normalizedToken, NormalizeAlias(item.Key))
            })
            .Where(item => item.Alias.Length >= 6 &&
                           item.Distance <= (Math.Max(item.Alias.Length, normalizedToken.Length) >= 10 ? 2 : 1))
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.Alias.Length)
            .Select(item => item.Value)
            .FirstOrDefault();
    }

    private static int EditDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }
            previous = current;
        }
        return previous[right.Length];
    }

    private static string? FindTrackConfiguration(
        string filePath,
        IReadOnlyList<string> tokens,
        string track)
    {
        if (!track.Equals("Donington Park", StringComparison.OrdinalIgnoreCase)) return null;

        var normalizedName = NormalizeAlias(Path.GetFileNameWithoutExtension(filePath));
        if (tokens.Any(token => token.Equals("NTL", StringComparison.OrdinalIgnoreCase) ||
                                token.Equals("National", StringComparison.OrdinalIgnoreCase)) ||
            normalizedName.Contains("doningtonntl", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Contains("donningtonntl", StringComparison.OrdinalIgnoreCase))
        {
            return "National";
        }

        if (tokens.Any(token => token.Equals("GP", StringComparison.OrdinalIgnoreCase))) return "GP";
        return null;
    }

    private static string NormalizeAlias(string value) =>
        Regex.Replace(value, "[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase).ToLowerInvariant();

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

            if (token.Equals("SRS", StringComparison.OrdinalIgnoreCase))
            {
                return "SRS";
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

    [GeneratedRegex(@"(?<year>(?:20)?\d{2})[ _-]*S(?<season>[1-9]\d*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonRegex();

    [GeneratedRegex(@"^R\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RaceRegex();

    [GeneratedRegex(@"^V(?<version>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();
}
