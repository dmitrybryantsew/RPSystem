using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPSystem.Desktop.Views;

public partial class GameOptionsView : UserControl
{
    public GameOptionsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
