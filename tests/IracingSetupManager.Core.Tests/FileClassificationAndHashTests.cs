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
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "2026 S3", "Spa", "Porsche 911 GT3 R", "HYMO"), result);
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
    [InlineData(@"C:\Provider\dallarap217\setup.sto", "Dallara P217", "LMP2")]
    [InlineData(@"C:\Provider\ferrari499p\setup.sto", "Ferrari 499P", "GTP")]
    [InlineData(@"C:\Provider\porsche9922cup\setup.sto", "Porsche 911 GT3 Cup (992) Gen 2", "PCUP")]
    public void MetadataAnalyzerRecognizesSelectedIracingCarCatalog(string path, string car, string category)
    {
        var metadata = new SetupMetadataAnalyzer().Analyze(path);

        Assert.Equal(car, metadata.Car);
        Assert.Equal(category, metadata.Category);
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
