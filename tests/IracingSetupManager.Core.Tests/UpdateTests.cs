using System.Net;
using System.Security.Cryptography;
using System.Text;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Settings;
using IracingSetupManager.Integrations.Updates;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class UpdateTests
{
    [Fact]
    public async Task FindsGitHubReleaseDownloadsAndVerifiesSha256()
    {
        var root = CreateRoot();
        try
        {
            var package = Encoding.UTF8.GetBytes("simulated installer");
            var hash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
            var service = CreateService(root, package, hash);
            var availability = await service.CheckAsync(new Version(0, 1, 1, 0));
            Assert.True(availability.IsAvailable);
            Assert.Equal(new Version(0, 1, 1, 1), availability.AvailableVersion);
            Assert.Contains("Nouveautés simulées", availability.ReleaseNotes);

            var downloaded = await service.DownloadAndVerifyAsync(availability);
            Assert.Equal(hash, downloaded.Sha256);
            Assert.Equal(package, await File.ReadAllBytesAsync(downloaded.InstallerPath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsPackageWhenPublishedSha256DoesNotMatch()
    {
        var root = CreateRoot();
        try
        {
            var service = CreateService(root, Encoding.UTF8.GetBytes("modified"), new string('a', 64));
            var availability = await service.CheckAsync(new Version(0, 1, 0));
            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAndVerifyAsync(availability));
            Assert.Empty(Directory.EnumerateFiles(root, "*.exe"));
            Assert.Empty(Directory.EnumerateFiles(root, "*.download"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FallsBackToPublicReleasePageWhenGitHubApiRateLimitIsReached()
    {
        var root = CreateRoot();
        try
        {
            var handler = new SimulatedGitHubHandler(request =>
            {
                if (request.RequestUri!.Host == "api.github.com")
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.RequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://github.com/Benaudo-oss/Iracing-Set-Up/releases/tag/v0.1.1.3");
                return response;
            });
            var service = new GitHubReleaseUpdateService(new HttpClient(handler), root);

            var availability = await service.CheckAsync(new Version(0, 1, 1, 2));

            Assert.True(availability.IsAvailable);
            Assert.Equal(new Version(0, 1, 1, 3), availability.AvailableVersion);
            Assert.EndsWith("IracingSetupManager-0.1.1.3-win-x64-setup.exe", availability.DownloadUri!.AbsoluteUri);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task IgnoredVersionIsNotOfferedButLaterVersionIs()
    {
        var root = CreateRoot();
        try
        {
            var factory = new LocalSetupDbContextFactory(Path.Combine(root, "settings.db"));
            await new SetupDatabase(factory).InitializeAsync();
            var preferences = new UpdatePreferenceService(factory);
            await preferences.IgnoreAsync(new Version(0, 2, 0));
            Assert.False(await preferences.ShouldOfferAsync(new Version(0, 2, 0)));
            Assert.True(await preferences.ShouldOfferAsync(new Version(0, 2, 1)));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }

    [Fact]
    public void RollbackSelectsNewestInstallerOlderThanInstalledVersion()
    {
        var root = CreateRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "IracingSetupManager-0.1.0-win-x64-setup.exe"), "old");
            File.WriteAllText(Path.Combine(root, "IracingSetupManager-0.2.0.1-win-x64-setup.exe"), "previous");
            File.WriteAllText(Path.Combine(root, "IracingSetupManager-0.3.0-win-x64-setup.exe"), "current");
            var result = new UpdateInstallerLauncher(root).FindPreviousInstaller(new Version(0, 3, 0));
            Assert.EndsWith("IracingSetupManager-0.2.0.1-win-x64-setup.exe", result);
        }
        finally { Directory.Delete(root, true); }
    }

    private static GitHubReleaseUpdateService CreateService(string root, byte[] package, string hash)
    {
        var json = """
        {"tag_name":"v0.1.1.1","body":"Nouveautés simulées","assets":[
          {"name":"IracingSetupManager-0.1.1.1-win-x64-setup.exe","browser_download_url":"https://github.com/test/update.exe"},
          {"name":"IracingSetupManager-0.1.1.1-win-x64-setup.exe.sha256","browser_download_url":"https://github.com/test/update.sha256"}]}
        """;
        var handler = new SimulatedGitHubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/repos/Benaudo-oss/Iracing-Set-Up/releases/latest" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") },
            "/test/update.exe" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) },
            "/test/update.sha256" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{hash}  installer.exe") },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        return new GitHubReleaseUpdateService(new HttpClient(handler), root);
    }

    private static string CreateRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "UpdateTests", Guid.NewGuid().ToString("N"))).FullName;
    private sealed class SimulatedGitHubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
