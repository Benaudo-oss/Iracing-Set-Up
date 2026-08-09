using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Infrastructure.Database;

public interface ISetupRepository
{
    Task AddAsync(SetupFile setup, CancellationToken cancellationToken = default);

    Task<SetupFile?> FindBySha256Async(string sha256, CancellationToken cancellationToken = default);
}

