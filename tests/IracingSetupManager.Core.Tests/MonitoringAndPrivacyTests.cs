using IracingSetupManager.Infrastructure.Files.Monitoring;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class MonitoringAndPrivacyTests
{
    [Fact]
    public void IracingSetupsFolderCannotBeMonitored()
    {
        var documents = Path.Combine(Path.GetTempPath(), "Documents");
        var policy = new MonitoredFolderPolicy(documents);
        var folder = new MonitoredFolder(
            Path.Combine(documents, "iRacing", "setups"),
            ImportFolderKind.OfficialProviderApplication,
            "GO Setups");

        var exception = Assert.Throws<InvalidOperationException>(() => policy.Validate(folder));

        Assert.Contains("ne peuvent pas être surveillés", exception.Message);
    }

    [Fact]
    public void DownloadsAndOfficialApplicationFoldersAreAllowed()
    {
        var documents = Path.Combine(Path.GetTempPath(), "Documents");
        var policy = new MonitoredFolderPolicy(documents);

        var downloads = policy.Validate(new MonitoredFolder(
            Path.Combine(Path.GetTempPath(), "Downloads"),
            ImportFolderKind.Downloads));
        var providerFolder = policy.Validate(new MonitoredFolder(
            Path.Combine(Path.GetTempPath(), "GOFast"),
            ImportFolderKind.OfficialProviderApplication,
            "GO Setups"));

        Assert.EndsWith("Downloads", downloads);
        Assert.EndsWith("GOFast", providerFolder);
    }

    [Fact]
    public void OnlyCatalogTrackTitanFoldersAreAllowedInsideIracingSetups()
    {
        var documents = Path.Combine(Path.GetTempPath(), "Documents");
        var policy = new MonitoredFolderPolicy(documents);
        var allowed = new MonitoredFolder(
            Path.Combine(documents, "iRacing", "setups", "bmwm4gt3", "Track Titan"),
            ImportFolderKind.TrackTitan,
            "HYMO");

        Assert.EndsWith(Path.Combine("bmwm4gt3", "Track Titan"), policy.Validate(allowed));
        Assert.Throws<InvalidOperationException>(() => policy.Validate(allowed with
        {
            Path = Path.Combine(documents, "iRacing", "setups", "bmwm4gt3", "Garage 61")
        }));
        Assert.Throws<InvalidOperationException>(() => policy.Validate(allowed with { Provider = "VRS" }));
    }

    [Fact]
    public void TrackTitanFoldersFollowSelectedHymoCategories()
    {
        var documents = Path.Combine(Path.GetTempPath(), "Documents");
        var resolver = new TrackTitanFolderResolver(documents);
        var folders = resolver.Resolve(new Infrastructure.Settings.SynchronizationSelection(
            ["HYMO"], ["GTP"]));

        Assert.Equal(5, folders.Count);
        Assert.All(folders, folder =>
        {
            Assert.Equal(ImportFolderKind.TrackTitan, folder.Kind);
            Assert.Equal("HYMO", folder.Provider);
            Assert.EndsWith("Track Titan", folder.Path);
        });
        Assert.Empty(resolver.Resolve(new Infrastructure.Settings.SynchronizationSelection(
            ["VRS"], ["GTP"])));
    }
}
