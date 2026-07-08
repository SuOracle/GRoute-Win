using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public static class DelayTester
{
    private static readonly string XrayPath =
        Path.Combine(AppContext.BaseDirectory, "libs", "xray.exe");

    private const string TestUrl = "https://www.gstatic.com/generate_204";

    public static bool XrayAvailable => File.Exists(XrayPath);

    public static async Task<int> Measure(ProxyConfig config, int timeoutMs = 5000)
    {
        if (!XrayAvailable) return -1;

        int port = FreePort();
        var json = ConfigBuilder.BuildForTest(config, port);
        var configPath = Path.Combine(Path.GetTempPath(), $"groute-test-{Guid.NewGuid():N}.json");

        Process? proc = null;
        try
        {
            await File.WriteAllTextAsync(configPath, json, new UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = XrayPath,
                Arguments = $"run -c \"{configPath}\"",
                WorkingDirectory = Path.GetDirectoryName(XrayPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            proc = Process.Start(psi);
            if (proc is null) return -1;
            _ = proc.StandardOutput.ReadToEndAsync();
            _ = proc.StandardError.ReadToEndAsync();

            if (!await WaitReady(port, 3000)) return -1;

            using var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://127.0.0.1:{port}"),
                UseProxy = true
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            http.DefaultRequestHeaders.Add("User-Agent", "GRoute");

            try
            {
                using var warm = await http.GetAsync(TestUrl);
            }
            catch
            {
                return -1;
            }

            var sw = Stopwatch.StartNew();
            using var resp = await http.GetAsync(TestUrl);
            sw.Stop();
            return (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
        finally
        {
            try { if (proc is { HasExited: false }) proc.Kill(entireProcessTree: true); } catch { }
            proc?.Dispose();
            try { File.Delete(configPath); } catch { }
        }
    }

    private static async Task<bool> WaitReady(int port, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var c = new TcpClient();
                using var cts = new CancellationTokenSource(500);
                await c.ConnectAsync("127.0.0.1", port, cts.Token);
                return true;
            }
            catch
            {
                await Task.Delay(50);
            }
        }
        return false;
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
