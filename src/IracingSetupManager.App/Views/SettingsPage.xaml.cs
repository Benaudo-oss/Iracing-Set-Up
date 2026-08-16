using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Iracing;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Core.Catalog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace IracingSetupManager.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly List<PathElementRow> pathLayout = [];
    private bool loadingSettings;

    public SettingsPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadSettingsAsync, "Impossible de charger les paramètres", SettingsInfo);

    private async Task LoadSettingsAsync()
    {
        loadingSettings = true;
        try
        {
        ArchivePathBox.Text = await App.Services.ArchivePaths.GetAsync() ?? string.Empty;
        IracingTeamNameBox.Text = await App.Services.IracingTeam.GetNameAsync() ?? string.Empty;
        AutomaticMonitoringToggle.IsOn = await App.Services.AutomaticMonitoring.IsEnabledAsync();
        var folders = await App.Services.MonitoredFolders.GetAsync();
        DownloadsPathBox.Text = folders.FirstOrDefault(item => item.Kind == ImportFolderKind.Downloads)?.Path
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        HymoPathBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "iRacing", "setups", "<voiture>", "Track Titan");
        var synchronizationSelection = await App.Services.SynchronizationSelection.GetAsync();
        var hymoActive = synchronizationSelection.Providers.Contains("HYMO", StringComparer.OrdinalIgnoreCase)
            && synchronizationSelection.Categories.Count > 0;
        HymoModeText.Text = hymoActive
            ? $"Automatique · {synchronizationSelection.Categories.Count} catégorie(s)"
            : "Inactif dans Synchronisation";
        HymoModeText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(hymoActive
            ? Windows.UI.Color.FromArgb(255, 102, 187, 106)
            : Windows.UI.Color.FromArgb(255, 170, 178, 188));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "GO Setups"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "Grid & Go"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "VRS"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "SRS"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "P1Doks"));
        SetProviderFolder(folders.FirstOrDefault(item => item.Provider == "Coach Dave Academy (CDA)"));
        SetPathLayout(await App.Services.IracingPathLayout.GetAsync());
        RefreshRecognitionAliases();
        }
        finally { loadingSettings = false; }
    }

    private async void OnAutomaticMonitoringToggled(object sender, RoutedEventArgs e)
    {
        if (loadingSettings) return;
        await UiOperation.RunAsync(SaveAutomaticMonitoringAsync, "Impossible de modifier la surveillance automatique", SettingsInfo);
    }

    private async Task SaveAutomaticMonitoringAsync()
    {
        var enabled = AutomaticMonitoringToggle.IsOn;
        await App.Services.AutomaticMonitoring.SaveAsync(enabled);
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(await App.Services.ArchivePaths.GetAsync()))
            {
                loadingSettings = true;
                AutomaticMonitoringToggle.IsOn = false;
                loadingSettings = false;
                await App.Services.AutomaticMonitoring.SaveAsync(false);
                throw new InvalidOperationException("Choisissez d’abord le dossier d’archive.");
            }
            await App.Services.Monitoring.StartAsync();
        }
        else await App.Services.Monitoring.StopAsync();
        SettingsInfo.Severity = InfoBarSeverity.Success;
        SettingsInfo.Message = enabled
            ? "La surveillance automatique est activée et mémorisée."
            : "La surveillance automatique est arrêtée et restera désactivée au prochain lancement.";
        SettingsInfo.IsOpen = true;
    }

    private void RefreshRecognitionAliases()
    {
        var rows = App.Services.RecognitionAliases.Snapshot.Select(item => new RecognitionAliasRow(
            item.Id,
            item.Kind,
            item.Alias,
            item.CanonicalValue)).ToArray();
        RecognitionAliasesList.ItemsSource = rows;
        NoRecognitionAliasesText.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnAddRecognitionAlias(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => EditRecognitionAliasAsync(null), "Impossible d’ajouter l’abréviation", SettingsInfo);

    private async void OnEditRecognitionAlias(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => EditRecognitionAliasAsync(sender), "Impossible de modifier l’abréviation", SettingsInfo);

    private async Task EditRecognitionAliasAsync(object? sender)
    {
        var existing = sender is Button { Tag: long id }
            ? App.Services.RecognitionAliases.Snapshot.FirstOrDefault(item => item.Id == id)
            : null;
        var kind = new ComboBox { Header = "Type", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Voiture", "Circuit" }, IsEnabled = existing is null };
        kind.SelectedIndex = existing?.Kind == RecognitionAliasKind.Track ? 1 : 0;
        var alias = new TextBox { Header = "Abréviation", Text = existing?.Alias ?? string.Empty, PlaceholderText = "Au moins 3 caractères", IsReadOnly = existing is not null };
        var canonical = new ComboBox { Header = "Valeur reconnue", HorizontalAlignment = HorizontalAlignment.Stretch };
        var tracks = SetupMetadataAnalyzer.KnownTrackNames.Concat((await App.Services.TrackCatalog.GetAllAsync()).Select(item => item.TrackName))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        void RefreshValues()
        {
            canonical.ItemsSource = kind.SelectedIndex == 0
                ? SetupCatalog.Cars.Select(item => item.DisplayName).Order(StringComparer.CurrentCultureIgnoreCase).ToArray()
                : tracks;
            canonical.SelectedItem = canonical.Items.Cast<string>().FirstOrDefault(item =>
                item.Equals(existing?.CanonicalValue, StringComparison.OrdinalIgnoreCase));
        }
        kind.SelectionChanged += (_, _) => RefreshValues();
        RefreshValues();
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(kind);
        panel.Children.Add(alias);
        panel.Children.Add(canonical);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = existing is null ? "Ajouter une abréviation" : "Modifier l’abréviation",
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (canonical.SelectedItem is not string value) throw new InvalidOperationException("Choisissez une valeur reconnue.");
        await App.Services.RecognitionAliases.SaveAsync(
            kind.SelectedIndex == 0 ? RecognitionAliasKind.Car : RecognitionAliasKind.Track,
            alias.Text,
            value);
        RefreshRecognitionAliases();
        SettingsInfo.Severity = InfoBarSeverity.Success;
        SettingsInfo.Message = "L’abréviation a été enregistrée.";
        SettingsInfo.IsOpen = true;
    }

    private async void OnDeleteRecognitionAlias(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => DeleteRecognitionAliasAsync(sender), "Impossible de supprimer l’abréviation", SettingsInfo);

    private async Task DeleteRecognitionAliasAsync(object sender)
    {
        if (sender is not Button { Tag: long id }) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Supprimer cette abréviation ?",
            Content = "Les fichiers déjà classés ne seront pas modifiés.",
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await App.Services.RecognitionAliases.DeleteAsync(id);
        RefreshRecognitionAliases();
        SettingsInfo.Severity = InfoBarSeverity.Success;
        SettingsInfo.Message = "L’abréviation a été supprimée.";
        SettingsInfo.IsOpen = true;
    }

    private async void OnChooseArchive(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(ChooseArchiveAsync, "Impossible de sélectionner l’archive", SettingsInfo);

    private async Task ChooseArchiveAsync()
    {
        var path = await WinUiFolderPicker.PickAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ArchivePathBox.Text = await App.Services.ArchivePaths.ChangeAsync(path);
        }
    }

    private async void OnReorganizeArchive(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(ReorganizeArchiveAsync, "Impossible de réorganiser l’archive", SettingsInfo);

    private async Task ReorganizeArchiveAsync()
    {
        var archive = await App.Services.ArchivePaths.GetAsync();
        if (string.IsNullOrWhiteSpace(archive))
        {
            SettingsInfo.Severity = InfoBarSeverity.Warning;
            SettingsInfo.Message = "Choisissez d’abord le dossier d’archive.";
            SettingsInfo.IsOpen = true;
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Réorganiser l’archive ?",
            Content = "Les fichiers seront replacés selon les métadonnées actuelles, sans écrasement.",
            PrimaryButtonText = "Réorganiser",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var moved = await App.Services.ArchiveReorganization.ReorganizeAsync(archive);
        SettingsInfo.Severity = InfoBarSeverity.Success;
        SettingsInfo.Message = $"Réorganisation terminée : {moved} fichier(s) déplacé(s).";
        SettingsInfo.IsOpen = true;
    }

    private async void OnSaveTeamName(object sender, RoutedEventArgs e) => await SaveTeamNameAsync();

    private async void OnTeamNameKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        await SaveTeamNameAsync();
    }

    private async Task SaveTeamNameAsync()
    {
        try
        {
            await App.Services.IracingTeam.SaveNameAsync(IracingTeamNameBox.Text);
            IracingTeamNameBox.Text = (await App.Services.IracingTeam.GetNameAsync()) ?? string.Empty;
            SettingsInfo.Severity = InfoBarSeverity.Success;
            SettingsInfo.Message = $"Team Garage61 enregistrée : {IracingTeamNameBox.Text}";
        }
        catch (Exception exception)
        {
            SettingsInfo.Severity = InfoBarSeverity.Error;
            SettingsInfo.Message = exception.Message;
        }
        SettingsInfo.IsOpen = true;
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

    private async void OnSave(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(SaveSettingsAsync, "Impossible d’enregistrer les paramètres", SettingsInfo);

    private async Task SaveSettingsAsync()
    {
        await App.Services.Monitoring.StopAsync();
        var folders = new List<MonitoredFolder>();
        if (DownloadsToggle.IsOn)
        {
            folders.Add(new MonitoredFolder(DownloadsPathBox.Text, ImportFolderKind.Downloads));
        }

        AddProviderIfEnabled(folders, GoToggle, GoPathBox, "GO Setups");
        AddProviderIfEnabled(folders, GngToggle, GngPathBox, "Grid & Go");
        AddProviderIfEnabled(folders, VrsToggle, VrsPathBox, "VRS");
        AddProviderIfEnabled(folders, SrsToggle, SrsPathBox, "SRS");
        AddProviderIfEnabled(folders, P1DoksToggle, P1DoksPathBox, "P1Doks");
        AddProviderIfEnabled(folders, CdaToggle, CdaPathBox, "Coach Dave Academy (CDA)");
        await App.Services.MonitoredFolders.SaveAsync(folders);
        await App.Services.AutomaticMonitoring.SaveAsync(AutomaticMonitoringToggle.IsOn);
        await App.Services.IracingPathLayout.SaveAsync(pathLayout.Select(item => item.Key).ToArray());
        if (!string.IsNullOrWhiteSpace(IracingTeamNameBox.Text))
        {
            await App.Services.IracingTeam.SaveNameAsync(IracingTeamNameBox.Text);
        }
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
    private sealed record RecognitionAliasRow(long Id, RecognitionAliasKind Kind, string Alias, string CanonicalValue)
    {
        public string KindLabel => Kind == RecognitionAliasKind.Car ? "Voiture" : "Circuit";
    }
}
