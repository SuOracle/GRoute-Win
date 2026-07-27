using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace GRoute.Desktop.Controls;

public sealed class StarField : Panel
{
    private const int Count = 320;

    private static readonly Random Rnd = new(19);
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly List<StarField> Instances = new();
    private static StarField? _driver;
    private static double _lastMs;

    private static readonly double[] _nx = new double[Count];
    private static readonly double[] _ny = new double[Count];
    private static readonly double[] _tx = new double[Count];
    private static readonly double[] _ty = new double[Count];
    private static readonly double[] _dx = new double[Count];
    private static readonly double[] _dy = new double[Count];
    private static readonly double[] _vx = new double[Count];
    private static readonly double[] _vy = new double[Count];
    private static readonly double[] _r = new double[Count];

    private static readonly double[] _from = new double[Count];
    private static readonly double[] _to = new double[Count];
    private static readonly double[] _t = new double[Count];
    private static readonly double[] _dur = new double[Count];

    private static double _flow = 1.0;
    private static bool _flowing;

    private const int ShootCount = 3;
    private static readonly double[] _shx = new double[ShootCount];
    private static readonly double[] _shy = new double[ShootCount];
    private static readonly double[] _shvx = new double[ShootCount];
    private static readonly double[] _shvy = new double[ShootCount];
    private static readonly double[] _shlen = new double[ShootCount];
    private static readonly double[] _shlife = new double[ShootCount];
    private static readonly double[] _shwait = new double[ShootCount];

    static StarField()
    {
        for (int i = 0; i < Count; i++)
        {
            _nx[i] = Rnd.NextDouble();
            _ny[i] = Rnd.NextDouble();
            _tx[i] = _nx[i];
            _ty[i] = _ny[i];

            double ang = Rnd.NextDouble() * Math.PI * 2;
            double mag = 0.10 + Rnd.NextDouble() * 0.22;
            _dx[i] = Math.Cos(ang) * mag;
            _dy[i] = Math.Sin(ang) * mag;

            double va = Rnd.NextDouble() * Math.PI * 2;
            double vm = 0.0016 + Rnd.NextDouble() * 0.0052;
            _vx[i] = Math.Cos(va) * vm;
            _vy[i] = Math.Sin(va) * vm;

            double s = Rnd.NextDouble();
            _r[i] = s < 0.70 ? 0.75 : s < 0.93 ? 1.05 : 1.45;

            _from[i] = Rnd.NextDouble();
            _to[i] = Rnd.NextDouble();
            _t[i] = Rnd.NextDouble() * 0.8;
            _dur[i] = 0.5 + Rnd.NextDouble() * 0.5;
        }
        for (int i = 0; i < ShootCount; i++) _shwait[i] = 3.0 + Rnd.NextDouble() * 18.0;
    }

    private static void LaunchShoot(int i)
    {
        double dir = Rnd.NextDouble() < 0.5 ? 1 : -1;
        double speed = 0.55 + Rnd.NextDouble() * 0.55;
        double ang = (28 + Rnd.NextDouble() * 24) * Math.PI / 180.0;
        _shvx[i] = Math.Cos(ang) * speed * dir;
        _shvy[i] = Math.Sin(ang) * speed;
        _shx[i] = dir > 0 ? -0.08 - Rnd.NextDouble() * 0.1 : 1.08 + Rnd.NextDouble() * 0.1;
        _shy[i] = Rnd.NextDouble() * 0.55;
        _shlen[i] = 0.10 + Rnd.NextDouble() * 0.14;
        _shlife[i] = 1.0;
    }

    private readonly StarLayer _layer;

    private bool _isDriver;
    private bool _hooked;
    private TopLevel? _top;

    private double _cr = 0xD8, _cg = 0xE6, _cb = 0xFF;
    private double _tr = 0xD8, _tg = 0xE6, _tb = 0xFF;

