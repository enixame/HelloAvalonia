using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HelloAvalonia.Converters;

/// <summary>
/// Convertisseur pour afficher ▼ ou ▶ selon l'état d'expansion
/// </summary>
public class BoolToExpanderIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "▼" : "▶";
        }
        return "▶";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
