using System.Collections.ObjectModel;
using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class SynchronizationPage : Page
{
    private static SynchronizationSelection? sessionSelection;
    private readonly ObservableCollection<SynchronizationResultRow> results = [];
    private readonly Dictionary<string, int> resultIndexes = new(StringComparer.OrdinalIgnoreCase);
    private bool listening;

    public SynchronizationPage()
    {
        InitializeComponent();
        SynchronizationResultsList.ItemsSource = results;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadPageAsync, "Impossible de charger la synchronisation", ActionInfo);

    private async Task LoadPageAsync()
    {
        ApplySelection(sessionSelection ?? await App.Services.SynchronizationSelection.GetAsync());
        SynchronizationModeText.Text = await App.Services.AutomaticMonitoring.IsEnabledAsync()
            ? "Automatique : les nouveaux fichiers sont traités en arrière-plan. Vous pouvez aussi lancer une analyse manuelle."
            : "Manuelle uniquement : aucun dossier n’est surveillé en arrière-plan.";
        ReloadStoredResults();
        if (!listening)
        {
            App.Services.SynchronizationActivity.Changed += OnProgressChanged;
            listening = true;
        }
        SetRunningState(App.Services.Monitoring.IsManualScanRunning);
        if (App.Services.SynchronizationActivity.LastSummary is { } summary) ShowSummary(summary, false);
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (listening)
        {
            App.Services.SynchronizationActivity.Changed -= OnProgressChanged;
            listening = false;
        }
        await UiOperation.RunAsync(SaveSelectionAsync, "Impossible d’enregistrer la sélection");
    }

    private async Task SaveSelectionAsync()
    {
        sessionSelection = ReadSelection();
        await App.Services.SynchronizationSelection.SaveAsync(sessionSelection);
        await App.Services.Monitoring.RefreshFoldersAsync();
    }

    private async void OnScanNow(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(ScanNowAsync, "La synchronisation a échoué", ActionInfo);

    private async Task ScanNowAsync()
    {
        if (App.Services.Monitoring.IsManualScanRunning)
        {
            ShowWarning("Une synchronisation est déjà en cours.");
            return;
        }
        sessionSelection = ReadSelection();
        await App.Services.SynchronizationSelection.SaveAsync(sessionSelection);
        await App.Services.Monitoring.RefreshFoldersAsync();
        if (string.IsNullOrWhiteSpace(await App.Services.ArchivePaths.GetAsync()))
        {
            ShowWarning("Choisissez d’abord le dossier d’archive dans les paramètres.");
            return;
        }
        App.Services.SynchronizationActivity.Clear();
        results.Clear();
        resultIndexes.Clear();
        EmptyResultsText.Visibility = Visibility.Visible;
        SetRunningState(true);
        ProgressText.Text = "Recherche des fichiers…";
        ScanProgress.IsIndeterminate = true;
        try
        {
            var summary = await App.Services.Monitoring.ImportNowAsync();
            App.Services.SynchronizationActivity.SetSummary(summary);
            ShowSummary(summary, true);
        }
        finally { SetRunningState(false); }
    }

    private async void OnStopScan(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Interrompre la synchronisation ?",
            Content = "Le fichier en cours sera arrêté proprement. Les fichiers déjà importés seront conservés.",
            PrimaryButtonText = "Interrompre",
            CloseButtonText = "Continuer",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            App.Services.Monitoring.CancelImportNow();
            StopScanButton.IsEnabled = false;
            ProgressText.Text = "Arrêt en cours…";
        }
    }

    private void OnClearResults(object sender, RoutedEventArgs e)
    {
        if (App.Services.Monitoring.IsManualScanRunning) return;
        App.Services.SynchronizationActivity.Clear();
        results.Clear();
        resultIndexes.Clear();
        EmptyResultsText.Visibility = Visibility.Visible;
        ProgressText.Text = "Aucune synchronisation en cours";
        ProgressCountersText.Text = string.Empty;
        ScanProgress.Value = 0;
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SettingsPage));

    private void OnSelectAllProviders(object sender, RoutedEventArgs e) => SetProviderSelection(true);
    private void OnClearProviders(object sender, RoutedEventArgs e) => SetProviderSelection(false);
    private void OnSelectAllCategories(object sender, RoutedEventArgs e) => SetCategorySelection(true);
    private void OnClearCategories(object sender, RoutedEventArgs e) => SetCategorySelection(false);

    private void SetProviderSelection(bool selected)
    {
        foreach (var button in new[] { HymoProvider, GoProvider, GngProvider, VrsProvider, SrsProvider, P1DoksProvider, CdaProvider })
            button.IsChecked = selected;
    }

    private void SetCategorySelection(bool selected)
    {
        foreach (var button in new[] { Gt3Category, Gt4Category, GteCategory, Lmp2Category, Lmp3Category, GtpCategory, PcupCategory })
            button.IsChecked = selected;
    }

    private void OnProgressChanged(object? sender, SynchronizationProgress progress) =>
        DispatcherQueue.TryEnqueue(() => ApplyProgress(progress));

    private void ApplyProgress(SynchronizationProgress progress)
    {
        var row = SynchronizationResultRow.From(progress);
        if (resultIndexes.TryGetValue(progress.FilePath, out var index)) results[index] = row;
        else
        {
            resultIndexes[progress.FilePath] = results.Count;
            results.Add(row);
        }
        EmptyResultsText.Visibility = Visibility.Collapsed;
        ProgressText.Text = progress.Automatic ? "Synchronisation automatique" : "Synchronisation manuelle";
        if (progress.Total > 0)
        {
            ScanProgress.IsIndeterminate = false;
            ScanProgress.Maximum = progress.Total;
            ScanProgress.Value = progress.Completed;
            ProgressCountersText.Text = $"{progress.Completed} / {progress.Total}";
        }
        else
        {
            ScanProgress.IsIndeterminate = progress.State == SynchronizationFileState.Analyzing;
            ProgressCountersText.Text = progress.Automatic ? "Traitement en arrière-plan" : string.Empty;
        }
    }

    private void ReloadStoredResults()
    {
        results.Clear();
        resultIndexes.Clear();
        foreach (var progress in App.Services.SynchronizationActivity.Snapshot())
        {
            resultIndexes[progress.FilePath] = results.Count;
            results.Add(SynchronizationResultRow.From(progress));
        }
        EmptyResultsText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowSummary(SynchronizationSummary summary, bool showInfo)
    {
        ScanProgress.IsIndeterminate = false;
        ScanProgress.Maximum = Math.Max(1, summary.Detected);
        ScanProgress.Value = summary.Detected;
        ProgressText.Text = summary.Cancelled ? "Synchronisation interrompue" : "Synchronisation terminée";
        ProgressCountersText.Text = $"{summary.Detected} détecté(s) · {summary.Imported} importé(s) · {summary.Duplicates} doublon(s) · {summary.Filtered} filtré(s) · {summary.Errors} erreur(s) · {summary.Duration.TotalSeconds:N0} s";
        if (!showInfo) return;
        ActionInfo.Severity = summary.Errors > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        ActionInfo.Message = summary.Cancelled
            ? "Synchronisation interrompue. Les imports terminés ont été conservés."
            : $"Terminé : {summary.Imported} importé(s), {summary.Duplicates} doublon(s), {summary.Filtered} ignoré(s), {summary.Errors} erreur(s).";
        ActionInfo.IsOpen = true;
    }

    private void SetRunningState(bool running)
    {
        ScanNowButton.IsEnabled = !running;
        StopScanButton.IsEnabled = running;
        ClearResultsButton.IsEnabled = !running;
    }

    private void ShowWarning(string message)
    {
        ActionInfo.Severity = InfoBarSeverity.Warning;
        ActionInfo.Message = message;
        ActionInfo.IsOpen = true;
    }

    private SynchronizationSelection ReadSelection() => new(
        SelectedValues((HymoProvider, "HYMO"), (GoProvider, "GO Setups"), (GngProvider, "Grid & Go"),
            (VrsProvider, "VRS"), (SrsProvider, "SRS"), (P1DoksProvider, "P1Doks"),
            (CdaProvider, "Coach Dave Academy (CDA)")),
        SelectedValues((Gt3Category, "GT3"), (Gt4Category, "GT4"), (GteCategory, "GTE"),
            (Lmp2Category, "LMP2"), (Lmp3Category, "LMP3"), (GtpCategory, "GTP"), (PcupCategory, "PCUP")));

    private void ApplySelection(SynchronizationSelection selection)
    {
        SetChecked(selection.Providers, (HymoProvider, "HYMO"), (GoProvider, "GO Setups"),
            (GngProvider, "Grid & Go"), (VrsProvider, "VRS"), (SrsProvider, "SRS"),
            (P1DoksProvider, "P1Doks"), (CdaProvider, "Coach Dave Academy (CDA)"));
        SetChecked(selection.Categories, (Gt3Category, "GT3"), (Gt4Category, "GT4"),
            (GteCategory, "GTE"), (Lmp2Category, "LMP2"), (Lmp3Category, "LMP3"),
            (GtpCategory, "GTP"), (PcupCategory, "PCUP"));
    }

    private static IReadOnlyList<string> SelectedValues(params (CheckBox Box, string Value)[] items) =>
        items.Where(item => item.Box.IsChecked == true).Select(item => item.Value).ToList();

    private static void SetChecked(IReadOnlyList<string> selected, params (CheckBox Box, string Value)[] items)
    {
        var values = selected.ToHashSet(StringComparer.Ordinal);
        foreach (var item in items) item.Box.IsChecked = values.Contains(item.Value);
    }

    private sealed record SynchronizationResultRow(string FullPath, string FileName, string State, string Message)
    {
        public static SynchronizationResultRow From(SynchronizationProgress progress) => new(
            progress.FilePath,
            Path.GetFileName(progress.FilePath),
            progress.State switch
            {
                SynchronizationFileState.Detected => "Détecté",
                SynchronizationFileState.Analyzing => "Analyse",
                SynchronizationFileState.Imported => "Importé",
                SynchronizationFileState.Duplicate => "Doublon",
                SynchronizationFileState.Filtered => "Ignoré",
                SynchronizationFileState.Unsupported => "Ignoré",
                SynchronizationFileState.Error => "Erreur",
                _ => "Interrompu"
            },
            progress.Message);
    }
}
