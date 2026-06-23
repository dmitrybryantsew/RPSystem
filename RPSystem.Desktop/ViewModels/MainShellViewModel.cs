using CommunityToolkit.Mvvm.ComponentModel;

namespace RPSystem.Desktop.ViewModels;

public sealed partial class MainShellViewModel : ObservableObject
{
    [ObservableProperty] private string _welcomeText = "RPSystem Desktop — Phase 04 Shell";
}
