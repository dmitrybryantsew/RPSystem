using System.Globalization;
using Avalonia.Data.Converters;

namespace RPSystem.Desktop.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue ? !boolValue : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue ? !boolValue : value;
}
