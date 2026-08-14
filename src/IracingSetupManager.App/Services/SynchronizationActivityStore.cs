using IracingSetupManager.Infrastructure.Files.Monitoring;

namespace IracingSetupManager.App.Services;

public sealed class SynchronizationActivityStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, SynchronizationProgress> items = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<SynchronizationProgress>? Changed;

    public SynchronizationSummary? LastSummary { get; private set; }

    public void Attach(ImportMonitoringService monitoring) => monitoring.ProgressChanged += OnProgressChanged;

    public IReadOnlyList<SynchronizationProgress> Snapshot()
    {
        lock (gate) return items.Values.ToArray();
    }

    public void Clear()
    {
        lock (gate) items.Clear();
        LastSummary = null;
    }

    public void SetSummary(SynchronizationSummary summary) => LastSummary = summary;

    private void OnProgressChanged(object? sender, SynchronizationProgress progress)
    {
        lock (gate)
        {
            items[progress.FilePath] = progress;
            if (items.Count > 500) items.Remove(items.Keys.First());
        }
        Changed?.Invoke(this, progress);
    }
}
