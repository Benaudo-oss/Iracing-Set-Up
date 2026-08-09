using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class UpdatesPage : Page
{
    public UpdatesPage() => InitializeComponent();

    private void OnCheckUpdates(object sender, RoutedEventArgs e) => UpdateInfo.IsOpen = true;
}

