using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files;
using IracingSetupManager.Infrastructure.Files.Import;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using IracingSetupManager.Infrastructure.Settings;
using IracingSetupManager.Infrastructure.Iracing;
using IracingSetupManager.Infrastructure.Security;

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
        Backups = new DatabaseBackupService(ContextFactory);
        SensitiveData = new SensitiveDataRetentionService(ContextFactory);
        Secrets = new WindowsCredentialManagerSecretStore();
        QueryService = new SetupQueryService(ContextFactory);
        Validation = new SetupValidationService(ContextFactory);
        IracingCopy = new IracingCopyService(ContextFactory);
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
    public DatabaseBackupService Backups { get; }
    public SensitiveDataRetentionService SensitiveData { get; }
    public ISecretStore Secrets { get; }
    public SetupQueryService QueryService { get; }
    public SetupValidationService Validation { get; }
    public IracingCopyService IracingCopy { get; }
    public ArchivePathService ArchivePaths { get; }
    public MonitoredFolderPolicy FolderPolicy { get; }
    public MonitoredFolderSettingsService MonitoredFolders { get; }
    public ImportMonitoringService Monitoring { get; }
}
