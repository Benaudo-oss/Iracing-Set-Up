using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using System.Text.RegularExpressions;

namespace IracingSetupManager.Infrastructure.Iracing;

public enum IracingConflictChoice
{
    None,
    Skip,
    KeepBoth
}

public sealed record IracingCopyPlanItem(
    Guid SetupId,
    string OriginalFileName,
    string Car,
    string SourcePath,
    string DestinationPath,
    int? Week,
    bool HasConflict,
    IracingConflictChoice ConflictChoice = IracingConflictChoice.None);

public sealed record IracingCopyResult(int Copied, int Skipped);

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
        var setups = await context.Setups.AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.Status == SetupStatus.Valide)
            .OrderBy(item => item.Car)
            .ThenBy(item => item.OriginalFileName)
            .ToListAsync(cancellationToken);

        return setups.Select(setup =>
        {
            var week = ReadWeek(setup.OriginalFileName);
            if (weekOverrides?.TryGetValue(setup.Id, out var overriddenWeek) == true)
            {
                if (overriddenWeek is < 1 or > 13) throw new ArgumentOutOfRangeException(nameof(weekOverrides), "La semaine doit être comprise entre 1 et 13.");
                week = overriddenWeek;
            }

            var carFolder = SetupMetadataAnalyzer.ResolveIracingFolderName(setup.Car, availableCarFolders)
                ?? SanitizeSegment(setup.Car);
            var season = SanitizeSegment(setup.Season ?? "Saison inconnue").Replace(' ', '_');
            var weekFolder = week is null ? "Week inconnue" : $"Week {week:00}";
            var dynamicSegments = layout.Select(element => element switch
            {
                "Season" => season,
                "Track" => SanitizeSegment(setup.Track),
                "Provider" => SanitizeSegment(setup.Provider),
                "Week" => weekFolder,
                _ => throw new InvalidOperationException("L’arborescence de copie iRacing est invalide.")
            });
            var destination = SecurePath.EnsureChildOf(Path.Combine([root, carFolder, "Garage 61", .. dynamicSegments, setup.OriginalFileName]), root);
            return new IracingCopyPlanItem(
                setup.Id,
                setup.OriginalFileName,
                setup.Car,
                setup.ArchivePath,
                destination,
                week,
                File.Exists(destination));
        }).ToList();
    }

    public async Task<IracingCopyResult> ExecuteAsync(
        IReadOnlyCollection<IracingCopyPlanItem> plan,
        bool confirmed,
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

        if (plan.Any(item => item.Week is null))
        {
            throw new InvalidOperationException("Indiquez une semaine comprise entre 1 et 13 pour chaque setup avant la copie.");
        }

        var planIds = plan.Select(item => item.SetupId).Distinct().ToArray();
        await using (var context = contextFactory.Create())
        {
            var validatedIds = await context.Setups.AsNoTracking()
                .Where(item => planIds.Contains(item.Id) && item.Status == SetupStatus.Valide)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            if (validatedIds.Count != planIds.Length)
            {
                throw new InvalidOperationException("La copie contient un setup qui n’est plus validé.");
            }
        }

        var copied = 0;
        var skipped = 0;
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

    private static string SanitizeSegment(string value)
    {
        var result = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? "Voiture à identifier" : result;
    }


    private static int? ReadWeek(string fileName)
    {
        var match = WeekPattern.Match(fileName);
        return match.Success && int.TryParse(match.Groups["week"].Value, out var week) ? week : null;
    }
}
