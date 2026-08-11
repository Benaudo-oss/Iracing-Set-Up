using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;

namespace IracingSetupManager.Infrastructure.Settings;

public sealed class IracingTeamSettingsService(ISetupDbContextFactory contextFactory)
{
    private const string SettingKey = "Iracing.TeamName";

    public async Task<string?> GetNameAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        return string.IsNullOrWhiteSpace(setting?.Value) ? null : setting.Value;
    }

    public async Task SaveNameAsync(string teamName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);
        var normalized = teamName.Trim();
        if (normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Le nom de la Team contient un caractère interdit.", nameof(teamName));

        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        if (setting is null)
        {
            context.ApplicationSettings.Add(new ApplicationSettingEntity
            {
                Key = SettingKey,
                Value = normalized,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            setting.Value = normalized;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
