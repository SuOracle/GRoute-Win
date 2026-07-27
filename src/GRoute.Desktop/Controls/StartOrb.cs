using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace GRoute.Desktop.Controls;

public sealed class StartOrb : Control
{
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<StartOrb, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    private const int PCount = 640;
    private const double RingInMs = 800.0;
    private const double DiscDelayMs = 260.0;
    private const double DiscMs = 560.0;
    private const double TextAtMs = 620.0;
    private const double SlideMs = 320.0;

    private static readonly Typeface Face = new(
        new FontFamily("avares://GRoute/Assets/Fonts#Audiowide, Consolas, Segoe UI"),
        FontStyle.Normal, FontWeight.Normal);
    private static readonly ImmutableSolidColorBrush TextBrush = new(Color.FromRgb(0xF0, 0xFF, 0xF6));

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Random _rnd = new(11);

    private readonly double[] _pAng = new double[PCount];
    private readonly double[] _pVel = new double[PCount];
    private readonly double[] _pOff = new double[PCount];
    private readonly double[] _pDep = new double[PCount];
    private readonly double[] _pJit = new double[PCount];
    private readonly double[] _pTw = new double[PCount];
    private readonly double[] _pR = new double[PCount];
    private bool _seeded;

    private double _t0 = -100000;
    private double _lastMs;
    private bool _hooked;
    private TopLevel? _top;

    private double _rotDeg = 205;
    private double _sweepPh = 0.9;
    private double _pulsePh;
    private double _busy, _hover, _press;
    private double _busyTarget, _hoverTarget, _pressTarget;

    private string _curText = "";
    private Run? _cur, _prev;
    private double _slideStart = -1;
    private bool _textShown;

    private double _cx, _cy, _ringR, _discR;
    private int _sizeEpoch;

    private int _brushKey = int.MinValue;
    private ImmutablePen? _penDim, _penGlow, _penMain;
    private ImmutableSolidColorBrush[] _pCore = Array.Empty<ImmutableSolidColorBrush>();

    public StartOrb()
    {
        FlowDirection = FlowDirection.LeftToRight;
        Focusable = false;
    }

    public void Restart()
    {
        double now = _clock.Elapsed.TotalMilliseconds;
        _t0 = now;
        _lastMs = now;
        _textShown = false;
        _slideStart = -1;
        _prev = null;
        _press = _pressTarget = 0;
    }

    public void SetText(string text, bool instant = false)
    {
        text = (text ?? "").Trim();
        if (text == _curText) return;
        _curText = text;
        if (instant || !_textShown || _cur is null)
        {
            _cur = null;
            _prev = null;
            _slideStart = -1;
        }
        else
        {
            _prev = _cur;
            _cur = null;
            _slideStart = _clock.Elapsed.TotalMilliseconds;
        }
        if (_hooked) InvalidateVisual();
    }

    public void SetBusy(bool busy) => _busyTarget = busy ? 1 : 0;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        Hook();
        Restart();
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

        double kBusy = 1.0 - Math.Exp(-dt / 200.0);
        double kHover = 1.0 - Math.Exp(-dt / 120.0);
        double kPress = 1.0 - Math.Exp(-dt / 70.0);
        _busy += (_busyTarget - _busy) * kBusy;
        _hover += (_hoverTarget - _hover) * kHover;
        _press += (_pressTarget - _press) * kPress;

        double s = dt / 1000.0;
        _rotDeg = (_rotDeg + (24.0 + 66.0 * _busy) * s) % 360.0;
        _sweepPh += (0.0009 + 0.0013 * _busy) * dt;
        _pulsePh += 0.0016 * dt;

