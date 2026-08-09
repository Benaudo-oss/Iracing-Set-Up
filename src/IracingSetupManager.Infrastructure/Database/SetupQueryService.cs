using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed record DashboardStatistics(
    int Total,
    int ToReview,
    int Validated,
    int SentToGarage61,
    int ProviderCount,
    DateTimeOffset? LastDownloadUtc);

public sealed class SetupQueryService(ISetupDbContextFactory contextFactory)
{
    public async Task<DashboardStatistics> GetDashboardStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var downloadDates = await context.Setups.AsNoTracking()
            .Select(item => item.DownloadedAtUtc)
            .ToListAsync(cancellationToken);
        return new DashboardStatistics(
            await context.Setups.CountAsync(cancellationToken),
            await context.Setups.CountAsync(item => item.Status == SetupStatus.AVerifier, cancellationToken),
            await context.Setups.CountAsync(item => item.Status == SetupStatus.Valide, cancellationToken),
            await context.Setups.CountAsync(item => item.Status == SetupStatus.EnvoyeVersGarage61, cancellationToken),
            await context.Setups.Select(item => item.Provider).Distinct().CountAsync(cancellationToken),
            downloadDates.Count == 0 ? null : downloadDates.Max());
    }

    public async Task<IReadOnlyList<SetupEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.Setups.AsNoTracking()
            .OrderByDescending(item => item.DownloadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SetupEntity>> GetToReviewAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.Setups.AsNoTracking()
            .Where(item => item.Status == SetupStatus.AVerifier)
            .OrderByDescending(item => item.DownloadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SetupChangeHistoryEntity>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.SetupChangeHistory.AsNoTracking()
            .OrderByDescending(item => item.ChangedAtUtc)
            .Take(1000)
            .ToListAsync(cancellationToken);
    }
}
