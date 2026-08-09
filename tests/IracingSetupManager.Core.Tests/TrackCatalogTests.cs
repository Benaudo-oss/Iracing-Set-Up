using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files.Import;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class TrackCatalogTests
{
    [Fact]
    public async Task ImportsImmediateLapfilesFoldersWithoutReadingLapFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "TrackCatalogTests", Guid.NewGuid().ToString("N"));
        var lapfiles = Directory.CreateDirectory(Path.Combine(root, "lapfiles")).FullName;
        Directory.CreateDirectory(Path.Combine(lapfiles, "fuji gp"));
        Directory.CreateDirectory(Path.Combine(lapfiles, "fuji nochicane"));
        Directory.CreateDirectory(Path.Combine(lapfiles, "mexico grandprix"));
        Directory.CreateDirectory(Path.Combine(lapfiles, "stpete grandprix"));
        var roadAmerica = Directory.CreateDirectory(Path.Combine(lapfiles, "roadamerica full")).FullName;
        await File.WriteAllTextAsync(Path.Combine(roadAmerica, "driver.blap"), "not imported");
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var catalog = new TrackCatalogService(factory);

            Assert.Equal(5, await catalog.SynchronizeAsync(lapfiles));
            var entries = await catalog.GetAllAsync();

            Assert.Equal(5, entries.Count);
            Assert.Contains(entries, item => item.IracingFolderName == "fuji gp" && item.TrackName == "Fuji" && item.Configuration == "GP");
            Assert.Contains(entries, item => item.IracingFolderName == "roadamerica full" && item.TrackName == "Road America" && item.Configuration == "Full");
            Assert.Contains(entries, item => item.IracingFolderName == "mexico grandprix" && item.TrackName == "Mexique");
            Assert.Contains(entries, item => item.IracingFolderName == "stpete grandprix" && item.TrackName == "Saint-Pétersbourg");
            Assert.Equal("Road America", catalog.Find("VRS_27S5_RoadAmerica_R.sto")?.TrackName);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AnalyzerUsesTrackAndConfigurationFromCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "TrackCatalogAnalyzerTests", Guid.NewGuid().ToString("N"));
        var lapfiles = Directory.CreateDirectory(Path.Combine(root, "lapfiles")).FullName;
        Directory.CreateDirectory(Path.Combine(lapfiles, "watkinsglen 2021 fullcourse"));
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var catalog = new TrackCatalogService(factory);
            await catalog.SynchronizeAsync(lapfiles);

            var metadata = new SetupMetadataAnalyzer(catalog).Analyze("GO_27S5_WatkinsGlen2021FullCourse_R.sto");

            Assert.Equal("Watkins Glen", metadata.Track);
            Assert.Equal("2021 Fullcourse", metadata.TrackConfiguration);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
