using IracingSetupManager.Providers.Contracts;

namespace IracingSetupManager.Providers.Synchronization;

public sealed class ProviderSynchronizationCoordinator(IEnumerable<ISetupProvider> providers)
{
    private readonly IReadOnlyDictionary<ProviderId, ISetupProvider> _providers =
        providers.ToDictionary(provider => provider.Id);

    public async Task<IReadOnlyList<ProviderSyncResult>> SynchronizeAsync(
        ProviderSelection selection,
        ProviderSyncRequest request,
        IProgress<ProviderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var selectedProviders = selection.Providers
            .Select(GetProvider)
            .Select(provider => SynchronizeIsolatedAsync(provider, request, progress, cancellationToken));

        return await Task.WhenAll(selectedProviders);
    }

    private ISetupProvider GetProvider(ProviderId id) =>
        _providers.TryGetValue(id, out var provider)
            ? provider
            : throw new InvalidOperationException($"Le fournisseur {id} n'est pas enregistré.");

    private static async Task<ProviderSyncResult> SynchronizeIsolatedAsync(
        ISetupProvider provider,
        ProviderSyncRequest request,
        IProgress<ProviderProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SynchronizeAsync(request, progress, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ProviderSyncResult(provider.Id, false, 0, exception.Message);
        }
    }
}

