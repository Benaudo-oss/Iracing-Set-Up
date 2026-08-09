using IracingSetupManager.App.Services;
using Microsoft.UI.Xaml;

namespace IracingSetupManager.App;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }

    public static AppServices Services { get; } = new();

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await Services.Database.InitializeAsync();
        await Services.SensitiveData.PurgeUnneededSourcePathsAsync();
        MainWindowInstance = new MainWindow();
        MainWindowInstance.Closed += async (_, _) => await Services.Monitoring.DisposeAsync();
        MainWindowInstance.Activate();
    }
}
