using RPSystem.Core.Services;

namespace RPSystem.Desktop.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public Task<string?> PickMarkdownFileAsync() => Task.FromResult<string?>(null);
}
