using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Database;

public sealed class SetupDbContext(DbContextOptions<SetupDbContext> options) : DbContext(options)
{
    public DbSet<SetupEntity> Setups => Set<SetupEntity>();

    public DbSet<ApplicationSettingEntity> ApplicationSettings => Set<ApplicationSettingEntity>();

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
        setup.Property(item => item.SetupType).HasMaxLength(128).IsRequired();
        setup.Property(item => item.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        setup.Property(item => item.ArchivePath).HasMaxLength(2048).IsRequired();
        setup.Property(item => item.SourceKind).HasConversion<string>().HasMaxLength(64);
        setup.Property(item => item.SourcePath).HasMaxLength(2048);
        setup.Property(item => item.Status).HasConversion<string>().HasMaxLength(64);
        setup.Property(item => item.Comment).HasMaxLength(4000);
        setup.Property(item => item.Garage61Result).HasMaxLength(4000);
        setup.Property(item => item.Garage61SetupId).HasMaxLength(256);
        setup.Property(item => item.Garage61SetupUrl).HasMaxLength(2048);
        setup.HasIndex(item => item.Sha256).IsUnique();
        setup.HasIndex(item => item.OriginalFileName);
        setup.HasIndex(item => new { item.Provider, item.Category, item.Status });
        setup.HasIndex(item => new { item.Car, item.Track, item.Season });
        setup.HasIndex(item => new { item.IsPrivate, item.Garage61ExportApproved, item.Status });

        var setting = modelBuilder.Entity<ApplicationSettingEntity>();
        setting.ToTable("ApplicationSettings");
        setting.HasKey(item => item.Key);
        setting.Property(item => item.Key).HasMaxLength(128);
        setting.Property(item => item.Value).HasMaxLength(4096).IsRequired();
    }
}
