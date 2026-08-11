namespace IracingSetupManager.Infrastructure.Database;

using System.Data;
using Microsoft.EntityFrameworkCore;

public sealed class SetupDatabase(ISetupDbContextFactory contextFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSetupColumnAsync(context, "LastCopiedToIracingAtUtc", "TEXT NULL", cancellationToken);
        await EnsureSetupColumnAsync(context, "IracingCopyCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSetupColumnAsync(context, "LastCopiedToIracingTeamAtUtc", "TEXT NULL", cancellationToken);
        await EnsureSetupColumnAsync(context, "IracingTeamCopyCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SetupChangeHistory" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SetupChangeHistory" PRIMARY KEY AUTOINCREMENT,
                "SetupId" TEXT NOT NULL,
                "OriginalFileName" TEXT NOT NULL,
                "ChangeType" TEXT NOT NULL,
                "PreviousStatus" TEXT NULL,
                "NewStatus" TEXT NULL,
                "PreviousRating" INTEGER NULL,
                "NewRating" INTEGER NULL,
                "PreviousComment" TEXT NULL,
                "NewComment" TEXT NULL,
                "ChangedAtUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_SetupChangeHistory_SetupId_ChangedAtUtc"
                ON "SetupChangeHistory" ("SetupId", "ChangedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_SetupChangeHistory_ChangedAtUtc"
                ON "SetupChangeHistory" ("ChangedAtUtc");
            CREATE TABLE IF NOT EXISTS "TrackCatalog" (
                "IracingFolderName" TEXT NOT NULL CONSTRAINT "PK_TrackCatalog" PRIMARY KEY,
                "TrackName" TEXT NOT NULL,
                "Configuration" TEXT NULL,
                "NormalizedAlias" TEXT NOT NULL,
                "LastSeenAtUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_TrackCatalog_NormalizedAlias"
                ON "TrackCatalog" ("NormalizedAlias");
            """,
            cancellationToken);
    }

    private static async Task EnsureSetupColumnAsync(
        SetupDbContext context,
        string columnName,
        string definition,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var inspect = connection.CreateCommand();
            inspect.CommandText = "PRAGMA table_info(\"Setups\");";
            await using var reader = await inspect.ExecuteReaderAsync(cancellationToken);
            var exists = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
            await reader.DisposeAsync();
            if (exists) return;

            await using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE \"Setups\" ADD COLUMN \"{columnName}\" {definition};";
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
