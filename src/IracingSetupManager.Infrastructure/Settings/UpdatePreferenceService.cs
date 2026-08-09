using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Settings;

public sealed class UpdatePreferenceService(ISetupDbContextFactory contextFactory)
{
    private const string IgnoredKey = "Updates.IgnoredVersion";
    private const string DeferredUntilKey = "Updates.DeferredUntilUtc";

    public async Task<bool> ShouldOfferAsync(Version version, CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var settings = await context.ApplicationSettings.AsNoTracking()
            .Where(item => item.Key == IgnoredKey || item.Key == DeferredUntilKey)
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
        if (settings.GetValueOrDefault(IgnoredKey) == version.ToString(3)) return false;
        return !DateTimeOffset.TryParse(settings.GetValueOrDefault(DeferredUntilKey), out var deferred) || deferred <= DateTimeOffset.UtcNow;
    }

    public Task IgnoreAsync(Version version, CancellationToken cancellationToken = default) => SaveAsync(IgnoredKey, version.ToString(3), cancellationToken);
    public Task DeferAsync(TimeSpan duration, CancellationToken cancellationToken = default) => SaveAsync(DeferredUntilKey, DateTimeOffset.UtcNow.Add(duration).ToString("O"), cancellationToken);

    private async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([key], cancellationToken);
        if (setting is null) context.ApplicationSettings.Add(new ApplicationSettingEntity { Key = key, Value = value, UpdatedAtUtc = DateTimeOffset.UtcNow });
        else { setting.Value = value; setting.UpdatedAtUtc = DateTimeOffset.UtcNow; }
        await context.SaveChangesAsync(cancellationToken);
    }
}
