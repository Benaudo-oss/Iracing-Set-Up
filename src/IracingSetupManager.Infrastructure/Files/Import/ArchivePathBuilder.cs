namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class ArchivePathBuilder
{
    public string BuildDirectory(string archiveRoot, SetupMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRoot);
        ArgumentNullException.ThrowIfNull(metadata);

        return Path.Combine(
            Path.GetFullPath(archiveRoot),
            SanitizeFolder(metadata.Season ?? "Saison inconnue"),
            SanitizeFolder(metadata.Track),
            SanitizeFolder(metadata.Car),
            SanitizeFolder(metadata.Provider));
    }

    private static string SanitizeFolder(string value)
    {
        var sanitized = value.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidCharacter, '_');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "À identifier" : sanitized;
    }
}
