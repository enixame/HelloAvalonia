using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
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

    private ObservableCollection<DataGridRow> _rows = new();
    private string _searchText = string.Empty;
    private int _filteredCount;
    private System.Threading.Timer? _searchDebounceTimer;
    private bool _isLoading;

    public ClientListViewModel(ClientDataService dataService, SearchService searchService, Messenger messenger)
    {
        _dataService = dataService;
        _searchService = searchService;
        _messenger = messenger;

        ToggleExpandCommand = new RelayCommand<DataGridRow>(ToggleExpand);
        ClearSearchCommand = new RelayCommand<object>(_ => SearchText = string.Empty);

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

    #endregion

    #region Commands

    public ICommand ToggleExpandCommand { get; }
    public ICommand ClearSearchCommand { get; }

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

    #endregion
}
