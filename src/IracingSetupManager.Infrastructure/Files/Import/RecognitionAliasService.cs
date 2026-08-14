using System.Text.RegularExpressions;
using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class RecognitionAliasService(ISetupDbContextFactory contextFactory)
{
    private RecognitionAliasEntity[] aliases = [];

    public IReadOnlyList<RecognitionAliasEntity> Snapshot => aliases;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        aliases = await context.RecognitionAliases.AsNoTracking()
            .OrderBy(item => item.Kind).ThenBy(item => item.Alias)
            .ToArrayAsync(cancellationToken);
    }

    public string? Find(RecognitionAliasKind kind, string fileName)
    {
        var normalizedName = Normalize(fileName);
        return aliases.Where(item => item.Kind == kind && normalizedName.Contains(item.NormalizedAlias, StringComparison.Ordinal))
            .OrderByDescending(item => item.NormalizedAlias.Length)
            .Select(item => item.CanonicalValue)
            .FirstOrDefault();
    }

    public async Task<RecognitionAliasEntity> SaveAsync(
        RecognitionAliasKind kind,
        string alias,
        string canonicalValue,
        CancellationToken cancellationToken = default)
    {
        var cleanAlias = alias.Trim();
        var normalized = Normalize(cleanAlias);
        if (normalized.Length < 3)
            throw new InvalidOperationException("L’abréviation doit contenir au moins 3 caractères significatifs.");

        await using var context = contextFactory.Create();
        await ValidateCanonicalValueAsync(context, kind, canonicalValue, cancellationToken);
        var entity = await context.RecognitionAliases
            .SingleOrDefaultAsync(item => item.Kind == kind && item.NormalizedAlias == normalized, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (entity is null)
        {
            entity = new RecognitionAliasEntity
            {
                Kind = kind,
                Alias = cleanAlias,
                NormalizedAlias = normalized,
                CanonicalValue = canonicalValue,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            context.RecognitionAliases.Add(entity);
        }
        else
        {
            entity.Alias = cleanAlias;
            entity.CanonicalValue = canonicalValue;
            entity.UpdatedAtUtc = now;
        }
        await context.SaveChangesAsync(cancellationToken);
        await LoadAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var entity = await context.RecognitionAliases.FindAsync([id], cancellationToken);
        if (entity is null) return;
        context.RecognitionAliases.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public static string Normalize(string value) =>
        Regex.Replace(value, "[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase).ToLowerInvariant();

    private static async Task ValidateCanonicalValueAsync(
        SetupDbContext context,
        RecognitionAliasKind kind,
        string value,
        CancellationToken cancellationToken)
    {
        var valid = kind switch
        {
            RecognitionAliasKind.Car => SetupCatalog.Cars.Any(item => item.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase)),
            RecognitionAliasKind.Track => SetupMetadataAnalyzer.KnownTrackNames.Contains(value, StringComparer.OrdinalIgnoreCase) ||
                await context.TrackCatalog.AnyAsync(item => item.TrackName == value, cancellationToken),
            _ => false
        };
        if (!valid) throw new InvalidOperationException("La valeur choisie n’existe pas dans le catalogue officiel.");
    }
}
