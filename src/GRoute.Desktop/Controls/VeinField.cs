using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace GRoute.Desktop.Controls;

public sealed class VeinField : Panel
{
    private const int Trees = 7;
    private const double VH = 1.45;
    private const double RootX = 0.87;
    private const double RootY = 0.33;

    private struct Seg
    {
        public double X1, Y1, X2, Y2;
        public double MX, MY;
        public double D0, D1;
        public double W;
        public int Tree;
        public int Bucket;
    }

    private static readonly Random Rnd = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly List<VeinField> Instances = new();
    private static readonly List<Seg> Segs = new();
    private static readonly double[] _span = new double[Trees];
    private static readonly double[] _head = new double[Trees];
    private static readonly double[] _speed = new double[Trees];
    private static readonly double[] _wait = new double[Trees];
    private static readonly bool[] _live = new bool[Trees];
    private static readonly int[] _treeStart = new int[Trees];
    private static readonly int[] _treeEnd = new int[Trees];

    private static double[] AX1 = Array.Empty<double>();
    private static double[] AY1 = Array.Empty<double>();
    private static double[] AX2 = Array.Empty<double>();
    private static double[] AY2 = Array.Empty<double>();
    private static double[] AMX = Array.Empty<double>();
    private static double[] AMY = Array.Empty<double>();
    private static double[] AD1 = Array.Empty<double>();
    private static int[] ABK = Array.Empty<int>();
    private static int SegCount;

    private static void Flatten()
    {
        int n = Segs.Count;
        AX1 = new double[n];
        AY1 = new double[n];
        AX2 = new double[n];
        AY2 = new double[n];
        AMX = new double[n];
        AMY = new double[n];
        AD1 = new double[n];
        ABK = new int[n];
        for (int i = 0; i < n; i++)
        {
            var s = Segs[i];
            AX1[i] = s.X1; AY1[i] = s.Y1;
            AX2[i] = s.X2; AY2[i] = s.Y2;
            AMX[i] = s.MX; AMY[i] = s.MY;
            AD1[i] = s.D1; ABK[i] = s.Bucket;
        }
        SegCount = n;
    }
    private static readonly double[] BucketW = { 2.7, 1.9, 1.35, 0.95, 0.62 };
    private static readonly double[] StepOf = { 0.046, 0.038, 0.031, 0.025, 0.021 };
    private static readonly double[] JitOf = { 0.16, 0.30, 0.48, 0.68, 0.88 };
    private static readonly double[] CurlOf = { 0.10, 0.20, 0.32, 0.46, 0.60 };
    private static readonly double[] BranchOf = { 0.40, 0.46, 0.52, 0.56, 0.0 };
    private static readonly double[] TurnOf = { 1.05, 0.90, 0.78, 0.66, 0.55 };

    private static double _clickX, _clickY, _clickR;
    private static bool _clickLive;
    private static DateTime _clickAt = DateTime.MinValue;

    public static void StrikeRoot(bool force = false)
    {
        if (!force && (DateTime.UtcNow - _clickAt).TotalMilliseconds < 800) return;
        _clickX = RootX;
        _clickY = RootY;
        _clickR = 0;
        _clickLive = true;
        _clickAt = DateTime.UtcNow;
    }

    public static bool Strike(double nx, double ny)
    {
        if ((DateTime.UtcNow - _clickAt).TotalMilliseconds < 800) return false;

        int best = -1;
        double bestD = 0.030 * 0.030;
        for (int i = 0; i < Segs.Count; i++)
        {
            var s = Segs[i];
            double mx = (s.X1 + s.X2) * 0.5 - nx;
            double my = (s.Y1 + s.Y2) * 0.5 - ny;
            double d = mx * mx + my * my;
            if (d >= bestD) continue;
            bestD = d;
            best = i;
        }
        if (best < 0) return false;

        var hit = Segs[best];
        _clickX = (hit.X1 + hit.X2) * 0.5;
        _clickY = (hit.Y1 + hit.Y2) * 0.5;
        _clickR = 0;
        _clickLive = true;
        _clickAt = DateTime.UtcNow;
        return true;
    }

    private static VeinField? _driver;
    private static double _lastMs;

    private const double Step = 0.034;
    private const double MinNode = 0.019;
    private const int MaxSegs = 3400;

    private static readonly Dictionary<long, List<int>> Nodes = new();
    private static readonly List<double> NodeX = new();
    private static readonly List<double> NodeY = new();

    private static long CellOf(double x, double y)
    {
        long cx = (long)Math.Floor(x / MinNode);
        long cy = (long)Math.Floor(y / MinNode);
        return (cx << 32) ^ (cy & 0xFFFFFFFFL);
    }

