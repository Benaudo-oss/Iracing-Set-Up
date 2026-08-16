using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Iracing;
using IracingSetupManager.Core.Setups;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using IracingSetupManager.App.Services;

namespace IracingSetupManager.App.Views;

public sealed partial class IracingCopyPage : Page
{
    private List<CopyRow> allRows = [];
    private List<CopyRow> rows = [];
    private bool filtersReady;
    private bool previewMode;
    private bool isTeam;
    private string? teamName;

    public IracingCopyPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        isTeam = e.Parameter as string == "team";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadPageAsync, "Impossible de charger les setups validés", ActionInfo);

    private async Task LoadPageAsync()
    {
        if (isTeam)
        {
            PageTitle.Text = "Copie vers iRacing Team";
            teamName = await App.Services.IracingTeam.GetNameAsync();
            PageSubtitle.Text = string.IsNullOrWhiteSpace(teamName)
                ? "Définissez d’abord le nom de la Team dans les paramètres."
                : $"Team Garage61 : {teamName} — dossier Garage 61 - {teamName}.";
            FolderPathBox.Text = IracingCopyService.DetectSetupsFolder() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(teamName))
            {
                Show("Indiquez d’abord le nom de la Team dans les paramètres.", InfoBarSeverity.Warning);
            }
        }
        else
        {
            FolderPathBox.Text = IracingCopyService.DetectSetupsFolder() ?? string.Empty;
        }
        await LoadValidatedAsync();
    }

    private async Task LoadValidatedAsync()
    {
        var setups = (await App.Services.QueryService.GetAllAsync())
            .Where(item => item.Status == SetupStatus.Valide)
            .ToList();
        allRows = setups.Select(setup => CopyRow.FromSetup(setup, isTeam)).ToList();
        previewMode = false;
        UpdatePreviewActions();
        PopulateFilters();
        ApplyFilters();
    }

    private void OnDetectFolder(object sender, RoutedEventArgs e)
    {
        FolderPathBox.Text = IracingCopyService.DetectSetupsFolder() ?? string.Empty;
        Show(FolderPathBox.Text.Length > 0 ? "Dossier iRacing détecté." : "Dossier non détecté : sélectionnez-le manuellement.", FolderPathBox.Text.Length > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private async void OnPickFolder(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(PickFolderAsync, "Impossible de sélectionner le dossier iRacing", ActionInfo);

    private async Task PickFolderAsync()
    {
        var path = await Services.WinUiFolderPicker.PickAsync();
        if (!string.IsNullOrWhiteSpace(path)) FolderPathBox.Text = path;
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => SetupList.SelectAll();

    private void OnFilterChanged(object sender, object e)
    {
        if (filtersReady) ApplyFilters();
    }

    private void OnClearFilters(object sender, RoutedEventArgs e)
    {
        filtersReady = false;
        SearchBox.Text = string.Empty;
        ProviderFilter.SelectedIndex = 0;
        CategoryFilter.SelectedIndex = 0;
        SeasonFilter.SelectedIndex = 0;
        CarFilter.SelectedIndex = 0;
        TrackFilter.SelectedIndex = 0;
        CopyStatusFilter.SelectedIndex = 2;
        filtersReady = true;
        ApplyFilters();
    }

    private void OnRemoveFilter(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        filtersReady = false;
        switch (key)
        {
            case "search": SearchBox.Text = string.Empty; break;
            case "provider": ProviderFilter.SelectedIndex = 0; break;
            case "category": CategoryFilter.SelectedIndex = 0; break;
            case "season": SeasonFilter.SelectedIndex = 0; break;
            case "car": CarFilter.SelectedIndex = 0; break;
            case "track": TrackFilter.SelectedIndex = 0; break;
            case "copy-status": CopyStatusFilter.SelectedIndex = 2; break;
        }
        filtersReady = true;
        ApplyFilters();
    }

    private async void OnCreatePreview(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(CreatePreviewAsync, "Impossible de créer l’aperçu", ActionInfo);

    private async Task CreatePreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPathBox.Text))
        {
            Show("Sélectionnez d’abord le dossier officiel des setups iRacing.", InfoBarSeverity.Warning);
            return;
        }

        if (isTeam && string.IsNullOrWhiteSpace(teamName))
        {
            Show("Indiquez d’abord le nom de la Team dans les paramètres.", InfoBarSeverity.Warning);
            return;
        }

        var selected = SetupList.SelectedItems.Cast<CopyRow>().ToList();
        if (selected.Count == 0)
        {
            Show("Sélectionnez au moins un setup validé.", InfoBarSeverity.Warning);
            return;
        }

        var ids = selected.Select(item => item.Id).ToArray();
        var plan = await App.Services.IracingCopy.CreatePlanAsync(
            ids,
            FolderPathBox.Text,
            teamName: isTeam ? teamName : null);
        var detectedWeeks = plan.Where(item => item.Week is not null).Select(item => item.Week!.Value).Distinct().ToList();
        if (plan.Any(item => item.Week is null) || detectedWeeks.Count > 1)
        {
            var description = plan.Count == 1
                ? plan[0].OriginalFileName
                : $"{plan.Count} setups sélectionnés";
            var week = await AskWeekAsync(description);
            if (week is null)
            {
                Show("Aperçu annulé : choisissez une semaine commune pour tous les setups.", InfoBarSeverity.Warning);
                return;
            }
            var weeks = plan.ToDictionary(item => item.SetupId, _ => week.Value);
            plan = await App.Services.IracingCopy.CreatePlanAsync(
                ids,
                FolderPathBox.Text,
                weeks,
                teamName: isTeam ? teamName : null);
        }
        var sourceRows = selected.ToDictionary(item => item.Id);
        allRows = plan.Select(item => CopyRow.FromPlan(item, sourceRows[item.SetupId])).ToList();
        previewMode = true;
        UpdatePreviewActions();
        PopulateFilters();
        ApplyFilters();
        SetupList.SelectAll();
        Show("Aperçu prêt. Vérifiez les destinations et résolvez les conflits.", InfoBarSeverity.Informational);
    }

    private async void OnExitPreview(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(ExitPreviewAsync, "Impossible de quitter l’aperçu", ActionInfo);

    private async Task ExitPreviewAsync()
    {
        await LoadValidatedAsync();
        Show("Aperçu fermé. Vous pouvez modifier votre sélection.", InfoBarSeverity.Informational);
    }

    private void UpdatePreviewActions()
    {
        PreviewButton.Visibility = previewMode ? Visibility.Collapsed : Visibility.Visible;
        CopyButton.Visibility = previewMode ? Visibility.Visible : Visibility.Collapsed;
        ExitPreviewButton.Visibility = previewMode ? Visibility.Visible : Visibility.Collapsed;
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
            Title = isTeam ? "Confirmer la copie vers iRacing Team" : "Confirmer la copie vers iRacing",
            Content = $"{plan.Count(item => !item.HasConflict || item.ConflictChoice != IracingConflictChoice.Skip)} fichier(s) seront copiés. Les originaux resteront dans l’archive.",
            PrimaryButtonText = "Copier",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var result = await App.Services.IracingCopy.ExecuteAsync(
                plan,
                confirmed: true,
                target: isTeam ? IracingCopyTarget.Team : IracingCopyTarget.Personal);
            await LoadValidatedAsync();
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

    private void PopulateFilters()
    {
        filtersReady = false;
        FillFilter(ProviderFilter, allRows.Select(item => item.Provider));
        FillFilter(CategoryFilter, allRows.Select(item => item.Category));
        FillFilter(SeasonFilter, allRows.Select(item => item.Season));
        FillFilter(CarFilter, allRows.Select(item => item.Car));
        FillFilter(TrackFilter, allRows.Select(item => item.Track));
        CopyStatusFilter.ItemsSource = new[] { "À copier", "Déjà copiés", "Tous" };
        CopyStatusFilter.SelectedIndex = previewMode ? 2 : 0;
        filtersReady = true;
    }

    private static void FillFilter(ComboBox filter, IEnumerable<string> values)
    {
        filter.ItemsSource = new[] { "Tous" }.Concat(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
            .ToList();
        filter.SelectedIndex = 0;
    }

    private void ApplyFilters()
    {
        var search = SearchBox.Text.Trim();
        rows = allRows.Where(item =>
            MatchesSelection(item.Provider, ProviderFilter) &&
            MatchesSelection(item.Category, CategoryFilter) &&
            MatchesSelection(item.Season, SeasonFilter) &&
            MatchesSelection(item.Car, CarFilter) &&
            MatchesSelection(item.Track, TrackFilter) &&
            MatchesCopyStatus(item) &&
            (search.Length == 0 || new[] { item.OriginalFileName, item.Provider, item.Category, item.Season, item.Car, item.Track }
                .Any(value => value.Contains(search, StringComparison.CurrentCultureIgnoreCase))))
            .ToList();
        SetupList.ItemsSource = rows;
        SelectionSummary.Text = previewMode
            ? $"Aperçu : {rows.Count}/{allRows.Count} fichier(s), {rows.Count(item => item.HasConflict)} conflit(s) affiché(s)"
            : $"{rows.Count}/{allRows.Count} setup(s) validé(s) affiché(s)";
        ResultCountText.Text = $"{rows.Count} résultat{(rows.Count > 1 ? "s" : string.Empty)} sur {allRows.Count}";
        var active = new List<(string Key, string Label)>();
        if (!string.IsNullOrWhiteSpace(search)) active.Add(("search", $"Recherche : {search}"));
        AddActive(active, "provider", "Fournisseur", ProviderFilter.SelectedItem as string);
        AddActive(active, "category", "Catégorie", CategoryFilter.SelectedItem as string);
        AddActive(active, "season", "Saison", SeasonFilter.SelectedItem as string);
        AddActive(active, "car", "Voiture", CarFilter.SelectedItem as string);
        AddActive(active, "track", "Circuit", TrackFilter.SelectedItem as string);
        AddActive(active, "copy-status", "État", CopyStatusFilter.SelectedItem as string);
        FilterPresentation.Rebuild(ActiveFiltersPanel, active, OnRemoveFilter);
    }

    private static void AddActive(List<(string Key, string Label)> filters, string key, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != "Tous") filters.Add((key, $"{label} : {value}"));
    }

    private static bool MatchesSelection(string value, ComboBox filter) =>
        filter.SelectedItem is not string selected || selected == "Tous" || value.Equals(selected, StringComparison.OrdinalIgnoreCase);

    private bool MatchesCopyStatus(CopyRow row) =>
        previewMode || CopyStatusFilter.SelectedItem switch
        {
            "À copier" => !row.IsCopied,
            "Déjà copiés" => row.IsCopied,
            _ => true
        };

    private async Task<int?> AskWeekAsync(string fileName)
    {
        int? selectedWeek = null;
        var weekButtons = new List<ToggleButton>();
        var weekGrid = new Grid { RowSpacing = 8, ColumnSpacing = 8 };
        for (var column = 0; column < 7; column++)
            weekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        weekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        weekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = $"Choisissez la semaine commune pour :\n{fileName}", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = "Cette Week sera appliquée à tous les setups de cette copie :", Opacity = 0.75 });
        content.Children.Add(weekGrid);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Choisir la semaine",
            Content = content,
            PrimaryButtonText = "Confirmer",
            CloseButtonText = "Annuler",
            IsPrimaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.Primary
        };

        for (var week = 1; week <= 13; week++)
        {
            var value = week;
            var button = new ToggleButton { Content = week.ToString(), Width = 48, Height = 40 };
            button.Click += (_, _) =>
            {
                foreach (var candidate in weekButtons) candidate.IsChecked = false;
                button.IsChecked = true;
                selectedWeek = value;
                dialog.IsPrimaryButtonEnabled = true;
            };
            Grid.SetRow(button, (week - 1) / 7);
            Grid.SetColumn(button, (week - 1) % 7);
            weekButtons.Add(button);
            weekGrid.Children.Add(button);
        }

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? selectedWeek : null;
    }

    private sealed class CopyRow
    {
        public Guid Id { get; init; }
        public required string OriginalFileName { get; init; }
        public required string Car { get; init; }
        public required string Provider { get; init; }
        public required string Category { get; init; }
        public required string Season { get; init; }
        public required string Track { get; init; }
        public DateTimeOffset? LastCopiedAtUtc { get; init; }
        public int CopyCount { get; init; }
        public IracingCopyPlanItem? Plan { get; init; }
        public bool IsCopied => LastCopiedAtUtc is not null || CopyCount > 0;
        public bool HasConflict => Plan?.HasConflict == true;
        public Visibility ConflictVisibility => HasConflict ? Visibility.Visible : Visibility.Collapsed;
        public string CopyDescription => Plan is not null
            ? $"{(HasConflict ? "Conflit — " : string.Empty)}{Plan.DestinationPath}"
            : IsCopied
                ? $"Déjà copié ({CopyCount} fois)"
                : "Sélectionnable pour l’aperçu";
        public int ConflictChoiceIndex { get; set; }

        public static CopyRow FromSetup(SetupEntity setup, bool team) => new()
        {
            Id = setup.Id, OriginalFileName = setup.OriginalFileName, Car = setup.Car, Provider = setup.Provider,
            Category = setup.Category, Season = setup.Season ?? string.Empty, Track = setup.Track,
            LastCopiedAtUtc = team ? setup.LastCopiedToIracingTeamAtUtc : setup.LastCopiedToIracingAtUtc,
            CopyCount = team ? setup.IracingTeamCopyCount : setup.IracingCopyCount
        };
        public static CopyRow FromPlan(IracingCopyPlanItem plan, CopyRow source) => new()
        {
            Id = plan.SetupId, OriginalFileName = plan.OriginalFileName, Car = plan.Car, Provider = source.Provider,
            Category = source.Category, Season = source.Season, Track = source.Track,
            LastCopiedAtUtc = source.LastCopiedAtUtc, CopyCount = source.CopyCount,
            Plan = plan, ConflictChoiceIndex = 0
        };
        public IracingCopyPlanItem ToPlan() => Plan! with { ConflictChoice = ConflictChoiceIndex switch { 1 => IracingConflictChoice.Skip, 2 => IracingConflictChoice.KeepBoth, _ => IracingConflictChoice.None } };
    }
}
