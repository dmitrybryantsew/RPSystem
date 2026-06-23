using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RPSystem.Desktop.Controls;

namespace RPSystem.Desktop.Views;

public partial class WorldView : UserControl
{
    public WorldView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
