namespace IracingSetupManager.Infrastructure.Settings;

public interface IArchiveFolderPicker
{
    Task<string?> PickArchiveFolderAsync(CancellationToken cancellationToken = default);
}

