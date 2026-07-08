using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public sealed record QualityResult(int? IdlePing, int? DownPing, int? UpPing, int? Jitter, double? DownMbps, double? UpMbps);

public static class QualityTest
{
    private const string DelayUrl = "https://www.gstatic.com/generate_204";
    private const string DownloadUrl = "https://speed.cloudflare.com/__down?bytes=26214400";
    private const string UploadUrl = "https://speed.cloudflare.com/__up";

    private static HttpClient Make(int httpPort, TimeSpan timeout)
    {
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy("127.0.0.1", httpPort),
            UseProxy = true
        };
        var client = new HttpClient(handler) { Timeout = timeout };
        client.DefaultRequestHeaders.Add("User-Agent", "GRoute");
        return client;
    }

    public static async Task<QualityResult> Run(int httpPort, IProgress<string>? stage,
        IProgress<(string Phase, double Mbps)>? live, CancellationToken ct)
    {
        await Delay(httpPort, ct);

        stage?.Report("latency");
        var (idle, jitter) = await PingJitter(httpPort, ct);

        stage?.Report("download");
        var (down, downPing) = await Loaded("download", httpPort, live,
            (l, token) => Download(httpPort, l, token), ct);

        stage?.Report("upload");
        var (up, upPing) = await Loaded("upload", httpPort, live,
            (l, token) => Upload(httpPort, l, token), ct);

        return new QualityResult(idle, downPing, upPing, jitter, down, up);
    }

    private static async Task<(double? Mbps, int? LoadedPing)> Loaded(string phase, int httpPort,
        IProgress<(string, double)>? live, Func<IProgress<double>?, CancellationToken, Task<double?>> work, CancellationToken ct)
    {
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pings = new List<int>();
        var pinger = Task.Run(async () =>
        {
            using var client = Make(httpPort, TimeSpan.FromSeconds(6));
            await PingOnce(client, pingCts.Token);
            try { await Task.Delay(1400, pingCts.Token); } catch { return; }
            while (!pingCts.Token.IsCancellationRequested)
            {
                int? d = await PingOnce(client, pingCts.Token);
                if (d.HasValue) pings.Add(d.Value);
                try { await Task.Delay(140, pingCts.Token); } catch { break; }
            }
        }, pingCts.Token);

        var relay = new Progress<double>(mbps => live?.Report((phase, mbps)));
        double? result = await work(relay, ct);

        pingCts.Cancel();
        try { await pinger; } catch { }

        int? loaded = null;
        if (pings.Count > 0)
        {
            pings.Sort();
            int idx = (int)(pings.Count * 0.75);
            if (idx >= pings.Count) idx = pings.Count - 1;
            loaded = pings[idx];
        }
        return (result, loaded);
    }

    private static async Task<int?> PingOnce(HttpClient client, CancellationToken ct)
    {
        try
        {
            long start = Environment.TickCount64;
            using var resp = await client.GetAsync(DelayUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            _ = (int)resp.StatusCode;
            return (int)(Environment.TickCount64 - start);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<(int? Ping, int? Jitter)> PingJitter(int httpPort, CancellationToken ct)
    {
        using var client = Make(httpPort, TimeSpan.FromSeconds(6));
        await PingOnce(client, ct);
        var samples = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            int? d = await PingOnce(client, ct);
            if (d.HasValue) samples.Add(d.Value);
            try { await Task.Delay(120, ct); } catch { }
        }
        if (samples.Count == 0) return (null, null);
        int ping = samples.Min();
        if (samples.Count < 2) return (ping, 0);
        double sum = 0;
        for (int i = 1; i < samples.Count; i++) sum += Math.Abs(samples[i] - samples[i - 1]);
        return (ping, (int)Math.Round(sum / (samples.Count - 1)));
    }

    public static async Task<int?> Delay(int httpPort, CancellationToken ct)
    {
        try
        {
            using var client = Make(httpPort, TimeSpan.FromSeconds(6));
            long start = Environment.TickCount64;
            using var resp = await client.GetAsync(DelayUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            _ = (int)resp.StatusCode;
            return (int)(Environment.TickCount64 - start);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<double?> Download(int httpPort, IProgress<double>? live, CancellationToken ct)
    {
        long bytes = 0;
        Task Stream(HttpClient client, CancellationToken token) => Task.Run(async () =>
        {
            try
            {
                var buf = new byte[64 * 1024];
                while (!token.IsCancellationRequested)
                {
                    using var resp = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
                    resp.EnsureSuccessStatusCode();
                    await using var stream = await resp.Content.ReadAsStreamAsync(token);
                    int n;
                    while ((n = await stream.ReadAsync(buf, token)) > 0) Interlocked.Add(ref bytes, n);
                }
            }
            catch { }
        }, token);
        return await Aggregate(httpPort, 5, () => bytes, Stream, live, ct, 0.8);
    }

    public static async Task<double?> Upload(int httpPort, IProgress<double>? live, CancellationToken ct)
    {
        long bytes = 0;
        Task Stream(HttpClient client, CancellationToken token) => Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var content = new ProgressUpload(8 * 1024 * 1024, n => Interlocked.Add(ref bytes, n));
                    using (content)
                    using (var resp = await client.PostAsync(UploadUrl, content, token))
                        resp.EnsureSuccessStatusCode();
                }
            }
            catch { }
        }, token);
        return await Aggregate(httpPort, 3, () => bytes, Stream, live, ct, 0.75);
    }

    private static async Task<double?> Aggregate(int httpPort, int streams, Func<long> counter,
        Func<HttpClient, CancellationToken, Task> stream, IProgress<double>? live, CancellationToken ct, double pct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var clients = new List<HttpClient>();
        try
        {
            var workers = new List<Task>();
            for (int i = 0; i < streams; i++)
            {
                var c = Make(httpPort, Timeout.InfiniteTimeSpan);
                clients.Add(c);
                workers.Add(stream(c, cts.Token));
            }

            var rates = new List<double>();
            long last = counter();
            long lastTick = Environment.TickCount64;
            long start = lastTick;
            const long maxDuration = 11000;
            const long sliceMs = 500;

            while (Environment.TickCount64 - start < maxDuration && !cts.Token.IsCancellationRequested)
            {
                try { await Task.Delay((int)sliceMs, cts.Token); } catch { break; }
                long now = Environment.TickCount64;
                long cur = counter();
                double elapsed = now - lastTick;
                if (elapsed <= 0) continue;
                double bytesPerSec = (cur - last) * 1000.0 / elapsed;
                double mbps = bytesPerSec * 8.0 / 1_000_000.0;
                if (now - start > 2500) rates.Add(mbps);
                live?.Report(mbps);
                last = cur;
                lastTick = now;
            }

            cts.Cancel();
            try { await Task.WhenAll(workers); } catch { }

            if (rates.Count == 0) return null;
            rates.Sort();
            int idx = (int)(rates.Count * pct);
            if (idx < 0) idx = 0;
            if (idx >= rates.Count) idx = rates.Count - 1;
            return rates[idx];
        }
        finally
        {
            foreach (var c in clients) c.Dispose();
        }
    }

    private sealed class ProgressUpload : HttpContent
    {
        private readonly long _total;
        private readonly Action<int> _wrote;

        public ProgressUpload(long total, Action<int> wrote)
        {
            _total = total;
            _wrote = wrote;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buf = new byte[64 * 1024];
            new Random().NextBytes(buf);
            long sent = 0;
            while (sent < _total)
            {
                int n = (int)Math.Min(buf.Length, _total - sent);
                await stream.WriteAsync(buf.AsMemory(0, n));
                await stream.FlushAsync();
                sent += n;
                _wrote(n);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _total;
            return true;
        }
    }

    public static int Score(QualityResult r)
    {
        int s = 0;
        double down = r.DownMbps ?? 0;
        double up = r.UpMbps ?? 0;
        int ping = r.IdlePing ?? 9999;
        int jitter = r.Jitter ?? 9999;

        if (down >= 50) s += 40; else if (down >= 20) s += 30; else if (down >= 8) s += 20; else if (down >= 2) s += 10;
        if (up >= 15) s += 20; else if (up >= 6) s += 15; else if (up >= 2) s += 10; else if (up > 0) s += 5;
        if (ping <= 90) s += 25; else if (ping <= 180) s += 18; else if (ping <= 350) s += 10; else if (ping <= 600) s += 4;
        if (jitter <= 20) s += 15; else if (jitter <= 50) s += 10; else if (jitter <= 100) s += 5;
        return s;
    }
}