using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Settings;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class FolderMonitoringTests
{
    [Fact]
    public async Task ScanFindsOnlyStoZipAndRarFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "setup.sto"), "setup");
            await File.WriteAllTextAsync(Path.Combine(root, "setups.zip"), "zip-placeholder");
            await File.WriteAllTextAsync(Path.Combine(root, "setups.rar"), "rar-placeholder");
            await File.WriteAllTextAsync(Path.Combine(root, "partial.crdownload"), "partial");
            await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "notes");
            using var monitor = new ImportFolderMonitor(new MonitoredFolderPolicy(Path.Combine(root, "Documents")));

            var files = await monitor.ScanAsync([new MonitoredFolder(root, ImportFolderKind.Downloads)]);

            Assert.Equal(3, files.Count);
            Assert.All(files, file => Assert.Contains(Path.GetExtension(file.FullPath), new[] { ".sto", ".zip", ".rar" }));
            Assert.All(files, file => Assert.NotNull(file.Snapshot));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanContinuesWhenOneConfiguredFolderIsRejected()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var documents = Path.Combine(root, "Documents");
            var validFolder = Directory.CreateDirectory(Path.Combine(root, "Downloads")).FullName;
            var setupPath = Path.Combine(validFolder, "setup.sto");
            await File.WriteAllTextAsync(setupPath, "setup");
            var forbiddenFolder = Path.Combine(documents, "iRacing", "setups", "private");
            using var monitor = new ImportFolderMonitor(new MonitoredFolderPolicy(documents));

            var result = await monitor.ScanWithDiagnosticsAsync(
            [
                new MonitoredFolder(forbiddenFolder, ImportFolderKind.Downloads),
                new MonitoredFolder(validFolder, ImportFolderKind.Downloads)
            ]);

            Assert.Single(result.Files);
            Assert.Equal(setupPath, result.Files[0].FullPath);
            Assert.Single(result.Failures);
            Assert.Equal(forbiddenFolder, result.Failures[0].Path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FolderExplorationHonorsCancellation()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            using var monitor = new ImportFolderMonitor(new MonitoredFolderPolicy(Path.Combine(root, "Documents")));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                monitor.ScanWithDiagnosticsAsync(
                    [new MonitoredFolder(root, ImportFolderKind.Downloads)],
                    cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AutomaticScanSkipsUnchangedFilesAfterRestartAndProcessesModifiedFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var downloads = Directory.CreateDirectory(Path.Combine(root, "Downloads")).FullName;
            var archive = Directory.CreateDirectory(Path.Combine(root, "Archive")).FullName;
            var source = Path.Combine(downloads, "VRS_26S3_M4GT3_LeMans_R.sto");
            await File.WriteAllTextAsync(source, "first version");
            File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(-1));
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "setups.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var policy = new MonitoredFolderPolicy(Path.Combine(root, "Documents"));
            await new MonitoredFolderSettingsService(factory, policy).SaveAsync(
                [new MonitoredFolder(downloads, ImportFolderKind.Downloads)]);
            await new HymoMonitoringSettingsService(factory).SaveAsync(false);

            await using (var first = CreateMonitoringService(factory, policy, archive, root))
            {
                var summary = await first.ImportNowAsync();
                Assert.Equal(1, summary.Imported);
            }

            await using (var restarted = CreateMonitoringService(factory, policy, archive, root))
            {
                var completed = 0;
                restarted.ImportCompleted += (_, _) => Interlocked.Increment(ref completed);
                await restarted.StartAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await restarted.WaitForInitialScanAsync(timeout.Token);
                await Task.Delay(200, timeout.Token);
                Assert.Equal(0, completed);
            }

            await File.WriteAllTextAsync(source, "modified version");
            File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(1));
            await using (var modified = CreateMonitoringService(factory, policy, archive, root))
            {
                var completion = new TaskCompletionSource<SetupImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                modified.ImportCompleted += (_, result) => completion.TrySetResult(result);
                await modified.StartAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await modified.WaitForInitialScanAsync(timeout.Token);
                var result = await completion.Task.WaitAsync(timeout.Token);
                Assert.Equal(SetupImportOutcome.Imported, result.Outcome);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WaitsUntilFileIsStableAndReadable()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "setup.sto");
            await File.WriteAllTextAsync(path, "complete");
            var awaiter = new StableFileAwaiter(
                TimeSpan.FromMilliseconds(10),
                requiredStableProbes: 2,
                TimeSpan.FromSeconds(10));

            Assert.True(await awaiter.WaitAsync(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AlreadyOldReadableFileDoesNotWaitForStabilityProbes()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "existing.sto");
            await File.WriteAllTextAsync(path, "complete");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));
            var awaiter = new StableFileAwaiter(
                TimeSpan.FromSeconds(1),
                requiredStableProbes: 3,
                TimeSpan.FromMilliseconds(50));

            Assert.True(await awaiter.WaitAsync(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ResumesDownloadAfterTemporaryFileIsRenamedAndAfterRestart()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var partial = Path.Combine(root, "race.sto.crdownload");
            var completed = Path.Combine(root, "race.sto");
            await File.WriteAllTextAsync(partial, "partial then complete");
            using var firstMonitor = new ImportFolderMonitor(new MonitoredFolderPolicy(Path.Combine(root, "Documents")));
            Assert.Empty(await firstMonitor.ScanAsync([new MonitoredFolder(root, ImportFolderKind.Downloads)]));

            File.Move(partial, completed);
            Assert.Single(await firstMonitor.ScanAsync([new MonitoredFolder(root, ImportFolderKind.Downloads)]));

            using var restartedMonitor = new ImportFolderMonitor(new MonitoredFolderPolicy(Path.Combine(root, "Documents")));
            var resumed = Assert.Single(await restartedMonitor.ScanAsync([new MonitoredFolder(root, ImportFolderKind.Downloads)]));
            Assert.Equal(completed, resumed.FullPath);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancelledArchiveCopyLeavesNoFinalOrTemporaryFile()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source.sto");
            var destination = Path.Combine(root, "archive");
            await File.WriteAllBytesAsync(source, new byte[1024 * 1024]);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new ArchiveFileManager(new Sha256Calculator()).CopyWithoutOverwriteAsync(source, destination, cancellation.Token));

            Assert.Empty(Directory.Exists(destination) ? Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories) : []);
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateTemporaryDirectory() =>
        Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "IracingSetupManagerMonitoringTests",
            Guid.NewGuid().ToString("N"))).FullName;

    private static ImportMonitoringService CreateMonitoringService(
        LocalSetupDbContextFactory factory,
        MonitoredFolderPolicy policy,
        string archive,
        string root)
    {
        var sha256 = new Sha256Calculator();
        var importer = new LibraryImportService(
            new SetupRepository(factory),
            sha256,
            new ArchiveFileManager(sha256),
            new SetupMetadataAnalyzer(),
            new ArchivePathBuilder(),
            new SecureZipExtractor(),
            new SecureRarExtractor());
        return new ImportMonitoringService(
            new ImportFolderMonitor(policy),
            new MonitoredFolderSettingsService(factory, policy),
            new StableFileAwaiter(alreadyStableAge: TimeSpan.Zero),
            importer,
            _ => Task.FromResult<string?>(archive),
            new SynchronizationSelectionSettingsService(factory),
            new HymoMonitoringSettingsService(factory),
            new TrackTitanFolderResolver(Path.Combine(root, "Documents")),
            new MonitoredFileStateStore(factory));
    }
}
