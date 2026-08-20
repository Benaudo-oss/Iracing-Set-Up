using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class AutomaticMonitoringSettingsTests
{
    [Fact]
    public async Task AutomaticMonitoringIsDisabledByDefaultAndPersistsUserChoice()
    {
        var root = Path.Combine(Path.GetTempPath(), "AutomaticMonitoringTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var settings = new AutomaticMonitoringSettingsService(factory);

            Assert.False(await settings.IsEnabledAsync());
            await settings.SaveAsync(true);
            Assert.True(await settings.IsEnabledAsync());
            await settings.SaveAsync(false);
            Assert.False(await settings.IsEnabledAsync());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task HymoMonitoringIsEnabledByDefaultAndPersistsUserChoice()
    {
        var root = Path.Combine(Path.GetTempPath(), "HymoMonitoringTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var settings = new HymoMonitoringSettingsService(factory);

            Assert.True(await settings.IsEnabledAsync());
            await settings.SaveAsync(false);
            Assert.False(await settings.IsEnabledAsync());
            await settings.SaveAsync(true);
            Assert.True(await settings.IsEnabledAsync());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DownloadsMonitoringCanRemainDisabledAfterReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "DownloadsMonitoringTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var settings = new MonitoredFolderSettingsService(factory, new MonitoredFolderPolicy());

            Assert.Contains(await settings.GetAsync(), folder => folder.Kind == ImportFolderKind.Downloads);
            await settings.SaveAsync([]);
            Assert.DoesNotContain(await settings.GetAsync(), folder => folder.Kind == ImportFolderKind.Downloads);

            var downloads = Path.Combine(root, "Downloads");
            await settings.SaveAsync([new MonitoredFolder(downloads, ImportFolderKind.Downloads)]);
            Assert.Contains(await settings.GetAsync(), folder =>
                folder.Kind == ImportFolderKind.Downloads && folder.Path == Path.GetFullPath(downloads));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
