using System.Collections.ObjectModel;
using IracingSetupManager.Infrastructure.Database.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Resilience;

namespace IracingSetupManager.App.Views;

public sealed partial class HistoryPage : Page
{
    private const int PageSize = 100;
    private readonly ObservableCollection<SetupChangeHistoryEntity> _history = [];
    private readonly SemaphoreSlim _pageLoadLock = new(1, 1);
    private readonly SingleFlightGate _incrementalLoadGate = new();
    private bool _isPageActive;
    private int _totalCount;
    private int _queryVersion;
    private CancellationTokenSource? _searchDelayCancellation;
    private CancellationTokenSource? _pageLoadCancellation;

    public HistoryPage()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _history;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = true;
        await UiOperation.RunAsync(LoadHistoryAsync, "Impossible de charger l’historique", HistoryInfo);
    }

    private async Task LoadHistoryAsync()
    {
        await ResetPagesAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;
        Interlocked.Increment(ref _queryVersion);
        _incrementalLoadGate.Exit();
        _searchDelayCancellation?.Cancel();
        _pageLoadCancellation?.Cancel();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchDelayCancellation?.Cancel();
        var cancellation = _searchDelayCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
                await UiOperation.RunAsync(ResetPagesAsync, "Impossible d’appliquer la recherche", HistoryInfo);
        }
        catch (OperationCanceledException) { }
    }

    private async void OnClearHistoryClick(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(ClearHistoryAsync, "Impossible d’effacer l’historique", HistoryInfo);

    private async Task ClearHistoryAsync()
    {
        var confirmation = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Effacer l’historique ?",
            Content = "Seul l’historique enregistré par l’application sera supprimé. Vos setups et vos fichiers seront conservés.",
            PrimaryButtonText = "Effacer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirmation.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;

        await App.Services.QueryService.ClearHistoryAsync();
        _history.Clear();
        _totalCount = 0;
        UpdateEmptyState();
    }

    private async Task ResetPagesAsync()
    {
        var version = Interlocked.Increment(ref _queryVersion);
        _pageLoadCancellation?.Cancel();
        _pageLoadCancellation = new CancellationTokenSource();
        _history.Clear();
        _totalCount = 0;
        UpdateEmptyState();
        await LoadNextPageAsync(version, _pageLoadCancellation.Token);
    }

    private async Task LoadNextPageAsync(int version, CancellationToken cancellationToken)
    {
        if (_history.Count >= _totalCount && _totalCount > 0) return;
        try { await _pageLoadLock.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return; }
        try
        {
            if (version != _queryVersion || _history.Count >= _totalCount && _totalCount > 0) return;
            var page = await App.Services.QueryService.GetHistoryPageAsync(
                _history.Count,
                PageSize,
                HistorySearch.Text,
                cancellationToken);
            if (!_isPageActive || version != _queryVersion || cancellationToken.IsCancellationRequested) return;
            _totalCount = page.TotalCount;
            foreach (var item in page.Items) _history.Add(item);
            UpdateEmptyState();
        }
        catch (OperationCanceledException) { }
        finally { _pageLoadLock.Release(); }
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!_isPageActive || args.InRecycleQueue || args.ItemIndex < _history.Count - 20 ||
            _history.Count >= _totalCount || !_incrementalLoadGate.TryEnter()) return;
        var version = _queryVersion;
        var cancellationToken = _pageLoadCancellation?.Token ?? CancellationToken.None;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            try
            {
                if (!_isPageActive || version != _queryVersion || cancellationToken.IsCancellationRequested) return;
                await UiOperation.RunAsync(
                    () => LoadNextPageAsync(version, cancellationToken),
                    "Impossible de charger la suite de l’historique",
                    HistoryInfo);
            }
            finally { _incrementalLoadGate.Exit(); }
        })) _incrementalLoadGate.Exit();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
