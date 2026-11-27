using IracingSetupManager.Core.Catalog;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed record TelemetryCompletion(string? Car);

public sealed class TelemetryCopyService(SetupMetadataAnalyzer analyzer, Sha256Calculator sha256)
{
    public const string FolderName = "Télémétrie";

    public static bool IsSupported(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".blap" or ".olap" or ".ibt";

    public static bool IsWithin(string path, string directory)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var fullPath = Path.GetFullPath(path);
        return fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SetupImportResult> CopyAsync(string source, string archiveRoot, SetupMetadata? defaults = null,
        CancellationToken cancellationToken = default, TelemetryCompletion? completion = null)
    {
        var root = SecurePath.EnsureChildOf(Path.Combine(archiveRoot, FolderName), archiveRoot);
        if (!IsSupported(source) || IsWithin(source, root))
            return new(source, SetupImportOutcome.Unsupported, Message: "Fichier de télémétrie hors périmètre.");

        var metadata = analyzer.Analyze(source, defaults);
        var car = metadata.Car;
        if (IsUnknown(car) && completion?.Car is { } selected)
            car = SetupCatalog.Cars.FirstOrDefault(item =>
                item.DisplayName.Equals(selected, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? car;
        if (IsUnknown(car))
            return new(source, SetupImportOutcome.Filtered, Week: metadata.Week, WeekKind: metadata.EffectiveWeekKind,
                Message: "Télémétrie non copiée : voiture à identifier.", NeedsTelemetryCar: true);

        var directory = SecurePath.EnsureChildOf(Path.Combine(root, SafeSegment(car)), root);
        for (var part = directory; part is not null && IsWithin(part, archiveRoot); part = Path.GetDirectoryName(part))
            if (Directory.Exists(part) && File.GetAttributes(part).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Un lien de dossier empêche la copie sécurisée de la télémétrie.");

        var filename = Path.GetFileName(source);
        var stem = Path.GetFileNameWithoutExtension(filename);
        var extension = Path.GetExtension(filename);
        string? sourceHash = null;
        for (var suffix = 1; ; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Path.Combine(directory, suffix == 1 ? filename : $"{stem} ({suffix}){extension}");
            if (File.Exists(destination))
            {
                if (File.GetAttributes(destination).HasFlag(FileAttributes.ReparsePoint)) continue;
                if (new FileInfo(source).Length != new FileInfo(destination).Length) continue;
                sourceHash ??= await sha256.CalculateAsync(source, cancellationToken);
                if (sourceHash == await sha256.CalculateAsync(destination, cancellationToken))
                    return new(source, SetupImportOutcome.Duplicate, destination, Week: metadata.Week,
                        WeekKind: metadata.EffectiveWeekKind, Message: "Télémétrie identique déjà présente.");
                continue;
            }
            if (Directory.Exists(destination)) continue;
            await new AtomicFileOperations().CopyAsync(source, destination, cancellationToken);
            return new(source, SetupImportOutcome.Imported, destination, Week: metadata.Week,
                WeekKind: metadata.EffectiveWeekKind, Message: "Copié dans le dossier Télémétrie.");
        }
    }

    private static bool IsUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals("À identifier", StringComparison.OrdinalIgnoreCase);

    private static string SafeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim(' ', '.');
        if (safe.Length == 0) throw new InvalidDataException("Nom de dossier de télémétrie invalide.");
        return safe;
    }
}
