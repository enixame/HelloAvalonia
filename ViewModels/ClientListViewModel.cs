using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using HelloAvalonia.Models;
using HelloAvalonia.Services;

namespace HelloAvalonia.ViewModels;

/// <summary>
/// ViewModel composite responsable de l'affichage et du filtrage de la liste des clients
/// </summary>
public class ClientListViewModel : ViewModelBase
{
    private readonly ClientDataService _dataService;
    private readonly SearchService _searchService;
    private readonly Messenger _messenger;
    private readonly IDialogService _dialogService;

    private ObservableCollection<DataGridRow> _rows = new();
    private string _searchText = string.Empty;
    private int _filteredCount;
    private System.Threading.Timer? _searchDebounceTimer;
    private bool _isLoading;
    private DataGridRow? _selectedRow;
    private Client? _selectedClient;
    private bool _isDetailPopupOpen;

    public ClientListViewModel(ClientDataService dataService, SearchService searchService, Messenger messenger, IDialogService dialogService)
    {
        _dataService = dataService;
        _searchService = searchService;
        _messenger = messenger;
        _dialogService = dialogService;

        ToggleExpandCommand = new RelayCommand<DataGridRow>(ToggleExpand);
        ClearSearchCommand = new RelayCommand<object>(_ => SearchText = string.Empty);
        ShowClientDetailPopupCommand = new RelayCommand(ShowClientDetailPopup, CanShowClientDetailPopup);

        // S'abonner aux messages
        _messenger.Subscribe<DataLoadedMessage>(OnDataLoaded);
    }

    #region Properties

    /// <summary>
    /// Lignes visibles dans le DataGrid
    /// </summary>
    public ObservableCollection<DataGridRow> Rows
    {
        get => _rows;
        set => SetProperty(ref _rows, value);
    }

