using CommunityToolkit.Mvvm.ComponentModel;

namespace ChemCalculationAndManagementApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string statusMessage = "Ready.";

    public MainViewModel()
    {
    }
}
