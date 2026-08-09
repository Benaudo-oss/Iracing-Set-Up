using IracingSetupManager.Providers.Contracts;
using IracingSetupManager.Providers.GoSetups;
using IracingSetupManager.Providers.GridAndGo;
using IracingSetupManager.Providers.Hymo;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class ConcreteProviderTests
{
    public static TheoryData<ProviderId> Providers => new() { ProviderId.Hymo, ProviderId.GoSetups, ProviderId.GridAndGo };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task EachProviderPassesSimulatedAuthorizedDataToItsOwnConnector(ProviderId expected)
    {
        var client = new SimulatedClient(3);
        ISetupProvider provider = expected switch
        {
            ProviderId.Hymo => new HymoSetupProvider(client),
            ProviderId.GoSetups => new GoSetupProvider(client),
            _ => new GridAndGoSetupProvider(client)
        };
        var request = new ProviderSyncRequest(new HashSet<string> { "GT3", "GTP" }, @"C:\Archive");
        var result = await provider.SynchronizeAsync(request);
        Assert.Equal(expected, client.ReceivedProvider);
        Assert.Same(request, client.ReceivedRequest);
        Assert.True(result.IsSuccessful);
        Assert.Equal(3, result.DownloadedFiles);
    }

    private sealed class SimulatedClient(int count) : IAuthorizedProviderClient
    {
        public ProviderId? ReceivedProvider { get; private set; }
        public ProviderSyncRequest? ReceivedRequest { get; private set; }
        public Task<int> DownloadAuthorizedSetupsAsync(ProviderId provider, ProviderSyncRequest request, IProgress<ProviderProgress>? progress, CancellationToken cancellationToken)
        {
            ReceivedProvider = provider; ReceivedRequest = request;
            return Task.FromResult(count);
        }
    }
}
