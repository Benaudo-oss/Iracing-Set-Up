using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class RecognitionAliasTests
{
    [Fact]
    public async Task LearnedCarAndTrackAliasesAreUsedByFutureAnalysis()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await environment.Aliases.SaveAsync(RecognitionAliasKind.Car, "NSXE22", "Acura NSX GT3 EVO 22");
        await environment.Aliases.SaveAsync(RecognitionAliasKind.Track, "MyRoadAm", "Road America");

        var metadata = new SetupMetadataAnalyzer(null, environment.Aliases)
            .Analyze("HYMO_26S3_NSXE22_MyRoadAm_R.sto");

        Assert.Equal("Acura NSX GT3 EVO 22", metadata.Car);
        Assert.Equal("GT3", metadata.Category);
        Assert.Equal("Road America", metadata.Track);
    }

    [Fact]
    public async Task DuplicateAliasUpdatesInsteadOfCreatingASecondRow()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await environment.Aliases.SaveAsync(RecognitionAliasKind.Track, "MyTrack", "Road America");
        await environment.Aliases.SaveAsync(RecognitionAliasKind.Track, "my-track", "Watkins Glen");

        Assert.Single(environment.Aliases.Snapshot);
        Assert.Equal("Watkins Glen", environment.Aliases.Snapshot[0].CanonicalValue);
    }

    [Fact]
    public async Task CorrectionUpdatesMetadataMovesArchiveAndKeepsReviewStatus()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var sourceDirectory = Path.Combine(environment.Archive, "old");
        Directory.CreateDirectory(sourceDirectory);
        var source = Path.Combine(sourceDirectory, "custom.sto");
        await File.WriteAllTextAsync(source, "setup");
        var id = Guid.NewGuid();
        await using (var context = environment.Factory.Create())
        {
            context.ApplicationSettings.Add(new ApplicationSettingEntity
            {
                Key = "ArchivePath", Value = environment.Archive, UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            context.Setups.Add(new SetupEntity
            {
                Id = id, OriginalFileName = "custom.sto", Provider = "HYMO", Category = "À identifier",
                Car = "À identifier", Track = "À identifier", SetupType = "Race", SizeInBytes = 5,
                Sha256 = new string('a', 64), ArchivePath = source, Status = SetupStatus.AVerifier,
                DownloadedAtUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var service = new SetupCorrectionService(environment.Factory, new ArchivePathBuilder(), environment.Aliases);
        await service.CorrectAsync(id, new SetupCorrection(
            "HYMO", "GT3", "Acura NSX GT3 EVO 22", "Road America", null, "2026 S3", "Race",
            "NSXE22", "MyRoadAm"));

        await using var verify = environment.Factory.Create();
        var setup = await verify.Setups.SingleAsync(item => item.Id == id);
        Assert.Equal(SetupStatus.AVerifier, setup.Status);
        Assert.Equal("Acura NSX GT3 EVO 22", setup.Car);
        Assert.Equal("Road America", setup.Track);
        Assert.True(File.Exists(setup.ArchivePath));
        Assert.False(File.Exists(source));
        Assert.Contains(await verify.SetupChangeHistory.ToListAsync(), item => item.ChangeType == "CorrectionIdentification");
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(string root, LocalSetupDbContextFactory factory, RecognitionAliasService aliases)
        {
            Root = root; Factory = factory; Aliases = aliases; Archive = Path.Combine(root, "archive");
            Directory.CreateDirectory(Archive);
        }
        public string Root { get; }
        public string Archive { get; }
        public LocalSetupDbContextFactory Factory { get; }
        public RecognitionAliasService Aliases { get; }

        public static async Task<TestEnvironment> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "IracingSetupManagerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var aliases = new RecognitionAliasService(factory);
            await aliases.LoadAsync();
            return new TestEnvironment(root, factory, aliases);
        }

        public ValueTask DisposeAsync()
        {
            try { Directory.Delete(Root, true); } catch { }
            return ValueTask.CompletedTask;
        }
    }
}
