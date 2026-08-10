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
}
