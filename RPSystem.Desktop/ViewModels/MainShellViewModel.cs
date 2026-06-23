using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;

namespace RPSystem.Desktop.ViewModels;

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
    }
}
