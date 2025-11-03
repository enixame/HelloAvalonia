using System;
using Avalonia.Controls;
using HelloAvalonia.ViewModels;

namespace HelloAvalonia.Services;

/// <summary>
/// Service Locator pour centraliser la création et la résolution des dépendances
/// Pattern simple pour applications de taille moyenne
/// </summary>
public class ServiceLocator
{
    private static ServiceLocator? _instance;
    
    private readonly ClientDataService _dataService;
    private readonly SearchService _searchService;
    private readonly Messenger _messenger;
    private IDialogService? _dialogService;

    private ServiceLocator()
    {
        // Créer les services singleton
        _dataService = new ClientDataService();
        _searchService = new SearchService();
        _messenger = Messenger.Default;
    }

    /// <summary>
    /// Instance singleton du ServiceLocator
    /// </summary>
    public static ServiceLocator Instance => _instance ??= new ServiceLocator();

    /// <summary>
    /// Configure le DialogService avec la fenêtre parente
    /// Doit être appelé au démarrage de l'application
    /// </summary>
    public void ConfigureDialogService(Window mainWindow)
    {
        _dialogService = new DialogService(mainWindow);
    }

    /// <summary>
    /// Crée une nouvelle instance de MainWindowViewModel avec toutes ses dépendances
    /// </summary>
    public MainWindowViewModel CreateMainWindowViewModel()
    {
        if (_dialogService == null)
            throw new InvalidOperationException("DialogService n'a pas été configuré. Appelez ConfigureDialogService d'abord.");

        var clientListViewModel = new ClientListViewModel(
            _dataService,
            _searchService,
            _messenger,
            _dialogService);

        return new MainWindowViewModel(
            _dataService,
            _searchService,
            _messenger,
            clientListViewModel);
    }
}
