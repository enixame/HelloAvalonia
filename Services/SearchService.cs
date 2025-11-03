using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HelloAvalonia.Models;
using HelloAvalonia.ViewModels;

namespace HelloAvalonia.Services;

/// <summary>
/// Service responsable de l'indexation et de la recherche des clients
/// </summary>
public class SearchService
{
    private bool _isIndexBuilt = false;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _indexCancellation;

    /// <summary>
    /// Indique si l'index est construit
    /// </summary>
    public bool IsIndexBuilt => _isIndexBuilt;

    /// <summary>
    /// Construit l'index de recherche en arrière-plan
    /// </summary>
    public async Task BuildSearchIndexAsync(IEnumerable<DataGridRow> rows)
    {
        _indexCancellation?.Cancel();
        _indexCancellation = new CancellationTokenSource();
        var token = _indexCancellation.Token;

        var timer = System.Diagnostics.Stopwatch.StartNew();

        await Task.Run(() =>
        {
            Parallel.ForEach(rows, (clientRow, state) =>
            {
                if (token.IsCancellationRequested)
                {
                    state.Break();
                    return;
                }

                if (string.IsNullOrEmpty(clientRow.SearchIndex) && clientRow.Tag is Client client)
                {
                    clientRow.SearchIndex = BuildSearchIndexForClient(client);
                }
            });

            if (!token.IsCancellationRequested)
            {
                _isIndexBuilt = true;
            }
        }, token);

        timer.Stop();
        
        if (!token.IsCancellationRequested)
        {
            Console.WriteLine($"✓ Index de recherche: {rows.Count()} clients indexés ({timer.ElapsedMilliseconds}ms)");
        }
    }

    /// <summary>
    /// Construit l'index de recherche pour un client
    /// </summary>
    private string BuildSearchIndexForClient(Client client)
    {
        var sb = new StringBuilder(256);
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

        return sb.ToString();
    }

    /// <summary>
    /// Recherche des clients par texte
    /// </summary>
    public async Task<List<DataGridRow>> SearchAsync(string searchText, IEnumerable<DataGridRow> allRows)
    {
        // Annuler la recherche précédente
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        var token = _searchCancellation.Token;

        var searchLower = searchText.ToLowerInvariant();
        var timer = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var results = await Task.Run(() =>
            {
                var matches = new List<DataGridRow>();

                foreach (var clientRow in allRows)
                {
                    if (token.IsCancellationRequested)
                        break;

                    if (MatchesSearch(clientRow, searchLower))
                    {
                        matches.Add(clientRow);
                    }
                }

                return matches;
            }, token);

            timer.Stop();
            
            if (!token.IsCancellationRequested)
            {
                var indexStatus = _isIndexBuilt ? "avec index" : "sans index";
                Console.WriteLine($"🔍 Recherche '{searchText}' ({indexStatus}): {results.Count}/{allRows.Count()} clients ({timer.ElapsedMilliseconds}ms)");
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            return new List<DataGridRow>();
        }
    }

    /// <summary>
    /// Vérifie si une ligne correspond à la recherche
    /// </summary>
    private bool MatchesSearch(DataGridRow clientRow, string searchLower)
    {
        // Utiliser l'index si disponible
        if (_isIndexBuilt && !string.IsNullOrEmpty(clientRow.SearchIndex))
        {
            return clientRow.SearchIndex.Contains(searchLower);
        }

        // Recherche simple sur les champs principaux
        if (clientRow.Tag is Client client)
        {
            return client.Nom.ToLowerInvariant().Contains(searchLower) ||
                   client.Email.ToLowerInvariant().Contains(searchLower) ||
                   client.Id.ToString().Contains(searchLower);
        }

        return false;
    }

    /// <summary>
    /// Annule toutes les opérations en cours
    /// </summary>
    public void Cancel()
    {
        _searchCancellation?.Cancel();
        _indexCancellation?.Cancel();
    }
}
