namespace IracingSetupManager.Providers.Contracts;

public interface ISetupProvider
{
    ProviderId Id { get; }

    Task<ProviderSyncResult> SynchronizeAsync(
        ProviderSyncRequest request,
        IProgress<ProviderProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

