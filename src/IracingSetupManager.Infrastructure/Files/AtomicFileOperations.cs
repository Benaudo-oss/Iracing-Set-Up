namespace IracingSetupManager.Infrastructure.Files;

public interface IAtomicFileOperations
{
    Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    Task WriteAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken = default);
}

public sealed class AtomicFileOperations : IAtomicFileOperations
{
    public Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var source = SecurePath.GetFullPath(sourcePath);
        return WriteAsync(destinationPath, async (output, token) =>
        {
            await using var input = new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, token);
        }, cancellationToken);
    }

    public async Task WriteAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(write);
        var destination = SecurePath.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Le dossier de destination est invalide.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await write(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destination);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
