using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.Infrastructure.Settings;
using IracingSetupManager.App.Services;

namespace IracingSetupManager.App.Views;

public sealed partial class SynchronizationPage : Page
{
    private static SynchronizationSelection? sessionSelection;

    public SynchronizationPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadSelectionAsync, "Impossible de charger la sélection", ActionInfo);

    private async Task LoadSelectionAsync()
    {
        var selection = sessionSelection ?? await App.Services.SynchronizationSelection.GetAsync();
        ApplySelection(selection);
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(SaveSelectionAsync, "Impossible d’enregistrer la sélection");

    private async Task SaveSelectionAsync()
    {
        sessionSelection = ReadSelection();
        await App.Services.SynchronizationSelection.SaveAsync(sessionSelection);
    }

    private async void OnScanNow(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(ScanNowAsync, "L’analyse des dossiers a échoué", ActionInfo);

    private async Task ScanNowAsync()
    {
        sessionSelection = ReadSelection();
        await App.Services.SynchronizationSelection.SaveAsync(sessionSelection);
        await App.Services.Monitoring.ImportNowAsync();
        ActionInfo.Severity = InfoBarSeverity.Success;
        ActionInfo.Message = "L’analyse des dossiers configurés est terminée.";
        ActionInfo.IsOpen = true;
    }

    private async void OnStartMonitoring(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(StartMonitoringAsync, "Le démarrage de la surveillance a échoué", ActionInfo);

    private async Task StartMonitoringAsync()
    {
        sessionSelection = ReadSelection();
        await App.Services.SynchronizationSelection.SaveAsync(sessionSelection);
        var archive = await App.Services.ArchivePaths.GetAsync();
        if (string.IsNullOrWhiteSpace(archive))
        {
            ActionInfo.Severity = InfoBarSeverity.Warning;
            ActionInfo.Message = "Choisissez d’abord le dossier d’archive dans les paramètres.";
            ActionInfo.IsOpen = true;
            return;
        }

        await App.Services.Monitoring.StartAsync();
        ActionInfo.Severity = InfoBarSeverity.Success;
        ActionInfo.Message = "La surveillance locale est activée.";
        ActionInfo.IsOpen = true;
    }

    private SynchronizationSelection ReadSelection() => new(
        SelectedValues(
            (HymoProvider, "HYMO"), (GoProvider, "GO Setups"), (GngProvider, "Grid & Go"),
            (VrsProvider, "VRS"), (SrsProvider, "SRS"), (P1DoksProvider, "P1Doks"),
            (CdaProvider, "Coach Dave Academy (CDA)")),
        SelectedValues(
            (Gt3Category, "GT3"), (Gt4Category, "GT4"), (GteCategory, "GTE"),
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
}
