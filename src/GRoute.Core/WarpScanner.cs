using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public static class WarpScanner
{
    private static readonly string[] Prefixes =
    {
        "162.159.192", "162.159.193", "162.159.195",
        "188.114.96", "188.114.97", "188.114.98", "188.114.99"
    };

    private static readonly int[] Ports =
    {
        500, 854, 859, 864, 878, 880, 890, 891, 894, 903,
        908, 928, 934, 939, 942, 943, 945, 946, 955, 968,
        987, 988, 1002, 1010, 1014, 1018, 1070, 1074, 1180, 1387,
        1701, 1843, 2371, 2408, 2506, 3138, 3476, 3581, 3854, 4177,
        4198, 4233, 4500, 5279, 5956, 7103, 7152, 7156, 7281, 7559,
        8319, 8742, 8854, 8886
    };

    public sealed class Hit
    {
        public string Host { get; init; } = "";
        public int Port { get; init; }
        public int Latency { get; init; }
    }

    public static List<(string Host, int Port)> Candidates(int count)
    {
        var rnd = new System.Random();
        var seen = new HashSet<string>();
        var list = new List<(string, int)>();

        foreach (var (h, p) in Warp.DefaultEndpoints)
        {
            if (seen.Add($"{h}:{p}")) list.Add((h, p));
            if (list.Count >= count) return list;
        }

        int guard = 0;
        while (list.Count < count && guard++ < count * 40)
        {
            string host = $"{Prefixes[rnd.Next(Prefixes.Length)]}.{rnd.Next(1, 255)}";
            int port = Ports[rnd.Next(Ports.Length)];
            if (seen.Add($"{host}:{port}")) list.Add((host, port));
        }
        return list;
    }

    public static async Task<List<Hit>> Scan(
        Warp.Account account,
        int target,
        int maxProbes,
        int concurrency,
        int budgetMs,
        IProgress<(int Done, int Total, int Found)>? progress,
        CancellationToken ct)
    {
        var candidates = Candidates(maxProbes);
        var hits = new System.Collections.Concurrent.ConcurrentBag<Hit>();
        int done = 0;
        long deadline = Environment.TickCount64 + budgetMs;
        int wave = Math.Max(concurrency * 2, 8);
        int waves = 0;

        for (int i = 0; i < candidates.Count; i += wave)
        {
            if (ct.IsCancellationRequested) break;
            if (hits.Count >= target) break;
            if (Environment.TickCount64 > deadline) break;

            var slice = candidates.Skip(i).Take(wave).ToList();
            using var sem = new SemaphoreSlim(concurrency);

            var tasks = slice.Select(async c =>
            {
                try
                {
                    await sem.WaitAsync(ct);
                }
                catch
                {
                    return;
                }
                try
                {
                    if (ct.IsCancellationRequested) return;
                    var probe = Warp.BuildConfig(account, c.Host, c.Port, "probe");
                    int ms = await DelayTester.Measure(probe, 4000);
                    if (ms >= 0) hits.Add(new Hit { Host = c.Host, Port = c.Port, Latency = ms });
                }
                catch
                {
                }
                finally
                {
                    sem.Release();
                    progress?.Report((Interlocked.Increment(ref done), candidates.Count, hits.Count));
                }
            }).ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
            }

            waves++;
            if (waves >= 2 && hits.Count == 0) break;
        }

        return hits.OrderBy(h => h.Latency).ToList();
    }
}