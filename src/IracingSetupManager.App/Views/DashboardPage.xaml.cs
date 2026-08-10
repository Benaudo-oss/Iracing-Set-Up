using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        ProvidersText.Text = $"{statistics.ProviderCount} / 5";
        LastSyncText.Text = statistics.LastDownloadUtc?.ToLocalTime().ToString("g") ?? "Jamais";
    }
}
