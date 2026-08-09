using System.Text.Json;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed class MonitoredFolderSettingsService(
    ISetupDbContextFactory contextFactory,
    MonitoredFolderPolicy policy)
{
    private const string SettingKey = "MonitoredFolders";

    public async Task<IReadOnlyList<MonitoredFolder>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var json = await context.ApplicationSettings
            .Where(setting => setting.Key == SettingKey)
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(json))
        {
            var stored = JsonSerializer.Deserialize<List<MonitoredFolder>>(json) ?? [];
            return stored.Select(folder => folder with { Path = policy.Validate(folder) }).ToList();
        }

        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        return [new MonitoredFolder(downloads, ImportFolderKind.Downloads)];
    }

    public async Task SaveAsync(
        IEnumerable<MonitoredFolder> folders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var validated = folders
            .Select(folder => folder with { Path = policy.Validate(folder) })
            .DistinctBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var json = JsonSerializer.Serialize(validated);

        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        if (setting is null)
        {
            context.ApplicationSettings.Add(new ApplicationSettingEntity
            {
                Key = SettingKey,
                Value = json,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            setting.Value = json;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

