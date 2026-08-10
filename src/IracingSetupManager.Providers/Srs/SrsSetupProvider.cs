using IracingSetupManager.Providers.Common;
using IracingSetupManager.Providers.Contracts;

namespace IracingSetupManager.Providers.Srs;

public sealed class SrsSetupProvider(IAuthorizedProviderClient client) : AuthorizedSetupProvider(client)
{
    public override ProviderId Id => ProviderId.Srs;
}
