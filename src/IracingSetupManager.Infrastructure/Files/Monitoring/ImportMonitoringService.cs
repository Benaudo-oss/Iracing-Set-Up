using System.Threading.Channels;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Settings;

namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed class ImportMonitoringService(
    ImportFolderMonitor folderMonitor,
    MonitoredFolderSettingsService settingsService,
    StableFileAwaiter stableFileAwaiter,
    LibraryImportService importService,
    Func<CancellationToken, Task<string?>> getArchivePath,
    SynchronizationSelectionSettingsService selectionSettings) : IAsyncDisposable
{
    private readonly Channel<DetectedImportFile> _queue = Channel.CreateUnbounded<DetectedImportFile>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private Task? _periodicScanner;
    private IReadOnlyList<MonitoredFolder> _folders = [];

    public event EventHandler<SetupImportResult>? ImportCompleted;
    public event EventHandler<Exception>? ImportFailed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_worker is not null)
        {
            return;
        }

        _folders = await settingsService.GetAsync(cancellationToken);
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        folderMonitor.FileDetected += OnFileDetected;
        folderMonitor.Start(_folders);
        _worker = RunAsync(_cancellation.Token);
        _periodicScanner = RunPeriodicScanAsync(_cancellation.Token);

        foreach (var file in await folderMonitor.ScanAsync(_folders, cancellationToken))
        {
            await _queue.Writer.WriteAsync(file, cancellationToken);
        }
    }

    public async Task ImportNowAsync(CancellationToken cancellationToken = default)
    {
        var folders = await settingsService.GetAsync(cancellationToken);
        foreach (var file in await folderMonitor.ScanAsync(folders, cancellationToken))
        {
            try
            {
                await ProcessFileAsync(file, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ImportFailed?.Invoke(this, exception);
            }
        }
    }

    public async Task StopAsync()
    {
        if (_worker is null)
        {
            return;
        }

        folderMonitor.FileDetected -= OnFileDetected;
        folderMonitor.Stop();
        _cancellation?.Cancel();
        try
        {
            await Task.WhenAll(_worker, _periodicScanner ?? Task.CompletedTask);
        }
        catch (OperationCanceledException)
        {
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _worker = null;
        _periodicScanner = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        folderMonitor.Dispose();
    }

    private void OnFileDetected(object? sender, DetectedImportFile file) =>
        _queue.Writer.TryWrite(file);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var file in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessFileAsync(file, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ImportFailed?.Invoke(this, exception);
            }
        }
    }

    private async Task ProcessFileAsync(DetectedImportFile file, CancellationToken cancellationToken)
    {
        if (!await stableFileAwaiter.WaitAsync(file.FullPath, cancellationToken)) return;
        var archivePath = await getArchivePath(cancellationToken);
        if (string.IsNullOrWhiteSpace(archivePath)) return;

        var sourceKind = file.SourceKind == ImportFolderKind.Downloads
            ? SetupSourceKind.DownloadsFolder
            : SetupSourceKind.OfficialProviderApplication;
        SetupMetadata? defaults = string.IsNullOrWhiteSpace(file.Provider)
            ? null
            : new SetupMetadata(file.Provider, "À identifier", "À identifier", "À identifier", null, null, "À identifier");
        var selection = await selectionSettings.GetAsync(cancellationToken);
        var results = await importService.ImportAsync(
            file.FullPath,
            archivePath,
            sourceKind,
            defaults,
            cancellationToken,
            metadata => SynchronizationImportPolicy.Allows(selection, metadata));
        foreach (var result in results) ImportCompleted?.Invoke(this, result);
    }

    private async Task RunPeriodicScanAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            foreach (var file in await folderMonitor.ScanAsync(_folders, cancellationToken))
            {
                await _queue.Writer.WriteAsync(file, cancellationToken);
            }
        }
    }
}
