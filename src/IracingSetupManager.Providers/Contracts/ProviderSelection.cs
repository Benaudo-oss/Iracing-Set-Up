namespace IracingSetupManager.Providers.Contracts;

public sealed record ProviderSelection(IReadOnlySet<ProviderId> Providers)
{
    public static ProviderSelection From(params ProviderId[] providers) =>
        new(providers.ToHashSet());

    public bool Contains(ProviderId providerId) => Providers.Contains(providerId);
}

