namespace IracingSetupManager.Core.Catalog;

public sealed record ProviderDefinition(string Name, string TeamFolderCode);

public sealed record CarDefinition(string Category, string DisplayName, string IracingFolder);

public static class SetupCatalog
{
    private static readonly IReadOnlyDictionary<string, string> ProviderAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hymo"] = "HYMO",
            ["go"] = "GO Setups",
            ["gosetup"] = "GO Setups",
            ["gosetups"] = "GO Setups",
            ["gng"] = "Grid & Go",
            ["gridgo"] = "Grid & Go",
            ["gridandgo"] = "Grid & Go",
            ["vrs"] = "VRS",
            ["virtualracingschool"] = "VRS",
            ["srs"] = "SRS",
            ["p1doks"] = "P1Doks",
            ["cda"] = "Coach Dave Academy (CDA)",
            ["coachdaveacademy"] = "Coach Dave Academy (CDA)",
            ["coachdaveacademycda"] = "Coach Dave Academy (CDA)"
        };

    private static readonly IReadOnlyDictionary<string, string> OfficialTrackNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lemans"] = "Circuit des 24 Heures du Mans",
            ["circuitdes24heuresdumans"] = "Circuit des 24 Heures du Mans",
            ["fuji"] = "Fuji International Speedway",
            ["fujiinternationalspeedway"] = "Fuji International Speedway",
            ["monza"] = "Autodromo Nazionale Monza",
            ["autodromonazionalemonza"] = "Autodromo Nazionale Monza",
            ["roadamerica"] = "Road America",
            ["roadatlanta"] = "Michelin Raceway Road Atlanta",
            ["michelinracewayroadatlanta"] = "Michelin Raceway Road Atlanta",
            ["redbullring"] = "Red Bull Ring",
            ["detroitbelleisle"] = "Raceway at Belle Isle Park",
            ["racewayatbelleislepark"] = "Raceway at Belle Isle Park",
            ["doningtonpark"] = "Donington Park Racing Circuit",
            ["doningtonparkracingcircuit"] = "Donington Park Racing Circuit",
            ["thruxtoncircuit"] = "Thruxton Circuit",
            ["zandvoort"] = "Circuit Zandvoort",
            ["circuitzandvoort"] = "Circuit Zandvoort",
            ["suzuka"] = "Suzuka International Racing Course",
            ["suzukainternationalracingcourse"] = "Suzuka International Racing Course",
            ["nürburgring"] = "Nürburgring Grand-Prix-Strecke",
            ["nurburgring"] = "Nürburgring Grand-Prix-Strecke",
            ["nürburgringgrandprixstrecke"] = "Nürburgring Grand-Prix-Strecke",
            ["nurburgringgrandprixstrecke"] = "Nürburgring Grand-Prix-Strecke",
            ["nürburgringcombined"] = "Nürburgring Combined",
            ["nurburgringcombined"] = "Nürburgring Combined",
            ["nürburgringnordschleife"] = "Nürburgring Nordschleife",
            ["nurburgringnordschleife"] = "Nürburgring Nordschleife",
            ["circuitoftheamericas"] = "Circuit of the Americas",
            ["interlagos"] = "Autódromo José Carlos Pace",
            ["autódromojosécarlospace"] = "Autódromo José Carlos Pace",
            ["montreal"] = "Circuit Gilles-Villeneuve",
            ["circuitgillesvilleneuve"] = "Circuit Gilles-Villeneuve",
            ["longbeach"] = "Long Beach Street Circuit",
            ["longbeachstreetcircuit"] = "Long Beach Street Circuit",
            ["brandshatch"] = "Brands Hatch Circuit",
            ["brandshatchcircuit"] = "Brands Hatch Circuit",
            ["phillipisland"] = "Phillip Island Grand Prix Circuit",
            ["phillipislandgrandprixcircuit"] = "Phillip Island Grand Prix Circuit",
            ["midohio"] = "Mid-Ohio Sports Car Course",
            ["midohiosportscarcourse"] = "Mid-Ohio Sports Car Course",
            ["sonomaraceway"] = "Sonoma Raceway",
            ["indianapolis"] = "Indianapolis Motor Speedway",
            ["indianapolismotorspeedway"] = "Indianapolis Motor Speedway",
            ["hungaroring"] = "Hungaroring",
            ["jerez"] = "Circuito de Jerez - Ángel Nieto",
            ["circuitodejerezángelnieto"] = "Circuito de Jerez - Ángel Nieto",
            ["motorlandaragón"] = "MotorLand Aragón",
            ["mugello"] = "Autodromo Internazionale del Mugello",
            ["autodromointernazionaledelmugello"] = "Autodromo Internazionale del Mugello",
            ["algarveinternationalcircuit"] = "Algarve International Circuit",
            ["zolder"] = "Circuit Zolder",
            ["circuitzolder"] = "Circuit Zolder",
            ["knockhill"] = "Knockhill Racing Circuit",
            ["knockhillracingcircuit"] = "Knockhill Racing Circuit",
            ["navarra"] = "Circuito de Navarra",
            ["circuitodenavarra"] = "Circuito de Navarra",
            ["sachsenring"] = "Sachsenring",
            ["willowsprings"] = "Willow Springs International Raceway",
            ["willowspringsinternationalraceway"] = "Willow Springs International Raceway",
            ["winton"] = "Winton Motor Raceway",
            ["wintonmotorraceway"] = "Winton Motor Raceway",
            ["sandown"] = "Sandown International Motor Raceway",
            ["sandowninternationalmotorraceway"] = "Sandown International Motor Raceway",
            ["barbermotorsportspark"] = "Barber Motorsports Park",
            ["circuitdebarcelonacatalunya"] = "Circuit de Barcelona-Catalunya",
            ["mountpanoramacircuit"] = "Mount Panorama Circuit",
            ["cadwellpark"] = "Cadwell Park Circuit",
            ["cadwellparkcircuit"] = "Cadwell Park Circuit",
            ["chicagostreetcourse"] = "Chicago Street Course",
            ["daytonainternationalspeedway"] = "Daytona International Speedway",
            ["hockenheimring"] = "Hockenheimring Baden-Württemberg",
            ["hockenheimringbadenwürttemberg"] = "Hockenheimring Baden-Württemberg",
            ["autodromointernazionaleenzoedinoferrari"] = "Autodromo Internazionale Enzo e Dino Ferrari",
            ["weathertechracewaylagunaseca"] = "WeatherTech Raceway Laguna Seca",
            ["circuitdelédenon"] = "Circuit de Lédenon",
            ["limerockpark"] = "Lime Rock Park",
            ["circuitdeneversmagnycours"] = "Circuit de Nevers Magny-Cours",
            ["miamiinternationalautodrome"] = "Miami International Autodrome",
            ["misanoworldcircuitmarcosimoncelli"] = "Misano World Circuit Marco Simoncelli",
            ["mobilityresortmotegi"] = "Mobility Resort Motegi",
            ["okayamainternationalcircuit"] = "Okayama International Circuit",
            ["oranparkraceway"] = "Oran Park Raceway",
            ["motorsportarenaoschersleben"] = "Motorsport Arena Oschersleben",
            ["oultonparkcircuit"] = "Oulton Park Circuit",
            ["rudskogenmotorsenter"] = "Rudskogen Motorsenter",
            ["sebringinternationalraceway"] = "Sebring International Raceway",
            ["silverstonecircuit"] = "Silverstone Circuit",
            ["snettertoncircuit"] = "Snetterton Circuit",
            ["spafrancorchamps"] = "Circuit de Spa-Francorchamps",
            ["circuitdespafrancorchamps"] = "Circuit de Spa-Francorchamps",
            ["summitpointmotorsportspark"] = "Summit Point Motorsports Park",
            ["tsukubacircuit"] = "Tsukuba Circuit",
            ["virginiainternationalraceway"] = "Virginia International Raceway",
            ["zhejianginternationalcircuit"] = "Zhejiang International Circuit",
            ["watkinsglen"] = "Watkins Glen International",
            ["watkinsgleninternational"] = "Watkins Glen International",
            ["mexique"] = "Autódromo Hermanos Rodríguez",
            ["autódromohermanosrodríguez"] = "Autódromo Hermanos Rodríguez",
            ["saintpétersbourg"] = "St. Petersburg Street Circuit",
            ["stpetersburgstreetcircuit"] = "St. Petersburg Street Circuit",
            ["adelaide"] = "Adelaide Street Circuit",
            ["adelaidestreetcircuit"] = "Adelaide Street Circuit",
            ["canadiantiremotorsportpark"] = "Canadian Tire Motorsport Park"
        };

    public static IReadOnlyList<ProviderDefinition> Providers { get; } =
    [
        new("HYMO", "HYMO"),
        new("GO Setups", "GO"),
        new("Grid & Go", "GNG"),
        new("VRS", "VRS"),
        new("SRS", "SRS"),
        new("P1Doks", "P1Doks"),
        new("Coach Dave Academy (CDA)", "CDA")
    ];

    public static IReadOnlyList<string> Categories { get; } =
        ["GT3", "GT4", "GTE", "LMP2", "LMP3", "GTP", "PCUP"];

    public static IReadOnlyList<CarDefinition> Cars { get; } =
    [
        new("GT3", "Acura NSX GT3 EVO 22", "acuransxevo22gt3"),
        new("GT3", "Aston Martin Vantage GT3 EVO", "amvantageevogt3"),
        new("GT3", "Audi R8 LMS EVO II GT3", "audir8lmsevo2gt3"),
        new("GT3", "BMW M4 GT3", "bmwm4gt3"),
        new("GT3", "Chevrolet Corvette Z06 GT3.R", "chevyvettez06rgt3"),
        new("GT3", "Ferrari 296 GT3", "ferrari296gt3"),
        new("GT3", "Ford Mustang GT3", "fordmustanggt3"),
        new("GT3", "Lamborghini Huracán GT3 EVO", "lamborghinievogt3"),
        new("GT3", "McLaren 720S GT3 EVO", "mclaren720sgt3"),
        new("GT3", "Mercedes-AMG GT3 2020", "mercedesamgevogt3"),
        new("GT3", "Porsche 911 GT3 R (992)", "porsche992rgt3"),
        new("GT4", "Aston Martin Vantage GT4", "amvantagegt4"),
        new("GT4", "BMW M4 G82 GT4", "bmwm4evogt4"),
        new("GT4", "Ford Mustang GT4", "fordmustanggt4"),
        new("GT4", "McLaren 570S GT4", "mclaren570sgt4"),
        new("GT4", "Mercedes-AMG GT4", "mercedesamggt4"),
        new("GT4", "Porsche 718 Cayman GT4 Clubsport MR", "porsche718gt4"),
        new("GTE", "BMW M8 GTE", "bmwm8gte"),
        new("GTE", "Chevrolet Corvette C8.R GTE", "c8rvettegte"),
        new("GTE", "Ferrari 488 GTE", "ferrari488gte"),
        new("GTE", "Ford GTE", "fordgt2017"),
        new("GTE", "Porsche 911 RSR", "porsche991rsr"),
        new("GTP", "Acura ARX-06 GTP", "acuraarx06gtp"),
        new("GTP", "Aston Martin Valkyrie AMR-LMH", "amvalkyriegtp"),
        new("GTP", "BMW M Hybrid V8", "bmwlmdh"),
        new("GTP", "Cadillac V-Series.R GTP", "cadillacvseriesgtp"),
        new("GTP", "Ferrari 499P", "ferrari499p"),
        new("GTP", "Porsche 963 GTP", "porsche963gtp"),
        new("LMP2", "Dallara P217", "dallarap217"),
        new("LMP3", "Ligier JS P320", "ligierjsp320"),
        new("PCUP", "Porsche 911 Cup (992.2)", "porsche9922cup")
    ];

    public static IReadOnlyList<string> ProviderNames { get; } = Providers.Select(item => item.Name).ToArray();

    public static string CanonicalizeProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return provider;
        var normalized = NormalizeFolderAlias(provider);
        return ProviderAliases.GetValueOrDefault(normalized, provider.Trim());
    }

    public static string CanonicalizeTrack(string track)
    {
        if (string.IsNullOrWhiteSpace(track)) return track;
        var normalized = NormalizeFolderAlias(track);
        return OfficialTrackNames.GetValueOrDefault(normalized, track.Trim());
    }

    public static string GetTeamFolderCode(string provider) =>
        Providers.FirstOrDefault(item => item.Name.Equals(CanonicalizeProvider(provider), StringComparison.OrdinalIgnoreCase))?.TeamFolderCode
        ?? provider;

    private static string NormalizeFolderAlias(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
