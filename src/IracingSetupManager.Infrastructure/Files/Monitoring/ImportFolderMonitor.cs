namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed record FolderScanFailure(
    MonitoredFolder Folder,
    string Path,
    Exception Exception);

public sealed record FolderScanResult(
    IReadOnlyList<DetectedImportFile> Files,
    IReadOnlyList<FolderScanFailure> Failures);

public sealed class ImportFolderMonitor(MonitoredFolderPolicy policy) : IDisposable
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".sto", ".zip", ".rar" };

    private readonly List<FileSystemWatcher> _watchers = [];

    public event EventHandler<DetectedImportFile>? FileDetected;
    public event EventHandler<FolderScanFailure>? MonitoringFailed;

    public void Start(IEnumerable<MonitoredFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        Stop();

        foreach (var folder in folders)
        {
            try
            {
                var validatedPath = policy.Validate(folder);
                if (!Directory.Exists(validatedPath)) continue;

                var watcher = new FileSystemWatcher(validatedPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                    InternalBufferSize = 32 * 1024,
                    EnableRaisingEvents = false
                };

                watcher.Created += (_, args) => RaiseIfSupported(args.FullPath, folder);
                watcher.Renamed += (_, args) => RaiseIfSupported(args.FullPath, folder);
                watcher.Error += (_, args) => ReportFailure(
                    new FolderScanFailure(folder, validatedPath, args.GetException()));
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception exception) when (IsRecoverableFolderError(exception))
            {
                ReportFailure(new FolderScanFailure(folder, folder.Path, exception));
            }
        }
    }

    public async Task<IReadOnlyList<DetectedImportFile>> ScanAsync(
        IEnumerable<MonitoredFolder> folders,
        CancellationToken cancellationToken = default) =>
        (await ScanWithDiagnosticsAsync(folders, cancellationToken)).Files;

    public async Task<FolderScanResult> ScanWithDiagnosticsAsync(
        IEnumerable<MonitoredFolder> folders,
        CancellationToken cancellationToken = default)
    {
        var files = new List<DetectedImportFile>();
        var failures = new List<FolderScanFailure>();
        var processedSinceYield = 0;
        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string validatedPath;
            try
            {
                validatedPath = policy.Validate(folder);
            }
            catch (Exception exception) when (IsRecoverableFolderError(exception))
            {
                failures.Add(new FolderScanFailure(folder, folder.Path, exception));
                continue;
            }

            if (!Directory.Exists(validatedPath)) continue;
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(validatedPath);
            while (pendingDirectories.TryPop(out var currentDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    foreach (var path in Directory.GetFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (IsSupported(path)) files.Add(CreateDetectedFile(path, folder));
                        if (++processedSinceYield >= 256)
                        {
                            processedSinceYield = 0;
                            await Task.Yield();
                        }
                    }

                    foreach (var directory in Directory.GetDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        pendingDirectories.Push(directory);
                    }
                }
                catch (Exception exception) when (IsRecoverableFolderError(exception))
                {
                    failures.Add(new FolderScanFailure(folder, currentDirectory, exception));
                }
            }
        }

        return new FolderScanResult(files, failures);
    }

    private static DetectedImportFile CreateDetectedFile(string path, MonitoredFolder folder)
    {
        try
        {
            var info = new FileInfo(path);
            return new DetectedImportFile(
                path,
                folder.Kind,
                folder.Provider,
                info.Length,
                info.LastWriteTimeUtc.Ticks);
        }
        catch (Exception exception) when (IsRecoverableFolderError(exception))
        {
            return new DetectedImportFile(path, folder.Kind, folder.Provider);
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    public void Dispose() => Stop();

    private void RaiseIfSupported(string path, MonitoredFolder folder)
    {
        if (IsSupported(path))
        {
            FileDetected?.Invoke(this, CreateDetectedFile(path, folder));
        }
    }

    private void ReportFailure(FolderScanFailure failure) => MonitoringFailed?.Invoke(this, failure);

    private static bool IsRecoverableFolderError(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        InvalidOperationException or
        ArgumentException or
        NotSupportedException;

    private static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));
}
