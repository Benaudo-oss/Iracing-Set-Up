namespace IracingSetupManager.Infrastructure.Files;

public static class SecurePath
{
    public static string GetFullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.IndexOf('\0') >= 0) throw new ArgumentException("Le chemin contient un caractère interdit.", nameof(path));
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new ArgumentException("Les chemins de périphérique Windows sont interdits.", nameof(path));
        return Path.GetFullPath(path);
    }

    public static string EnsureChildOf(string candidate, string parent)
    {
        var fullCandidate = GetFullPath(candidate);
        var fullParent = GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!fullCandidate.Equals(fullParent, StringComparison.OrdinalIgnoreCase) &&
            !fullCandidate.StartsWith(fullParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Le chemin sort du dossier autorisé.");
        return fullCandidate;
    }

    public static void ValidateArchiveEntry(string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        if (Path.IsPathRooted(entryName) || entryName.StartsWith('/') || entryName.StartsWith('\\'))
            throw new InvalidDataException("Un chemin absolu est interdit dans une archive.");
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var segment in entryName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or ".." || segment.IndexOfAny(invalid) >= 0 || segment.Contains(':'))
                throw new InvalidDataException("L'archive contient un chemin interdit.");
        }
    }
}
