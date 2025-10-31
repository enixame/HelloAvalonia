using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using HelloAvalonia.Models;

namespace HelloAvalonia.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Client> _clients = new();
        private ObservableCollection<DataGridRow> _rows = new();
        private List<DataGridRow> _allClientRows = new(); // Cache de toutes les lignes
        private string _searchText = string.Empty;
        private int _filteredCount;
        private System.Threading.Timer? _searchDebounceTimer;
        private bool _isIndexBuilt = false;
        private System.Threading.CancellationTokenSource? _searchCancellation;

        public ObservableCollection<Client> Clients
        {
            get => _clients;
            set
            {
                _clients = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DataGridRow> Rows
        {
            get => _rows;
            set
            {
                _rows = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                // Débounce : attendre 300ms après la dernière frappe
                _searchDebounceTimer?.Dispose();
                _searchDebounceTimer = new System.Threading.Timer(
                    _ => Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyFilter()),
                    null,
                    300,
                    System.Threading.Timeout.Infinite
                );
            }
        }

        public int FilteredCount
        {
            get => _filteredCount;
            set
            {
                _filteredCount = value;
                OnPropertyChanged();
            }
        }

        public ICommand ToggleExpandCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public MainWindowViewModel()
        {
            Console.WriteLine("MainWindowViewModel créé");
            ToggleExpandCommand = new RelayCommand<DataGridRow>(ToggleExpand);
            ClearSearchCommand = new RelayCommand<object>(_ => SearchText = string.Empty);
            LoadClientsAsync();
        }

        private void ToggleExpand(DataGridRow? row)
        {
            if (row == null || !row.IsParent) return;

            row.IsExpanded = !row.IsExpanded;
            
            // Créer les enfants à la demande (lazy loading)
            if (row.IsExpanded && row.Children.Count == 0)
            {
                var client = (Client)row.Tag!;
                foreach (var adresse in client.Adresses)
                {
                    var adresseRow = new DataGridRow
                    {
                        Id = "",
                        Nom = $"📍 {adresse.Type}",
                        Email = adresse.Rue,
                        Details = $"{adresse.CodePostal} - {adresse.Ville}",
                        IsParent = false,
                        Level = 1,
                        IsExpanded = false,
                        Parent = row,
                        Tag = adresse
                    };
                    row.Children.Add(adresseRow);
                }
            }
            
            UpdateChildrenVisibility(row);
        }

        private async void LoadClientsAsync()
        {
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "clients.json");
                Console.WriteLine($"Chemin du fichier JSON: {jsonPath}");
                Console.WriteLine($"Le fichier existe: {File.Exists(jsonPath)}");
                
                if (!File.Exists(jsonPath))
                {
                    Console.WriteLine($"Répertoire de base: {AppContext.BaseDirectory}");
                    return;
                }

                var loadTimer = System.Diagnostics.Stopwatch.StartNew();
                // Utiliser FileStream pour lecture plus rapide
                using var fileStream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true);
                loadTimer.Stop();
                Console.WriteLine($"✓ Ouverture fichier: {loadTimer.ElapsedMilliseconds} ms ({new FileInfo(jsonPath).Length / 1024} KB)");
                
                var deserializeTimer = System.Diagnostics.Stopwatch.StartNew();
                // Désérialiser directement depuis le stream (plus rapide et moins de mémoire)
                var clients = await JsonSerializer.DeserializeAsync<Client[]>(fileStream);
                deserializeTimer.Stop();
                Console.WriteLine($"✓ Désérialisation: {deserializeTimer.ElapsedMilliseconds} ms ({clients?.Length ?? 0} clients)");

                if (clients != null)
                {
                    var addTimer = System.Diagnostics.Stopwatch.StartNew();
                    Clients.Clear();
                    foreach (var client in clients)
                    {
                        Clients.Add(client);
                    }
                    addTimer.Stop();
                    Console.WriteLine($"✓ Ajout à la collection: {addTimer.ElapsedMilliseconds} ms");
                    
                    // Créer les lignes hiérarchiques
                    var hierarchyTimer = System.Diagnostics.Stopwatch.StartNew();
                    CreateHierarchicalRows();
                    hierarchyTimer.Stop();
                    Console.WriteLine($"✓ Création hiérarchie: {hierarchyTimer.ElapsedMilliseconds} ms");
                }
                
                totalTimer.Stop();
                Console.WriteLine($"");
                Console.WriteLine($"⏱️  TEMPS TOTAL: {totalTimer.ElapsedMilliseconds} ms ({totalTimer.Elapsed.TotalSeconds:F2}s)");
                Console.WriteLine($"📊 Mémoire: ~{GC.GetTotalMemory(false) / 1024 / 1024} MB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void CreateHierarchicalRows()
        {
            var hierarchyTimer = System.Diagnostics.Stopwatch.StartNew();
            
            _allClientRows.Clear();
            _allClientRows.Capacity = Clients.Count;
            var totalAdresses = 0;
            
            // Traitement parallèle pour créer les lignes (beaucoup plus rapide)
            var clientArray = Clients.ToArray();
            var rows = new DataGridRow[clientArray.Length];
            
            System.Threading.Tasks.Parallel.For(0, clientArray.Length, i =>
            {
                var client = clientArray[i];
                
                // Index de recherche lazy (on le créera seulement si nécessaire lors de la recherche)
                // Pour le démarrage, on ne crée PAS l'index - gain énorme !
                
                // Ligne parent (client)
                rows[i] = new DataGridRow
                {
                    Id = client.Id.ToString(),
                    Nom = client.Nom,
                    Email = client.Email,
                    Details = $"📊 {client.Adresses.Count} adresse(s)",
                    IsParent = true,
                    Level = 0,
                    IsExpanded = false,
                    Parent = null,
                    Tag = client,
                    SearchIndex = string.Empty // Lazy - sera créé à la première recherche
                };
                
                rows[i].Children = new List<DataGridRow>(client.Adresses.Count);
                System.Threading.Interlocked.Add(ref totalAdresses, client.Adresses.Count);
            });
            
            // Ajouter toutes les lignes
            _allClientRows.AddRange(rows);
            
            hierarchyTimer.Stop();
            Console.WriteLine($"Hiérarchie créée: {_allClientRows.Count} clients + {totalAdresses} adresses (lazy load + lazy index) en cache ({hierarchyTimer.ElapsedMilliseconds} ms)");
            
            // Appliquer le filtre initial en arrière-plan pour ne pas bloquer l'UI
            System.Threading.Tasks.Task.Run(() =>
            {
                System.Threading.Thread.Sleep(100); // Laisser l'UI se charger d'abord
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyFilterInitial());
            });
        }
        
        private void ApplyFilterInitial()
        {
            var initialTimer = System.Diagnostics.Stopwatch.StartNew();
            
            // Affichage ultra-rapide : seulement 500 clients au départ
            const int initialDisplayCount = 500;
            const int batchSize = 2000;
            
            // Créer une nouvelle collection avec les premiers clients (plus rapide)
            var initialRows = _allClientRows.Take(initialDisplayCount).ToList();
            Rows = new ObservableCollection<DataGridRow>(initialRows);
            
            FilteredCount = _allClientRows.Count;
            initialTimer.Stop();
            Console.WriteLine($"✓ Affichage initial: {initialDisplayCount}/{_allClientRows.Count} clients affichés ({initialTimer.ElapsedMilliseconds} ms) - UI prête");
            
            // Charger le reste en arrière-plan de manière progressive
            if (_allClientRows.Count > initialDisplayCount)
            {
                LoadRemainingBatchesAsync(initialDisplayCount, batchSize);
            }
            
            // Pré-calculer les index de recherche en arrière-plan (priorité basse)
            BuildSearchIndexesAsync();
        }
        
        private async void LoadRemainingBatchesAsync(int startIndex, int batchSize)
        {
            // Attendre un peu pour laisser l'UI s'initialiser complètement
            await System.Threading.Tasks.Task.Delay(200);
            
            await System.Threading.Tasks.Task.Run(async () =>
            {
                var currentIndex = startIndex;
                
                while (currentIndex < _allClientRows.Count)
                {
                    var batchEnd = Math.Min(currentIndex + batchSize, _allClientRows.Count);
                    var batch = _allClientRows.GetRange(currentIndex, batchEnd - currentIndex);
                    
                    // Reconstruire la collection au lieu d'ajouter un par un (beaucoup plus rapide)
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var newRows = new List<DataGridRow>(Rows);
                        newRows.AddRange(batch);
                        Rows = new ObservableCollection<DataGridRow>(newRows);
                    });
                    
                    currentIndex = batchEnd;
                    
                    // Petite pause pour laisser l'UI respirer
                    await System.Threading.Tasks.Task.Delay(50);
                }
                
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Console.WriteLine($"✓ Chargement complet: {_allClientRows.Count} clients affichés");
                });
            });
        }
        
        private async void BuildSearchIndexesAsync()
        {
            var indexTimer = System.Diagnostics.Stopwatch.StartNew();
            
            await System.Threading.Tasks.Task.Run(() =>
            {
                // Construire les index en parallèle en arrière-plan
                System.Threading.Tasks.Parallel.ForEach(_allClientRows, clientRow =>
                {
                    if (string.IsNullOrEmpty(clientRow.SearchIndex))
                    {
                        var client = (Client)clientRow.Tag!;
                        var sb = new System.Text.StringBuilder(256);
                        sb.Append(client.Id).Append(' ')
                          .Append(client.Nom.ToLowerInvariant()).Append(' ')
                          .Append(client.Email.ToLowerInvariant());
                        
                        foreach (var addr in client.Adresses)
                        {
                            sb.Append(' ').Append(addr.Type.ToLowerInvariant())
                              .Append(' ').Append(addr.Rue.ToLowerInvariant())
                              .Append(' ').Append(addr.Ville.ToLowerInvariant())
                              .Append(' ').Append(addr.CodePostal);
                        }
                        clientRow.SearchIndex = sb.ToString();
                    }
                });
                
                _isIndexBuilt = true;
            });
            
            indexTimer.Stop();
            Console.WriteLine($"✓ Index de recherche construit: {_allClientRows.Count} clients indexés ({indexTimer.ElapsedMilliseconds} ms)");
        }

        private async void ApplyFilter()
        {
            // Annuler la recherche précédente si elle est encore en cours
            _searchCancellation?.Cancel();
            _searchCancellation = new System.Threading.CancellationTokenSource();
            var token = _searchCancellation.Token;
            
            var searchLower = SearchText?.ToLowerInvariant() ?? string.Empty;
            var hasSearch = !string.IsNullOrWhiteSpace(searchLower);
            
            if (!hasSearch)
            {
                // Pas de recherche, réassigner toute la collection (plus rapide que Clear + Add)
                var newRows = new ObservableCollection<DataGridRow>(_allClientRows);
                Rows = newRows;
                FilteredCount = _allClientRows.Count;
                return;
            }
            
            // Recherche asynchrone pour ne pas bloquer l'UI
            try
            {
                var searchTimer = System.Diagnostics.Stopwatch.StartNew();
                
                var matchingRows = await System.Threading.Tasks.Task.Run(() =>
                {
                    // Recherche séquentielle simple (plus rapide que Parallel pour la recherche)
                    var matches = new List<DataGridRow>();
                    
                    foreach (var clientRow in _allClientRows)
                    {
                        if (token.IsCancellationRequested)
                            break;
                        
                        // Si l'index n'est pas encore construit, faire une recherche simple sur les champs principaux
                        if (!_isIndexBuilt || string.IsNullOrEmpty(clientRow.SearchIndex))
                        {
                            var client = (Client)clientRow.Tag!;
                            if (client.Nom.ToLowerInvariant().Contains(searchLower) ||
                                client.Email.ToLowerInvariant().Contains(searchLower) ||
                                client.Id.ToString().Contains(searchLower))
                            {
                                matches.Add(clientRow);
                            }
                        }
                        else
                        {
                            // Utiliser l'index pré-calculé
                            if (clientRow.SearchIndex.Contains(searchLower))
                            {
                                matches.Add(clientRow);
                            }
                        }
                    }
                    
                    return matches;
                }, token);
                
                if (token.IsCancellationRequested)
                    return;
                
                // Remplacer la collection entière au lieu de Clear + Add (beaucoup plus rapide!)
                var newRows = new ObservableCollection<DataGridRow>(matchingRows);
                Rows = newRows;
                FilteredCount = matchingRows.Count;
                
                searchTimer.Stop();
                
                var indexStatus = _isIndexBuilt ? "avec index" : "sans index";
                Console.WriteLine($"🔍 Recherche '{SearchText}' ({indexStatus}): {matchingRows.Count}/{_allClientRows.Count} clients ({searchTimer.ElapsedMilliseconds} ms)");
            }
            catch (System.OperationCanceledException)
            {
                // Recherche annulée, c'est normal
            }
        }

        private void UpdateChildrenVisibility(DataGridRow parentRow)
        {
            if (parentRow.IsExpanded)
            {
                // Ajouter les enfants après le parent (chargement paresseux)
                var parentIndex = Rows.IndexOf(parentRow);
                if (parentIndex == -1) return;
                
                foreach (var child in parentRow.Children)
                {
                    if (!Rows.Contains(child))
                    {
                        Rows.Insert(++parentIndex, child);
                    }
                }
                Console.WriteLine($"Expanded: {parentRow.Children.Count} adresses affichées pour {parentRow.Nom}");
            }
            else
            {
                // Retirer les enfants (libérer la mémoire visuelle)
                var removedCount = 0;
                foreach (var child in parentRow.Children.ToList())
                {
                    if (Rows.Remove(child))
                    {
                        removedCount++;
                    }
                }
                Console.WriteLine($"Collapsed: {removedCount} adresses masquées pour {parentRow.Nom}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
