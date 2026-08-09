using System.Security.Cryptography;
using System.Text;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class FileClassificationAndHashTests
{
    [Fact]
    public void ClassificationUsesSeasonTrackCarProviderAndType()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive");
        var metadata = new SetupMetadata("HYMO", "GT3", "Porsche 911 GT3 R", "Spa", "Grand Prix", "2026 S3", "Race");
        var result = new ArchivePathBuilder().BuildDirectory(root, metadata);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "2026 S3", "Spa", "Porsche 911 GT3 R", "HYMO", "Race"), result);
    }

    [Fact]
    public void ClassificationSanitizesInvalidFolderCharacters()
    {
        var metadata = new SetupMetadata("GO Setups", "GT3", "Ferrari:296", "Spa/GP", null, null, "Qualifying");
        var result = new ArchivePathBuilder().BuildDirectory(Path.GetTempPath(), metadata);
        Assert.DoesNotContain(':', Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(result))!));
        Assert.Contains("Saison inconnue", result);
    }

    [Fact]
    public async Task Sha256MatchesKnownVectorAndChangesWithContent()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "HashTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var first = Path.Combine(root, "first.sto"); var second = Path.Combine(root, "second.sto");
            await File.WriteAllTextAsync(first, "abc", new UTF8Encoding(false));
            await File.WriteAllTextAsync(second, "abcd", new UTF8Encoding(false));
            var calculator = new Sha256Calculator();
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("abc"))),
                await calculator.CalculateAsync(first),
                ignoreCase: true);
            Assert.NotEqual(await calculator.CalculateAsync(first), await calculator.CalculateAsync(second));
        }
        finally { Directory.Delete(root, true); }
    }
}
