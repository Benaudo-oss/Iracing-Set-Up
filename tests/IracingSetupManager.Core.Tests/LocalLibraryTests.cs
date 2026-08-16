using System.IO.Compression;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class LocalLibraryTests
{
    [Fact]
    public async Task ImportsWithoutRenamingMovingOrDeletingSource()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var source = Path.Combine(environment.SourcePath, "HYMO_2026S3_GT3_Race.sto");
        await File.WriteAllTextAsync(source, "setup-content");

        var result = Assert.Single(await environment.Service.ImportAsync(
            source,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder,
            new SetupMetadata("HYMO", "GT3", "Porsche 911 GT3 R", "Spa", "Grand Prix", "2026 S3", "Race")));

        Assert.Equal(SetupImportOutcome.Imported, result.Outcome);
        Assert.True(File.Exists(source));
        Assert.NotNull(result.ArchivePath);
        Assert.Equal(Path.GetFileName(source), Path.GetFileName(result.ArchivePath));
        Assert.Contains(Path.Combine("2026_S3", "Week inconnue", "Spa", "porsche911rgt3", "HYMO"), result.ArchivePath);
    }

    [Theory]
    [InlineData("nom totalement libre.STO")]
    [InlineData("HYMO_GTS_26S12_M4GT3_LeMans_ER.sto")]
    [InlineData("123456789.sto")]
    public async Task ImportsEveryStoFileRegardlessOfItsName(string fileName)
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var source = Path.Combine(environment.SourcePath, fileName);
        await File.WriteAllTextAsync(source, $"content-{fileName}");

        var result = Assert.Single(await environment.Service.ImportAsync(
            source,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder));
        var setup = Assert.Single(await new SetupQueryService(environment.Factory).GetAllAsync());

        Assert.Equal(SetupImportOutcome.Imported, result.Outcome);
        Assert.Equal(fileName, setup.OriginalFileName);
        Assert.Equal(SetupStatus.AVerifier, setup.Status);
        Assert.True(File.Exists(result.ArchivePath));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task IdenticalContentIsDetectedAsDuplicate()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var first = Path.Combine(environment.SourcePath, "first.sto");
        var second = Path.Combine(environment.SourcePath, "second.sto");
        await File.WriteAllTextAsync(first, "identical");
        await File.WriteAllTextAsync(second, "identical");
        var metadata = new SetupMetadata("GO Setups", "GTP", "Porsche 963", "Daytona", null, "2026 S3", "Race");

        await environment.Service.ImportAsync(first, environment.ArchivePath, SetupSourceKind.DownloadsFolder, metadata);
        var duplicate = Assert.Single(await environment.Service.ImportAsync(
            second,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder,
            metadata));

        Assert.Equal(SetupImportOutcome.Duplicate, duplicate.Outcome);
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task ReimportRestoresMissingSetupAndReturnsItToReview()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var source = Path.Combine(environment.SourcePath, "HYMO_26S3_ARX06_Fuji_WR.sto");
        await File.WriteAllTextAsync(source, "restorable-content");
        var first = Assert.Single(await environment.Service.ImportAsync(
            source,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder));
        File.Delete(first.ArchivePath!);
        await new SetupLibraryIntegrityService(environment.Factory).MarkMissingFilesAsync();

        var restored = Assert.Single(await environment.Service.ImportAsync(
            source,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder));
        var setup = Assert.Single(await new SetupQueryService(environment.Factory).GetToReviewAsync());

        Assert.Equal(SetupImportOutcome.Imported, restored.Outcome);
        Assert.Equal(SetupStatus.AVerifier, setup.Status);
        Assert.True(File.Exists(setup.ArchivePath));
    }

    [Fact]
    public async Task ExistingArchiveIsMovedSoProviderIsTheLastFolder()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var oldDirectory = Directory.CreateDirectory(Path.Combine(
            environment.ArchivePath, "2026 S3", "Le Mans", "BMW M4 GT3", "VRS", "Race V2")).FullName;
        var oldPath = Path.Combine(oldDirectory, "VRS_26S3PG_M4GT3_LeMans_R1_V2.sto");
        await File.WriteAllTextAsync(oldPath, "archive-to-move");
        var hash = await new Sha256Calculator().CalculateAsync(oldPath);
        await new SetupRepository(environment.Factory).AddAsync(new IracingSetupManager.Infrastructure.Database.Entities.SetupEntity
        {
            Id = Guid.NewGuid(), OriginalFileName = Path.GetFileName(oldPath), Provider = "VRS", Category = "GT3",
            Car = "BMW M4 GT3", Track = "Le Mans", Season = "2026 S3", SetupType = "Race V2",
            SizeInBytes = new FileInfo(oldPath).Length, Sha256 = hash, ArchivePath = oldPath,
            Status = SetupStatus.AVerifier, DownloadedAtUtc = DateTimeOffset.UtcNow
        });

        var moved = await new ArchiveReorganizationService(
            environment.Factory,
            new ArchivePathBuilder(),
            new Sha256Calculator()).ReorganizeAsync(environment.ArchivePath);
        var setup = Assert.Single(await new SetupQueryService(environment.Factory).GetAllAsync());

        Assert.Equal(1, moved);
        Assert.False(File.Exists(oldPath));
        Assert.Equal(Path.Combine(environment.ArchivePath, "2026_S3", "Week inconnue", "Le Mans", "bmwm4gt3", "VRS", Path.GetFileName(oldPath)), setup.ArchivePath);
        Assert.True(File.Exists(setup.ArchivePath));
    }

    [Fact]
    public async Task SameNameWithDifferentContentUsesConflictFolder()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var folderOne = Directory.CreateDirectory(Path.Combine(environment.SourcePath, "one")).FullName;
        var folderTwo = Directory.CreateDirectory(Path.Combine(environment.SourcePath, "two")).FullName;
        var first = Path.Combine(folderOne, "race.sto");
        var second = Path.Combine(folderTwo, "race.sto");
        await File.WriteAllTextAsync(first, "content-one");
        await File.WriteAllTextAsync(second, "content-two");
        var metadata = new SetupMetadata("Grid & Go", "GT3", "Ferrari 296", "Monza", null, "2026 S3", "Race");

        var firstResult = Assert.Single(await environment.Service.ImportAsync(first, environment.ArchivePath, SetupSourceKind.DownloadsFolder, metadata));
        var secondResult = Assert.Single(await environment.Service.ImportAsync(second, environment.ArchivePath, SetupSourceKind.DownloadsFolder, metadata));

        Assert.NotEqual(firstResult.ArchivePath, secondResult.ArchivePath);
        Assert.Contains("Conflits", secondResult.ArchivePath);
        Assert.Equal("race.sto", Path.GetFileName(secondResult.ArchivePath));
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task RejectsZipPathTraversal()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var zipPath = Path.Combine(environment.SourcePath, "unsafe.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../outside.sto");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("unsafe");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => environment.Service.ImportAsync(
            zipPath,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder));
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public async Task SafelyExtractsAndImportsZipSetups()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var zipPath = Path.Combine(environment.SourcePath, "GO_2026S3_GT3_Race.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("Spa/Porsche/original-name.sto");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("zip-setup");
        }

        var result = Assert.Single(await environment.Service.ImportAsync(
            zipPath,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder,
            new SetupMetadata("GO Setups", "GT3", "Porsche 911 GT3 R", "Spa", null, "2026 S3", "Race")));

        Assert.Equal(SetupImportOutcome.Imported, result.Outcome);
        Assert.Equal("original-name.sto", Path.GetFileName(result.ArchivePath));
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public async Task ArchiveWithoutStoIsIgnoredBeforeExtractionLimitsAreEvaluated()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var zipPath = Path.Combine(environment.SourcePath, "software.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("installer.exe", CompressionLevel.SmallestSize);
            await using var stream = entry.Open();
            await stream.WriteAsync(new byte[1024 * 1024]);
        }

        var result = Assert.Single(await environment.Service.ImportAsync(
            zipPath, environment.ArchivePath, SetupSourceKind.DownloadsFolder));

        Assert.Equal(SetupImportOutcome.Unsupported, result.Outcome);
    }

    [Fact]
    public async Task DoesNotArchiveASetupRejectedBySynchronizationFilter()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var setupPath = Path.Combine(environment.SourcePath, "HYMO_26S3_ARX06_Fuji_R.sto");
        await File.WriteAllTextAsync(setupPath, "gtp-setup");

        var result = Assert.Single(await environment.Service.ImportAsync(
            setupPath,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder,
            cancellationToken: default,
            metadataFilter: metadata => metadata.Category == "GT3"));

        Assert.Equal(SetupImportOutcome.Filtered, result.Outcome);
        Assert.Empty(Directory.EnumerateFiles(environment.ArchivePath, "*.sto", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AppliesSynchronizationFilterToEverySetupInsideZip()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var zipPath = Path.Combine(environment.SourcePath, "setups.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            await using (var gt3 = new StreamWriter(archive.CreateEntry("VRS_M4GT3_R.sto").Open()))
            {
                await gt3.WriteAsync("gt3-setup");
            }
            await using (var gtp = new StreamWriter(archive.CreateEntry("HYMO_ARX06_R.sto").Open()))
            {
                await gtp.WriteAsync("gtp-setup");
            }
        }

        var results = await environment.Service.ImportAsync(
            zipPath,
            environment.ArchivePath,
            SetupSourceKind.DownloadsFolder,
            cancellationToken: default,
            metadataFilter: metadata => metadata.Category == "GT3");

        Assert.Contains(results, result => result.Outcome == SetupImportOutcome.Imported);
        Assert.Contains(results, result => result.Outcome == SetupImportOutcome.Filtered);
        Assert.Single(Directory.EnumerateFiles(environment.ArchivePath, "*.sto", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RejectsZipWithDuplicateDestinationNamesBeforeWriting()
    {
        await using var environment = await LibraryTestEnvironment.CreateAsync();
        var zipPath = Path.Combine(environment.SourcePath, "duplicates.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("same.sto");
            archive.CreateEntry("same.sto");
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => environment.Service.ImportAsync(zipPath, environment.ArchivePath, SetupSourceKind.DownloadsFolder));
        Assert.True(File.Exists(zipPath));
    }

    private sealed class LibraryTestEnvironment : IAsyncDisposable
    {
        private LibraryTestEnvironment(string rootPath, LocalSetupDbContextFactory factory, LibraryImportService service)
        {
            RootPath = rootPath;
            SourcePath = Directory.CreateDirectory(Path.Combine(rootPath, "Source")).FullName;
            ArchivePath = Directory.CreateDirectory(Path.Combine(rootPath, "Archive")).FullName;
            Factory = factory;
            Service = service;
        }

        public string RootPath { get; }
        public string SourcePath { get; }
        public string ArchivePath { get; }
        public LibraryImportService Service { get; }
        public LocalSetupDbContextFactory Factory { get; }

        public static async Task<LibraryTestEnvironment> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "IracingSetupManagerLibraryTests", Guid.NewGuid().ToString("N"));
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var sha = new Sha256Calculator();
            var service = new LibraryImportService(
                new SetupRepository(factory),
                sha,
                new ArchiveFileManager(sha),
                new SetupMetadataAnalyzer(),
                new ArchivePathBuilder(),
                new SecureZipExtractor(),
                new SecureRarExtractor());
            return new LibraryTestEnvironment(root, factory, service);
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
