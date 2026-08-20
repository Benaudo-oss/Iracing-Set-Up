using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;

namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed class HymoMonitoringSettingsService(ISetupDbContextFactory contextFactory)
{
    private const string SettingKey = "HymoTrackTitanMonitoringEnabled";

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        return setting is null || !bool.TryParse(setting.Value, out var enabled) || enabled;
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
