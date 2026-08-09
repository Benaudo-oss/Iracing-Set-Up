using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class ReviewPage : Page
{
    public ReviewPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var setups = await App.Services.QueryService.GetToReviewAsync();
        ReviewList.ItemsSource = setups;
        EmptyState.Visibility = setups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnValidateSelection(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Confirmer la validation groupée",
            Content = $"{ReviewList.SelectedItems.Count} setup(s) seront validés. Voulez-vous continuer ?",
            PrimaryButtonText = "Valider",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
    }
}
