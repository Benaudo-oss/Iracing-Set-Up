using IracingSetupManager.App.Services;
using IracingSetupManager.Core.Catalog;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class SynchronizationPage
{
    private bool completingTelemetry;

    private async void OnCompleteTelemetry(object sender, RoutedEventArgs e)
    {
        if (completingTelemetry || sender is not Button { Tag: string path }) return;
        if (App.Services.Monitoring.IsManualScanRunning)
        {
            ShowWarning("Attendez la fin de la synchronisation avant de compléter ce fichier.");
            return;
        }
        if (!allResultRows.TryGetValue(path, out var row) || !row.NeedsTelemetryCar) return;
        completingTelemetry = true;
        try
        {
            await UiOperation.RunAsync(() => CompleteTelemetryAsync(row),
                "Impossible de compléter le fichier de télémétrie", ActionInfo);
        }
        finally { completingTelemetry = false; }
    }

    private async Task CompleteTelemetryAsync(SynchronizationResultRow row)
    {
        var car = CreateTelemetryCombo("Voiture", SetupCatalog.Cars.Select(item => item.DisplayName));
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = row.FileName, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(car);
        var dialog = new ContentDialog
        {
            Title = "Identifier la voiture", XamlRoot = XamlRoot,
            Content = content, IsPrimaryButtonEnabled = false,
            PrimaryButtonText = "Copier", CloseButtonText = "Annuler", DefaultButton = ContentDialogButton.Close
        };
        car.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = car.SelectedItem is string;
        if (await dialog.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary || car.SelectedItem is not string selected) return;
        var completion = new TelemetryCompletion(selected);
        var result = await App.Services.Monitoring.CompleteTelemetryAsync(row.FullPath, completion);
        ActionInfo.Severity = result.Outcome is SetupImportOutcome.Imported or SetupImportOutcome.Duplicate
            ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ActionInfo.Message = result.Message;
        ActionInfo.IsOpen = true;
    }

    private static ComboBox CreateTelemetryCombo(string title, IEnumerable<string> values) => new()
    {
        Header = title, HorizontalAlignment = HorizontalAlignment.Stretch,
        ItemsSource = values.Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToArray()
    };
}
