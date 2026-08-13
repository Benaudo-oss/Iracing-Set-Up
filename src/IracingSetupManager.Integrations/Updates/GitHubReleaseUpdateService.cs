using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IracingSetupManager.Integrations.Updates;

public sealed class GitHubReleaseUpdateService(HttpClient httpClient, string updateRoot) : IUpdateService
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/Benaudo-oss/Iracing-Set-Up/releases/latest");
    private static readonly Uri LatestReleasePageUri = new("https://github.com/Benaudo-oss/Iracing-Set-Up/releases/latest");
    private static string CurrentUserAgent =>
        $"IracingSetupManager/{Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0)}";

    public async Task<UpdateAvailability> CheckAsync(Version installedVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
        request.Headers.UserAgent.ParseAdd(CurrentUserAgent);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new(false, installedVersion, null, null, null, null, null);
        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests)
            return await CheckFromPublicReleasePageAsync(installedVersion, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var available)) throw new InvalidDataException("La version publiée sur GitHub est invalide.");
        var expectedName = $"IracingSetupManager-{available}-win-x64-setup.exe";
        Uri? installer = null; Uri? checksum = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString();
            if (name == expectedName) installer = ValidateDownloadUri(url);
            if (name == expectedName + ".sha256") checksum = ValidateDownloadUri(url);
        }
        var isNewer = available > installedVersion;
        if (isNewer && (installer is null || checksum is null))
            throw new InvalidDataException("La release ne contient pas l'installateur et son empreinte SHA-256.");
        return new(isNewer, installedVersion, available, installer, checksum, expectedName,
            root.TryGetProperty("body", out var body) ? body.GetString() : null);
    }

    private async Task<UpdateAvailability> CheckFromPublicReleasePageAsync(
        Version installedVersion,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleasePageUri);
        request.Headers.UserAgent.ParseAdd(CurrentUserAgent);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri;
        var tag = finalUri?.Segments.LastOrDefault()?.Trim('/').TrimStart('v', 'V');
        if (finalUri is null || !finalUri.AbsolutePath.Contains("/releases/tag/", StringComparison.OrdinalIgnoreCase) ||
            !Version.TryParse(tag, out var available))
            throw new InvalidDataException("La dernière version GitHub n'a pas pu être déterminée.");

        var expectedName = $"IracingSetupManager-{available}-win-x64-setup.exe";
        var isNewer = available > installedVersion;
        if (!isNewer)
            return new(false, installedVersion, available, null, null, expectedName, null);

        var releaseRoot = $"https://github.com/Benaudo-oss/Iracing-Set-Up/releases/download/v{available}/";
        return new(
            true,
            installedVersion,
            available,
            new Uri(releaseRoot + expectedName),
            new Uri(releaseRoot + expectedName + ".sha256"),
            expectedName,
            "Mise à jour détectée via la page publique GitHub Releases.");
    }

    public async Task<DownloadedUpdate> DownloadAndVerifyAsync(UpdateAvailability update, CancellationToken cancellationToken = default)
    {
        if (!update.IsAvailable || update.AvailableVersion is null || update.DownloadUri is null || update.Sha256Uri is null || update.AssetName is null)
            throw new InvalidOperationException("Aucune mise à jour téléchargeable.");
        Directory.CreateDirectory(updateRoot);
        var finalPath = Path.Combine(updateRoot, update.AssetName);
        var temporaryPath = finalPath + ".download";
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        try
        {
            var expected = await ReadExpectedHashAsync(update.Sha256Uri, cancellationToken);
            using var response = await httpClient.GetAsync(update.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await input.CopyToAsync(output, cancellationToken);
            string actual;
            await using (var installer = File.OpenRead(temporaryPath))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(installer, cancellationToken)).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expected)))
                throw new InvalidDataException("L'empreinte SHA-256 de la mise à jour ne correspond pas.");
            File.Move(temporaryPath, finalPath, true);
            return new(update.AvailableVersion, finalPath, actual);
        }
        catch { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); throw; }
    }

    private async Task<string> ReadExpectedHashAsync(Uri uri, CancellationToken cancellationToken)
    {
        var text = await httpClient.GetStringAsync(uri, cancellationToken);
        var match = Regex.Match(text, "(?i)\\b[a-f0-9]{64}\\b");
        if (!match.Success) throw new InvalidDataException("Le fichier SHA-256 publié est invalide.");
        return match.Value.ToLowerInvariant();
    }

    private static Uri? ValidateDownloadUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("L'adresse de téléchargement GitHub est invalide.");
        return uri;
    }
}
