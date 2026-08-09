namespace IracingSetupManager.Providers;

public interface ISetupProvider
{
    string Name { get; }

    Task SynchronizeAsync(CancellationToken cancellationToken = default);
}

