using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.Infrastructure.Settings;
using IracingSetupManager.App.Services;

namespace IracingSetupManager.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadDashboardAsync, "Impossible d’actualiser le tableau de bord", DashboardInfo);

    private async Task LoadDashboardAsync()
    {
        await App.Services.LibraryIntegrity.MarkMissingFilesAsync();
        var statistics = await App.Services.QueryService.GetDashboardStatisticsAsync();
        TotalText.Text = statistics.Total.ToString();
        ReviewText.Text = statistics.ToReview.ToString();
        ValidatedText.Text = statistics.Validated.ToString();
        IracingTeamText.Text = statistics.CopiedToIracingTeam.ToString();
        ProvidersText.Text = $"{statistics.ProviderCount} / {SynchronizationSelectionSettingsService.Default.Providers.Count}";
        LastSyncText.Text = statistics.LastDownloadUtc?.ToLocalTime().ToString("g") ?? "Jamais";

        var recentActivity = (await App.Services.QueryService.GetHistoryAsync()).Take(10).ToList();
        RecentActivityList.ItemsSource = recentActivity;
        RecentActivityList.Visibility = recentActivity.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoRecentActivityText.Visibility = recentActivity.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
