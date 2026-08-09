using IracingSetupManager.Providers.Common;
using IracingSetupManager.Providers.Contracts;

namespace IracingSetupManager.Providers.GoSetups;

public sealed class GoSetupProvider(IAuthorizedProviderClient client) : AuthorizedSetupProvider(client)
{
    public override ProviderId Id => ProviderId.GoSetups;
}

