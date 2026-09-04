namespace IracingSetupManager.Infrastructure.Files;

public static class EmptyArchiveDirectoryCleaner
{
    public static void RemoveEmptyParents(string filePath, string archiveRoot)
    {
        var root = SecurePath.GetFullPath(archiveRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Path.GetDirectoryName(SecurePath.EnsureChildOf(filePath, root));

        while (!string.IsNullOrWhiteSpace(current) &&
               !current.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            var directory = SecurePath.EnsureChildOf(current, root);
            try
            {
                if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any()) return;
                Directory.Delete(directory, recursive: false);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            current = Path.GetDirectoryName(directory);
        }
    }
}
