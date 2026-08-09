using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Integrations.Garage61;

public sealed class Garage61Exporter(IGarage61Client client, Garage61ExportPolicy policy) : IGarage61Exporter
{
    public async Task ExportValidatedSetupsAsync(IReadOnlyCollection<SetupFile> setups, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setups);
        foreach (var setup in setups)
        {
            if (!policy.CanExport(setup, out var reason)) throw new InvalidOperationException(reason);
        }

        foreach (var setup in setups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.UploadAsync(setup, cancellationToken);
        }
    }
}
