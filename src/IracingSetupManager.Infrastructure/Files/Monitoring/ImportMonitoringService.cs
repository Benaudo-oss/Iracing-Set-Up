using System.Threading.Channels;
using System.Collections.Concurrent;
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
    SynchronizationSelectionSettingsService selectionSettings,
    HymoMonitoringSettingsService hymoMonitoringSettings,
    TrackTitanFolderResolver trackTitanFolders,
    MonitoredFileStateStore fileStateStore) : IAsyncDisposable
{
    private const int QueueCapacity = 2048;
    private const int StateSaveBatchSize = 100;
    private static readonly TimeSpan StateSaveInterval = TimeSpan.FromSeconds(10);

    private readonly Channel<DetectedImportFile> queue = Channel.CreateBounded<DetectedImportFile>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    private readonly ConcurrentDictionary<string, byte> queuedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MonitoredFileFingerprint> examinedFiles = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MonitoredFileSnapshot> pendingFileStates = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim manualScanLock = new(1, 1);
    private readonly SemaphoreSlim processingLock = new(1, 1);
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim folderRefreshLock = new(1, 1);
    private readonly SemaphoreSlim fileStateLoadLock = new(1, 1);
    private readonly SemaphoreSlim fileStateSaveLock = new(1, 1);
    private CancellationTokenSource? cancellation;
    private CancellationTokenSource? manualScanCancellation;
    private Task? worker;
    private Task? initialScanner;
    private Task? periodicScanner;
    private Task? stateSaver;
    private IReadOnlyList<MonitoredFolder> folders = [];
    private bool fileStatesLoaded;
    private int queueSaturationReported;

    public event EventHandler<SetupImportResult>? ImportCompleted;
    public event EventHandler<Exception>? ImportFailed;
    public event EventHandler<SynchronizationProgress>? ProgressChanged;

    public bool IsManualScanRunning => manualScanCancellation is not null;

    public async Task WaitForInitialScanAsync(CancellationToken cancellationToken = default)
    {
        var scan = initialScanner;
        if (scan is not null) await scan.WaitAsync(cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (worker is not null) return;
            await EnsureFileStatesLoadedAsync(cancellationToken);
            folders = await ResolveFoldersAsync(cancellationToken);
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            folderMonitor.FileDetected += OnFileDetected;
            folderMonitor.MonitoringFailed += OnMonitoringFailed;
            try
            {
                folderMonitor.Start(folders);
                worker = RunAsync(cancellation.Token);
                initialScanner = ScanAndQueueChangedFilesSafelyAsync(folders, cancellation.Token);
                periodicScanner = RunPeriodicScanAsync(cancellation.Token);
                stateSaver = RunStateSaverAsync(cancellation.Token);
            }
            catch
            {
                folderMonitor.FileDetected -= OnFileDetected;
                folderMonitor.MonitoringFailed -= OnMonitoringFailed;
                folderMonitor.Stop();
                cancellation.Dispose();
                cancellation = null;
                throw;
            }
        }
        finally
        {
            lifecycleLock.Release();
        }
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
            await EnsureFileStatesLoadedAsync(linked.Token);
            var scan = await folderMonitor.ScanWithDiagnosticsAsync(
                await ResolveFoldersAsync(linked.Token),
                linked.Token);
            files = scan.Files;
            foreach (var failure in scan.Failures)
            {
                errors++;
                ReportFolderFailure(failure, automatic: false);
            }
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
                    NotifyImportFailed(exception);
                    Raise(file.FullPath, SynchronizationFileState.Error, exception.Message, completed + 1, files.Count, false);
                }
                completed++;
            }
            await TryFlushFileStatesAsync(linked.Token);
            return Summary(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return Summary(true); }
        finally
        {
            await TryFlushFileStatesAsync(CancellationToken.None);
            manualScanCancellation = null;
            manualScanLock.Release();
        }

        SynchronizationSummary Summary(bool cancelled) =>
            new(files.Count, imported, duplicates, filtered, unsupported, errors, cancelled, DateTimeOffset.UtcNow - started);
    }

    public void CancelImportNow() => manualScanCancellation?.Cancel();

    public async Task StopAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {
            if (worker is null) return;
            folderMonitor.FileDetected -= OnFileDetected;
            folderMonitor.MonitoringFailed -= OnMonitoringFailed;
            folderMonitor.Stop();
            cancellation?.Cancel();
            try
            {
                await Task.WhenAll(
                    worker,
                    initialScanner ?? Task.CompletedTask,
                    periodicScanner ?? Task.CompletedTask,
                    stateSaver ?? Task.CompletedTask);
            }
            catch (OperationCanceledException) { }
            await TryFlushFileStatesAsync(CancellationToken.None);
            cancellation?.Dispose();
            queuedFiles.Clear();
            while (queue.Reader.TryRead(out _)) { }
            cancellation = null;
            worker = null;
            initialScanner = null;
            periodicScanner = null;
            stateSaver = null;
            Interlocked.Exchange(ref queueSaturationReported, 0);
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        CancelImportNow();
        await manualScanLock.WaitAsync();
        manualScanLock.Release();
        await StopAsync();
        folderMonitor.Dispose();
        manualScanLock.Dispose();
        processingLock.Dispose();
        lifecycleLock.Dispose();
        folderRefreshLock.Dispose();
        fileStateLoadLock.Dispose();
        fileStateSaveLock.Dispose();
    }

    public async Task RefreshFoldersAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (worker is null) return;
            await folderRefreshLock.WaitAsync(cancellationToken);
            try
            {
                folders = await ResolveFoldersAsync(cancellationToken);
                folderMonitor.Start(folders);
            }
            finally { folderRefreshLock.Release(); }
        }
        finally { lifecycleLock.Release(); }

        var monitoringToken = cancellation?.Token ?? cancellationToken;
        _ = ScanAndQueueChangedFilesSafelyAsync(folders, monitoringToken);
    }

    private void OnFileDetected(object? sender, DetectedImportFile file) => TryQueueIfNeeded(file);

    private void OnMonitoringFailed(object? sender, FolderScanFailure failure) =>
        ReportFolderFailure(failure, automatic: true);

    private void TryQueueIfNeeded(DetectedImportFile file)
    {
        if (IsAlreadyExamined(file)) return;
        if (!queuedFiles.TryAdd(file.FullPath, 0)) return;
        if (queue.Writer.TryWrite(file)) return;

        queuedFiles.TryRemove(file.FullPath, out _);
        if (Interlocked.Exchange(ref queueSaturationReported, 1) == 0)
        {
            var exception = new InvalidOperationException(
                "La file de synchronisation est pleine. Les fichiers restants seront repris au prochain contrôle.");
            NotifyImportFailed(exception);
            Raise(file.FullPath, SynchronizationFileState.Error, exception.Message, 0, 0, true);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var file in queue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (!IsAlreadyExamined(file, refreshSnapshot: true))
                {
                    await ProcessFileAsync(file, cancellationToken, true);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                NotifyImportFailed(exception);
                Raise(file.FullPath, SynchronizationFileState.Error, exception.Message, 0, 0, true);
            }
            finally
            {
                queuedFiles.TryRemove(file.FullPath, out _);
                if (queuedFiles.Count < QueueCapacity / 2)
                {
                    Interlocked.Exchange(ref queueSaturationReported, 0);
                }
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
            NotifyImportCompleted(result);
            if (raiseProgress)
            {
                var state = ToState(result.Outcome);
                Raise(file.FullPath, state, ToMessage(state), 0, 0, automatic, result);
            }
        }
        if (results.Count > 0) await RememberExaminedFileAsync(file.FullPath, cancellationToken);
        return results;
    }

    private async Task RunPeriodicScanAsync(CancellationToken cancellationToken)
    {
        if (initialScanner is not null) await initialScanner;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            try
            {
                folders = await ResolveFoldersAsync(cancellationToken);
                await ScanAndQueueChangedFilesAsync(folders, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                NotifyImportFailed(exception);
            }
        }
    }

    private async Task ScanAndQueueChangedFilesSafelyAsync(
        IReadOnlyList<MonitoredFolder> foldersToScan,
        CancellationToken cancellationToken)
    {
        try
        {
            await ScanAndQueueChangedFilesAsync(foldersToScan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            NotifyImportFailed(exception);
        }
    }

    private async Task ScanAndQueueChangedFilesAsync(
        IReadOnlyList<MonitoredFolder> foldersToScan,
        CancellationToken cancellationToken)
    {
        var scan = await folderMonitor.ScanWithDiagnosticsAsync(foldersToScan, cancellationToken);
        foreach (var failure in scan.Failures) ReportFolderFailure(failure, automatic: true);
        foreach (var file in scan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await QueueIfNeededAsync(file, cancellationToken);
        }
    }

    private async Task QueueIfNeededAsync(
        DetectedImportFile file,
        CancellationToken cancellationToken)
    {
        if (IsAlreadyExamined(file)) return;
        if (!queuedFiles.TryAdd(file.FullPath, 0)) return;
        try
        {
            await queue.Writer.WriteAsync(file, cancellationToken);
        }
        catch
        {
            queuedFiles.TryRemove(file.FullPath, out _);
            throw;
        }
    }

    private bool IsAlreadyExamined(DetectedImportFile file, bool refreshSnapshot = false)
    {
        var snapshot = refreshSnapshot ? TryCaptureSnapshot(file.FullPath) : file.Snapshot;
        if (snapshot is null) return false;
        var pathKey = MonitoredFileStateStore.CreatePathKey(snapshot.FullPath);
        return examinedFiles.TryGetValue(pathKey, out var examined) &&
               examined.Length == snapshot.Length &&
               examined.LastWriteTimeUtcTicks == snapshot.LastWriteTimeUtcTicks;
    }

    private async Task EnsureFileStatesLoadedAsync(CancellationToken cancellationToken)
    {
        if (fileStatesLoaded) return;
        await fileStateLoadLock.WaitAsync(cancellationToken);
        try
        {
            if (fileStatesLoaded) return;
            foreach (var snapshot in await fileStateStore.LoadAsync(cancellationToken))
            {
                examinedFiles[snapshot.PathKey] = snapshot;
            }
            fileStatesLoaded = true;
        }
        finally
        {
            fileStateLoadLock.Release();
        }
    }

    private async Task RememberExaminedFileAsync(string path, CancellationToken cancellationToken)
    {
        var snapshot = TryCaptureSnapshot(path);
        if (snapshot is null) return;
        var pathKey = MonitoredFileStateStore.CreatePathKey(path);
        examinedFiles[pathKey] = new MonitoredFileFingerprint(
            pathKey,
            snapshot.Length,
            snapshot.LastWriteTimeUtcTicks);
        pendingFileStates[pathKey] = snapshot;
        if (pendingFileStates.Count >= StateSaveBatchSize)
        {
            await TryFlushFileStatesAsync(cancellationToken);
        }
    }

    private static MonitoredFileSnapshot? TryCaptureSnapshot(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? new MonitoredFileSnapshot(path, info.Length, info.LastWriteTimeUtc.Ticks)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task RunStateSaverAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StateSaveInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await TryFlushFileStatesAsync(cancellationToken);
        }
    }

    private async Task TryFlushFileStatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await FlushFileStatesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            NotifyImportFailed(exception);
        }
    }

    private async Task FlushFileStatesAsync(CancellationToken cancellationToken)
    {
        await fileStateSaveLock.WaitAsync(cancellationToken);
        try
        {
            while (!pendingFileStates.IsEmpty)
            {
                var batch = pendingFileStates.Take(StateSaveBatchSize).ToArray();
                await fileStateStore.SaveAsync(batch.Select(item => item.Value).ToArray(), cancellationToken);
                foreach (var item in batch)
                {
                    if (pendingFileStates.TryGetValue(item.Key, out var current) && current == item.Value)
                    {
                        pendingFileStates.TryRemove(item.Key, out _);
                    }
                }
            }
        }
        finally
        {
            fileStateSaveLock.Release();
        }
    }

    private async Task<IReadOnlyList<MonitoredFolder>> ResolveFoldersAsync(CancellationToken cancellationToken)
    {
        var configured = await settingsService.GetAsync(cancellationToken);
        var selection = await selectionSettings.GetAsync(cancellationToken);
        var hymoEnabled = await hymoMonitoringSettings.IsEnabledAsync(cancellationToken);
        return configured
            .Where(folder => !(folder.Kind == ImportFolderKind.OfficialProviderApplication &&
                               string.Equals(folder.Provider, "HYMO", StringComparison.OrdinalIgnoreCase)))
            .Concat(hymoEnabled ? trackTitanFolders.Resolve(selection) : [])
            .DistinctBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ReportFolderFailure(FolderScanFailure failure, bool automatic)
    {
        NotifyImportFailed(failure.Exception);
        Raise(
            failure.Path,
            SynchronizationFileState.Error,
            $"Dossier ignoré : {failure.Exception.Message}",
            0,
            0,
            automatic);
    }

    private void NotifyImportCompleted(SetupImportResult result)
    {
        if (ImportCompleted is null) return;
        foreach (EventHandler<SetupImportResult> handler in ImportCompleted.GetInvocationList())
        {
            try
            {
                handler(this, result);
            }
            catch (Exception exception)
            {
                NotifyImportFailed(new InvalidOperationException(
                    "Un écran n’a pas pu traiter la notification d’import.",
                    exception));
            }
        }
    }

    private void NotifyImportFailed(Exception exception)
    {
        if (ImportFailed is null) return;
        foreach (EventHandler<Exception> handler in ImportFailed.GetInvocationList())
        {
            try { handler(this, exception); }
            catch { }
        }
    }

    private void Raise(
        string path,
        SynchronizationFileState state,
        string message,
        int completed,
        int total,
        bool automatic,
        SetupImportResult? result = null)
    {
        if (ProgressChanged is null) return;
        var progress = new SynchronizationProgress(path, state, message, completed, total, automatic, result);
        foreach (EventHandler<SynchronizationProgress> handler in ProgressChanged.GetInvocationList())
        {
            try { handler(this, progress); }
            catch (Exception exception)
            {
                NotifyImportFailed(new InvalidOperationException(
                    "Un écran n’a pas pu traiter la progression de synchronisation.",
                    exception));
            }
        }
    }

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
