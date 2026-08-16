using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Infrastructure.Settings;

namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed class TrackTitanFolderResolver(string? documentsPath = null)
{
    private readonly string setupsRoot = Path.Combine(
        documentsPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "iRacing",
        "setups");

    public IReadOnlyList<MonitoredFolder> Resolve(SynchronizationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!selection.Providers.Contains("HYMO", StringComparer.OrdinalIgnoreCase)) return [];

        var categories = selection.Categories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SetupCatalog.Cars
            .Where(car => categories.Contains(car.Category))
            .Select(car => new MonitoredFolder(
                Path.Combine(setupsRoot, car.IracingFolder, "Track Titan"),
                ImportFolderKind.TrackTitan,
                "HYMO"))
            .DistinctBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
