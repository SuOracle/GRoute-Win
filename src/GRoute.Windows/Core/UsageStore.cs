using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GRoute.Windows.Core;

public static class UsageStore
{
    public sealed record Bar(string Label, string Short, long Up, long Down)
    {
        public long Total => Up + Down;
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const int HourlyRetentionHours = 24 * 31;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute", "usage.json");

    private static readonly object Lock = new();
    private static Dictionary<string, long[]> _daily = new();
    private static Dictionary<string, long[]> _hourly = new();
    private static Dictionary<string, long[]> _byConfig = new();
    public static string? CurrentConfigKey;
    private static bool _loaded;
    private static int _ticks;

    public static void Add(long up, long down)
    {
        if (up <= 0 && down <= 0) return;
        lock (Lock)
        {
            EnsureLoaded();
            var now = DateTime.Now;
            Accumulate(_daily, now.ToString("yyyy-MM-dd", Inv), up, down);
            Accumulate(_hourly, now.ToString("yyyy-MM-dd-HH", Inv), up, down);
            if (!string.IsNullOrEmpty(CurrentConfigKey)) Accumulate(_byConfig, CurrentConfigKey!, up, down);
            TrimHourly();
            if (++_ticks >= 5)
            {
                Persist();
                _ticks = 0;
            }
        }
    }

    public static void Flush()
    {
        lock (Lock)
        {
            if (_loaded)
            {
                Persist();
                _ticks = 0;
            }
        }
    }

    public static long[] TotalAll()
    {
        lock (Lock)
        {
            EnsureLoaded();
            long up = 0, down = 0;
            foreach (var v in _daily.Values)
            {
                up += v[0];
                down += v[1];
            }
            return new[] { up, down };
        }
    }

    public static (string Name, long Total)? TopConfig()
    {
        lock (Lock)
        {
            EnsureLoaded();
            string? best = null;
            long bestTotal = 0;
            foreach (var kv in _byConfig)
            {
                long tot = kv.Value[0] + kv.Value[1];
                if (tot > bestTotal)
                {
                    bestTotal = tot;
                    best = kv.Key;
                }
            }
            return best is null ? null : (best, bestTotal);
        }
    }

    public static List<Bar> HourlyToday()
    {
        lock (Lock)
        {
            EnsureLoaded();
            var now = DateTime.Now;
            var cursor = DateTime.Today;
            var end = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            var list = new List<Bar>();
            while (cursor <= end)
            {
                AddHourBar(list, cursor);
                cursor = cursor.AddHours(1);
            }
            return list;
        }
    }

    public static List<Bar> HourlyBarsRange(DateTime from, DateTime to)
    {
        lock (Lock)
        {
            EnsureLoaded();
            var lo = from <= to ? from : to;
            var hi = from <= to ? to : from;
            var cursor = new DateTime(lo.Year, lo.Month, lo.Day, 0, 0, 0);
            var end = new DateTime(hi.Year, hi.Month, hi.Day, 23, 0, 0);
            var list = new List<Bar>();
            while (cursor <= end)
            {
                AddHourBar(list, cursor);
                cursor = cursor.AddHours(1);
            }
            return list;
        }
    }

    public static List<Bar> DailyBars(int days)
    {
        lock (Lock)
        {
            EnsureLoaded();
            var today = DateTime.Today;
            var list = new List<Bar>();
            for (int back = days - 1; back >= 0; back--)
            {
                AddDayBar(list, today.AddDays(-back));
            }
            return list;
        }
    }

    public static List<Bar> DailyBarsRange(DateTime from, DateTime to)
    {
        lock (Lock)
        {
            EnsureLoaded();
            var cursor = (from <= to ? from : to).Date;
            var end = (from <= to ? to : from).Date;
            var list = new List<Bar>();
            while (cursor <= end)
            {
                AddDayBar(list, cursor);
                cursor = cursor.AddDays(1);
            }
            return list;
        }
    }

    public static long[] Sum(IEnumerable<Bar> bars)
    {
        long up = 0, down = 0;
        foreach (var b in bars)
        {
            up += b.Up;
            down += b.Down;
        }
        return new[] { up, down };
    }

    private static void AddHourBar(List<Bar> list, DateTime slot)
    {
        var v = Get(_hourly, slot.ToString("yyyy-MM-dd-HH", Inv));
        var hh = slot.Hour.ToString("00", Inv);
        var nn = ((slot.Hour + 1) % 24).ToString("00", Inv);
        list.Add(new Bar($"{hh}:00-{nn}:00", hh, v[0], v[1]));
    }

    private static void AddDayBar(List<Bar> list, DateTime d)
    {
        var v = Get(_daily, d.ToString("yyyy-MM-dd", Inv));
        var lbl = d.Month.ToString(Inv) + "/" + d.Day.ToString(Inv);
        list.Add(new Bar(lbl, lbl, v[0], v[1]));
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(FilePath));
                if (data is not null)
                {
                    _daily = data.Daily ?? new();
                    _hourly = data.Hourly ?? new();
                    _byConfig = data.ByConfig ?? new();
                }
            }
        }
        catch
        {
            _daily = new();
            _hourly = new();
            _byConfig = new();
        }
        _loaded = true;
    }

    private static void Accumulate(Dictionary<string, long[]> map, string key, long up, long down)
    {
        if (map.TryGetValue(key, out var cur))
        {
            cur[0] += up;
            cur[1] += down;
        }
        else
        {
            map[key] = new[] { up, down };
        }
    }

    private static long[] Get(Dictionary<string, long[]> map, string key)
        => map.TryGetValue(key, out var v) ? v : new long[] { 0, 0 };

    private static void TrimHourly()
    {
        var cutoff = DateTime.Now.AddHours(-HourlyRetentionHours);
        var stale = _hourly.Keys.Where(k =>
            DateTime.TryParseExact(k, "yyyy-MM-dd-HH", Inv, DateTimeStyles.None, out var t) && t < cutoff).ToList();
        foreach (var k in stale) _hourly.Remove(k);
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new StoreData { Daily = _daily, Hourly = _hourly, ByConfig = _byConfig }));
        }
        catch
        {
        }
    }

    private sealed class StoreData
    {
        public Dictionary<string, long[]>? Daily { get; set; }
        public Dictionary<string, long[]>? Hourly { get; set; }
        public Dictionary<string, long[]>? ByConfig { get; set; }
    }
}
