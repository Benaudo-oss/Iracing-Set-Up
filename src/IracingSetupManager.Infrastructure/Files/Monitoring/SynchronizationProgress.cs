using IracingSetupManager.Infrastructure.Files.Import;

namespace IracingSetupManager.Infrastructure.Files.Monitoring;

public enum SynchronizationFileState { Detected, Analyzing, Imported, Duplicate, Filtered, Unsupported, Error, Cancelled }

public sealed record SynchronizationProgress(
    string FilePath, SynchronizationFileState State, string Message,
    int Completed, int Total, bool Automatic, SetupImportResult? Result = null);

public sealed record SynchronizationSummary(
    int Detected, int Imported, int Duplicates, int Filtered,
    int Unsupported, int Errors, bool Cancelled, TimeSpan Duration);
