using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.Infrastructure.Settings;

namespace IracingSetupManager.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await App.Services.LibraryIntegrity.MarkMissingFilesAsync();
        var statistics = await App.Services.QueryService.GetDashboardStatisticsAsync();
        TotalText.Text = statistics.Total.ToString();
        ReviewText.Text = statistics.ToReview.ToString();
        ValidatedText.Text = statistics.Validated.ToString();
        Garage61Text.Text = statistics.SentToGarage61.ToString();
        IracingTeamText.Text = statistics.CopiedToIracingTeam.ToString();
        ProvidersText.Text = $"{statistics.ProviderCount} / {SynchronizationSelectionSettingsService.Default.Providers.Count}";
        LastSyncText.Text = statistics.LastDownloadUtc?.ToLocalTime().ToString("g") ?? "Jamais";

        var recentActivity = (await App.Services.QueryService.GetHistoryAsync()).Take(10).ToList();
        RecentActivityList.ItemsSource = recentActivity;
        RecentActivityList.Visibility = recentActivity.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoRecentActivityText.Visibility = recentActivity.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
