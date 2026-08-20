using IracingSetupManager.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Files.Import;

public sealed class ArchiveReorganizationService(
    ISetupDbContextFactory contextFactory,
    ArchivePathBuilder pathBuilder,
    Sha256Calculator sha256Calculator)
{
    public async Task<int> ReorganizeAsync(
        string archiveRoot,
        CancellationToken cancellationToken = default)
    {
        var root = SecurePath.GetFullPath(archiveRoot);
        await using var context = contextFactory.Create();
        var setups = await context.Setups.ToListAsync(cancellationToken);
        var moved = 0;

        foreach (var setup in setups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(setup.ArchivePath)) continue;

            var source = SecurePath.EnsureChildOf(setup.ArchivePath, root);
            var metadata = new SetupMetadata(
                setup.Provider,
                setup.Category,
                setup.Car,
                setup.Track,
                setup.TrackConfiguration,
                setup.Season,
                setup.SetupType,
                setup.Week,
                setup.WeekKind);
            var destinationDirectory = pathBuilder.BuildDirectory(root, metadata);
            var destination = SecurePath.EnsureChildOf(
                Path.Combine(destinationDirectory, setup.OriginalFileName),
                root);

            if (source.Equals(destination, StringComparison.OrdinalIgnoreCase)) continue;
            destination = await ResolveDestinationAsync(source, destination, root, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (File.Exists(destination))
            {
                File.Delete(source);
            }
            else
            {
                File.Move(source, destination);
            }

            setup.ArchivePath = destination;
            moved++;
        }

        if (moved > 0) await context.SaveChangesAsync(cancellationToken);
        return moved;
    }

    private async Task<string> ResolveDestinationAsync(
        string source,
        string destination,
        string archiveRoot,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(destination)) return destination;

        var sourceHash = await sha256Calculator.CalculateAsync(source, cancellationToken);
        var destinationHash = await sha256Calculator.CalculateAsync(destination, cancellationToken);
        if (sourceHash.Equals(destinationHash, StringComparison.OrdinalIgnoreCase)) return destination;

        var conflict = Path.Combine(
            Path.GetDirectoryName(destination)!,
            "Conflits",
            sourceHash[..12],
            Path.GetFileName(destination));
        conflict = SecurePath.EnsureChildOf(conflict, archiveRoot);
        if (!File.Exists(conflict)) return conflict;

        var conflictHash = await sha256Calculator.CalculateAsync(conflict, cancellationToken);
        if (sourceHash.Equals(conflictHash, StringComparison.OrdinalIgnoreCase)) return conflict;
        throw new IOException("Le déplacement de l’archive créerait un conflit impossible à résoudre sans écrasement.");
    }
}
