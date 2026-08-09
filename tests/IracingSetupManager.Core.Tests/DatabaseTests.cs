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
        Assert.Equal("Envoi accepté", stored.Garage61Result);
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
        await new SetupRepository(environment.Factory).AddAsync(setup);
        var populated = await queries.GetDashboardStatisticsAsync();
        Assert.Equal(1, populated.Total);
        Assert.Equal(setup.DownloadedAtUtc, populated.LastDownloadUtc);
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
    public async Task ExistingSetupMetadataIsRefreshedWithoutChangingItsArchivePath()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var setup = CreateSetup();
        setup.OriginalFileName = "VRS_26S3PG_M4GT3_LeMans_R1_V2.sto";
        setup.Provider = "À identifier";
        setup.Category = "À identifier";
        setup.Car = "À identifier";
        setup.Track = "À identifier";
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
        Status = SetupStatus.EnvoyeVersGarage61,
        PersonalRating = 5,
        Comment = "Stable",
        DownloadedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
        SentToGarage61AtUtc = DateTimeOffset.UtcNow,
        Garage61Succeeded = true,
        Garage61Result = "Envoi accepté",
        Garage61SetupId = "g61-42",
        Garage61SetupUrl = "https://garage61.net/setup/g61-42"
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
