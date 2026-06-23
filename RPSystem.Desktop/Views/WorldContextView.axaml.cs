using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPSystem.Desktop.Views;

public partial class WorldContextView : UserControl
{
    public WorldContextView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
