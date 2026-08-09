using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SensitiveDataRetentionService(ISetupDbContextFactory contextFactory)
{
    public async Task<int> PurgeUnneededSourcePathsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.Setups.Where(item => item.SourcePath != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SourcePath, (string?)null), cancellationToken);
    }
}
