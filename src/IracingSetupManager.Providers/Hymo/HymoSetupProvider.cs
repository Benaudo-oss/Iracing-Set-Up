using IracingSetupManager.Providers.Common;
using IracingSetupManager.Providers.Contracts;

namespace IracingSetupManager.Providers.Hymo;

public sealed class HymoSetupProvider(IAuthorizedProviderClient client) : AuthorizedSetupProvider(client)
{
    public override ProviderId Id => ProviderId.Hymo;
}

