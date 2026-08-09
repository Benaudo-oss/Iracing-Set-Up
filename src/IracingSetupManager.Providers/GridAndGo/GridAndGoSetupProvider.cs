using IracingSetupManager.Providers.Common;
using IracingSetupManager.Providers.Contracts;

namespace IracingSetupManager.Providers.GridAndGo;

public sealed class GridAndGoSetupProvider(IAuthorizedProviderClient client) : AuthorizedSetupProvider(client)
{
    public override ProviderId Id => ProviderId.GridAndGo;
}

