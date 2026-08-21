using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupLibraryIntegrityService(ISetupDbContextFactory contextFactory)
{
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private DateTimeOffset? _lastCompletedAtUtc;

    public async Task<int> MarkMissingFilesAsync(CancellationToken cancellationToken = default)
    {
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            var count = await MarkMissingFilesCoreAsync(cancellationToken);
            _lastCompletedAtUtc = DateTimeOffset.UtcNow;
            return count;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    public async Task<int> MarkMissingFilesIfDueAsync(
        TimeSpan minimumInterval,
        CancellationToken cancellationToken = default)
    {
        if (minimumInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        }

        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            if (_lastCompletedAtUtc is not null &&
                DateTimeOffset.UtcNow - _lastCompletedAtUtc < minimumInterval)
            {
                return 0;
            }

            var count = await MarkMissingFilesCoreAsync(cancellationToken);
            _lastCompletedAtUtc = DateTimeOffset.UtcNow;
            return count;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private async Task<int> MarkMissingFilesCoreAsync(CancellationToken cancellationToken)
    {
        await using var context = contextFactory.Create();
        var setups = await context.Setups
            .Where(item => item.Status != SetupStatus.FichierManquant)
            .ToListAsync(cancellationToken);
        var missingIds = await Task.Run(() =>
        {
            var result = new HashSet<Guid>();
            foreach (var setup in setups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(setup.ArchivePath)) result.Add(setup.Id);
            }
            return result;
        }, cancellationToken);
        var missing = setups.Where(item => missingIds.Contains(item.Id)).ToList();

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
