using IracingSetupManager.App.Services;
using IracingSetupManager.Infrastructure.Database.Entities;
using IracingSetupManager.Infrastructure.Files.Import;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IracingSetupManager.App.Views;

public sealed partial class LibraryPage
{
    private async void OnExportZip(object sender, RoutedEventArgs e)
    {
        ExportZipButton.IsEnabled = false;
        try { await UiOperation.RunAsync(ExportZipAsync, "Impossible de créer le ZIP"); }
        finally { ExportZipButton.IsEnabled = true; }
    }

    private async Task ExportZipAsync()
    {
        var checkedSetups = SetupList.SelectedItems.OfType<LibrarySetupRow>().Select(row => row.Setup).ToArray();
        var limit = SetupZipExportService.MaximumBatchSize;
        if (checkedSetups.Length > limit)
        {
            await ShowExportMessageAsync($"Sélectionnez au maximum {limit} setups à la fois.");
            return;
        }

        var mode = new ComboBox
        {
            Header = "Date d’ajout à la bibliothèque",
            ItemsSource = new[] { "Toutes les dates", "Depuis le dernier export", "Période personnalisée" },
            SelectedIndex = checkedSetups.Length == 0 ? 1 : 0
        };
        var from = new CalendarDatePicker { Header = "Du (inclus)" };
        var to = new CalendarDatePicker { Header = "Au (inclus)" };
        var dates = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        dates.Children.Add(from);
        dates.Children.Add(to);
        var filter = new StackPanel { Spacing = 12 };
        filter.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = checkedSetups.Length == 0
            ? "Aucune ligne cochée : l’aperçu utilisera les filtres actuels de la Bibliothèque."
            : $"{checkedSetups.Length} setup(s) cochés dans la Bibliothèque." });
        filter.Children.Add(mode);
        filter.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
            Text = "« Depuis le dernier export » propose les setups jamais exportés, y compris ceux laissés de côté." });
        filter.Children.Add(dates);
        var dateDialog = ExportDialog("Préparer l’export ZIP", filter, "Afficher l’aperçu");
        void UpdateDates()
        {
            dates.Visibility = mode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            dateDialog.IsPrimaryButtonEnabled = mode.SelectedIndex != 2 ||
                (from.Date is { } start && to.Date is { } end && start.Date <= end.Date);
        }
        mode.SelectionChanged += (_, _) => UpdateDates();
        from.DateChanged += (_, _) => UpdateDates();
        to.DateChanged += (_, _) => UpdateDates();
        UpdateDates();
        if (await dateDialog.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;

        long? fromKey = mode.SelectedIndex == 2 ? LocalDayKey(from.Date!.Value.Date) : null;
        long? beforeKey = mode.SelectedIndex == 2 ? LocalDayKey(to.Date!.Value.Date.AddDays(1)) : null;
        var neverExported = mode.SelectedIndex == 1;
        IReadOnlyList<SetupEntity> candidates;
        int total;
        if (checkedSetups.Length == 0)
        {
            var page = await App.Services.QueryService.GetPageAsync(CreatePageRequest(0) with
            {
                Take = limit, NeverExported = neverExported, AddedFrom = fromKey, AddedBefore = beforeKey
            });
            candidates = page.Items;
            total = page.TotalCount;
        }
        else
        {
            candidates = checkedSetups.Where(setup =>
                (!neverExported || setup.LastExportedAtUtc is null) &&
                (fromKey is null || setup.DownloadedAtUtcSortKey >= fromKey) &&
                (beforeKey is null || setup.DownloadedAtUtcSortKey < beforeKey)).ToArray();
            total = candidates.Count;
        }
        if (candidates.Count == 0)
        {
            await ShowExportMessageAsync("Aucun setup ne correspond à ce filtre.");
            return;
        }

        var rows = candidates.Select(setup => new ExportRow(setup, SetupZipExportService.GetBlockReason(setup))).ToArray();
        var list = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.Multiple,
            IsMultiSelectCheckBoxEnabled = true, MaxHeight = 270, MinWidth = 380 };
        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxHeight = 95 };
        var name = new TextBox { Header = "Nom du ZIP principal (sans .zip)", Text = $"Setups-{DateTime.Now:yyyy-MM-dd-HHmmss}" };
        var panel = new StackPanel { Spacing = 10 };
        if (total > candidates.Count)
            panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
                Text = $"{candidates.Count} setups proposés sur {total}. Les autres resteront disponibles." });
        panel.Children.Add(summary);
        panel.Children.Add(list);
        panel.Children.Add(name);
        var preview = ExportDialog("Aperçu de l’export", panel, "Choisir le dossier");
        void UpdatePreview()
        {
            var selected = list.SelectedItems.OfType<ExportRow>().ToArray();
            var blocked = selected.Count(row => row.BlockReason is not null);
            var groups = selected.GroupBy(row => row.Setup.Car.Trim(), StringComparer.OrdinalIgnoreCase).ToArray();
            summary.Text = blocked > 0
                ? $"{blocked} setup(s) bloquent l’export. Décochez les lignes signalées ou annulez pour les corriger."
                : $"{selected.Length} setup(s) · {groups.Length} ZIP voiture" +
                    (groups.Length == 0 ? "" : "\n" + string.Join(" · ", groups.Select(group => $"{group.Key} : {group.Count()}")));
            preview.IsPrimaryButtonEnabled = selected.Length > 0 && blocked == 0 && IsValidZipStem(name.Text.Trim());
        }
        list.SelectAll();
        list.SelectionChanged += (_, _) => UpdatePreview();
        name.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();
        if (await preview.ApplyActionStyles().ShowAsync() != ContentDialogResult.Primary) return;
        var selection = list.SelectedItems.OfType<ExportRow>().Select(row => row.Setup.Id).ToArray();
        var folder = await WinUiFolderPicker.PickAsync();
        if (folder is null) return;
        var destination = Path.Combine(folder, name.Text.Trim() + ".zip");
        if (File.Exists(destination))
        {
            await ShowExportMessageAsync("Ce ZIP existe déjà. Choisissez un autre nom : aucun fichier ne sera écrasé.");
            return;
        }
        var service = new SetupZipExportService(App.Services.ContextFactory);
        var recorded = await Task.Run(() => service.ExportAsync(selection, destination));
        var exported = selection.ToHashSet();
        if (recorded)
            foreach (var row in rows.Where(row => exported.Contains(row.Setup.Id)))
                row.Setup.LastExportedAtUtc = DateTimeOffset.UtcNow;
        await ShowExportMessageAsync(recorded
            ? $"{selection.Length} setup(s) exportés dans {destination}."
            : $"Le ZIP a été créé dans {destination}, mais le suivi n’a pas été enregistré. Ces setups pourront être reproposés.");
    }

    private ContentDialog ExportDialog(string title, object content, string action) => new()
    {
        XamlRoot = XamlRoot, Title = title, Content = content, PrimaryButtonText = action,
        CloseButtonText = "Annuler", DefaultButton = ContentDialogButton.Close
    };

    private async Task ShowExportMessageAsync(string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = "Export ZIP",
            Content = message, CloseButtonText = "Fermer" }.ApplyActionStyles().ShowAsync();

    private static long LocalDayKey(DateTime day) =>
        new DateTimeOffset(DateTime.SpecifyKind(day, DateTimeKind.Local)).ToUnixTimeMilliseconds();

    private static bool IsValidZipStem(string name) =>
        name.Length is > 0 and <= 120 && !name.EndsWith('.') &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !System.Text.RegularExpressions.Regex.IsMatch(name,
            @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private sealed record ExportRow(SetupEntity Setup, string? BlockReason)
    {
        public override string ToString() => $"{Setup.Car} · {Setup.OriginalFileName}" +
            (BlockReason is null ? "" : $" — {BlockReason}");
    }
}
