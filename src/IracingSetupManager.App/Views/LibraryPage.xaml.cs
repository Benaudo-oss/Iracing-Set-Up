using System.Collections.ObjectModel;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.App.Services;
using IracingSetupManager.Core.Presentation;

namespace IracingSetupManager.App.Views;

public sealed partial class LibraryPage : Page
{
    private readonly List<SetupEntity> _setups = [];
    private readonly ObservableCollection<SetupEntity> _visibleSetups = [];
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
        ResetOptions(_providers, Distinct(item => item.Provider));
        ResetOptions(_categories, Distinct(item => item.Category));
        ResetOptions(_cars, Distinct(item => item.Car));
        ResetOptions(_tracks, Distinct(item => item.Track));
        ResetOptions(_statuses, _setups.Select(item => item.StatusDisplay).Distinct().Order());
        ApplyFilters();
    }

    private void AddOrUpdateSetup(SetupEntity setup)
    {
        var existingIndex = _setups.FindIndex(item => item.Id == setup.Id);
        if (existingIndex >= 0) _setups[existingIndex] = setup;
        else _setups.Insert(0, setup);

        AddOption(_providers, setup.Provider);
        AddOption(_categories, setup.Category);
        AddOption(_cars, setup.Car);
        AddOption(_tracks, setup.Track);
        AddOption(_statuses, setup.StatusDisplay);

        var visibleIndex = IndexOf(_visibleSetups, setup.Id);
        var isVisible = MatchesCurrentFilters(setup);
        if (visibleIndex >= 0 && isVisible) _visibleSetups[visibleIndex] = setup;
        else if (visibleIndex >= 0) _visibleSetups.RemoveAt(visibleIndex);
        else if (isVisible) _visibleSetups.Insert(0, setup);
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
            var index = IndexOf(_visibleSetups, id);
            if (index >= 0) _visibleSetups.RemoveAt(index);
        }
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

    private void ApplyFilters()
    {
        _visibleSetups.Clear();
        foreach (var setup in _setups.Where(MatchesCurrentFilters)) _visibleSetups.Add(setup);
        UpdateEmptyState();
    }

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

    private void UpdateEmptyState() =>
        EmptyState.Visibility = _visibleSetups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private static SetupListItem ToListItem(SetupEntity item) => new(
        item.OriginalFileName, item.Provider, item.Category, item.Car, item.Track,
        item.TrackConfiguration, item.Season, item.SetupType, item.StatusDisplay);
}
