using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Settings;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class SynchronizationImportPolicyTests
{
    private static readonly SynchronizationSelection Gt3Only =
        new(["HYMO", "VRS"], ["GT3"]);

    [Theory]
    [InlineData("HYMO", "GT3", true)]
    [InlineData("VRS", "GT3", true)]
    [InlineData("HYMO", "GTP", false)]
    [InlineData("HYMO", "GTE", false)]
    [InlineData("SRS", "GT3", false)]
    [InlineData("À identifier", "GT3", true)]
    [InlineData("HYMO", "À identifier", false)]
    [InlineData("HYMO", "", false)]
    public void AppliesSelectedProvidersAndCategories(string provider, string category, bool expected)
    {
        var metadata = new SetupMetadata(provider, category, "Voiture", "Circuit", null, "2026 S3", "Race");

        Assert.Equal(expected, SynchronizationImportPolicy.Allows(Gt3Only, metadata));
    }

    [Fact]
    public void RejectsEveryKnownCategoryWhenNoneIsSelected()
    {
        var selection = new SynchronizationSelection(["HYMO"], []);
        var metadata = new SetupMetadata("HYMO", "GT3", "Voiture", "Circuit", null, "2026 S3", "Race");

        Assert.False(SynchronizationImportPolicy.Allows(selection, metadata));
    }
}
