namespace IracingSetupManager.Providers.Contracts;

public sealed record ProviderProgress(
    ProviderId Provider,
    string Stage,
    string? Category = null,
    string? Car = null,
    string? Track = null,
    int DownloadedFiles = 0,
    int RemainingFiles = 0);

