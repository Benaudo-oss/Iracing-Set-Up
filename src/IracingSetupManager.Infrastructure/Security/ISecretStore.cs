namespace IracingSetupManager.Infrastructure.Security;

public interface ISecretStore
{
    Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default);

    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
