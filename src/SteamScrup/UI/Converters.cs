using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SteamScrup.Core;

namespace SteamScrup.UI;

/// <summary>Maps a <see cref="Confidence"/> to a short localized label.</summary>
public sealed class ConfidenceLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            Confidence.Safe => "conf.safe",
            Confidence.Likely => "conf.likely",
            Confidence.Review => "conf.review",
            Confidence.Protected => "conf.protected",
            _ => "conf.review",
        };
        return L.T(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Maps a <see cref="Confidence"/> to the badge background brush key.</summary>
public sealed class ConfidenceBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            Confidence.Safe => "OkBg",
            Confidence.Likely => "InfoBg",
            Confidence.Review => "WarnBg",
            Confidence.Protected => "BgHover",
            _ => "InfoBg",
        };
        return Application.Current?.TryFindResource(key) ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Maps a <see cref="Confidence"/> to the badge foreground brush key.</summary>
public sealed class ConfidenceForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            Confidence.Safe => "OkFg",
            Confidence.Likely => "InfoFg",
            Confidence.Review => "WarnFg",
            Confidence.Protected => "FgMuted",
            _ => "InfoFg",
        };
        return Application.Current?.TryFindResource(key) ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Formats a byte count for display.</summary>
public sealed class SizeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes) return string.Empty;
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / 1048576.0:N1} MB",
            _ => $"{bytes / 1073741824.0:N2} GB",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Formats a timestamp for the Modified column.</summary>
public sealed class TimeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTimeOffset dto
            ? dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Turns a localized key into text, used for reason text inside rows.</summary>
public sealed class ReasonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string key || key.Length == 0) return string.Empty;
        var text = L.T(key);
        if (parameter is string detail && detail.Length > 0) return $"{text} · {detail}";
        return text;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
