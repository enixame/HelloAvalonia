using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HelloAvalonia.Models;
using HelloAvalonia.ViewModels;

namespace HelloAvalonia.Services;

/// <summary>
/// Service responsable du chargement et de la gestion des données clients
/// </summary>
public class ClientDataService
{
    private List<Client> _clients = new();
    private List<DataGridRow> _clientRows = new();
    private bool _isLoaded = false;

    /// <summary>
    /// Tous les clients chargés
    /// </summary>
    public IReadOnlyList<Client> Clients => _clients.AsReadOnly();

    /// <summary>
    /// Toutes les lignes DataGrid (cache)
    /// </summary>
    public IReadOnlyList<DataGridRow> ClientRows => _clientRows.AsReadOnly();

    /// <summary>
    /// Indique si les données sont chargées
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Charge les clients depuis le fichier JSON
    /// </summary>
    public async Task<LoadResult> LoadClientsAsync()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var result = new LoadResult();

        try
        {
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "clients.json");
            
            if (!File.Exists(jsonPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Fichier non trouvé: {jsonPath}";
                return result;
            }

            // Chargement et désérialisation
            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            using var fileStream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true);
            var clients = await JsonSerializer.DeserializeAsync<Client[]>(fileStream);
            loadTimer.Stop();

            if (clients == null || clients.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Aucun client trouvé dans le fichier";
                return result;
            }

            _clients = clients.ToList();
            result.ClientCount = _clients.Count;
            result.LoadTime = loadTimer.ElapsedMilliseconds;

            // Créer les lignes DataGrid en parallèle
            var rowTimer = System.Diagnostics.Stopwatch.StartNew();
            CreateDataGridRows();
            rowTimer.Stop();

            result.RowCreationTime = rowTimer.ElapsedMilliseconds;
            result.TotalAddresses = _clients.Sum(c => c.Adresses.Count);
            
            _isLoaded = true;
            result.Success = true;

            timer.Stop();
            result.TotalTime = timer.ElapsedMilliseconds;

            Console.WriteLine($"✓ Chargement: {result.ClientCount} clients, {result.TotalAddresses} adresses ({result.LoadTime}ms)");
            Console.WriteLine($"✓ Création lignes: {result.RowCreationTime}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"Erreur chargement: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Crée les lignes DataGrid en parallèle
    /// </summary>
    private void CreateDataGridRows()
    {
        _clientRows.Clear();
        var rows = new DataGridRow[_clients.Count];

        Parallel.For(0, _clients.Count, i =>
        {
            var client = _clients[i];
            rows[i] = new DataGridRow
            {
                Id = client.Id.ToString(),
                Nom = client.Nom,
                Email = client.Email,
                Details = $"📊 {client.Adresses.Count} adresse(s)",
                IsParent = true,
                Level = 0,
                IsExpanded = false,
                Tag = client,
                SearchIndex = string.Empty // Sera créé par SearchService
            };
            rows[i].Children = new List<DataGridRow>(client.Adresses.Count);
        });

        _clientRows = rows.ToList();
    }

    /// <summary>
    /// Obtient un client par son ID
    /// </summary>
    public Client? GetClientById(int id)
    {
        return _clients.FirstOrDefault(c => c.Id == id);
    }

    /// <summary>
    /// Crée les adresses (lazy loading) pour un client
    /// </summary>
    public void ExpandClientRow(DataGridRow clientRow)
    {
        if (clientRow.Children.Count > 0 || !clientRow.IsParent)
            return;

        var client = (Client)clientRow.Tag!;
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
                Parent = clientRow,
                Tag = adresse
            };
            clientRow.Children.Add(adresseRow);
        }
    }
}
