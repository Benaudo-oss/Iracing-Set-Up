using IracingSetupManager.Providers.Contracts;
using IracingSetupManager.Providers.Synchronization;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class ProviderSynchronizationCoordinatorTests
{
    [Fact]
    public async Task RunsOnlySelectedProviders()
    {
        var hymo = new FakeProvider(ProviderId.Hymo);
        var go = new FakeProvider(ProviderId.GoSetups);
        var coordinator = new ProviderSynchronizationCoordinator([hymo, go]);

        var results = await coordinator.SynchronizeAsync(
            ProviderSelection.From(ProviderId.GoSetups),
            new ProviderSyncRequest(new HashSet<string> { "GT3" }, @"C:\Archive"));

        Assert.False(hymo.WasCalled);
        Assert.True(go.WasCalled);
        Assert.Single(results);
    }

    [Fact]
    public async Task FailureOfOneProviderDoesNotStopOthers()
    {
        var failing = new FakeProvider(ProviderId.Hymo, shouldFail: true);
        var working = new FakeProvider(ProviderId.GridAndGo);
        var coordinator = new ProviderSynchronizationCoordinator([failing, working]);

        var results = await coordinator.SynchronizeAsync(
            ProviderSelection.From(ProviderId.Hymo, ProviderId.GridAndGo),
            new ProviderSyncRequest(new HashSet<string> { "GTP" }, @"C:\Archive"));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Provider == ProviderId.Hymo && !result.IsSuccessful);
        Assert.Contains(results, result => result.Provider == ProviderId.GridAndGo && result.IsSuccessful);
    }

    private sealed class FakeProvider(ProviderId id, bool shouldFail = false) : ISetupProvider
    {
        public ProviderId Id => id;

        public bool WasCalled { get; private set; }

        public Task<ProviderSyncResult> SynchronizeAsync(
            ProviderSyncRequest request,
            IProgress<ProviderProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return shouldFail
                ? throw new InvalidOperationException("Fournisseur indisponible")
                : Task.FromResult(new ProviderSyncResult(Id, true, 1));
        }
    }
}

