using RPSystem.ViewModels;

namespace RPSystem;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async void OnOpenRpSystemClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//RpSystemPage");
    }

    private async void OnOpenSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//SettingsPage");
    }
}
