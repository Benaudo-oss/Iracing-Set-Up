using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class LocalSetupDbContextFactory : ISetupDbContextFactory
{
    private readonly DbContextOptions<SetupDbContext> _options;

    public LocalSetupDbContextFactory(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Le chemin de la base est invalide.", nameof(databasePath));

        Directory.CreateDirectory(directory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 15,
            ForeignKeys = true
        }.ToString();

        _options = new DbContextOptionsBuilder<SetupDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public SetupDbContext Create() => new(_options);
}