        if (_seeded)
        {
            double mul = 1.0 + 1.2 * _busy;
            for (int i = 0; i < PCount; i++)
                _pAng[i] += _pVel[i] * s * mul;
        }
        InvalidateVisual();
        _top.RequestAnimationFrame(OnFrame);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        double sz = Math.Min(Bounds.Width, Bounds.Height);
        _cx = Bounds.Width * 0.5;
        _cy = Bounds.Height * 0.5;
        _ringR = sz * 0.5 - 6.0;
        _discR = _ringR * 0.83;
        _sizeEpoch++;
        _cur = null;
        _prev = null;
        if (!_seeded && _ringR > 20) Seed();
    }

    private void Seed()
    {
        for (int i = 0; i < PCount; i++)
        {
            _pAng[i] = _rnd.NextDouble() * Math.PI * 2;
            _pVel[i] = (0.04 + _rnd.NextDouble() * 0.22) * (_rnd.NextDouble() < 0.3 ? -1 : 1);
            _pOff[i] = Math.Sqrt(_rnd.NextDouble());
            _pDep[i] = _rnd.NextDouble() * 0.35;
            _pJit[i] = (_rnd.NextDouble() - 0.5) * 1.8;
            _pTw[i] = _rnd.NextDouble() * Math.PI * 2;
            _pR[i] = 0.4 + _rnd.NextDouble() * 0.8;
        }
        _seeded = true;
    }

    public override void Render(DrawingContext dc)
    {
        dc.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_ringR < 20) return;
        double now = _clock.Elapsed.TotalMilliseconds;
        double t = now - _t0;

        double ringP = EaseOutCubic(Clamp01(t / RingInMs));
        double discP = EaseOutBack(Clamp01((t - DiscDelayMs) / DiscMs));
        double ringOp = Clamp01(t / 180.0);
        double partP = EaseOutCubic(Clamp01((t - 150.0) / 650.0));

        double mix = Clamp01(Math.Max(_hover * 0.7, _busy));
        EnsureBrushes(mix);

        double pulse = 0.5 + 0.5 * Math.Sin(_pulsePh);
        double breathe = 0.945 + 0.075 * pulse;
        double scale = discP * breathe * (1.0 + 0.020 * _hover * (1.0 - _busy)) * (1.0 - 0.045 * _press);

        if (_seeded && partP > 0.02)
        {
            double inner = 0.0;
            double outer = _ringR - 5.0;
            for (int i = 0; i < PCount; i++)
            {
                double w = 0.5 + 0.5 * Math.Sin(_pulsePh * 0.9 + _pJit[i]);
                double frac = _pOff[i] * (1.0 - _pDep[i] * w);
                double r = inner + (outer - inner) * frac;
                double px = _cx + r * Math.Cos(_pAng[i]);
                double py = _cy + r * Math.Sin(_pAng[i]);
                double tw = (0.55 + 0.45 * Math.Sin(now / 700.0 + _pTw[i])) * (1.0 - 0.35 * w) * partP;
                int b = (int)(tw * 8.0);
                if (b > 7) b = 7;
                dc.DrawEllipse(_pCore[b], null, new Point(px, py), _pR[i], _pR[i]);
            }
        }

        using (ringOp < 0.999 ? dc.PushOpacity(ringOp) : default)
        {
            dc.DrawEllipse(null, _penDim, new Point(_cx, _cy), _ringR, _ringR);
            double sweep = (178.0 - 20.0 * _busy) + 132.0 * Math.Sin(_sweepPh);
            if (sweep < 40.0) sweep = 40.0;
            double drawSweep = Math.Max(2.0, sweep * ringP);
            double a0 = _rotDeg - drawSweep * 0.5 - 160.0 * (1.0 - ringP);
            var arc = BuildArc(new Point(_cx, _cy), _ringR, a0, drawSweep);
            dc.DrawGeometry(null, _penGlow, arc);
            dc.DrawGeometry(null, _penMain, arc);
        }

        if (!_textShown && t >= TextAtMs && _curText.Length > 0)
        {
            _textShown = true;
            _slideStart = now;
        }
        if (_textShown && _curText.Length > 0)
        {
            EnsureRuns();
            if (_cur is not null)
            {
                using var clip = dc.PushClip(new Rect(_cx - _discR * 0.94, _cy - 34, _discR * 1.88, 68));
                if (_slideStart >= 0)
                {
                    double sp = Clamp01((now - _slideStart) / SlideMs);
                    double e = EaseOutCubic(sp);
                    if (_prev is not null)
                        DrawRun(dc, _prev, -e * 16.0, Math.Pow(1.0 - e, 1.3));
                    DrawRun(dc, _cur, (1.0 - e) * 16.0, e);
                    if (sp >= 1.0) { _slideStart = -1; _prev = null; }
                }
                else
                {
                    DrawRun(dc, _cur, 0, 1);
                }
            }
        }
    }

    private void DrawRun(DrawingContext dc, Run r, double dy, double op)
    {
        if (op <= 0.01) return;
        using var o = op < 0.999 ? dc.PushOpacity(op) : default;
        double x0 = _cx - r.W * 0.5;
        double y = _cy - r.H * 0.5 + dy;
        if (r.Single is not null)
        {
            dc.DrawText(r.Single, new Point(x0, y));
        }
        else
        {
            for (int i = 0; i < r.Ch.Length; i++)
                dc.DrawText(r.Ch[i], new Point(x0 + r.X[i], y));
        }
    }

    private sealed class Run
    {
        public string Text = "";
        public int Epoch;
        public FormattedText[] Ch = Array.Empty<FormattedText>();
        public double[] X = Array.Empty<double>();
        public FormattedText? Single;
        public double W, H;
    }

    private void EnsureRuns()
    {
        if (_cur is null || _cur.Text != _curText || _cur.Epoch != _sizeEpoch)
            _cur = BuildRun(_curText);
        if (_prev is not null && _prev.Epoch != _sizeEpoch)
            _prev = null;
    }

    private Run BuildRun(string text)
    {
        var run = new Run { Text = text, Epoch = _sizeEpoch };
        double maxW = _discR * 1.52;
        double size = Math.Clamp(_discR * 0.30, 11.0, 30.0);

        if (HasRtl(text))
        {
            var ft = MakeFt(text, size, FlowDirection.RightToLeft, CultureInfo.CurrentUICulture);
            if (ft.Width > maxW)
            {
                size = Math.Max(10.0, size * maxW / ft.Width);
                ft = MakeFt(text, size, FlowDirection.RightToLeft, CultureInfo.CurrentUICulture);
            }
            run.Single = ft;
            run.W = ft.Width;
            run.H = ft.Height;
            return run;
        }

        for (int pass = 0; pass < 2; pass++)
        {
            double spacing = size * 0.12;
            int n = text.Length;
            var ch = new FormattedText[n];
            var xs = new double[n];
            double x = 0, h = 0;
            for (int i = 0; i < n; i++)
            {
                ch[i] = MakeFt(text[i].ToString(), size, FlowDirection.LeftToRight, CultureInfo.InvariantCulture);
                xs[i] = x;
                x += ch[i].WidthIncludingTrailingWhitespace + (i < n - 1 ? spacing : 0);
                if (ch[i].Height > h) h = ch[i].Height;
            }
            if (x > maxW && pass == 0)
            {
                size = Math.Max(10.0, size * maxW / x);
                continue;
            }
            run.Ch = ch;
            run.X = xs;
            run.W = x;
            run.H = h;
            break;
        }
        return run;
    }

    private static FormattedText MakeFt(string s, double size, FlowDirection fd, CultureInfo cu) =>
        new(s, cu, fd, Face, size, TextBrush);

    private static bool HasRtl(string s)
    {
        foreach (char c in s)
            if ((c >= 0x0590 && c <= 0x08FF) || (c >= 0xFB50 && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFF))
                return true;
        return false;
    }

    private void EnsureBrushes(double mix)
    {
        int key = (int)(mix * 24.0 + 0.5) | (_sizeEpoch << 8);
        if (key == _brushKey) return;
        _brushKey = key;

        byte mr = LerpB(0x2E, 0x6C, mix), mg = LerpB(0xE8, 0xFF, mix), mb = LerpB(0x7B, 0xAE, mix);

        _penDim = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb((byte)(60 + 40 * mix), 0x1C, 0xA8, 0x5C)), 4.0, lineCap: PenLineCap.Round);
        _penGlow = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb((byte)(46 + 30 * mix), mr, mg, mb)), 11.0, lineCap: PenLineCap.Round);
        _penMain = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(mr, mg, mb)), 5.5, lineCap: PenLineCap.Round);

        _pCore = new ImmutableSolidColorBrush[8];
        for (int b = 0; b < 8; b++)
        {
            double tw = (b + 0.5) / 8.0;
            _pCore[b] = new ImmutableSolidColorBrush(Color.FromArgb((byte)(12 + 38 * tw), mr, mg, mb));
        }
    }

    private bool Clickable => (_clock.Elapsed.TotalMilliseconds - _t0) > 850.0;

    private bool Inside(Point p)
    {
        double dx = p.X - _cx, dy = p.Y - _cy;
        double r = _ringR + 8.0;
        return dx * dx + dy * dy <= r * r;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        bool inside = Inside(e.GetPosition(this));
        _hoverTarget = inside && Clickable ? 1 : 0;
        Cursor = inside && Clickable ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverTarget = 0;
        _pressTarget = 0;
        Cursor = Cursor.Default;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!Clickable || !Inside(e.GetPosition(this))) return;
        _pressTarget = 1;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!ReferenceEquals(e.Pointer.Captured, this)) return;
        e.Pointer.Capture(null);
        bool fire = _pressTarget > 0.5 && Clickable && Inside(e.GetPosition(this));
        _pressTarget = 0;
        if (fire)
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(ClickEvent));
        }
    }

    private static Geometry BuildArc(Point c, double r, double a0Deg, double sweepDeg)
    {
        if (sweepDeg >= 359.5)
            return new EllipseGeometry(new Rect(c.X - r, c.Y - r, r * 2, r * 2));
        double a0 = a0Deg * Math.PI / 180.0;
        double a1 = (a0Deg + sweepDeg) * Math.PI / 180.0;
        var p0 = new Point(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0));
        var p1 = new Point(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(p0, false);
            ctx.ArcTo(p1, new Size(r, r), 0, sweepDeg > 180.0, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        return g;
    }

    private static byte LerpB(byte a, byte b, double m) => (byte)(a + (b - a) * m);

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    private static double EaseOutCubic(double p)
    {
        double q = 1.0 - p;
        return 1.0 - q * q * q;
    }

    private static double EaseOutBack(double p)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1.0;
        double q = p - 1.0;
        return 1.0 + c3 * q * q * q + c1 * q * q;
    }
}