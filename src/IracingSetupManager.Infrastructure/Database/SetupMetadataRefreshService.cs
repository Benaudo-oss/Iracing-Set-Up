using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupMetadataRefreshService(
    ISetupDbContextFactory contextFactory,
    SetupMetadataAnalyzer analyzer)
{
    private const string Unknown = "À identifier";

    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setups = await context.Setups.ToListAsync(cancellationToken);
        var updated = 0;

        foreach (var setup in setups)
        {
            var metadata = analyzer.Analyze(
                setup.OriginalFileName,
                new SetupMetadata(
                    setup.Provider,
                    setup.Category,
                    setup.Car,
                    setup.Track,
                    setup.TrackConfiguration,
                    setup.Season,
                    setup.SetupType,
                    setup.Week,
                    setup.WeekKind));

            var changed = false;
            changed |= AssignKnown(metadata.Provider, setup.Provider, value => setup.Provider = value);
            changed |= AssignKnown(metadata.Category, setup.Category, value => setup.Category = value);
            changed |= AssignKnown(metadata.Car, setup.Car, value => setup.Car = value);
            changed |= AssignKnown(metadata.Track, setup.Track, value => setup.Track = value);
            changed |= AssignKnown(metadata.TrackConfiguration, setup.TrackConfiguration, value => setup.TrackConfiguration = value);
            changed |= AssignKnown(metadata.Season, setup.Season, value => setup.Season = value);
            if (setup.Week is null && metadata.Week is not null)
            {
                setup.Week = metadata.Week;
                setup.WeekKind = metadata.EffectiveWeekKind;
                changed = true;
            }
            else if (setup.Week is null && setup.WeekKind == IracingSetupManager.Core.Setups.SetupWeekKind.Unknown &&
                     metadata.EffectiveWeekKind != IracingSetupManager.Core.Setups.SetupWeekKind.Unknown)
            {
                setup.WeekKind = metadata.EffectiveWeekKind;
                changed = true;
            }
            changed |= AssignKnown(metadata.SetupType, setup.SetupType, value => setup.SetupType = value);
            if (changed)
                updated++;
        }

        if (updated > 0)
            await context.SaveChangesAsync(cancellationToken);

        return updated;
    }

    private static bool AssignKnown(string? candidate, string? current, Action<string> assign)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Equals(Unknown, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, current, StringComparison.Ordinal))
            return false;

        assign(candidate);
        return true;
    }
}
