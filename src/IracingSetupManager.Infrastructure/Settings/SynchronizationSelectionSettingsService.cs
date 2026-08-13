using System.Text.Json;
using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;

namespace IracingSetupManager.Infrastructure.Settings;

public sealed record SynchronizationSelection(IReadOnlyList<string> Providers, IReadOnlyList<string> Categories);

public sealed class SynchronizationSelectionSettingsService(ISetupDbContextFactory contextFactory)
{
    private const string SettingKey = "SynchronizationSelection";
    public static SynchronizationSelection Default { get; } = new(
        SetupCatalog.ProviderNames,
        SetupCatalog.Categories);

    public async Task<SynchronizationSelection> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        if (setting is null) return Default;
        try
        {
            return JsonSerializer.Deserialize<SynchronizationSelection>(setting.Value) ?? Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public async Task SaveAsync(SynchronizationSelection selection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var allowedProviders = Default.Providers.ToHashSet(StringComparer.Ordinal);
        var allowedCategories = Default.Categories.ToHashSet(StringComparer.Ordinal);
        var normalized = new SynchronizationSelection(
            selection.Providers.Where(allowedProviders.Contains).Distinct(StringComparer.Ordinal).ToList(),
            selection.Categories.Where(allowedCategories.Contains).Distinct(StringComparer.Ordinal).ToList());
        var value = JsonSerializer.Serialize(normalized);

        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        if (setting is null)
        {
            context.ApplicationSettings.Add(new ApplicationSettingEntity
            {
                Key = SettingKey,
                Value = value,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
