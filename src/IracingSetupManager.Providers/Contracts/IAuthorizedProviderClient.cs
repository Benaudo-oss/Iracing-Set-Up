namespace IracingSetupManager.Providers.Contracts;

public interface IAuthorizedProviderClient
{
    Task<int> DownloadAuthorizedSetupsAsync(
        ProviderId provider,
        ProviderSyncRequest request,
        IProgress<ProviderProgress>? progress,
        CancellationToken cancellationToken);
}

