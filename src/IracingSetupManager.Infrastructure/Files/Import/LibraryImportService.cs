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
    SecureZipExtractor zipExtractor)
{
    public async Task<IReadOnlyList<SetupImportResult>> ImportAsync(
        string sourcePath,
        string archiveRoot,
        SetupSourceKind sourceKind,
        SetupMetadata? metadataDefaults = null,
        CancellationToken cancellationToken = default)
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
                cancellationToken)];
        }

        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
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
            var extractedFiles = await zipExtractor.ExtractAsync(
                sourcePath,
                temporaryRoot,
                cancellationToken);
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
                    originalSourcePath: sourcePath));
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
        string? originalSourcePath = null)
    {
        var sha256 = await sha256Calculator.CalculateAsync(setupPath, cancellationToken);
        var existing = await repository.FindBySha256Async(sha256, cancellationToken);
        if (existing is not null)
        {
            return new SetupImportResult(
                originalSourcePath ?? setupPath,
                SetupImportOutcome.Duplicate,
                existing.ArchivePath,
                sha256);
        }

        var metadata = metadataAnalyzer.Analyze(setupPath, metadataDefaults);
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
            SourcePath = originalSourcePath ?? Path.GetFullPath(setupPath),
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

    private static bool IsSupported(string extension) =>
        extension.Equals(".sto", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
}

