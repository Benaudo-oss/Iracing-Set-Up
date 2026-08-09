namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed class ImportFolderMonitor(MonitoredFolderPolicy policy) : IDisposable
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".sto", ".zip", ".rar" };

    private readonly List<FileSystemWatcher> _watchers = [];

    public event EventHandler<DetectedImportFile>? FileDetected;

    public void Start(IEnumerable<MonitoredFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        Stop();

        foreach (var folder in folders)
        {
            var validatedPath = policy.Validate(folder);
            if (!Directory.Exists(validatedPath))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(validatedPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                EnableRaisingEvents = false
            };

            watcher.Created += (_, args) => RaiseIfSupported(args.FullPath, folder);
            watcher.Renamed += (_, args) => RaiseIfSupported(args.FullPath, folder);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    public Task<IReadOnlyList<DetectedImportFile>> ScanAsync(
        IEnumerable<MonitoredFolder> folders,
        CancellationToken cancellationToken = default)
    {
        var files = new List<DetectedImportFile>();
        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validatedPath = policy.Validate(folder);
            if (!Directory.Exists(validatedPath))
            {
                continue;
            }

            files.AddRange(Directory.EnumerateFiles(validatedPath, "*", SearchOption.AllDirectories)
                .Where(IsSupported)
                .Select(path => new DetectedImportFile(path, folder.Kind, folder.Provider)));
        }

        return Task.FromResult<IReadOnlyList<DetectedImportFile>>(files);
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
            FileDetected?.Invoke(this, new DetectedImportFile(path, folder.Kind, folder.Provider));
        }
    }

    private static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));
}
