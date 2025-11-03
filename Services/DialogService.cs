using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using HelloAvalonia.Models;

namespace HelloAvalonia.Services;

/// <summary>
/// Implémentation du service de dialogs
/// </summary>
public class DialogService : IDialogService
{
    private readonly Window _owner;

    /// <summary>
    /// Constructeur avec injection de la fenêtre parente
    /// </summary>
    public DialogService(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public async Task ShowClientDetailsAsync(Client client)
    {
        var popup = new Views.ClientDetailPopup(client);
        await popup.ShowDialog(_owner);
    }
}
