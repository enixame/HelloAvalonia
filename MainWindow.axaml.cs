using Avalonia.Controls;
using HelloAvalonia.ViewModels;

namespace HelloAvalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}