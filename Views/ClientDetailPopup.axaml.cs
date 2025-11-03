using Avalonia.Controls;
using Avalonia.Interactivity;
using HelloAvalonia.Models;

namespace HelloAvalonia.Views;

public partial class ClientDetailPopup : Window
{
    public Client? Client { get; set; }

    public ClientDetailPopup()
    {
        InitializeComponent();
    }

    public ClientDetailPopup(Client client) : this()
    {
        Client = client;
        DataContext = this;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
