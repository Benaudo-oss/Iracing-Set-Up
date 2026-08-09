using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Iracing;
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
