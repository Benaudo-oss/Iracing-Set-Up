using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Settings;
using IracingSetupManager.Infrastructure.Iracing;
using IracingSetupManager.Infrastructure.Security;
using IracingSetupManager.Integrations.Updates;

namespace IracingSetupManager.App.Services;

public sealed class AppServices
{
    public AppServices()
    {
        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IracingSetupManager");
        var installerCache = Path.Combine(dataRoot, "Updates", "Installers");
        ContextFactory = new LocalSetupDbContextFactory(Path.Combine(dataRoot, "setups.db"));
        Database = new SetupDatabase(ContextFactory);
        Backups = new DatabaseBackupService(ContextFactory);
        SensitiveData = new SensitiveDataRetentionService(ContextFactory);
        Secrets = new WindowsCredentialManagerSecretStore();
        QueryService = new SetupQueryService(ContextFactory);
        TrackCatalog = new TrackCatalogService(ContextFactory);
        var metadataAnalyzer = new SetupMetadataAnalyzer(TrackCatalog);
        MetadataRefresh = new SetupMetadataRefreshService(ContextFactory, metadataAnalyzer);
        LibraryIntegrity = new SetupLibraryIntegrityService(ContextFactory);
        ArchiveReorganization = new ArchiveReorganizationService(ContextFactory, new ArchivePathBuilder(), new Sha256Calculator());
        Validation = new SetupValidationService(ContextFactory);
        IracingPathLayout = new IracingPathLayoutService(ContextFactory);
        IracingCopy = new IracingCopyService(ContextFactory, IracingPathLayout);
        ArchivePaths = new ArchivePathService(ContextFactory, new WinUiFolderPicker());
        FolderPolicy = new MonitoredFolderPolicy();
        MonitoredFolders = new MonitoredFolderSettingsService(ContextFactory, FolderPolicy);
        AutomaticMonitoring = new AutomaticMonitoringSettingsService(ContextFactory);
        UpdatePreferences = new UpdatePreferenceService(ContextFactory);
        SynchronizationSelection = new SynchronizationSelectionSettingsService(ContextFactory);
        IracingTeam = new IracingTeamSettingsService(ContextFactory);
        Updates = new GitHubReleaseUpdateService(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, installerCache);
        UpdateInstaller = new UpdateInstallerLauncher(installerCache);

        var sha256 = new Sha256Calculator();
        var importer = new LibraryImportService(
            new SetupRepository(ContextFactory),
            sha256,
            new ArchiveFileManager(sha256),
            metadataAnalyzer,
            new ArchivePathBuilder(),
            new SecureZipExtractor(),
            new SecureRarExtractor());
        Monitoring = new ImportMonitoringService(
            new ImportFolderMonitor(FolderPolicy),
            MonitoredFolders,
            new StableFileAwaiter(),
            importer,
            ArchivePaths.GetAsync,
            SynchronizationSelection);
    }

    public LocalSetupDbContextFactory ContextFactory { get; }
    public SetupDatabase Database { get; }
    public DatabaseBackupService Backups { get; }
    public SensitiveDataRetentionService SensitiveData { get; }
    public ISecretStore Secrets { get; }
    public SetupQueryService QueryService { get; }
    public TrackCatalogService TrackCatalog { get; }
    public SetupMetadataRefreshService MetadataRefresh { get; }
    public SetupLibraryIntegrityService LibraryIntegrity { get; }
    public ArchiveReorganizationService ArchiveReorganization { get; }
    public SetupValidationService Validation { get; }
    public IracingCopyService IracingCopy { get; }
    public IracingPathLayoutService IracingPathLayout { get; }
    public ArchivePathService ArchivePaths { get; }
    public MonitoredFolderPolicy FolderPolicy { get; }
    public MonitoredFolderSettingsService MonitoredFolders { get; }
    public AutomaticMonitoringSettingsService AutomaticMonitoring { get; }
    public ImportMonitoringService Monitoring { get; }
    public UpdatePreferenceService UpdatePreferences { get; }
    public SynchronizationSelectionSettingsService SynchronizationSelection { get; }
    public IracingTeamSettingsService IracingTeam { get; }
    public IUpdateService Updates { get; }
    public UpdateInstallerLauncher UpdateInstaller { get; }
}
