using System.Collections.ObjectModel;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.App.Services;
using IracingSetupManager.Core.Presentation;

namespace IracingSetupManager.App.Views;

public sealed partial class ReviewPage : Page
{
    private readonly List<SetupEntity> _setups = [];
    private readonly ObservableCollection<SetupEntity> _visibleSetups = [];
    private readonly ObservableCollection<string> _providers = [];
    private readonly ObservableCollection<string> _categories = [];
    private readonly ObservableCollection<string> _cars = [];
    private readonly ObservableCollection<string> _tracks = [];
    private bool _isListeningForImports;

    public ReviewPage()
    {
        InitializeComponent();
        ReviewList.ItemsSource = _visibleSetups;
        ProviderFilter.ItemsSource = _providers;
        CategoryFilter.ItemsSource = _categories;
        CarFilter.ItemsSource = _cars;
        TrackFilter.ItemsSource = _tracks;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadPageAsync, "Impossible de charger les setups à vérifier", ReviewInfo);

    private async Task LoadPageAsync()
    {
        StartListeningForImports();
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
                if (setup is not null && setup.Status == SetupStatus.AVerifier) AddOrUpdateSetup(setup);
            }, "Impossible d’actualiser les setups à vérifier", ReviewInfo);
        });
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => ReviewList.SelectAll();

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
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        if (validate) await App.Services.Validation.ValidateManyAsync(ids, true);
        else await App.Services.Validation.RefuseManyAsync(ids, true);

        ShowSuccess($"{ids.Count} setup(s) ont été traités.");
        foreach (var id in ids) RemoveSetup(id);
    }

    private async Task ReloadAsync()
    {
        _setups.Clear();
        _setups.AddRange(await App.Services.QueryService.GetToReviewAsync());
        ResetOptions(_providers, Distinct(item => item.Provider));
        ResetOptions(_categories, Distinct(item => item.Category));
        ResetOptions(_cars, Distinct(item => item.Car));
        ResetOptions(_tracks, Distinct(item => item.Track));
        ApplyFilters();
        UpdateEmptyState();
        RatingBox.Value = double.NaN;
        CommentBox.Text = string.Empty;
    }

    private void AddOrUpdateSetup(SetupEntity setup)
    {
        var existing = _setups.FirstOrDefault(item => item.Id == setup.Id);
        if (existing is not null) _setups[_setups.IndexOf(existing)] = setup;
        else _setups.Insert(0, setup);

        AddOption(_providers, setup.Provider);
        AddOption(_categories, setup.Category);
        AddOption(_cars, setup.Car);
        AddOption(_tracks, setup.Track);

        var visible = _visibleSetups.FirstOrDefault(item => item.Id == setup.Id);
        var matches = MatchesCurrentFilters(setup);
        if (visible is not null && matches) _visibleSetups[_visibleSetups.IndexOf(visible)] = setup;
        else if (visible is not null) _visibleSetups.Remove(visible);
        else if (matches) _visibleSetups.Insert(0, setup);
        UpdateEmptyState();
    }

    private void RemoveSetup(Guid id)
    {
        var setup = _setups.FirstOrDefault(item => item.Id == id);
        if (setup is not null) _setups.Remove(setup);
        var visible = _visibleSetups.FirstOrDefault(item => item.Id == id);
        if (visible is not null) _visibleSetups.Remove(visible);
        UpdateEmptyState();
    }

    private void UpdateEmptyState() =>
        EmptyState.Visibility = _visibleSetups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void OnClearFilters(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        ProviderFilter.SelectedItem = null;
        CategoryFilter.SelectedItem = null;
        CarFilter.SelectedItem = null;
        TrackFilter.SelectedItem = null;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _visibleSetups.Clear();
        foreach (var setup in _setups.Where(MatchesCurrentFilters)) _visibleSetups.Add(setup);
        UpdateEmptyState();
    }

    private bool MatchesCurrentFilters(SetupEntity setup)
    {
        return SetupListFilter.Matches(new SetupListItem(
                setup.OriginalFileName, setup.Provider, setup.Category, setup.Car, setup.Track,
                setup.TrackConfiguration, setup.Season, setup.SetupType, setup.StatusDisplay),
            new SetupFilterCriteria(
                SearchBox.Text,
                ProviderFilter.SelectedItem as string,
                CategoryFilter.SelectedItem as string,
                CarFilter.SelectedItem as string,
                TrackFilter.SelectedItem as string));
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
