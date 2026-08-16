using System.Text.RegularExpressions;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class ArchivePathBuilder
{
    public string BuildDirectory(string archiveRoot, SetupMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRoot);
        ArgumentNullException.ThrowIfNull(metadata);

        return Path.Combine(
            Path.GetFullPath(archiveRoot),
            FormatSeasonFolder(metadata.Season),
            FormatWeekFolder(metadata.Week),
            SanitizeFolder(metadata.Track),
            SetupMetadataAnalyzer.ResolveIracingFolderName(metadata.Car, []) ?? SanitizeFolder(metadata.Car),
            SanitizeFolder(metadata.Provider));
    }

    private static string FormatWeekFolder(int? week) =>
        week is >= 1 and <= 13 ? $"Week {week:00}" : "Week inconnue";

    private static string FormatSeasonFolder(string? season)
    {
        var sanitized = SanitizeFolder(season ?? "Saison inconnue");
        return Regex.Replace(
            sanitized,
            @"^(?<year>\d{4})[ _-]*S(?<season>\d+)$",
            "${year}_S${season}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
