using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed record SetupCorrection(
    string Provider,
    string Category,
    string Car,
    string Track,
    string? TrackConfiguration,
    string? Season,
    string SetupType,
    string? CarAlias = null,
    string? TrackAlias = null,
    int? Week = null,
    SetupWeekKind? WeekKind = null);

public sealed record SetupBatchCorrection(
    string? Provider = null,
    string? Category = null,
    string? Car = null,
    string? Track = null,
    string? Season = null,
    string? SetupType = null,
    int? Week = null,
    SetupWeekKind? WeekKind = null);

public sealed class SetupCorrectionService(
    ISetupDbContextFactory contextFactory,
    ArchivePathBuilder pathBuilder,
    RecognitionAliasService recognitionAliases)
{
    public async Task CorrectManyAsync(
        IReadOnlyCollection<Guid> setupIds,
        SetupBatchCorrection correction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(correction);
        foreach (var setupId in setupIds.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var context = contextFactory.Create();
            var setup = await context.Setups.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == setupId, cancellationToken)
                ?? throw new KeyNotFoundException("Un setup sélectionné n’existe plus.");
            await CorrectAsync(setupId, new SetupCorrection(
                correction.Provider ?? setup.Provider,
                correction.Category ?? setup.Category,
                correction.Car ?? setup.Car,
                correction.Track ?? setup.Track,
                setup.TrackConfiguration,
                correction.Season ?? setup.Season,
                correction.SetupType ?? setup.SetupType,
                Week: correction.WeekKind.HasValue ? correction.Week : setup.Week,
                WeekKind: correction.WeekKind ?? setup.WeekKind), cancellationToken);
        }
    }

    public async Task CorrectAsync(Guid setupId, SetupCorrection correction, CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        await ValidateAsync(context, correction, cancellationToken);
        var setup = await context.Setups.SingleOrDefaultAsync(item => item.Id == setupId, cancellationToken)
            ?? throw new KeyNotFoundException("Le setup demandé n’existe pas.");
        var archiveRoot = await context.ApplicationSettings.Where(item => item.Key == "ArchivePath")
            .Select(item => item.Value).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Le dossier d’archive n’est pas configuré.");

        var correctedWeek = correction.WeekKind.HasValue ? correction.Week : setup.Week;
        var correctedWeekKind = correction.WeekKind ?? setup.WeekKind;
        var metadata = new SetupMetadata(correction.Provider, correction.Category, correction.Car, correction.Track,
            EmptyAsNull(correction.TrackConfiguration), EmptyAsNull(correction.Season), correction.SetupType, correctedWeek, correctedWeekKind);
        var destinationDirectory = pathBuilder.BuildDirectory(archiveRoot, metadata);
        Directory.CreateDirectory(destinationDirectory);
        var oldPath = setup.ArchivePath;
        var newPath = Path.Combine(destinationDirectory, setup.OriginalFileName);
        var moved = !Path.GetFullPath(oldPath).Equals(Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase);
        if (moved)
        {
            if (!File.Exists(oldPath)) throw new FileNotFoundException("Le fichier archivé est introuvable.", oldPath);
            if (File.Exists(newPath)) throw new IOException("Un fichier du même nom existe déjà dans le classement corrigé.");
            File.Move(oldPath, newPath);
        }

        try
        {
            setup.Provider = correction.Provider;
            setup.Category = correction.Category;
            setup.Car = correction.Car;
            setup.Track = correction.Track;
            setup.TrackConfiguration = EmptyAsNull(correction.TrackConfiguration);
            setup.Season = EmptyAsNull(correction.Season);
            setup.SetupType = correction.SetupType;
            setup.Week = correctedWeek;
            setup.WeekKind = correctedWeekKind;
            setup.ArchivePath = newPath;
            context.SetupChangeHistory.Add(new SetupChangeHistoryEntity
            {
                SetupId = setup.Id,
                OriginalFileName = setup.OriginalFileName,
                ChangeType = "CorrectionIdentification",
                PreviousStatus = setup.Status,
                NewStatus = setup.Status,
                PreviousRating = setup.PersonalRating,
                NewRating = setup.PersonalRating,
                PreviousComment = setup.Comment,
                NewComment = setup.Comment,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (moved && File.Exists(newPath) && !File.Exists(oldPath)) File.Move(newPath, oldPath);
            throw;
        }
        if (!string.IsNullOrWhiteSpace(correction.CarAlias))
            await recognitionAliases.SaveAsync(RecognitionAliasKind.Car, correction.CarAlias, correction.Car, cancellationToken);
        if (!string.IsNullOrWhiteSpace(correction.TrackAlias))
            await recognitionAliases.SaveAsync(RecognitionAliasKind.Track, correction.TrackAlias, correction.Track, cancellationToken);
    }

    private static async Task ValidateAsync(SetupDbContext context, SetupCorrection correction, CancellationToken cancellationToken)
    {
        if (!SetupCatalog.ProviderNames.Contains(correction.Provider, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fournisseur inconnu.");
        if (!SetupCatalog.Categories.Contains(correction.Category, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Catégorie inconnue.");
        var car = SetupCatalog.Cars.FirstOrDefault(item => item.DisplayName.Equals(correction.Car, StringComparison.OrdinalIgnoreCase));
        if (car is null || !car.Category.Equals(correction.Category, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La voiture ne correspond pas à la catégorie choisie.");
        if (!SetupMetadataAnalyzer.KnownTrackNames.Contains(correction.Track, StringComparer.OrdinalIgnoreCase) &&
            !await context.TrackCatalog.AnyAsync(item => item.TrackName == correction.Track, cancellationToken))
            throw new InvalidOperationException("Circuit inconnu.");
        if (string.IsNullOrWhiteSpace(correction.SetupType)) throw new InvalidOperationException("Le type de setup est obligatoire.");
    }

    private static string? EmptyAsNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
