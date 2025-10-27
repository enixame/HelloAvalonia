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
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                loadTimer.Stop();
                Console.WriteLine($"✓ Chargement fichier: {loadTimer.ElapsedMilliseconds} ms ({jsonContent.Length / 1024} KB)");
                
                var deserializeTimer = System.Diagnostics.Stopwatch.StartNew();
                var clients = JsonSerializer.Deserialize<Client[]>(jsonContent);
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
            var totalAdresses = 0;
            
            foreach (var client in Clients)
            {
                // Créer l'index de recherche pré-calculé (une seule fois)
                var searchTerms = new List<string>
                {
                    client.Id.ToString(),
                    client.Nom.ToLower(),
                    client.Email.ToLower()
                };
                
                // Ajouter les termes des adresses
                foreach (var adresse in client.Adresses)
                {
                    searchTerms.Add(adresse.Type.ToLower());
                    searchTerms.Add(adresse.Rue.ToLower());
                    searchTerms.Add(adresse.Ville.ToLower());
                    searchTerms.Add(adresse.CodePostal);
                }
                
                var searchIndex = string.Join(" ", searchTerms);
                
                // Ligne parent (client)
                var clientRow = new DataGridRow
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
                    SearchIndex = searchIndex // Index pré-calculé
                };
                _allClientRows.Add(clientRow);

                // Pré-créer les lignes enfants
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
                        Parent = clientRow,
                        Tag = adresse
                    };
                    clientRow.Children.Add(adresseRow);
                    totalAdresses++;
                }

                // Écouter les changements d'expansion
                clientRow.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(DataGridRow.IsExpanded))
                    {
                        UpdateChildrenVisibility(clientRow);
                    }
                };
            }
            
            hierarchyTimer.Stop();
            Console.WriteLine($"Hiérarchie créée: {_allClientRows.Count} clients + {totalAdresses} adresses en cache ({hierarchyTimer.ElapsedMilliseconds} ms)");
            
            // Appliquer le filtre initial
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var searchTimer = System.Diagnostics.Stopwatch.StartNew();
            
            Rows.Clear();
            var searchLower = SearchText?.ToLower() ?? string.Empty;
            var hasSearch = !string.IsNullOrWhiteSpace(searchLower);
            
            if (hasSearch)
            {
                // Recherche optimisée avec index pré-calculé
                foreach (var clientRow in _allClientRows)
                {
                    // Une seule opération Contains sur l'index
                    if (clientRow.SearchIndex.Contains(searchLower))
                    {
                        Rows.Add(clientRow);
                    }
                }
            }
            else
            {
                // Pas de recherche, afficher tous les clients
                foreach (var clientRow in _allClientRows)
                {
                    Rows.Add(clientRow);
                }
            }
            
            FilteredCount = Rows.Count;
            searchTimer.Stop();
            
            if (hasSearch)
            {
                Console.WriteLine($"🔍 Recherche '{SearchText}': {Rows.Count}/{_allClientRows.Count} clients ({searchTimer.ElapsedMilliseconds} ms)");
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