    /// <summary>
    /// Texte de recherche
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                // Debounce : attendre 300ms après la dernière frappe
                _searchDebounceTimer?.Dispose();
                _searchDebounceTimer = new System.Threading.Timer(
                    _ => Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyFilterAsync()),
                    null,
                    300,
                    System.Threading.Timeout.Infinite
                );

                _messenger.Send(new SearchTextChangedMessage { SearchText = value });
            }
        }
    }

    /// <summary>
    /// Nombre de clients filtrés
    /// </summary>
    public int FilteredCount
    {
        get => _filteredCount;
        set => SetProperty(ref _filteredCount, value);
    }

    /// <summary>
    /// Indique si le chargement est en cours
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Ligne sélectionnée dans le DataGrid
    /// </summary>
    public DataGridRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                UpdateSelectedClient();
            }
        }
    }

    /// <summary>
    /// Client sélectionné (pour affichage des détails)
    /// </summary>
    public Client? SelectedClient
    {
        get => _selectedClient;
        set => SetProperty(ref _selectedClient, value);
    }

    /// <summary>
    /// Indique si la popup de détails est ouverte
    /// </summary>
    public bool IsDetailPopupOpen
    {
        get => _isDetailPopupOpen;
        set => SetProperty(ref _isDetailPopupOpen, value);
    }

    #endregion

    #region Commands

    public ICommand ToggleExpandCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ShowClientDetailPopupCommand { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Appelé quand les données sont chargées
    /// </summary>
    private async void OnDataLoaded(DataLoadedMessage message)
    {
        await DisplayInitialDataAsync();
    }

    /// <summary>
    /// Affiche les données initiales
    /// </summary>
    private async Task DisplayInitialDataAsync()
    {
        IsLoading = true;

        await Task.Run(async () =>
        {
            // Afficher les 500 premiers clients immédiatement
            const int initialCount = 500;
            var clientRows = _dataService.ClientRows;
            var initialRows = clientRows.Take(initialCount).ToList();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Rows = new ObservableCollection<DataGridRow>(initialRows);
                FilteredCount = clientRows.Count;
            });

            Console.WriteLine($"✓ Affichage initial: {initialCount}/{clientRows.Count} clients");

            // Charger le reste progressivement
            if (clientRows.Count > initialCount)
            {
                await LoadRemainingDataAsync(initialCount);
            }

            IsLoading = false;
        });
    }

    /// <summary>
    /// Charge les données restantes par batches
    /// </summary>
    private async Task LoadRemainingDataAsync(int startIndex)
    {
        await Task.Delay(200); // Laisser l'UI s'initialiser

        const int batchSize = 2000;
        var clientRows = _dataService.ClientRows;
        var currentIndex = startIndex;

        while (currentIndex < clientRows.Count)
        {
            var batchEnd = Math.Min(currentIndex + batchSize, clientRows.Count);
            var batch = clientRows.Skip(currentIndex).Take(batchEnd - currentIndex).ToList();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var newRows = new List<DataGridRow>(Rows);
                newRows.AddRange(batch);
                Rows = new ObservableCollection<DataGridRow>(newRows);
            });

            currentIndex = batchEnd;
            await Task.Delay(50);
        }

        Console.WriteLine($"✓ Chargement complet: {clientRows.Count} clients affichés");
    }

    /// <summary>
    /// Applique le filtre de recherche
    /// </summary>
    private async void ApplyFilterAsync()
    {
        var searchText = SearchText?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            // Pas de recherche : afficher tous les clients
            var allRows = _dataService.ClientRows.ToList();
            Rows = new ObservableCollection<DataGridRow>(allRows);
            FilteredCount = allRows.Count;
            return;
        }

        // Recherche asynchrone
        var results = await _searchService.SearchAsync(searchText, _dataService.ClientRows);
        Rows = new ObservableCollection<DataGridRow>(results);
        FilteredCount = results.Count;
    }

    /// <summary>
    /// Expand/collapse un client
    /// </summary>
    private void ToggleExpand(DataGridRow? row)
    {
        if (row == null || !row.IsParent)
            return;

        row.IsExpanded = !row.IsExpanded;

        // Créer les enfants si nécessaire (lazy loading)
        if (row.IsExpanded && row.Children.Count == 0)
        {
            _dataService.ExpandClientRow(row);
        }

        UpdateChildrenVisibility(row);
        
        _messenger.Send(new ToggleExpandMessage { Row = row });
    }

    /// <summary>
    /// Met à jour la visibilité des enfants
    /// </summary>
    private void UpdateChildrenVisibility(DataGridRow parentRow)
    {
        if (parentRow.IsExpanded)
        {
            var parentIndex = Rows.IndexOf(parentRow);
            if (parentIndex == -1) return;

            foreach (var child in parentRow.Children)
            {
                if (!Rows.Contains(child))
                {
                    Rows.Insert(++parentIndex, child);
                }
            }
        }
        else
        {
            foreach (var child in parentRow.Children.ToList())
            {
                Rows.Remove(child);
            }
        }
    }

    /// <summary>
    /// Met à jour le client sélectionné en fonction de la ligne sélectionnée
    /// </summary>
    private void UpdateSelectedClient()
    {
        if (SelectedRow == null)
        {
            SelectedClient = null;
            return;
        }

        // Si c'est un client parent, prendre directement
        if (SelectedRow.IsParent && SelectedRow.Tag is Client client)
        {
            SelectedClient = client;
        }
        // Si c'est une adresse enfant, trouver le client parent
        else if (!SelectedRow.IsParent && SelectedRow.Tag is Adresse)
        {
            // Trouver le parent dans les lignes
            var parentRow = Rows.FirstOrDefault(r => 
                r.IsParent && 
                r.Children.Contains(SelectedRow));
            
            if (parentRow?.Tag is Client parentClient)
            {
                SelectedClient = parentClient;
            }
        }
    }

    /// <summary>
    /// Vérifie si on peut afficher la popup de détails
    /// </summary>
    private bool CanShowClientDetailPopup()
    {
        return SelectedClient != null;
    }

    /// <summary>
    /// Affiche la popup de détails du client sélectionné
    /// </summary>
    private async void ShowClientDetailPopup()
    {
        if (SelectedClient is not Client client)
            return;

        await _dialogService.ShowClientDetailsAsync(client);
    }

    #endregion
}
