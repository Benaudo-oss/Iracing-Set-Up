using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Settings;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class SynchronizationSelectionSettingsTests
{
    [Fact]
    public void DefaultSelectionIncludesLmp3()
    {
        Assert.Contains("LMP3", SynchronizationSelectionSettingsService.Default.Categories);
    }

    [Fact]
    public async Task SelectionPersistsUncheckedProvidersAndCategories()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "SyncSelectionTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var settings = new SynchronizationSelectionSettingsService(factory);
            Assert.Contains("SRS", (await settings.GetAsync()).Providers);

            await settings.SaveAsync(new SynchronizationSelection(["SRS"], ["GTE"]));
            var restored = await settings.GetAsync();

            Assert.Equal(["SRS"], restored.Providers);
            Assert.Equal(["GTE"], restored.Categories);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ExactGarage61TeamNameIsPersisted()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "TeamSettingsTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var settings = new IracingTeamSettingsService(factory);

            await settings.SaveNameAsync("BENAUDO Racing");

            Assert.Equal("BENAUDO Racing", await settings.GetNameAsync());
            await Assert.ThrowsAsync<ArgumentException>(() => settings.SaveNameAsync("Team/Interdite"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
