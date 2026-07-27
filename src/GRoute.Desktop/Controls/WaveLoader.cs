using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace GRoute.Desktop.Controls;

public sealed class WaveLoader : Control
{
    public static readonly StyledProperty<IBrush> BarBrushProperty =
        AvaloniaProperty.Register<WaveLoader, IBrush>(nameof(BarBrush),
            new ImmutableSolidColorBrush(Color.FromRgb(0x5B, 0x83, 0xD6)));

    public static readonly StyledProperty<int> BarsProperty =
        AvaloniaProperty.Register<WaveLoader, int>(nameof(Bars), 4);

    public IBrush BarBrush
    {
        get => GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public int Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    private const double BarW = 10.0;
    private const double Gap = 5.0;
    private const double MinH = 5.0;
    private const double MaxH = 25.0;
    private const double PeriodMs = 1000.0;
    private const double DelayMs = 100.0;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TopLevel? _top;
    private bool _hooked;

    public WaveLoader()
    {
        IsHitTestVisible = false;
        Height = MaxH;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int n = Math.Max(1, Bars);
        return new Size(n * BarW + (n - 1) * Gap, MaxH);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_hooked || _top is null) return;
        _hooked = true;
        _top.RequestAnimationFrame(OnFrame);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _hooked = false;
        _top = null;
    }

    private void OnFrame(TimeSpan _)
    {
        if (!_hooked || _top is null) return;
        if (IsEffectivelyVisible) InvalidateVisual();
        _top.RequestAnimationFrame(OnFrame);
    }

    public override void Render(DrawingContext context)
    {
        int n = Math.Max(1, Bars);
        double now = _clock.Elapsed.TotalMilliseconds;
        double bottom = Bounds.Height;

        for (int i = 0; i < n; i++)
        {
            double t = (now - i * DelayMs) % PeriodMs;
            if (t < 0) t += PeriodMs;
            double phase = t / PeriodMs;
            double wave = phase < 0.5 ? phase * 2.0 : (1.0 - phase) * 2.0;
            double eased = wave * wave * (3.0 - 2.0 * wave);
            double h = MinH + (MaxH - MinH) * eased;

            double x = i * (BarW + Gap);
            var rect = new Rect(x, bottom - h, BarW, h);
            context.DrawRectangle(BarBrush, null, new RoundedRect(rect, BarW / 2.0));
        }
    }
}
