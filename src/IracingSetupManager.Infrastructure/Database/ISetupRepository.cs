using IracingSetupManager.Infrastructure.Database.Entities;

namespace IracingSetupManager.Infrastructure.Database;

public interface ISetupRepository
{
    Task AddAsync(SetupEntity setup, CancellationToken cancellationToken = default);

    Task UpdateAsync(SetupEntity setup, CancellationToken cancellationToken = default);

    Task<SetupEntity?> FindBySha256Async(string sha256, CancellationToken cancellationToken = default);
}
