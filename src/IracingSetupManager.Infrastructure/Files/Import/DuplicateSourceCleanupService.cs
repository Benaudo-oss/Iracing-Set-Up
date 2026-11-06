using IracingSetupManager.Infrastructure.Database;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed record DuplicateCleanupResult(int Deleted, int Skipped);

public sealed class DuplicateSourceCleanupService(
    ISetupDbContextFactory contextFactory,
    Sha256Calculator sha256Calculator,
    SecureZipExtractor zipExtractor,
    SecureRarExtractor rarExtractor)
{
    public async Task<DuplicateCleanupResult> DeleteVerifiedAsync(
        IEnumerable<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        var deleted = 0;
        var skipped = 0;
        foreach (var sourcePath in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(sourcePath) || !await IsFullyDuplicatedAsync(sourcePath, cancellationToken))
            {
                skipped++;
                continue;
            }

            File.Delete(sourcePath);
            deleted++;
        }
        return new DuplicateCleanupResult(deleted, skipped);
    }

    private async Task<bool> IsFullyDuplicatedAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(sourcePath);
        if (extension.Equals(".sto", StringComparison.OrdinalIgnoreCase))
            return await HasRetainedCopyAsync(sourcePath, cancellationToken);

        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".rar", StringComparison.OrdinalIgnoreCase)) return false;

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "IracingSetupManager", "DuplicateCheck", Guid.NewGuid().ToString("N"));
        try
        {
            var extracted = extension.Equals(".rar", StringComparison.OrdinalIgnoreCase)
                ? await rarExtractor.ExtractAsync(sourcePath, temporaryRoot, cancellationToken)
                : await zipExtractor.ExtractAsync(sourcePath, temporaryRoot, cancellationToken);
            var setups = extracted.Where(path => Path.GetExtension(path).Equals(".sto", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (setups.Length == 0) return false;
            foreach (var setup in setups)
                if (!await HasRetainedCopyAsync(setup, cancellationToken)) return false;
            return true;
        }
        catch (InvalidDataException) { return false; }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                try { Directory.Delete(temporaryRoot, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private async Task<bool> HasRetainedCopyAsync(string candidatePath, CancellationToken cancellationToken)
    {
        var hash = await sha256Calculator.CalculateAsync(candidatePath, cancellationToken);
        await using var context = contextFactory.Create();
        var retainedPath = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            context.Setups.Where(item => item.Sha256 == hash).Select(item => item.ArchivePath), cancellationToken);
        if (string.IsNullOrWhiteSpace(retainedPath) || !File.Exists(retainedPath)) return false;
        if (Path.GetFullPath(candidatePath).Equals(Path.GetFullPath(retainedPath), StringComparison.OrdinalIgnoreCase)) return false;
        return string.Equals(hash, await sha256Calculator.CalculateAsync(retainedPath, cancellationToken), StringComparison.OrdinalIgnoreCase);
    }
}
