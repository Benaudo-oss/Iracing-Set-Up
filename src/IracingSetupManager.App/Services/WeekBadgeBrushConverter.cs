using IracingSetupManager.Core.Setups;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IracingSetupManager.App.Services;

public sealed class WeekBadgeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var kind = value is SetupWeekKind parsed ? parsed : SetupWeekKind.Unknown;
        var colors = kind switch
        {
            SetupWeekKind.Numeric => ("#FF173852", "#FF285D84", "#FFA9D8FF"),
            SetupWeekKind.Nec => ("#FF34294E", "#FF544377", "#FFD5C4FF"),
            SetupWeekKind.NoWeek => ("#FF303238", "#FF4A4E56", "#FFC4C7CC"),
            _ => ("#FF493313", "#FF78541C", "#FFFFD18A")
        };
        var color = (parameter as string) switch
        {
            "Border" => colors.Item2,
            "Foreground" => colors.Item3,
            _ => colors.Item1
        };
        return new SolidColorBrush(Color.FromArgb(
            System.Convert.ToByte(color.Substring(1, 2), 16),
            System.Convert.ToByte(color.Substring(3, 2), 16),
            System.Convert.ToByte(color.Substring(5, 2), 16),
            System.Convert.ToByte(color.Substring(7, 2), 16)));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
