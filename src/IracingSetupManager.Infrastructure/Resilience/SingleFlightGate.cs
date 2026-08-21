namespace IracingSetupManager.Infrastructure.Resilience;

public sealed class SingleFlightGate
{
    private int active;

    public bool TryEnter() => Interlocked.CompareExchange(ref active, 1, 0) == 0;

    public void Exit() => Volatile.Write(ref active, 0);
}
