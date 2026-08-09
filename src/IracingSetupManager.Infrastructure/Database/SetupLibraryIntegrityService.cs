using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupLibraryIntegrityService(ISetupDbContextFactory contextFactory)
{
    public async Task<int> MarkMissingFilesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setups = await context.Setups
            .Where(item => item.Status != SetupStatus.FichierManquant)
            .ToListAsync(cancellationToken);
        var missing = setups.Where(item => !File.Exists(item.ArchivePath)).ToList();

        foreach (var setup in missing)
        {
            var previousStatus = setup.Status;
            setup.Status = SetupStatus.FichierManquant;
            context.SetupChangeHistory.Add(new SetupChangeHistoryEntity
            {
                SetupId = setup.Id,
                OriginalFileName = setup.OriginalFileName,
                ChangeType = "Fichier manquant",
                PreviousStatus = previousStatus,
                NewStatus = SetupStatus.FichierManquant,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });
        }

        if (missing.Count > 0) await context.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    public async Task<int> RemoveMissingEntriesAsync(
        IReadOnlyCollection<Guid> setupIds,
        CancellationToken cancellationToken = default)
    {
        var ids = setupIds.Distinct().ToArray();
        await using var context = contextFactory.Create();
        var setups = await context.Setups
            .Where(item => ids.Contains(item.Id) && item.Status == SetupStatus.FichierManquant)
            .ToListAsync(cancellationToken);

        context.Setups.RemoveRange(setups);
        if (setups.Count > 0) await context.SaveChangesAsync(cancellationToken);
        return setups.Count;
    }
}
