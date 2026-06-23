// Converters/StringToImageSourceConverter.cs
using System.Globalization;
using System.Collections.Concurrent;

namespace ChemCalculationAndManagementApp.Converters
{
    /// <summary>
    /// Converts string paths or data URLs to ImageSource.
    /// Handles both file paths and base64 data URLs with caching.
    /// </summary>
    public class StringToImageSourceConverter : IValueConverter
    {
        // Cache for ImageSource objects to prevent memory leaks from repeated base64 decoding
        private static readonly ConcurrentDictionary<string, ImageSource> _imageCache = new();
        // Cache for data URLs to prevent repeated base64 string operations
        private static readonly ConcurrentDictionary<string, string> _dataUrlCache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string stringValue || string.IsNullOrEmpty(stringValue))
            {
                return null;
            }

            try
            {
                // Check if it's a data URL (base64 encoded image)
                if (stringValue.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    // Return cached image source if available
                    if (_imageCache.TryGetValue(stringValue, out var cachedSource))
                    {
                        return cachedSource;
                    }

                    // For very large data URLs, use a stream source instead of string
                    // This prevents MAUI from re-decoding the base64 on every render
                    if (stringValue.Length > 100_000) // Larger than ~100KB
                    {
                        var imageSource = ImageSource.FromStream(() =>
                        {
                            var base64Data = stringValue.Substring(stringValue.IndexOf(',') + 1);
                            var bytes = System.Convert.FromBase64String(base64Data);
                            return new MemoryStream(bytes);
                        });

                        // Cache it (with size limit check to avoid memory issues)
                        if (_imageCache.Count < 50) // Limit cache size
                        {
                            _imageCache.TryAdd(stringValue, imageSource);
                        }

                        System.Diagnostics.Debug.WriteLine($"[StringToImageSource] Created stream source for large data URL ({stringValue.Length} chars)");
                        return imageSource;
                    }
                    else
                    {
                        // For smaller data URLs, use the string directly
                        var imageSource = ImageSource.FromUri(new Uri(stringValue));

                        // Cache it
                        if (_imageCache.Count < 50)
                        {
                            _imageCache.TryAdd(stringValue, imageSource);
                        }

                        System.Diagnostics.Debug.WriteLine($"[StringToImageSource] Created URI source for small data URL ({stringValue.Length} chars)");
                        return imageSource;
                    }
                }
                else
                {
                    // It's a file path
                    if (_imageCache.TryGetValue(stringValue, out var cachedSource))
                    {
                        return cachedSource;
                    }

                    if (File.Exists(stringValue))
                    {
                        var imageSource = ImageSource.FromFile(stringValue);

                        // Cache file sources too (with lower limit since file system handles access)
                        if (_imageCache.Count < 100)
                        {
                            _imageCache.TryAdd(stringValue, imageSource);
                        }

                        return imageSource;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[StringToImageSource] File not found: {stringValue}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StringToImageSource] Error converting string to ImageSource: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[StringToImageSource] String value: {stringValue.Substring(0, Math.Min(100, stringValue.Length))}...");
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clears the image cache to free memory.
        /// Call this when navigating away from a page with many images.
        /// </summary>
        public static void ClearCache()
        {
            _imageCache.Clear();
            _dataUrlCache.Clear();
            System.Diagnostics.Debug.WriteLine("[StringToImageSource] Cache cleared");
        }
    }
}
