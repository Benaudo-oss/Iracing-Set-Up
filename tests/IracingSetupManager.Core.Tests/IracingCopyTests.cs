using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Iracing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class IracingCopyTests
{
    [Fact]
    public async Task PreviewContainsOnlyValidatedSetups()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync(environment.Ids, environment.Target, new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });
        var item = Assert.Single(plan);
        Assert.Equal(environment.ValidatedId, item.SetupId);
        Assert.EndsWith(Path.Combine("bmwlmdh", "Garage 61", "2026_S3", "Monza", "Grid & Go", "Week 07", "race.sto"), item.DestinationPath);
    }

    [Fact]
    public async Task CopyRequiresConfirmationAndKeepsOriginal()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target, new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.ExecuteAsync(plan, false));
        var result = await environment.Service.ExecuteAsync(plan, true);
        Assert.Equal(1, result.Copied);
        Assert.Equal("original", await File.ReadAllTextAsync(environment.Source));
        Assert.True(File.Exists(plan[0].DestinationPath));
    }

    [Fact]
    public async Task SuccessfulCopyIsRecordedAndCanBeCopiedAgain()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var firstPlan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId], environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });

        await environment.Service.ExecuteAsync(firstPlan, true);

        await using (var context = environment.Factory.Create())
        {
            var setup = (await context.Setups.FindAsync(environment.ValidatedId))!;
            Assert.NotNull(setup.LastCopiedToIracingAtUtc);
            Assert.Equal(1, setup.IracingCopyCount);
        }

        var secondPlan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId], environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 8 });
        await environment.Service.ExecuteAsync(secondPlan, true);

        await using var verification = environment.Factory.Create();
        var recopiedSetup = (await verification.Setups.FindAsync(environment.ValidatedId))!;
        Assert.Equal(2, recopiedSetup.IracingCopyCount);
        Assert.True(File.Exists(secondPlan[0].DestinationPath));
    }

    [Fact]
    public async Task TeamCopyUsesExactGarage61TeamFolderAndSeparateHistory()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId],
            environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 7 },
            teamName: "BENAUDO Racing");

        Assert.EndsWith(
            Path.Combine("bmwlmdh", "Garage 61 - BENAUDO Racing", "2026_S3", "Monza", "GNG", "week_7", "race.sto"),
            plan[0].DestinationPath);

        await environment.Service.ExecuteAsync(plan, true, IracingCopyTarget.Team);

        await using var context = environment.Factory.Create();
        var setup = (await context.Setups.FindAsync(environment.ValidatedId))!;
        Assert.Equal(0, setup.IracingCopyCount);
        Assert.Null(setup.LastCopiedToIracingAtUtc);
        Assert.Equal(1, setup.IracingTeamCopyCount);
        Assert.NotNull(setup.LastCopiedToIracingTeamAtUtc);
    }

    [Theory]
    [InlineData(1, "week_1")]
    [InlineData(5, "week_5")]
    [InlineData(9, "week_9")]
    [InlineData(12, "week_12")]
    public async Task TeamCopyUsesGarage61WeekFolderFormat(int week, string expectedFolder)
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId],
            environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = week },
            teamName: "BENAUDO Racing");

        Assert.Contains(Path.Combine("GNG", expectedFolder, "race.sto"), plan[0].DestinationPath);
    }

    [Fact]
    public async Task ConflictMustBeResolvedAndKeepBothNeverOverwrites()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var destination = Path.Combine(environment.Target, "bmwlmdh", "Garage 61", "2026_S3", "Monza", "Grid & Go", "Week 07", "race.sto");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(destination, "existing");
        var plan = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target, new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });
        Assert.True(plan[0].HasConflict);
        await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.ExecuteAsync(plan, true));
        await environment.Service.ExecuteAsync(plan.Select(item => item with { ConflictChoice = IracingConflictChoice.KeepBoth }).ToList(), true);
        Assert.Equal("existing", await File.ReadAllTextAsync(destination));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(destination)!, "race (2).sto")));
    }

    [Fact]
    public async Task SetupMustStillBeValidatedAtExecutionTime()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target, new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });
        await using (var context = environment.Factory.Create())
        {
            (await context.Setups.FindAsync(environment.ValidatedId))!.Status = SetupStatus.Refuse;
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.ExecuteAsync(plan, true));
        Assert.False(File.Exists(plan[0].DestinationPath));
    }

    [Fact]
    public async Task UnknownWeekMustBeProvidedBetweenOneAndThirteen()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var unknown = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target);
        Assert.Null(unknown[0].Week);
        await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.ExecuteAsync(unknown, true));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target, new Dictionary<Guid, int> { [environment.ValidatedId] = 14 }));
    }

    [Theory]
    [InlineData(SetupWeekKind.Nec, "Week NEC")]
    [InlineData(SetupWeekKind.NoWeek, "Sans Week")]
    public async Task SpecialWeekChoiceIsPersistedAndCanBeCopied(SetupWeekKind kind, string expectedFolder)
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var choice = kind == SetupWeekKind.Nec ? SetupWeekChoice.Nec : SetupWeekChoice.NoWeek;

        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId], environment.Target,
            weekChoices: new Dictionary<Guid, SetupWeekChoice> { [environment.ValidatedId] = choice });

        Assert.Equal(kind, plan[0].WeekKind);
        Assert.Contains(expectedFolder, plan[0].DestinationPath);
        await using var context = environment.Factory.Create();
        var stored = await context.Setups.FindAsync(environment.ValidatedId);
        Assert.Equal(kind, stored!.WeekKind);
    }

    [Theory]
    [InlineData(5, SetupWeekKind.Numeric, "week_5")]
    [InlineData(null, SetupWeekKind.Nec, "week_NEC")]
    [InlineData(null, SetupWeekKind.NoWeek, "sans_week")]
    [InlineData(null, SetupWeekKind.Unknown, "week_inconnue")]
    public async Task TeamCopyUsesExactWeekFolderNames(int? week, SetupWeekKind kind, string expectedFolder)
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using (var context = environment.Factory.Create())
        {
            var setup = await context.Setups.FindAsync(environment.ValidatedId);
            setup!.Week = week;
            setup.WeekKind = kind;
            await context.SaveChangesAsync();
        }

        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId], environment.Target, teamName: "Team Test");

        Assert.Contains(Path.DirectorySeparatorChar + expectedFolder + Path.DirectorySeparatorChar, plan[0].DestinationPath);
    }

    [Fact]
    public async Task ARecognizedWeekIsNeverReplacedByAGroupedChoice()
    {
        await using var environment = await TestEnvironment.CreateAsync("26S3-W07-GnG-Monza-BMWGTP-R-Safe.sto");

        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId], environment.Target,
            weekChoices: new Dictionary<Guid, SetupWeekChoice> { [environment.ValidatedId] = SetupWeekChoice.Nec });

        Assert.Equal(7, plan[0].Week);
        Assert.Equal(SetupWeekKind.Numeric, plan[0].WeekKind);
    }

    [Fact]
    public async Task WeekIsReadFromFileName()
    {
        await using var environment = await TestEnvironment.CreateAsync("26S3-W07-GnG-Monza-BMWGTP-R-Safe.sto");
        var plan = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target);
        Assert.Equal(7, plan[0].Week);
        Assert.Contains(Path.Combine("Week 07", "26S3-W07-GnG-Monza-BMWGTP-R-Safe.sto"), plan[0].DestinationPath);
    }

    [Fact]
    public async Task UserCanChangeDynamicFolderOrder()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var layout = new IracingPathLayoutService(environment.Factory);
        await layout.SaveAsync(["Provider", "Week", "Season", "Track"]);

        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId],
            environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });

        Assert.EndsWith(Path.Combine("bmwlmdh", "Garage 61", "Grid & Go", "Week 07", "2026_S3", "Monza", "race.sto"), plan[0].DestinationPath);
    }

    [Fact]
    public async Task DynamicFolderOrderRejectsMissingOrDuplicateElements()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var layout = new IracingPathLayoutService(environment.Factory);
        await Assert.ThrowsAsync<ArgumentException>(() => layout.SaveAsync(["Season", "Season", "Week"]));
    }

    [Fact]
    public async Task ExistingLayoutIsAutomaticallyUpgradedWithTrack()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using (var context = environment.Factory.Create())
        {
            context.ApplicationSettings.Add(new ApplicationSettingEntity
            {
                Key = "Iracing.CopyPathLayout",
                Value = "[\"Season\",\"Provider\",\"Week\"]",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var layout = await new IracingPathLayoutService(environment.Factory).GetAsync();

        Assert.Equal(["Season", "Track", "Provider", "Week"], layout);
    }

    [Theory]
    [InlineData("2025 S12", "2025_S12")]
    [InlineData("2027 S5", "2027_S5")]
    public async Task CopyPathUsesFullSeasonNumber(string season, string expectedFolder)
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using (var context = environment.Factory.Create())
        {
            (await context.Setups.FindAsync(environment.ValidatedId))!.Season = season;
            await context.SaveChangesAsync();
        }

        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId],
            environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });

        Assert.Contains(Path.Combine("Garage 61", expectedFolder, "Monza", "Grid & Go"), plan[0].DestinationPath);
    }

    [Fact]
    public async Task CopyUsesTheActualIracingCarFolderRegardlessOfDetectedAlias()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        const string actualFolder = "acuransxevo22gt3";
        Directory.CreateDirectory(Path.Combine(environment.Target, actualFolder));
        await using (var context = environment.Factory.Create())
        {
            (await context.Setups.FindAsync(environment.ValidatedId))!.Car = "Acura NSX GT3 Evo 22";
            await context.SaveChangesAsync();
        }

        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId],
            environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });

        Assert.Contains(Path.Combine(environment.Target, actualFolder, "Garage 61"), plan[0].DestinationPath);
    }

    [Fact]
    public async Task SetupWithUnknownCarIsExcludedFromCopyPreview()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using (var context = environment.Factory.Create())
        {
            (await context.Setups.FindAsync(environment.ValidatedId))!.Car = "À identifier";
            await context.SaveChangesAsync();
        }

        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId],
            environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });

        Assert.Empty(plan);
    }

    [Fact]
    public async Task CopyIsBlockedIfCarBecomesUnknownAfterPreview()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync(
            [environment.ValidatedId],
            environment.Target,
            new Dictionary<Guid, int> { [environment.ValidatedId] = 7 });
        await using (var context = environment.Factory.Create())
        {
            (await context.Setups.FindAsync(environment.ValidatedId))!.Car = "À identifier";
            await context.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            environment.Service.ExecuteAsync(plan, true));

        Assert.Contains("identifiée", exception.Message);
        Assert.False(File.Exists(plan[0].DestinationPath));
    }

    [Fact]
    public async Task InterruptedBatchKeepsCompletedCopiesAndCanResumeRemainingFiles()
    {
        await using var environment = await TestEnvironment.CreateAsync("first.sto");
        var secondId = await environment.AddValidatedAsync("second.sto", "second");
        var ids = new[] { environment.ValidatedId, secondId };
        var weeks = ids.ToDictionary(id => id, _ => 7);
        var failingFiles = new FailOnCopyAtomicFileOperations(2);
        var interruptedService = new IracingCopyService(environment.Factory, atomicFileOperations: failingFiles);
        var plan = await interruptedService.CreatePlanAsync(ids, environment.Target, weeks);

        await Assert.ThrowsAsync<IOException>(() => interruptedService.ExecuteAsync(plan, true));

        await using (var context = environment.Factory.Create())
        {
            var completed = await context.Setups.SingleAsync(item => item.Id == plan[0].SetupId);
            var interrupted = await context.Setups.SingleAsync(item => item.Id == plan[1].SetupId);
            Assert.Equal(1, completed.IracingCopyCount);
            Assert.Equal(0, interrupted.IracingCopyCount);
            Assert.Single(await context.SetupChangeHistory.Where(item => item.SetupId == completed.Id).ToListAsync());
        }
        Assert.True(File.Exists(plan[0].DestinationPath));
        Assert.False(File.Exists(plan[1].DestinationPath));

        var remaining = await environment.Service.CreatePlanAsync([plan[1].SetupId], environment.Target, weeks);
        var resumed = await environment.Service.ExecuteAsync(remaining, true);

        Assert.Equal(1, resumed.Copied);
        Assert.True(File.Exists(remaining[0].DestinationPath));
    }

    private sealed class FailOnCopyAtomicFileOperations(int failureCall) : IAtomicFileOperations
    {
        private readonly AtomicFileOperations inner = new();
        private int calls;

        public Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref calls) == failureCall)
                throw new IOException("Interruption simulée pendant la copie.");
            return inner.CopyAsync(sourcePath, destinationPath, cancellationToken);
        }

        public Task WriteAsync(string destinationPath, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken = default) =>
            inner.WriteAsync(destinationPath, write, cancellationToken);
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private readonly string root;
        private TestEnvironment(string root, LocalSetupDbContextFactory factory, Guid validatedId, Guid rejectedId, string validFileName)
        {
            this.root = root; Factory = factory; ValidatedId = validatedId; Ids = [validatedId, rejectedId];
            Source = Path.Combine(root, "archive", validFileName); Target = Path.Combine(root, "iRacing", "setups");
            Service = new IracingCopyService(factory);
        }
        public LocalSetupDbContextFactory Factory { get; }
        public IracingCopyService Service { get; }
        public Guid ValidatedId { get; }
        public Guid[] Ids { get; }
        public string Source { get; }
        public string Target { get; }

        public async Task<Guid> AddValidatedAsync(string fileName, string contents)
        {
            var id = Guid.NewGuid();
            var path = Path.Combine(root, "archive", fileName);
            await File.WriteAllTextAsync(path, contents);
            await using var context = Factory.Create();
            context.Setups.Add(Create(id, fileName, SetupStatus.Valide, path));
            await context.SaveChangesAsync();
            return id;
        }

        public static async Task<TestEnvironment> CreateAsync(string validFileName = "race.sto")
        {
            var root = Path.Combine(Path.GetTempPath(), "IracingCopyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "archive"));
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var valid = Guid.NewGuid(); var rejected = Guid.NewGuid();
            await File.WriteAllTextAsync(Path.Combine(root, "archive", validFileName), "original");
            await File.WriteAllTextAsync(Path.Combine(root, "archive", "rejected.sto"), "rejected");
            await using var context = factory.Create();
            context.Setups.AddRange(Create(valid, validFileName, SetupStatus.Valide, Path.Combine(root, "archive", validFileName)), Create(rejected, "rejected.sto", SetupStatus.Refuse, Path.Combine(root, "archive", "rejected.sto")));
            await context.SaveChangesAsync();
            return new TestEnvironment(root, factory, valid, rejected, validFileName);
        }
        private static SetupEntity Create(Guid id, string name, SetupStatus status, string path) => new()
        {
            Id = id, OriginalFileName = name, Provider = "Grid & Go", Category = "GTP", Car = "BMW M Hybrid V8", Track = "Monza", Season = "2026 S3", SetupType = "Race",
            SizeInBytes = 8, Sha256 = id.ToString("N").PadRight(64, '0'), ArchivePath = path, Status = status, DownloadedAtUtc = DateTimeOffset.UtcNow
        };
        public ValueTask DisposeAsync() { Directory.Delete(root, true); return ValueTask.CompletedTask; }
    }
}
