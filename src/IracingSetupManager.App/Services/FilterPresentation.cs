using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IracingSetupManager.App.Services;

public static class FilterPresentation
{
    public static void Rebuild(
        StackPanel panel,
        IEnumerable<(string Key, string Label)> filters,
        RoutedEventHandler removeHandler)
    {
        panel.Children.Clear();
        foreach (var filter in filters)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            content.Children.Add(new TextBlock { Text = filter.Label, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new FontIcon { Glyph = "\uE711", FontSize = 11 });
            var button = new Button
            {
                Tag = filter.Key,
                Content = content,
                Padding = new Thickness(10, 5, 9, 5),
                MinHeight = 30,
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(Color.FromArgb(255, 36, 63, 78))
            };
            button.Click += removeHandler;
            panel.Children.Add(button);
        }
    }
}
