using System.Windows;
using System.Windows.Data;

namespace SteamScrup.UI;

/// <summary>
/// True to Collapsed and false to Visible.
///
/// A separate class rather than a ConverterParameter on the normal converter: parameters
/// arriving as null made the "invert" behaviour silently indistinguishable from the plain
/// converter, which showed an empty-state message on top of a perfectly good chart.
/// </summary>
public sealed class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var flag = value is bool b && b;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Boolean to Visibility.</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var flag = value is bool b && b;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        Binding.DoNothing;
}
