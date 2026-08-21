using System.Collections.ObjectModel;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.App.Services;
using IracingSetupManager.Core.Presentation;
using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Infrastructure.Resilience;

namespace IracingSetupManager.App.Views;

public sealed partial class ReviewPage : Page
{
    private const int PageSize = 100;
    private readonly HashSet<Guid> _setupIds = [];
    private readonly ObservableCollection<SetupEntity> _visibleSetups = [];
    private readonly HashSet<Guid> _visibleSetupIds = [];
    private readonly ObservableCollection<string> _providers = [];
    private readonly ObservableCollection<string> _categories = [];
    private readonly ObservableCollection<string> _cars = [];
    private readonly ObservableCollection<string> _tracks = [];
    private readonly ObservableCollection<string> _weeks = [];
    private readonly SemaphoreSlim _pageLoadLock = new(1, 1);
    private readonly SingleFlightGate _incrementalLoadGate = new();
    private bool _isPageActive;
    private bool _isListeningForImports;
    private bool _suppressFilterChanges;
    private int _totalCount;
    private int _queryVersion;
    private SetupFilterOptions? _filterOptions;
    private CancellationTokenSource? _searchDelayCancellation;
    private CancellationTokenSource? _pageLoadCancellation;

    public ReviewPage()
    {
        InitializeComponent();
        ReviewList.ItemsSource = _visibleSetups;
        ProviderFilter.ItemsSource = _providers;
        CategoryFilter.ItemsSource = _categories;
        CarFilter.ItemsSource = _cars;
        TrackFilter.ItemsSource = _tracks;
        WeekFilter.ItemsSource = _weeks;
        IdentificationFilter.ItemsSource = new[] { "Tous", "À identifier", "Identifiés" };
        IdentificationFilter.SelectedIndex = 0;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = true;
        await UiOperation.RunAsync(LoadPageAsync, "Impossible de charger les setups à vérifier", ReviewInfo);
    }