    private static void AddNode(double x, double y)
    {
        long c = CellOf(x, y);
        if (!Nodes.TryGetValue(c, out var list))
        {
            list = new List<int>();
            Nodes[c] = list;
        }
        list.Add(NodeX.Count);
        NodeX.Add(x);
        NodeY.Add(y);
    }

    private static bool NearNode(double x, double y)
    {
        long bx = (long)Math.Floor(x / MinNode);
        long by = (long)Math.Floor(y / MinNode);
        for (long ax = bx - 1; ax <= bx + 1; ax++)
        {
            for (long ay = by - 1; ay <= by + 1; ay++)
            {
                if (!Nodes.TryGetValue((ax << 32) ^ (ay & 0xFFFFFFFFL), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    double dx = NodeX[list[i]] - x;
                    double dy = NodeY[list[i]] - y;
                    if (dx * dx + dy * dy < MinNode * MinNode) return true;
                }
            }
        }
        return false;
    }

    private struct Road
    {
        public double X, Y, Ang, Dist, Curl;
        public int Tree, Depth;
    }

    static VeinField()
    {
        var q = new Queue<Road>();
        double spin = Rnd.NextDouble() * Math.PI * 2;

        AddNode(RootX, RootY);
        for (int t = 0; t < Trees; t++)
        {
            q.Enqueue(new Road
            {
                X = RootX,
                Y = RootY,
                Ang = spin + t * Math.PI * 2 / Trees + (Rnd.NextDouble() - 0.5) * 0.30,
                Curl = (Rnd.NextDouble() - 0.5) * CurlOf[0],
                Dist = 0,
                Tree = t,
                Depth = 0
            });
            _wait[t] = Rnd.NextDouble() * 15.0;
            _speed[t] = 0.20 + Rnd.NextDouble() * 0.26;
        }

        while (q.Count > 0 && Segs.Count < MaxSegs)
        {
            var rd = q.Dequeue();
            int lvl = Math.Min(BucketW.Length - 1, rd.Depth);
            double curl = rd.Curl;
            if (Rnd.NextDouble() < 0.16) curl = (Rnd.NextDouble() - 0.5) * CurlOf[lvl];
            double a = rd.Ang + curl + (Rnd.NextDouble() - 0.5) * JitOf[lvl];
            double stepLen = StepOf[lvl];
            double nx = rd.X + Math.Cos(a) * stepLen;
            double ny = rd.Y + Math.Sin(a) * stepLen;

            if (nx < -0.10 || nx > 1.10 || ny < -0.10 || ny > VH + 0.10) continue;
            if (NearNode(nx, ny)) continue;

            AddNode(nx, ny);
            int bucket = lvl;
            Segs.Add(new Seg
            {
                X1 = rd.X,
                Y1 = rd.Y,
                X2 = nx,
                Y2 = ny,
                D0 = rd.Dist,
                D1 = rd.Dist + stepLen,
                W = BucketW[bucket],
                Tree = rd.Tree,
                Bucket = bucket
            });

            double nd = rd.Dist + stepLen;
            if (nd > _span[rd.Tree]) _span[rd.Tree] = nd;

            q.Enqueue(new Road { X = nx, Y = ny, Ang = a, Curl = curl, Dist = nd, Tree = rd.Tree, Depth = rd.Depth });

            if (rd.Depth < BucketW.Length - 1 && Rnd.NextDouble() < BranchOf[lvl])
            {
                double side = Rnd.NextDouble() < 0.5 ? 1 : -1;
                double turn = TurnOf[lvl] * (0.72 + Rnd.NextDouble() * 0.56);
                q.Enqueue(new Road
                {
                    X = nx,
                    Y = ny,
                    Ang = a + side * turn,
                    Curl = (Rnd.NextDouble() - 0.5) * CurlOf[Math.Min(BucketW.Length - 1, rd.Depth + 1)],
                    Dist = nd,
                    Tree = rd.Tree,
                    Depth = rd.Depth + 1
                });
            }
        }

        for (int i = 0; i < Segs.Count; i++)
        {
            var sg = Segs[i];
            sg.MX = (sg.X1 + sg.X2) * 0.5;
            sg.MY = (sg.Y1 + sg.Y2) * 0.5;
            Segs[i] = sg;
        }
        Segs.Sort((p, r) => p.Tree.CompareTo(r.Tree));
        int at = 0;
        for (int t = 0; t < Trees; t++)
        {
            _treeStart[t] = at;
            while (at < Segs.Count && Segs[at].Tree == t) at++;
            _treeEnd[t] = at;
        }

        Flatten();

        Nodes.Clear();
        NodeX.Clear();
        NodeY.Clear();
    }

    private readonly VeinLayer _glow;
    private readonly VeinLayer _line;

    private bool _isDriver;
    private bool _hooked;
    private bool _dirty = true;
    private TopLevel? _top;

    private double _cr = 0x6E, _cg = 0x9C, _cb = 0xF0;
    private double _tr = 0x6E, _tg = 0x9C, _tb = 0xF0;

    public VeinField()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        _glow = new VeinLayer(this, soft: true)
        {
            Effect = new BlurEffect { Radius = 5.0 }
        };
        _line = new VeinLayer(this, soft: false);
        Children.Add(_glow);
        Children.Add(_line);
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

    private void OnFrame(TimeSpan _)
    {
        if (_top is null || !_isDriver) return;

        double now = Clock.Elapsed.TotalMilliseconds;
        double dt = (now - _lastMs) / 1000.0;
        _lastMs = now;
        if (dt > 0.05) dt = 0.05;

        if (_clickLive)
        {
            _clickR += 0.62 * dt;
            if (_clickR > 2.3) _clickLive = false;
        }

        for (int t = 0; t < Trees; t++)
        {
            if (_live[t])
            {
                _head[t] += _speed[t] * dt;
                if (_head[t] > _span[t] + 0.30)
                {
                    _live[t] = false;
                    _wait[t] = 6.0 + Rnd.NextDouble() * 14.0;
                    _speed[t] = 0.20 + Rnd.NextDouble() * 0.26;
                }
            }
            else
            {
                _wait[t] -= dt;
                if (_wait[t] <= 0)
                {
                    _head[t] = 0;
                    _live[t] = true;
                }
            }
        }

        bool anyLive = _clickLive;
        if (!anyLive)
        {
            for (int t = 0; t < Trees; t++)
            {
                if (!_live[t]) continue;
                anyLive = true;
                break;
            }
        }

        double ease = 1 - Math.Pow(0.001, dt);
        for (int i = 0; i < Instances.Count; i++)
        {
            var f = Instances[i];
            if (!f.IsVisible) continue;
            double dr = f._tr - f._cr, dg = f._tg - f._cg, db = f._tb - f._cb;
            bool tinting = Math.Abs(dr) > 0.4 || Math.Abs(dg) > 0.4 || Math.Abs(db) > 0.4;
            if (tinting)
            {
                f._cr += dr * ease;
                f._cg += dg * ease;
                f._cb += db * ease;
            }
            bool need = anyLive || tinting;
            if (!need && !f._dirty) continue;
            f._dirty = need;
            f._glow.InvalidateVisual();
            f._line.InvalidateVisual();
        }

        _top.RequestAnimationFrame(OnFrame);
    }

    private StreamGeometry[]? _baseGeo;
    private double _geoW, _geoH;
    private IPen[]? _basePens;
    private byte _penR, _penG, _penB;

    private const int Steps = 12;
    private IPen[,]? _hotPens;
    private IPen[,]? _softPens;
    private byte _litR, _litG, _litB;

    private void BuildLitPens(byte r, byte g, byte b)
    {
        int n = BucketW.Length;
        var hot = new IPen[n, Steps];
        var soft = new IPen[n, Steps];
        for (int i = 0; i < n; i++)
        {
            for (int k = 0; k < Steps; k++)
            {
                double a = (k + 1) / (double)Steps;
                byte hr = (byte)Math.Min(255, r + (255 - r) * a * 0.75);
                byte hg = (byte)Math.Min(255, g + (255 - g) * a * 0.75);
                byte hb = (byte)Math.Min(255, b + (255 - b) * a * 0.75);
                hot[i, k] = new ImmutablePen(
                    new ImmutableSolidColorBrush(Color.FromArgb((byte)(a * 185), hr, hg, hb)),
                    BucketW[i], lineCap: PenLineCap.Round);
                soft[i, k] = new ImmutablePen(
                    new ImmutableSolidColorBrush(Color.FromArgb((byte)(a * 118), r, g, b)),
                    BucketW[i] * 2.6, lineCap: PenLineCap.Round);
            }
        }
        _hotPens = hot;
        _softPens = soft;
        _litR = r;
        _litG = g;
        _litB = b;
    }

    private void BuildGeometry(double w, double h)
    {
        int n = BucketW.Length;
        var geo = new StreamGeometry[n];
        var ctx = new StreamGeometryContext[n];
        for (int i = 0; i < n; i++)
        {
            geo[i] = new StreamGeometry();
            ctx[i] = geo[i].Open();
        }

        for (int i = 0; i < Segs.Count; i++)
        {
            var s = Segs[i];
            var c = ctx[s.Bucket];
            c.BeginFigure(new Point(s.X1 * w, s.Y1 * w), false);
            c.LineTo(new Point(s.X2 * w, s.Y2 * w));
            c.EndFigure(false);
        }

        for (int i = 0; i < n; i++) ctx[i].Dispose();
        _baseGeo = geo;
        _geoW = w;
        _geoH = h;
    }

    private void BuildPens(byte r, byte g, byte b)
    {
        int n = BucketW.Length;
        var pens = new IPen[n];
        var brush = new ImmutableSolidColorBrush(Color.FromArgb(20, r, g, b));
        for (int i = 0; i < n; i++)
            pens[i] = new ImmutablePen(brush, BucketW[i] * 0.7, lineCap: PenLineCap.Round);
        _basePens = pens;
        _penR = r;
        _penG = g;
        _penB = b;
    }

    public void Warmup(double w, double h)
    {
        if (w <= 0 || h <= 0 || Segs.Count == 0) return;
        byte r = (byte)Math.Clamp(_cr, 0, 255);
        byte g = (byte)Math.Clamp(_cg, 0, 255);
        byte b = (byte)Math.Clamp(_cb, 0, 255);
        BuildGeometry(w, h);
        BuildPens(r, g, b);
        BuildLitPens(r, g, b);
    }

    private void Paint(DrawingContext dc, Size size, bool soft)
    {
        double w = size.Width;
        double h = size.Height;
        if (w <= 0 || h <= 0 || Segs.Count == 0) return;

        byte r = (byte)Math.Clamp(_cr, 0, 255);
        byte g = (byte)Math.Clamp(_cg, 0, 255);
        byte b = (byte)Math.Clamp(_cb, 0, 255);
        const double tail = 0.26;

        if (!soft)
        {
            if (_baseGeo is null || Math.Abs(_geoW - w) > 0.5 || Math.Abs(_geoH - h) > 0.5)
                BuildGeometry(w, h);
            if (_basePens is null || _penR != r || _penG != g || _penB != b)
                BuildPens(r, g, b);

            for (int i = 0; i < _baseGeo!.Length; i++)
                dc.DrawGeometry(null, _basePens![i], _baseGeo[i]);
        }

        if (_hotPens is null || _litR != r || _litG != g || _litB != b) BuildLitPens(r, g, b);
        var pens = soft ? _softPens! : _hotPens!;

        if (_clickLive)
        {
            const double ring = 0.20;
            double inner = _clickR - ring;
            double innerSq = inner > 0 ? inner * inner : 0;
            double outerSq = _clickR * _clickR;
            for (int i = 0; i < Segs.Count; i++)
            {
                var s = Segs[i];
                double mx = s.MX - _clickX;
                double my = s.MY - _clickY;
                double q = mx * mx + my * my;
                if (q > outerSq || q < innerSq) continue;

                double lit = 1 - (_clickR - Math.Sqrt(q)) / ring;
                if (lit <= 0.1) continue;
                int k = (int)(lit * lit * Steps);
                if (k >= Steps) k = Steps - 1;

                dc.DrawLine(pens[s.Bucket, k],
                    new Point(s.X1 * w, s.Y1 * w),
                    new Point(s.X2 * w, s.Y2 * w));
            }
        }

        for (int t = 0; t < Trees; t++)
        {
            if (!_live[t]) continue;
            double head = _head[t];
            int from = _treeStart[t];
            int to = _treeEnd[t];

            for (int i = from; i < to; i++)
            {
                var s = Segs[i];
                double d = head - s.D1;
                if (d < -0.01 || d > tail) continue;

                double lit = 1 - d / tail;
                if (lit <= 0.1) continue;
                int k = (int)(lit * lit * Steps);
                if (k >= Steps) k = Steps - 1;

                dc.DrawLine(pens[s.Bucket, k],
                    new Point(s.X1 * w, s.Y1 * w),
                    new Point(s.X2 * w, s.Y2 * w));
            }
        }
    }

    private sealed class VeinLayer : Control
    {
        private readonly VeinField _owner;
        private readonly bool _soft;

        public VeinLayer(VeinField owner, bool soft)
        {
            _owner = owner;
            _soft = soft;
            IsHitTestVisible = false;
            ClipToBounds = false;
        }

        public override void Render(DrawingContext dc) => _owner.Paint(dc, Bounds.Size, _soft);
    }
}
