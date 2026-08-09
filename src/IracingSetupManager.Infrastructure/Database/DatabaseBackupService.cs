using IracingSetupManager.Infrastructure.Files;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class DatabaseBackupService(ISetupDbContextFactory contextFactory)
{
    public async Task<string> BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var fullDestination = SecurePath.GetFullPath(destinationPath);
        if (!fullDestination.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("La sauvegarde doit utiliser l'extension .db.", nameof(destinationPath));
        if (File.Exists(fullDestination)) throw new IOException("Une sauvegarde portant ce nom existe déjà.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
        await using var context = contextFactory.Create();
        var source = (SqliteConnection)context.Database.GetDbConnection();
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullDestination, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        return fullDestination;
    }
}
