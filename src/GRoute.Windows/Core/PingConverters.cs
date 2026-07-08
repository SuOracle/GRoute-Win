using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GRoute.Windows;

public static class PingPalette
{
    public static readonly System.Windows.Media.Color Grey = System.Windows.Media.Color.FromRgb(0x8A, 0x97, 0xAD);
    public static readonly System.Windows.Media.Color Red = System.Windows.Media.Color.FromRgb(0xE0, 0x57, 0x5C);
    public static readonly System.Windows.Media.Color Orange = System.Windows.Media.Color.FromRgb(0xE5, 0xA6, 0x3C);
    public static readonly System.Windows.Media.Color Green = System.Windows.Media.Color.FromRgb(0x3C, 0xCB, 0x7F);

    public static System.Windows.Media.Color For(int ping)
    {
        if (ping == int.MinValue || ping == -2 || ping == -1) return Grey;
        if (ping < 250) return Green;
        if (ping < 700) return Orange;
        return Red;
    }
}

public sealed class PingBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var c = PingPalette.For(value is int i ? i : int.MinValue);
        byte a = (parameter as string) switch
        {
            "bg" => 0x22,
            "border" => 0x8A,
            _ => 0xFF
        };
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, c.R, c.G, c.B));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class PingGlowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => PingPalette.For(value is int i ? i : int.MinValue);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class PingLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int i = value is int v ? v : int.MinValue;
        if (i == int.MinValue) return "";
        if (i == -2) return "\u2026";
        if (i == -1) return "fail";
        return i + " ms";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class PingBoxVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is int i && i != int.MinValue) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
