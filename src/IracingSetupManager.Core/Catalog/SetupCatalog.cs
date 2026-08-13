namespace IracingSetupManager.Core.Catalog;

public sealed record ProviderDefinition(string Name, string TeamFolderCode);

public sealed record CarDefinition(string Category, string DisplayName, string IracingFolder);

public static class SetupCatalog
{
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
        new("GTP", "BMW M Hybrid V8", "bmwlmdh"),
        new("GTP", "Cadillac V-Series.R GTP", "cadillacvseriesgtp"),
        new("GTP", "Ferrari 499P", "ferrari499p"),
        new("GTP", "Porsche 963 GTP", "porsche963gtp"),
        new("LMP2", "Dallara P217", "dallarap217"),
        new("LMP3", "Ligier JS P320", "ligierjsp320"),
        new("PCUP", "Porsche 911 Cup (992.2)", "porsche9922cup")
    ];

    public static IReadOnlyList<string> ProviderNames { get; } = Providers.Select(item => item.Name).ToArray();

    public static string GetTeamFolderCode(string provider) =>
        Providers.FirstOrDefault(item => item.Name.Equals(provider, StringComparison.OrdinalIgnoreCase))?.TeamFolderCode
        ?? provider;
}
