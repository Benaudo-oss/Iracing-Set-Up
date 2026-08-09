namespace IracingSetupManager.Providers.Contracts;

public sealed record ProviderSyncResult(
    ProviderId Provider,
    bool IsSuccessful,
    int DownloadedFiles,
    string? ErrorMessage = null);

