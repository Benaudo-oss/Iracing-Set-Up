using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Integrations.Garage61;

public interface IGarage61Client
{
    Task UploadAsync(SetupFile setup, CancellationToken cancellationToken = default);
}
