using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Integrations.Garage61;
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

    [Theory]
    [InlineData(true, true, SetupStatus.Valide, false)]
    [InlineData(false, false, SetupStatus.Valide, false)]
    [InlineData(false, true, SetupStatus.AVerifier, false)]
    [InlineData(false, true, SetupStatus.Valide, true)]
    public void Garage61RequiresNonPrivateValidatedAndApprovedSetup(
        bool isPrivate,
        bool approved,
        SetupStatus status,
        bool expected)
    {
        var setup = new SetupFile(
            Guid.NewGuid(),
            "setup.sto",
            new string('a', 64),
            1024,
            @"C:\Archive\setup.sto",
            status,
            DateTimeOffset.UtcNow,
            SetupSourceKind.OfficialProviderApplication,
            @"C:\GOFast\setup.sto",
            isPrivate,
            approved);

        var allowed = new Garage61ExportPolicy().CanExport(setup, out _);

        Assert.Equal(expected, allowed);
    }
}

