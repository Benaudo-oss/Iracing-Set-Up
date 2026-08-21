using System.Data;
using System.Globalization;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed record DashboardStatistics(
    int Total,
    int ToReview,
    int Validated,
    int CopiedToIracingTeam,
    int ProviderCount,
    DateTimeOffset? LastDownloadUtc);

public sealed record DashboardCount(string Label, int Count);

public sealed record DashboardStatusCount(SetupStatus Status, int Count);

public sealed record DashboardBreakdown(
    IReadOnlyList<DashboardCount> Providers,
    IReadOnlyList<DashboardStatusCount> Statuses);

public sealed record SetupPageRequest(
    int Skip,
    int Take,
    string? Search = null,
    string? Provider = null,
    string? Category = null,
    string? Car = null,
    string? Track = null,
    string? Season = null,
    string? Week = null,
    string? Status = null,
    string? Identification = null,
    bool ToReviewOnly = false,
    bool ValidatedOnly = false,
    string? CopyState = null,
    bool TeamCopy = false);

public sealed record SetupPage(IReadOnlyList<SetupEntity> Items, int TotalCount);

public sealed record SetupFilterOptions(
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Cars,
    IReadOnlyList<string> Tracks,
    IReadOnlyList<string> Weeks,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> Seasons,
    IReadOnlyList<string> SetupTypes);

public sealed record HistoryPage(IReadOnlyList<SetupChangeHistoryEntity> Items, int TotalCount);

public sealed class SetupQueryService(ISetupDbContextFactory contextFactory)
{
    private const string Unidentified = "À identifier";

