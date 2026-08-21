using System.Security.Cryptography;
using System.Text;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class FileClassificationAndHashTests
{
    [Fact]
    public void AnalyzerRecognizesVrsVantageGt4CompactAlias()
    {
        var analyzer = new SetupMetadataAnalyzer();

        var metadata = analyzer.Analyze("VRS_26S1_JA_Spa_VantageGT4_R1_Wet.sto");

        Assert.Equal("VRS", metadata.Provider);
        Assert.Equal("GT4", metadata.Category);
        Assert.Equal("Aston Martin Vantage GT4", metadata.Car);
        Assert.Equal("Spa-Francorchamps", metadata.Track);
    }

    [Theory]
    [InlineData("VRS_26S1_Spa_UnknownGT3_R.sto", "GT3")]
    [InlineData("VRS_26S1_Spa_UnknownGT4_R.sto", "GT4")]
    [InlineData("VRS_26S1_Spa_UnknownGTE_R.sto", "GTE")]
    [InlineData("VRS_26S1_Spa_UnknownLMP2_R.sto", "LMP2")]
    [InlineData("VRS_26S1_Spa_UnknownLMP3_R.sto", "LMP3")]
    [InlineData("VRS_26S1_Spa_UnknownGTP_R.sto", "GTP")]
    [InlineData("VRS_26S1_Spa_UnknownPCUP_R.sto", "PCUP")]
    public void AnalyzerRecognizesEveryCompactCategoryMarker(string fileName, string expectedCategory)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(expectedCategory, metadata.Category);
    }

    [Theory]
    [InlineData("VRS_26S1_Spa_ARXGTP_R.sto")]
    [InlineData("26S3-W09-GnG-Daytona-ARXGTP-Endu-Safe.sto")]
    public void AnalyzerRecognizesArxGtpCompactAlias(string fileName)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal("GTP", metadata.Category);
        Assert.Equal("Acura ARX-06 GTP", metadata.Car);
    }

    [Theory]
    [InlineData("VRS_26S1_Spa_CadillacGTP_R.sto", "Cadillac V-Series.R GTP", "GTP")]
    [InlineData("VRS_26S1_Spa_PorscheGTP_R.sto", "Porsche 963 GTP", "GTP")]
    [InlineData("VRS_26S1_Spa_FerrariGTP_R.sto", "Ferrari 499P", "GTP")]
    [InlineData("VRS_26S1_Spa_BMWGT3_R.sto", "BMW M4 GT3", "GT3")]
    [InlineData("VRS_26S1_Spa_AstonGT4_R.sto", "Aston Martin Vantage GT4", "GT4")]
    [InlineData("VRS_26S1_Spa_PorschePCUP_R.sto", "Porsche 911 Cup (992.2)", "PCUP")]
    public void AnalyzerUsesManufacturerAndCategoryRule(string fileName, string expectedCar, string expectedCategory)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(expectedCategory, metadata.Category);
        Assert.Equal(expectedCar, metadata.Car);
    }

    [Theory]
    [InlineData("VRS_26S1_Spa_963GTP_R.sto")]
    [InlineData("26S3-W09-GnG-Daytona-963GTP-Endu-Safe.sto")]
    public void AnalyzerRecognizesPorsche963CompactModel(string fileName)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal("GTP", metadata.Category);
        Assert.Equal("Porsche 963 GTP", metadata.Car);
    }

    public static TheoryData<string, string, string> OfficialIracingCarFolders => new()
    {
        { "GT3", "Acura NSX GT3 EVO 22", "acuransxevo22gt3" },
        { "GT3", "Aston Martin Vantage GT3 EVO", "amvantageevogt3" },
        { "GT3", "Audi R8 LMS EVO II GT3", "audir8lmsevo2gt3" },
        { "GT3", "BMW M4 GT3", "bmwm4gt3" },
        { "GT3", "Chevrolet Corvette Z06 GT3.R", "chevyvettez06rgt3" },
        { "GT3", "Ferrari 296 GT3", "ferrari296gt3" },
        { "GT3", "Ford Mustang GT3", "fordmustanggt3" },
        { "GT3", "Lamborghini Huracán GT3 EVO", "lamborghinievogt3" },
        { "GT3", "McLaren 720S GT3 EVO", "mclaren720sgt3" },
        { "GT3", "Mercedes-AMG GT3 2020", "mercedesamgevogt3" },
        { "GT3", "Porsche 911 GT3 R (992)", "porsche992rgt3" },
        { "GT4", "Aston Martin Vantage GT4", "amvantagegt4" },
        { "GT4", "BMW M4 G82 GT4", "bmwm4evogt4" },
        { "GT4", "Ford Mustang GT4", "fordmustanggt4" },
        { "GT4", "McLaren 570S GT4", "mclaren570sgt4" },
        { "GT4", "Mercedes-AMG GT4", "mercedesamggt4" },
        { "GT4", "Porsche 718 Cayman GT4 Clubsport MR", "porsche718gt4" },
        { "GTE", "BMW M8 GTE", "bmwm8gte" },
        { "GTE", "Chevrolet Corvette C8.R GTE", "c8rvettegte" },
        { "GTE", "Ferrari 488 GTE", "ferrari488gte" },
        { "GTE", "Ford GTE", "fordgt2017" },
        { "GTE", "Porsche 911 RSR", "porsche991rsr" },
        { "GTP", "Acura ARX-06 GTP", "acuraarx06gtp" },
        { "GTP", "BMW M Hybrid V8", "bmwlmdh" },
        { "GTP", "Cadillac V-Series.R GTP", "cadillacvseriesgtp" },
        { "GTP", "Porsche 963 GTP", "porsche963gtp" },
        { "GTP", "Ferrari 499P", "ferrari499p" },
        { "LMP2", "Dallara P217", "dallarap217" },
        { "LMP3", "Ligier JS P320", "ligierjsp320" },
        { "PCUP", "Porsche 911 Cup (992.2)", "porsche9922cup" }
    };

    [Theory]
    [MemberData(nameof(OfficialIracingCarFolders))]
    public void OfficialCarReferenceDrivesDetectionAndIracingFolder(
        string category,
        string car,
        string folder)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(Path.Combine("Provider", folder, "setup.sto"));

        Assert.Equal(car, metadata.Car);
        Assert.Equal(category, metadata.Category);
        Assert.Equal(folder, SetupMetadataAnalyzer.ResolveIracingFolderName(car, []));
    }

    [Fact]
    public void ClassificationUsesSeasonWeekTrackCarAndProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive");
        var metadata = new SetupMetadata("HYMO", "GT3", "Porsche 911 GT3 R", "Spa", "Grand Prix", "2026 S3", "Race", 5);
        var result = new ArchivePathBuilder().BuildDirectory(root, metadata);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "2026_S3", "Week 05", "Spa", "porsche911rgt3", "HYMO"), result);
    }

    [Theory]
    [InlineData("2022 S13", "2022_S13")]
    [InlineData("2027_S5", "2027_S5")]
    public void ClassificationPreservesSeasonSeparator(string season, string expectedFolder)
    {
        var metadata = new SetupMetadata("HYMO", "GT3", "BMW M4 GT3", "Watkins Glen", null, season, "Race");

        var result = new ArchivePathBuilder().BuildDirectory(Path.GetTempPath(), metadata);

        Assert.Contains(Path.DirectorySeparatorChar + expectedFolder + Path.DirectorySeparatorChar, result);
    }

    [Fact]
    public void ClassificationUsesTheIracingFolderNameForPorscheCupGen2()
    {
        var metadata = new SetupMetadata("VRS", "PCUP", "Porsche 911 Cup (992.2)", "Watkins Glen", null, "2026 S3", "Race");

        var result = new ArchivePathBuilder().BuildDirectory(Path.GetTempPath(), metadata);

        Assert.Contains(Path.Combine("Watkins Glen", "porsche9922cup", "VRS"), result);
    }

    [Fact]
    public void ClassificationSanitizesInvalidFolderCharacters()
    {
        var metadata = new SetupMetadata("GO Setups", "GT3", "Ferrari:296", "Spa/GP", null, null, "Qualifying");
        var result = new ArchivePathBuilder().BuildDirectory(Path.GetTempPath(), metadata);
        Assert.DoesNotContain(':', Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(result))!));
        Assert.Contains("Saison inconnue", result);
    }

    [Theory]
    [InlineData("VRS_2026S3_GT3_Race.sto")]
    [InlineData("VirtualRacingSchool-2026S3-GTP-Quali.sto")]
    public void MetadataAnalyzerRecognizesVrs(string fileName)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal("VRS", metadata.Provider);
        Assert.Equal("2026 S3", metadata.Season);
    }

    [Theory]
    [InlineData("HYMO_GTS_25S12_M4GT3_LeMans_ER.sto", "2025 S12")]
    [InlineData("VRS_27S5_M4GT3_LeMans_R.sto", "2027 S5")]
    [InlineData("setup_2030S123.sto", "2030 S123")]
    public void MetadataAnalyzerAcceptsAnyPositiveSeasonNumber(string fileName, string expectedSeason)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(expectedSeason, metadata.Season);
    }

    [Theory]
    [InlineData("26S3-W05-GNG-Porsche-Virginia-Endu.sto", 5)]
    [InlineData("HYMO_26S3_Week 5_Fuji.sto", 5)]
    [InlineData("HYMO_26S3_Week_5_Fuji.sto", 5)]
    [InlineData("HYMO_26S3_Week-05_Fuji.sto", 5)]
    [InlineData("HYMO_26S3_W05_Fuji.sto", 5)]
    [InlineData(@"C:\Track Titan\Week_9\HYMO_IMSA_26S3_ARX06_Fuji_WR.sto", 9)]
    [InlineData(@"C:\Track Titan\Week-13\HYMO_IMSA_26S3_ARX06_Fuji_WR.sto", 13)]
    public void MetadataAnalyzerReadsWeekFromNameOrFolder(string path, int expectedWeek)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(path);

        Assert.Equal(expectedWeek, metadata.Week);
        Assert.Equal(SetupWeekKind.Numeric, metadata.EffectiveWeekKind);
    }

    [Theory]
    [InlineData("HYMO_26S3_Week NEC_Fuji.sto")]
    [InlineData(@"C:\Track Titan\2026\Season 3\Week_NEC\Fuji\setup.sto")]
    public void MetadataAnalyzerRecognizesWeekNec(string path)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(path);

        Assert.Null(metadata.Week);
        Assert.Equal(SetupWeekKind.Nec, metadata.EffectiveWeekKind);
        Assert.Equal("Week NEC", SetupWeekPresentation.Display(metadata.Week, metadata.EffectiveWeekKind));
    }

    [Fact]
    public void WeekPresentationDistinguishesEveryState()
    {
        Assert.Equal("Week 05", SetupWeekPresentation.Display(5, SetupWeekKind.Unknown));
        Assert.Equal("Week NEC", SetupWeekPresentation.Display(null, SetupWeekKind.Nec));
        Assert.Equal("Week inconnue", SetupWeekPresentation.Display(null, SetupWeekKind.Unknown));
        Assert.Equal("Sans Week", SetupWeekPresentation.Display(null, SetupWeekKind.NoWeek));
    }

    [Theory]
    [InlineData(@"C:\iRacing\setups\amvantageevogt3\Track Titan\2026\Season 3\Week 5\Le Mans\race.sto", "2026 S3")]
    [InlineData(@"C:\iRacing\setups\amvantageevogt3\Track Titan\2025\Season_12\Week NEC\Spa\race.sto", "2025 S12")]
    [InlineData(@"C:\iRacing\setups\amvantageevogt3\Track Titan\2027S5\Week 2\Fuji\race.sto", "2027 S5")]
    public void MetadataAnalyzerReadsTrackTitanSeasonFromFolders(string path, string expectedSeason)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(path);

        Assert.Equal(expectedSeason, metadata.Season);
    }

    [Fact]
    public void TrackTitanFolderSeasonTakesPriorityOverFileName()
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(
            @"C:\iRacing\setups\amvantageevogt3\Track Titan\2026\Season 3\Week 5\Le Mans\HYMO_25S2_race.sto");

        Assert.Equal("2026 S3", metadata.Season);
    }

    [Fact]
    public void MetadataAnalyzerReadsCompleteTrackTitanHierarchyBeforeFileName()
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(
            @"C:\iRacing\setups\amvantageevogt3\Track Titan\2026\Season 3\Week 5\Le Mans\HYMO_25S2_W02_Spa_race.sto");

        Assert.Equal("2026 S3", metadata.Season);
        Assert.Equal(5, metadata.Week);
        Assert.Equal("Le Mans", metadata.Track);
    }

    [Fact]
    public void MetadataAnalyzerRecognizesLocalizedTrackTitanTrackAndConfiguration()
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(
            @"C:\Users\Benaudo\Documents\iRacing\setups\amvantageevogt3\Track Titan\2026\Season 2\Week 11\Circuit des 24 Heures du Mans - 24 Heures du Mans\setup.sto");

        Assert.Equal("2026 S2", metadata.Season);
        Assert.Equal(11, metadata.Week);
        Assert.Equal("Le Mans", metadata.Track);
        Assert.Equal("24 Heures du Mans", metadata.TrackConfiguration);
    }

    [Fact]
    public void ClassificationUsesUnknownWeekFolderWhenWeekIsMissing()
    {
        var metadata = new SetupMetadata("VRS", "GT3", "BMW M4 GT3", "Le Mans", null, "2026 S3", "Race");

        var result = new ArchivePathBuilder().BuildDirectory(Path.GetTempPath(), metadata);

        Assert.Contains(Path.Combine("2026_S3", "Week inconnue", "Le Mans"), result);
    }

    [Theory]
    [InlineData(5, SetupWeekKind.Numeric, "Week 05")]
    [InlineData(null, SetupWeekKind.Nec, "Week NEC")]
    [InlineData(null, SetupWeekKind.NoWeek, "Sans Week")]
    [InlineData(null, SetupWeekKind.Unknown, "Week inconnue")]
    public void ArchiveUsesExactWeekFolderNames(int? week, SetupWeekKind kind, string expectedFolder)
    {
        var metadata = new SetupMetadata(
            "VRS", "GT3", "BMW M4 GT3", "Le Mans", null, "2026 S3", "Race", week, kind);

        var result = new ArchivePathBuilder().BuildDirectory(Path.GetTempPath(), metadata);

        Assert.Contains(Path.Combine("2026_S3", expectedFolder, "Le Mans"), result);
    }

    [Theory]
    [InlineData("GO_26S3_GTS_720SGT3_LeMans_R_Safe.sto", "GO Setups", "McLaren 720S GT3 EVO", "GT3", "Le Mans", "Race Safe")]
    [InlineData("VRS_26S3PG_M4GT3_LeMans_R1_V2.sto", "VRS", "BMW M4 GT3", "GT3", "Le Mans", "Race V2")]
    [InlineData("HYMO_IMSA_26S3_ARX06_Fuji_WR.sto", "HYMO", "Acura ARX-06 GTP", "GTP", "Fuji", "Wet Race")]
    [InlineData("HYMO_IMSA_26S3_ARX_Fuji_R.sto", "HYMO", "Acura ARX-06 GTP", "GTP", "Fuji", "Race")]
    [InlineData("HYMO_IMSA_26S3_NSX_Fuji_WR.sto", "HYMO", "Acura NSX GT3 EVO 22", "GT3", "Fuji", "Wet Race")]
    [InlineData("26S3-W07-GnG-Monza-BMWGTP-R-Safe.sto", "Grid & Go", "BMW M Hybrid V8", "GTP", "Monza", "Race Safe")]
    [InlineData("SRS_26S3_M8_Mosport_R.sto", "SRS", "BMW M8 GTE", "GTE", "Canadian Tire Motorsport Park", "Race")]
    [InlineData("SRS_26S3_Caddy_Mosport_R.sto", "SRS", "Cadillac V-Series.R GTP", "GTP", "Canadian Tire Motorsport Park", "Race")]
    [InlineData("P1Doks_26S3_M4GT3_LeMans_R.sto", "P1Doks", "BMW M4 GT3", "GT3", "Le Mans", "Race")]
    [InlineData("CDA_26S3_M4GT3_LeMans_R.sto", "Coach Dave Academy (CDA)", "BMW M4 GT3", "GT3", "Le Mans", "Race")]
    public void MetadataAnalyzerUnderstandsKnownProviderNamingConventions(
        string fileName,
        string provider,
        string car,
        string category,
        string track,
        string setupType)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(provider, metadata.Provider);
        Assert.Equal("2026 S3", metadata.Season);
        Assert.Equal(car, metadata.Car);
        Assert.Equal(category, metadata.Category);
        Assert.Equal(track, metadata.Track);
        Assert.Equal(setupType, metadata.SetupType);
    }

    [Theory]
    [InlineData("HYMO_GTEsprint_26S3_RSRGTE_RedbullRing_CEND.sto", "Porsche 911 RSR")]
    [InlineData("HYMO_GTEsprint_26S3_FERRARIGTE_RedbullRing_CQ.sto", "Ferrari 488 GTE")]
    public void MetadataAnalyzerRecognizesHymoGteAliases(string fileName, string expectedCar)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal("HYMO", metadata.Provider);
        Assert.Equal("GTE", metadata.Category);
        Assert.Equal("2026 S3", metadata.Season);
        Assert.Equal(expectedCar, metadata.Car);
        Assert.Equal("Red Bull Ring", metadata.Track);
    }

    [Theory]
    [InlineData("26S3-W05-GNG-Acura-Virginia-Endu.sto", "Acura NSX GT3 EVO 22")]
    [InlineData("26S3-W05-GNG-Aston-Virginia-Endu.sto", "Aston Martin Vantage GT3 EVO")]
    [InlineData("26S3-W05-GNG-AstonMartin-Virginia-Endu.sto", "Aston Martin Vantage GT3 EVO")]
    [InlineData("26S3-W05-GNG-Porsche-Virginia-Endu.sto", "Porsche 911 GT3 R (992)")]
    [InlineData("26S3-W05-GNG-Ford-Virginia-Endu.sto", "Ford Mustang GT3")]
    [InlineData("26S3-W05-GNG-Audi-Virginia-Endu.sto", "Audi R8 LMS EVO II GT3")]
    [InlineData("26S3-W05-GNG-BMW-Virginia-Endu.sto", "BMW M4 GT3")]
    [InlineData("26S3-W05-GNG-Chevrolet-Virginia-Endu.sto", "Chevrolet Corvette Z06 GT3.R")]
    [InlineData("26S3-W05-GNG-Corvette-Virginia-Endu.sto", "Chevrolet Corvette Z06 GT3.R")]
    [InlineData("26S3-W05-GNG-Ferrari-Virginia-Endu.sto", "Ferrari 296 GT3")]
    [InlineData("26S3-W05-GNG-Lamborghini-Virginia-Endu.sto", "Lamborghini Huracán GT3 EVO")]
    [InlineData("26S3-W05-GNG-Lambo-Virginia-Endu.sto", "Lamborghini Huracán GT3 EVO")]
    [InlineData("26S3-W05-GNG-McLaren-Virginia-Endu.sto", "McLaren 720S GT3 EVO")]
    [InlineData("26S3-W05-GNG-Mercedes-Virginia-Endu.sto", "Mercedes-AMG GT3 2020")]
    [InlineData("26S3-W05-GNG-MercedesAMG-Virginia-Endu.sto", "Mercedes-AMG GT3 2020")]
    public void MetadataAnalyzerTreatsBareGngBrandsAsGt3(string fileName, string expectedCar)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal("Grid & Go", metadata.Provider);
        Assert.Equal(expectedCar, metadata.Car);
        Assert.Equal("GT3", metadata.Category);
    }

    [Theory]
    [InlineData("26S3-W05-GNG-Porsche963GTP-Virginia-Endu.sto", "Porsche 963 GTP", "GTP")]
    [InlineData("26S3-W05-GNG-FordGT4-Virginia-Endu.sto", "Ford Mustang GT4", "GT4")]
    [InlineData("26S3-W05-GNG-BMWGTP-Virginia-Endu.sto", "BMW M Hybrid V8", "GTP")]
    [InlineData("26S3-W05-GNG-Ferrari499P-Virginia-Endu.sto", "Ferrari 499P", "GTP")]
    [InlineData("26S3-W05-GNG-CorvetteC8R-Virginia-Endu.sto", "Chevrolet Corvette C8.R GTE", "GTE")]
    public void MetadataAnalyzerKeepsExplicitGngModelsAheadOfBareBrands(
        string fileName,
        string expectedCar,
        string expectedCategory)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(expectedCar, metadata.Car);
        Assert.Equal(expectedCategory, metadata.Category);
    }

    [Fact]
    public void MetadataAnalyzerDoesNotApplyBareBrandRuleOutsideGng()
    {
        var metadata = new SetupMetadataAnalyzer().Analyze("VRS_26S3_Porsche_Virginia_Endu.sto");

        Assert.Equal("À identifier", metadata.Car);
        Assert.Equal("À identifier", metadata.Category);
    }

    [Theory]
    [InlineData(@"C:\Provider\bmwm4gt3\setup.sto", "BMW M4 GT3", "GT3")]
    [InlineData(@"C:\Provider\porsche718gt4\setup.sto", "Porsche 718 Cayman GT4 Clubsport MR", "GT4")]
    [InlineData(@"C:\Provider\bmwm8gte\setup.sto", "BMW M8 GTE", "GTE")]
    [InlineData("VRS_26S3_M8_LeMans_R.sto", "BMW M8 GTE", "GTE")]
    [InlineData(@"C:\Provider\dallarap217\setup.sto", "Dallara P217", "LMP2")]
    [InlineData(@"C:\Provider\ligierjsp320\setup.sto", "Ligier JS P320", "LMP3")]
    [InlineData(@"C:\Provider\ferrari499p\setup.sto", "Ferrari 499P", "GTP")]
    [InlineData(@"C:\Provider\porsche9922cup\setup.sto", "Porsche 911 Cup (992.2)", "PCUP")]
    public void MetadataAnalyzerRecognizesSelectedIracingCarCatalog(string path, string car, string category)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(path);

        Assert.Equal(car, metadata.Car);
        Assert.Equal(category, metadata.Category);
    }

    [Theory]
    [InlineData("26S1-W02-GnG-PCUP-Spa-Q-safe.sto", "Porsche 911 Cup (992.2)")]
    [InlineData("GO 26S1 992.2Cup Adelaide E Q.sto", "Porsche 911 Cup (992.2)")]
    [InlineData("GO 26S1 9922Cup Sebring E R.sto", "Porsche 911 Cup (992.2)")]
    [InlineData("VRS_26S1MS_PC992.2_Sebring_Q1.sto", "Porsche 911 Cup (992.2)")]
    [InlineData("VRS_26S1JF_992Cup_Miami_Q.sto", "Porsche 911 GT3 Cup (992)")]
    public void MetadataAnalyzerRecognizesPorscheCupProviderAliases(string fileName, string expectedCar)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(expectedCar, metadata.Car);
        Assert.Equal("PCUP", metadata.Category);
    }

    [Fact]
    public void MetadataAnalyzerRecognizesGlenAsWatkinsGlen()
    {
        var metadata = new SetupMetadataAnalyzer().Analyze("HYMO_GTS_26S3_M4GT3_Glen_R.sto");

        Assert.Equal("Watkins Glen", metadata.Track);
    }

    [Theory]
    [InlineData("VRS_26S3_M4GT3_Mexico_R.sto", "Mexique")]
    [InlineData("HYMO_26S3_M4GT3_StPete_R.sto", "Saint-Pétersbourg")]
    [InlineData("GO_26S3_992Cup_Adelaide_R.sto", "Adelaide")]
    public void MetadataAnalyzerRecognizesLocalizedTrackAliases(string fileName, string expectedTrack)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(expectedTrack, metadata.Track);
    }

    [Theory]
    [InlineData("VRS_26S3_M4GT3_RoAmerica_R.sto", "Road America")]
    [InlineData("VRS_26S3_M4GT3_roadam_R.sto", "Road America")]
    [InlineData("VRS_26S3_M4GT3_bathrust_R.sto", "Mount Panorama Circuit")]
    [InlineData("VRS_26S3_M4GT3_magny_R.sto", "Circuit de Nevers Magny-Cours")]
    [InlineData("VRS_26S3_M4GT3_watkins_R.sto", "Watkins Glen")]
    [InlineData("HYMO_26S3_M4GT3_RoAtlanta_R.sto", "Road Atlanta")]
    [InlineData("GO_26S3_720SGT3_Detroit_R.sto", "Detroit Belle Isle")]
    [InlineData("VRS_26S3_M4GT3_Thruxton_R.sto", "Thruxton Circuit")]
    [InlineData("VRS_26S3_M4GT3_nuerbconbined_R.sto", "Nürburgring Combined")]
    [InlineData("HYMO_26S3_M4GT3_Zandvoort_R.sto", "Zandvoort")]
    [InlineData("GO_26S3_720SGT3_Suzuka_R.sto", "Suzuka")]
    [InlineData("VRS26S3M4GT3RoAmericaR.sto", "Road America")]
    public void MetadataAnalyzerRecognizesCommonTrackVariations(string fileName, string expectedTrack)
    {
        Assert.Equal(expectedTrack, new SetupMetadataAnalyzer().Analyze(fileName).Track);
    }

    [Theory]
    [InlineData("VRS_26S3_M4GT3_RBRing_R.sto")]
    [InlineData("VRS_26S3_M4GT3_RBR_R.sto")]
    [InlineData("HYMO_26S3_M4GT3_RedBullRing_R.sto")]
    [InlineData("GO_26S3_720SGT3_Spielberg_R.sto")]
    [InlineData("VRS_26S3_M4GT3_A1Ring_R.sto")]
    [InlineData("VRS26S3M4GT3RBRingR.sto")]
    public void MetadataAnalyzerRecognizesRedBullRingVariations(string fileName)
    {
        Assert.Equal("Red Bull Ring", new SetupMetadataAnalyzer().Analyze(fileName).Track);
    }

    [Theory]
    [InlineData("VRS_26S3_M4GT3_Donington_NTL_R.sto")]
    [InlineData("VRS_26S3_M4GT3_Donnington_NTL_R.sto")]
    [InlineData("VRS26S3M4GT3DonningtonNTLR.sto")]
    public void MetadataAnalyzerRecognizesDoningtonNationalVariations(string fileName)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal("Donington Park", metadata.Track);
        Assert.Equal("National", metadata.TrackConfiguration);
    }

    [Theory]
    [InlineData("VRS_26S3_M4GT3_Barcelone_R.sto", "Circuit de Barcelona-Catalunya")]
    [InlineData("VRS_26S3_M4GT3_LagunaSeca_R.sto", "WeatherTech Raceway Laguna Seca")]
    [InlineData("HYMO_26S3_M4GT3_PhillipIslad_R.sto", "Phillip Island")]
    [InlineData("GO_26S3_720SGT3_Silverston_R.sto", "Silverstone Circuit")]
    [InlineData("VRS_26S3_M4GT3_Oscherslebe_R.sto", "Motorsport Arena Oschersleben")]
    [InlineData("HYMO_26S3_M4GT3_MagnyCour_R.sto", "Circuit de Nevers Magny-Cours")]
    public void MetadataAnalyzerToleratesLimitedTrackMisspellings(string fileName, string expectedTrack)
    {
        Assert.Equal(expectedTrack, new SetupMetadataAnalyzer().Analyze(fileName).Track);
    }

    [Fact]
    public void MetadataAnalyzerDoesNotFuzzyMatchUnknownShortWords()
    {
        Assert.Equal("À identifier", new SetupMetadataAnalyzer().Analyze("VRS_26S3_M4GT3_Spi_R.sto").Track);
    }

    [Theory]
    [InlineData("VRS_26S3_Z06GT3_RoAmerica_R.sto", "Chevrolet Corvette Z06 GT3.R", "GT3")]
    [InlineData("HYMO_26S3_C8R_LeMans_R.sto", "Chevrolet Corvette C8.R GTE", "GTE")]
    [InlineData("setup_corvettec6r.sto", "Chevrolet Corvette C6.R", "GT1")]
    [InlineData("setup_c7vettedp.sto", "Chevrolet Corvette C7 Daytona Prototype", "DP")]
    public void MetadataAnalyzerDistinguishesCorvetteModels(string fileName, string expectedCar, string expectedCategory)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(fileName);

        Assert.Equal(expectedCar, metadata.Car);
        Assert.Equal(expectedCategory, metadata.Category);
    }

    [Fact]
    public void MetadataAnalyzerDoesNotGuessAnAmbiguousCorvetteName()
    {
        var metadata = new SetupMetadataAnalyzer().Analyze("VRS_26S3_Corvette_LeMans_R.sto");

        Assert.Equal("À identifier", metadata.Car);
        Assert.Equal("À identifier", metadata.Category);
    }

    [Fact]
    public async Task Sha256MatchesKnownVectorAndChangesWithContent()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "HashTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var first = Path.Combine(root, "first.sto"); var second = Path.Combine(root, "second.sto");
            await File.WriteAllTextAsync(first, "abc", new UTF8Encoding(false));
            await File.WriteAllTextAsync(second, "abcd", new UTF8Encoding(false));
            var calculator = new Sha256Calculator();
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("abc"))),
                await calculator.CalculateAsync(first),
                ignoreCase: true);
            Assert.NotEqual(await calculator.CalculateAsync(first), await calculator.CalculateAsync(second));
        }
        finally { Directory.Delete(root, true); }
    }
}
