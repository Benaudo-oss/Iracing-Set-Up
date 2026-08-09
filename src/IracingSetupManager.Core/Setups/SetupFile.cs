namespace IracingSetupManager.Core.Setups;

public sealed record SetupFile(
    Guid Id,
    string OriginalFileName,
    string Sha256,
    long SizeInBytes,
    string ArchivePath,
    SetupStatus Status,
    DateTimeOffset ImportedAtUtc,
    SetupSourceKind SourceKind = SetupSourceKind.Unknown,
    string? SourcePath = null,
    bool IsPrivate = false,
    bool Garage61ExportApproved = false);
