using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class ReviewPage : Page
{
    public ReviewPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e) => await ReloadAsync();

    private void OnSelectAll(object sender, RoutedEventArgs e) => ReviewList.SelectAll();

    private async void OnValidateOne(object sender, RoutedEventArgs e)
    {
        if (TryGetSetupId(sender, out var setupId))
        {
            await App.Services.Validation.ValidateAsync(setupId);
            ShowSuccess("Le setup a été validé.");
            await ReloadAsync();
        }
    }

    private async void OnRefuseOne(object sender, RoutedEventArgs e)
    {
        if (TryGetSetupId(sender, out var setupId))
        {
            await App.Services.Validation.RefuseAsync(setupId);
            ShowSuccess("Le setup a été refusé.");
            await ReloadAsync();
        }
    }

    private async void OnValidateSelection(object sender, RoutedEventArgs e) =>
        await RunGroupedActionAsync(validate: true);

    private async void OnRefuseSelection(object sender, RoutedEventArgs e) =>
        await RunGroupedActionAsync(validate: false);

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
        ShowSuccess("La note et le commentaire ont été enregistrés.");
        await ReloadAsync();
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
            Title = $"Confirmer l’action groupée",
            Content = $"Voulez-vous {verb} {ids.Count} setup(s) ? Cette action sera enregistrée dans l’historique.",
            PrimaryButtonText = validate ? "Valider" : "Refuser",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (validate)
        {
            await App.Services.Validation.ValidateManyAsync(ids, confirmed: true);
        }
        else
        {
            await App.Services.Validation.RefuseManyAsync(ids, confirmed: true);
        }

        ShowSuccess($"{ids.Count} setup(s) ont été traités.");
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var setups = await App.Services.QueryService.GetToReviewAsync();
        ReviewList.ItemsSource = setups;
        EmptyState.Visibility = setups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RatingBox.Value = double.NaN;
        CommentBox.Text = string.Empty;
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
