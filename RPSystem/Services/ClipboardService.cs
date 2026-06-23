using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.Diagnostics;

namespace ChemCalculationAndManagementApp.Services
{
    /// <summary>
    /// MAUI implementation of clipboard service using the Clipboard API
    /// </summary>
    public class ClipboardService : IClipboardService
    {
        public async Task SetTextAsync(string text)
        {
            Debug.WriteLine($"[ClipboardService] SetTextAsync: Setting {text?.Length ?? 0} chars to clipboard");
            Debug.WriteLine($"[ClipboardService] SetTextAsync: Thread: {Thread.CurrentThread.ManagedThreadId}, IsThreadPoolThread={Thread.CurrentThread.IsThreadPoolThread}");
            try
            {
                await Clipboard.Default.SetTextAsync(text);
                Debug.WriteLine($"[ClipboardService] SetTextAsync: SUCCESS - Text written to clipboard");

                // Verify by reading it back
                var verify = await Clipboard.Default.GetTextAsync();
                Debug.WriteLine($"[ClipboardService] SetTextAsync: VERIFICATION - Read back {verify?.Length ?? 0} chars from clipboard");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClipboardService] SetTextAsync: ERROR - {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[ClipboardService] SetTextAsync: StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<string?> GetTextAsync()
        {
            Debug.WriteLine($"[ClipboardService] GetTextAsync: Reading from clipboard");
            try
            {
                var text = await Clipboard.Default.GetTextAsync();
                Debug.WriteLine($"[ClipboardService] GetTextAsync: SUCCESS - Got {text?.Length ?? 0} chars");
                return text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClipboardService] GetTextAsync: ERROR - {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[ClipboardService] GetTextAsync: StackTrace: {ex.StackTrace}");
                return null;
            }
        }
    }
}
