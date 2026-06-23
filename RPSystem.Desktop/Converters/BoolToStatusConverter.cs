using System.Globalization;
using Avalonia.Data.Converters;

namespace RPSystem.Desktop.Converters;

public class BoolToStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue ? (boolValue ? "Debug Mode: ON" : "Debug Mode: OFF") : "Debug Mode: OFF";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
