using IracingSetupManager.Providers.Contracts;

namespace IracingSetupManager.Providers.Common;

public abstract class AuthorizedSetupProvider(IAuthorizedProviderClient client) : ISetupProvider
{
    public abstract ProviderId Id { get; }

    public async Task<ProviderSyncResult> SynchronizeAsync(
        ProviderSyncRequest request,
        IProgress<ProviderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloaded = await client.DownloadAuthorizedSetupsAsync(
            Id,
            request,
            progress,
            cancellationToken);

        return new ProviderSyncResult(Id, true, downloaded);
    }
}

