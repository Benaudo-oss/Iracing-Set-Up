using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public async Task StoresAndFindsAllSetupMetadata()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var repository = new SetupRepository(environment.Factory);
        var setup = CreateSetup();

        await repository.AddAsync(setup);
        var stored = await repository.FindBySha256Async(setup.Sha256);

        Assert.NotNull(stored);
        Assert.Equal("HYMO", stored.Provider);
        Assert.Equal("Spa-Francorchamps", stored.Track);
        Assert.Equal(setup.Week, stored.Week);
    }

    [Fact]
    public async Task ArchiveFolderIsAskedOnlyOnce()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var archivePath = Path.Combine(environment.RootPath, "Archive");
        var picker = new FakeFolderPicker(archivePath);
        var service = new ArchivePathService(environment.Factory, picker);

        var first = await service.GetOrChooseAsync();
        var second = await service.GetOrChooseAsync();

        Assert.Equal(first, second);
        Assert.Equal(1, picker.CallCount);
        Assert.True(Directory.Exists(archivePath));
    }

    [Fact]
    public async Task DataPersistsAfterDatabaseIsReopenedAndHashRemainsUnique()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var repository = new SetupRepository(environment.Factory);
        var setup = CreateSetup();
        await repository.AddAsync(setup);
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(CreateSetup()));

        var reopened = new LocalSetupDbContextFactory(Path.Combine(environment.RootPath, "setups.db"));
        await using var context = reopened.Create();
        var stored = await context.Setups.FindAsync(setup.Id);
        Assert.NotNull(stored);
        Assert.Equal(setup.Comment, stored.Comment);
    }

    [Fact]
    public async Task DashboardStatisticsSupportEmptyAndPopulatedDatabase()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var queries = new SetupQueryService(environment.Factory);
        var empty = await queries.GetDashboardStatisticsAsync();
        Assert.Equal(0, empty.Total);
        Assert.Null(empty.LastDownloadUtc);

        var setup = CreateSetup();
        setup.LastCopiedToIracingTeamAtUtc = DateTimeOffset.UtcNow;
        await new SetupRepository(environment.Factory).AddAsync(setup);
        var populated = await queries.GetDashboardStatisticsAsync();
        Assert.Equal(1, populated.Total);
        Assert.Equal(1, populated.CopiedToIracingTeam);
        Assert.Equal(setup.DownloadedAtUtc, populated.LastDownloadUtc);
    }

    [Fact]
    public async Task DashboardBreakdownGroupsProvidersAndStatuses()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var repository = new SetupRepository(environment.Factory);
        var first = CreateSetup();
        first.Provider = "VRS";
        first.Status = SetupStatus.Valide;
        var second = CreateSetup();
        second.Id = Guid.NewGuid();
        second.Sha256 = new string('b', 64);
        second.Provider = "VRS";
        second.Status = SetupStatus.AVerifier;
        var missing = CreateSetup();
        missing.Id = Guid.NewGuid();
        missing.Sha256 = new string('c', 64);
        missing.Provider = "HYMO";
        missing.Status = SetupStatus.FichierManquant;

        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await repository.AddAsync(missing);

        var breakdown = await new SetupQueryService(environment.Factory).GetDashboardBreakdownAsync();

        var provider = Assert.Single(breakdown.Providers);
        Assert.Equal("VRS", provider.Label);
        Assert.Equal(2, provider.Count);
        Assert.Contains(breakdown.Statuses, item => item.Status == SetupStatus.Valide && item.Count == 1);
        Assert.Contains(breakdown.Statuses, item => item.Status == SetupStatus.AVerifier && item.Count == 1);
        Assert.Contains(breakdown.Statuses, item => item.Status == SetupStatus.FichierManquant && item.Count == 1);
    }

    [Fact]
    public async Task LegacyGarage61UploadSchemaIsRemovedWithoutLosingTheSetup()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var setup = CreateSetup();
        await new SetupRepository(environment.Factory).AddAsync(setup);

        await using (var context = environment.Factory.Create())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE "Setups" ADD COLUMN "IsPrivate" INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE "Setups" ADD COLUMN "Garage61ExportApproved" INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE "Setups" ADD COLUMN "SentToGarage61AtUtc" TEXT NULL;
                ALTER TABLE "Setups" ADD COLUMN "Garage61Succeeded" INTEGER NULL;
                ALTER TABLE "Setups" ADD COLUMN "Garage61Result" TEXT NULL;
                ALTER TABLE "Setups" ADD COLUMN "Garage61SetupId" TEXT NULL;
                ALTER TABLE "Setups" ADD COLUMN "Garage61SetupUrl" TEXT NULL;
                CREATE INDEX "IX_Setups_IsPrivate_Garage61ExportApproved_Status"
                    ON "Setups" ("IsPrivate", "Garage61ExportApproved", "Status");
                UPDATE "Setups" SET "Status" = 'EnvoyeVersGarage61';
                DELETE FROM "SchemaMigrations" WHERE "Version" >= 2;
                """);
        }

        await new SetupDatabase(environment.Factory).InitializeAsync();

        await using var migratedContext = environment.Factory.Create();
        var migrated = await migratedContext.Setups.SingleAsync();
        Assert.Equal(SetupStatus.Valide, migrated.Status);

        var connection = migratedContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"Setups\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(1));

        Assert.DoesNotContain("IsPrivate", columns);
        Assert.DoesNotContain("Garage61ExportApproved", columns);
        Assert.DoesNotContain("SentToGarage61AtUtc", columns);
        Assert.DoesNotContain("Garage61Succeeded", columns);
        Assert.DoesNotContain("Garage61Result", columns);
        Assert.DoesNotContain("Garage61SetupId", columns);
        Assert.DoesNotContain("Garage61SetupUrl", columns);
        Assert.Equal(SetupDatabase.CurrentSchemaVersion, await new SetupDatabase(environment.Factory).GetSchemaVersionAsync());
    }

    [Fact]
    public async Task SchemaMigrationsAreVersionedAndIdempotent()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var database = new SetupDatabase(environment.Factory);

        await database.InitializeAsync();
        await database.InitializeAsync();

        Assert.Equal(SetupDatabase.CurrentSchemaVersion, await database.GetSchemaVersionAsync());
        await using var context = environment.Factory.Create();
        var versions = await context.Database.SqlQueryRaw<int>(
            "SELECT \"Version\" AS \"Value\" FROM \"SchemaMigrations\" ORDER BY \"Version\"").ToListAsync();
        Assert.Equal(Enumerable.Range(1, SetupDatabase.CurrentSchemaVersion), versions);
    }

    [Fact]
    public async Task InitializationRepairsWeekColumnEvenWhenMigrationLedgerClaimsItExists()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using (var context = environment.Factory.Create())
        {
            await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_Setups_Season_Week\";");
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Setups\" DROP COLUMN \"Week\";");
        }

        await new SetupDatabase(environment.Factory).InitializeAsync();

        await using var repairedContext = environment.Factory.Create();
        var connection = repairedContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"Setups\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        Assert.Contains("Week", columns);
        Assert.Equal(SetupDatabase.CurrentSchemaVersion,
            await new SetupDatabase(environment.Factory).GetSchemaVersionAsync());
    }

    [Fact]
    public async Task QueriesSortDateTimeOffsetValuesAfterLoadingFromSqlite()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var repository = new SetupRepository(environment.Factory);
        var older = CreateSetup();
        older.Status = SetupStatus.AVerifier;
        older.DownloadedAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        var newer = CreateSetup();
        newer.Status = SetupStatus.AVerifier;
        newer.Sha256 = new string('b', 64);
        newer.DownloadedAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        await repository.AddAsync(older);
        await repository.AddAsync(newer);

        var queries = new SetupQueryService(environment.Factory);
        var toReview = await queries.GetToReviewAsync();
        Assert.Equal(newer.Id, toReview[0].Id);

        var validation = new SetupValidationService(environment.Factory);
        await validation.ValidateAsync(older.Id);
        await validation.RefuseAsync(newer.Id);

        var all = await queries.GetAllAsync();
        var history = await queries.GetHistoryAsync();

        Assert.Equal(newer.Id, all[0].Id);
        Assert.Equal(2, history.Count);
        Assert.True(history[0].ChangedAtUtc >= history[1].ChangedAtUtc);
    }

    [Fact]
    public async Task ClearingApplicationHistoryKeepsSetups()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var setup = CreateSetup();
        setup.Status = SetupStatus.AVerifier;
        await new SetupRepository(environment.Factory).AddAsync(setup);
        await new SetupValidationService(environment.Factory).ValidateAsync(setup.Id);

        var queries = new SetupQueryService(environment.Factory);
        Assert.Equal(1, await queries.ClearHistoryAsync());
        Assert.Empty(await queries.GetHistoryAsync());
        Assert.Single(await queries.GetAllAsync());
    }

    [Fact]
    public async Task ExistingSetupMetadataIsRefreshedWithoutChangingItsArchivePath()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var setup = CreateSetup();
        setup.OriginalFileName = "VRS_26S3PG_M4GT3_LeMans_R1_V2.sto";
        setup.Provider = "À identifier";
        setup.Category = "À identifier";
        setup.Car = "À identifier";
        setup.Track = "À identifier";
        setup.TrackConfiguration = null;
        setup.Season = null;
        setup.SetupType = "À identifier";
        var originalArchivePath = setup.ArchivePath;
        await new SetupRepository(environment.Factory).AddAsync(setup);

        var count = await new SetupMetadataRefreshService(
            environment.Factory,
            new SetupMetadataAnalyzer()).RefreshAsync();
        var refreshed = await new SetupRepository(environment.Factory).FindBySha256Async(setup.Sha256);

        Assert.Equal(1, count);
        Assert.NotNull(refreshed);
        Assert.Equal("VRS", refreshed.Provider);
        Assert.Equal("BMW M4 GT3", refreshed.Car);
        Assert.Equal("Le Mans", refreshed.Track);
        Assert.Equal("2026 S3", refreshed.Season);
        Assert.Equal("Race V2", refreshed.SetupType);
        Assert.Equal(originalArchivePath, refreshed.ArchivePath);
    }

    [Fact]
    public async Task MetadataRefreshUpdatesTrackConfigurationWhenItBecomesKnown()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var setup = CreateSetup();
        setup.OriginalFileName = "VRS_26S3_M4GT3_Donington_NTL_R.sto";
        setup.Track = "À identifier";
        setup.TrackConfiguration = null;
        await new SetupRepository(environment.Factory).AddAsync(setup);

        await new SetupMetadataRefreshService(environment.Factory, new SetupMetadataAnalyzer()).RefreshAsync();
        var refreshed = await new SetupRepository(environment.Factory).FindBySha256Async(setup.Sha256);

        Assert.NotNull(refreshed);
        Assert.Equal("Donington Park", refreshed.Track);
        Assert.Equal("National", refreshed.TrackConfiguration);
    }

    [Fact]
    public async Task MissingArchiveFileIsExcludedFromDashboardAndCanBeRemoved()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var setup = CreateSetup();
        setup.Status = SetupStatus.Valide;
        setup.ArchivePath = Path.Combine(environment.RootPath, "missing.sto");
        await new SetupRepository(environment.Factory).AddAsync(setup);
        var integrity = new SetupLibraryIntegrityService(environment.Factory);

        Assert.Equal(1, await integrity.MarkMissingFilesAsync());
        var statistics = await new SetupQueryService(environment.Factory).GetDashboardStatisticsAsync();
        Assert.Equal(0, statistics.Total);
        Assert.Equal(0, statistics.Validated);

        Assert.Equal(1, await integrity.RemoveMissingEntriesAsync([setup.Id]));
        Assert.Empty(await new SetupQueryService(environment.Factory).GetAllAsync());
    }

    private static SetupEntity CreateSetup() => new()
    {
        Id = Guid.NewGuid(),
        OriginalFileName = "spa_race.sto",
        Provider = "HYMO",
        Category = "GT3",
        Car = "Porsche 911 GT3 R",
        Track = "Spa-Francorchamps",
        TrackConfiguration = "Grand Prix Pits",
        Season = "2026 S3",
        SetupType = "Race",
        SizeInBytes = 4096,
        Sha256 = new string('a', 64),
        ArchivePath = @"C:\Archive\2026 S3\Spa\Porsche\HYMO\Race\spa_race.sto",
        SourceKind = SetupSourceKind.OfficialProviderApplication,
        SourcePath = @"C:\HYMO\spa_race.sto",
        Status = SetupStatus.Valide,
        PersonalRating = 5,
        Comment = "Stable",
        DownloadedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
    };

    private sealed class FakeFolderPicker(string path) : IArchiveFolderPicker
    {
        public int CallCount { get; private set; }

        public Task<string?> PickArchiveFolderAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<string?>(path);
        }
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(string rootPath, LocalSetupDbContextFactory factory)
        {
            RootPath = rootPath;
            Factory = factory;
        }

        public string RootPath { get; }

        public LocalSetupDbContextFactory Factory { get; }

        public static async Task<TestEnvironment> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "IracingSetupManagerTests", Guid.NewGuid().ToString("N"));
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            return new TestEnvironment(root, factory);
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
