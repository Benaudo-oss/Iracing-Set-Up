namespace IracingSetupManager.Infrastructure.Files.Import;

internal static class ArchiveImportEntry
{
    public static bool IsArchive(string name) =>
        Path.GetExtension(name).ToLowerInvariant() is ".zip" or ".rar";

    public static bool IsSupported(string name, bool includeNestedArchives) =>
        Path.GetExtension(name).Equals(".sto", StringComparison.OrdinalIgnoreCase) ||
        (includeNestedArchives && IsArchive(name));
}
