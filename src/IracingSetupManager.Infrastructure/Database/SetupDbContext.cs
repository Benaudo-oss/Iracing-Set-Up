using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupDbContext(DbContextOptions<SetupDbContext> options) : DbContext(options)
{
    private static readonly TimeSpan[] BusyRetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500)
    ];

    public DbSet<SetupEntity> Setups => Set<SetupEntity>();

    public DbSet<ApplicationSettingEntity> ApplicationSettings => Set<ApplicationSettingEntity>();

    public DbSet<SetupChangeHistoryEntity> SetupChangeHistory => Set<SetupChangeHistoryEntity>();

    public DbSet<TrackCatalogEntity> TrackCatalog => Set<TrackCatalogEntity>();

    public DbSet<RecognitionAliasEntity> RecognitionAliases => Set<RecognitionAliasEntity>();

    public DbSet<MonitoredFileStateEntity> MonitoredFileStates => Set<MonitoredFileStateEntity>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<SetupEntity>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.DownloadedAtUtcSortKey = entry.Entity.DownloadedAtUtc.ToUnixTimeMilliseconds();
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
            catch (Exception exception) when (
                attempt < BusyRetryDelays.Length &&
                IsTransientSqliteLock(exception))
            {
                await Task.Delay(BusyRetryDelays[attempt], cancellationToken);
            }
        }
    }

    private static bool IsTransientSqliteLock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException { SqliteErrorCode: 5 or 6 }) return true;
        }

        return false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var setup = modelBuilder.Entity<SetupEntity>();
        setup.ToTable("Setups");
        setup.HasKey(item => item.Id);
        setup.Property(item => item.OriginalFileName).HasMaxLength(512).IsRequired();
        setup.Property(item => item.Provider).HasMaxLength(64).IsRequired();
        setup.Property(item => item.Category).HasMaxLength(64).IsRequired();
        setup.Property(item => item.Car).HasMaxLength(256).IsRequired();
        setup.Property(item => item.Track).HasMaxLength(256).IsRequired();
        setup.Property(item => item.TrackConfiguration).HasMaxLength(256);
        setup.Property(item => item.Season).HasMaxLength(128);
        setup.Property(item => item.Week);
        setup.Property(item => item.WeekKind).HasConversion<string>().HasMaxLength(32);
        setup.Property(item => item.SetupType).HasMaxLength(128).IsRequired();
        setup.Property(item => item.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        setup.Property(item => item.ArchivePath).HasMaxLength(2048).IsRequired();
        setup.Property(item => item.SourceKind).HasConversion<string>().HasMaxLength(64);
        setup.Property(item => item.SourcePath).HasMaxLength(2048);
        setup.Property(item => item.Status).HasConversion<string>().HasMaxLength(64);
        setup.Property(item => item.Comment).HasMaxLength(4000);
        setup.Property(item => item.IracingCopyCount).HasDefaultValue(0);
        setup.Property(item => item.IracingTeamCopyCount).HasDefaultValue(0);
        setup.Property(item => item.DownloadedAtUtcSortKey).HasDefaultValue(0);
        setup.HasIndex(item => item.Sha256).IsUnique();
        setup.HasIndex(item => item.OriginalFileName);
        setup.HasIndex(item => new { item.Provider, item.Category, item.Status });
        setup.HasIndex(item => new { item.Car, item.Track, item.Season });
        setup.HasIndex(item => new { item.Season, item.Week });
        setup.HasIndex(item => new { item.WeekKind, item.Week });
        setup.HasIndex(item => item.DownloadedAtUtcSortKey);

        var history = modelBuilder.Entity<SetupChangeHistoryEntity>();
        history.ToTable("SetupChangeHistory");
        history.HasKey(item => item.Id);
        history.Property(item => item.OriginalFileName).HasMaxLength(512).IsRequired();
        history.Property(item => item.ChangeType).HasMaxLength(64).IsRequired();
        history.Property(item => item.PreviousStatus).HasConversion<string>().HasMaxLength(64);
        history.Property(item => item.NewStatus).HasConversion<string>().HasMaxLength(64);
        history.Property(item => item.PreviousComment).HasMaxLength(4000);
        history.Property(item => item.NewComment).HasMaxLength(4000);
        history.HasIndex(item => new { item.SetupId, item.ChangedAtUtc });
        history.HasIndex(item => item.ChangedAtUtc);

        var setting = modelBuilder.Entity<ApplicationSettingEntity>();
        setting.ToTable("ApplicationSettings");
        setting.HasKey(item => item.Key);
        setting.Property(item => item.Key).HasMaxLength(128);
        setting.Property(item => item.Value).HasMaxLength(4096).IsRequired();

        var track = modelBuilder.Entity<TrackCatalogEntity>();
        track.ToTable("TrackCatalog");
        track.HasKey(item => item.IracingFolderName);
        track.Property(item => item.IracingFolderName).HasMaxLength(256);
        track.Property(item => item.TrackName).HasMaxLength(256).IsRequired();
        track.Property(item => item.Configuration).HasMaxLength(256);
        track.Property(item => item.NormalizedAlias).HasMaxLength(256).IsRequired();
        track.HasIndex(item => item.NormalizedAlias);

        var recognitionAlias = modelBuilder.Entity<RecognitionAliasEntity>();
        recognitionAlias.ToTable("RecognitionAliases");
        recognitionAlias.HasKey(item => item.Id);
        recognitionAlias.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        recognitionAlias.Property(item => item.Alias).HasMaxLength(128).IsRequired();
        recognitionAlias.Property(item => item.NormalizedAlias).HasMaxLength(128).IsRequired();
        recognitionAlias.Property(item => item.CanonicalValue).HasMaxLength(256).IsRequired();
        recognitionAlias.HasIndex(item => new { item.Kind, item.NormalizedAlias }).IsUnique();

        var monitoredFile = modelBuilder.Entity<MonitoredFileStateEntity>();
        monitoredFile.ToTable("MonitoredFileStates");
        monitoredFile.HasKey(item => item.PathKey);
        monitoredFile.Property(item => item.PathKey).HasMaxLength(64).IsFixedLength();
        monitoredFile.HasIndex(item => item.LastExaminedAtUtc);
    }
}
