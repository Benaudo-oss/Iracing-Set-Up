namespace IracingSetupManager.Infrastructure.Resilience;

public sealed record OptionalTask(
    string Name,
    Func<CancellationToken, Task> Action);

public static class OptionalTaskSequence
{
    public static async Task RunAsync(
        IEnumerable<OptionalTask> tasks,
        Action<string, Exception> reportFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(reportFailure);

        foreach (var task in tasks)
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                await task.Action(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                reportFailure(task.Name, exception);
            }
        }
    }
}
