using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;

namespace GRoute.Windows;

public sealed class ParticleField : FrameworkElement
{
    private const int Count = 128;
    private const double LinkDist = 96.0;

    private readonly double[] _x = new double[Count];
    private readonly double[] _y = new double[Count];
    private readonly double[] _vx = new double[Count];
    private readonly double[] _vy = new double[Count];
    private readonly double[] _ph = new double[Count];
    private readonly double[] _r = new double[Count];
    private readonly Random _rnd = new(7);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastMs;
    private bool _seeded;
    private double _mx = -9999;
    private double _my = -9999;
    private bool _mouseIn;

    private double _cr = 0x5B, _cg = 0x83, _cb = 0xD6;
    private double _tr = 0x5B, _tg = 0x83, _tb = 0xD6;

    public ParticleField()
    {
        IsHitTestVisible = false;
        Opacity = 0.7;
        Loaded += (_, _) => { CompositionTarget.Rendering += OnFrame; };
        Unloaded += (_, _) => { CompositionTarget.Rendering -= OnFrame; };
    }

    public void SetTint(Color c)
    {
        _tr = c.R; _tg = c.G; _tb = c.B;
    }

    private void Seed()
    {
        double w = ActualWidth, h = ActualHeight;
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

    private void OnFrame(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalMilliseconds;
        double dt = Math.Min(64.0, now - _lastMs);
        _lastMs = now;
        double w = ActualWidth, h = ActualHeight;
        if (w < 20 || h < 20) return;
        if (!_seeded) Seed();

        double k = 1.0 - Math.Exp(-dt / 350.0);
        _cr += (_tr - _cr) * k;
        _cg += (_tg - _cg) * k;
        _cb += (_tb - _cb) * k;

        var mp = System.Windows.Input.Mouse.GetPosition(this);
        _mx = mp.X; _my = mp.Y;
        _mouseIn = _mx >= 0 && _my >= 0 && _mx <= w && _my <= h;

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
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_seeded) return;
        double now = _clock.Elapsed.TotalMilliseconds;
        byte cr = (byte)_cr, cg = (byte)_cg, cb = (byte)_cb;

        var pens = new Pen[8];
        for (int b = 0; b < 8; b++)
        {
            var pb = new SolidColorBrush(Color.FromArgb((byte)(((b + 1) * 0.30 / 8.0) * 255), cr, cg, cb));
            pb.Freeze();
            pens[b] = new Pen(pb, 1.0);
            pens[b].Freeze();
        }
        var mousePens = new Pen[8];
        for (int b = 0; b < 8; b++)
        {
            var pb = new SolidColorBrush(Color.FromArgb((byte)(((b + 1) * 0.40 / 8.0) * 255), cr, cg, cb));
            pb.Freeze();
            mousePens[b] = new Pen(pb, 1.0);
            mousePens[b].Freeze();
        }
        var glows = new SolidColorBrush[8];
        var cores = new SolidColorBrush[8];
        for (int b = 0; b < 8; b++)
        {
            double tw = 0.55 + 0.45 * (b + 0.5) / 8.0;
            glows[b] = new SolidColorBrush(Color.FromArgb((byte)(38 * tw), cr, cg, cb));
            glows[b].Freeze();
            cores[b] = new SolidColorBrush(Color.FromArgb((byte)(70 + 150 * tw), cr, cg, cb));
            cores[b].Freeze();
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
            var cursorGlow = new SolidColorBrush(Color.FromArgb(56, cr, cg, cb));
            cursorGlow.Freeze();
            dc.DrawEllipse(cursorGlow, null, new Point(_mx, _my), 7.0, 7.0);
            var cursorCore = new SolidColorBrush(Color.FromArgb(190, cr, cg, cb));
            cursorCore.Freeze();
            dc.DrawEllipse(cursorCore, null, new Point(_mx, _my), 2.2, 2.2);
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
