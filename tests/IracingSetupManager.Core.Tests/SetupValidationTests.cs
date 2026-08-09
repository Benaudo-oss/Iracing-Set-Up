using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class SetupValidationTests
{
    [Fact]
    public async Task ValidatesAndRefusesIndividualSetupsWithHistory()
    {
        await using var environment = await ValidationEnvironment.CreateAsync(2);
        var service = new SetupValidationService(environment.Factory);

        await service.ValidateAsync(environment.SetupIds[0]);
        await service.RefuseAsync(environment.SetupIds[1]);

        await using var context = environment.Factory.Create();
        Assert.Equal(SetupStatus.Valide, (await context.Setups.FindAsync(environment.SetupIds[0]))!.Status);
        Assert.Equal(SetupStatus.Refuse, (await context.Setups.FindAsync(environment.SetupIds[1]))!.Status);
        Assert.Equal(2, await context.SetupChangeHistory.CountAsync());
    }

    [Fact]
    public async Task GroupedActionRequiresExplicitConfirmation()
    {
        await using var environment = await ValidationEnvironment.CreateAsync(2);
        var service = new SetupValidationService(environment.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ValidateManyAsync(environment.SetupIds, confirmed: false));

        await using var context = environment.Factory.Create();
        Assert.All(await context.Setups.ToListAsync(), setup => Assert.Equal(SetupStatus.AVerifier, setup.Status));
        Assert.Empty(await context.SetupChangeHistory.ToListAsync());
    }

    [Fact]
    public async Task ConfirmedGroupedActionChangesEverySetupAndWritesHistory()
    {
        await using var environment = await ValidationEnvironment.CreateAsync(3);
        var service = new SetupValidationService(environment.Factory);

        await service.ValidateManyAsync(environment.SetupIds, confirmed: true);

        await using var context = environment.Factory.Create();
        Assert.All(await context.Setups.ToListAsync(), setup => Assert.Equal(SetupStatus.Valide, setup.Status));
        Assert.Equal(3, await context.SetupChangeHistory.CountAsync());
    }

    [Fact]
    public async Task StoresRatingCommentAndTheirPreviousValuesInHistory()
    {
        await using var environment = await ValidationEnvironment.CreateAsync(1);
        var service = new SetupValidationService(environment.Factory);

        await service.UpdateNotesAsync(environment.SetupIds[0], 4, "Stable sur les relais longs");
        await service.UpdateNotesAsync(environment.SetupIds[0], 5, "Excellent");

        await using var context = environment.Factory.Create();
        var setup = await context.Setups.FindAsync(environment.SetupIds[0]);
        var history = await context.SetupChangeHistory.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(5, setup!.PersonalRating);
        Assert.Equal("Excellent", setup.Comment);
        Assert.Equal(2, history.Count);
        Assert.Equal(4, history[1].PreviousRating);
        Assert.Equal("Stable sur les relais longs", history[1].PreviousComment);
    }

    private sealed class ValidationEnvironment : IAsyncDisposable
    {
        private ValidationEnvironment(string root, LocalSetupDbContextFactory factory, IReadOnlyList<Guid> setupIds)
        {
            Root = root;
            Factory = factory;
            SetupIds = setupIds;
        }

        public string Root { get; }
        public LocalSetupDbContextFactory Factory { get; }
        public IReadOnlyList<Guid> SetupIds { get; }

        public static async Task<ValidationEnvironment> CreateAsync(int count)
        {
            var root = Path.Combine(Path.GetTempPath(), "IracingValidationTests", Guid.NewGuid().ToString("N"));
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
            await using var context = factory.Create();
            foreach (var id in ids)
            {
                context.Setups.Add(new SetupEntity
                {
                    Id = id,
                    OriginalFileName = $"{id:N}.sto",
                    Provider = "HYMO",
                    Category = "GT3",
                    Car = "Porsche",
                    Track = "Spa",
                    SetupType = "Race",
                    SizeInBytes = 1024,
                    Sha256 = id.ToString("N").PadRight(64, '0'),
                    ArchivePath = Path.Combine(root, $"{id:N}.sto"),
                    Status = SetupStatus.AVerifier,
                    DownloadedAtUtc = DateTimeOffset.UtcNow
                });
            }

            await context.SaveChangesAsync();
            return new ValidationEnvironment(root, factory, ids);
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}

