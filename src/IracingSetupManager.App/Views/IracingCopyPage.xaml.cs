using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Iracing;
using IracingSetupManager.Core.Setups;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class IracingCopyPage : Page
{
    private List<CopyRow> rows = [];

    public IracingCopyPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        FolderPathBox.Text = IracingCopyService.DetectSetupsFolder() ?? string.Empty;
        await LoadValidatedAsync();
    }

    private async Task LoadValidatedAsync()
    {
        var setups = (await App.Services.QueryService.GetAllAsync())
            .Where(item => item.Status == SetupStatus.Valide)
            .ToList();
        rows = setups.Select(CopyRow.FromSetup).ToList();
        SetupList.ItemsSource = rows;
        SelectionSummary.Text = $"{rows.Count} setup(s) validé(s) disponible(s)";
    }

    private void OnDetectFolder(object sender, RoutedEventArgs e)
    {
        FolderPathBox.Text = IracingCopyService.DetectSetupsFolder() ?? string.Empty;
        Show(FolderPathBox.Text.Length > 0 ? "Dossier iRacing détecté." : "Dossier non détecté : sélectionnez-le manuellement.", FolderPathBox.Text.Length > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private async void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var path = await Services.WinUiFolderPicker.PickAsync();
        if (!string.IsNullOrWhiteSpace(path)) FolderPathBox.Text = path;
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => SetupList.SelectAll();

    private async void OnCreatePreview(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FolderPathBox.Text))
        {
            Show("Sélectionnez d’abord le dossier des setups iRacing.", InfoBarSeverity.Warning);
            return;
        }

        var selected = SetupList.SelectedItems.Cast<CopyRow>().ToList();
        if (selected.Count == 0)
        {
            Show("Sélectionnez au moins un setup validé.", InfoBarSeverity.Warning);
            return;
        }

        var ids = selected.Select(item => item.Id).ToArray();
        var plan = await App.Services.IracingCopy.CreatePlanAsync(ids, FolderPathBox.Text);
        var weeks = new Dictionary<Guid, int>();
        foreach (var item in plan.Where(item => item.Week is null))
        {
            var week = await AskWeekAsync(item.OriginalFileName);
            if (week is null)
            {
                Show("Aperçu annulé : la semaine est obligatoire pour tous les setups.", InfoBarSeverity.Warning);
                return;
            }
            weeks[item.SetupId] = week.Value;
        }
        if (weeks.Count > 0)
        {
            plan = await App.Services.IracingCopy.CreatePlanAsync(ids, FolderPathBox.Text, weeks);
        }
        rows = plan.Select(CopyRow.FromPlan).ToList();
        SetupList.ItemsSource = rows;
        SetupList.SelectAll();
        SelectionSummary.Text = $"Aperçu : {rows.Count} fichier(s), {rows.Count(item => item.HasConflict)} conflit(s)";
        Show("Aperçu prêt. Vérifiez les destinations et résolvez les conflits.", InfoBarSeverity.Informational);
    }

    private async void OnCopy(object sender, RoutedEventArgs e)
    {
        if (rows.Count == 0 || rows.Any(item => item.Plan is null))
        {
            Show("Créez d’abord l’aperçu des fichiers.", InfoBarSeverity.Warning);
            return;
        }

        var plan = rows.Select(item => item.ToPlan()).ToList();
        if (plan.Any(item => item.HasConflict && item.ConflictChoice == IracingConflictChoice.None))
        {
            Show("Choisissez une action pour chaque conflit.", InfoBarSeverity.Warning);
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Confirmer la copie vers iRacing",
            Content = $"{plan.Count(item => item.ConflictChoice != IracingConflictChoice.Skip)} fichier(s) seront copiés. Les originaux resteront dans l’archive.",
            PrimaryButtonText = "Copier",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var result = await App.Services.IracingCopy.ExecuteAsync(plan, confirmed: true);
            Show($"Copie terminée : {result.Copied} copié(s), {result.Skipped} ignoré(s).", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            Show(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void Show(string message, InfoBarSeverity severity)
    {
        ActionInfo.Message = message;
        ActionInfo.Severity = severity;
        ActionInfo.IsOpen = true;
    }

    private async Task<int?> AskWeekAsync(string fileName)
    {
        var input = new NumberBox
        {
            Header = "Numéro de semaine",
            Minimum = 1,
            Maximum = 13,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            PlaceholderText = "Entre 1 et 13"
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = $"La semaine est inconnue pour :\n{fileName}", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(input);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Choisir la semaine",
            Content = content,
            PrimaryButtonText = "Valider",
            CloseButtonText = "Annuler",
            IsPrimaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.Primary
        };
        input.ValueChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !double.IsNaN(input.Value) && input.Value >= 1 && input.Value <= 13 && input.Value == Math.Truncate(input.Value);
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? (int)input.Value : null;
    }

    private sealed class CopyRow
    {
        public Guid Id { get; init; }
        public required string OriginalFileName { get; init; }
        public required string Car { get; init; }
        public required string Provider { get; init; }
        public IracingCopyPlanItem? Plan { get; init; }
        public bool HasConflict => Plan?.HasConflict == true;
        public string CopyDescription => Plan is null ? "Sélectionnable pour l’aperçu" : $"{(HasConflict ? "Conflit — " : string.Empty)}{Plan.DestinationPath}";
        public int ConflictChoiceIndex { get; set; }

        public static CopyRow FromSetup(SetupEntity setup) => new() { Id = setup.Id, OriginalFileName = setup.OriginalFileName, Car = setup.Car, Provider = setup.Provider };
        public static CopyRow FromPlan(IracingCopyPlanItem plan) => new() { Id = plan.SetupId, OriginalFileName = plan.OriginalFileName, Car = plan.Car, Provider = string.Empty, Plan = plan, ConflictChoiceIndex = plan.HasConflict ? 0 : 1 };
        public IracingCopyPlanItem ToPlan() => Plan! with { ConflictChoice = ConflictChoiceIndex switch { 1 => IracingConflictChoice.Skip, 2 => IracingConflictChoice.KeepBoth, _ => IracingConflictChoice.None } };
    }
}
