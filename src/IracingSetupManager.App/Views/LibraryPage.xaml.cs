using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var setups = await App.Services.QueryService.GetAllAsync();
        SetupList.ItemsSource = setups;
        EmptyState.Visibility = setups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

