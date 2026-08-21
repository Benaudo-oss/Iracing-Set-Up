using System.Text.RegularExpressions;

namespace IracingSetupManager.Infrastructure.Logging;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var result = RedactUserPaths(value);
        result = Authorization().Replace(result, "$1[MASQUÉ]");
        result = NamedSecret().Replace(result, "$1=[MASQUÉ]");
        result = Cookie().Replace(result, "$1: [MASQUÉ]");
        return result;
    }

    private static string RedactUserPaths(string value)
    {
        var paths = new[]
        {
            (Path: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Replacement: "%LOCALAPPDATA%"),
            (Path: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Replacement: "%DOCUMENTS%"),
            (Path: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Replacement: "%USERPROFILE%")
        };

        var result = value;
        foreach (var item in paths
                     .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                     .DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(item => item.Path.Length))
        {
            result = result.Replace(item.Path, item.Replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    [GeneratedRegex("(?i)(authorization\\s*[:=]\\s*(?:bearer|basic)\\s+)[^\\s,;]+")]
    private static partial Regex Authorization();
    [GeneratedRegex("(?i)(password|motdepasse|passwd|token|secret|api[_-]?key|sessionid)\\s*=\\s*[^\\s,;&]+")]
    private static partial Regex NamedSecret();
    [GeneratedRegex("(?i)(cookie|set-cookie)\\s*:\\s*[^\\r\\n]+")]
    private static partial Regex Cookie();
}
