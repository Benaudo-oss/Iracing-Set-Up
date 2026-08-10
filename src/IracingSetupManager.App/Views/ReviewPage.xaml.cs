using System.Collections.ObjectModel;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class ReviewPage : Page
{
    private readonly ObservableCollection<SetupEntity> _setups = [];
    private bool _isListeningForImports;

    public ReviewPage()
    {
        InitializeComponent();
        ReviewList.ItemsSource = _setups;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
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
            if (!IsLoaded) return;
            var setup = await App.Services.QueryService.GetBySha256Async(result.Sha256);
            if (setup is not null && setup.Status == SetupStatus.AVerifier) AddOrUpdateSetup(setup);
        });
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => ReviewList.SelectAll();

    private async void OnValidateOne(object sender, RoutedEventArgs e)
    {
        if (!TryGetSetupId(sender, out var setupId)) return;
        await App.Services.Validation.ValidateAsync(setupId);
        ShowSuccess("Le setup a été validé.");
        RemoveSetup(setupId);
    }

    private async void OnRefuseOne(object sender, RoutedEventArgs e)
    {
        if (!TryGetSetupId(sender, out var setupId)) return;
        await App.Services.Validation.RefuseAsync(setupId);
        ShowSuccess("Le setup a été refusé.");
        RemoveSetup(setupId);
    }

    private async void OnValidateSelection(object sender, RoutedEventArgs e) => await RunGroupedActionAsync(true);
    private async void OnRefuseSelection(object sender, RoutedEventArgs e) => await RunGroupedActionAsync(false);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReviewList.SelectedItems.Count == 1 && ReviewList.SelectedItem is SetupEntity setup)
        {
            RatingBox.Value = setup.PersonalRating ?? double.NaN;
            CommentBox.Text = setup.Comment ?? string.Empty;
        }
    }

    private async void OnSaveNotes(object sender, RoutedEventArgs e)
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
        foreach (var setup in await App.Services.QueryService.GetToReviewAsync()) _setups.Add(setup);
        UpdateEmptyState();
        RatingBox.Value = double.NaN;
        CommentBox.Text = string.Empty;
    }

    private void AddOrUpdateSetup(SetupEntity setup)
    {
        var existing = _setups.FirstOrDefault(item => item.Id == setup.Id);
        if (existing is not null) _setups[_setups.IndexOf(existing)] = setup;
        else _setups.Insert(0, setup);
        UpdateEmptyState();
    }

    private void RemoveSetup(Guid id)
    {
        var setup = _setups.FirstOrDefault(item => item.Id == id);
        if (setup is not null) _setups.Remove(setup);
        UpdateEmptyState();
    }

    private void UpdateEmptyState() =>
        EmptyState.Visibility = _setups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
