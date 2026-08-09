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
        var plan = await environment.Service.CreatePlanAsync(environment.Ids, environment.Target);
        var item = Assert.Single(plan);
        Assert.Equal(environment.ValidatedId, item.SetupId);
        Assert.EndsWith(Path.Combine("Porsche 911", "race.sto"), item.DestinationPath);
    }

    [Fact]
    public async Task CopyRequiresConfirmationAndKeepsOriginal()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target);
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
        var destination = Path.Combine(environment.Target, "Porsche 911", "race.sto");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(destination, "existing");
        var plan = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target);
        Assert.True(plan[0].HasConflict);
        await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.ExecuteAsync(plan, true));
        await environment.Service.ExecuteAsync(plan.Select(item => item with { ConflictChoice = IracingConflictChoice.KeepBoth }).ToList(), true);
        Assert.Equal("existing", await File.ReadAllTextAsync(destination));
        Assert.True(File.Exists(Path.Combine(environment.Target, "Porsche 911", "race (2).sto")));
    }

    [Fact]
    public async Task SetupMustStillBeValidatedAtExecutionTime()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var plan = await environment.Service.CreatePlanAsync([environment.ValidatedId], environment.Target);
        await using (var context = environment.Factory.Create())
        {
            (await context.Setups.FindAsync(environment.ValidatedId))!.Status = SetupStatus.Refuse;
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.ExecuteAsync(plan, true));
        Assert.False(File.Exists(plan[0].DestinationPath));
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private readonly string root;
        private TestEnvironment(string root, LocalSetupDbContextFactory factory, Guid validatedId, Guid rejectedId)
        {
            this.root = root; Factory = factory; ValidatedId = validatedId; Ids = [validatedId, rejectedId];
            Source = Path.Combine(root, "archive", "race.sto"); Target = Path.Combine(root, "iRacing", "setups");
            Service = new IracingCopyService(factory);
        }
        public LocalSetupDbContextFactory Factory { get; }
        public IracingCopyService Service { get; }
        public Guid ValidatedId { get; }
        public Guid[] Ids { get; }
        public string Source { get; }
        public string Target { get; }

        public static async Task<TestEnvironment> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "IracingCopyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "archive"));
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var valid = Guid.NewGuid(); var rejected = Guid.NewGuid();
            await File.WriteAllTextAsync(Path.Combine(root, "archive", "race.sto"), "original");
            await File.WriteAllTextAsync(Path.Combine(root, "archive", "rejected.sto"), "rejected");
            await using var context = factory.Create();
            context.Setups.AddRange(Create(valid, "race.sto", SetupStatus.Valide, Path.Combine(root, "archive", "race.sto")), Create(rejected, "rejected.sto", SetupStatus.Refuse, Path.Combine(root, "archive", "rejected.sto")));
            await context.SaveChangesAsync();
            return new TestEnvironment(root, factory, valid, rejected);
        }
        private static SetupEntity Create(Guid id, string name, SetupStatus status, string path) => new()
        {
            Id = id, OriginalFileName = name, Provider = "Test", Category = "GT3", Car = "Porsche 911", Track = "Spa", SetupType = "Race",
            SizeInBytes = 8, Sha256 = id.ToString("N").PadRight(64, '0'), ArchivePath = path, Status = status, DownloadedAtUtc = DateTimeOffset.UtcNow
        };
        public ValueTask DisposeAsync() { Directory.Delete(root, true); return ValueTask.CompletedTask; }
    }
}
