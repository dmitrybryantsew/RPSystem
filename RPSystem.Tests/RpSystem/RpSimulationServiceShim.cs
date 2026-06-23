namespace Microsoft.Maui.Storage;

/// <summary>
/// Test-only replacement for the MAUI FileSystem API used by RP debug logging.
/// </summary>
public static class FileSystem
{
    public static string AppDataDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "ChemCalculationAndManagementApp.Tests");
}
