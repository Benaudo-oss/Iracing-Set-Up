using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using System.Text.Json;

namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed class TelemetryCopySettingsService(ISetupDbContextFactory contextFactory)
{
    private const string SettingKey = "TelemetryCopyEnabled";

    public async Task<TelemetryCompletion?> ReadCompletionAsync(MonitoredFileSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var context = contextFactory.Create();
        var row = await context.ApplicationSettings.FindAsync([CompletionKey(snapshot)], cancellationToken);
        if (row is null) return null;
        try
        {
            var saved = JsonSerializer.Deserialize<StoredCompletion>(row.Value);
            return saved is not null && saved.Length == snapshot.Length && saved.LastWriteTimeUtcTicks == snapshot.LastWriteTimeUtcTicks
                ? saved.Fields : null;
        }
        catch (JsonException) { return null; }
    }

    public async Task SaveCompletionAsync(MonitoredFileSnapshot snapshot, TelemetryCompletion fields, CancellationToken cancellationToken)
    {
        await using var context = contextFactory.Create();
        var key = CompletionKey(snapshot);
        var row = await context.ApplicationSettings.FindAsync([key], cancellationToken);
        var value = JsonSerializer.Serialize(new StoredCompletion(snapshot.Length, snapshot.LastWriteTimeUtcTicks, fields));
        if (row is null)
            context.ApplicationSettings.Add(new ApplicationSettingEntity { Key = key, Value = value, UpdatedAtUtc = DateTimeOffset.UtcNow });
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string CompletionKey(MonitoredFileSnapshot snapshot) =>
        "TelemetryCompletion:" + MonitoredFileStateStore.CreatePathKey(snapshot.FullPath);

    private sealed record StoredCompletion(long Length, long LastWriteTimeUtcTicks, TelemetryCompletion Fields);

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        return setting is not null && bool.TryParse(setting.Value, out var enabled) && enabled;
    }

    public async Task SaveAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        if (setting is null)
        {
            context.ApplicationSettings.Add(new ApplicationSettingEntity
            {
                Key = SettingKey,
                Value = enabled.ToString(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            setting.Value = enabled.ToString();
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
