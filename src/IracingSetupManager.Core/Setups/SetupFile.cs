namespace IracingSetupManager.Core.Setups;

public sealed record SetupFile(
    Guid Id,
    string OriginalFileName,
    string Sha256,
    long SizeInBytes,
    string ArchivePath,
    SetupStatus Status,
    DateTimeOffset ImportedAtUtc);

