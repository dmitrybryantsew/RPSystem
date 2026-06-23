using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public Task<string?> PickMarkdownFileAsync() => PickFileAsync("Import markdown", new[] { ".md", ".txt" });

    public async Task<string?> PickFileAsync(string title, IEnumerable<string> extensions)
    {
        // Return null for now — actual implementation requires TopLevel reference
        // This is a placeholder that will be replaced when the file picker is wired to a window
        return null;
    }
}
