using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HelloAvalonia.ViewModels;

/// <summary>
/// Classe représentant une ligne du DataGrid (Client ou Adresse)
/// </summary>
public class DataGridRow : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Id { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool IsParent { get; set; }
    public int Level { get; set; }
    public DataGridRow? Parent { get; set; }
    public object? Tag { get; set; }
    public List<DataGridRow> Children { get; set; } = new();
    
    /// <summary>
    /// Index de recherche pré-calculé pour optimisation des performances
    /// </summary>
    public string SearchIndex { get; set; } = string.Empty;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Marge gauche pour l'indentation hiérarchique
    /// </summary>
    public int LeftMargin => Level * 30;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
