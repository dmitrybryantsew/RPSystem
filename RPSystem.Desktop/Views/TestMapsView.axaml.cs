using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPSystem.Desktop.Views;

public partial class TestMapsView : UserControl
{
    public TestMapsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
