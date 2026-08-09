using System.Text.RegularExpressions;

namespace IracingSetupManager.Infrastructure.Logging;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var result = Authorization().Replace(value, "$1[MASQUÉ]");
        result = NamedSecret().Replace(result, "$1=[MASQUÉ]");
        result = Cookie().Replace(result, "$1: [MASQUÉ]");
        return result;
    }

    [GeneratedRegex("(?i)(authorization\\s*[:=]\\s*(?:bearer|basic)\\s+)[^\\s,;]+")]
    private static partial Regex Authorization();
    [GeneratedRegex("(?i)(password|motdepasse|passwd|token|secret|api[_-]?key|sessionid)\\s*=\\s*[^\\s,;&]+")]
    private static partial Regex NamedSecret();
    [GeneratedRegex("(?i)(cookie|set-cookie)\\s*:\\s*[^\\r\\n]+")]
    private static partial Regex Cookie();
}
