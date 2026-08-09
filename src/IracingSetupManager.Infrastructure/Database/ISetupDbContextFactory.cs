namespace IracingSetupManager.Infrastructure.Database;

public interface ISetupDbContextFactory
{
    SetupDbContext Create();
}

