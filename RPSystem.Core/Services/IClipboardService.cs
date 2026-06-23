namespace RPSystem.Core.Services
{
    /// <summary>
    /// Interface for clipboard operations to enable testing without MAUI dependencies
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>
        /// Sets text to the clipboard
        /// </summary>
        Task SetTextAsync(string text);

        /// <summary>
        /// Gets text from the clipboard
        /// </summary>
        Task<string?> GetTextAsync();
    }
}
