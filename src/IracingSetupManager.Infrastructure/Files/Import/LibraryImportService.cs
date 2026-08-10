using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class LibraryImportService(
    ISetupRepository repository,
    Sha256Calculator sha256Calculator,
    IArchiveFileManager archiveFileManager,
    SetupMetadataAnalyzer metadataAnalyzer,
    ArchivePathBuilder archivePathBuilder,
    SecureZipExtractor zipExtractor,
    SecureRarExtractor rarExtractor)
{
    public async Task<IReadOnlyList<SetupImportResult>> ImportAsync(
        string sourcePath,
        string archiveRoot,
        SetupSourceKind sourceKind,
        SetupMetadata? metadataDefaults = null,
        CancellationToken cancellationToken = default,
        Func<SetupMetadata, bool>? metadataFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var extension = Path.GetExtension(sourcePath);

        if (extension.Equals(".sto", StringComparison.OrdinalIgnoreCase))
        {
            return [await ImportSetupAsync(
                sourcePath,
                archiveRoot,
                sourceKind,
                metadataDefaults,
                cancellationToken,
                metadataFilter: metadataFilter)];
        }

        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".rar", StringComparison.OrdinalIgnoreCase))
        {
            return [new SetupImportResult(sourcePath, SetupImportOutcome.Unsupported)];
        }

        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "IracingSetupManager",
            "ZipImport",
            Guid.NewGuid().ToString("N"));

        try
        {
            var extractedFiles = extension.Equals(".rar", StringComparison.OrdinalIgnoreCase)
                ? await rarExtractor.ExtractAsync(sourcePath, temporaryRoot, cancellationToken)
                : await zipExtractor.ExtractAsync(sourcePath, temporaryRoot, cancellationToken);
            var results = new List<SetupImportResult>();

            foreach (var extractedFile in extractedFiles.Where(path =>
                         Path.GetExtension(path).Equals(".sto", StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(await ImportSetupAsync(
                    extractedFile,
                    archiveRoot,
                    sourceKind,
                    metadataDefaults,
                    cancellationToken,
                    originalSourcePath: sourcePath,
                    metadataFilter: metadataFilter));
            }

            return results.Count == 0
                ? [new SetupImportResult(sourcePath, SetupImportOutcome.Unsupported)]
                : results;
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    public async Task<IReadOnlyList<SetupImportResult>> ImportExistingDirectoryAsync(
        string sourceDirectory,
        string archiveRoot,
        SetupSourceKind sourceKind,
        SetupMetadata? metadataDefaults = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        var results = new List<SetupImportResult>();

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                     .Where(path => IsSupported(Path.GetExtension(path))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.AddRange(await ImportAsync(
                file,
                archiveRoot,
                sourceKind,
                metadataDefaults,
                cancellationToken));
        }

        return results;
    }

    private async Task<SetupImportResult> ImportSetupAsync(
        string setupPath,
        string archiveRoot,
        SetupSourceKind sourceKind,
        SetupMetadata? metadataDefaults,
        CancellationToken cancellationToken,
        string? originalSourcePath = null,
        Func<SetupMetadata, bool>? metadataFilter = null)
    {
        var metadata = metadataAnalyzer.Analyze(setupPath, metadataDefaults);
        if (metadataFilter is not null && !metadataFilter(metadata))
        {
            return new SetupImportResult(originalSourcePath ?? setupPath, SetupImportOutcome.Filtered);
        }

        var sha256 = await sha256Calculator.CalculateAsync(setupPath, cancellationToken);
        var existing = await repository.FindBySha256Async(sha256, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == SetupStatus.FichierManquant && !File.Exists(existing.ArchivePath))
            {
                return await RestoreMissingSetupAsync(
                    existing,
                    setupPath,
                    archiveRoot,
                    sourceKind,
                    metadataDefaults,
                    cancellationToken,
                    originalSourcePath);
            }

            return new SetupImportResult(
                originalSourcePath ?? setupPath,
                SetupImportOutcome.Duplicate,
                existing.ArchivePath,
                sha256);
        }

        var destinationDirectory = archivePathBuilder.BuildDirectory(archiveRoot, metadata);
        var archivePath = await archiveFileManager.CopyWithoutOverwriteAsync(
            setupPath,
            destinationDirectory,
            cancellationToken);
        var fileInfo = new FileInfo(setupPath);
        var isUnidentified = metadata.Provider.Equals("À identifier", StringComparison.OrdinalIgnoreCase);

        await repository.AddAsync(new SetupEntity
        {
            Id = Guid.NewGuid(),
            OriginalFileName = fileInfo.Name,
            Provider = metadata.Provider,
            Category = metadata.Category,
            Car = metadata.Car,
            Track = metadata.Track,
            TrackConfiguration = metadata.TrackConfiguration,
            Season = metadata.Season,
            SetupType = metadata.SetupType,
            SizeInBytes = fileInfo.Length,
            Sha256 = sha256,
            ArchivePath = archivePath,
            SourceKind = sourceKind,
            SourcePath = null,
            IsPrivate = isUnidentified,
            Garage61ExportApproved = false,
            Status = SetupStatus.AVerifier,
            DownloadedAtUtc = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)
        }, cancellationToken);

        return new SetupImportResult(
            originalSourcePath ?? setupPath,
            SetupImportOutcome.Imported,
            archivePath,
            sha256);
    }

    private async Task<SetupImportResult> RestoreMissingSetupAsync(
        SetupEntity existing,
        string setupPath,
        string archiveRoot,
        SetupSourceKind sourceKind,
        SetupMetadata? metadataDefaults,
        CancellationToken cancellationToken,
        string? originalSourcePath)
    {
        var metadata = metadataAnalyzer.Analyze(setupPath, metadataDefaults);
        var destinationDirectory = archivePathBuilder.BuildDirectory(archiveRoot, metadata);
        var archivePath = await archiveFileManager.CopyWithoutOverwriteAsync(
            setupPath,
            destinationDirectory,
            cancellationToken);
        var fileInfo = new FileInfo(setupPath);

        existing.OriginalFileName = fileInfo.Name;
        existing.Provider = metadata.Provider;
        existing.Category = metadata.Category;
        existing.Car = metadata.Car;
        existing.Track = metadata.Track;
        existing.TrackConfiguration = metadata.TrackConfiguration;
        existing.Season = metadata.Season;
        existing.SetupType = metadata.SetupType;
        existing.SizeInBytes = fileInfo.Length;
        existing.ArchivePath = archivePath;
        existing.SourceKind = sourceKind;
        existing.SourcePath = null;
        existing.IsPrivate = metadata.Provider.Equals("À identifier", StringComparison.OrdinalIgnoreCase);
        existing.Garage61ExportApproved = false;
        existing.Status = SetupStatus.AVerifier;
        existing.DownloadedAtUtc = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
        await repository.UpdateAsync(existing, cancellationToken);

        return new SetupImportResult(
            originalSourcePath ?? setupPath,
            SetupImportOutcome.Imported,
            archivePath,
            existing.Sha256);
    }

    private static bool IsSupported(string extension) =>
        extension.Equals(".sto", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".rar", StringComparison.OrdinalIgnoreCase);
}
