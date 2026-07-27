using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GRoute.Desktop.Controls;

public static class PingPalette
{
    public static readonly Color Grey = Color.FromRgb(0x8A, 0x97, 0xAD);
    public static readonly Color Red = Color.FromRgb(0xE0, 0x57, 0x5C);
    public static readonly Color Orange = Color.FromRgb(0xE5, 0xA6, 0x3C);
    public static readonly Color Green = Color.FromRgb(0x3C, 0xCB, 0x7F);

    public static Color For(int ping)
    {
        if (ping == int.MinValue || ping == -2 || ping == -1) return Grey;
        if (ping < 250) return Green;
        if (ping < 700) return Orange;
        return Red;
    }
}

public sealed class PingBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var c = PingPalette.For(value is int i ? i : int.MinValue);
        byte a = (parameter as string) switch
        {
            "bg" => 0x22,
            "border" => 0x8A,
            _ => 0xFF
        };
        return new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PingLabelConverter : IValueConverter
{
    public static bool Farsi;

    private static string Digits(string s)
    {
        if (!Farsi) return s;
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] >= '0' && chars[i] <= '9')
                chars[i] = (char)(chars[i] - '0' + 0x06F0);
        return new string(chars);
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int p = value is int i ? i : int.MinValue;
        if (p == int.MinValue) return "—";
        if (p == -2) return "…";
        if (p == -1) return Farsi ? "ناموفق" : "timeout";
        return Digits(p.ToString()) + (Farsi ? " میلی‌ثانیه" : " ms");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PingBoxVisConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int p = value is int i ? i : int.MinValue;
        return p != int.MinValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CheckBorderConverter : IValueConverter
{
    private static readonly SolidColorBrush On = new(Color.FromRgb(0x4B, 0x8C, 0xF0));
    private static readonly SolidColorBrush Off = new(Colors.Transparent);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? On : Off;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
