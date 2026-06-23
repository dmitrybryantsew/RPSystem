using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPSystem.Desktop.Views;

public partial class DevMenuView : UserControl
{
    public DevMenuView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
