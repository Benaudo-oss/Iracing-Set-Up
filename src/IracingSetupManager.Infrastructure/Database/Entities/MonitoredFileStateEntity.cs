namespace IracingSetupManager.Infrastructure.Database.Entities;

public sealed class MonitoredFileStateEntity
{
    public required string PathKey { get; set; }

    public long Length { get; set; }

    public long LastWriteTimeUtcTicks { get; set; }

    public DateTimeOffset LastExaminedAtUtc { get; set; }
}
