using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace GRoute.Desktop.Controls;

public sealed class GridGlow : Control
{
    public static readonly StyledProperty<double> CellProperty =
        AvaloniaProperty.Register<GridGlow, double>(nameof(Cell), 15.0);

    public static readonly StyledProperty<Color> MainColorProperty =
        AvaloniaProperty.Register<GridGlow, Color>(nameof(MainColor), Color.FromRgb(0x2E, 0xD5, 0x73));

    public double Cell
    {
        get => GetValue(CellProperty);
        set => SetValue(CellProperty, value);
    }

    public Color MainColor
    {
        get => GetValue(MainColorProperty);
        set => SetValue(MainColorProperty, value);
    }

    static GridGlow()
    {
        AffectsRender<GridGlow>(CellProperty, MainColorProperty);
    }

    public GridGlow()
    {
        IsHitTestVisible = false;
        Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = CellProperty,
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new CubicEaseInOut()
            }
        };
    }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var c = MainColor;
        var full = new Rect(0, 0, w, h);

        var fade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.22),
                new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.78),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
            }
        };
        using var _mask = context.PushOpacityMask(fade, full);

        var lineBrush = new ImmutableSolidColorBrush(Color.FromArgb(19, c.R, c.G, c.B));
        var pen = new ImmutablePen(lineBrush, 1.0);
        double step = Math.Max(4.0, Cell);

        double cx = w / 2.0;
        double cy = h / 2.0;
        for (double x = cx; x < w; x += step) context.DrawLine(pen, new Point(x, 0), new Point(x, h));
        for (double x = cx - step; x > 0; x -= step) context.DrawLine(pen, new Point(x, 0), new Point(x, h));
        for (double y = cy; y < h; y += step) context.DrawLine(pen, new Point(0, y), new Point(w, y));
        for (double y = cy - step; y > 0; y -= step) context.DrawLine(pen, new Point(0, y), new Point(w, y));

        var glow = new RadialGradientBrush
        {
            Center = RelativePoint.Center,
            GradientOrigin = RelativePoint.Center,
            RadiusX = new RelativeScalar(0.6, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(92, c.R, c.G, c.B), 0),
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0.95)
            }
        };
        context.FillRectangle(glow, full);

        var edge = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0),
                new GradientStop(Color.FromArgb(255, c.R, c.G, c.B), 0.5),
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1)
            }
        };
        context.FillRectangle(edge, new Rect(0, 0, w, 1));
        context.FillRectangle(edge, new Rect(0, h - 1, w, 1));
    }
}
