using System.Text.Json;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Iracing;

public sealed class IracingPathLayoutService(ISetupDbContextFactory contextFactory)
{
    private const string SettingKey = "Iracing.CopyPathLayout";
    public static IReadOnlyList<string> DefaultLayout { get; } = ["Season", "Track", "Provider", "Week"];
    public static IReadOnlySet<string> AllowedElements { get; } = new HashSet<string>(DefaultLayout, StringComparer.Ordinal);

    public async Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var json = await context.ApplicationSettings.AsNoTracking()
            .Where(item => item.Key == SettingKey)
            .Select(item => item.Value)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return DefaultLayout;

        try
        {
            var layout = JsonSerializer.Deserialize<string[]>(json);
            if (IsValid(layout)) return layout!;
            if (IsLegacyLayout(layout))
            {
                var upgraded = layout!.ToList();
                upgraded.Insert(upgraded.IndexOf("Season") + 1, "Track");
                return upgraded;
            }
            return DefaultLayout;
        }
        catch (JsonException)
        {
            return DefaultLayout;
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<string> layout, CancellationToken cancellationToken = default)
    {
        if (!IsValid(layout))
            throw new ArgumentException("L’arborescence doit contenir une seule fois Saison, Circuit, Fournisseur et Week.", nameof(layout));

        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([SettingKey], cancellationToken);
        var value = JsonSerializer.Serialize(layout);
        if (setting is null)
            context.ApplicationSettings.Add(new ApplicationSettingEntity { Key = SettingKey, Value = value, UpdatedAtUtc = DateTimeOffset.UtcNow });
        else
        {
            setting.Value = value;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsValid(IReadOnlyCollection<string>? layout) =>
        layout is not null && layout.Count == AllowedElements.Count && layout.Distinct(StringComparer.Ordinal).Count() == AllowedElements.Count && layout.All(AllowedElements.Contains);

    private static bool IsLegacyLayout(IReadOnlyCollection<string>? layout) =>
        layout is not null && layout.Count == 3 &&
        layout.ToHashSet(StringComparer.Ordinal).SetEquals(["Season", "Provider", "Week"]);
}
