namespace IracingSetupManager.Infrastructure.Database;

using System.Data;
using Microsoft.EntityFrameworkCore;

public sealed class SetupDatabase(ISetupDbContextFactory contextFactory)
{
    public const int CurrentSchemaVersion = 6;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        await context.Database.EnsureCreatedAsync(cancellationToken);
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
            CREATE TABLE IF NOT EXISTS "RecognitionAliases" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecognitionAliases" PRIMARY KEY AUTOINCREMENT,
                "Kind" TEXT NOT NULL,
                "Alias" TEXT NOT NULL,
                "NormalizedAlias" TEXT NOT NULL,
                "CanonicalValue" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecognitionAliases_Kind_NormalizedAlias"
                ON "RecognitionAliases" ("Kind", "NormalizedAlias");
            CREATE TABLE IF NOT EXISTS "SchemaMigrations" (
                "Version" INTEGER NOT NULL CONSTRAINT "PK_SchemaMigrations" PRIMARY KEY,
                "AppliedAtUtc" TEXT NOT NULL
            );
            """,
            cancellationToken);
        // Repair required columns from their physical schema instead of trusting only
        // the migration ledger. This also heals databases created by an interrupted update.
        await RepairRequiredSchemaAsync(context, cancellationToken);
        await ApplyPendingMigrationsAsync(context, cancellationToken);
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await ReadSchemaVersionAsync(context, cancellationToken);
    }

    private static async Task ApplyPendingMigrationsAsync(
        SetupDbContext context,
        CancellationToken cancellationToken)
    {
        var version = await ReadSchemaVersionAsync(context, cancellationToken);
        if (version < 1)
        {
            await RunMigrationAsync(context, 1, async () =>
            {
                await EnsureSetupColumnAsync(context, "LastCopiedToIracingAtUtc", "TEXT NULL", cancellationToken);
                await EnsureSetupColumnAsync(context, "IracingCopyCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
                await EnsureSetupColumnAsync(context, "LastCopiedToIracingTeamAtUtc", "TEXT NULL", cancellationToken);
                await EnsureSetupColumnAsync(context, "IracingTeamCopyCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            }, cancellationToken);
            version = 1;
        }

        if (version < 2)
        {
            await RunMigrationAsync(
                context,
                2,
                () => RemoveLegacyGarage61UploadSchemaAsync(context, cancellationToken),
                cancellationToken);
            version = 2;
        }

        if (version < 3)
        {
            await RunMigrationAsync(context, 3, async () =>
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE \"Setups\" SET \"Status\" = 'AVerifier' WHERE \"Status\" IN ('Nouveau', 'ACorriger');",
                    cancellationToken);
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE \"SetupChangeHistory\" SET \"PreviousStatus\" = 'AVerifier' WHERE \"PreviousStatus\" IN ('Nouveau', 'ACorriger');",
                    cancellationToken);
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE \"SetupChangeHistory\" SET \"NewStatus\" = 'AVerifier' WHERE \"NewStatus\" IN ('Nouveau', 'ACorriger');",
                    cancellationToken);
            }, cancellationToken);
            version = 3;
        }

        if (version < 4)
        {
            await RunMigrationAsync(context, 4, () => Task.CompletedTask, cancellationToken);
            version = 4;
        }

        if (version < 5)
        {
            await RunMigrationAsync(context, 5, async () =>
            {
                await EnsureSetupColumnAsync(context, "Week", "INTEGER NULL", cancellationToken);
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_Setups_Season_Week\" ON \"Setups\" (\"Season\", \"Week\");",
                    cancellationToken);
            }, cancellationToken);
            version = 5;
        }

        if (version < 6)
        {
            await RunMigrationAsync(
                context,
                6,
                () => RepairRequiredSchemaAsync(context, cancellationToken),
                cancellationToken);
        }
    }

    private static async Task RepairRequiredSchemaAsync(
        SetupDbContext context,
        CancellationToken cancellationToken)
    {
        await EnsureSetupColumnAsync(context, "Week", "INTEGER NULL", cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Setups_Season_Week\" ON \"Setups\" (\"Season\", \"Week\");",
            cancellationToken);
    }

    private static async Task RunMigrationAsync(
        SetupDbContext context,
        int version,
        Func<Task> migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await migration();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"SchemaMigrations\" (\"Version\", \"AppliedAtUtc\") VALUES ({version}, {DateTimeOffset.UtcNow});",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<int> ReadSchemaVersionAsync(
        SetupDbContext context,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(\"Version\"), 0) FROM \"SchemaMigrations\";";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static async Task RemoveLegacyGarage61UploadSchemaAsync(
        SetupDbContext context,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"Setups\" SET \"Status\" = 'Valide' WHERE \"Status\" IN ('EnvoyeVersGarage61', 'ErreurEnvoi');",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"SetupChangeHistory\" SET \"PreviousStatus\" = 'Valide' WHERE \"PreviousStatus\" IN ('EnvoyeVersGarage61', 'ErreurEnvoi');",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"SetupChangeHistory\" SET \"NewStatus\" = 'Valide' WHERE \"NewStatus\" IN ('EnvoyeVersGarage61', 'ErreurEnvoi');",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"IX_Setups_IsPrivate_Garage61ExportApproved_Status\";",
            cancellationToken);

        string[] legacyColumns =
        [
            "IsPrivate",
            "Garage61ExportApproved",
            "SentToGarage61AtUtc",
            "Garage61Succeeded",
            "Garage61Result",
            "Garage61SetupId",
            "Garage61SetupUrl"
        ];
        foreach (var column in legacyColumns)
        {
            await DropSetupColumnIfExistsAsync(context, column, cancellationToken);
        }
    }

    private static async Task EnsureSetupColumnAsync(
        SetupDbContext context,
        string columnName,
        string definition,
        CancellationToken cancellationToken)
    {
        if (await SetupColumnExistsAsync(context, columnName, cancellationToken)) return;
        await ExecuteSetupSchemaCommandAsync(
            context,
            $"ALTER TABLE \"Setups\" ADD COLUMN \"{columnName}\" {definition};",
            cancellationToken);
    }

    private static async Task DropSetupColumnIfExistsAsync(
        SetupDbContext context,
        string columnName,
        CancellationToken cancellationToken)
    {
        if (!await SetupColumnExistsAsync(context, columnName, cancellationToken)) return;
        await ExecuteSetupSchemaCommandAsync(
            context,
            $"ALTER TABLE \"Setups\" DROP COLUMN \"{columnName}\";",
            cancellationToken);
    }

    private static async Task ExecuteSetupSchemaCommandAsync(
        SetupDbContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static async Task<bool> SetupColumnExistsAsync(
        SetupDbContext context,
        string columnName,
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
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
