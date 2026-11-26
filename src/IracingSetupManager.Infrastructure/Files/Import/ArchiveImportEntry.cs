namespace IracingSetupManager.Infrastructure.Files.Import;

internal static class ArchiveImportEntry
{
    public static bool IsMacMetadata(string name)
    {
        var segments = name.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)) ||
            (segments.Length > 0 && segments[^1].StartsWith("._", StringComparison.Ordinal));
    }

    public static bool IsArchive(string name) =>
        Path.GetExtension(name).ToLowerInvariant() is ".zip" or ".rar";

    public static bool IsSupported(string name, bool includeNestedArchives) =>
        Path.GetExtension(name).Equals(".sto", StringComparison.OrdinalIgnoreCase) ||
        (includeNestedArchives && IsArchive(name));
}
