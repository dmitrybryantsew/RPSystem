using CommunityToolkit.Mvvm.ComponentModel;

namespace RPSystem.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string statusMessage = "Ready.";

    public MainViewModel()
    {
    }
}
