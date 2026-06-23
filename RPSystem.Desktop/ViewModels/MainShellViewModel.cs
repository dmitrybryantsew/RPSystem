using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Top-level root viewmodel for the main window. Composes all child viewmodels.
/// </summary>
public sealed partial class MainShellViewModel : ObservableObject
{
    [ObservableProperty] private RpViewMode _activeRpViewMode = RpViewMode.World;

    public bool IsMainMenuView => ActiveRpViewMode == RpViewMode.MainMenu;
    public bool IsWorldView => ActiveRpViewMode == RpViewMode.World;
    public bool IsSettingsView => ActiveRpViewMode == RpViewMode.Settings;
    public bool IsWorldContextView => ActiveRpViewMode == RpViewMode.WorldContext;
    public bool IsGameOptionsView => ActiveRpViewMode == RpViewMode.GameOptions;
    public bool IsDevMenuView => ActiveRpViewMode == RpViewMode.DevMenu;
    public bool IsTestMapsView => ActiveRpViewMode == RpViewMode.TestMaps;

    public string ActiveViewText => ActiveRpViewMode switch
    {
        RpViewMode.MainMenu => "Main Menu",
        RpViewMode.Settings => "Settings",
        RpViewMode.WorldContext => "World Context",
        RpViewMode.GameOptions => "Game Options",
        RpViewMode.DevMenu => "Developer Menu",
        RpViewMode.TestMaps => "Test Maps",
        _ => "World"
    };

    public WorldSimulationViewModel Simulation { get; }
    public PlayerControlViewModel Player { get; }
    public WorldMapViewModel Map { get; }
    public TestMapsViewModel TestMaps { get; }
    public WorldContextEditorViewModel WorldContextEditor { get; }

    public MainShellViewModel(
        WorldSimulationViewModel simulation,
        PlayerControlViewModel player,
        WorldMapViewModel map,
        TestMapsViewModel testMaps,
        WorldContextEditorViewModel worldContextEditor)
    {
        Simulation = simulation;
        Player = player;
        Map = map;
        TestMaps = testMaps;
        WorldContextEditor = worldContextEditor;
    }

    [RelayCommand] private void ShowMainMenu() => ActiveRpViewMode = RpViewMode.MainMenu;
    [RelayCommand] private void ShowWorldView() => ActiveRpViewMode = RpViewMode.World;
    [RelayCommand] private void ShowSettingsView() => ActiveRpViewMode = RpViewMode.Settings;
    [RelayCommand] private void ShowWorldContextView() => ActiveRpViewMode = RpViewMode.WorldContext;
    [RelayCommand] private void ShowGameOptionsView() => ActiveRpViewMode = RpViewMode.GameOptions;
    [RelayCommand] private void ShowDevMenu() => ActiveRpViewMode = RpViewMode.DevMenu;
    [RelayCommand] private void ShowTestMaps() => ActiveRpViewMode = RpViewMode.TestMaps;

    partial void OnActiveRpViewModeChanged(RpViewMode value)
    {
        OnPropertyChanged(nameof(IsMainMenuView));
        OnPropertyChanged(nameof(IsWorldView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsWorldContextView));
        OnPropertyChanged(nameof(IsGameOptionsView));
        OnPropertyChanged(nameof(IsDevMenuView));
        OnPropertyChanged(nameof(IsTestMapsView));
        OnPropertyChanged(nameof(ActiveViewText));
    }
}
