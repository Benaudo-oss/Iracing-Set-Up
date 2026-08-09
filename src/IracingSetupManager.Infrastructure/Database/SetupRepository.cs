using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupRepository(ISetupDbContextFactory contextFactory) : ISetupRepository
{
    public async Task AddAsync(SetupEntity setup, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setup.Sha256);

        await using var context = contextFactory.Create();
        context.Setups.Add(setup);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SetupEntity?> FindBySha256Async(
        string sha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        await using var context = contextFactory.Create();
        return await context.Setups
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Sha256 == sha256, cancellationToken);
    }
}

