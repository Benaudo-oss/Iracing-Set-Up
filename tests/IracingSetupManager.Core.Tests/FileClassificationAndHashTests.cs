using System.Security.Cryptography;
using System.Text;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class FileClassificationAndHashTests
{
    [Fact]
    public void ClassificationUsesSeasonTrackCarAndProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive");
        var metadata = new SetupMetadata("HYMO", "GT3", "Porsche 911 GT3 R", "Spa", "Grand Prix", "2026 S3", "Race");
        var result = new ArchivePathBuilder().BuildDirectory(root, metadata);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "2026_S3", "Spa", "porsche911rgt3", "HYMO"), result);
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
        var metadata = new SetupMetadata("VRS", "PCUP", "Porsche 911 GT3 Cup (992) Gen 2", "Watkins Glen", null, "2026 S3", "Race");

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
    [InlineData("GO_26S3_GTS_720SGT3_LeMans_R_Safe.sto", "GO Setups", "McLaren 720S GT3", "GT3", "Le Mans", "Race Safe")]
    [InlineData("VRS_26S3PG_M4GT3_LeMans_R1_V2.sto", "VRS", "BMW M4 GT3", "GT3", "Le Mans", "Race V2")]
    [InlineData("HYMO_IMSA_26S3_ARX06_Fuji_WR.sto", "HYMO", "Acura ARX-06", "GTP", "Fuji", "Wet Race")]
    [InlineData("HYMO_IMSA_26S3_NSX_Fuji_WR.sto", "HYMO", "Acura NSX GT3 Evo 22", "GT3", "Fuji", "Wet Race")]
    [InlineData("26S3-W07-GnG-Monza-BMWGTP-R-Safe.sto", "Grid & Go", "BMW M Hybrid V8", "GTP", "Monza", "Race Safe")]
    [InlineData("SRS_26S3_M8_Mosport_R.sto", "SRS", "BMW M8 GTE", "GTE", "Canadian Tire Motorsport Park", "Race")]
    [InlineData("SRS_26S3_Caddy_Mosport_R.sto", "SRS", "Cadillac V-Series.R", "GTP", "Canadian Tire Motorsport Park", "Race")]
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
    [InlineData(@"C:\Provider\bmwm4gt3\setup.sto", "BMW M4 GT3", "GT3")]
    [InlineData(@"C:\Provider\porsche718gt4\setup.sto", "Porsche 718 Cayman GT4 Clubsport", "GT4")]
    [InlineData(@"C:\Provider\bmwm8gte\setup.sto", "BMW M8 GTE", "GTE")]
    [InlineData("VRS_26S3_M8_LeMans_R.sto", "BMW M8 GTE", "GTE")]
    [InlineData(@"C:\Provider\dallarap217\setup.sto", "Dallara P217", "LMP2")]
    [InlineData(@"C:\Provider\ferrari499p\setup.sto", "Ferrari 499P", "GTP")]
    [InlineData(@"C:\Provider\porsche9922cup\setup.sto", "Porsche 911 GT3 Cup (992) Gen 2", "PCUP")]
    public void MetadataAnalyzerRecognizesSelectedIracingCarCatalog(string path, string car, string category)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(path);

        Assert.Equal(car, metadata.Car);
        Assert.Equal(category, metadata.Category);
    }

    [Theory]
    [InlineData("26S1-W02-GnG-PCUP-Spa-Q-safe.sto", "Porsche 911 GT3 Cup (992) Gen 2")]
    [InlineData("GO 26S1 992.2Cup Adelaide E Q.sto", "Porsche 911 GT3 Cup (992) Gen 2")]
    [InlineData("GO 26S1 9922Cup Sebring E R.sto", "Porsche 911 GT3 Cup (992) Gen 2")]
    [InlineData("VRS_26S1MS_PC992.2_Sebring_Q1.sto", "Porsche 911 GT3 Cup (992) Gen 2")]
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