    private async Task LoadPageAsync()
    {
        StartListeningForImports();
        await ReloadAsync(true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;
        Interlocked.Increment(ref _queryVersion);
        _incrementalLoadGate.Exit();
        _searchDelayCancellation?.Cancel();
        _pageLoadCancellation?.Cancel();
        if (!_isListeningForImports) return;
        App.Services.Monitoring.ImportCompleted -= OnImportCompleted;
        _isListeningForImports = false;
    }

    private void StartListeningForImports()
    {
        if (_isListeningForImports) return;
        App.Services.Monitoring.ImportCompleted += OnImportCompleted;
        _isListeningForImports = true;
    }

    private void OnImportCompleted(object? sender, SetupImportResult result)
    {
        if (result.Outcome != SetupImportOutcome.Imported || string.IsNullOrWhiteSpace(result.Sha256)) return;
        DispatcherQueue.TryEnqueue(async () =>
        {
            await UiOperation.RunAsync(async () =>
            {
                if (!IsLoaded) return;
                var setup = await App.Services.QueryService.GetBySha256Async(result.Sha256);
                if (setup is not null && setup.Status == SetupStatus.AVerifier) AddOrUpdateSetup(setup);
            }, "Impossible d’actualiser les setups à vérifier", ReviewInfo);
        });
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => ReviewList.SelectAll();

    private void OnSelectIdentified(object sender, RoutedEventArgs e)
    {
        ReviewList.SelectedItems.Clear();
        foreach (var setup in _visibleSetups.Where(IsFullyIdentified))
            ReviewList.SelectedItems.Add(setup);

        var excluded = _visibleSetups.Count - ReviewList.SelectedItems.Count;
        ShowSuccess($"{ReviewList.SelectedItems.Count} setup(s) complètement identifié(s) sélectionné(s). " +
                    $"{excluded} setup(s) à identifier laissé(s) de côté.");
    }

    private static bool IsFullyIdentified(SetupEntity setup) =>
        IsIdentified(setup.Provider) &&
        IsIdentified(setup.Category) &&
        IsIdentified(setup.Car) &&
        IsIdentified(setup.Track) &&
        IsIdentified(setup.Season);

    private static bool IsIdentified(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Equals("À identifier", StringComparison.OrdinalIgnoreCase);

    private async void OnCorrectOne(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => CorrectOneAsync(sender), "La correction a échoué", ReviewInfo);

    private async void OnCorrectSelection(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(CorrectSelectionAsync, "La correction groupée a échoué", ReviewInfo);

    private async Task CorrectSelectionAsync()
    {
        var selected = ReviewList.SelectedItems.Cast<SetupEntity>().ToList();
        if (selected.Count == 0)
        {
            ShowWarning("Sélectionnez au moins un setup à corriger.");
            return;
        }

        var trackCatalog = await App.Services.TrackCatalog.GetAllAsync();
        var provider = CreateOptionalCombo("Fournisseur", SetupCatalog.ProviderNames);
        var category = CreateOptionalCombo("Catégorie", SetupCatalog.Categories);
        var car = CreateOptionalCombo("Voiture", SetupCatalog.Cars.Select(item => item.DisplayName));
        var track = CreateOptionalCombo("Circuit", SetupMetadataAnalyzer.KnownTrackNames.Concat(trackCatalog.Select(item => item.TrackName)));
        var season = CreateOptionalCombo("Saison", _filterOptions?.Seasons ?? [], editable: true);
        var week = CreateOptionalCombo("Week", Enumerable.Range(1, 13).Select(value => $"Week {value:00}")
            .Concat(["Week NEC", "Sans Week", "Week inconnue"]));
        var type = CreateOptionalCombo("Type de setup", _filterOptions?.SetupTypes ?? [], editable: true);
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"{selected.Count} setup(s) sélectionné(s). Seuls les champs différents de « Ne pas modifier » seront appliqués.",
            TextWrapping = TextWrapping.Wrap
        });
        foreach (var control in new Control[] { provider, category, car, track, season, week, type }) content.Children.Add(control);
        var editDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Corriger la sélection",
            Content = new ScrollViewer { Content = content, MaxHeight = 620 },
            PrimaryButtonText = "Afficher l’aperçu",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await editDialog.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;

        var providerValue = OptionalValue(provider);
        var categoryValue = OptionalValue(category);
        var carValue = OptionalValue(car);
        var trackValue = OptionalValue(track);
        var seasonValue = OptionalValue(season);
        var typeValue = OptionalValue(type);
        var weekValue = OptionalValue(week);
        var (weekNumber, weekKind) = ParseWeekChoice(weekValue);
        var changes = new List<string>();
        AddPreview(changes, "Fournisseur", providerValue);
        AddPreview(changes, "Catégorie", categoryValue);
        AddPreview(changes, "Voiture", carValue);
        AddPreview(changes, "Circuit", trackValue);
        AddPreview(changes, "Saison", seasonValue);
        AddPreview(changes, "Week", weekValue);
        AddPreview(changes, "Type", typeValue);
        if (changes.Count == 0)
        {
            ShowWarning("Aucun champ n’a été choisi : aucune modification appliquée.");
            return;
        }

        var confirmation = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Confirmer la correction groupée",
            Content = new TextBlock
            {
                Text = $"{selected.Count} setup(s) seront modifiés :\n\n{string.Join("\n", changes.Select(value => "• " + value))}\n\nUne entrée d’historique sera créée pour chaque setup.",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "Appliquer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirmation.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;

        await App.Services.SetupCorrection.CorrectManyAsync(
            selected.Select(item => item.Id).ToArray(),
            new SetupBatchCorrection(providerValue, categoryValue, carValue, trackValue, seasonValue,
                typeValue, weekNumber, weekKind));
        foreach (var setup in selected)
        {
            var refreshed = await App.Services.QueryService.GetBySha256Async(setup.Sha256);
            if (refreshed is not null) AddOrUpdateSetup(refreshed);
        }
        ShowSuccess($"{selected.Count} setup(s) corrigé(s). L’historique individuel a été conservé.");
    }

    private static ComboBox CreateOptionalCombo(string header, IEnumerable<string?> values, bool editable = false)
    {
        var items = new[] { "Ne pas modifier" }.Concat(values
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)).ToArray();
        return new ComboBox
        {
            Header = header,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = items,
            SelectedIndex = 0,
            IsEditable = editable
        };
    }

    private static string? OptionalValue(ComboBox combo)
    {
        var value = combo.IsEditable && !string.IsNullOrWhiteSpace(combo.Text) ? combo.Text : combo.SelectedItem as string;
        return string.IsNullOrWhiteSpace(value) || value.Equals("Ne pas modifier", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();
    }

    private static (int? Week, SetupWeekKind? Kind) ParseWeekChoice(string? value)
    {
        if (value is null) return (null, null);
        if (value.Equals("Week NEC", StringComparison.OrdinalIgnoreCase)) return (null, SetupWeekKind.Nec);
        if (value.Equals("Sans Week", StringComparison.OrdinalIgnoreCase)) return (null, SetupWeekKind.NoWeek);
        if (value.Equals("Week inconnue", StringComparison.OrdinalIgnoreCase)) return (null, SetupWeekKind.Unknown);
        return int.TryParse(value.Replace("Week", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(), out var number)
            ? (number, SetupWeekKind.Numeric)
            : (null, null);
    }

    private static void AddPreview(ICollection<string> changes, string label, string? value)
    {
        if (value is not null) changes.Add($"{label} → {value}");
    }

    private async Task CorrectOneAsync(object sender)
    {
        if (!TryGetSetupId(sender, out var setupId)) return;
        var setup = _visibleSetups.Single(item => item.Id == setupId);
        var provider = CreateCombo("Fournisseur", SetupCatalog.ProviderNames, setup.Provider);
        var category = CreateCombo("Catégorie", SetupCatalog.Categories, setup.Category);
        var car = CreateCombo("Voiture", SetupCatalog.Cars.Select(item => item.DisplayName), setup.Car);
        var trackCatalog = await App.Services.TrackCatalog.GetAllAsync();
        var track = CreateCombo("Circuit", SetupMetadataAnalyzer.KnownTrackNames.Concat(trackCatalog.Select(item => item.TrackName)), setup.Track);
        var configuration = new TextBox { Header = "Configuration", Text = setup.TrackConfiguration ?? string.Empty };
        var season = new TextBox { Header = "Saison", Text = setup.Season ?? string.Empty, PlaceholderText = "Exemple : 2026 S3" };
        var type = new TextBox { Header = "Type de setup", Text = setup.SetupType };
        var carAlias = new TextBox { Header = "Mémoriser une abréviation voiture (facultatif)", PlaceholderText = "Exemple : NSXE22" };
        var trackAlias = new TextBox { Header = "Mémoriser une abréviation circuit (facultatif)", PlaceholderText = "Exemple : RoadAm" };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock { Text = setup.OriginalFileName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        foreach (var control in new Control[] { provider, category, car, track, configuration, season, type, carAlias, trackAlias })
            content.Children.Add(control);
        content.Children.Add(new TextBlock
        {
            Text = "Laissez les abréviations vides pour corriger uniquement ce fichier.",
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap
        });
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Corriger l’identification",
            Content = new ScrollViewer { Content = content, MaxHeight = 620 },
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;
        await App.Services.SetupCorrection.CorrectAsync(setup.Id, new SetupCorrection(
            provider.SelectedItem as string ?? string.Empty,
            category.SelectedItem as string ?? string.Empty,
            car.SelectedItem as string ?? string.Empty,
            track.SelectedItem as string ?? string.Empty,
            configuration.Text, season.Text, type.Text, carAlias.Text, trackAlias.Text));
        var refreshed = await App.Services.QueryService.GetBySha256Async(setup.Sha256);
        if (refreshed is not null) AddOrUpdateSetup(refreshed);
        ShowSuccess("L’identification et le classement ont été corrigés.");
    }

    private static ComboBox CreateCombo(string header, IEnumerable<string> values, string selected)
    {
        var combo = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.ItemsSource = values.Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        combo.SelectedItem = combo.Items.Cast<string>()
            .FirstOrDefault(item => item.Equals(selected, StringComparison.OrdinalIgnoreCase));
        return combo;
    }

    private async void OnValidateOne(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => ValidateOneAsync(sender), "La validation a échoué", ReviewInfo);

    private async Task ValidateOneAsync(object sender)
    {
        if (!TryGetSetupId(sender, out var setupId)) return;
        await App.Services.Validation.ValidateAsync(setupId);
        ShowSuccess("Le setup a été validé.");
        RemoveSetup(setupId);
    }

    private async void OnRefuseOne(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => RefuseOneAsync(sender), "Le refus a échoué", ReviewInfo);

    private async Task RefuseOneAsync(object sender)
    {
        if (!TryGetSetupId(sender, out var setupId)) return;
        await App.Services.Validation.RefuseAsync(setupId);
        ShowSuccess("Le setup a été refusé.");
        RemoveSetup(setupId);
    }

    private async void OnValidateSelection(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => RunGroupedActionAsync(true), "La validation groupée a échoué", ReviewInfo);
    private async void OnRefuseSelection(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(() => RunGroupedActionAsync(false), "Le refus groupé a échoué", ReviewInfo);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReviewList.SelectedItems.Count == 1 && ReviewList.SelectedItem is SetupEntity setup)
        {
            RatingBox.Value = setup.PersonalRating ?? double.NaN;
            CommentBox.Text = setup.Comment ?? string.Empty;
        }
    }

    private async void OnSaveNotes(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(SaveNotesAsync, "L’enregistrement des notes a échoué", ReviewInfo);

    private async Task SaveNotesAsync()
    {
        if (ReviewList.SelectedItems.Count != 1 || ReviewList.SelectedItem is not SetupEntity setup)
        {
            ShowWarning("Sélectionnez exactement un setup pour modifier sa note et son commentaire.");
            return;
        }

        var rating = double.IsNaN(RatingBox.Value) ? null : (int?)RatingBox.Value;
        await App.Services.Validation.UpdateNotesAsync(setup.Id, rating, CommentBox.Text);
        setup.PersonalRating = rating;
        setup.Comment = CommentBox.Text;
        ShowSuccess("La note et le commentaire ont été enregistrés.");
    }

    private async Task RunGroupedActionAsync(bool validate)
    {
        var ids = ReviewList.SelectedItems.Cast<SetupEntity>().Select(item => item.Id).ToList();
        if (ids.Count == 0)
        {
            ShowWarning("Sélectionnez au moins un setup.");
            return;
        }

        var verb = validate ? "valider" : "refuser";
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Confirmer l’action groupée",
            Content = $"Voulez-vous {verb} {ids.Count} setup(s) ? Cette action sera enregistrée dans l’historique.",
            PrimaryButtonText = validate ? "Valider" : "Refuser",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;

        if (validate) await App.Services.Validation.ValidateManyAsync(ids, true);
        else await App.Services.Validation.RefuseManyAsync(ids, true);

        ShowSuccess($"{ids.Count} setup(s) ont été traités.");
        foreach (var id in ids) RemoveSetup(id);
    }

    private async Task ReloadAsync(bool refreshOptions = false)
    {
        if (refreshOptions)
        {
            _filterOptions = await App.Services.QueryService.GetFilterOptionsAsync(true);
            _suppressFilterChanges = true;
            try
            {
                ResetOptions(_providers, _filterOptions.Providers);
                ResetOptions(_categories, _filterOptions.Categories);
                ResetOptions(_cars, _filterOptions.Cars);
                ResetOptions(_tracks, _filterOptions.Tracks);
                ResetOptions(_weeks, _filterOptions.Weeks);
            }
            finally { _suppressFilterChanges = false; }
        }
        await ResetPagesAsync();
        RatingBox.Value = double.NaN;
        CommentBox.Text = string.Empty;
    }

    private void AddOrUpdateSetup(SetupEntity setup)
    {
        AddOption(_providers, setup.Provider);
        AddOption(_categories, setup.Category);
        AddOption(_cars, setup.Car);
        AddOption(_tracks, setup.Track);
        AddOption(_weeks, setup.WeekDisplay);

        var matches = MatchesCurrentFilters(setup);
        var wasVisible = _visibleSetupIds.Contains(setup.Id);
        var visible = wasVisible ? _visibleSetups.FirstOrDefault(item => item.Id == setup.Id) : null;
        if (visible is not null && matches) _visibleSetups[_visibleSetups.IndexOf(visible)] = setup;
        else if (visible is not null)
        {
            _visibleSetups.Remove(visible);
            _visibleSetupIds.Remove(setup.Id);
            _setupIds.Remove(setup.Id);
            _totalCount = Math.Max(0, _totalCount - 1);
        }
        else if (matches && _setupIds.Add(setup.Id))
        {
            _visibleSetups.Add(setup);
            _visibleSetupIds.Add(setup.Id);
            _totalCount++;
        }
        UpdateEmptyState();
    }

    private void RemoveSetup(Guid id)
    {
        _setupIds.Remove(id);
        var visible = _visibleSetups.FirstOrDefault(item => item.Id == id);
        if (visible is not null)
        {
            _visibleSetups.Remove(visible);
            _visibleSetupIds.Remove(id);
            _totalCount = Math.Max(0, _totalCount - 1);
        }
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _visibleSetups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultCountText.Text = $"{_visibleSetups.Count} affiché{(_visibleSetups.Count > 1 ? "s" : string.Empty)} sur {_totalCount}";
        var active = new List<(string Key, string Label)>();
        if (!string.IsNullOrWhiteSpace(SearchBox.Text)) active.Add(("search", $"Recherche : {SearchBox.Text.Trim()}"));
        AddActive(active, "provider", "Fournisseur", ProviderFilter.SelectedItem as string);
        AddActive(active, "category", "Catégorie", CategoryFilter.SelectedItem as string);
        AddActive(active, "car", "Voiture", CarFilter.SelectedItem as string);
        AddActive(active, "track", "Circuit", TrackFilter.SelectedItem as string);
        AddActive(active, "week", "Week", WeekFilter.SelectedItem as string);
        var identification = IdentificationFilter.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(identification) && identification != "Tous")
            active.Add(("identification", $"Identification : {identification}"));
        FilterPresentation.Rebuild(ActiveFiltersPanel, active, OnRemoveFilter);
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFilterChanges) return;
        _searchDelayCancellation?.Cancel();
        var cancellation = _searchDelayCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
                await UiOperation.RunAsync(ResetPagesAsync, "Impossible d’appliquer la recherche", ReviewInfo);
        }
        catch (OperationCanceledException) { }
    }

    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressFilterChanges)
            await UiOperation.RunAsync(ResetPagesAsync, "Impossible d’appliquer les filtres", ReviewInfo);
    }

