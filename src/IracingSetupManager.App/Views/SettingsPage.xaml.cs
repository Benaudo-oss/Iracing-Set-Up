using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Iracing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly List<PathElementRow> pathLayout = [];

    public SettingsPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ArchivePathBox.Text = await App.Services.ArchivePaths.GetAsync() ?? string.Empty;
        AutomaticMonitoringToggle.IsOn = await App.Services.AutomaticMonitoring.IsEnabledAsync();
        var folders = await App.Services.MonitoredFolders.GetAsync();
        DownloadsPathBox.Text = folders.FirstOrDefault(item => item.Kind == ImportFolderKind.Downloads)?.Path
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "HYMO"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "GO Setups"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "Grid & Go"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "VRS"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "SRS"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "P1Doks"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "Coach Dave Academy (CDA)"));
        SetPathLayout(await App.Services.IracingPathLayout.GetAsync());
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
        AddProviderIfEnabled(folders, VrsToggle, VrsPathBox, "VRS");
        AddProviderIfEnabled(folders, SrsToggle, SrsPathBox, "SRS");
        AddProviderIfEnabled(folders, P1DoksToggle, P1DoksPathBox, "P1Doks");
        AddProviderIfEnabled(folders, CdaToggle, CdaPathBox, "Coach Dave Academy (CDA)");
        await App.Services.MonitoredFolders.SaveAsync(folders);
        await App.Services.AutomaticMonitoring.SaveAsync(AutomaticMonitoringToggle.IsOn);
        await App.Services.IracingPathLayout.SaveAsync(pathLayout.Select(item => item.Key).ToArray());
        if (AutomaticMonitoringToggle.IsOn)
        {
            await App.Services.Monitoring.StartAsync();
        }
        SettingsInfo.Severity = InfoBarSeverity.Success;
        SettingsInfo.Message = "Les paramètres ont été enregistrés.";
        SettingsInfo.IsOpen = true;
    }

    private void OnMovePathUp(object sender, RoutedEventArgs e) => MovePathElement(sender, -1);

    private void OnMovePathDown(object sender, RoutedEventArgs e) => MovePathElement(sender, 1);

    private void OnResetPathLayout(object sender, RoutedEventArgs e) => SetPathLayout(IracingPathLayoutService.DefaultLayout);

    private void MovePathElement(object sender, int offset)
    {
        if (sender is not Button { Tag: string key }) return;
        var index = pathLayout.FindIndex(item => item.Key == key);
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= pathLayout.Count) return;
        (pathLayout[index], pathLayout[destination]) = (pathLayout[destination], pathLayout[index]);
        RefreshPathLayout();
        PathLayoutList.SelectedIndex = destination;
    }

    private void SetPathLayout(IEnumerable<string> layout)
    {
        pathLayout.Clear();
        pathLayout.AddRange(layout.Select(key => new PathElementRow(key, key switch
        {
            "Season" => "Saison",
            "Track" => "Circuit",
            "Provider" => "Fournisseur",
            "Week" => "Week",
            _ => key
        })));
        RefreshPathLayout();
    }

    private void RefreshPathLayout()
    {
        PathLayoutList.ItemsSource = null;
        PathLayoutList.ItemsSource = pathLayout;
        var examples = pathLayout.Select(item => item.Key switch
        {
            "Season" => "2026_S3",
            "Track" => "Le Mans",
            "Provider" => "HYMO",
            "Week" => "Week 07",
            _ => item.Label
        });
        PathLayoutPreview.Text = $@"…\setups\acuraarx06gtp\Garage 61\{string.Join("\\", examples)}\setup.sto";
    }

    private async void OnBackupDatabase(object sender, RoutedEventArgs e)
    {
        var folder = await WinUiFolderPicker.PickAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;
        try
        {
            var name = $"IracingSetupManager-{DateTime.Now:yyyyMMdd-HHmmss}.db";
            var path = await App.Services.Backups.BackupAsync(Path.Combine(folder, name));
            SettingsInfo.Severity = InfoBarSeverity.Success;
            SettingsInfo.Message = $"Sauvegarde créée : {path}";
        }
        catch (Exception exception)
        {
            SettingsInfo.Severity = InfoBarSeverity.Error;
            SettingsInfo.Message = exception.Message;
        }
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
            "Grid & Go" => (GngPathBox, GngToggle),
            "VRS" => (VrsPathBox, VrsToggle),
            "SRS" => (SrsPathBox, SrsToggle),
            "P1Doks" => (P1DoksPathBox, P1DoksToggle),
            "Coach Dave Academy (CDA)" => (CdaPathBox, CdaToggle),
            _ => throw new InvalidOperationException("Fournisseur inconnu.")
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

    private sealed record PathElementRow(string Key, string Label);
}
