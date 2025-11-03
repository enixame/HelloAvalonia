namespace HelloAvalonia.Services;

/// <summary>
/// Résultat du chargement des données
/// </summary>
public class LoadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ClientCount { get; set; }
    public int TotalAddresses { get; set; }
    public long LoadTime { get; set; }
    public long RowCreationTime { get; set; }
    public long TotalTime { get; set; }
}
