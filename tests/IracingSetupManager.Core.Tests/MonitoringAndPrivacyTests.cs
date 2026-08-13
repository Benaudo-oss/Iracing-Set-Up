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
}
