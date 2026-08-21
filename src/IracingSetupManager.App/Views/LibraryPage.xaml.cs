using System.Collections.ObjectModel;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using IracingSetupManager.App.Services;
using IracingSetupManager.Core.Presentation;
using Windows.UI;

namespace IracingSetupManager.App.Views;

public sealed partial class LibraryPage : Page
{
    private const int PageSize = 100;
    private readonly HashSet<Guid> _setupIds = [];
    private readonly ObservableCollection<LibrarySetupRow> _visibleSetups = [];
    private readonly HashSet<Guid> _visibleSetupIds = [];
    private readonly ObservableCollection<string> _providers = [];
    private readonly ObservableCollection<string> _categories = [];
    private readonly ObservableCollection<string> _cars = [];
    private readonly ObservableCollection<string> _tracks = [];
    private readonly ObservableCollection<string> _weeks = [];
    private readonly ObservableCollection<string> _statuses = [];
    private bool _isListeningForImports;
    private readonly SemaphoreSlim _pageLoadLock = new(1, 1);
    private bool _suppressFilterChanges;
    private int _totalCount;
    private int _queryVersion;
    private CancellationTokenSource? _searchDelayCancellation;
    private CancellationTokenSource? _pageLoadCancellation;

    public LibraryPage()
    {
        InitializeComponent();
        SetupList.ItemsSource = _visibleSetups;
        ProviderFilter.ItemsSource = _providers;
        CategoryFilter.ItemsSource = _categories;
        CarFilter.ItemsSource = _cars;
        TrackFilter.ItemsSource = _tracks;
        WeekFilter.ItemsSource = _weeks;
        StatusFilter.ItemsSource = _statuses;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await UiOperation.RunAsync(LoadPageAsync, "Impossible de charger la bibliothèque");
    }

