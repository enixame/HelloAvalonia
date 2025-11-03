using Avalonia.Controls;

namespace HelloAvalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Le DataContext est assigné par App.axaml.cs (Composition Root)
    }
}