    private async void OnClearFilters(object sender, RoutedEventArgs e)
    {
        _suppressFilterChanges = true;
        SearchBox.Text = string.Empty;
        ProviderFilter.SelectedItem = null;
        CategoryFilter.SelectedItem = null;
        CarFilter.SelectedItem = null;
        TrackFilter.SelectedItem = null;
        WeekFilter.SelectedItem = null;
        IdentificationFilter.SelectedIndex = 0;
        _suppressFilterChanges = false;
        await UiOperation.RunAsync(ResetPagesAsync, "Impossible d’effacer les filtres", ReviewInfo);
    }

    private async void OnRemoveFilter(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        _suppressFilterChanges = true;
        switch (key)
        {
            case "search": SearchBox.Text = string.Empty; break;
            case "provider": ProviderFilter.SelectedItem = null; break;
            case "category": CategoryFilter.SelectedItem = null; break;
            case "car": CarFilter.SelectedItem = null; break;
            case "track": TrackFilter.SelectedItem = null; break;
            case "week": WeekFilter.SelectedItem = null; break;
            case "identification": IdentificationFilter.SelectedIndex = 0; break;
        }
        _suppressFilterChanges = false;
        await UiOperation.RunAsync(ResetPagesAsync, "Impossible de retirer le filtre", ReviewInfo);
    }

