using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Settings;

public sealed class UpdatePreferenceService(ISetupDbContextFactory contextFactory)
{
    private const string IgnoredKey = "Updates.IgnoredVersion";
    private const string DeferredUntilKey = "Updates.DeferredUntilUtc";
    private const string PendingInstallationKey = "Updates.PendingInstallationVersion";

    public async Task<bool> ShouldOfferAsync(Version version, CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var settings = await context.ApplicationSettings.AsNoTracking()
            .Where(item => item.Key == IgnoredKey || item.Key == DeferredUntilKey)
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
        if (settings.GetValueOrDefault(IgnoredKey) == version.ToString()) return false;
        return !DateTimeOffset.TryParse(settings.GetValueOrDefault(DeferredUntilKey), out var deferred) || deferred <= DateTimeOffset.UtcNow;
    }

    public Task IgnoreAsync(Version version, CancellationToken cancellationToken = default) => SaveAsync(IgnoredKey, version.ToString(), cancellationToken);
    public Task DeferAsync(TimeSpan duration, CancellationToken cancellationToken = default) => SaveAsync(DeferredUntilKey, DateTimeOffset.UtcNow.Add(duration).ToString("O"), cancellationToken);

    public Task MarkInstallationPendingAsync(Version version, CancellationToken cancellationToken = default) =>
        SaveAsync(PendingInstallationKey, version.ToString(), cancellationToken);

    public async Task<UpdateVerificationResult?> VerifyInstallationAfterRestartAsync(
        Version installedVersion,
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([PendingInstallationKey], cancellationToken);
        if (setting is null || !Version.TryParse(setting.Value, out var expected)) return null;
        var success = installedVersion >= expected;
        context.ApplicationSettings.Remove(setting);
        await context.SaveChangesAsync(cancellationToken);
        return new UpdateVerificationResult(success, expected, installedVersion,
            success
                ? $"La mise à jour {installedVersion} a été installée avec succès."
                : $"La mise à jour vers {expected} n’a pas abouti. La version {installedVersion} est toujours installée.");
    }

    private async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([key], cancellationToken);
        if (setting is null) context.ApplicationSettings.Add(new ApplicationSettingEntity { Key = key, Value = value, UpdatedAtUtc = DateTimeOffset.UtcNow });
        else { setting.Value = value; setting.UpdatedAtUtc = DateTimeOffset.UtcNow; }
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UpdateVerificationResult(bool Success, Version ExpectedVersion, Version InstalledVersion, string Message);
