using IracingSetupManager.Infrastructure.Files.Import;

namespace IracingSetupManager.Infrastructure.Settings;

public static class SynchronizationImportPolicy
{
    private const string Unknown = "À identifier";

    public static bool Allows(SynchronizationSelection selection, SetupMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(metadata);

        var providerAllowed = IsUnknown(metadata.Provider) ||
            selection.Providers.Contains(metadata.Provider, StringComparer.OrdinalIgnoreCase);
        // A category filter must remain strict. Importing an unidentified category here can
        // make a later metadata refresh reveal a category that the user explicitly excluded.
        var categoryAllowed = selection.Categories.Contains(metadata.Category, StringComparer.OrdinalIgnoreCase);

        return providerAllowed && categoryAllowed;
    }

    private static bool IsUnknown(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals(Unknown, StringComparison.OrdinalIgnoreCase);
}
