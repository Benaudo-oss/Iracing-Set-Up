using System.Collections.ObjectModel;
using IracingSetupManager.Infrastructure.Database;
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
    private const int PageSize = 100;
    private readonly ObservableCollection<CopyRow> rows = [];
    private readonly HashSet<Guid> loadedIds = [];
    private readonly SemaphoreSlim pageLoadLock = new(1, 1);
    private List<CopyRow> previewRows = [];
    private bool filtersReady;
    private bool previewMode;
    private bool isTeam;
    private string? teamName;
    private int totalCount;
    private int queryVersion;
    private SetupFilterOptions? filterOptions;
    private CancellationTokenSource? searchDelayCancellation;
    private CancellationTokenSource? pageLoadCancellation;

    public IracingCopyPage()
    {
        InitializeComponent();
        SetupList.ItemsSource = rows;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        isTeam = e.Parameter as string == "team";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadPageAsync, "Impossible de charger les setups validés", ActionInfo);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        searchDelayCancellation?.Cancel();
        pageLoadCancellation?.Cancel();
    }

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
        filterOptions = await App.Services.QueryService.GetFilterOptionsAsync(requiredStatus: SetupStatus.Valide);
        previewMode = false;
        previewRows.Clear();
        UpdatePreviewActions();
        PopulateFilters();
        await ResetPagesAsync();
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

    private async void OnFilterChanged(object sender, object e)
    {
        if (!filtersReady) return;
        if (sender is TextBox)
        {
            searchDelayCancellation?.Cancel();
            var cancellation = searchDelayCancellation = new CancellationTokenSource();
            try
            {
                await Task.Delay(300, cancellation.Token);
                if (!cancellation.IsCancellationRequested)
                    await UiOperation.RunAsync(ApplyFiltersAsync, "Impossible d’appliquer la recherche", ActionInfo);
            }
            catch (OperationCanceledException) { }
            return;
        }
        await UiOperation.RunAsync(ApplyFiltersAsync, "Impossible d’appliquer les filtres", ActionInfo);
    }

    private async void OnClearFilters(object sender, RoutedEventArgs e)
    {
        filtersReady = false;
        SearchBox.Text = string.Empty;
        ProviderFilter.SelectedIndex = 0;
        CategoryFilter.SelectedIndex = 0;
        SeasonFilter.SelectedIndex = 0;
        WeekFilter.SelectedIndex = 0;
        CarFilter.SelectedIndex = 0;
        TrackFilter.SelectedIndex = 0;
        CopyStatusFilter.SelectedIndex = 2;
        filtersReady = true;
        await UiOperation.RunAsync(ApplyFiltersAsync, "Impossible d’effacer les filtres", ActionInfo);
    }

    private async void OnRemoveFilter(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        filtersReady = false;
        switch (key)
        {
            case "search": SearchBox.Text = string.Empty; break;
            case "provider": ProviderFilter.SelectedIndex = 0; break;
            case "category": CategoryFilter.SelectedIndex = 0; break;
            case "season": SeasonFilter.SelectedIndex = 0; break;
            case "week": WeekFilter.SelectedIndex = 0; break;
            case "car": CarFilter.SelectedIndex = 0; break;
            case "track": TrackFilter.SelectedIndex = 0; break;
            case "copy-status": CopyStatusFilter.SelectedIndex = 2; break;
        }
        filtersReady = true;
        await UiOperation.RunAsync(ApplyFiltersAsync, "Impossible de retirer le filtre", ActionInfo);
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
        var unknownIds = plan.Where(item => item.WeekKind == SetupWeekKind.Unknown)
            .Select(item => item.SetupId).ToHashSet();
        if (unknownIds.Count > 0)
        {
            var description = unknownIds.Count == 1
                ? plan.Single(item => unknownIds.Contains(item.SetupId)).OriginalFileName
                : $"{unknownIds.Count} setups avec une Week inconnue";
            var choice = await AskWeekAsync(description);
            if (choice is null)
            {
                Show("Aperçu annulé : aucun choix de Week n’a été confirmé.", InfoBarSeverity.Warning);
                return;
            }
            var choices = unknownIds.ToDictionary(id => id, _ => choice);
            plan = await App.Services.IracingCopy.CreatePlanAsync(
                ids,
                FolderPathBox.Text,
                teamName: isTeam ? teamName : null,
                weekChoices: choices);
        }
        var sourceRows = selected.ToDictionary(item => item.Id);
        previewRows = plan.Select(item => CopyRow.FromPlan(item, sourceRows[item.SetupId])).ToList();
        previewMode = true;
        UpdatePreviewActions();
        PopulateFilters();
        await ApplyFiltersAsync();
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
        if (await dialog.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;

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
        FillFilter(ProviderFilter, previewMode ? previewRows.Select(item => item.Provider) : filterOptions?.Providers ?? []);
        FillFilter(CategoryFilter, previewMode ? previewRows.Select(item => item.Category) : filterOptions?.Categories ?? []);
        FillFilter(SeasonFilter, previewMode ? previewRows.Select(item => item.Season) : filterOptions?.Seasons ?? []);
        FillFilter(WeekFilter, previewMode ? previewRows.Select(item => item.WeekDisplay) : filterOptions?.Weeks ?? []);
        FillFilter(CarFilter, previewMode ? previewRows.Select(item => item.Car) : filterOptions?.Cars ?? []);
        FillFilter(TrackFilter, previewMode ? previewRows.Select(item => item.Track) : filterOptions?.Tracks ?? []);
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

    private async Task ApplyFiltersAsync()
    {
        if (!previewMode)
        {
            await ResetPagesAsync();
            return;
        }

        var search = SearchBox.Text.Trim();
        var filtered = previewRows.Where(item =>
            MatchesSelection(item.Provider, ProviderFilter) &&
            MatchesSelection(item.Category, CategoryFilter) &&
            MatchesSelection(item.Season, SeasonFilter) &&
            MatchesSelection(item.WeekDisplay, WeekFilter) &&
            MatchesSelection(item.Car, CarFilter) &&
            MatchesSelection(item.Track, TrackFilter) &&
            MatchesCopyStatus(item) &&
            (search.Length == 0 || new[] { item.OriginalFileName, item.Provider, item.Category, item.Season, item.WeekDisplay, item.Car, item.Track }
                .Any(value => value.Contains(search, StringComparison.CurrentCultureIgnoreCase))))
            .ToList();
        rows.Clear();
        foreach (var row in filtered) rows.Add(row);
        totalCount = previewRows.Count;
        UpdateResultPresentation();
        RebuildActiveFilters();
    }

    private async Task ResetPagesAsync()
    {
        var version = Interlocked.Increment(ref queryVersion);
        pageLoadCancellation?.Cancel();
        pageLoadCancellation = new CancellationTokenSource();
        rows.Clear();
        loadedIds.Clear();
        totalCount = 0;
        UpdateResultPresentation();
        RebuildActiveFilters();
        await LoadNextPageAsync(version, pageLoadCancellation.Token);
    }

    private async Task LoadNextPageAsync(int version, CancellationToken cancellationToken)
    {
        if (previewMode || rows.Count >= totalCount && totalCount > 0) return;
        try { await pageLoadLock.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return; }
        try
        {
            if (version != queryVersion || previewMode || rows.Count >= totalCount && totalCount > 0) return;
            var page = await App.Services.QueryService.GetPageAsync(CreatePageRequest(rows.Count), cancellationToken);
            if (version != queryVersion || cancellationToken.IsCancellationRequested) return;
            totalCount = page.TotalCount;
            foreach (var setup in page.Items)
            {
                if (loadedIds.Add(setup.Id)) rows.Add(CopyRow.FromSetup(setup, isTeam));
            }
            UpdateResultPresentation();
        }
        catch (OperationCanceledException) { }
        finally { pageLoadLock.Release(); }
    }

    private SetupPageRequest CreatePageRequest(int skip) => new(
        skip,
        PageSize,
        SearchBox.Text,
        Selection(ProviderFilter),
        Selection(CategoryFilter),
        Selection(CarFilter),
        Selection(TrackFilter),
        Season: Selection(SeasonFilter),
        Week: Selection(WeekFilter),
        ValidatedOnly: true,
        CopyState: Selection(CopyStatusFilter),
        TeamCopy: isTeam);

    private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (previewMode || args.InRecycleQueue || args.ItemIndex < rows.Count - 20 || rows.Count >= totalCount) return;
        var cancellationToken = pageLoadCancellation?.Token ?? CancellationToken.None;
        await UiOperation.RunAsync(
            () => LoadNextPageAsync(queryVersion, cancellationToken),
            "Impossible de charger la suite des setups validés",
            ActionInfo);
    }

    private void UpdateResultPresentation()
    {
        SelectionSummary.Text = previewMode
            ? $"Aperçu : {rows.Count}/{totalCount} fichier(s), {rows.Count(item => item.HasConflict)} conflit(s) affiché(s)"
            : $"{rows.Count} chargé{(rows.Count > 1 ? "s" : string.Empty)} sur {totalCount} setup(s) validé(s)";
        ResultCountText.Text = $"{rows.Count} affiché{(rows.Count > 1 ? "s" : string.Empty)} sur {totalCount}";
    }

    private void RebuildActiveFilters()
    {
        var search = SearchBox.Text.Trim();
        var active = new List<(string Key, string Label)>();
        if (!string.IsNullOrWhiteSpace(search)) active.Add(("search", $"Recherche : {search}"));
        AddActive(active, "provider", "Fournisseur", ProviderFilter.SelectedItem as string);
        AddActive(active, "category", "Catégorie", CategoryFilter.SelectedItem as string);
        AddActive(active, "season", "Saison", SeasonFilter.SelectedItem as string);
        AddActive(active, "week", "Week", WeekFilter.SelectedItem as string);
        AddActive(active, "car", "Voiture", CarFilter.SelectedItem as string);
        AddActive(active, "track", "Circuit", TrackFilter.SelectedItem as string);
        AddActive(active, "copy-status", "État", CopyStatusFilter.SelectedItem as string);
        FilterPresentation.Rebuild(ActiveFiltersPanel, active, OnRemoveFilter);
    }

    private static string? Selection(ComboBox filter) =>
        filter.SelectedItem is string value && value != "Tous" ? value : null;

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

    private async Task<SetupWeekChoice?> AskWeekAsync(string fileName)
    {
        SetupWeekChoice? selectedWeek = null;
        var weekButtons = new List<ToggleButton>();
        var weekGrid = new Grid { RowSpacing = 8, ColumnSpacing = 8 };
        for (var column = 0; column < 8; column++)
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
                selectedWeek = SetupWeekChoice.Numeric(value);
                dialog.IsPrimaryButtonEnabled = true;
            };
            Grid.SetRow(button, (week - 1) / 7);
            Grid.SetColumn(button, (week - 1) % 7);
            weekButtons.Add(button);
            weekGrid.Children.Add(button);
        }

        AddSpecialChoice("NEC", SetupWeekChoice.Nec, 0, 7);
        AddSpecialChoice("Sans Week", SetupWeekChoice.NoWeek, 1, 7);

        return await dialog.ApplyActionStyles().ShowAsync() == ContentDialogResult.Primary ? selectedWeek : null;

        void AddSpecialChoice(string label, SetupWeekChoice choice, int row, int column)
        {
            var button = new ToggleButton { Content = label, MinWidth = 78, Height = 40 };
            button.Click += (_, _) =>
            {
                foreach (var candidate in weekButtons) candidate.IsChecked = false;
                button.IsChecked = true;
                selectedWeek = choice;
                dialog.IsPrimaryButtonEnabled = true;
            };
            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);
            weekButtons.Add(button);
            weekGrid.Children.Add(button);
        }
    }

    private sealed class CopyRow
    {
        public Guid Id { get; init; }
        public required string OriginalFileName { get; init; }
        public required string Car { get; init; }
        public required string Provider { get; init; }
        public required string Category { get; init; }
        public required string Season { get; init; }
        public required string WeekDisplay { get; init; }
        public SetupWeekKind WeekKind { get; init; }
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
            Category = setup.Category, Season = setup.Season ?? string.Empty, WeekDisplay = setup.WeekDisplay, WeekKind = setup.WeekKind, Track = setup.Track,
            LastCopiedAtUtc = team ? setup.LastCopiedToIracingTeamAtUtc : setup.LastCopiedToIracingAtUtc,
            CopyCount = team ? setup.IracingTeamCopyCount : setup.IracingCopyCount
        };
        public static CopyRow FromPlan(IracingCopyPlanItem plan, CopyRow source) => new()
        {
            Id = plan.SetupId, OriginalFileName = plan.OriginalFileName, Car = plan.Car, Provider = source.Provider,
            Category = source.Category, Season = source.Season, WeekDisplay = SetupWeekPresentation.Display(plan.Week, plan.WeekKind), WeekKind = plan.WeekKind, Track = source.Track,
            LastCopiedAtUtc = source.LastCopiedAtUtc, CopyCount = source.CopyCount,
            Plan = plan, ConflictChoiceIndex = 0
        };
        public IracingCopyPlanItem ToPlan() => Plan! with { ConflictChoice = ConflictChoiceIndex switch { 1 => IracingConflictChoice.Skip, 2 => IracingConflictChoice.KeepBoth, _ => IracingConflictChoice.None } };
    }
}
