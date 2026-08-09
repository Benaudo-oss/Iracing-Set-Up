using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Settings;

namespace IracingSetupManager.App.Services;

public sealed class AppServices
{
    public AppServices()
    {
        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IracingSetupManager");
        ContextFactory = new LocalSetupDbContextFactory(Path.Combine(dataRoot, "setups.db"));
        Database = new SetupDatabase(ContextFactory);
        QueryService = new SetupQueryService(ContextFactory);
        ArchivePaths = new ArchivePathService(ContextFactory, new WinUiFolderPicker());
        FolderPolicy = new MonitoredFolderPolicy();
        MonitoredFolders = new MonitoredFolderSettingsService(ContextFactory, FolderPolicy);

        var sha256 = new Sha256Calculator();
        var importer = new LibraryImportService(
            new SetupRepository(ContextFactory),
            sha256,
            new ArchiveFileManager(sha256),
            new SetupMetadataAnalyzer(),
            new ArchivePathBuilder(),
            new SecureZipExtractor());
        Monitoring = new ImportMonitoringService(
            new ImportFolderMonitor(FolderPolicy),
            MonitoredFolders,
            new StableFileAwaiter(),
            importer,
            ArchivePaths.GetAsync);
    }

    public LocalSetupDbContextFactory ContextFactory { get; }
    public SetupDatabase Database { get; }
    public SetupQueryService QueryService { get; }
    public ArchivePathService ArchivePaths { get; }
    public MonitoredFolderPolicy FolderPolicy { get; }
    public MonitoredFolderSettingsService MonitoredFolders { get; }
    public ImportMonitoringService Monitoring { get; }
}

