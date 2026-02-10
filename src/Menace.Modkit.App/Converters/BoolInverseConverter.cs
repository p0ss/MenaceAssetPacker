using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Menace.Modkit.App.Converters;

/// <summary>
/// Inverts a boolean value. Used for visibility bindings when IsSearching should hide tree view.
/// </summary>
public class BoolInverseConverter : IValueConverter
{
    public static readonly BoolInverseConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
