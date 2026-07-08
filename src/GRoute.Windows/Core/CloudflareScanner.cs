using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public sealed record ScanResult(string Ip, int Ms)
{
    public string MsText => Ms + " ms";
}

public static class CloudflareScanner
{
    private static readonly string[] Cidrs =
    {
        "104.16.0.0/13", "104.24.0.0/14", "172.64.0.0/13", "162.158.0.0/15",
        "141.101.64.0/18", "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20",
        "198.41.128.0/17", "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22"
    };

    private static readonly (uint Base, uint Mask)[] Ranges = Cidrs.Select(Parse).ToArray();
    private static readonly Random Rng = new();

    public static async Task<List<ScanResult>> Scan(int sample, int port, int top,
        IProgress<int>? progress, CancellationToken ct)
    {
        var ips = RandomIps(sample);
        var results = new ConcurrentBag<ScanResult>();
        int done = 0;
        using var sem = new SemaphoreSlim(32);

        var tasks = ips.Select(async ip =>
        {
            try
            {
                await sem.WaitAsync(ct);
                try
                {
                    int ms = await TcpPing(ip, port, 3000, ct);
                    if (ms >= 0) results.Add(new ScanResult(ip, ms));
                }
                finally
                {
                    sem.Release();
                    progress?.Report(Interlocked.Increment(ref done));
                }
            }
            catch (OperationCanceledException)
            {
            }
        }).ToList();

        await Task.WhenAll(tasks);
        return results.OrderBy(r => r.Ms).Take(top).ToList();
    }

    private static async Task<int> TcpPing(string ip, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            var sw = Stopwatch.StartNew();
            await client.ConnectAsync(ip, port, cts.Token);
            sw.Stop();
            return (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    private static List<string> RandomIps(int n)
    {
        var set = new HashSet<string>();
        int guard = 0;
        while (set.Count < n && guard < n * 20)
        {
            guard++;
            var (baseIp, mask) = Ranges[Rng.Next(Ranges.Length)];
            uint host = (uint)Rng.Next() & ~mask;
            set.Add(ToIp((baseIp & mask) | host));
        }
        return set.ToList();
    }

    private static (uint, uint) Parse(string cidr)
    {
        var parts = cidr.Split('/');
        uint ip = ToUint(parts[0]);
        int prefix = int.Parse(parts[1]);
        uint mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        return (ip, mask);
    }

    private static uint ToUint(string ip)
    {
        var o = ip.Split('.');
        return ((uint)byte.Parse(o[0]) << 24) | ((uint)byte.Parse(o[1]) << 16)
             | ((uint)byte.Parse(o[2]) << 8) | byte.Parse(o[3]);
    }

    private static string ToIp(uint v)
        => $"{(v >> 24) & 0xFF}.{(v >> 16) & 0xFF}.{(v >> 8) & 0xFF}.{v & 0xFF}";
}
