using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Logging;
using Microsoft.UI.Xaml;
using System.Reflection;

namespace IracingSetupManager.App;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }

    public static AppServices Services { get; } = new();

    public App()
    {
        UnhandledException += OnUnhandledException;
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await Services.Database.InitializeAsync();
            Services.StartupUpdateVerification = await Services.UpdatePreferences.VerifyInstallationAfterRestartAsync(
                Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0));
            await Services.RecognitionAliases.LoadAsync();
            await Services.TrackCatalog.SynchronizeAsync();
            await Services.MetadataRefresh.RefreshAsync();
            await Services.SensitiveData.PurgeUnneededSourcePathsAsync();
            MainWindowInstance = new MainWindow();
            MainWindowInstance.Closed += async (_, _) => await Services.Monitoring.DisposeAsync();
            MainWindowInstance.Activate();
            if (await Services.AutomaticMonitoring.IsEnabledAsync())
            {
                await Services.Monitoring.StartAsync();
            }
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            throw;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args) =>
        WriteStartupError(args.Exception);

    private static void WriteStartupError(Exception exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IracingSetupManager", "Logs");
            Directory.CreateDirectory(folder);
            var safeDetails = SensitiveDataRedactor.Redact(exception.ToString());
            File.AppendAllText(Path.Combine(folder, "startup-errors.log"), $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{safeDetails}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }
    }
}
