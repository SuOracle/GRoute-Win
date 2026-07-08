using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using WM = System.Windows.Media;
using WPoint = System.Windows.Point;

namespace GRoute.Windows;

public static class Flags
{
    private const double W = 90.0;
    private const double H = 60.0;

    private static readonly Dictionary<string, WM.ImageSource?> _cache = new();

    private static readonly (string Key, string Iso)[] _names =
    {
        ("united states", "US"), ("usa", "US"), ("america", "US"),
        ("united kingdom", "GB"), ("britain", "GB"), ("england", "GB"), ("scotland", "GB"), ("uk", "GB"), ("london", "GB"),
        ("germany", "DE"), ("deutschland", "DE"), ("frankfurt", "DE"),
        ("netherlands", "NL"), ("netherland", "NL"), ("holland", "NL"), ("amsterdam", "NL"),
        ("france", "FR"), ("paris", "FR"),
        ("italy", "IT"), ("milan", "IT"),
        ("ireland", "IE"), ("dublin", "IE"),
        ("belgium", "BE"),
        ("luxembourg", "LU"),
        ("romania", "RO"),
        ("russia", "RU"), ("moscow", "RU"),
        ("hungary", "HU"),
        ("bulgaria", "BG"),
        ("lithuania", "LT"),
        ("latvia", "LV"),
        ("estonia", "EE"),
        ("austria", "AT"), ("vienna", "AT"),
        ("ukraine", "UA"),
        ("poland", "PL"), ("warsaw", "PL"),
        ("indonesia", "ID"),
        ("finland", "FI"), ("helsinki", "FI"),
        ("sweden", "SE"), ("stockholm", "SE"),
        ("denmark", "DK"),
        ("norway", "NO"),
        ("iceland", "IS"),
        ("greece", "GR"),
        ("japan", "JP"), ("tokyo", "JP"),
        ("switzerland", "CH"), ("zurich", "CH"),
        ("spain", "ES"), ("madrid", "ES"),
        ("portugal", "PT"), ("lisbon", "PT"),
        ("turkiye", "TR"), ("turkey", "TR"), ("istanbul", "TR"),
        ("china", "CN"), ("shanghai", "CN"), ("beijing", "CN"),
        ("singapore", "SG"),
        ("canada", "CA"), ("toronto", "CA"), ("montreal", "CA"),
        ("emirates", "AE"), ("dubai", "AE"), ("uae", "AE"),
        ("thailand", "TH"), ("bangkok", "TH"),
        ("vietnam", "VN"),
        ("israel", "IL"),
        ("india", "IN"), ("mumbai", "IN"),
        ("north korea", "KP"), ("south korea", "KR"), ("korea", "KR"), ("seoul", "KR"),
        ("czechia", "CZ"), ("czech", "CZ"), ("prague", "CZ"),
        ("albania", "AL"), ("serbia", "RS"), ("croatia", "HR"), ("slovenia", "SI"), ("slovakia", "SK"),
        ("bosnia", "BA"), ("montenegro", "ME"), ("north macedonia", "MK"), ("macedonia", "MK"), ("kosovo", "XK"),
        ("moldova", "MD"), ("belarus", "BY"), ("georgia", "GE"), ("armenia", "AM"), ("azerbaijan", "AZ"),
        ("kazakhstan", "KZ"), ("uzbekistan", "UZ"), ("turkmenistan", "TM"), ("kyrgyzstan", "KG"), ("tajikistan", "TJ"),
        ("mongolia", "MN"), ("malta", "MT"), ("cyprus", "CY"), ("monaco", "MC"), ("liechtenstein", "LI"),
        ("andorra", "AD"), ("san marino", "SM"),
        ("australia", "AU"), ("sydney", "AU"), ("new zealand", "NZ"),
        ("brazil", "BR"), ("argentina", "AR"), ("chile", "CL"), ("colombia", "CO"), ("peru", "PE"),
        ("mexico", "MX"), ("venezuela", "VE"), ("uruguay", "UY"), ("ecuador", "EC"), ("bolivia", "BO"),
        ("paraguay", "PY"), ("panama", "PA"), ("costa rica", "CR"), ("guatemala", "GT"), ("dominican", "DO"),
        ("south africa", "ZA"), ("egypt", "EG"), ("nigeria", "NG"), ("kenya", "KE"), ("morocco", "MA"),
        ("algeria", "DZ"), ("tunisia", "TN"), ("ghana", "GH"), ("ethiopia", "ET"), ("angola", "AO"),
        ("saudi", "SA"), ("qatar", "QA"), ("kuwait", "KW"), ("bahrain", "BH"), ("oman", "OM"),
        ("jordan", "JO"), ("lebanon", "LB"), ("iraq", "IQ"), ("iran", "IR"),
        ("pakistan", "PK"), ("bangladesh", "BD"), ("sri lanka", "LK"), ("nepal", "NP"),
        ("malaysia", "MY"), ("philippines", "PH"), ("taiwan", "TW"), ("hong kong", "HK"), ("macau", "MO"),
        ("cambodia", "KH"), ("laos", "LA"), ("myanmar", "MM"), ("brunei", "BN"),
    };