    private async Task ResetPagesAsync()
    {
        var version = Interlocked.Increment(ref _queryVersion);
        _pageLoadCancellation?.Cancel();
        _pageLoadCancellation = new CancellationTokenSource();
        _visibleSetups.Clear();
        _visibleSetupIds.Clear();
        _setupIds.Clear();
        _totalCount = 0;
        UpdateEmptyState();
        await LoadNextPageAsync(version, _pageLoadCancellation.Token);
    }

    private async Task LoadNextPageAsync(int version, CancellationToken cancellationToken)
    {
        if (_visibleSetups.Count >= _totalCount && _totalCount > 0) return;
        try { await _pageLoadLock.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return; }
        try
        {
            if (version != _queryVersion || _visibleSetups.Count >= _totalCount && _totalCount > 0) return;
            var page = await App.Services.QueryService.GetPageAsync(CreatePageRequest(_visibleSetups.Count), cancellationToken);
            if (!_isPageActive || version != _queryVersion || cancellationToken.IsCancellationRequested) return;
            _totalCount = page.TotalCount;
            foreach (var setup in page.Items)
            {
                if (!_setupIds.Add(setup.Id)) continue;
                _visibleSetupIds.Add(setup.Id);
                _visibleSetups.Add(setup);
            }
            UpdateEmptyState();
        }
        catch (OperationCanceledException) { }
        finally { _pageLoadLock.Release(); }
    }

