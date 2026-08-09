using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class SynchronizationPage : Page
{
    public SynchronizationPage() => InitializeComponent();

    private async void OnScanNow(object sender, RoutedEventArgs e)
    {
        await App.Services.Monitoring.ImportNowAsync();
        ActionInfo.Message = "L’analyse des dossiers configurés est en cours.";
        ActionInfo.IsOpen = true;
    }

    private async void OnStartMonitoring(object sender, RoutedEventArgs e)
    {
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
}
