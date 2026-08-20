using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using System.Text.RegularExpressions;
using IracingSetupManager.Core.Catalog;

namespace IracingSetupManager.Infrastructure.Iracing;

public enum IracingConflictChoice
{
    None,
    Skip,
    KeepBoth
}

public enum IracingCopyTarget
{
    Personal,
    Team
}

public sealed record IracingCopyPlanItem(
    Guid SetupId,
    string OriginalFileName,
    string Car,
    string SourcePath,
    string DestinationPath,
    int? Week,
    SetupWeekKind WeekKind,
    bool HasConflict,
    IracingConflictChoice ConflictChoice = IracingConflictChoice.None);

public sealed record IracingCopyResult(int Copied, int Skipped);
public sealed record SetupWeekChoice(int? Week, SetupWeekKind Kind)
{
    public static SetupWeekChoice Numeric(int week) => new(week, SetupWeekKind.Numeric);
    public static SetupWeekChoice Nec { get; } = new(null, SetupWeekKind.Nec);
    public static SetupWeekChoice NoWeek { get; } = new(null, SetupWeekKind.NoWeek);
}

public sealed class IracingCopyService(ISetupDbContextFactory contextFactory, IracingPathLayoutService? pathLayoutService = null)
{
    private readonly IracingPathLayoutService pathLayout = pathLayoutService ?? new IracingPathLayoutService(contextFactory);
    private static readonly Regex WeekPattern = new(@"(?:^|[_\- ])W(?<week>0?[1-9]|1[0-3])(?:[_\- .]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? DetectSetupsFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var detected = Path.Combine(documents, "iRacing", "setups");
        return Directory.Exists(detected) ? detected : null;
    }

