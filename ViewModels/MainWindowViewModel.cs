using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using HelloAvalonia.Models;
using Avalonia.Threading;

namespace HelloAvalonia.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // -------- Mini collection pour AddRange avec 1 seule notif (reset) --------
        private sealed class BulkObservableCollection<T> : ObservableCollection<T>
        {
            public BulkObservableCollection()
            {
            }

            public BulkObservableCollection(IEnumerable<T> collection) 
            : base(collection)
            {
            }

            public void AddRange(IEnumerable<T> items)
            {
                if (items is null) return;
                var any = false;
                foreach (var it in items) { Items.Add(it); any = true; }
                if (any) OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                    System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
            }
        }

        private BulkObservableCollection<DataGridRow> _rows = new();
        private ObservableCollection<Client> _clients = new();
        private readonly List<DataGridRow> _allClientRows = new(); // cache parents
        private string _searchText = string.Empty;
        private int _filteredCount;

        // OPT: une seule CTS “maître” + sous-CTS
        private CancellationTokenSource _rootCts = new();
        private CancellationTokenSource? _searchDebounceCts;
        private CancellationTokenSource? _searchCts;
        private CancellationTokenSource? _indexCts;
        private CancellationTokenSource? _batchCts;

        private volatile bool _isIndexBuilt;

        public ObservableCollection<Client> Clients
        {
            get => _clients;
            private set { _clients = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DataGridRow> Rows
        {
            get => _rows;
            private set
            {
                if (value is BulkObservableCollection<DataGridRow> b)
                    _rows = b;
                else
                    _rows = new BulkObservableCollection<DataGridRow>(value);
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();

                // OPT: debounce via CTS + Task.Delay annulable (plus sûr que Timer)
                _searchDebounceCts?.Cancel();
                _searchDebounceCts = CancellationTokenSource.CreateLinkedTokenSource(_rootCts.Token);
                _ = DebouncedApplyFilterAsync(_searchDebounceCts.Token);
            }
        }

        public int FilteredCount
        {
            get => _filteredCount;
            private set { _filteredCount = value; OnPropertyChanged(); }
        }

        public ICommand ToggleExpandCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public MainWindowViewModel()
        {
            ToggleExpandCommand = new RelayCommand<DataGridRow>(ToggleExpand);
            ClearSearchCommand = new RelayCommand<object>(_ => SearchText = string.Empty);

            _ = LoadClientsAsync(_rootCts.Token);
        }

        private void ToggleExpand(DataGridRow? row)
        {
            if (row is null || !row.IsParent) return;

            row.IsExpanded = !row.IsExpanded;

            if (row.IsExpanded && row.Children.Count == 0)
            {
                var client = (Client)row.Tag!;
                foreach (var adresse in client.Adresses)
                {
                    var child = new DataGridRow
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
                    row.Children.Add(child);
                }
            }

            UpdateChildrenVisibility(row);
        }

        private async Task LoadClientsAsync(CancellationToken ct)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "clients.json");
                if (!File.Exists(jsonPath))
                    return;

                // OPT: options d’I/O
                var loadSw = System.Diagnostics.Stopwatch.StartNew();
                await using var fileStream = new FileStream(
                    jsonPath,
                    FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 1 << 16,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan);
                loadSw.Stop();

                var deserSw = System.Diagnostics.Stopwatch.StartNew();
                var clients = await JsonSerializer.DeserializeAsync<Client[]>(fileStream, cancellationToken: ct)
                               .ConfigureAwait(false);
                deserSw.Stop();

                if (ct.IsCancellationRequested) return;

                if (clients is { Length: > 0 })
                {
                    var addSw = System.Diagnostics.Stopwatch.StartNew();
                    var tmp = new ObservableCollection<Client>(clients);
                    await Dispatcher.UIThread.InvokeAsync(() => Clients = tmp);
                    addSw.Stop();

                    var hierSw = System.Diagnostics.Stopwatch.StartNew();
                    CreateHierarchicalRows(); // construit _allClientRows (parents only)
                    hierSw.Stop();
                }

                totalSw.Stop();
            }
            catch (OperationCanceledException) { /* normal */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur chargement: {ex}");
            }

            // Affichage initial et pré-chargements
            ApplyFilterInitial();
            _ = BuildSearchIndexesAsync(); // volontairement fire&forget mais en Task
        }

        private void CreateHierarchicalRows()
        {
            _allClientRows.Clear();
            _allClientRows.Capacity = Math.Max(_allClientRows.Capacity, Clients.Count);

            var clientArray = Clients.ToArray();
            var rows = new DataGridRow[clientArray.Length];
            int totalAdresses = 0;

            Parallel.For(0, clientArray.Length, i =>
            {
                var c = clientArray[i];
                var r = new DataGridRow
                {
                    Id = c.Id.ToString(),
                    Nom = c.Nom,
                    Email = c.Email,
                    Details = $"📊 {c.Adresses.Count} adresse(s)",
                    IsParent = true,
                    Level = 0,
                    IsExpanded = false,
                    Parent = null,
                    Tag = c,
                    SearchIndex = string.Empty
                };
                r.Children = new List<DataGridRow>(c.Adresses.Count);
                rows[i] = r;
                Interlocked.Add(ref totalAdresses, c.Adresses.Count);
            });

            _allClientRows.AddRange(rows);
        }

        private void ApplyFilterInitial()
        {
            const int initialDisplayCount = 500;

            var initialRows = _allClientRows.Take(initialDisplayCount).ToList();
            Rows = new BulkObservableCollection<DataGridRow>();
            _rows.AddRange(initialRows);

            FilteredCount = _allClientRows.Count;

            // OPT: démarrer le chargement progressif avec CTS dédiée
            _batchCts?.Cancel();
            _batchCts = CancellationTokenSource.CreateLinkedTokenSource(_rootCts.Token);
            _ = LoadRemainingBatchesAsync(initialDisplayCount, batchSize: 2000, _batchCts.Token);
        }

        private async Task LoadRemainingBatchesAsync(int startIndex, int batchSize, CancellationToken ct)
        {
            try
            {
                await Task.Delay(200, ct);
                var current = startIndex;

                // AddRange par batch (une notification reset / batch)
                while (current < _allClientRows.Count && !ct.IsCancellationRequested)
                {
                    var end = Math.Min(current + batchSize, _allClientRows.Count);
                    var slice = _allClientRows.GetRange(current, end - current);
                    await Dispatcher.UIThread.InvokeAsync(() => _rows.AddRange(slice));
                    current = end;

                    await Task.Delay(50, ct);
                }
            }
            catch (OperationCanceledException) { /* normal */ }
        }

        private async Task BuildSearchIndexesAsync()
        {
            _indexCts?.Cancel();
            _indexCts = CancellationTokenSource.CreateLinkedTokenSource(_rootCts.Token);
            var token = _indexCts.Token;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(_allClientRows, new ParallelOptions { CancellationToken = token }, row =>
                    {
                        if (token.IsCancellationRequested) return;
                        if (!string.IsNullOrEmpty(row.SearchIndex)) return;

                        var client = (Client)row.Tag!;
                        var sb = new StringBuilder(256);
                        sb.Append(client.Id).Append(' ')
                          .Append(client.Nom).Append(' ')
                          .Append(client.Email);

                        foreach (var a in client.Adresses)
                        {
                            sb.Append(' ').Append(a.Type)
                              .Append(' ').Append(a.Rue)
                              .Append(' ').Append(a.Ville)
                              .Append(' ').Append(a.CodePostal);
                        }

                        // Stocke en minuscule une seule fois
                        row.SearchIndex = sb.ToString().ToLowerInvariant();
                    });

                    _isIndexBuilt = true;
                }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* normal */ }
            finally
            {
                sw.Stop();
            }
        }

        private async Task DebouncedApplyFilterAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(300, ct); // debounce
                if (!ct.IsCancellationRequested)
                    await ApplyFilterAsync();
            }
            catch (OperationCanceledException) { /* normal */ }
        }

        private async Task ApplyFilterAsync()
        {
            _searchCts?.Cancel();
            _searchCts = CancellationTokenSource.CreateLinkedTokenSource(_rootCts.Token);
            var token = _searchCts.Token;

            var query = SearchText?.Trim();
            var hasSearch = !string.IsNullOrEmpty(query);
            if (!hasSearch)
            {
                // reset complet → une seule notif
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Rows = new BulkObservableCollection<DataGridRow>();
                    _rows.AddRange(_allClientRows);
                    FilteredCount = _allClientRows.Count;
                });
                return;
            }

            var lower = query!.ToLowerInvariant();

            try
            {
                var matches = await Task.Run(() =>
                {
                    var list = new List<DataGridRow>(capacity: Math.Min(_allClientRows.Count, 4096));

                    foreach (var row in _allClientRows)
                    {
                        if (token.IsCancellationRequested) break;

                        if (_isIndexBuilt && !string.IsNullOrEmpty(row.SearchIndex))
                        {
                            if (row.SearchIndex.IndexOf(lower, StringComparison.Ordinal) >= 0)
                                list.Add(row);
                        }
                        else
                        {
                            var c = (Client)row.Tag!;
                            if ((c.Nom?.IndexOf(lower, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                                (c.Email?.IndexOf(lower, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                                c.Id.ToString().IndexOf(lower, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                list.Add(row);
                            }
                        }
                    }

                    return list;
                }, token).ConfigureAwait(false);

                if (token.IsCancellationRequested) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Rows = new BulkObservableCollection<DataGridRow>();
                    _rows.AddRange(matches);
                    FilteredCount = matches.Count;
                });
            }
            catch (OperationCanceledException) { /* normal */ }
        }

        private void UpdateChildrenVisibility(DataGridRow parentRow)
        {
            if (parentRow.IsExpanded)
            {
                var parentIndex = _rows.IndexOf(parentRow);
                if (parentIndex < 0) return;

                // OPT: on suppose qu’ils ne sont pas déjà visibles (pas de Contains O(n))
                var insertionIndex = parentIndex;
                foreach (var child in parentRow.Children)
                {
                    _rows.Insert(++insertionIndex, child);
                }
            }
            else
            {
                // Retirer tous les enfants visibles du parent
                foreach (var child in parentRow.Children)
                {
                    _rows.Remove(child); // Remove O(n) mais #enfants est limité
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Appeler lors de la fermeture fenêtre/VM pour annuler les tâches en cours
        public void Dispose()
        {
            _rootCts.Cancel();
            _rootCts.Dispose();
            _searchDebounceCts?.Cancel(); _searchDebounceCts?.Dispose();
            _searchCts?.Cancel(); _searchCts?.Dispose();
            _indexCts?.Cancel(); _indexCts?.Dispose();
            _batchCts?.Cancel(); _batchCts?.Dispose();
        }
    }
}
