using HelloAvalonia.ViewModels;

namespace HelloAvalonia.Services;

/// <summary>
/// Message envoyé quand un client est sélectionné
/// </summary>
public class ClientSelectedMessage
{
    public int ClientId { get; set; }
    public DataGridRow? ClientRow { get; set; }
}
