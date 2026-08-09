using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Core.Setups;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class LibraryPage : Page
{
    private IReadOnlyList<SetupEntity> _setups = [];

    public LibraryPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await App.Services.LibraryIntegrity.MarkMissingFilesAsync();
        await App.Services.MetadataRefresh.RefreshAsync();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _setups = await App.Services.QueryService.GetAllAsync();
        ProviderFilter.ItemsSource = Distinct(item => item.Provider);
        CategoryFilter.ItemsSource = Distinct(item => item.Category);
        CarFilter.ItemsSource = Distinct(item => item.Car);
        TrackFilter.ItemsSource = Distinct(item => item.Track);
        StatusFilter.ItemsSource = _setups.Select(item => item.StatusDisplay).Distinct().Order().ToList();
        ApplyFilters();
    }

    private async void OnRemoveMissing(object sender, RoutedEventArgs e)
    {
        var selected = _setups
            .Where(item => item.Status == SetupStatus.FichierManquant)
            .Select(item => item.Id)
            .ToArray();
        if (selected.Length == 0)
        {
            return;
        }

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
        await ReloadAsync();
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
        var search = SearchBox.Text.Trim();
        var filtered = _setups.Where(item =>
            MatchesSearch(item, search) &&
            Matches(item.Provider, ProviderFilter.SelectedItem) &&
            Matches(item.Category, CategoryFilter.SelectedItem) &&
            Matches(item.Car, CarFilter.SelectedItem) &&
            Matches(item.Track, TrackFilter.SelectedItem) &&
            Matches(item.StatusDisplay, StatusFilter.SelectedItem)).ToList();

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
