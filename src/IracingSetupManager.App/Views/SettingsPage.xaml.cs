using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ArchivePathBox.Text = await App.Services.ArchivePaths.GetAsync() ?? string.Empty;
        var folders = await App.Services.MonitoredFolders.GetAsync();
        DownloadsPathBox.Text = folders.FirstOrDefault(item => item.Kind == ImportFolderKind.Downloads)?.Path
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "HYMO"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "GO Setups"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "Grid & Go"));
    }

    private async void OnChooseArchive(object sender, RoutedEventArgs e)
    {
        var path = await WinUiFolderPicker.PickAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ArchivePathBox.Text = await App.Services.ArchivePaths.ChangeAsync(path);
        }
    }

    private async void OnChooseProviderFolder(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string provider })
        {
            return;
        }

        var path = await WinUiFolderPicker.PickAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            App.Services.FolderPolicy.Validate(new MonitoredFolder(
                path,
                ImportFolderKind.OfficialProviderApplication,
                provider));
            SetProviderFolder(new MonitoredFolder(path, ImportFolderKind.OfficialProviderApplication, provider));
        }
        catch (InvalidOperationException exception)
        {
            SettingsInfo.Severity = InfoBarSeverity.Error;
            SettingsInfo.Message = exception.Message;
            SettingsInfo.IsOpen = true;
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        await App.Services.Monitoring.StopAsync();
        var folders = new List<MonitoredFolder>();
        if (DownloadsToggle.IsOn)
        {
            folders.Add(new MonitoredFolder(DownloadsPathBox.Text, ImportFolderKind.Downloads));
        }

        AddProviderIfEnabled(folders, HymoToggle, HymoPathBox, "HYMO");
        AddProviderIfEnabled(folders, GoToggle, GoPathBox, "GO Setups");
        AddProviderIfEnabled(folders, GngToggle, GngPathBox, "Grid & Go");
        await App.Services.MonitoredFolders.SaveAsync(folders);
        SettingsInfo.Severity = InfoBarSeverity.Success;
        SettingsInfo.Message = "Les paramètres ont été enregistrés.";
        SettingsInfo.IsOpen = true;
    }

    private void SetProviderFolder(MonitoredFolder? folder)
    {
        if (folder is null)
        {
            return;
        }

        var (textBox, toggle) = folder.Provider switch
        {
            "HYMO" => (HymoPathBox, HymoToggle),
            "GO Setups" => (GoPathBox, GoToggle),
            _ => (GngPathBox, GngToggle)
        };
        textBox.Text = folder.Path;
        toggle.IsOn = true;
    }

    private static void AddProviderIfEnabled(
        ICollection<MonitoredFolder> folders,
        ToggleSwitch toggle,
        TextBox textBox,
        string provider)
    {
        if (toggle.IsOn && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            folders.Add(new MonitoredFolder(
                textBox.Text,
                ImportFolderKind.OfficialProviderApplication,
                provider));
        }
    }
}
