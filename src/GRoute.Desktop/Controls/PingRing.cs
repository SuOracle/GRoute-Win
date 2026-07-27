using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace GRoute.Desktop.Controls;

public sealed class PingRing : Panel
{
    public static readonly StyledProperty<int> PingProperty =
        AvaloniaProperty.Register<PingRing, int>(nameof(Ping), int.MinValue);

    public static readonly StyledProperty<bool> BusyProperty =
        AvaloniaProperty.Register<PingRing, bool>(nameof(Busy));

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<PingRing, double>(nameof(Radius), 10.0);

    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<PingRing, double>(nameof(Inset));

    public int Ping
    {
        get => GetValue(PingProperty);
        set => SetValue(PingProperty, value);
    }

    public bool Busy
    {
        get => GetValue(BusyProperty);
        set => SetValue(BusyProperty, value);
    }

    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public double Inset
    {
        get => GetValue(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    private static readonly Color Accent = Color.FromRgb(0x8A, 0x97, 0xAD);

    private const double Step = 1.5;
    private const double TrailFraction = 0.46;
    private const double RiseExp = 1.8;
    private const double FallExp = 1.4;

    private static readonly double PeakNorm =
        Math.Pow(RiseExp / (RiseExp + FallExp), RiseExp) *
        Math.Pow(FallExp / (RiseExp + FallExp), FallExp);

    private readonly RingLayer _soft;
    private readonly RingLayer _line;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TopLevel? _top;
    private bool _hooked;
    private double _phase;
    private double _lastMs;
    private double _glow;

    private double _x, _y, _w, _h, _r;
    private double _perimeter;
    private Size _geoSize;
    private double _geoInset = double.NaN;
    private double _geoRadius = double.NaN;

    public PingRing()
    {
        IsHitTestVisible = false;
        _soft = new RingLayer(this, soft: true) { IsVisible = false };
        _line = new RingLayer(this, soft: false);
        Children.Add(_line);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        Hook();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _hooked = false;
        _top = null;
    }

    private void Hook()
    {
        if (_hooked || _top is null) return;
        _hooked = true;
        _lastMs = _clock.Elapsed.TotalMilliseconds;
        _top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        if (!_hooked || _top is null) return;
        double now = _clock.Elapsed.TotalMilliseconds;
        double dt = Math.Min(64.0, now - _lastMs);
        _lastMs = now;

        _glow += ((Busy ? 1.0 : 0.0) - _glow) * (1.0 - Math.Exp(-dt / 180.0));

        if (_perimeter > 0)
        {
            _phase += (58.0 + 150.0 * _glow) * dt / 1000.0;
            if (_phase > _perimeter) _phase -= _perimeter;
        }

        _soft.InvalidateVisual();
        _line.InvalidateVisual();
        _top.RequestAnimationFrame(OnFrame);
    }

    private void EnsureGeometry(Size size)
    {
        if (size.Equals(_geoSize) && Inset.Equals(_geoInset) &&
            Radius.Equals(_geoRadius) && _perimeter > 0) return;
        _geoSize = size;
        _geoInset = Inset;
        _geoRadius = Radius;

        double inset = Inset + 1.0;
        _x = inset;
        _y = inset;
        _w = size.Width - inset * 2.0;
        _h = size.Height - inset * 2.0;
        if (_w <= 0 || _h <= 0)
        {
            _perimeter = 0;
            return;
        }
        _r = Math.Min(Radius, Math.Min(_w, _h) * 0.5);
        _perimeter = 2.0 * (_w - 2.0 * _r) + 2.0 * (_h - 2.0 * _r) + 2.0 * Math.PI * _r;
    }

    private Point PointAt(double d)
    {
        d %= _perimeter;
        if (d < 0) d += _perimeter;

        double sw = _w - 2.0 * _r;
        double sh = _h - 2.0 * _r;
        double arc = Math.PI * _r * 0.5;
        double q = Math.PI * 0.5;

        if (d < sw) return new Point(_x + _r + d, _y);
        d -= sw;
        if (d < arc)
        {
            double a = d / arc * q;
            return new Point(_x + _w - _r + _r * Math.Sin(a), _y + _r - _r * Math.Cos(a));
        }
        d -= arc;
        if (d < sh) return new Point(_x + _w, _y + _r + d);
        d -= sh;
        if (d < arc)
        {
            double a = d / arc * q;
            return new Point(_x + _w - _r + _r * Math.Cos(a), _y + _h - _r + _r * Math.Sin(a));
        }
        d -= arc;
        if (d < sw) return new Point(_x + _w - _r - d, _y + _h);
        d -= sw;
        if (d < arc)
        {
            double a = d / arc * q;
            return new Point(_x + _r - _r * Math.Sin(a), _y + _h - _r + _r * Math.Cos(a));
        }
        d -= arc;
        if (d < sh) return new Point(_x, _y + _h - _r - d);
        d -= sh;
        {
            double a = d / arc * q;
            return new Point(_x + _r - _r * Math.Cos(a), _y + _r - _r * Math.Sin(a));
        }
    }

    private static ImmutablePen Pen(Color c, double alpha, double width)
    {
        byte a = (byte)Math.Clamp(alpha * 255.0, 0, 255);
        return new ImmutablePen(
            new ImmutableSolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)),
            width, null, PenLineCap.Flat, PenLineJoin.Round);
    }

    private void RenderLayer(DrawingContext context, Size size, bool soft)
    {
        EnsureGeometry(size);
        if (_perimeter <= 0) return;

        var c = Accent;
        double lift = 0.62 + 0.38 * _glow;

        double ringWidth = soft ? 2.6 : 1.6;
        double ringAlpha = soft ? 0.30 : 0.16;
        var ringPrev = PointAt(0);
        int loop = Math.Max(24, (int)(_perimeter / 3.0));
        for (int i = 1; i <= loop; i++)
        {
            var next = PointAt(_perimeter * i / loop);
            context.DrawLine(Pen(c, ringAlpha, ringWidth), ringPrev, next);
            ringPrev = next;
        }

        double trail = _perimeter * TrailFraction;
        int count = Math.Max(8, (int)(trail / Step));

        var prev = PointAt(_phase - trail);
        for (int i = 1; i <= count; i++)
        {
            double t = i / (double)count;
            var point = PointAt(_phase - trail * (1.0 - t));

            double a = Math.Pow(t, RiseExp) * Math.Pow(1.0 - t, FallExp) / PeakNorm * lift;
            if (a > 0.004)
            {
                if (soft)
                {
                    context.DrawLine(Pen(c, a * 0.95, 3.6), prev, point);
                }
                else
                {
                    context.DrawLine(Pen(c, a * 0.30, 3.0), prev, point);
                    context.DrawLine(Pen(c, a, 1.8), prev, point);
                }
            }
            prev = point;
        }
    }

    private sealed class RingLayer : Control
    {
        private readonly PingRing _owner;
        private readonly bool _soft;

        public RingLayer(PingRing owner, bool soft)
        {
            _owner = owner;
            _soft = soft;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            var size = Bounds.Size;
            if (size.Width <= 0 || size.Height <= 0) return;
            _owner.RenderLayer(context, size, _soft);
        }
    }
}
