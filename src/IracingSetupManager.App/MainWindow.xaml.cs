using IracingSetupManager.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using Windows.Graphics;

namespace IracingSetupManager.App;

public sealed partial class MainWindow : Window
{
    private object? currentNavigationParameter;
    public MainWindow()
    {
        InitializeComponent();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppLogo.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        AppWindow.Resize(new SizeInt32(1440, 900));
        ContentFrame.NavigationFailed += (_, args) =>
        {
            Services.UiOperation.Report(args.Exception, $"Impossible d’ouvrir la page {args.SourcePageType.Name}");
            args.Handled = true;
        };
        ContentFrame.Navigate(typeof(DashboardPage));
        MainNavigation.SelectedItem = MainNavigation.MenuItems[0];
    }

    private void OnNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        try
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

        var pageType = tag switch
        {
            "library" => typeof(LibraryPage),
            "synchronization" => typeof(SynchronizationPage),
            "iracing-copy" => typeof(IracingCopyPage),
            "iracing-team-copy" => typeof(IracingCopyPage),
            "review" => typeof(ReviewPage),
            "history" => typeof(HistoryPage),
            "updates" => typeof(UpdatesPage),
            "about" => typeof(AboutPage),
            _ => typeof(DashboardPage)
        };
        Navigate(pageType, tag == "iracing-team-copy" ? "team" : null);
        }
        catch (Exception exception)
        {
            Services.UiOperation.Report(exception, "La navigation a échoué");
        }
    }

    private void Navigate(Type pageType, object? parameter = null)
    {
        if (ContentFrame.CurrentSourcePageType != pageType || !Equals(currentNavigationParameter, parameter))
        {
            currentNavigationParameter = parameter;
            ContentFrame.Navigate(pageType, parameter);
        }
    }
}
