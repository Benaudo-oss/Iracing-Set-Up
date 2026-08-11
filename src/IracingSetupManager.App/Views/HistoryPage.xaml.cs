using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class HistoryPage : Page
{
    private IReadOnlyList<SetupChangeHistoryEntity> _history = [];

    public HistoryPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _history = await App.Services.QueryService.GetHistoryAsync();
        ApplySearch();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplySearch();

    private async void OnClearHistoryClick(object sender, RoutedEventArgs e)
    {
        var confirmation = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Effacer l’historique ?",
            Content = "Seul l’historique enregistré par l’application sera supprimé. Vos setups et vos fichiers seront conservés.",
            PrimaryButtonText = "Effacer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;

        await App.Services.QueryService.ClearHistoryAsync();
        _history = [];
        ApplySearch();
    }

    private void ApplySearch()
    {
        var search = HistorySearch.Text.Trim();
        var filtered = _history.Where(item =>
            string.IsNullOrWhiteSpace(search) ||
            item.OriginalFileName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
            item.ChangeType.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();
        HistoryList.ItemsSource = filtered;
        EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
