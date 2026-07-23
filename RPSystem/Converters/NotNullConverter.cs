using System.Globalization;

namespace RPSystem.Converters
{
    /// <summary>
    /// Converts non-null values to true, null to false.
    /// Useful for showing/hiding UI elements based on whether a value exists.
    /// </summary>
    public class NotNullConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
