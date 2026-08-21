using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Logging;
using IracingSetupManager.Infrastructure.Resilience;
using Microsoft.UI.Xaml;
using System.Reflection;

namespace IracingSetupManager.App;

public partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _optionalStartupTask;

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
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            throw;
        }

        MainWindowInstance = new MainWindow();
        MainWindowInstance.Closed += OnMainWindowClosed;
        MainWindowInstance.Activate();
        _optionalStartupTask = Task.Run(() => RunOptionalStartupTasksAsync(_shutdown.Token), _shutdown.Token);
    }

    private static async Task RunOptionalStartupTasksAsync(CancellationToken cancellationToken)
    {
        OptionalTask[] tasks =
        [
            new("Vérification après mise à jour",
            async token => Services.StartupUpdateVerification =
                await Services.UpdatePreferences.VerifyInstallationAfterRestartAsync(
                    Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0),
                    token)),
            new("Chargement des alias de reconnaissance",
                token => Services.RecognitionAliases.LoadAsync(token)),
            new("Actualisation du catalogue des circuits",
                token => Services.TrackCatalog.SynchronizeAsync(cancellationToken: token)),
            new("Démarrage de la surveillance automatique", async token =>
            {
                if (await Services.AutomaticMonitoring.IsEnabledAsync(token))
                {
                    await Services.Monitoring.StartAsync(token);
                }
            }),
            new("Actualisation des métadonnées", token => Services.MetadataRefresh.RefreshAsync(token)),
            new("Contrôle d’intégrité de la bibliothèque",
                token => Services.LibraryIntegrity.MarkMissingFilesIfDueAsync(TimeSpan.FromMinutes(10), token)),
            new("Nettoyage des chemins sensibles", token => Services.SensitiveData.PurgeUnneededSourcePathsAsync(token))
        ];

        await OptionalTaskSequence.RunAsync(
            tasks,
            (operation, exception) => Services.ApplicationLog.Error(
                exception,
                $"Échec de la tâche de démarrage : {operation}"),
            cancellationToken);
    }

    private async void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        _shutdown.Cancel();
        try
        {
            await Services.Monitoring.DisposeAsync();
            if (_optionalStartupTask is not null) await _optionalStartupTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Services.ApplicationLog.Error(exception, "Arrêt des services d’arrière-plan");
        }
        finally
        {
            _shutdown.Dispose();
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
