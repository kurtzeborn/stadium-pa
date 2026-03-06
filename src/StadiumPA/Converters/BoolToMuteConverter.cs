using System.Globalization;
using System.Windows.Data;

namespace StadiumPA.Converters;

/// <summary>
/// Converts a boolean (IsMuted) to a mute/unmute icon string for the mute button.
/// </summary>
public sealed class BoolToMuteConverter : IValueConverter
{
    public static readonly BoolToMuteConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "🔇" : "🔊";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
