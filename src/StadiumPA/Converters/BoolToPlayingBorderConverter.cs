using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StadiumPA.Converters;

/// <summary>
/// Converts a boolean (IsPlaying) to a border color.
/// True = green (#44CC44), False = default gray (#555555).
/// </summary>
public sealed class BoolToPlayingBorderConverter : IValueConverter
{
    private static readonly Color PlayingColor = (Color)ColorConverter.ConvertFromString("#44CC44");
    private static readonly Color IdleColor = (Color)ColorConverter.ConvertFromString("#555555");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? PlayingColor : IdleColor;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
