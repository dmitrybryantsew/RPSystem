using SkiaSharp;
using Microsoft.Maui.Storage;

namespace RPSystem.Services
{
    public static class ImageHelper
    {
        public static async Task CompressAndSaveImageAsync(FileResult photo, string destPath, int quality, int maxDimension)
        {
            using var stream = await photo.OpenReadAsync();
            using var originalBitmap = SKBitmap.Decode(stream);

            if (originalBitmap == null)
            {
                throw new InvalidOperationException("Failed to decode image. The file may be corrupted or in an unsupported format.");
            }

            // Calculate new dimensions (maintain aspect ratio)
            int newWidth = originalBitmap.Width;
            int newHeight = originalBitmap.Height;

            if (originalBitmap.Width > maxDimension || originalBitmap.Height > maxDimension)
            {
                float ratio = Math.Min((float)maxDimension / originalBitmap.Width, (float)maxDimension / originalBitmap.Height);
                newWidth = (int)(originalBitmap.Width * ratio);
                newHeight = (int)(originalBitmap.Height * ratio);
            }

            // Prepare the bitmap to encode (either original or a new resized one)
            SKBitmap bitmapToEncode = originalBitmap;
            SKBitmap? tempResizedBitmap = null;

            try
            {
                // Resize if needed
                if (newWidth != originalBitmap.Width || newHeight != originalBitmap.Height)
                {
                    tempResizedBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium);
                    if (tempResizedBitmap == null)
                    {
                        throw new InvalidOperationException("Failed to resize image.");
                    }
                    bitmapToEncode = tempResizedBitmap;
                }

                using var image = SKImage.FromBitmap(bitmapToEncode);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

                // Save to file
                using var destStream = File.OpenWrite(destPath);
                data.SaveTo(destStream);
            }
            finally
            {
                // Only dispose the resized bitmap if we created one.
                // originalBitmap is disposed automatically by the 'using' declaration at the top.
                tempResizedBitmap?.Dispose();
            }
        }
    }
}
