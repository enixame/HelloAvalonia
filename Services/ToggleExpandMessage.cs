using HelloAvalonia.ViewModels;

namespace HelloAvalonia.Services;

/// <summary>
/// Message envoyé pour expand/collapse un client
/// </summary>
public class ToggleExpandMessage
{
    public DataGridRow? Row { get; set; }
}
