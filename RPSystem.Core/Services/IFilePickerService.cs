namespace RPSystem.Core.Services;

public interface IFilePickerService
{
    /// <summary>Returns the picked file's full path, or null if cancelled.</summary>
    Task<string?> PickMarkdownFileAsync();
}
