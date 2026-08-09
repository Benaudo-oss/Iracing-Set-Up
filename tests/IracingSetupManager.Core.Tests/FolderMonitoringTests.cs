using IracingSetupManager.Infrastructure.Files.Monitoring;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class FolderMonitoringTests
{
    [Fact]
    public async Task ScanFindsOnlyStoAndZipFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "setup.sto"), "setup");
            await File.WriteAllTextAsync(Path.Combine(root, "setups.zip"), "zip-placeholder");
            await File.WriteAllTextAsync(Path.Combine(root, "partial.crdownload"), "partial");
            await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "notes");
            using var monitor = new ImportFolderMonitor(new MonitoredFolderPolicy(Path.Combine(root, "Documents")));

            var files = await monitor.ScanAsync([new MonitoredFolder(root, ImportFolderKind.Downloads)]);

            Assert.Equal(2, files.Count);
            Assert.All(files, file => Assert.Contains(Path.GetExtension(file.FullPath), new[] { ".sto", ".zip" }));
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
                TimeSpan.FromSeconds(1));

            Assert.True(await awaiter.WaitAsync(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory() =>
        Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "IracingSetupManagerMonitoringTests",
            Guid.NewGuid().ToString("N"))).FullName;
}

