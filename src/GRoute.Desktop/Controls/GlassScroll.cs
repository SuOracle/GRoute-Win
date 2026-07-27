using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace GRoute.Desktop.Controls;

public class GlassScroll : Control
{
    public static readonly StyledProperty<ScrollViewer?> TargetProperty =
        AvaloniaProperty.Register<GlassScroll, ScrollViewer?>(nameof(Target));

    public ScrollViewer? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    private const double HitWidth = 14.0;
    private const double BarWidth = 8.0;
    private const double MinThumb = 34.0;
    private const double LingerMs = 900.0;
    private const double FadeRate = 7.0;
    private const double HeatRate = 11.0;

    private static readonly ImmutableSolidColorBrush[] Ramp = BuildRamp();

    private ScrollViewer? _sv;
    private bool _hot;
    private bool _drag;
    private double _grab;

    private double _shown;
    private double _heat;
    private double _lastActivity = double.NegativeInfinity;
    private double _lastOffset = double.NaN;

    private TopLevel? _top;
    private bool _running;
    private double _lastFrame;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    static GlassScroll()
    {
        TargetProperty.Changed.AddClassHandler<GlassScroll>((c, e) => c.Bind(e.NewValue as ScrollViewer));
    }

    public GlassScroll()
    {
        Width = HitWidth;
        ClipToBounds = false;
    }

    private static ImmutableSolidColorBrush[] BuildRamp()
    {
        var a = new byte[] { 0x2B, 0x46, 0x74 };
        var b = new byte[] { 0x3D, 0x6A, 0xD6 };
        var c = new byte[] { 0x5B, 0x8A, 0xD6 };
        var ramp = new ImmutableSolidColorBrush[65];
        for (int i = 0; i < ramp.Length; i++)
        {
            double t = i / 32.0;
            byte[] lo = t <= 1.0 ? a : b;
            byte[] hi = t <= 1.0 ? b : c;
            double k = t <= 1.0 ? t : t - 1.0;
            ramp[i] = new ImmutableSolidColorBrush(Color.FromRgb(
                (byte)(lo[0] + (hi[0] - lo[0]) * k),
                (byte)(lo[1] + (hi[1] - lo[1]) * k),
                (byte)(lo[2] + (hi[2] - lo[2]) * k)));
        }
        return ramp;
    }

    private void Bind(ScrollViewer? sv)
    {
        if (_sv is not null) _sv.ScrollChanged -= OnScroll;
        _sv = sv;
        if (_sv is not null) _sv.ScrollChanged += OnScroll;
        _drag = false;
        _hot = false;
        _shown = 0;
        _heat = 0;
        InvalidateVisual();
    }

    private void OnScroll(object? sender, ScrollChangedEventArgs e)
    {
        _lastActivity = _clock.Elapsed.TotalMilliseconds;
        _lastOffset = double.NaN;
        InvalidateVisual();
        Wake();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        Wake();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _running = false;
        _top = null;
    }

    private void Wake()
    {
        if (_running || _top is null) return;
        _running = true;
        _lastFrame = _clock.Elapsed.TotalMilliseconds;
        _top.RequestAnimationFrame(Frame);
    }

    private void Frame(TimeSpan _)
    {
        if (!_running || _top is null) return;

        double now = _clock.Elapsed.TotalMilliseconds;
        double dt = Math.Min(0.05, Math.Max(0.0, (now - _lastFrame) / 1000.0));
        _lastFrame = now;

        bool live = _drag || _hot || (now - _lastActivity) < LingerMs;
        double wantShown = live && Scrollable() ? 1.0 : 0.0;
        double wantHeat = _drag ? 2.0 : _hot ? 1.0 : 0.0;

        double s = Approach(_shown, wantShown, FadeRate, dt);
        double h = Approach(_heat, wantHeat, HeatRate, dt);

        double off = _sv?.Offset.Y ?? 0;
        bool moved = double.IsNaN(_lastOffset) || Math.Abs(off - _lastOffset) > 0.01;
        _lastOffset = off;

        bool changed = Math.Abs(s - _shown) > 0.0008 || Math.Abs(h - _heat) > 0.0008;
        _shown = s;
        _heat = h;
        if (changed || moved) InvalidateVisual();

        bool settled = !changed && !moved
                       && Math.Abs(_shown - wantShown) < 0.0008
                       && Math.Abs(_heat - wantHeat) < 0.0008;
        if (settled && !_drag && !_hot && (now - _lastActivity) >= LingerMs)
        {
            _running = false;
            return;
        }
        _top.RequestAnimationFrame(Frame);
    }

