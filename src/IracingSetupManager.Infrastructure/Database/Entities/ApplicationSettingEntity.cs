namespace IracingSetupManager.Infrastructure.Database.Entities;

public sealed class ApplicationSettingEntity
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

