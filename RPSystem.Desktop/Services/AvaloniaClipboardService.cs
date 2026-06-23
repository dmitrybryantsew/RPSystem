using RPSystem.Core.Services;

namespace RPSystem.Desktop.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Task.CompletedTask;
    public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
}
