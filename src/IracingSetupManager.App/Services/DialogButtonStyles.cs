using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Services;

public static class DialogButtonStyles
{
    public static ContentDialog ApplyActionStyles(this ContentDialog dialog)
    {
        dialog.PrimaryButtonStyle = Resolve(dialog.PrimaryButtonText);
        dialog.SecondaryButtonStyle = Resolve(dialog.SecondaryButtonText);
        dialog.CloseButtonStyle = Resolve(dialog.CloseButtonText);
        return dialog;
    }

    private static Style? Resolve(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var normalized = label.Trim();
        var resource = StartsWithAny(normalized, "Valider", "Confirmer", "Enregistrer", "Copier", "Appliquer", "Installer", "Réinstaller")
            ? "BlueActionButtonStyle"
            : StartsWithAny(normalized, "Annuler", "Refuser", "Supprimer", "Interrompre", "Retirer", "Effacer")
                ? "RedDangerButtonStyle"
                : "GraySecondaryButtonStyle";
        return Application.Current.Resources[resource] as Style;
    }

    private static bool StartsWithAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.StartsWith(candidate, StringComparison.CurrentCultureIgnoreCase));
}
