using System.Reflection;
using IracingSetupManager.Integrations.Updates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.App.Services;

namespace IracingSetupManager.App.Views;

public sealed partial class UpdatesPage : Page
{
    private readonly Version installedVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);
    private UpdateAvailability? availableUpdate;
    private DownloadedUpdate? downloadedUpdate;
    private string? rollbackInstaller;

    public UpdatesPage() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InstalledVersionText.Text = installedVersion.ToString();
        rollbackInstaller = App.Services.UpdateInstaller.FindPreviousInstaller(installedVersion);
        RollbackButton.IsEnabled = rollbackInstaller is not null;
        if (rollbackInstaller is not null) RollbackDescription.Text = $"Installateur conservé : {Path.GetFileName(rollbackInstaller)}";
    }

    private async void OnCheckUpdates(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            availableUpdate = await App.Services.Updates.CheckAsync(installedVersion);
            AvailableVersionText.Text = availableUpdate.AvailableVersion?.ToString() ?? "—";
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(availableUpdate.ReleaseNotes) ? "Aucune note de version." : availableUpdate.ReleaseNotes;
            var shouldOffer = availableUpdate.IsAvailable && availableUpdate.AvailableVersion is not null &&
                              await App.Services.UpdatePreferences.ShouldOfferAsync(availableUpdate.AvailableVersion);
            DownloadButton.IsEnabled = shouldOffer;
            DeferButton.IsEnabled = shouldOffer;
            IgnoreButton.IsEnabled = shouldOffer;
            Show(availableUpdate.IsAvailable
                ? shouldOffer ? "Une nouvelle version est disponible." : "Cette version a été reportée ou ignorée."
                : "Aucune mise à jour disponible.", InfoBarSeverity.Informational);
        }
        catch (Exception exception) { Show($"Vérification impossible : {exception.Message}", InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (availableUpdate is null) return;
        SetBusy(true);
        try
        {
            downloadedUpdate = await App.Services.Updates.DownloadAndVerifyAsync(availableUpdate);
            InstallButton.IsEnabled = true;
            DownloadButton.IsEnabled = false;
            Show("Téléchargement terminé et empreinte SHA-256 vérifiée.", InfoBarSeverity.Success);
        }
        catch (Exception exception) { Show($"Téléchargement refusé : {exception.Message}", InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (downloadedUpdate is null) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Installer la mise à jour", Content = "L’application va se fermer, installer la mise à jour puis redémarrer automatiquement.", PrimaryButtonText = "Installer", CloseButtonText = "Annuler", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!await TryCreateSafetyBackupAsync("avant-mise-a-jour")) return;
        App.Services.UpdateInstaller.Launch(downloadedUpdate.InstallerPath);
        Application.Current.Exit();
    }

    private async void OnDefer(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(DeferAsync, "Impossible de reporter la mise à jour", UpdateInfo);

    private async Task DeferAsync()
    {
        await App.Services.UpdatePreferences.DeferAsync(TimeSpan.FromDays(7));
        DisableOfferActions();
        Show("La proposition est reportée de 7 jours.", InfoBarSeverity.Informational);
    }

    private async void OnIgnore(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(IgnoreAsync, "Impossible d’ignorer la mise à jour", UpdateInfo);

    private async Task IgnoreAsync()
    {
        if (availableUpdate?.AvailableVersion is null) return;
        await App.Services.UpdatePreferences.IgnoreAsync(availableUpdate.AvailableVersion);
        DisableOfferActions();
        Show("Cette version sera ignorée. Une version ultérieure sera toujours proposée.", InfoBarSeverity.Informational);
    }

    private async void OnRollback(object sender, RoutedEventArgs e)
    {
        if (rollbackInstaller is null) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Revenir à la version précédente", Content = Path.GetFileName(rollbackInstaller), PrimaryButtonText = "Réinstaller", CloseButtonText = "Annuler", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!await TryCreateSafetyBackupAsync("avant-retour-arriere")) return;
        App.Services.UpdateInstaller.Launch(rollbackInstaller);
        Application.Current.Exit();
    }

    private void SetBusy(bool busy) => UpdateProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    private async Task<bool> TryCreateSafetyBackupAsync(string label)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IracingSetupManager", "Updates", "DatabaseBackups");
            await App.Services.Backups.BackupAsync(Path.Combine(folder, $"{label}-{DateTime.Now:yyyyMMdd-HHmmss}.db"));
            return true;
        }
        catch (Exception exception)
        {
            Show($"Installation annulée : sauvegarde impossible ({exception.Message})", InfoBarSeverity.Error);
            return false;
        }
    }
    private void DisableOfferActions() { DownloadButton.IsEnabled = false; DeferButton.IsEnabled = false; IgnoreButton.IsEnabled = false; }
    private void Show(string message, InfoBarSeverity severity) { UpdateInfo.Message = message; UpdateInfo.Severity = severity; UpdateInfo.IsOpen = true; }
}
