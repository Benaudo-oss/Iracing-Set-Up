namespace IracingSetupManager.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;

public sealed class SetupDatabase(ISetupDbContextFactory contextFactory)
{
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
            """,
            cancellationToken);
    }
}
