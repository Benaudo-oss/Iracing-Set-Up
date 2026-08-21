using System.Security.Cryptography;
using System.Text;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed record MonitoredFileSnapshot(
    string FullPath,
    long Length,
    long LastWriteTimeUtcTicks);

public sealed record MonitoredFileFingerprint(
    string PathKey,
    long Length,
    long LastWriteTimeUtcTicks);

public sealed class MonitoredFileStateStore(ISetupDbContextFactory contextFactory)
{
    public async Task<IReadOnlyList<MonitoredFileFingerprint>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.MonitoredFileStates.AsNoTracking()
            .Select(item => new MonitoredFileFingerprint(
                item.PathKey,
                item.Length,
                item.LastWriteTimeUtcTicks))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(
        IReadOnlyCollection<MonitoredFileSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        if (snapshots.Count == 0) return;
        var latestByPath = snapshots
            .Select(item => new MonitoredFileFingerprint(
                CreatePathKey(item.FullPath),
                item.Length,
                item.LastWriteTimeUtcTicks))
            .GroupBy(item => item.PathKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
        var paths = latestByPath.Select(item => item.PathKey).ToArray();

        await using var context = contextFactory.Create();
        var existing = await context.MonitoredFileStates
            .Where(item => paths.Contains(item.PathKey))
            .ToDictionaryAsync(item => item.PathKey, StringComparer.Ordinal, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var snapshot in latestByPath)
        {
            if (!existing.TryGetValue(snapshot.PathKey, out var entity))
            {
                context.MonitoredFileStates.Add(new MonitoredFileStateEntity
                {
                    PathKey = snapshot.PathKey,
                    Length = snapshot.Length,
                    LastWriteTimeUtcTicks = snapshot.LastWriteTimeUtcTicks,
                    LastExaminedAtUtc = now
                });
                continue;
            }

            entity.Length = snapshot.Length;
            entity.LastWriteTimeUtcTicks = snapshot.LastWriteTimeUtcTicks;
            entity.LastExaminedAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public static string CreatePathKey(string path)
    {
        var normalized = Path.GetFullPath(path).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
