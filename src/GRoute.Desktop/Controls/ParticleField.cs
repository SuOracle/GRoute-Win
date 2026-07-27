using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace GRoute.Desktop.Controls;

public sealed class ParticleField : Control
{
    private const int Count = 128;
    private const double LinkDist = 96.0;

    private static readonly double[] _x = new double[Count];
    private static readonly double[] _y = new double[Count];
    private static readonly double[] _vx = new double[Count];
    private static readonly double[] _vy = new double[Count];
    private static readonly double[] _ph = new double[Count];
    private static readonly double[] _r = new double[Count];
    private static readonly Random _rnd = new(7);
    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    private static double _lastMs;
    private static bool _seeded;
    private static ParticleField? _driver;
    private static readonly List<ParticleField> _instances = new();
    private bool _isDriver;
    private double _mx = -9999;
    private double _my = -9999;
    private bool _mouseIn;
    private bool _hooked;
    private TopLevel? _top;

    private double _cr = 0x5B, _cg = 0x83, _cb = 0xD6;
    private double _tr = 0x5B, _tg = 0x83, _tb = 0xD6;

    public ParticleField()
    {
        IsHitTestVisible = false;
        Opacity = 0.7;
    }

    public void SetTint(Color c)
    {
        _tr = c.R;
        _tg = c.G;
        _tb = c.B;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is not null)
        {
            _top.PointerMoved += TopPointerMoved;
            _top.PointerExited += TopPointerExited;
        }
        if (!_instances.Contains(this)) _instances.Add(this);
        if (_driver is null)
        {
            _driver = this;
            _isDriver = true;
        }
        if (!_hooked && _top is not null && _isDriver)
        {
            _hooked = true;
            _lastMs = _clock.Elapsed.TotalMilliseconds;
            _top.RequestAnimationFrame(OnFrame);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_top is not null)
        {
            _top.PointerMoved -= TopPointerMoved;
            _top.PointerExited -= TopPointerExited;
        }
        _instances.Remove(this);
        if (ReferenceEquals(_driver, this))
        {
            _driver = null;
            _isDriver = false;
        }
        _hooked = false;
        _top = null;
    }

    private void TopPointerMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        _mx = p.X;
        _my = p.Y;
        _mouseIn = _mx >= 0 && _my >= 0 && _mx <= Bounds.Width && _my <= Bounds.Height;
    }

    private void TopPointerExited(object? sender, PointerEventArgs e) => _mouseIn = false;

    private void Seed()
    {
        double w = Bounds.Width, h = Bounds.Height;
        for (int i = 0; i < Count; i++)
        {
            _x[i] = _rnd.NextDouble() * w;
            _y[i] = _rnd.NextDouble() * h;
            double ang = _rnd.NextDouble() * Math.PI * 2;
            double spd = 6.0 + _rnd.NextDouble() * 9.0;
            _vx[i] = Math.Cos(ang) * spd;
            _vy[i] = Math.Sin(ang) * spd;
            _ph[i] = _rnd.NextDouble() * Math.PI * 2;
            _r[i] = 1.1 + _rnd.NextDouble() * 1.3;
        }
        _seeded = true;
    }

    private void OnFrame(TimeSpan _)
    {
        if (!_hooked || _top is null) return;
        double now = _clock.Elapsed.TotalMilliseconds;
        double dt = Math.Min(64.0, now - _lastMs);
        _lastMs = now;
        double w = Bounds.Width, h = Bounds.Height;
        if (w >= 20 && h >= 20)
        {
            if (!_seeded) Seed();

            double k = 1.0 - Math.Exp(-dt / 350.0);
            _cr += (_tr - _cr) * k;
            _cg += (_tg - _cg) * k;
            _cb += (_tb - _cb) * k;

            double s = dt / 1000.0;
            for (int i = 0; i < Count; i++)
            {
                _x[i] += _vx[i] * s;
                _y[i] += _vy[i] * s;
                if (_mouseIn)
                {
                    double ddx = _x[i] - _mx;
                    double ddy = _y[i] - _my;
                    double d2 = ddx * ddx + ddy * ddy;
                    if (d2 < 120.0 * 120.0 && d2 > 0.01)
                    {
                        double d = Math.Sqrt(d2);
                        double f = (1.0 - d / 120.0) * 70.0 * s;
                        _x[i] += ddx / d * f;
                        _y[i] += ddy / d * f;
                    }
                }
                if (_x[i] < -12) _x[i] = w + 12;
                if (_x[i] > w + 12) _x[i] = -12;
                if (_y[i] < -12) _y[i] = h + 12;
                if (_y[i] > h + 12) _y[i] = -12;
            }
            for (int inst = 0; inst < _instances.Count; inst++) _instances[inst].InvalidateVisual();
        }
        _top.RequestAnimationFrame(OnFrame);
    }

    public override void Render(DrawingContext dc)
    {
        if (!_seeded) return;
        double now = _clock.Elapsed.TotalMilliseconds;
        byte cr = (byte)_cr, cg = (byte)_cg, cb = (byte)_cb;

        var pens = new ImmutablePen[8];
        for (int b = 0; b < 8; b++)
            pens[b] = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb((byte)(((b + 1) * 0.30 / 8.0) * 255), cr, cg, cb)), 1.0);
        var mousePens = new ImmutablePen[8];
        for (int b = 0; b < 8; b++)
            mousePens[b] = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb((byte)(((b + 1) * 0.40 / 8.0) * 255), cr, cg, cb)), 1.0);
        var glows = new ImmutableSolidColorBrush[8];
        var cores = new ImmutableSolidColorBrush[8];
        for (int b = 0; b < 8; b++)
        {
            double tw = 0.55 + 0.45 * (b + 0.5) / 8.0;
            glows[b] = new ImmutableSolidColorBrush(Color.FromArgb((byte)(38 * tw), cr, cg, cb));
            cores[b] = new ImmutableSolidColorBrush(Color.FromArgb((byte)(70 + 150 * tw), cr, cg, cb));
        }

        for (int i = 0; i < Count; i++)
        {
            for (int j = i + 1; j < Count; j++)
            {
                double dx = _x[i] - _x[j];
                double dy = _y[i] - _y[j];
                double d2 = dx * dx + dy * dy;
                if (d2 > LinkDist * LinkDist) continue;
                double d = Math.Sqrt(d2);
                int b = (int)((1.0 - d / LinkDist) * 8.0);
                if (b > 7) b = 7;
                dc.DrawLine(pens[b], new Point(_x[i], _y[i]), new Point(_x[j], _y[j]));
            }
        }

        if (_mouseIn)
        {
            for (int i = 0; i < Count; i++)
            {
                double dx = _x[i] - _mx;
                double dy = _y[i] - _my;
                double d2 = dx * dx + dy * dy;
                if (d2 > 150.0 * 150.0) continue;
                double d = Math.Sqrt(d2);
                int b = (int)((1.0 - d / 150.0) * 8.0);
                if (b > 7) b = 7;
                dc.DrawLine(mousePens[b], new Point(_mx, _my), new Point(_x[i], _y[i]));
            }
            dc.DrawEllipse(new ImmutableSolidColorBrush(Color.FromArgb(56, cr, cg, cb)), null, new Point(_mx, _my), 7.0, 7.0);
            dc.DrawEllipse(new ImmutableSolidColorBrush(Color.FromArgb(190, cr, cg, cb)), null, new Point(_mx, _my), 2.2, 2.2);
        }

        for (int i = 0; i < Count; i++)
        {
            double tw = 0.5 + 0.5 * Math.Sin(now / 900.0 + _ph[i]);
            int b = (int)(tw * 8.0);
            if (b > 7) b = 7;
            dc.DrawEllipse(glows[b], null, new Point(_x[i], _y[i]), _r[i] * 3.2, _r[i] * 3.2);
            dc.DrawEllipse(cores[b], null, new Point(_x[i], _y[i]), _r[i], _r[i]);
        }
    }
}