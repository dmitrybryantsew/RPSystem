using System.Globalization;
using Avalonia.Data.Converters;
using RPSystem.Core.Models;

namespace RPSystem.Desktop.Converters;

public class BoolToArrowConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isExpanded ? (isExpanded ? "▼" : "▶") : "▶";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
