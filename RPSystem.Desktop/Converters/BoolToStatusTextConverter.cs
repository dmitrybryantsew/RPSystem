using System.Globalization;
using Avalonia.Data.Converters;

namespace RPSystem.Desktop.Converters;

public class BoolToStatusTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue ? (boolValue ? "Debug Mode: ENABLED" : "Debug Mode: DISABLED") : "Debug Mode: DISABLED";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
