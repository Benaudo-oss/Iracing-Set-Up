using IracingSetupManager.Providers.Common;
using IracingSetupManager.Providers.Contracts;

namespace IracingSetupManager.Providers.Vrs;

public sealed class VrsSetupProvider(IAuthorizedProviderClient client) : AuthorizedSetupProvider(client)
{
    public override ProviderId Id => ProviderId.Vrs;
}
