using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Files;
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
}
