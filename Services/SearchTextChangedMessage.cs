namespace HelloAvalonia.Services;

/// <summary>
/// Message envoyé quand la recherche change
/// </summary>
public class SearchTextChangedMessage
{
    public string SearchText { get; set; } = string.Empty;
}
