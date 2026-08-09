using IracingSetupManager.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace IracingSetupManager.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1440, 900));
        ContentFrame.Navigate(typeof(DashboardPage));
        MainNavigation.SelectedItem = MainNavigation.MenuItems[0];
    }

    private void OnNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        Navigate(tag switch
        {
            "library" => typeof(LibraryPage),
            "synchronization" => typeof(SynchronizationPage),
            "review" => typeof(ReviewPage),
            "history" => typeof(HistoryPage),
            "updates" => typeof(UpdatesPage),
            _ => typeof(DashboardPage)
        });
    }

    private void Navigate(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}

