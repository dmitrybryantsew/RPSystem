using System.Globalization;
using RPSystem.Core.Models;

namespace RPSystem.Converters
{
    /// <summary>
    /// Converts boolean to arrow indicator (▼ for expanded, ▶ for collapsed).
    /// Used for collapsible thinking sections.
    /// </summary>
    public class BoolToArrowConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▼" : "▶";
            }
            return "▶";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