    private static double Approach(double current, double target, double rate, double dt)
    {
        double k = 1.0 - Math.Exp(-rate * dt);
        double next = current + (target - current) * k;
        return Math.Abs(target - next) < 0.0006 ? target : next;
    }

    protected override Size MeasureOverride(Size availableSize) => new(HitWidth, 0);

    private bool Scrollable()
    {
        var sv = _sv;
        if (sv is null) return false;
        return sv.Viewport.Height > 0 && sv.Extent.Height > sv.Viewport.Height + 0.5;
    }

    private bool Metrics(out double top, out double height)
    {
        top = 0;
        height = 0;
        var sv = _sv;
        if (sv is null) return false;

        double extent = sv.Extent.Height;
        double viewport = sv.Viewport.Height;
        double track = Bounds.Height;
        if (track <= 4 || viewport <= 0 || extent <= viewport + 0.5) return false;

        height = Math.Max(MinThumb, Math.Min(track, track * viewport / extent));
        double span = track - height;
        double maxOffset = extent - viewport;
        double t = maxOffset <= 0 ? 0 : sv.Offset.Y / maxOffset;
        top = span * Math.Clamp(t, 0.0, 1.0);
        return true;
    }

    public override void Render(DrawingContext dc)
    {
        if (_shown <= 0.004) return;
        if (!Metrics(out double top, out double height)) return;

        int idx = (int)Math.Round(Math.Clamp(_heat, 0.0, 2.0) * 32.0);
        var brush = Ramp[Math.Clamp(idx, 0, Ramp.Length - 1)];

        double x = Bounds.Width - BarWidth;
        var rect = new Rect(x, top, BarWidth, height);

        using var fade = _shown < 0.999 ? dc.PushOpacity(_shown) : default;
        dc.DrawRectangle(brush, null, new RoundedRect(rect, BarWidth * 0.5));
    }

    private bool OverThumb(double y)
        => Metrics(out double top, out double h) && y >= top && y <= top + h;

    private void ScrollToThumbTop(double top, double height)
    {
        var sv = _sv;
        if (sv is null) return;
        double span = Bounds.Height - height;
        double t = span <= 0 ? 0 : Math.Clamp(top / span, 0.0, 1.0);
        double maxOffset = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
        sv.Offset = sv.Offset.WithY(t * maxOffset);
        _lastActivity = _clock.Elapsed.TotalMilliseconds;
        InvalidateVisual();
    }

    private void Refresh(PointerEventArgs e)
    {
        bool over = this.GetVisualRoot() is not null && OverThumb(e.GetPosition(this).Y);
        if (over != _hot)
        {
            _hot = over;
            Wake();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _lastActivity = _clock.Elapsed.TotalMilliseconds;
        Refresh(e);
        Wake();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag)
        {
            if (!Metrics(out _, out double h)) return;
            ScrollToThumbTop(e.GetPosition(this).Y - _grab, h);
            e.Handled = true;
            return;
        }
        _lastActivity = _clock.Elapsed.TotalMilliseconds;
        Refresh(e);
        Wake();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_drag) return;
        if (_hot)
        {
            _hot = false;
            Wake();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!Metrics(out double top, out double height)) return;

        double y = e.GetPosition(this).Y;
        if (y >= top && y <= top + height)
        {
            _drag = true;
            _grab = y - top;
            e.Pointer.Capture(this);
        }
        else
        {
            ScrollToThumbTop(y - height * 0.5, height);
        }
        _lastActivity = _clock.Elapsed.TotalMilliseconds;
        Wake();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_drag) return;
        _drag = false;
        e.Pointer.Capture(null);
        _hot = OverThumb(e.GetPosition(this).Y) && IsPointerOver;
        _lastActivity = _clock.Elapsed.TotalMilliseconds;
        Wake();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_drag) return;
        _drag = false;
        _hot = false;
        Wake();
    }
}
