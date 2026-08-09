using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Integrations.Garage61;

public sealed class Garage61ExportPolicy
{
    public bool CanExport(SetupFile setup, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(setup);

        if (setup.IsPrivate)
        {
            reason = "Le setup est marqué comme privé.";
            return false;
        }

        if (!setup.Garage61ExportApproved)
        {
            reason = "L'envoi vers Garage61 n'a pas été approuvé manuellement.";
            return false;
        }

        if (setup.Status != SetupStatus.Valide)
        {
            reason = "Le setup doit être validé avant l'envoi.";
            return false;
        }

        reason = null;
        return true;
    }
}

