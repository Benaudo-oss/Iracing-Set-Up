using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Core.Presentation;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class PresentationLogicTests
{
    private static readonly SetupListItem Setup = new(
        "VRS_26S3_M4GT3_LeMans_R.sto",
        "VRS",
        "GT3",
        "BMW M4 GT3",
        "Le Mans",
        "Grand Prix",
        "2026 S3",
        "Race",
        "À vérifier");

    [Theory]
    [InlineData("M4GT3")]
    [InlineData("Grand Prix")]
    [InlineData("2026 S3")]
    [InlineData("à VÉRIFIER")]
    public void SearchCoversEveryVisibleMetadataField(string search) =>
        Assert.True(SetupListFilter.Matches(Setup, new SetupFilterCriteria(Search: search)));

    [Fact]
    public void FiltersAreCombinedAndCaseInsensitive()
    {
        Assert.True(SetupListFilter.Matches(
            Setup,
            new SetupFilterCriteria(Provider: "vrs", Category: "gt3", Track: "LE MANS")));
        Assert.False(SetupListFilter.Matches(Setup, new SetupFilterCriteria(Category: "GTP")));
    }

    [Fact]
    public void FilterOptionsAreUniqueSortedAndIgnoreEmptyValues()
    {
        var values = SetupListFilter.Options(["VRS", null, "", "HYMO", "vrs"]);
        Assert.Equal(["HYMO", "VRS"], values);
    }

    [Fact]
    public void CentralCatalogHasUniqueProvidersCategoriesCarsAndFolders()
    {
        Assert.Equal(SetupCatalog.Providers.Count, SetupCatalog.ProviderNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(SetupCatalog.Categories.Count, SetupCatalog.Categories.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(SetupCatalog.Cars.Count, SetupCatalog.Cars.Select(item => item.IracingFolder).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(SetupCatalog.Cars, car => Assert.Contains(car.Category, SetupCatalog.Categories));
        Assert.Equal("GNG", SetupCatalog.GetTeamFolderCode("Grid & Go"));
    }
}