    private static readonly Dictionary<string, string?> _isoByName = new();
    private static readonly Dictionary<string, WM.ImageSource?> _packCache = new();
    private static readonly Dictionary<string, WM.ImageSource?> _chipCache = new();
    private static readonly char[] _seps = { ' ', '-', '_', '|', '.', ',', '/', '(', ')', '[', ']', ':', '@', '+' };
    private static readonly System.Collections.Generic.HashSet<string> _isoSet = BuildIsoSet();

    private static System.Collections.Generic.HashSet<string> BuildIsoSet()
    {
        var s = new System.Collections.Generic.HashSet<string>();
        foreach (var (_, iso) in _names) s.Add(iso);
        return s;
    }

    public static WM.ImageSource? ForName(string? name)
    {
        var iso = IsoFromName(name);
        if (iso == null) return null;
        return FromPack(iso) ?? Get(iso) ?? Chip(iso);
    }

    public static double WidthFor(string? name, double height)
    {
        var img = ForName(name);
        if (img == null || img.Height <= 0) return 0;
        double ratio = img.Width / img.Height;
        if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0) ratio = 1.5;
        if (ratio < 0.85) ratio = 0.85;
        if (ratio > 2.2) ratio = 2.2;
        return Math.Round(height * ratio);
    }

    public static string DisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name ?? "";
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length;)
        {
            int cp;
            int adv;
            if (i + 1 < name.Length && char.IsSurrogatePair(name[i], name[i + 1]))
            {
                cp = char.ConvertToUtf32(name[i], name[i + 1]);
                adv = 2;
            }
            else
            {
                cp = name[i];
                adv = 1;
            }
            bool ri = cp >= 0x1F1E6 && cp <= 0x1F1FF;
            bool vsel = cp == 0xFE0F;
            if (!ri && !vsel) sb.Append(name.Substring(i, adv));
            i += adv;
        }
        var s = sb.ToString().Trim();
        s = s.TrimStart('-', '|', '·', ':', '–', '—', ' ').Trim();
        return string.IsNullOrEmpty(s) ? name.Trim() : s;
    }

    public static string? IsoFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        int prev = -1;
        for (int i = 0; i < name.Length;)
        {
            int cp;
            int adv;
            if (i + 1 < name.Length && char.IsSurrogatePair(name[i], name[i + 1]))
            {
                cp = char.ConvertToUtf32(name[i], name[i + 1]);
                adv = 2;
            }
            else
            {
                cp = name[i];
                adv = 1;
            }
            if (cp >= 0x1F1E6 && cp <= 0x1F1FF)
            {
                int l = cp - 0x1F1E6;
                if (prev >= 0)
                    return string.Concat((char)('A' + prev), (char)('A' + l));
                prev = l;
            }
            else prev = -1;
            i += adv;
        }
        if (_isoByName.TryGetValue(name, out var cachedIso)) return cachedIso;
        var lower = name.ToLowerInvariant();
        string? found = null;
        foreach (var (key, iso) in _names)
            if (HasWord(lower, key)) { found = iso; break; }
        if (found == null)
        {
            foreach (var tok in name.Split(_seps, StringSplitOptions.RemoveEmptyEntries))
                if (tok.Length == 2 && char.IsUpper(tok[0]) && char.IsUpper(tok[1]) && _isoSet.Contains(tok))
                { found = tok; break; }
        }
        _isoByName[name] = found;
        return found;
    }

    private static bool HasWord(string hay, string key)
    {
        int idx = 0;
        while ((idx = hay.IndexOf(key, idx, StringComparison.Ordinal)) >= 0)
        {
            bool left = idx == 0 || !char.IsLetter(hay[idx - 1]);
            int end = idx + key.Length;
            bool right = end == hay.Length || !char.IsLetter(hay[end]);
            if (left && right) return true;
            idx = end;
        }
        return false;
    }

    private static WM.ImageSource? FromPack(string iso)
    {
        if (_packCache.TryGetValue(iso, out var cached)) return cached;
        WM.ImageSource? img = null;
        try
        {
            var path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "Flags", iso.ToLowerInvariant() + ".png");
            if (System.IO.File.Exists(path))
            {
                var bmp = new WM.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = WM.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                img = bmp;
            }
        }
        catch { img = null; }
        _packCache[iso] = img;
        return img;
    }

    private static WM.ImageSource Chip(string iso)
    {
        if (_chipCache.TryGetValue(iso, out var cached) && cached != null) return cached;
        var g = new WM.DrawingGroup();
        g.Children.Add(Rect(0, 0, W, H, Br(0x24, 0x38, 0x60)));
        var fg = Br(0xCF, 0xDD, 0xF4);
        try
        {
            var text = new WM.FormattedText(
                iso,
                CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new WM.Typeface("Segoe UI Semibold"),
                H * 0.46,
                fg,
                1.0);
            var geo = text.BuildGeometry(new WPoint((W - text.Width) / 2, (H - text.Height) / 2));
            g.Children.Add(new WM.GeometryDrawing(fg, null, geo));
        }
        catch { }
        var img = Make(g);
        _chipCache[iso] = img;
        return img;
    }

    public static WM.ImageSource? Get(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        iso = iso.ToUpperInvariant();
        if (_cache.TryGetValue(iso, out var cached)) return cached;
        WM.ImageSource? img;
        try { img = Build(iso); }
        catch { img = null; }
        _cache[iso] = img;
        return img;
    }

    private static WM.ImageSource? Build(string iso)
    {
        switch (iso)
        {
            case "DE": return Make(HStripes(Br(0, 0, 0), Br(0xDD, 0, 0), Br(0xFF, 0xCE, 0)));
            case "NL": return Make(HStripes(Br(0xAE, 0x1C, 0x28), Br(0xFF, 0xFF, 0xFF), Br(0x21, 0x46, 0x8B)));
            case "RU": return Make(HStripes(Br(0xFF, 0xFF, 0xFF), Br(0, 0x39, 0xA6), Br(0xD5, 0x2B, 0x1E)));
            case "HU": return Make(HStripes(Br(0xCE, 0x25, 0x39), Br(0xFF, 0xFF, 0xFF), Br(0x47, 0x70, 0x50)));
            case "BG": return Make(HStripes(Br(0xFF, 0xFF, 0xFF), Br(0, 0x96, 0x6E), Br(0xD6, 0x26, 0x12)));
            case "LT": return Make(HStripes(Br(0xFD, 0xB9, 0x13), Br(0, 0x6A, 0x44), Br(0xC1, 0x27, 0x2D)));
            case "AT": return Make(HStripes(Br(0xED, 0x29, 0x39), Br(0xFF, 0xFF, 0xFF), Br(0xED, 0x29, 0x39)));
            case "LV": return Make(HStripes(Br(0x9E, 0x30, 0x39), Br(0xFF, 0xFF, 0xFF), Br(0x9E, 0x30, 0x39)));
            case "EE": return Make(HStripes(Br(0, 0x72, 0xCE), Br(0, 0, 0), Br(0xFF, 0xFF, 0xFF)));
            case "FR": return Make(VStripes(Br(0, 0x23, 0x95), Br(0xFF, 0xFF, 0xFF), Br(0xED, 0x29, 0x39)));
            case "IT": return Make(VStripes(Br(0, 0x92, 0x46), Br(0xFF, 0xFF, 0xFF), Br(0xCE, 0x2B, 0x37)));
            case "IE": return Make(VStripes(Br(0x16, 0x9B, 0x62), Br(0xFF, 0xFF, 0xFF), Br(0xFF, 0x88, 0x3E)));
            case "BE": return Make(VStripes(Br(0, 0, 0), Br(0xFD, 0xDA, 0x24), Br(0xEF, 0x33, 0x40)));
            case "RO": return Make(VStripes(Br(0, 0x2B, 0x7F), Br(0xFC, 0xD1, 0x16), Br(0xCE, 0x11, 0x26)));
            case "UA": return Make(HStripes(Br(0, 0x57, 0xB7), Br(0xFF, 0xD7, 0)));
            case "ID": return Make(HStripes(Br(0xCE, 0x11, 0x26), Br(0xFF, 0xFF, 0xFF)));
            case "PL": return Make(HStripes(Br(0xFF, 0xFF, 0xFF), Br(0xDC, 0x14, 0x3C)));
            case "FI": return Make(Nordic(Br(0xFF, 0xFF, 0xFF), Br(0, 0x35, 0x80), null));
            case "SE": return Make(Nordic(Br(0, 0x6A, 0xA7), Br(0xFE, 0xCC, 0), null));
            case "DK": return Make(Nordic(Br(0xC6, 0x0C, 0x30), Br(0xFF, 0xFF, 0xFF), null));
            case "NO": return Make(Nordic(Br(0xEF, 0x2B, 0x2D), Br(0xFF, 0xFF, 0xFF), Br(0, 0x20, 0x5B)));
            case "IS": return Make(Nordic(Br(0x02, 0x52, 0x9C), Br(0xFF, 0xFF, 0xFF), Br(0xDC, 0x1E, 0x35)));
            case "GR": return Greece();
            case "JP": return Make(Rect(0, 0, W, H, Br(0xFF, 0xFF, 0xFF)), Ellipse(W / 2, H / 2, H * 0.30, H * 0.30, Br(0xBC, 0, 0x2D)));
            case "CH": return Swiss();
            case "ES": return Make(Rect(0, 0, W, H * 0.25, Br(0xAA, 0x15, 0x1B)), Rect(0, H * 0.25, W, H * 0.5, Br(0xF1, 0xBF, 0)), Rect(0, H * 0.75, W, H * 0.25, Br(0xAA, 0x15, 0x1B)));
            case "US": return Usa();
            case "GB": return Union();
            case "TR": return Turkey();
            case "CN": return China();
            case "SG": return Singapore();
            case "CA": return Canada();
            case "AE": return Uae();
            case "TH": return Thailand();
            case "VN": return Make(Rect(0, 0, W, H, Br(0xDA, 0x25, 0x1D)), new WM.GeometryDrawing(Br(0xFF, 0xFF, 0), null, Star(W / 2, H / 2, H * 0.30, 0)));
            case "IL": return Israel();
            case "IN": return India();
            case "KR": return Korea();
            case "CZ": return Czech();
            default: return null;
        }
    }

    private static WM.SolidColorBrush Br(byte r, byte g, byte b)
    {
        var br = new WM.SolidColorBrush(WM.Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    private static WM.GeometryDrawing Rect(double x, double y, double w, double h, WM.Brush fill)
        => new WM.GeometryDrawing(fill, null, new WM.RectangleGeometry(new Rect(x, y, w, h)));

    private static WM.GeometryDrawing Ellipse(double cx, double cy, double rx, double ry, WM.Brush fill)
        => new WM.GeometryDrawing(fill, null, new WM.EllipseGeometry(new WPoint(cx, cy), rx, ry));

    private static WM.Drawing HStripes(params WM.Brush[] c)
    {
        var g = new WM.DrawingGroup();
        double h = H / c.Length;
        for (int i = 0; i < c.Length; i++) g.Children.Add(Rect(0, i * h, W, h, c[i]));
        return g;
    }

    private static WM.Drawing VStripes(params WM.Brush[] c)
    {
        var g = new WM.DrawingGroup();
        double w = W / c.Length;
        for (int i = 0; i < c.Length; i++) g.Children.Add(Rect(i * w, 0, w, H, c[i]));
        return g;
    }

    private static WM.Drawing Nordic(WM.Brush field, WM.Brush cross, WM.Brush? inner)
    {
        var g = new WM.DrawingGroup();
        g.Children.Add(Rect(0, 0, W, H, field));
        double vx = W * 0.34;
        double hy = H * 0.5;
        double to = inner != null ? H * 0.34 : H * 0.24;
        g.Children.Add(Rect(vx - to / 2, 0, to, H, cross));
        g.Children.Add(Rect(0, hy - to / 2, W, to, cross));
        if (inner != null)
        {
            double ti = H * 0.16;
            g.Children.Add(Rect(vx - ti / 2, 0, ti, H, inner));
            g.Children.Add(Rect(0, hy - ti / 2, W, ti, inner));
        }
        return g;
    }

    private static WM.ImageSource Greece()
    {
        var g = new WM.DrawingGroup();
        var blue = Br(0, 0x57, 0xB8);
        var white = Br(0xFF, 0xFF, 0xFF);
        double sh = H / 9.0;
        for (int i = 0; i < 9; i++) g.Children.Add(Rect(0, i * sh, W, sh, i % 2 == 0 ? blue : white));
        double c = sh * 5;
        g.Children.Add(Rect(0, 0, c, c, blue));
        double t = c * 0.2;
        g.Children.Add(Rect(c / 2 - t / 2, 0, t, c, white));
        g.Children.Add(Rect(0, c / 2 - t / 2, c, t, white));
        return Make(g);
    }

    private static WM.ImageSource Swiss()
    {
        var white = Br(0xFF, 0xFF, 0xFF);
        double th = H * 0.16;
        double len = H * 0.62;
        return Make(
            Rect(0, 0, W, H, Br(0xD5, 0x2B, 0x1E)),
            Rect(W / 2 - th / 2, H / 2 - len / 2, th, len, white),
            Rect(W / 2 - len / 2, H / 2 - th / 2, len, th, white));
    }

    private static WM.ImageSource Usa()
    {
        var g = new WM.DrawingGroup();
        var red = Br(0xB2, 0x22, 0x34);
        var white = Br(0xFF, 0xFF, 0xFF);
        var blue = Br(0x3C, 0x3B, 0x6E);
        double sh = H / 13.0;
        for (int i = 0; i < 13; i++) g.Children.Add(Rect(0, i * sh, W, sh, i % 2 == 0 ? red : white));
        double cw = W * 0.42;
        double ch = sh * 7;
        g.Children.Add(Rect(0, 0, cw, ch, blue));
        double r = H * 0.028;
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 5; col++)
            {
                double dx = cw * (0.12 + col * 0.19);
                double dy = ch * (0.16 + row * 0.23);
                g.Children.Add(Ellipse(dx, dy, r, r, white));
            }
        return Make(g);
    }

    private static WM.ImageSource Union()
    {
        var g = new WM.DrawingGroup();
        var blue = Br(0x01, 0x22, 0x69);
        var white = Br(0xFF, 0xFF, 0xFF);
        var red = Br(0xC8, 0x10, 0x2E);
        g.Children.Add(Rect(0, 0, W, H, blue));
        var wp = new WM.Pen(white, H * 0.30);
        wp.Freeze();
        var rp = new WM.Pen(red, H * 0.12);
        rp.Freeze();
        var d1 = new WM.LineGeometry(new WPoint(0, 0), new WPoint(W, H));
        var d2 = new WM.LineGeometry(new WPoint(W, 0), new WPoint(0, H));
        g.Children.Add(new WM.GeometryDrawing(null, wp, d1));
        g.Children.Add(new WM.GeometryDrawing(null, wp, d2));
        g.Children.Add(new WM.GeometryDrawing(null, rp, d1));
        g.Children.Add(new WM.GeometryDrawing(null, rp, d2));
        g.Children.Add(Rect(W / 2 - H * 0.20, 0, H * 0.40, H, white));
        g.Children.Add(Rect(0, H / 2 - H * 0.20, W, H * 0.40, white));
        g.Children.Add(Rect(W / 2 - H * 0.12, 0, H * 0.24, H, red));
        g.Children.Add(Rect(0, H / 2 - H * 0.12, W, H * 0.24, red));
        return Make(g);
    }

    private static WM.ImageSource Turkey()
    {
        var red = Br(0xE3, 0x0A, 0x17);
        var white = Br(0xFF, 0xFF, 0xFF);
        return Make(
            Rect(0, 0, W, H, red),
            Ellipse(W * 0.36, H / 2, H * 0.26, H * 0.26, white),
            Ellipse(W * 0.42, H / 2, H * 0.20, H * 0.20, red),
            new WM.GeometryDrawing(white, null, Star(W * 0.58, H / 2, H * 0.15, -0.35)));
    }

    private static WM.ImageSource China()
    {
        var g = new WM.DrawingGroup();
        var yellow = Br(0xFF, 0xDE, 0);
        g.Children.Add(Rect(0, 0, W, H, Br(0xDE, 0x29, 0x10)));
        g.Children.Add(new WM.GeometryDrawing(yellow, null, Star(W * 0.16, H * 0.28, H * 0.16, 0)));
        (double x, double y, double rot)[] small =
        {
            (W * 0.32, H * 0.12, 0.9),
            (W * 0.38, H * 0.24, 0.4),
            (W * 0.38, H * 0.40, -0.2),
            (W * 0.32, H * 0.52, -0.7),
        };
        foreach (var s in small)
            g.Children.Add(new WM.GeometryDrawing(yellow, null, Star(s.x, s.y, H * 0.055, s.rot)));
        return Make(g);
    }

    private static WM.ImageSource Singapore()
    {
        var g = new WM.DrawingGroup();
        var red = Br(0xED, 0x29, 0x39);
        var white = Br(0xFF, 0xFF, 0xFF);
        g.Children.Add(Rect(0, 0, W, H / 2, red));
        g.Children.Add(Rect(0, H / 2, W, H / 2, white));
        g.Children.Add(Ellipse(W * 0.17, H * 0.25, H * 0.17, H * 0.17, white));
        g.Children.Add(Ellipse(W * 0.23, H * 0.25, H * 0.14, H * 0.14, red));
        (double x, double y)[] stars =
        {
            (W * 0.30, H * 0.14),
            (W * 0.40, H * 0.20),
            (W * 0.43, H * 0.32),
            (W * 0.36, H * 0.40),
            (W * 0.27, H * 0.34),
        };
        foreach (var s in stars)
            g.Children.Add(new WM.GeometryDrawing(white, null, Star(s.x, s.y, H * 0.05, 0)));
        return Make(g);
    }

    private static WM.Geometry Star(double cx, double cy, double r, double rot)
    {
        var pts = new List<WPoint>();
        for (int i = 0; i < 5; i++)
        {
            double ao = rot + i * (2 * Math.PI / 5) - Math.PI / 2;
            pts.Add(new WPoint(cx + r * Math.Cos(ao), cy + r * Math.Sin(ao)));
            double ai = ao + Math.PI / 5;
            pts.Add(new WPoint(cx + r * 0.40 * Math.Cos(ai), cy + r * 0.40 * Math.Sin(ai)));
        }
        var fig = new WM.PathFigure { StartPoint = pts[0], IsClosed = true };
        fig.Segments.Add(new WM.PolyLineSegment(pts.GetRange(1, pts.Count - 1), true));
        var pg = new WM.PathGeometry();
        pg.Figures.Add(fig);
        pg.Freeze();
        return pg;
    }

    private static WM.Pen FrozenPen(WM.Brush b, double t)
    {
        var p = new WM.Pen(b, t) { LineJoin = WM.PenLineJoin.Round, StartLineCap = WM.PenLineCap.Round, EndLineCap = WM.PenLineCap.Round };
        p.Freeze();
        return p;
    }

    private static WM.Geometry Triangle(double cx, double cy, double r, bool down)
    {
        double d = down ? -1 : 1;
        var p1 = new WPoint(cx, cy - d * r);
        var p2 = new WPoint(cx - r * 0.866, cy + d * r * 0.5);
        var p3 = new WPoint(cx + r * 0.866, cy + d * r * 0.5);
        var fig = new WM.PathFigure { StartPoint = p1, IsClosed = true };
        fig.Segments.Add(new WM.PolyLineSegment(new[] { p2, p3 }, true));
        var pg = new WM.PathGeometry();
        pg.Figures.Add(fig);
        pg.Freeze();
        return pg;
    }

    private static WM.ImageSource Canada()
    {
        var g = new WM.DrawingGroup();
        var red = Br(0xD5, 0x2B, 0x1E);
        g.Children.Add(Rect(0, 0, W, H, Br(0xFF, 0xFF, 0xFF)));
        g.Children.Add(Rect(0, 0, W * 0.25, H, red));
        g.Children.Add(Rect(W * 0.75, 0, W * 0.25, H, red));
        var leaf = WM.Geometry.Parse("M383.8 351.7c2.5-2.5 105.2-92.4 105.2-92.4l-17.5-7.5c-10-4.9-7.4-11.5-5-17.4 2.4-7.6 20.1-67.3 20.1-67.3s-47.4 10-57.3 12.5c-7.5 2.4-10-2.5-12.5-7.5s-15-32.4-15-32.4-52.3 59.8-54.8 62.3c-10 7.5-20.1 0-17.6-10 0-10 27.6-129.6 27.6-129.6s-30.1 17.5-40.1 22.5c-7.5 5-12.5 5-17.5-5C312.7 62.4 256 0 256 0s-56.7 62.4-71.7 90.4c-5 10-10 10-17.5 5-10-5-40.1-22.5-40.1-22.5S154.3 197.9 154.3 207.9c2.5 10-7.5 17.5-17.6 10-2.5-2.5-54.8-62.3-54.8-62.3s-12.5 27.4-15 32.4-5 9.9-12.5 7.5c-9.9-2.5-57.3-12.5-57.3-12.5s17.7 59.7 20.1 67.3c2.4 5.9 5 12.5-5 17.4L0 259.3s102.7 89.9 105.2 92.4c5.1 5 10 7.5 5.1 22.5-5.1 15-10.1 35-10.1 35s95.2-20 105.3-22.5c8.7-2.7 17.5 2.5 17.5 12.5S215.4 512 215.4 512h81.3s-7.6-90.3-7.6-100.3 8.8-15.2 17.5-12.5c10.1 2.5 105.3 22.5 105.3 22.5s-5-20-10.1-35c-4.9-15 0-17.5 5.1-22.5z");
        double s = 42.0 / 512.0;
        double lw = 489.0 * s, lh = 512.0 * s;
        var tg = new WM.TransformGroup();
        tg.Children.Add(new WM.ScaleTransform(s, s));
        tg.Children.Add(new WM.TranslateTransform(W / 2 - lw / 2, H / 2 - lh / 2));
        leaf.Transform = tg;
        g.Children.Add(new WM.GeometryDrawing(red, null, leaf));
        return Make(g);
    }

    private static WM.ImageSource Uae()
    {
        var g = new WM.DrawingGroup();
        g.Children.Add(Rect(0, 0, W * 0.25, H, Br(0xEF, 0x33, 0x40)));
        double x = W * 0.25, w = W * 0.75, h = H / 3.0;
        g.Children.Add(Rect(x, 0, w, h, Br(0x00, 0x73, 0x2F)));
        g.Children.Add(Rect(x, h, w, h, Br(0xFF, 0xFF, 0xFF)));
        g.Children.Add(Rect(x, 2 * h, w, h, Br(0x00, 0x00, 0x00)));
        return Make(g);
    }

    private static WM.ImageSource Thailand()
    {
        var g = new WM.DrawingGroup();
        var red = Br(0xA5, 0x19, 0x31);
        var white = Br(0xFF, 0xFF, 0xFF);
        var blue = Br(0x24, 0x1D, 0x4F);
        double h = H / 6.0;
        g.Children.Add(Rect(0, 0, W, h, red));
        g.Children.Add(Rect(0, h, W, h, white));
        g.Children.Add(Rect(0, 2 * h, W, 2 * h, blue));
        g.Children.Add(Rect(0, 4 * h, W, h, white));
        g.Children.Add(Rect(0, 5 * h, W, h, red));
        return Make(g);
    }

    private static WM.ImageSource Israel()
    {
        var g = new WM.DrawingGroup();
        var blue = Br(0x00, 0x38, 0xB8);
        g.Children.Add(Rect(0, 0, W, H, Br(0xFF, 0xFF, 0xFF)));
        g.Children.Add(Rect(0, H * 0.18, W, H * 0.11, blue));
        g.Children.Add(Rect(0, H * 0.71, W, H * 0.11, blue));
        double cx = W / 2, cy = H / 2, r = H * 0.20;
        var pen = FrozenPen(blue, H * 0.035);
        g.Children.Add(new WM.GeometryDrawing(null, pen, Triangle(cx, cy, r, false)));
        g.Children.Add(new WM.GeometryDrawing(null, pen, Triangle(cx, cy, r, true)));
        return Make(g);
    }

    private static WM.ImageSource India()
    {
        var g = new WM.DrawingGroup();
        g.Children.Add(Rect(0, 0, W, H / 3.0, Br(0xFF, 0x99, 0x33)));
        g.Children.Add(Rect(0, H / 3.0, W, H / 3.0, Br(0xFF, 0xFF, 0xFF)));
        g.Children.Add(Rect(0, 2 * H / 3.0, W, H / 3.0, Br(0x13, 0x88, 0x08)));
        var navy = Br(0x00, 0x00, 0x80);
        double cx = W / 2, cy = H / 2, r = H * 0.15;
        g.Children.Add(new WM.GeometryDrawing(null, FrozenPen(navy, H * 0.012), new WM.EllipseGeometry(new WPoint(cx, cy), r, r)));
        g.Children.Add(Ellipse(cx, cy, r * 0.16, r * 0.16, navy));
        for (int i = 0; i < 16; i++)
        {
            double a = i * 2 * Math.PI / 16;
            var line = new WM.LineGeometry(new WPoint(cx, cy), new WPoint(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
            g.Children.Add(new WM.GeometryDrawing(null, FrozenPen(navy, H * 0.008), line));
        }
        return Make(g);
    }

    private static WM.ImageSource Czech()
    {
        var g = new WM.DrawingGroup();
        g.Children.Add(Rect(0, 0, W, H / 2, Br(0xFF, 0xFF, 0xFF)));
        g.Children.Add(Rect(0, H / 2, W, H / 2, Br(0xD7, 0x14, 0x1A)));
        var tri = new WM.PathFigure { StartPoint = new WPoint(0, 0), IsClosed = true };
        tri.Segments.Add(new WM.PolyLineSegment(new[] { new WPoint(W * 0.5, H / 2), new WPoint(0, H) }, true));
        var pg = new WM.PathGeometry();
        pg.Figures.Add(tri);
        g.Children.Add(new WM.GeometryDrawing(Br(0x11, 0x45, 0x7E), null, pg));
        return Make(g);
    }

    private static WM.Drawing Trigram(double cx, double cy, double deg, bool[] solid)
    {
        var g = new WM.DrawingGroup();
        var black = Br(0, 0, 0);
        double bw = W * 0.13, bh = H * 0.028, gap = H * 0.024;
        double totalH = 3 * bh + 2 * gap;
        double y0 = cy - totalH / 2;
        for (int i = 0; i < 3; i++)
        {
            double y = y0 + i * (bh + gap);
            double x = cx - bw / 2;
            if (solid[i]) g.Children.Add(Rect(x, y, bw, bh, black));
            else
            {
                double seg = bw * 0.42;
                g.Children.Add(Rect(x, y, seg, bh, black));
                g.Children.Add(Rect(x + bw - seg, y, seg, bh, black));
            }
        }
        var rot = new WM.RotateTransform(deg, cx, cy);
        rot.Freeze();
        g.Transform = rot;
        return g;
    }

    private static WM.ImageSource Korea()
    {
        var g = new WM.DrawingGroup();
        g.Children.Add(Rect(0, 0, W, H, Br(0xFF, 0xFF, 0xFF)));
        var tae = new WM.DrawingGroup();
        var red = Br(0xC6, 0x0C, 0x30);
        var blue = Br(0x00, 0x38, 0xA8);
        double cx = W / 2, cy = H / 2, R = H * 0.22;
        tae.Children.Add(Ellipse(cx, cy, R, R, red));
        var semi = new WM.PathFigure { StartPoint = new WPoint(cx - R, cy), IsClosed = true };
        semi.Segments.Add(new WM.ArcSegment(new WPoint(cx + R, cy), new System.Windows.Size(R, R), 0, false, WM.SweepDirection.Counterclockwise, true));
        var semipg = new WM.PathGeometry();
        semipg.Figures.Add(semi);
        tae.Children.Add(new WM.GeometryDrawing(blue, null, semipg));
        tae.Children.Add(Ellipse(cx, cy - R / 2, R / 2, R / 2, blue));
        tae.Children.Add(Ellipse(cx, cy + R / 2, R / 2, R / 2, red));
        var rot = new WM.RotateTransform(-33, cx, cy);
        rot.Freeze();
        tae.Transform = rot;
        g.Children.Add(tae);
        g.Children.Add(Trigram(W * 0.21, H * 0.26, 33, new[] { true, true, true }));
        g.Children.Add(Trigram(W * 0.21, H * 0.74, -33, new[] { true, false, true }));
        g.Children.Add(Trigram(W * 0.79, H * 0.26, -33, new[] { false, true, false }));
        g.Children.Add(Trigram(W * 0.79, H * 0.74, 33, new[] { false, false, false }));
        return Make(g);
    }

    private static WM.ImageSource Make(params WM.Drawing[] parts)
    {
        var g = new WM.DrawingGroup();
        foreach (var p in parts) g.Children.Add(p);
        g.ClipGeometry = new WM.RectangleGeometry(new Rect(0, 0, W, H), 7, 7);
        var img = new WM.DrawingImage(g);
        img.Freeze();
        return img;
    }
}

public sealed class FlagConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Flags.ForName(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class FlagNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Flags.DisplayName(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class FlagWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double h = 18;
        if (parameter != null && double.TryParse(parameter.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var ph)) h = ph;
        return Flags.WidthFor(value as string, h);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
