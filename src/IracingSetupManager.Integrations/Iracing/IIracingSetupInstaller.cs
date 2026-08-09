using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Integrations.Iracing;

public interface IIracingSetupInstaller
{
    Task CopyValidatedSetupsAsync(
        IReadOnlyCollection<SetupFile> setups,
        string iracingSetupsPath,
        CancellationToken cancellationToken = default);
}

