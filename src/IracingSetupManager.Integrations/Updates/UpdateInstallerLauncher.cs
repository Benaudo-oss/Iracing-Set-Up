using System.Diagnostics;

namespace IracingSetupManager.Integrations.Updates;

public sealed class UpdateInstallerLauncher(string installerCache)
{
    public const string AutomaticUpdateArguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /RELAUNCHAPP=1";

    public void Launch(string installerPath)
    {
        var fullPath = Path.GetFullPath(installerPath);
        if (!File.Exists(fullPath) || !fullPath.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("L'installateur de mise à jour est introuvable.", fullPath);
        Process.Start(new ProcessStartInfo(fullPath, AutomaticUpdateArguments)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(fullPath)!
        });
    }

    public string? FindPreviousInstaller(Version installedVersion) =>
        !Directory.Exists(installerCache) ? null : Directory.EnumerateFiles(installerCache, "IracingSetupManager-*-win-x64-setup.exe")
            .Select(path => (Path: path, Version: ParseVersion(path)))
            .Where(item => item.Version is not null && item.Version < installedVersion)
            .OrderByDescending(item => item.Version)
            .Select(item => item.Path)
            .FirstOrDefault();

    private static Version? ParseVersion(string path)
    {
        var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(path), @"-(\d+\.\d+\.\d+(?:\.\d+)?)-win-x64-setup\.exe$");
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
    }
}
