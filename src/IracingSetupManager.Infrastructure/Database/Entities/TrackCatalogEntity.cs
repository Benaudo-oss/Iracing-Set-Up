namespace IracingSetupManager.Infrastructure.Database.Entities;

public sealed class TrackCatalogEntity
{
    public required string IracingFolderName { get; set; }
    public required string TrackName { get; set; }
    public string? Configuration { get; set; }
    public required string NormalizedAlias { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
}
