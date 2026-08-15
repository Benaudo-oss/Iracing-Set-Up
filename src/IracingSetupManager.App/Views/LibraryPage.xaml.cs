using System.Collections.ObjectModel;
using IracingSetupManager.Core.Setups;
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
    private readonly List<SetupEntity> _setups = [];
    private readonly HashSet<Guid> _setupIds = [];
    private readonly ObservableCollection<LibrarySetupRow> _visibleSetups = [];
    private readonly HashSet<Guid> _visibleSetupIds = [];
    private readonly ObservableCollection<string> _providers = [];
    private readonly ObservableCollection<string> _categories = [];
    private readonly ObservableCollection<string> _cars = [];
    private readonly ObservableCollection<string> _tracks = [];
    private readonly ObservableCollection<string> _statuses = [];
    private bool _isListeningForImports;

    public LibraryPage()
    {
        InitializeComponent();
        SetupList.ItemsSource = _visibleSetups;
        ProviderFilter.ItemsSource = _providers;
        CategoryFilter.ItemsSource = _categories;
        CarFilter.ItemsSource = _cars;
        TrackFilter.ItemsSource = _tracks;
        StatusFilter.ItemsSource = _statuses;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await UiOperation.RunAsync(LoadPageAsync, "Impossible de charger la bibliothèque");
    }

    private async Task LoadPageAsync()
    {
        StartListeningForImports();
        await App.Services.LibraryIntegrity.MarkMissingFilesAsync();
        await App.Services.MetadataRefresh.RefreshAsync();
        await ReloadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
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

    private async Task ReloadAsync()
    {
        _setups.Clear();
        _setups.AddRange(await App.Services.QueryService.GetAllAsync());
        _setupIds.Clear();
        foreach (var setup in _setups) _setupIds.Add(setup.Id);
        ResetOptions(_providers, Distinct(item => item.Provider));
        ResetOptions(_categories, Distinct(item => item.Category));
        ResetOptions(_cars, Distinct(item => item.Car));
        ResetOptions(_tracks, Distinct(item => item.Track));
        ResetOptions(_statuses, _setups.Select(item => item.StatusDisplay).Distinct().Order());
        ApplyFilters();
    }

    private void AddOrUpdateSetup(SetupEntity setup)
    {
        if (_setupIds.Add(setup.Id)) _setups.Add(setup);
        else
        {
            var existingIndex = _setups.FindIndex(item => item.Id == setup.Id);
            if (existingIndex >= 0) _setups[existingIndex] = setup;
        }

        AddOption(_providers, setup.Provider);
        AddOption(_categories, setup.Category);
        AddOption(_cars, setup.Car);
        AddOption(_tracks, setup.Track);
        AddOption(_statuses, setup.StatusDisplay);

        var isVisible = MatchesCurrentFilters(setup);
        var wasVisible = _visibleSetupIds.Contains(setup.Id);
        var visibleIndex = wasVisible ? IndexOf(_visibleSetups.Select(item => item.Setup), setup.Id) : -1;
        if (visibleIndex >= 0 && isVisible) _visibleSetups[visibleIndex] = CreateRow(setup, visibleIndex);
        else if (visibleIndex >= 0)
        {
            _visibleSetups.RemoveAt(visibleIndex);
            _visibleSetupIds.Remove(setup.Id);
        }
        else if (isVisible) _visibleSetups.Add(CreateRow(setup, _visibleSetups.Count));
        if (isVisible) _visibleSetupIds.Add(setup.Id);
        UpdateEmptyState();
    }

    private async void OnRemoveMissing(object sender, RoutedEventArgs e)
    {
        await UiOperation.RunAsync(RemoveMissingAsync, "Impossible de retirer les fichiers manquants");
    }

    private async Task RemoveMissingAsync()
    {
        var selected = _setups.Where(item => item.Status == SetupStatus.FichierManquant).Select(item => item.Id).ToArray();
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
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await App.Services.LibraryIntegrity.RemoveMissingEntriesAsync(selected);
        foreach (var id in selected)
        {
            _setups.RemoveAll(item => item.Id == id);
            _setupIds.Remove(id);
            var index = IndexOf(_visibleSetups.Select(item => item.Setup), id);
            if (index >= 0)
            {
                _visibleSetups.RemoveAt(index);
                _visibleSetupIds.Remove(id);
            }
        }
        RefreshRowAppearance();
        UpdateEmptyState();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void OnClearFilters(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        ProviderFilter.SelectedItem = null;
        CategoryFilter.SelectedItem = null;
        CarFilter.SelectedItem = null;
        TrackFilter.SelectedItem = null;
        StatusFilter.SelectedItem = null;
        ApplyFilters();
    }

    private void OnRemoveFilter(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        switch (key)
        {
            case "search": SearchBox.Text = string.Empty; break;
            case "provider": ProviderFilter.SelectedItem = null; break;
            case "category": CategoryFilter.SelectedItem = null; break;
            case "car": CarFilter.SelectedItem = null; break;
            case "track": TrackFilter.SelectedItem = null; break;
            case "status": StatusFilter.SelectedItem = null; break;
        }
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _visibleSetups.Clear();
        _visibleSetupIds.Clear();
        foreach (var setup in _setups.Where(MatchesCurrentFilters))
        {
            _visibleSetups.Add(CreateRow(setup, _visibleSetups.Count));
            _visibleSetupIds.Add(setup.Id);
        }
        UpdateEmptyState();
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row) row.Background = Brush("#FF2B333B");
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border { DataContext: LibrarySetupRow item } row) row.Background = item.RowBackground;
    }

    private void RefreshRowAppearance()
    {
        for (var index = 0; index < _visibleSetups.Count; index++)
            _visibleSetups[index] = CreateRow(_visibleSetups[index].Setup, index);
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

    private static SolidColorBrush Brush(string value) => new(Color.FromArgb(
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
            StatusFilter.SelectedItem as string));
    }

    private IReadOnlyList<string> Distinct(Func<SetupEntity, string?> selector) =>
        _setups.Select(selector).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();

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
        ResultCountText.Text = $"{_visibleSetups.Count} résultat{(_visibleSetups.Count > 1 ? "s" : string.Empty)} sur {_setups.Count}";
        var active = new List<(string Key, string Label)>();
        if (!string.IsNullOrWhiteSpace(SearchBox.Text)) active.Add(("search", $"Recherche : {SearchBox.Text.Trim()}"));
        AddActive(active, "provider", "Fournisseur", ProviderFilter.SelectedItem as string);
        AddActive(active, "category", "Catégorie", CategoryFilter.SelectedItem as string);
        AddActive(active, "car", "Voiture", CarFilter.SelectedItem as string);
        AddActive(active, "track", "Circuit", TrackFilter.SelectedItem as string);
        AddActive(active, "status", "Statut", StatusFilter.SelectedItem as string);
        FilterPresentation.Rebuild(ActiveFiltersPanel, active, OnRemoveFilter);
    }

    private static void AddActive(List<(string Key, string Label)> filters, string key, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) filters.Add((key, $"{label} : {value}"));
    }

    private static SetupListItem ToListItem(SetupEntity item) => new(
        item.OriginalFileName, item.Provider, item.Category, item.Car, item.Track,
        item.TrackConfiguration, item.Season, item.SetupType, item.StatusDisplay);
}

public sealed record LibrarySetupRow(
    SetupEntity Setup,
    SolidColorBrush RowBackground,
    SolidColorBrush StatusBackground,
    SolidColorBrush StatusBorder,
    SolidColorBrush StatusForeground,
    string StatusGlyph);
