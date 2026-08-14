namespace IracingSetupManager.Infrastructure.Database.Entities;

public enum RecognitionAliasKind
{
    Car,
    Track
}

public sealed class RecognitionAliasEntity
{
    public long Id { get; set; }
    public RecognitionAliasKind Kind { get; set; }
    public required string Alias { get; set; }
    public required string NormalizedAlias { get; set; }
    public required string CanonicalValue { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
