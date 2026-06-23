namespace RPSystem.Core.Services;

/// <summary>
/// Provides application data directory path. The host application sets this at startup.
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Set by the host application (MAUI or Avalonia) at startup.
    /// Defaults to a local "AppData/RPSystem" subdirectory for desktop use.
    /// </summary>
    public static string AppDataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPSystem");

    public static string Combine(string relativePath)
        => Path.Combine(AppDataDirectory, relativePath);
}