    public async Task<IReadOnlyList<IracingCopyPlanItem>> CreatePlanAsync(
        IReadOnlyCollection<Guid> setupIds,
        string iracingSetupsFolder,
        IReadOnlyDictionary<Guid, int>? weekOverrides = null,
        string? teamName = null,
        IReadOnlyDictionary<Guid, SetupWeekChoice>? weekChoices = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iracingSetupsFolder);
        var root = SecurePath.GetFullPath(iracingSetupsFolder);
        var availableCarFolders = Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name)).Cast<string>().ToList()
            : [];
        var ids = setupIds.Distinct().ToArray();
        var layout = await pathLayout.GetAsync(cancellationToken);

        await using var context = contextFactory.Create();
        var setups = (await context.Setups
            .Where(item => ids.Contains(item.Id) && item.Status == SetupStatus.Valide)
            .ToListAsync(cancellationToken))
            .Where(item => IsIdentifiedCar(item.Car))
            .OrderBy(item => item.Car)
            .ThenBy(item => item.OriginalFileName)
            .ToList();

        var weekMetadataChanged = false;
        foreach (var setup in setups)
        {
            var existingWeek = setup.Week ?? ReadWeek(setup.OriginalFileName);
            if (SetupWeekPresentation.EffectiveKind(existingWeek, setup.WeekKind) != SetupWeekKind.Unknown) continue;
            if (weekChoices?.TryGetValue(setup.Id, out var choice) != true || choice is null) continue;
            if (choice.Kind == SetupWeekKind.Numeric && choice.Week is not (>= 1 and <= 13))
                throw new ArgumentOutOfRangeException(nameof(weekChoices), "La Week numérique doit être comprise entre 1 et 13.");
            if (choice.Kind is SetupWeekKind.Unknown || choice.Kind == SetupWeekKind.Numeric && choice.Week is null)
                throw new ArgumentException("Le choix de Week est invalide.", nameof(weekChoices));
            setup.Week = choice.Kind == SetupWeekKind.Numeric ? choice.Week : null;
            setup.WeekKind = choice.Kind;
            weekMetadataChanged = true;
        }
        if (weekMetadataChanged) await context.SaveChangesAsync(cancellationToken);

        return setups.Select(setup =>
        {
            var isTeamCopy = !string.IsNullOrWhiteSpace(teamName);
            var week = setup.Week ?? ReadWeek(setup.OriginalFileName);
            var weekKind = SetupWeekPresentation.EffectiveKind(week, setup.WeekKind);
            if (weekKind == SetupWeekKind.Unknown && weekOverrides?.TryGetValue(setup.Id, out var overriddenWeek) == true)
            {
                if (overriddenWeek is < 1 or > 13) throw new ArgumentOutOfRangeException(nameof(weekOverrides), "La semaine doit être comprise entre 1 et 13.");
                week = overriddenWeek;
                weekKind = SetupWeekKind.Numeric;
            }

            var carFolder = SetupMetadataAnalyzer.ResolveIracingFolderName(setup.Car, availableCarFolders)
                ?? SanitizeSegment(setup.Car);
            var season = SanitizeSegment(setup.Season ?? "Saison inconnue").Replace(' ', '_');
            var weekFolder = weekKind switch
            {
                SetupWeekKind.Numeric when week is not null => isTeamCopy ? $"week_{week}" : $"Week {week:00}",
                SetupWeekKind.Nec => isTeamCopy ? "week_NEC" : "Week NEC",
                SetupWeekKind.NoWeek => isTeamCopy ? "sans_week" : "Sans Week",
                _ => isTeamCopy ? "week_inconnue" : "Week inconnue"
            };
            var dynamicSegments = layout.Select(element => element switch
            {
                "Season" => season,
                "Track" => SanitizeSegment(setup.Track),
                "Provider" => isTeamCopy
                    ? GetTeamProviderFolder(setup.Provider)
                    : SanitizeSegment(setup.Provider),
                "Week" => weekFolder,
                _ => throw new InvalidOperationException("L’arborescence de copie iRacing est invalide.")
            });
            var fixedSegments = !isTeamCopy
                ? new[] { root, carFolder, "Garage 61" }
                : new[] { root, carFolder, $"Garage 61 - {SanitizeSegment(teamName!)}" };
            var destination = SecurePath.EnsureChildOf(
                Path.Combine([.. fixedSegments, .. dynamicSegments, setup.OriginalFileName]),
                root);
            return new IracingCopyPlanItem(
                setup.Id,
                setup.OriginalFileName,
                setup.Car,
                setup.ArchivePath,
                destination,
                week,
                weekKind,
                File.Exists(destination));
        }).ToList();
    }

    public async Task<IracingCopyResult> ExecuteAsync(
        IReadOnlyCollection<IracingCopyPlanItem> plan,
        bool confirmed,
        IracingCopyTarget target = IracingCopyTarget.Personal,
        CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            throw new InvalidOperationException("La copie doit être confirmée après affichage de l’aperçu.");
        }

        if (plan.Any(item => item.HasConflict && item.ConflictChoice == IracingConflictChoice.None))
        {
            throw new InvalidOperationException("Chaque conflit doit être résolu avant la copie.");
        }

        if (plan.Any(item => item.WeekKind == SetupWeekKind.Unknown))
        {
            throw new InvalidOperationException("Indiquez une semaine comprise entre 1 et 13 pour chaque setup avant la copie.");
        }

        var planIds = plan.Select(item => item.SetupId).Distinct().ToArray();
        await using (var context = contextFactory.Create())
        {
            var validatedSetups = await context.Setups.AsNoTracking()
                .Where(item => planIds.Contains(item.Id) && item.Status == SetupStatus.Valide)
                .Select(item => new { item.Id, item.Car })
                .ToListAsync(cancellationToken);
            if (validatedSetups.Count != planIds.Length)
            {
                throw new InvalidOperationException("La copie contient un setup qui n’est plus validé.");
            }
            if (validatedSetups.Any(item => !IsIdentifiedCar(item.Car)))
            {
                throw new InvalidOperationException("La voiture doit être identifiée avant la copie vers iRacing.");
            }
        }

        var copied = 0;
        var skipped = 0;
        var copiedIds = new List<Guid>();
        foreach (var item in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.HasConflict && item.ConflictChoice == IracingConflictChoice.Skip)
            {
                skipped++;
                continue;
            }

            if (!File.Exists(item.SourcePath))
            {
                throw new FileNotFoundException("Le fichier original est absent de l’archive.", item.SourcePath);
            }

            var destination = item.HasConflict
                ? FindAvailablePath(item.DestinationPath)
                : item.DestinationPath;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(item.SourcePath, destination, overwrite: false);
            copied++;
            copiedIds.Add(item.SetupId);
        }

        if (copiedIds.Count > 0)
        {
            await using var context = contextFactory.Create();
            var copiedSetups = await context.Setups
                .Where(item => copiedIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            var copiedAt = DateTimeOffset.UtcNow;
            foreach (var setup in copiedSetups)
            {
                if (target == IracingCopyTarget.Team)
                {
                    setup.LastCopiedToIracingTeamAtUtc = copiedAt;
                    setup.IracingTeamCopyCount++;
                }
                else
                {
                    setup.LastCopiedToIracingAtUtc = copiedAt;
                    setup.IracingCopyCount++;
                }

                context.SetupChangeHistory.Add(new SetupChangeHistoryEntity
                {
                    SetupId = setup.Id,
                    OriginalFileName = setup.OriginalFileName,
                    ChangeType = target == IracingCopyTarget.Team
                        ? "Copie vers iRacing Team"
                        : "Copie vers iRacing",
                    ChangedAtUtc = copiedAt
                });
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        return new IracingCopyResult(copied, skipped);
    }

    private static string FindAvailablePath(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string GetTeamProviderFolder(string provider) =>
        SanitizeSegment(SetupCatalog.GetTeamFolderCode(provider));

    private static string SanitizeSegment(string value)
    {
        var result = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? "Voiture à identifier" : result;
    }


    private static bool IsIdentifiedCar(string? car) =>
        !string.IsNullOrWhiteSpace(car) &&
        !car.Equals("À identifier", StringComparison.OrdinalIgnoreCase) &&
        !car.Equals("Voiture à identifier", StringComparison.OrdinalIgnoreCase);

    private static int? ReadWeek(string fileName)
    {
        var match = WeekPattern.Match(fileName);
        return match.Success && int.TryParse(match.Groups["week"].Value, out var week) ? week : null;
    }
}