    public async Task<SetupEntity?> GetBySha256Async(
        string sha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        await using var context = contextFactory.Create();
        return await context.Setups.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Sha256 == sha256, cancellationToken);
    }

    public async Task<DashboardStatistics> GetDashboardStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    COUNT(CASE WHEN "Status" <> $missing THEN 1 END),
                    COUNT(CASE WHEN "Status" = $review THEN 1 END),
                    COUNT(CASE WHEN "Status" = $validated THEN 1 END),
                    COUNT(CASE WHEN "Status" <> $missing AND "LastCopiedToIracingTeamAtUtc" IS NOT NULL THEN 1 END),
                    COUNT(DISTINCT CASE WHEN "Status" <> $missing THEN "Provider" END),
                    MAX(CASE WHEN "Status" <> $missing THEN "DownloadedAtUtc" END)
                FROM "Setups";
                """;
            AddParameter(command, "$missing", SetupStatus.FichierManquant.ToString());
            AddParameter(command, "$review", SetupStatus.AVerifier.ToString());
            AddParameter(command, "$validated", SetupStatus.Valide.ToString());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return new DashboardStatistics(
                ReadInt32(reader, 0),
                ReadInt32(reader, 1),
                ReadInt32(reader, 2),
                ReadInt32(reader, 3),
                ReadInt32(reader, 4),
                ReadDateTimeOffset(reader, 5));
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    public async Task<DashboardBreakdown> GetDashboardBreakdownAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var providerGroups = await context.Setups.AsNoTracking()
            .Where(item => item.Status != SetupStatus.FichierManquant)
            .GroupBy(item => item.Provider)
            .Select(group => new { Label = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var statusGroups = await context.Setups.AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var providers = providerGroups
            .Select(item => new DashboardCount(item.Label, item.Count))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .ToList();
        var statuses = statusGroups
            .Select(item => new DashboardStatusCount(item.Status, item.Count))
            .ToList();

        return new DashboardBreakdown(providers, statuses);
    }

    public async Task<IReadOnlyList<SetupEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setups = await context.Setups.AsNoTracking().ToListAsync(cancellationToken);
        return setups.OrderByDescending(item => item.DownloadedAtUtc).ToList();
    }

    public async Task<SetupPage> GetPageAsync(
        SetupPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Skip < 0) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(request));

        await using var context = contextFactory.Create();
        var query = ApplyFilters(context.Setups.AsNoTracking(), request);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.DownloadedAtUtcSortKey)
            .ThenByDescending(item => item.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(cancellationToken);
        return new SetupPage(items, totalCount);
    }

    public async Task<SetupFilterOptions> GetFilterOptionsAsync(
        bool toReviewOnly = false,
        SetupStatus? requiredStatus = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var query = context.Setups.AsNoTracking();
        if (toReviewOnly) query = query.Where(item => item.Status == SetupStatus.AVerifier);
        if (requiredStatus is not null) query = query.Where(item => item.Status == requiredStatus);

        var providers = await DistinctAsync(query.Select(item => item.Provider), cancellationToken);
        var categories = await DistinctAsync(query.Select(item => item.Category), cancellationToken);
        var cars = await DistinctAsync(query.Select(item => item.Car), cancellationToken);
        var tracks = await DistinctAsync(query.Select(item => item.Track), cancellationToken);
        var seasons = await DistinctAsync(query.Select(item => item.Season), cancellationToken);
        var setupTypes = await DistinctAsync(query.Select(item => item.SetupType), cancellationToken);
        var weekValues = await query.Select(item => new { item.Week, item.WeekKind })
            .Distinct()
            .ToListAsync(cancellationToken);
        var statuses = await query.Select(item => item.Status).Distinct().ToListAsync(cancellationToken);

        return new SetupFilterOptions(
            providers,
            categories,
            cars,
            tracks,
            weekValues.Select(item => SetupWeekPresentation.Display(item.Week, item.WeekKind))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            statuses.Select(StatusDisplay)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            seasons,
            setupTypes);
    }

    public async Task<IReadOnlyList<Guid>> GetIdsByStatusAsync(
        SetupStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.Setups.AsNoTracking()
            .Where(item => item.Status == status)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SetupEntity>> GetToReviewAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setups = await context.Setups.AsNoTracking()
            .Where(item => item.Status == SetupStatus.AVerifier)
            .ToListAsync(cancellationToken);
        return setups.OrderByDescending(item => item.DownloadedAtUtc).ToList();
    }

    public async Task<IReadOnlyList<SetupEntity>> GetValidatedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        var setups = await context.Setups.AsNoTracking()
            .Where(item => item.Status == SetupStatus.Valide)
            .ToListAsync(cancellationToken);
        return setups.OrderByDescending(item => item.DownloadedAtUtc).ToList();
    }

    public async Task<IReadOnlyList<SetupChangeHistoryEntity>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.SetupChangeHistory.AsNoTracking()
            .OrderByDescending(item => item.Id)
            .Take(1000)
            .ToListAsync(cancellationToken);
    }

    public async Task<HistoryPage> GetHistoryPageAsync(
        int skip,
        int take,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        await using var context = contextFactory.Create();
        var query = context.SetupChangeHistory.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(item =>
                EF.Functions.Like(item.OriginalFileName, pattern, "\\") ||
                EF.Functions.Like(item.ChangeType, pattern, "\\"));
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new HistoryPage(items, totalCount);
    }

    public async Task<int> ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.Create();
        return await context.SetupChangeHistory.ExecuteDeleteAsync(cancellationToken);
    }

    private static IQueryable<SetupEntity> ApplyFilters(
        IQueryable<SetupEntity> query,
        SetupPageRequest request)
    {
        if (request.ToReviewOnly) query = query.Where(item => item.Status == SetupStatus.AVerifier);
        if (request.ValidatedOnly) query = query.Where(item => item.Status == SetupStatus.Valide);
        if (!string.IsNullOrWhiteSpace(request.Provider)) query = query.Where(item => item.Provider == request.Provider);
        if (!string.IsNullOrWhiteSpace(request.Category)) query = query.Where(item => item.Category == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Car)) query = query.Where(item => item.Car == request.Car);
        if (!string.IsNullOrWhiteSpace(request.Track)) query = query.Where(item => item.Track == request.Track);
        if (!string.IsNullOrWhiteSpace(request.Season)) query = query.Where(item => item.Season == request.Season);

        if (request.CopyState == "À copier")
            query = request.TeamCopy
                ? query.Where(item => item.IracingTeamCopyCount == 0)
                : query.Where(item => item.IracingCopyCount == 0);
        else if (request.CopyState == "Déjà copiés")
            query = request.TeamCopy
                ? query.Where(item => item.IracingTeamCopyCount > 0)
                : query.Where(item => item.IracingCopyCount > 0);

        if (TryParseStatus(request.Status, out var status)) query = query.Where(item => item.Status == status);
        query = ApplyWeekFilter(query, request.Week);

        var identification = request.Identification?.Trim();
        if (identification is "À identifier" or "Identifiés")
        {
            var wantsUnidentified = identification == "À identifier";
            query = wantsUnidentified
                ? query.Where(item =>
                    item.Provider == Unidentified || item.Category == Unidentified || item.Car == Unidentified ||
                    item.Track == Unidentified || item.Season == null || item.Season == "" || item.Season == Unidentified)
                : query.Where(item =>
                    item.Provider != Unidentified && item.Category != Unidentified && item.Car != Unidentified &&
                    item.Track != Unidentified && item.Season != null && item.Season != "" && item.Season != Unidentified);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLikePattern(request.Search.Trim())}%";
            query = query.Where(item =>
                EF.Functions.Like(item.OriginalFileName, pattern, "\\") ||
                EF.Functions.Like(item.Provider, pattern, "\\") ||
                EF.Functions.Like(item.Category, pattern, "\\") ||
                EF.Functions.Like(item.Car, pattern, "\\") ||
                EF.Functions.Like(item.Track, pattern, "\\") ||
                (item.TrackConfiguration != null && EF.Functions.Like(item.TrackConfiguration, pattern, "\\")) ||
                (item.Season != null && EF.Functions.Like(item.Season, pattern, "\\")) ||
                EF.Functions.Like(item.SetupType, pattern, "\\"));
        }

        return query;
    }

    private static IQueryable<SetupEntity> ApplyWeekFilter(IQueryable<SetupEntity> query, string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected)) return query;
        if (selected.Equals("Week NEC", StringComparison.OrdinalIgnoreCase))
            return query.Where(item => item.WeekKind == SetupWeekKind.Nec && item.Week == null);
        if (selected.Equals("Sans Week", StringComparison.OrdinalIgnoreCase))
            return query.Where(item => item.WeekKind == SetupWeekKind.NoWeek && item.Week == null);
        if (selected.Equals("Week inconnue", StringComparison.OrdinalIgnoreCase))
            return query.Where(item => item.Week == null && item.WeekKind != SetupWeekKind.Nec && item.WeekKind != SetupWeekKind.NoWeek);
        if (int.TryParse(selected.Replace("Week", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(), out var week) && week is >= 1 and <= 13)
            return query.Where(item => item.Week == week);
        return query;
    }

    private static bool TryParseStatus(string? value, out SetupStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Equals("À vérifier", StringComparison.OrdinalIgnoreCase))
        {
            status = SetupStatus.AVerifier;
            return true;
        }
        if (value.Equals("Fichier manquant", StringComparison.OrdinalIgnoreCase))
        {
            status = SetupStatus.FichierManquant;
            return true;
        }
        return Enum.TryParse(value, true, out status);
    }

    private static string StatusDisplay(SetupStatus status) => status switch
    {
        SetupStatus.AVerifier => "À vérifier",
        SetupStatus.FichierManquant => "Fichier manquant",
        _ => status.ToString()
    };

    private static async Task<IReadOnlyList<string>> DistinctAsync(
        IQueryable<string?> query,
        CancellationToken cancellationToken)
    {
        var values = await query.Where(value => value != null && value != string.Empty)
            .Distinct()
            .ToListAsync(cancellationToken);
        return values.Select(value => value!)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt32(System.Data.Common.DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? 0
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static DateTimeOffset? ReadDateTimeOffset(System.Data.Common.DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var value = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }
}
