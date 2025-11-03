using System.Threading.Tasks;
using HelloAvalonia.Models;

namespace HelloAvalonia.Services;

/// <summary>
/// Service pour gérer l'affichage des dialogs/popups
/// Permet de découpler le ViewModel de la logique UI
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Affiche une popup avec les détails d'un client
    /// </summary>
    Task ShowClientDetailsAsync(Client client);
}
