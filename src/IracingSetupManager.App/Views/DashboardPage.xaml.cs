using IracingSetupManager.App.Services;
using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Core.Setups;
using IracingSetupManager.Infrastructure.Database;
using IracingSetupManager.Infrastructure.Files.Monitoring;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace IracingSetupManager.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? refreshTimer;
    private volatile bool dashboardRefreshPending;
    private bool activitySubscribed;

    public DashboardPage() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!activitySubscribed)
        {
            App.Services.SynchronizationActivity.Changed += OnSynchronizationActivityChanged;
            activitySubscribed = true;
        }

        if (refreshTimer is null)
        {
            refreshTimer = DispatcherQueue.CreateTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(1);
            refreshTimer.IsRepeating = true;
            refreshTimer.Tick += OnRefreshTimerTick;
        }
        refreshTimer.Start();

        await UiOperation.RunAsync(LoadDashboardAsync, "Impossible d’actualiser le tableau de bord", DashboardInfo);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!activitySubscribed) return;
        App.Services.SynchronizationActivity.Changed -= OnSynchronizationActivityChanged;
        activitySubscribed = false;
        dashboardRefreshPending = false;
        refreshTimer?.Stop();
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e) =>
        await UiOperation.RunAsync(LoadDashboardAsync, "Impossible d’actualiser le tableau de bord", DashboardInfo);

    private void OnSynchronizationActivityChanged(object? sender, SynchronizationProgress progress) =>
        dashboardRefreshPending = true;

    private async void OnRefreshTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if (!dashboardRefreshPending) return;
        dashboardRefreshPending = false;
        await UiOperation.RunAsync(LoadDashboardAsync, "Impossible d’actualiser le tableau de bord", DashboardInfo);
    }

    private async Task LoadDashboardAsync()
    {
        if (!await refreshLock.WaitAsync(0)) return;
        try
        {
            await App.Services.LibraryIntegrity.MarkMissingFilesAsync();
            var statisticsTask = App.Services.QueryService.GetDashboardStatisticsAsync();
            var breakdownTask = App.Services.QueryService.GetDashboardBreakdownAsync();
            var historyTask = App.Services.QueryService.GetHistoryAsync();
            await Task.WhenAll(statisticsTask, breakdownTask, historyTask);

            var statistics = await statisticsTask;
            var breakdown = await breakdownTask;
            var recentActivity = (await historyTask).Take(8)
                .Select(item => new RecentActivityItem(
                    item.ChangeType,
                    item.OriginalFileName,
                    FormatActivityDate(item.ChangedAtUtc)))
                .ToList();

            TotalText.Text = statistics.Total.ToString();
            ReviewText.Text = statistics.ToReview.ToString();
            ValidatedText.Text = statistics.Validated.ToString();
            IracingTeamText.Text = statistics.CopiedToIracingTeam.ToString();
            TotalContextText.Text = statistics.Total == 0
                ? "Bibliothèque vide"
                : $"{breakdown.Providers.Count} fournisseur{Plural(breakdown.Providers.Count)} actif{Plural(breakdown.Providers.Count)}";
            ReviewContextText.Text = statistics.ToReview == 0 ? "Aucune action nécessaire" : "Action nécessaire";
            var validatedPercentage = statistics.Total == 0 ? 0 : (int)Math.Round(statistics.Validated * 100d / statistics.Total);
            ValidatedContextText.Text = $"{validatedPercentage} % de la bibliothèque";
            ProvidersText.Text = $"{statistics.ProviderCount} / {SetupCatalog.Providers.Count} fournisseurs";
            LastSyncText.Text = statistics.LastDownloadUtc is null
                ? "Aucun import"
                : $"Dernier import {statistics.LastDownloadUtc.Value.ToLocalTime():g}";

            RenderProviderBars(breakdown.Providers);
            RenderStatusDonut(breakdown.Statuses);

            RecentActivityList.ItemsSource = recentActivity;
            RecentActivityList.Visibility = recentActivity.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NoRecentActivityPanel.Visibility = recentActivity.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private void RenderProviderBars(IReadOnlyList<DashboardCount> providers)
    {
        ProviderBarsPanel.Children.Clear();
        NoProviderDataText.Visibility = providers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (providers.Count == 0) return;

        var maximum = providers.Max(item => item.Count);
        foreach (var provider in providers)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            var label = new TextBlock
            {
                Text = provider.Label,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var track = new Grid
            {
                Height = 12,
                Background = new SolidColorBrush(Color.FromArgb(255, 45, 51, 60)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var bar = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(6, 260d * provider.Count / maximum),
                MaxWidth = 260,
                Background = (Brush)Resources["DashboardBlueBrush"],
                CornerRadius = new CornerRadius(6)
            };
            track.Children.Add(bar);
            var count = new TextBlock
            {
                Text = provider.Count.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Resources["DashboardMutedBrush"]
            };
            Grid.SetColumn(track, 1);
            Grid.SetColumn(count, 2);
            row.Children.Add(label);
            row.Children.Add(track);
            row.Children.Add(count);
            ProviderBarsPanel.Children.Add(row);
        }
    }

    private void RenderStatusDonut(IReadOnlyList<DashboardStatusCount> statuses)
    {
        StatusDonutCanvas.Children.Clear();
        StatusLegendPanel.Children.Clear();
        var allStatuses = new[]
        {
            (SetupStatus.Valide, "Validés", Color.FromArgb(255, 102, 187, 106)),
            (SetupStatus.AVerifier, "À vérifier", Color.FromArgb(255, 255, 183, 77)),
            (SetupStatus.FichierManquant, "Manquants", Color.FromArgb(255, 239, 83, 80)),
            (SetupStatus.Refuse, "Refusés", Color.FromArgb(255, 120, 130, 142))
        };
        var values = allStatuses.Select(definition => new
        {
            definition.Item1,
            definition.Item2,
            definition.Item3,
            Count = statuses.FirstOrDefault(item => item.Status == definition.Item1)?.Count ?? 0
        }).ToList();
        var total = values.Sum(item => item.Count);
        DonutTotalText.Text = total.ToString();

        var backgroundRing = new Ellipse
        {
            Width = 124,
            Height = 124,
            StrokeThickness = 18,
            Stroke = new SolidColorBrush(Color.FromArgb(255, 52, 58, 67))
        };
        Canvas.SetLeft(backgroundRing, 23);
        Canvas.SetTop(backgroundRing, 23);
        StatusDonutCanvas.Children.Add(backgroundRing);

        if (total > 0)
        {
            var angle = -90d;
            foreach (var value in values.Where(item => item.Count > 0))
            {
                var sweep = value.Count * 360d / total;
                AddDonutArc(angle, sweep, value.Item3);
                angle += sweep;
            }
        }

        foreach (var value in values)
        {
            var legend = new Grid { ColumnSpacing = 9 };
            legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            legend.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dot = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(value.Item3), VerticalAlignment = VerticalAlignment.Center };
            var label = new TextBlock { Text = value.Item2 };
            var count = new TextBlock
            {
                Text = value.Count.ToString(),
                FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }
            };
            Grid.SetColumn(label, 1);
            Grid.SetColumn(count, 2);
            legend.Children.Add(dot);
            legend.Children.Add(label);
            legend.Children.Add(count);
            StatusLegendPanel.Children.Add(legend);
        }
    }

    private void AddDonutArc(double startAngle, double sweepAngle, Color color)
    {
        const double center = 85;
        const double radius = 62;
        if (sweepAngle >= 359.99)
        {
            var ring = new Ellipse { Width = 124, Height = 124, StrokeThickness = 18, Stroke = new SolidColorBrush(color) };
            Canvas.SetLeft(ring, 23);
            Canvas.SetTop(ring, 23);
            StatusDonutCanvas.Children.Add(ring);
            return;
        }

        static Point PointOnCircle(double angle, double centerPoint, double circleRadius)
        {
            var radians = angle * Math.PI / 180d;
            return new Point(centerPoint + circleRadius * Math.Cos(radians), centerPoint + circleRadius * Math.Sin(radians));
        }

        var figure = new PathFigure { StartPoint = PointOnCircle(startAngle, center, radius) };
        figure.Segments.Add(new ArcSegment
        {
            Point = PointOnCircle(startAngle + sweepAngle, center, radius),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = sweepAngle > 180
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        StatusDonutCanvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 18
        });
    }

    private static string FormatActivityDate(DateTimeOffset utcDate)
    {
        var local = utcDate.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? $"Aujourd’hui, {local:HH:mm}" : local.ToString("g");
    }

    private static string Plural(int count) => count > 1 ? "s" : string.Empty;

    private sealed record RecentActivityItem(string ChangeType, string FileName, string DateDisplay);
}
