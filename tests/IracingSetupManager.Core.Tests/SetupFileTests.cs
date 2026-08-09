using IracingSetupManager.Core.Setups;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class SetupFileTests
{
    [Fact]
    public void NewSetupCanBePlacedInReviewQueue()
    {
        var setup = new SetupFile(
            Guid.NewGuid(),
            "original.sto",
            new string('a', 64),
            1024,
            @"Saison\Circuit\Voiture\Fournisseur\Race\original.sto",
            SetupStatus.AVerifier,
            DateTimeOffset.UtcNow);

        Assert.Equal(SetupStatus.AVerifier, setup.Status);
        Assert.Equal("original.sto", setup.OriginalFileName);
    }
}
