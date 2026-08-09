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
        Assert.Contains(Path.Combine("2026 S3", "Spa", "Porsche 911 GT3 R", "HYMO", "Race"), result.ArchivePath);
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
        private LibraryTestEnvironment(string rootPath, LibraryImportService service)
        {
            RootPath = rootPath;
            SourcePath = Directory.CreateDirectory(Path.Combine(rootPath, "Source")).FullName;
            ArchivePath = Directory.CreateDirectory(Path.Combine(rootPath, "Archive")).FullName;
            Service = service;
        }

        public string RootPath { get; }
        public string SourcePath { get; }
        public string ArchivePath { get; }
        public LibraryImportService Service { get; }

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
                new SecureZipExtractor());
            return new LibraryTestEnvironment(root, service);
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
