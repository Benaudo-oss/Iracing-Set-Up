namespace IracingSetupManager.Infrastructure.Files.Monitoring;

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

    private static bool IsSameOrChildOf(string candidate, string parent) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}

