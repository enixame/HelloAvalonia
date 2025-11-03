namespace HelloAvalonia.Services;

/// <summary>
/// Message envoyé quand les données sont chargées
/// </summary>
public class DataLoadedMessage
{
    public int ClientCount { get; set; }
    public int AddressCount { get; set; }
}