    public StarField()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        _layer = new StarLayer(this);
        Children.Add(_layer);
    }

    public void SetTint(Color c)
    {
        _tr = Math.Min(255, c.R * 0.45 + 150);
        _tg = Math.Min(255, c.G * 0.45 + 150);
        _tb = Math.Min(255, c.B * 0.45 + 150);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (!Instances.Contains(this)) Instances.Add(this);
        if (_driver is null)
        {
            _driver = this;
            _isDriver = true;
        }
        if (!_hooked && _top is not null && _isDriver)
        {
            _hooked = true;
            _lastMs = Clock.Elapsed.TotalMilliseconds;
            _top.RequestAnimationFrame(OnFrame);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Instances.Remove(this);
        if (!_isDriver) return;
        _isDriver = false;
        _hooked = false;
        _driver = Instances.Count > 0 ? Instances[0] : null;
        if (_driver is null) return;
        _driver._isDriver = true;
        _driver._hooked = false;
    }

    public static void Reflow()
    {
        if (_flowing) return;
        for (int i = 0; i < Count; i++)
        {
            _tx[i] = Rnd.NextDouble();
            _ty[i] = Rnd.NextDouble();
            double ang = Rnd.NextDouble() * Math.PI * 2;
            double mag = 0.10 + Rnd.NextDouble() * 0.22;
            _dx[i] = Math.Cos(ang) * mag;
            _dy[i] = Math.Sin(ang) * mag;
        }
        _flow = 0.0;
        _flowing = true;
    }

    private void OnFrame(TimeSpan _)
    {
        if (_top is null || !_isDriver) return;

        double now = Clock.Elapsed.TotalMilliseconds;
        double dt = (now - _lastMs) / 1000.0;
        _lastMs = now;
        if (dt > 0.05) dt = 0.05;

        for (int i = 0; i < Count; i++)
        {
            _nx[i] += _vx[i] * dt;
            _ny[i] += _vy[i] * dt;
            if (_nx[i] < -0.05) _nx[i] += 1.10;
            if (_nx[i] > 1.05) _nx[i] -= 1.10;
            if (_ny[i] < -0.05) _ny[i] += 1.10;
            if (_ny[i] > 1.05) _ny[i] -= 1.10;

            _tx[i] += _vx[i] * dt;
            _ty[i] += _vy[i] * dt;
            if (_tx[i] < -0.05) _tx[i] += 1.10;
            if (_tx[i] > 1.05) _tx[i] -= 1.10;
            if (_ty[i] < -0.05) _ty[i] += 1.10;
            if (_ty[i] > 1.05) _ty[i] -= 1.10;

            _t[i] += dt;
            if (_t[i] >= _dur[i])
            {
                _from[i] = _to[i];
                _to[i] = Rnd.NextDouble();
                _dur[i] = 0.5 + Rnd.NextDouble() * 0.5;
                _t[i] = 0;
            }
        }

        for (int i = 0; i < ShootCount; i++)
        {
            if (_shlife[i] > 0)
            {
                _shx[i] += _shvx[i] * dt;
                _shy[i] += _shvy[i] * dt;
                if (_shx[i] < -0.4 || _shx[i] > 1.4 || _shy[i] > 1.3)
                {
                    _shlife[i] = 0;
                    _shwait[i] = 6.0 + Rnd.NextDouble() * 22.0;
                }
            }
            else
            {
                _shwait[i] -= dt;
                if (_shwait[i] <= 0) LaunchShoot(i);
            }
        }

        if (_flowing)
        {
            _flow += dt / 0.85;
            if (_flow >= 1.0)
            {
                _flow = 1.0;
                _flowing = false;
                for (int i = 0; i < Count; i++)
                {
                    _nx[i] = _tx[i];
                    _ny[i] = _ty[i];
                }
            }
        }

        double ease = 1 - Math.Pow(0.001, dt);
        for (int i = 0; i < Instances.Count; i++)
        {
            var f = Instances[i];
            f._cr += (f._tr - f._cr) * ease;
            f._cg += (f._tg - f._cg) * ease;
            f._cb += (f._tb - f._cb) * ease;
            f._layer.InvalidateVisual();
        }

        _top.RequestAnimationFrame(OnFrame);
    }

    private void Paint(DrawingContext dc, Size size)
    {
        double w = size.Width;
        double h = size.Height;
        if (w <= 0 || h <= 0) return;

        byte r = (byte)Math.Clamp(_cr, 0, 255);
        byte g = (byte)Math.Clamp(_cg, 0, 255);
        byte b = (byte)Math.Clamp(_cb, 0, 255);

        double p = _flow;
        bool going = _flowing && p < 0.5;
        double leg = _flowing ? (going ? p * 2.0 : (p - 0.5) * 2.0) : 1.0;
        double eased = 1 - Math.Pow(1 - leg, 3);
        double fade = _flowing ? (going ? 1 - eased : eased) : 1.0;

        for (int i = 0; i < Count; i++)
        {
            double bx, by;
            if (!_flowing)
            {
                bx = _nx[i];
                by = _ny[i];
            }
            else if (going)
            {
                bx = _nx[i] + _dx[i] * eased;
                by = _ny[i] + _dy[i] * eased;
            }
            else
            {
                bx = _tx[i] - _dx[i] * (1 - eased);
                by = _ty[i] - _dy[i] * (1 - eased);
            }

            double x = bx * w;
            double y = by * h;

            double k = _from[i] + (_to[i] - _from[i]) * (_t[i] / _dur[i]);
            k = Math.Clamp(k, 0, 1) * fade;
            if (k <= 0.02) continue;

            var ring = new ImmutableSolidColorBrush(Color.FromArgb((byte)(k * 102), r, g, b));
            dc.DrawEllipse(ring, null, new Point(x, y), _r[i] + 1.0, _r[i] + 1.0);

            var core = new ImmutableSolidColorBrush(Color.FromArgb((byte)(k * 255), r, g, b));
            dc.DrawEllipse(core, null, new Point(x, y), _r[i], _r[i]);
        }

        for (int i = 0; i < ShootCount; i++)
        {
            if (_shlife[i] <= 0) continue;
            double hx = _shx[i] * w;
            double hy = _shy[i] * h;
            double n = Math.Sqrt(_shvx[i] * _shvx[i] + _shvy[i] * _shvy[i]);
            if (n <= 0) continue;
            double tailX = hx - _shvx[i] / n * _shlen[i] * w;
            double tailY = hy - _shvy[i] / n * _shlen[i] * w;

            var grad = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(tailX, tailY, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(hx, hy, RelativeUnit.Absolute)
            };
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(0, r, g, b), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(fade * 210), r, g, b), 1));

            dc.DrawLine(new Pen(grad, 1.6, lineCap: PenLineCap.Round),
                new Point(tailX, tailY), new Point(hx, hy));
            var head = new ImmutableSolidColorBrush(Color.FromArgb((byte)(fade * 255), r, g, b));
            dc.DrawEllipse(head, null, new Point(hx, hy), 1.3, 1.3);
        }
    }

    private sealed class StarLayer : Control
    {
        private readonly StarField _owner;

        public StarLayer(StarField owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
            ClipToBounds = false;
        }

        public override void Render(DrawingContext dc) => _owner.Paint(dc, Bounds.Size);
    }
}
