using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GRoute.Desktop.Controls;

public static class AppCursors
{
    private static Cursor? _arrow;
    private static Cursor? _hand;

    public static Cursor Arrow => _arrow ??= Load("cursor-arrow.png", StandardCursorType.Arrow);

    public static Cursor Hand => _hand ??= Load("cursor-hand.png", StandardCursorType.Hand);

    private static Cursor Load(string file, StandardCursorType fallback)
    {
        try
        {
            var uri = new Uri("avares://GRoute/Assets/" + file);
            using var stream = AssetLoader.Open(uri);
            return new Cursor(new Bitmap(stream), new PixelPoint(3, 2));
        }
        catch
        {
            return new Cursor(fallback);
        }
    }
}
