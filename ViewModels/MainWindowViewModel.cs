using System;
using System.Threading.Tasks;
using HelloAvalonia.Services;

namespace HelloAvalonia.ViewModels;

/// <summary>
/// ViewModel principal coordonnant les différents ViewModels composites
/// Pattern: Composite ViewModel avec Mediator (Messenger)
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly ClientDataService _dataService;
    private readonly SearchService _searchService;
    private readonly Messenger _messenger;
    
    private bool _isInitialized;
    private string _statusMessage = "Initialisation...";

    /// <summary>
    /// Constructeur avec injection de dépendances
    /// </summary>
    public MainWindowViewModel(
        ClientDataService dataService,
        SearchService searchService,
        Messenger messenger,
        ClientListViewModel clientListViewModel)
    {
        Console.WriteLine("MainWindowViewModel créé");

        // Injection de dépendances
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        ClientListViewModel = clientListViewModel ?? throw new ArgumentNullException(nameof(clientListViewModel));

        // S'abonner aux événements
        _messenger.Subscribe<DataLoadedMessage>(OnDataLoaded);
        _messenger.Subscribe<SearchTextChangedMessage>(OnSearchTextChanged);

        // Charger les données
        InitializeAsync();
    }

    #region Properties

    /// <summary>
    /// ViewModel de la liste des clients
    /// </summary>
    public ClientListViewModel ClientListViewModel { get; }

    /// <summary>
    /// Message de statut affiché dans l'UI
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Indique si l'initialisation est terminée
    /// </summary>
    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value);
    }

    #endregion



    #region Initialization

    /// <summary>
    /// Initialise l'application (chargement des données)
    /// </summary>
    private async void InitializeAsync()
    {
        var totalTimer = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            StatusMessage = "Chargement des données...";

            // 1. Charger les données
            var result = await _dataService.LoadClientsAsync();

            if (!result.Success)
            {
                StatusMessage = $"Erreur: {result.ErrorMessage}";
                Console.WriteLine($"❌ Erreur: {result.ErrorMessage}");
                return;
            }

            // 2. Notifier que les données sont chargées
            _messenger.Send(new DataLoadedMessage
            {
                ClientCount = result.ClientCount,
                AddressCount = result.TotalAddresses
            });

            // 3. Construire l'index de recherche en arrière-plan
            _ = Task.Run(async () =>
            {
                await _searchService.BuildSearchIndexAsync(_dataService.ClientRows);
            });

            totalTimer.Stop();
            
            StatusMessage = $"✓ {result.ClientCount} clients chargés";
            IsInitialized = true;

            Console.WriteLine($"");
            Console.WriteLine($"⏱️  TEMPS TOTAL: {totalTimer.ElapsedMilliseconds} ms ({totalTimer.Elapsed.TotalSeconds:F2}s)");
            Console.WriteLine($"📊 Mémoire: ~{GC.GetTotalMemory(false) / 1024 / 1024} MB");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur: {ex.Message}";
            Console.WriteLine($"❌ Erreur d'initialisation: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Appelé quand les données sont chargées
    /// </summary>
    private void OnDataLoaded(DataLoadedMessage message)
    {
        Console.WriteLine($"📨 Message reçu: {message.ClientCount} clients, {message.AddressCount} adresses");
    }

    /// <summary>
    /// Appelé quand le texte de recherche change
    /// </summary>
    private void OnSearchTextChanged(SearchTextChangedMessage message)
    {
        // Le MainViewModel peut réagir aux changements de recherche si nécessaire
        // Par exemple, mettre à jour des statistiques, logger, etc.
    }

    #endregion
}