    private async Task LoadPageAsync()
    {
        StartListeningForImports();
        await App.Services.LibraryIntegrity.MarkMissingFilesIfDueAsync(TimeSpan.FromMinutes(10));
        await ReloadAsync(true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
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
                if (setup is not null) AddOrUpdateSetup(setup);
            }, "Impossible d’actualiser la bibliothèque après l’import");
        });
    }

    private async Task ReloadAsync(bool refreshOptions = false)
    {
        if (refreshOptions)
        {
            var options = await App.Services.QueryService.GetFilterOptionsAsync();
            _suppressFilterChanges = true;
            try
            {
                ResetOptions(_providers, options.Providers);
                ResetOptions(_categories, options.Categories);
                ResetOptions(_cars, options.Cars);
                ResetOptions(_tracks, options.Tracks);
                ResetOptions(_weeks, options.Weeks);
                ResetOptions(_statuses, options.Statuses);
            }
            finally { _suppressFilterChanges = false; }
        }
        await ResetPagesAsync();
    }

    private void AddOrUpdateSetup(SetupEntity setup)
    {
        AddOption(_providers, setup.Provider);
        AddOption(_categories, setup.Category);
        AddOption(_cars, setup.Car);
        AddOption(_tracks, setup.Track);
        AddOption(_weeks, setup.WeekDisplay);
        AddOption(_statuses, setup.StatusDisplay);

        var isVisible = MatchesCurrentFilters(setup);
        var visibleIndex = IndexOf(_visibleSetups.Select(item => item.Setup), setup.Id);
        if (visibleIndex >= 0 && isVisible) _visibleSetups[visibleIndex] = CreateRow(setup, visibleIndex);
        else if (visibleIndex >= 0)
        {
            _visibleSetups.RemoveAt(visibleIndex);
            _visibleSetupIds.Remove(setup.Id);
            _setupIds.Remove(setup.Id);
            _totalCount = Math.Max(0, _totalCount - 1);
        }
        else if (isVisible && _setupIds.Add(setup.Id))
        {
            _visibleSetups.Add(CreateRow(setup, _visibleSetups.Count));
            _visibleSetupIds.Add(setup.Id);
            _totalCount++;
        }
        UpdateEmptyState();
    }

    private async void OnRemoveMissing(object sender, RoutedEventArgs e)
    {
        await UiOperation.RunAsync(RemoveMissingAsync, "Impossible de retirer les fichiers manquants");
    }

    private async Task RemoveMissingAsync()
    {
        var selected = (await App.Services.QueryService.GetIdsByStatusAsync(SetupStatus.FichierManquant)).ToArray();
        if (selected.Length == 0) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Retirer de la bibliothèque ?",
            Content = $"{selected.Length} entrée(s) seront retirées. Aucun fichier ne sera supprimé.",
            PrimaryButtonText = "Retirer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;
        await App.Services.LibraryIntegrity.RemoveMissingEntriesAsync(selected);
        foreach (var id in selected)
        {
            _setupIds.Remove(id);
            var index = IndexOf(_visibleSetups.Select(item => item.Setup), id);
            if (index >= 0)
            {
                _visibleSetups.RemoveAt(index);
                _visibleSetupIds.Remove(id);
            }
        }
        _totalCount = Math.Max(0, _totalCount - selected.Length);
        UpdateEmptyState();
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
                await UiOperation.RunAsync(ResetPagesAsync, "Impossible d’appliquer la recherche");
        }
        catch (OperationCanceledException) { }
    }

    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressFilterChanges)
            await UiOperation.RunAsync(ResetPagesAsync, "Impossible d’appliquer les filtres");
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
        StatusFilter.SelectedItem = null;
        _suppressFilterChanges = false;
        await UiOperation.RunAsync(ResetPagesAsync, "Impossible d’effacer les filtres");
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
            case "status": StatusFilter.SelectedItem = null; break;
        }
        _suppressFilterChanges = false;
        await UiOperation.RunAsync(ResetPagesAsync, "Impossible de retirer le filtre");
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
            if (version != _queryVersion || cancellationToken.IsCancellationRequested) return;
            _totalCount = page.TotalCount;
            foreach (var setup in page.Items)
            {
                if (!_setupIds.Add(setup.Id)) continue;
                _visibleSetupIds.Add(setup.Id);
                _visibleSetups.Add(CreateRow(setup, _visibleSetups.Count));
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
        Status: StatusFilter.SelectedItem as string);

    private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.ItemIndex < _visibleSetups.Count - 20 || _visibleSetups.Count >= _totalCount) return;
        var cancellationToken = _pageLoadCancellation?.Token ?? CancellationToken.None;
        await UiOperation.RunAsync(
            () => LoadNextPageAsync(_queryVersion, cancellationToken),
            "Impossible de charger la suite de la bibliothèque");
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row) row.Background = Brush("#FF2B333B");
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border { DataContext: LibrarySetupRow item } row) row.Background = item.RowBackground;
    }

    private static LibrarySetupRow CreateRow(SetupEntity setup, int index)
    {
        var (background, border, foreground, glyph) = setup.Status switch
        {
            SetupStatus.Valide => ("#FF173C26", "#FF28643B", "#FFA9E8B3", "\uE73E"),
            SetupStatus.AVerifier => ("#FF493313", "#FF78541C", "#FFFFD18A", "\uE823"),
            SetupStatus.FichierManquant => ("#FF4B2222", "#FF773331", "#FFFFAAA5", "\uE783"),
            _ => ("#FF183852", "#FF285D84", "#FFA9D8FF", "\uE8B0")
        };
        return new LibrarySetupRow(
            setup,
            Brush(index % 2 == 0 ? "#FF1F2227" : "#FF23262C"),
            Brush(background), Brush(border), Brush(foreground), glyph);
    }

    internal static SolidColorBrush Brush(string value) => new(Color.FromArgb(
        Convert.ToByte(value.Substring(1, 2), 16),
        Convert.ToByte(value.Substring(3, 2), 16),
        Convert.ToByte(value.Substring(5, 2), 16),
        Convert.ToByte(value.Substring(7, 2), 16)));

    private bool MatchesCurrentFilters(SetupEntity item)
    {
        return SetupListFilter.Matches(ToListItem(item), new SetupFilterCriteria(
            SearchBox.Text,
            ProviderFilter.SelectedItem as string,
            CategoryFilter.SelectedItem as string,
            CarFilter.SelectedItem as string,
            TrackFilter.SelectedItem as string,
            WeekFilter.SelectedItem as string,
            StatusFilter.SelectedItem as string));
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

    private static int IndexOf(IEnumerable<SetupEntity> setups, Guid id)
    {
        var index = 0;
        foreach (var setup in setups)
        {
            if (setup.Id == id) return index;
            index++;
        }
        return -1;
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
        AddActive(active, "status", "Statut", StatusFilter.SelectedItem as string);
        FilterPresentation.Rebuild(ActiveFiltersPanel, active, OnRemoveFilter);
    }

    private static void AddActive(List<(string Key, string Label)> filters, string key, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) filters.Add((key, $"{label} : {value}"));
    }

    private static SetupListItem ToListItem(SetupEntity item) => new(
        item.OriginalFileName, item.Provider, item.Category, item.Car, item.Track,
        item.TrackConfiguration, item.Season, item.SetupType, item.StatusDisplay, item.WeekDisplay);
}

public sealed record LibrarySetupRow(
    SetupEntity Setup,
    SolidColorBrush RowBackground,
    SolidColorBrush StatusBackground,
    SolidColorBrush StatusBorder,
    SolidColorBrush StatusForeground,
    string StatusGlyph)
{
    public SolidColorBrush WeekBackground => BrushFor(Setup.WeekKind, "background");
    public SolidColorBrush WeekBorder => BrushFor(Setup.WeekKind, "border");
    public SolidColorBrush WeekForeground => BrushFor(Setup.WeekKind, "foreground");

    private SolidColorBrush BrushFor(SetupWeekKind kind, string part)
    {
        var colors = SetupWeekPresentation.EffectiveKind(Setup.Week, kind) switch
        {
            SetupWeekKind.Numeric => ("#FF173852", "#FF285D84", "#FFA9D8FF"),
            SetupWeekKind.Nec => ("#FF34294E", "#FF544377", "#FFD5C4FF"),
            SetupWeekKind.NoWeek => ("#FF303238", "#FF4A4E56", "#FFC4C7CC"),
            _ => ("#FF493313", "#FF78541C", "#FFFFD18A")
        };
        return LibraryPage.Brush(part == "background" ? colors.Item1 : part == "border" ? colors.Item2 : colors.Item3);
    }
}
