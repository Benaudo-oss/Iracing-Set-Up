namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public sealed class StableFileAwaiter(
    TimeSpan? probeInterval = null,
    int requiredStableProbes = 2,
    TimeSpan? timeout = null,
    TimeSpan? alreadyStableAge = null)
{
    private readonly TimeSpan _probeInterval = probeInterval ?? TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(2);
    private readonly TimeSpan _alreadyStableAge = alreadyStableAge ?? TimeSpan.FromSeconds(5);

    public async Task<bool> WaitAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (File.Exists(path))
        {
            var existing = new FileInfo(path);
            if (DateTime.UtcNow - existing.LastWriteTimeUtc >= _alreadyStableAge && CanRead(path))
                return true;
        }

        var startedAt = DateTimeOffset.UtcNow;
        long? previousLength = null;
        DateTime? previousWriteTime = null;
        var stableProbes = 0;

        while (DateTimeOffset.UtcNow - startedAt < _timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (previousLength == info.Length && previousWriteTime == info.LastWriteTimeUtc && CanRead(path))
                {
                    stableProbes++;
                    if (stableProbes >= requiredStableProbes)
                    {
                        return true;
                    }
                }
                else
                {
                    stableProbes = 0;
                }

                previousLength = info.Length;
                previousWriteTime = info.LastWriteTimeUtc;
            }

            await Task.Delay(_probeInterval, cancellationToken);
        }

        return false;
    }

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.Length >= 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
