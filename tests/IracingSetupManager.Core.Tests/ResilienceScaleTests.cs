using System.Diagnostics;
using System.Reflection;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Resilience;
using IracingSetupManager.Infrastructure.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class ResilienceScaleTests
{
    [Fact]
    public async Task ThirtyThousandSetupsRemainPagedAndQueryable()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        const int count = 30_000;
        var stopwatch = Stopwatch.StartNew();
        await using (var context = environment.Factory.Create())
        {
            for (var offset = 0; offset < count; offset += 1_000)
            {
                var batch = Enumerable.Range(offset, Math.Min(1_000, count - offset))
                    .Select(index => CreateSetup(index));
                context.Setups.AddRange(batch);
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
            }
        }

        var service = new SetupQueryService(environment.Factory);
        var lastPage = await service.GetPageAsync(new SetupPageRequest(29_950, 50));
        var filtered = await service.GetPageAsync(new SetupPageRequest(0, 100, Category: "GTP"));

        Assert.Equal(count, lastPage.TotalCount);
        Assert.Equal(50, lastPage.Items.Count);
        Assert.Equal(10_000, filtered.TotalCount);
        Assert.Equal(100, filtered.Items.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(2), $"Test de volumétrie trop lent : {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task SaveWaitsForTemporarySqliteWriteLockAndThenSucceeds()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using var blocker = new SqliteConnection($"Data Source={environment.DatabasePath};Pooling=False;Default Timeout=15");
        await blocker.OpenAsync();
        await using (var begin = blocker.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            await begin.ExecuteNonQueryAsync();
        }

        var save = Task.Run(async () =>
        {
            await using var context = environment.Factory.Create();
            context.Setups.Add(CreateSetup(1));
            return await context.SaveChangesAsync();
        });
        await Task.Delay(300);
        Assert.False(save.IsCompletedSuccessfully);
        await using (var commit = blocker.CreateCommand())
        {
            commit.CommandText = "COMMIT;";
            await commit.ExecuteNonQueryAsync();
        }

        Assert.Equal(1, await save.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task FileDeletedDuringStabilityCheckIsIgnoredWithoutCrash()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "downloading.sto");
            await File.WriteAllTextAsync(path, "partial");
            var awaiter = new StableFileAwaiter(
                TimeSpan.FromMilliseconds(100),
                requiredStableProbes: 3,
                timeout: TimeSpan.FromSeconds(1),
                alreadyStableAge: TimeSpan.FromMinutes(1));

            var check = awaiter.WaitAsync(path);
            await Task.Delay(30);
            File.Delete(path);

            Assert.False(await check.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MonitoringQueueOverflowIsReportedOnceAndNeverBlocksProducer()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using var monitoring = CreateMonitoringService(environment);
        var failures = new List<Exception>();
        monitoring.ImportFailed += (_, exception) => failures.Add(exception);
        var queueMethod = typeof(ImportMonitoringService).GetMethod(
            "TryQueueIfNeeded",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < 3_000; index++)
        {
            queueMethod.Invoke(monitoring,
            [new DetectedImportFile(
                Path.Combine(environment.RootPath, $"queued-{index}.sto"),
                ImportFolderKind.Downloads,
                null)]);
        }

        Assert.Single(failures);
        Assert.Contains("file de synchronisation est pleine", failures[0].Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OptionalStartupFailureDoesNotPreventFollowingTasks()
    {
        var completed = new List<string>();
        var failures = new List<string>();
        OptionalTask[] tasks =
        [
            new("première", _ => throw new IOException("Échec simulé")),
            new("deuxième", _ => { completed.Add("deuxième"); return Task.CompletedTask; }),
            new("troisième", _ => { completed.Add("troisième"); return Task.CompletedTask; })
        ];

        await OptionalTaskSequence.RunAsync(tasks, (name, _) => failures.Add(name));

        Assert.Equal(["première"], failures);
        Assert.Equal(["deuxième", "troisième"], completed);
    }

    [Fact]
    public void ThirtyThousandRapidScrollRequestsAreCoalescedIntoOneLoad()
    {
        var gate = new SingleFlightGate();
        var accepted = 0;

        Parallel.For(0, 30_000, _ =>
        {
            if (gate.TryEnter()) Interlocked.Increment(ref accepted);
        });

        Assert.Equal(1, accepted);
        gate.Exit();
        Assert.True(gate.TryEnter());
        gate.Exit();
    }

    private static SetupEntity CreateSetup(int index) => new()
    {
        Id = Guid.NewGuid(),
        OriginalFileName = $"setup-{index:D5}.sto",
        Provider = $"Provider {index % 7}",
        Category = index % 3 == 0 ? "GTP" : "GT3",
        Car = $"Car {index % 20}",
        Track = $"Track {index % 50}",
        Season = "2026 S3",
        Week = index % 13 + 1,
        WeekKind = SetupWeekKind.Numeric,
        SetupType = "Race",
        SizeInBytes = 8,
        Sha256 = index.ToString("x64"),
        ArchivePath = $@"C:\Archive\setup-{index:D5}.sto",
        Status = index % 2 == 0 ? SetupStatus.Valide : SetupStatus.AVerifier,
        DownloadedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(index)
    };

    private static ImportMonitoringService CreateMonitoringService(TestEnvironment environment)
    {
        var policy = new MonitoredFolderPolicy(Path.Combine(environment.RootPath, "Documents"));
        var sha256 = new Sha256Calculator();
        return new ImportMonitoringService(
            new ImportFolderMonitor(policy),
            new MonitoredFolderSettingsService(environment.Factory, policy),
            new StableFileAwaiter(alreadyStableAge: TimeSpan.Zero),
            new LibraryImportService(
                new SetupRepository(environment.Factory),
                sha256,
                new ArchiveFileManager(sha256),
                new SetupMetadataAnalyzer(),
                new ArchivePathBuilder(),
                new SecureZipExtractor(),
                new SecureRarExtractor()),
            _ => Task.FromResult<string?>(Path.Combine(environment.RootPath, "Archive")),
            new SynchronizationSelectionSettingsService(environment.Factory),
            new HymoMonitoringSettingsService(environment.Factory),
            new TrackTitanFolderResolver(Path.Combine(environment.RootPath, "Documents")),
            new MonitoredFileStateStore(environment.Factory));
    }

    private static string CreateTemporaryDirectory() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "IracingSetupManagerResilienceTests", Guid.NewGuid().ToString("N"))).FullName;

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(string rootPath, string databasePath, LocalSetupDbContextFactory factory)
        {
            RootPath = rootPath;
            DatabasePath = databasePath;
            Factory = factory;
        }

        public string RootPath { get; }
        public string DatabasePath { get; }
        public LocalSetupDbContextFactory Factory { get; }

        public static async Task<TestEnvironment> CreateAsync()
        {
            var root = CreateTemporaryDirectory();
            var database = Path.Combine(root, "setups.db");
            var factory = new LocalSetupDbContextFactory(database);
            await new SetupDatabase(factory).InitializeAsync();
            return new TestEnvironment(root, database, factory);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(RootPath, true);
            return ValueTask.CompletedTask;
        }
    }
}
