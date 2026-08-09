using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class LibraryPage : Page
{
    private IReadOnlyList<SetupEntity> _setups = [];

    public LibraryPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await App.Services.MetadataRefresh.RefreshAsync();
        _setups = await App.Services.QueryService.GetAllAsync();
        ProviderFilter.ItemsSource = Distinct(item => item.Provider);
        CategoryFilter.ItemsSource = Distinct(item => item.Category);
        SeasonFilter.ItemsSource = Distinct(item => item.Season);
        TypeFilter.ItemsSource = Distinct(item => item.SetupType);
        StatusFilter.ItemsSource = _setups.Select(item => item.Status.ToString()).Distinct().Order().ToList();
        ApplyFilters();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void OnClearFilters(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        ProviderFilter.SelectedItem = null;
        CategoryFilter.SelectedItem = null;
        SeasonFilter.SelectedItem = null;
        TypeFilter.SelectedItem = null;
        StatusFilter.SelectedItem = null;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var search = SearchBox.Text.Trim();
        var filtered = _setups.Where(item =>
            MatchesSearch(item, search) &&
            Matches(item.Provider, ProviderFilter.SelectedItem) &&
            Matches(item.Category, CategoryFilter.SelectedItem) &&
            Matches(item.Season, SeasonFilter.SelectedItem) &&
            Matches(item.SetupType, TypeFilter.SelectedItem) &&
            Matches(item.Status.ToString(), StatusFilter.SelectedItem)).ToList();

        SetupList.ItemsSource = filtered;
        EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private IReadOnlyList<string> Distinct(Func<SetupEntity, string?> selector) =>
        _setups.Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static bool Matches(string? value, object? selection) =>
        selection is not string selected || string.Equals(value, selected, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSearch(SetupEntity item, string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Contains(item.OriginalFileName, search) ||
        Contains(item.Provider, search) ||
        Contains(item.Car, search) ||
        Contains(item.Track, search) ||
        Contains(item.TrackConfiguration, search) ||
        Contains(item.Season, search) ||
        Contains(item.SetupType, search);

    private static bool Contains(string? value, string search) =>
        value?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true;
}
