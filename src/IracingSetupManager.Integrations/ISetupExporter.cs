using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Integrations;

public interface ISetupExporter
{
    Task ExportAsync(IReadOnlyCollection<SetupFile> setups, CancellationToken cancellationToken = default);
}

