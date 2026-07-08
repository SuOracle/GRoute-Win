using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;
using Border = System.Windows.Controls.Border;
using Rectangle = System.Windows.Shapes.Rectangle;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using FontWeights = System.Windows.FontWeights;
using Stretch = System.Windows.Media.Stretch;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace GRoute.Windows;

public sealed class EarthGlobe : System.Windows.Controls.Grid
{
    private const int MaskW = 2048;
    private const int MaskH = 1024;
    private const string MaskB64 = "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";

    private readonly byte[] _mask;

    private readonly Ellipse _glow = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 100, Height = 100 };
    private readonly ScaleTransform _glowScale = new();
    private readonly Image _dots = new() { IsHitTestVisible = false, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Rectangle _beam = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Width = 2 };
    private readonly Ellipse _ring = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Ellipse _ring2 = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Ellipse _core = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Ellipse _dot = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Border _popup;
    private readonly TextBlock _popupIp = new() { FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0xBE, 0xDC, 0xFF)) };
    private readonly TextBlock _popupLoc = new() { FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xD4, 0xE4)), Margin = new Thickness(0, 2, 0, 0) };

    private readonly TextBlock _popupLabel;
    private WriteableBitmap? _bmp;
    private int[] _pixels = Array.Empty<int>();
    private int _bs;

    private double _spin = -0.62;
    private double _tilt = 0.40;
    private double _flyStartSpin, _flyStartTilt, _flyTargetSpin, _flyTargetTilt;
    private double _flyT = 1.0;
    private IpLocation _loc = IpLocator.Fallback;

    private bool _dragging;
    private Point _lastDrag;
    private bool _dirty = true;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastMs;
    private double _animRad = -1.0;

    private static readonly int GridColor = Premul(30, 0x6A, 0x96, 0xE8);
    private static readonly Color NodeColor = Color.FromRgb(0x8F, 0xC0, 0xFF);

    public EarthGlobe()
    {
        _mask = LoadMask();

        RenderOptions.SetBitmapScalingMode(_dots, BitmapScalingMode.Linear);

    _beam.Fill = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(190, NodeColor.R, NodeColor.G, NodeColor.B), 0.0),
                new GradientStop(Color.FromArgb(0, NodeColor.R, NodeColor.G, NodeColor.B), 1.0)
            }, new Point(0, 1), new Point(0, 0));

        _ring.Stroke = new SolidColorBrush(NodeColor); _ring.StrokeThickness = 1.4; _ring.Fill = Brushes.Transparent;
        _ring2.Stroke = new SolidColorBrush(NodeColor); _ring2.StrokeThickness = 1.4; _ring2.Fill = Brushes.Transparent;
        _core.Fill = new SolidColorBrush(Color.FromRgb(0xE4, 0xF1, 0xFF));
        _core.Effect = new DropShadowEffect { Color = NodeColor, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 };
        _dot.Fill = Brushes.White;

        _popup = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(236, 0x0E, 0x1A, 0x38)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0x3D, 0x6A, 0xD6)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Padding = new Thickness(11, 7, 11, 7),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 2, Opacity = 0.5 }
        };
        var col = new StackPanel();
        _popupLabel = new TextBlock
        {
            Text = "PUBLIC IP",
            FontSize = 8.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xA8, 0xD8)),
            Margin = new Thickness(0, 0, 0, 1)
        };
        col.Children.Add(_popupLabel);
        col.Children.Add(_popupIp);
        col.Children.Add(_popupLoc);
        _popup.Child = col;
        _popupIp.Text = _loc.Ip;
        _popupLoc.Text = _loc.City + ", " + _loc.Country;

        _glow.Fill = new RadialGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0x00, 0x5E, 0x8F, 0xE0), 0.735),
                new GradientStop(Color.FromArgb(0x42, 0x5E, 0x8F, 0xE0), 0.775),
                new GradientStop(Color.FromArgb(0x24, 0x54, 0x86, 0xD8), 0.84),
                new GradientStop(Color.FromArgb(0x0E, 0x4A, 0x7C, 0xCC), 0.91),
                new GradientStop(Color.FromArgb(0x00, 0x4A, 0x7C, 0xCC), 1.0)
            });

        _glow.RenderTransformOrigin = new Point(0.5, 0.5);
        _glow.RenderTransform = _glowScale;

        Background = Brushes.Transparent;
        Children.Add(_glow);
        Children.Add(_dots);
        Children.Add(_beam);
        Children.Add(_ring);
        Children.Add(_ring2);
        Children.Add(_core);
        Children.Add(_dot);
        Children.Add(_popup);

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseLeave += (_, _) => { _dragging = false; ReleaseMouseCapture(); };
        SizeChanged += (_, _) => { RebuildBitmap(); _dirty = true; };
        Loaded += (_, _) => { RebuildBitmap(); CompositionTarget.Rendering += OnFrame; };
        Unloaded += (_, _) => { CompositionTarget.Rendering -= OnFrame; };
    }

    public void SetLocation(IpLocation loc)
    {
        _loc = loc;
        _popupIp.Text = string.IsNullOrEmpty(loc.Ip) ? "\u2014" : loc.Ip;
        _popupLoc.Text = loc.City + ", " + loc.Country;
        _flyStartSpin = _spin;
        _flyStartTilt = _tilt;
        _flyTargetSpin = NearestAngle(-Deg2Rad(loc.Lon), _spin);
        _flyTargetTilt = Clamp(Deg2Rad(loc.Lat), -1.35, 1.35);
        _flyT = 0.0;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _lastDrag = e.GetPosition(this);
        _flyT = 1.0;
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(this);
        double rad = _animRad > 0 ? _animRad : 1.0;
        _spin += (p.X - _lastDrag.X) / rad;
        _tilt = Clamp(_tilt + (p.Y - _lastDrag.Y) / rad, -1.35, 1.35);
        _lastDrag = p;
        _dirty = true;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        ReleaseMouseCapture();
    }

    private void RebuildBitmap()
    {
        double side = Math.Min(ActualWidth, ActualHeight);
        int s = (int)Math.Round(side);
        if (s <= 8) return;
        _bs = Math.Clamp(s, 64, 640);
        _pixels = new int[_bs * _bs];
        _bmp = new WriteableBitmap(_bs, _bs, 96, 96, PixelFormats.Pbgra32, null);
        _dots.Source = _bmp;
        _dots.Width = side;
        _dots.Height = side;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalMilliseconds;
        double dt = now - _lastMs;
        _lastMs = now;

        if (_flyT < 1.0)
        {
            _flyT = Math.Min(1.0, _flyT + dt / 900.0);
            double k = _flyT * _flyT * (3.0 - 2.0 * _flyT);
            _spin = _flyStartSpin + (_flyTargetSpin - _flyStartSpin) * k;
            _tilt = _flyStartTilt + (_flyTargetTilt - _flyStartTilt) * k;
            _dirty = true;
        }

        double side = Math.Min(ActualWidth, ActualHeight);
        if (side <= 4 || _bmp == null) return;

        double target = side / 2.0 * 0.91;
        if (_animRad < 0) _animRad = target;
        else _animRad += (target - _animRad) * (1.0 - Math.Exp(-dt / 90.0));
        double fit = side / 2.0 * 0.96;
        if (_animRad > fit) _animRad = fit;
        if (Math.Abs(target - _animRad) > 0.4) _dirty = true;

        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;

        double gk = _animRad * 2.6 / 100.0;
        _glowScale.ScaleX = gk;
        _glowScale.ScaleY = gk;

        if (_dots.Width != side) { _dots.Width = side; _dots.Height = side; }

        if (_dirty)
        {
            RenderGlobe(_animRad / side * _bs);
            _dirty = false;
        }

        UpdateMarker(cx, cy, _animRad, now);
    }

    private void UpdateMarker(double cx, double cy, double rad, double now)
    {
        Project(_loc.Lat, _loc.Lon, cx, cy, rad, out double mx, out double my, out double tz);
        bool vis = tz > 0.0;
        var v = vis ? Visibility.Visible : Visibility.Hidden;
        _beam.Visibility = v; _ring.Visibility = v; _ring2.Visibility = v; _core.Visibility = v; _dot.Visibility = v;

        if (vis)
        {
            double ph1 = (now % 1700.0) / 1700.0;
            double ph2 = ((now + 850.0) % 1700.0) / 1700.0;
            SetRing(_ring, mx, my, rad * (0.028 + 0.11 * ph1), 0.45 * (1.0 - ph1));
            SetRing(_ring2, mx, my, rad * (0.028 + 0.11 * ph2), 0.45 * (1.0 - ph2));

            double coreR = rad * 0.019;
            _core.Width = coreR * 2; _core.Height = coreR * 2;
            _core.Margin = new Thickness(mx - coreR, my - coreR, 0, 0);

            double dotR = rad * 0.008;
            _dot.Width = dotR * 2; _dot.Height = dotR * 2;
            _dot.Margin = new Thickness(mx - dotR, my - dotR, 0, 0);

            double beamH = rad * 0.16;
            _beam.Height = beamH;
            _beam.Margin = new Thickness(mx - 1, my - beamH, 0, 0);
        }

        double alpha = Clamp(tz / 0.22, 0.0, 1.0);
        _popup.Opacity = alpha;
        _popup.Visibility = alpha > 0.01 ? Visibility.Visible : Visibility.Hidden;
        double px = Clamp(mx - 68.0, 2.0, Math.Max(2.0, ActualWidth - 142.0));
        double py = Clamp(my - rad * 0.16 - 50.0, 2.0, Math.Max(2.0, ActualHeight - 48.0));
        _popup.Margin = new Thickness(px, py, 0, 0);
    }

    private static void SetRing(Ellipse ring, double mx, double my, double r, double opacity)
    {
        ring.Width = r * 2; ring.Height = r * 2;
        ring.Opacity = Clamp(opacity, 0.0, 1.0);
        ring.Margin = new Thickness(mx - r, my - r, 0, 0);
    }

    private void RenderGlobe(double radS)
    {
        if (_bmp == null || _pixels.Length == 0 || radS <= 2) return;
        Array.Clear(_pixels, 0, _pixels.Length);

        double cs = Math.Cos(_spin), sn = Math.Sin(_spin);
        double ct = Math.Cos(_tilt), st = Math.Sin(_tilt);
        double c = _bs / 2.0;

        double lx = -0.42, ly = 0.52, lz = 0.72;
        double ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
        lx /= ll; ly /= ll; lz /= ll;

        for (int py = 0; py < _bs; py++)
        {
            double dyn = (c - py - 0.5) / radS;
            int row = py * _bs;
            for (int px = 0; px < _bs; px++)
            {
                double dxn = (px + 0.5 - c) / radS;
                double d2 = dxn * dxn + dyn * dyn;
                if (d2 > 1.0) continue;

                double dd = Math.Sqrt(d2);
                double fr = 0, fg = 0, fb = 0, fa = 0;

                {
                    double tz = Math.Sqrt(1.0 - d2);

                    double ndotl = dxn * lx + dyn * ly + tz * lz;
                    if (ndotl < 0) ndotl = 0;
                    double shade = 0.26 + 0.74 * ndotl;
                    double s2 = ndotl * ndotl;
                    double s4 = s2 * s2;
                    double spec = s4 * s4 * ndotl * ndotl * 0.30;

                    double limb = (1.0 - d2) * 3.0;
                    if (limb > 1) limb = 1;
                    limb = Math.Sqrt(limb);

                    double ay = dyn * ct + tz * st;
                    double rz = -dyn * st + tz * ct;
                    double ax = dxn * cs - rz * sn;
                    double az = dxn * sn + rz * cs;
                    if (ay > 1.0) ay = 1.0; else if (ay < -1.0) ay = -1.0;
                    double lat = Math.Asin(ay) * (180.0 / Math.PI);
                    double lon = Math.Atan2(ax, az) * (180.0 / Math.PI);

                    double cov = SampleMask(lon, lat);
                    double coast = cov * (1.0 - cov) * 4.0;

                    double ocR = (0x04 + (0x18 - 0x04) * shade) + spec * 0x8A * 0.5;
                    double ocG = (0x0B + (0x3A - 0x0B) * shade) + spec * 0xB8 * 0.5;
                    double ocB = (0x1A + (0x6E - 0x1A) * shade) + spec * 0xFF * 0.5;

                    double lnR = 0x2A + (0x6E - 0x2A) * shade;
                    double lnG = 0x4E + (0x9E - 0x4E) * shade;
                    double lnB = 0xA2 + (0xEC - 0xA2) * shade;

                    fr = (ocR * (1.0 - cov) + lnR * cov + coast * 0x6F * 0.38) * limb;
                    fg = (ocG * (1.0 - cov) + lnG * cov + coast * 0xB6 * 0.38) * limb;
                    fb = (ocB * (1.0 - cov) + lnB * cov + coast * 0xFF * 0.38) * limb;

                    double discA = (1.0 - dd) * radS * 2.0;
                    if (discA > 1) discA = 1; else if (discA < 0) discA = 0;
                    fa = discA;
                    fr *= discA; fg *= discA; fb *= discA;
                }

                if (fa > 1) fa = 1;

                int a = (int)(fa * 255.0);
                int r2 = (int)fr; if (r2 > 255) r2 = 255;
                int g2 = (int)fg; if (g2 > 255) g2 = 255;
                int b2 = (int)fb; if (b2 > 255) b2 = 255;
                _pixels[row + px] = (a << 24) | (r2 << 16) | (g2 << 8) | b2;
            }
        }

        DrawGrid(cs, sn, ct, st, c, c, radS);

        _bmp.WritePixels(new Int32Rect(0, 0, _bs, _bs), _pixels, _bs * 4, 0);
    }

    private double SampleMask(double lon, double lat)
    {
        double u = (lon + 180.0) / 360.0 * MaskW - 0.5;
        double v = (90.0 - lat) / 180.0 * MaskH - 0.5;
        int u0 = (int)Math.Floor(u);
        int v0 = (int)Math.Floor(v);
        double fu = u - u0;
        double fv = v - v0;
        int iu0 = ((u0 % MaskW) + MaskW) % MaskW;
        int iu1 = (iu0 + 1) % MaskW;
        int iv0 = v0 < 0 ? 0 : (v0 >= MaskH ? MaskH - 1 : v0);
        int iv1 = v0 + 1 < 0 ? 0 : (v0 + 1 >= MaskH ? MaskH - 1 : v0 + 1);
        double m00 = _mask[iv0 * MaskW + iu0];
        double m10 = _mask[iv0 * MaskW + iu1];
        double m01 = _mask[iv1 * MaskW + iu0];
        double m11 = _mask[iv1 * MaskW + iu1];
        double top = m00 + (m10 - m00) * fu;
        double bot = m01 + (m11 - m01) * fu;
        return (top + (bot - top) * fv) / 255.0;
    }

    private void DrawGrid(double cs, double sn, double ct, double st, double cx, double cy, double rad)
    {
        for (int lon = -180; lon < 180; lon += 30)
        {
            bool have = false; int px0 = 0, py0 = 0; double d0 = 0;
            for (int la = -80; la <= 80; la += 6)
            {
                Gp(la, lon, cs, sn, ct, st, cx, cy, rad, out int px, out int py, out double d);
                if (have && d0 > 0 && d > 0) PlotLine(px0, py0, px, py, GridColor);
                px0 = px; py0 = py; d0 = d; have = true;
            }
        }
        int[] par = { -60, -30, 0, 30, 60 };
        foreach (int la in par)
        {
            bool have = false; int px0 = 0, py0 = 0; double d0 = 0;
            for (int lon = -180; lon <= 180; lon += 6)
            {
                Gp(la, lon, cs, sn, ct, st, cx, cy, rad, out int px, out int py, out double d);
                if (have && d0 > 0 && d > 0) PlotLine(px0, py0, px, py, GridColor);
                px0 = px; py0 = py; d0 = d; have = true;
            }
        }
    }

    private static void Gp(double lat, double lon, double cs, double sn, double ct, double st, double cx, double cy, double rad, out int px, out int py, out double depth)
    {
        double la = Deg2Rad(lat), lo = Deg2Rad(lon);
        double ax = Math.Cos(la) * Math.Sin(lo), ay = Math.Sin(la), az = Math.Cos(la) * Math.Cos(lo);
        double rx = ax * cs + az * sn;
        double rz = -ax * sn + az * cs;
        double ty = ay * ct - rz * st;
        depth = ay * st + rz * ct;
        px = (int)(cx + rad * rx);
        py = (int)(cy - rad * ty);
    }

    private void Project(double lat, double lon, double cx, double cy, double rad, out double px, out double py, out double depth)
    {
        double cs = Math.Cos(_spin), sn = Math.Sin(_spin);
        double ct = Math.Cos(_tilt), st = Math.Sin(_tilt);
        double la = Deg2Rad(lat), lo = Deg2Rad(lon);
        double ax = Math.Cos(la) * Math.Sin(lo), ay = Math.Sin(la), az = Math.Cos(la) * Math.Cos(lo);
        double rx = ax * cs + az * sn;
        double rz = -ax * sn + az * cs;
        double ty = ay * ct - rz * st;
        depth = ay * st + rz * ct;
        px = cx + rad * rx;
        py = cy - rad * ty;
    }

    private void PlotLine(int x0, int y0, int x1, int y1, int argb)
    {
        int ga = (argb >> 24) & 0xFF;
        int gr = (argb >> 16) & 0xFF;
        int gg = (argb >> 8) & 0xFF;
        int gb = argb & 0xFF;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            if (x0 >= 0 && x0 < _bs && y0 >= 0 && y0 < _bs)
            {
                int idx = y0 * _bs + x0;
                int e = _pixels[idx];
                int ea = (e >> 24) & 0xFF;
                int er = ((e >> 16) & 0xFF) + gr; if (er > 255) er = 255;
                int eg2 = ((e >> 8) & 0xFF) + gg; if (eg2 > 255) eg2 = 255;
                int eb = (e & 0xFF) + gb; if (eb > 255) eb = 255;
                int na = ea > ga ? ea : ga;
                _pixels[idx] = (na << 24) | (er << 16) | (eg2 << 8) | eb;
            }
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private static byte[] LoadMask()
    {
        byte[] comp = Convert.FromBase64String(MaskB64);
        using var ms = new MemoryStream(comp);
        using var gz = new GZipStream(ms, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        gz.CopyTo(outMs);
        byte[] bits = outMs.ToArray();
        var mask = new byte[MaskW * MaskH];
        for (int i = 0; i < mask.Length; i++)
            mask[i] = (byte)(((bits[i >> 3] >> (7 - (i & 7))) & 1) != 0 ? 255 : 0);
        return mask;
    }

    private static int Premul(int a, int r, int g, int b)
    {
        if (a < 0) a = 0; else if (a > 255) a = 255;
        return (a << 24) | (r * a / 255 << 16) | (g * a / 255 << 8) | (b * a / 255);
    }

    private static double Deg2Rad(double d) => d * Math.PI / 180.0;
    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

    private static double NearestAngle(double target, double current)
    {
        double t = target;
        double twoPi = 2 * Math.PI;
        while (t - current > Math.PI) t -= twoPi;
        while (t - current < -Math.PI) t += twoPi;
        return t;
    }
}