    private SetupPageRequest CreatePageRequest(int skip) => new(
        skip,
        PageSize,
        SearchBox.Text,
        ProviderFilter.SelectedItem as string,
        CategoryFilter.SelectedItem as string,
        CarFilter.SelectedItem as string,
        TrackFilter.SelectedItem as string,
        Week: WeekFilter.SelectedItem as string,
        Identification: IdentificationFilter.SelectedItem as string,
        ToReviewOnly: true);

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!_isPageActive || args.InRecycleQueue || args.ItemIndex < _visibleSetups.Count - 20 ||
            _visibleSetups.Count >= _totalCount || !_incrementalLoadGate.TryEnter()) return;
        var version = _queryVersion;
        var cancellationToken = _pageLoadCancellation?.Token ?? CancellationToken.None;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            try
            {
                if (!_isPageActive || version != _queryVersion || cancellationToken.IsCancellationRequested) return;
                await UiOperation.RunAsync(
                    () => LoadNextPageAsync(version, cancellationToken),
                    "Impossible de charger la suite des setups à vérifier",
                    ReviewInfo);
            }
            finally { _incrementalLoadGate.Exit(); }
        })) _incrementalLoadGate.Exit();
    }

    private bool MatchesCurrentFilters(SetupEntity setup)
    {
        var matchesIdentification = IdentificationFilter.SelectedItem switch
        {
            "À identifier" => !IsFullyIdentified(setup),
            "Identifiés" => IsFullyIdentified(setup),
            _ => true
        };
        return matchesIdentification && SetupListFilter.Matches(new SetupListItem(
                setup.OriginalFileName, setup.Provider, setup.Category, setup.Car, setup.Track,
                setup.TrackConfiguration, setup.Season, setup.SetupType, setup.StatusDisplay, setup.WeekDisplay),
            new SetupFilterCriteria(
                SearchBox.Text,
                ProviderFilter.SelectedItem as string,
                CategoryFilter.SelectedItem as string,
                CarFilter.SelectedItem as string,
                TrackFilter.SelectedItem as string,
                WeekFilter.SelectedItem as string));
    }

    private static void ResetOptions(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static void AddOption(ObservableCollection<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || target.Any(item => item.Equals(value, StringComparison.OrdinalIgnoreCase))) return;
        var index = 0;
        while (index < target.Count && StringComparer.CurrentCultureIgnoreCase.Compare(target[index], value) < 0) index++;
        target.Insert(index, value);
    }

    private static void AddActive(List<(string Key, string Label)> filters, string key, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) filters.Add((key, $"{label} : {value}"));
    }

    private static bool TryGetSetupId(object sender, out Guid setupId)
    {
        setupId = default;
        return sender is Button { Tag: Guid id } && (setupId = id) != Guid.Empty;
    }

    private void ShowSuccess(string message)
    {
        ReviewInfo.Severity = InfoBarSeverity.Success;
        ReviewInfo.Message = message;
        ReviewInfo.IsOpen = true;
    }

    private void ShowWarning(string message)
    {
        ReviewInfo.Severity = InfoBarSeverity.Warning;
        ReviewInfo.Message = message;
        ReviewInfo.IsOpen = true;
    }
}
