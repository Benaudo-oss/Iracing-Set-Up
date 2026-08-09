namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupDatabase(ISetupDbContextFactory contextFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }
}

