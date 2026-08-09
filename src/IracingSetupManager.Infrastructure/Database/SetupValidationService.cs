using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupValidationService(ISetupDbContextFactory contextFactory)
{
    public Task ValidateAsync(Guid setupId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync([setupId], SetupStatus.Valide, confirmed: true, cancellationToken);

    public Task RefuseAsync(Guid setupId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync([setupId], SetupStatus.Refuse, confirmed: true, cancellationToken);

    public Task ValidateManyAsync(
        IReadOnlyCollection<Guid> setupIds,
        bool confirmed,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(setupIds, SetupStatus.Valide, confirmed, cancellationToken);

    public Task RefuseManyAsync(
        IReadOnlyCollection<Guid> setupIds,
        bool confirmed,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(setupIds, SetupStatus.Refuse, confirmed, cancellationToken);

    public async Task UpdateNotesAsync(
        Guid setupId,
        int? personalRating,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (personalRating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(personalRating), "La note doit être comprise entre 1 et 5.");
        }

        await using var context = contextFactory.Create();
        var setup = await context.Setups.SingleOrDefaultAsync(item => item.Id == setupId, cancellationToken)
            ?? throw new KeyNotFoundException("Le setup demandé n'existe pas.");
        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        context.SetupChangeHistory.Add(new SetupChangeHistoryEntity
        {
            SetupId = setup.Id,
            OriginalFileName = setup.OriginalFileName,
            ChangeType = "NoteEtCommentaire",
            PreviousStatus = setup.Status,
            NewStatus = setup.Status,
            PreviousRating = setup.PersonalRating,
            NewRating = personalRating,
            PreviousComment = setup.Comment,
            NewComment = normalizedComment,
            ChangedAtUtc = DateTimeOffset.UtcNow
        });
        setup.PersonalRating = personalRating;
        setup.Comment = normalizedComment;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ChangeStatusAsync(
        IReadOnlyCollection<Guid> setupIds,
        SetupStatus newStatus,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setupIds);
        var distinctIds = setupIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return;
        }

        if (distinctIds.Count > 1 && !confirmed)
        {
            throw new InvalidOperationException("Une action groupée doit être confirmée explicitement.");
        }

        await using var context = contextFactory.Create();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var setups = await context.Setups
            .Where(item => distinctIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (setups.Count != distinctIds.Count)
        {
            throw new KeyNotFoundException("Au moins un setup sélectionné n'existe pas.");
        }

        foreach (var setup in setups)
        {
            var previousStatus = setup.Status;
            setup.Status = newStatus;
            context.SetupChangeHistory.Add(new SetupChangeHistoryEntity
            {
                SetupId = setup.Id,
                OriginalFileName = setup.OriginalFileName,
                ChangeType = newStatus == SetupStatus.Valide ? "Validation" : "Refus",
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                PreviousRating = setup.PersonalRating,
                NewRating = setup.PersonalRating,
                PreviousComment = setup.Comment,
                NewComment = setup.Comment,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

