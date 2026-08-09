using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace IracingSetupManager.Infrastructure.Settings;

public sealed class ArchivePathService(
    ISetupDbContextFactory contextFactory,
    IArchiveFolderPicker folderPicker)
{
    private const string ArchivePathKey = "ArchivePath";

    public async Task<string?> GetOrChooseAsync(CancellationToken cancellationToken = default)
    {
        var savedPath = await GetSavedPathAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(savedPath))
        {
            return savedPath;
        }

        var selectedPath = await folderPicker.PickArchiveFolderAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        return await SaveAsync(selectedPath, cancellationToken);
    }

    public async Task<string> ChangeAsync(string path, CancellationToken cancellationToken = default) =>
        await SaveAsync(path, cancellationToken);

    private async Task<string?> GetSavedPathAsync(CancellationToken cancellationToken)
    {
        await using var context = contextFactory.Create();
        return await context.ApplicationSettings
            .Where(setting => setting.Key == ArchivePathKey)
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<string> SaveAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedPath = Path.GetFullPath(path);
        Directory.CreateDirectory(normalizedPath);

        await using var context = contextFactory.Create();
        var setting = await context.ApplicationSettings.FindAsync([ArchivePathKey], cancellationToken);
        if (setting is null)
        {
            context.ApplicationSettings.Add(new ApplicationSettingEntity
            {
                Key = ArchivePathKey,
                Value = normalizedPath,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            setting.Value = normalizedPath;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return normalizedPath;
    }
}

