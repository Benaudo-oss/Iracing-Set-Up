using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class TelemetryCopyTests
{
    [Fact]
    public async Task ScanHonorsOptionAndExcludesTelemetryDestination()
    {
        var root = Directory.CreateTempSubdirectory("paddock-telemetry-scan-");
        try
        {
            var destination = Directory.CreateDirectory(Path.Combine(root.FullName, "Télémétrie")).FullName;
            var telemetry = Path.Combine(root.FullName, "source.ibt");
            var setup = Path.Combine(root.FullName, "source.sto");
            foreach (var path in new[] { telemetry, setup, Path.Combine(destination, "already-copied.ibt") })
                await File.WriteAllTextAsync(path, "data");
            var factory = new LocalSetupDbContextFactory(Path.Combine(root.FullName, "settings.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var settings = new TelemetryCopySettingsService(factory);
            Assert.False(await settings.IsEnabledAsync());
            using var monitor = new ImportFolderMonitor(new MonitoredFolderPolicy(Path.Combine(root.FullName, "Documents")));
            var folder = new MonitoredFolder(root.FullName, ImportFolderKind.Downloads) { TelemetryDestination = destination };
            Assert.Equal(setup, Assert.Single(await monitor.ScanAsync([folder])).FullPath);
            await settings.SaveAsync(true);
            var enabled = await new TelemetryCopySettingsService(factory).IsEnabledAsync();
            Assert.True(enabled);
            var found = await monitor.ScanAsync([folder with { IncludeTelemetry = enabled }]);
            Assert.Equal(new[] { telemetry, setup }, found.Select(file => file.FullPath).Order());
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(".blap")]
    [InlineData(".olap")]
    [InlineData(".ibt")]
    public async Task CopiesRecognizedTelemetryWithoutChangingItsSource(string extension)
    {
        var root = Directory.CreateTempSubdirectory("paddock-telemetry-");
        try
        {
            var source = Path.Combine(root.FullName, "VRS_M4GT3" + extension);
            await File.WriteAllTextAsync(source, "first");
            var archive = Path.Combine(root.FullName, "Library");
            var service = new TelemetryCopyService(new SetupMetadataAnalyzer(), new Sha256Calculator());
            var result = await service.CopyAsync(source, archive);
            Assert.Equal(SetupImportOutcome.Imported, result.Outcome);
            Assert.Equal(Path.Combine(archive, "Télémétrie", "BMW M4 GT3", Path.GetFileName(source)), result.ArchivePath);
            Assert.Equal("first", await File.ReadAllTextAsync(source));
            var duplicate = await service.CopyAsync(source, archive);
            Assert.Equal(SetupImportOutcome.Duplicate, duplicate.Outcome);
            Assert.False(duplicate.SourceFullyDuplicate);
            await File.WriteAllTextAsync(source, "other");
            var next = await service.CopyAsync(source, archive);
            Assert.Equal(SetupImportOutcome.Imported, next.Outcome);
            Assert.EndsWith(" (2)" + extension, next.ArchivePath);
            Assert.Equal("first", await File.ReadAllTextAsync(result.ArchivePath!));
            Assert.Equal("other", await File.ReadAllTextAsync(next.ArchivePath!));
            Assert.Equal(SetupImportOutcome.Unsupported, (await service.CopyAsync(next.ArchivePath!, archive)).Outcome);
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task ManualCarIsSavedAndReusedForUnchangedSource()
    {
        var root = Directory.CreateTempSubdirectory("paddock-telemetry-completion-");
        try
        {
            var source = Path.Combine(root.FullName, "session.ibt");
            await File.WriteAllTextAsync(source, "session");
            var factory = new LocalSetupDbContextFactory(Path.Combine(root.FullName, "settings.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var settings = new TelemetryCopySettingsService(factory);
            var service = new TelemetryCopyService(new SetupMetadataAnalyzer(), new Sha256Calculator());
            var blocked = await service.CopyAsync(source, root.FullName);
            Assert.True(blocked.NeedsTelemetryCar);

            var fields = new TelemetryCompletion("Ligier JS P320");
            var saved = await service.CopyAsync(source, root.FullName, completion: fields);
            Assert.Equal(SetupImportOutcome.Imported, saved.Outcome);
            Assert.Equal(Path.Combine(root.FullName, "Télémétrie", "Ligier JS P320", "session.ibt"), saved.ArchivePath);
            Assert.Equal("session", await File.ReadAllTextAsync(source));

            var info = new FileInfo(source);
            var snapshot = new MonitoredFileSnapshot(source, info.Length, info.LastWriteTimeUtc.Ticks);
            await settings.SaveCompletionAsync(snapshot, fields, CancellationToken.None);
            var reopened = new TelemetryCopySettingsService(factory);
            var restored = await reopened.ReadCompletionAsync(snapshot, CancellationToken.None);
            Assert.Equal(fields, restored);
            Assert.Equal(SetupImportOutcome.Duplicate,
                (await service.CopyAsync(source, root.FullName, completion: restored)).Outcome);
            Assert.Null(await reopened.ReadCompletionAsync(snapshot with { Length = snapshot.Length + 1 }, CancellationToken.None));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task MissingMetadataDoesNotCopyFile()
    {
        var root = Directory.CreateTempSubdirectory("paddock-telemetry-");
        try
        {
            var source = Path.Combine(root.FullName, "session.ibt");
            await File.WriteAllTextAsync(source, "session");
            var result = await new TelemetryCopyService(new SetupMetadataAnalyzer(), new Sha256Calculator())
                .CopyAsync(source, root.FullName);
            Assert.Equal(SetupImportOutcome.Filtered, result.Outcome);
            Assert.True(result.NeedsTelemetryCar);
            Assert.Contains("voiture à identifier", result.Message);
            Assert.False(Directory.Exists(Path.Combine(root.FullName, "Télémétrie")));
            Assert.True(File.Exists(source));
        }
        finally { root.Delete(recursive: true); }
    }
}
