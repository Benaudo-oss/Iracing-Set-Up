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
    private readonly Channel<DetectedImportFile> queue = Channel.CreateUnbounded<DetectedImportFile>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly SemaphoreSlim manualScanLock = new(1, 1);
    private readonly SemaphoreSlim processingLock = new(1, 1);
    private CancellationTokenSource? cancellation;
    private CancellationTokenSource? manualScanCancellation;
    private Task? worker;
    private Task? periodicScanner;
    private IReadOnlyList<MonitoredFolder> folders = [];

    public event EventHandler<SetupImportResult>? ImportCompleted;
    public event EventHandler<Exception>? ImportFailed;
    public event EventHandler<SynchronizationProgress>? ProgressChanged;

    public bool IsManualScanRunning => manualScanCancellation is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (worker is not null) return;
        folders = await settingsService.GetAsync(cancellationToken);
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        folderMonitor.FileDetected += OnFileDetected;
        folderMonitor.Start(folders);
        worker = RunAsync(cancellation.Token);
        periodicScanner = RunPeriodicScanAsync(cancellation.Token);
        foreach (var file in await folderMonitor.ScanAsync(folders, cancellationToken))
            await queue.Writer.WriteAsync(file, cancellationToken);
    }

    public async Task<SynchronizationSummary> ImportNowAsync(CancellationToken cancellationToken = default)
    {
        if (!await manualScanLock.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Une synchronisation manuelle est déjà en cours.");
        var started = DateTimeOffset.UtcNow;
        var imported = 0; var duplicates = 0; var filtered = 0; var unsupported = 0; var errors = 0;
        IReadOnlyList<DetectedImportFile> files = [];
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        manualScanCancellation = linked;
        try
        {
            files = await folderMonitor.ScanAsync(await settingsService.GetAsync(linked.Token), linked.Token);
            foreach (var file in files) Raise(file.FullPath, SynchronizationFileState.Detected, "Détecté", 0, files.Count, false);
            var completed = 0;
            foreach (var file in files)
            {
                linked.Token.ThrowIfCancellationRequested();
                Raise(file.FullPath, SynchronizationFileState.Analyzing, "Analyse en cours", completed, files.Count, false);
                try
                {
                    var results = await ProcessFileAsync(file, linked.Token, false, false);
                    foreach (var result in results)
                    {
                        var state = ToState(result.Outcome);
                        if (state == SynchronizationFileState.Imported) imported++;
                        else if (state == SynchronizationFileState.Duplicate) duplicates++;
                        else if (state == SynchronizationFileState.Filtered) filtered++;
                        else unsupported++;
                        Raise(file.FullPath, state, ToMessage(state), completed + 1, files.Count, false, result);
                    }
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    errors++;
                    ImportFailed?.Invoke(this, exception);
                    Raise(file.FullPath, SynchronizationFileState.Error, exception.Message, completed + 1, files.Count, false);
                }
                completed++;
            }
            return Summary(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return Summary(true); }
        finally
        {
            manualScanCancellation = null;
            manualScanLock.Release();
        }

        SynchronizationSummary Summary(bool cancelled) =>
            new(files.Count, imported, duplicates, filtered, unsupported, errors, cancelled, DateTimeOffset.UtcNow - started);
    }

    public void CancelImportNow() => manualScanCancellation?.Cancel();

    public async Task StopAsync()
    {
        if (worker is null) return;
        folderMonitor.FileDetected -= OnFileDetected;
        folderMonitor.Stop();
        cancellation?.Cancel();
        try { await Task.WhenAll(worker, periodicScanner ?? Task.CompletedTask); }
        catch (OperationCanceledException) { }
        cancellation?.Dispose();
        cancellation = null; worker = null; periodicScanner = null;
    }

    public async ValueTask DisposeAsync()
    {
        CancelImportNow();
        await StopAsync();
        folderMonitor.Dispose();
        manualScanLock.Dispose();
        processingLock.Dispose();
    }

    private void OnFileDetected(object? sender, DetectedImportFile file) => queue.Writer.TryWrite(file);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var file in queue.Reader.ReadAllAsync(cancellationToken))
        {
            try { await ProcessFileAsync(file, cancellationToken, true); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                ImportFailed?.Invoke(this, exception);
                Raise(file.FullPath, SynchronizationFileState.Error, exception.Message, 0, 0, true);
            }
        }
    }

    private async Task<IReadOnlyList<SetupImportResult>> ProcessFileAsync(
        DetectedImportFile file, CancellationToken cancellationToken, bool automatic, bool raiseProgress = true)
    {
        await processingLock.WaitAsync(cancellationToken);
        try
        {
            return await ProcessFileCoreAsync(file, cancellationToken, automatic, raiseProgress);
        }
        finally { processingLock.Release(); }
    }

    private async Task<IReadOnlyList<SetupImportResult>> ProcessFileCoreAsync(
        DetectedImportFile file, CancellationToken cancellationToken, bool automatic, bool raiseProgress)
    {
        if (raiseProgress) Raise(file.FullPath, SynchronizationFileState.Analyzing, "Analyse automatique", 0, 0, automatic);
        if (!await stableFileAwaiter.WaitAsync(file.FullPath, cancellationToken)) return [];
        var archivePath = await getArchivePath(cancellationToken);
        if (string.IsNullOrWhiteSpace(archivePath)) return [];
        var sourceKind = file.SourceKind == ImportFolderKind.Downloads
            ? SetupSourceKind.DownloadsFolder : SetupSourceKind.OfficialProviderApplication;
        SetupMetadata? defaults = string.IsNullOrWhiteSpace(file.Provider) ? null
            : new SetupMetadata(file.Provider, "À identifier", "À identifier", "À identifier", null, null, "À identifier");
        var selection = await selectionSettings.GetAsync(cancellationToken);
        var results = await importService.ImportAsync(file.FullPath, archivePath, sourceKind, defaults, cancellationToken,
            metadata => SynchronizationImportPolicy.Allows(selection, metadata));
        foreach (var result in results)
        {
            ImportCompleted?.Invoke(this, result);
            if (raiseProgress)
            {
                var state = ToState(result.Outcome);
                Raise(file.FullPath, state, ToMessage(state), 0, 0, automatic, result);
            }
        }
        return results;
    }

    private async Task RunPeriodicScanAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(cancellationToken))
            foreach (var file in await folderMonitor.ScanAsync(folders, cancellationToken))
                await queue.Writer.WriteAsync(file, cancellationToken);
    }

    private void Raise(string path, SynchronizationFileState state, string message, int completed, int total, bool automatic, SetupImportResult? result = null) =>
        ProgressChanged?.Invoke(this, new SynchronizationProgress(path, state, message, completed, total, automatic, result));

    private static SynchronizationFileState ToState(SetupImportOutcome outcome) => outcome switch
    {
        SetupImportOutcome.Imported => SynchronizationFileState.Imported,
        SetupImportOutcome.Duplicate => SynchronizationFileState.Duplicate,
        SetupImportOutcome.Filtered => SynchronizationFileState.Filtered,
        _ => SynchronizationFileState.Unsupported
    };

    private static string ToMessage(SynchronizationFileState state) => state switch
    {
        SynchronizationFileState.Imported => "Importé dans À vérifier",
        SynchronizationFileState.Duplicate => "Déjà présent dans l’archive",
        SynchronizationFileState.Filtered => "Ignoré par les filtres",
        SynchronizationFileState.Unsupported => "Ignoré : aucun fichier .sto dans l’archive",
        _ => state.ToString()
    };
}
