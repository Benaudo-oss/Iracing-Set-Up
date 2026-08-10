using IracingSetupManager.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using Windows.Graphics;

namespace IracingSetupManager.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppLogo.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

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
            "iracing-copy" => typeof(IracingCopyPage),
            "review" => typeof(ReviewPage),
            "history" => typeof(HistoryPage),
            "updates" => typeof(UpdatesPage),
            "about" => typeof(AboutPage),
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
