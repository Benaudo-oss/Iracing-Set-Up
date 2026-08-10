using System.Text.RegularExpressions;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed record TrackCatalogMatch(string TrackName, string? Configuration);

public sealed class TrackCatalogService(ISetupDbContextFactory contextFactory)
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adelaide"] = "Adelaide", ["algarve"] = "Algarve", ["barcelona"] = "Barcelona", ["bathurst"] = "Bathurst",
            ["charlotte"] = "Charlotte", ["daytona"] = "Daytona", ["donington"] = "Donington Park",
            ["fuji"] = "Fuji", ["hockenheim"] = "Hockenheim", ["imola"] = "Imola",
            ["lagunaseca"] = "Laguna Seca", ["lakeland"] = "Lakeland", ["ledenon"] = "Lédenon",
            ["lemans"] = "Le Mans", ["limerock"] = "Lime Rock Park", ["magnycours"] = "Magny-Cours",
            ["miami"] = "Miami", ["misano"] = "Misano", ["monza"] = "Monza",
            ["mexico"] = "Mexique", ["stpete"] = "Saint-Pétersbourg",
            ["mosport"] = "Canadian Tire Motorsport Park",
            ["nurburgring"] = "Nürburgring", ["okayama"] = "Okayama", ["oran"] = "Oran Park",
            ["oschersleben"] = "Oschersleben", ["oulton"] = "Oulton Park", ["phoenix"] = "Phoenix",
            ["roadamerica"] = "Road America", ["roadatlanta"] = "Road Atlanta", ["rudskogen"] = "Rudskogen",
            ["sebring"] = "Sebring", ["silverstone"] = "Silverstone", ["snetterton"] = "Snetterton",
            ["spa"] = "Spa-Francorchamps", ["summit"] = "Summit Point", ["twinring"] = "Twin Ring Motegi",
            ["virginia"] = "Virginia International Raceway", ["watkinsglen"] = "Watkins Glen",
            ["wildwest"] = "Wild West"
        };

    private volatile IReadOnlyList<TrackCatalogEntity> snapshot = [];

    public static string? DetectLapfilesFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(documents, "iRacing", "lapfiles");
        return Directory.Exists(path) ? path : null;
    }

    public async Task<int> SynchronizeAsync(string? lapfilesFolder = null, CancellationToken cancellationToken = default)
    {
        var folder = string.IsNullOrWhiteSpace(lapfilesFolder) ? DetectLapfilesFolder() : Path.GetFullPath(lapfilesFolder);
        if (folder is null)
        {
            await LoadSnapshotAsync(cancellationToken);
            return 0;
        }

        var names = Directory.EnumerateDirectories(folder)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var now = DateTimeOffset.UtcNow;
        await using var context = contextFactory.Create();
        foreach (var folderName in names)
        {
            var parsed = Parse(folderName);
            var entity = await context.TrackCatalog.FindAsync([folderName], cancellationToken);
            if (entity is null)
            {
                context.TrackCatalog.Add(new TrackCatalogEntity
                {
                    IracingFolderName = folderName,
                    TrackName = parsed.TrackName,
                    Configuration = parsed.Configuration,
                    NormalizedAlias = Normalize(folderName),
                    LastSeenAtUtc = now
                });
            }
            else
            {
                entity.TrackName = parsed.TrackName;
                entity.Configuration = parsed.Configuration;
                entity.NormalizedAlias = Normalize(folderName);
                entity.LastSeenAtUtc = now;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        await LoadSnapshotAsync(cancellationToken);
        return names.Count;
    }

    public TrackCatalogMatch? Find(string value)
    {
        var normalized = Normalize(value);
        var match = snapshot
            .Select(item => new { Item = item, BaseAlias = Normalize(item.IracingFolderName.Split(' ', 2)[0]) })
            .Where(candidate => normalized.Contains(candidate.Item.NormalizedAlias, StringComparison.OrdinalIgnoreCase) ||
                                normalized.Contains(candidate.BaseAlias, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Item.NormalizedAlias.Length)
            .ThenByDescending(candidate => candidate.BaseAlias.Length)
            .FirstOrDefault();
        return match is null ? null : new TrackCatalogMatch(match.Item.TrackName, match.Item.Configuration);
    }

    public async Task<IReadOnlyList<TrackCatalogEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.TrackCatalog.AsNoTracking().OrderBy(item => item.TrackName).ThenBy(item => item.Configuration).ToListAsync(cancellationToken);
    }

    private async Task LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        snapshot = await GetAllAsync(cancellationToken);
    }

    private static TrackCatalogMatch Parse(string folderName)
    {
        var parts = folderName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var baseName = parts[0];
        var trackName = DisplayNames.GetValueOrDefault(baseName) ?? ToTitleCase(baseName);
        var configuration = parts.Length > 1 ? string.Join(' ', parts.Skip(1).Select(ToTitleCase)) : null;
        return new TrackCatalogMatch(trackName, configuration);
    }

    private static string Normalize(string value) => Regex.Replace(value, "[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase).ToLowerInvariant();
    private static string ToTitleCase(string value) => value.ToLowerInvariant() switch
    {
        "gp" => "GP",
        "rc" => "RC",
        "up" => "UP",
        _ => string.IsNullOrWhiteSpace(value) ? value : char.ToUpperInvariant(value[0]) + value[1..]
    };
}
