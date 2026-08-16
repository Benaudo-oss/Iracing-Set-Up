namespace IracingSetupManager.Infrastructure.Files.Monitoring;

using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Infrastructure.Files;

public sealed class MonitoredFolderPolicy(string? documentsPath = null)
{
    private readonly string _iracingSetupsPath = Normalize(
        Path.Combine(
            documentsPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "iRacing",
            "setups"));

    public string Validate(MonitoredFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder.Path);

        var normalizedPath = Normalize(folder.Path);
        if (IsSameOrChildOf(normalizedPath, _iracingSetupsPath))
        {
            if (folder.Kind == ImportFolderKind.TrackTitan && IsAllowedTrackTitanPath(normalizedPath, folder.Provider))
            {
                return normalizedPath;
            }

            throw new InvalidOperationException(
                "Le dossier Documents\\iRacing\\setups et ses sous-dossiers ne peuvent pas être surveillés.");
        }

        if (folder.Kind == ImportFolderKind.OfficialProviderApplication &&
            string.IsNullOrWhiteSpace(folder.Provider))
        {
            throw new InvalidOperationException(
                "Un dossier d'application officielle doit être associé à un fournisseur.");
        }

        return normalizedPath;
    }

    private bool IsAllowedTrackTitanPath(string path, string? provider)
    {
        if (!string.Equals(provider, "HYMO", StringComparison.OrdinalIgnoreCase)) return false;

        return SetupCatalog.Cars.Any(car => path.Equals(
            Normalize(Path.Combine(_iracingSetupsPath, car.IracingFolder, "Track Titan")),
            StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameOrChildOf(string candidate, string parent) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(SecurePath.GetFullPath(path));
}
