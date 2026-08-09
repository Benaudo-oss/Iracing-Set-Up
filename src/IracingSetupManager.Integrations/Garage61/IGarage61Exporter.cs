using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Integrations.Garage61;

public interface IGarage61Exporter
{
    Task ExportValidatedSetupsAsync(
        IReadOnlyCollection<SetupFile> setups,
        CancellationToken cancellationToken = default);
}

