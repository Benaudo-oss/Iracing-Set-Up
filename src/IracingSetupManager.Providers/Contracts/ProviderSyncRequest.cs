namespace IracingSetupManager.Providers.Contracts;

public sealed record ProviderSyncRequest(
    IReadOnlySet<string> Categories,
    string ArchivePath);

