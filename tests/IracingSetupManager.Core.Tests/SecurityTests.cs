using System.IO.Compression;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void RedactorRemovesPasswordsTokensAndCookies()
    {
        var result = SensitiveDataRedactor.Redact("password=hunter2 token=abc Authorization: Bearer xyz Cookie: sid=private");
        Assert.DoesNotContain("hunter2", result); Assert.DoesNotContain("abc", result);
        Assert.DoesNotContain("xyz", result); Assert.DoesNotContain("private", result);
    }

    [Fact]
    public void SecurePathRejectsTraversalDeviceAndAlternateStreamPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "safe-root");
        Assert.Throws<InvalidDataException>(() => SecurePath.EnsureChildOf(Path.Combine(root, "..", "outside"), root));
        Assert.Throws<ArgumentException>(() => SecurePath.GetFullPath(@"\\.\PhysicalDrive0"));
        Assert.Throws<InvalidDataException>(() => SecurePath.ValidateArchiveEntry("setup.sto:secret"));
    }

    [Fact]
    public async Task ZipBombCompressionRatioIsRejectedBeforeExtraction()
    {
        var root = Path.Combine(Path.GetTempPath(), "SecureZipTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zip = Path.Combine(root, "bomb.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("large.sto", CompressionLevel.SmallestSize);
                await using var stream = entry.Open();
                await stream.WriteAsync(new byte[1024 * 1024]);
            }
            await Assert.ThrowsAsync<InvalidDataException>(() => new SecureZipExtractor().ExtractAsync(zip, Path.Combine(root, "out")));
            Assert.False(File.Exists(Path.Combine(root, "out", "large.sto")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BackupIsReadableAndSensitiveSourcePathsArePurged()
    {
        var root = Path.Combine(Path.GetTempPath(), "DatabaseSecurityTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "live.db"));
            await new SetupDatabase(factory).InitializeAsync();
            await using (var context = factory.Create())
            {
                context.Setups.Add(new SetupEntity
                {
                    Id = Guid.NewGuid(), OriginalFileName = "race.sto", Provider = "Test", Category = "GT3", Car = "Car", Track = "Spa", SetupType = "Race",
                    SizeInBytes = 1, Sha256 = new string('a', 64), ArchivePath = Path.Combine(root, "archive", "race.sto"), SourcePath = @"C:\Users\PrivateName\Downloads\race.sto",
                    Status = SetupStatus.Valide, DownloadedAtUtc = DateTimeOffset.UtcNow
                });
                await context.SaveChangesAsync();
            }
            Assert.Equal(1, await new SensitiveDataRetentionService(factory).PurgeUnneededSourcePathsAsync());
            var backupPath = await new DatabaseBackupService(factory).BackupAsync(Path.Combine(root, "backup.db"));
            var backupFactory = new LocalSetupDbContextFactory(backupPath);
            await using (var backup = backupFactory.Create())
            {
                var setup = Assert.Single(await backup.Setups.AsNoTracking().ToListAsync());
                Assert.Null(setup.SourcePath);
            }
            SqliteConnection.ClearAllPools();
        }
        finally { Directory.Delete(root, true); }
    }
}
