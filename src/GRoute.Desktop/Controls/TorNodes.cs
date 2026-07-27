using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public sealed class TorNode
{
    public string Country = "";
    public string Label = "";
    public int Port;
}

public sealed class TorNodes
{
    public const int BasePort = 9060;

    private static readonly string TorPath =
        Path.Combine(AppContext.BaseDirectory, "libs", "tor.exe");

    private static string Locate(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int up = 0; up < 6 && dir is not null; up++, dir = dir.Parent)
        {
            string a = Path.Combine(dir.FullName, "libs", file);
            if (File.Exists(a)) return a;
            string b = Path.Combine(dir.FullName, file);
            if (File.Exists(b)) return b;
        }
        return Path.Combine(AppContext.BaseDirectory, "libs", file);
    }

    private static readonly string GeoIp = Locate("geoip");

    private static readonly string GeoIp6 = Locate("geoip6");

    public static bool GeoAvailable => File.Exists(GeoIp) && File.Exists(GeoIp6);

    public static string GeoIpExpected => GeoIp;

    private static readonly string RootDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute", "tornodes");

    public event Action<string>? Log;

    public event Action<string, int>? Bootstrap;

    private readonly Dictionary<int, int> _progress = new();

    public int ProgressFor(int port)
    {
        lock (_progress) return _progress.TryGetValue(port, out var v) ? v : 0;
    }

    private readonly List<Process> _procs = new();

    public static bool Available => File.Exists(TorPath);

    public static string BinaryPath => TorPath;

    public bool IsRunning => _procs.Count > 0;

    public async Task<List<TorNode>> Start(IReadOnlyList<(string Country, string Label)> wanted,
                                           int readyTimeoutMs, CancellationToken ct)
    {
        Stop();
        var live = new List<TorNode>();
        if (!Available)
        {
            Log?.Invoke($"Tor needs this file (not found): {TorPath}");
            return live;
        }

        var started = await Task.Run(() =>
        {
            var list = new List<TorNode>();
            for (int i = 0; i < wanted.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var w = wanted[i];
                int port = BasePort + i;
                if (!Spawn(w.Country, port)) continue;
                list.Add(new TorNode { Country = w.Country, Label = w.Label, Port = port });
                Log?.Invoke($"Tor [{w.Label}] starting on 127.0.0.1:{port}");
            }
            return list;
        }, ct);

        var sw = Stopwatch.StartNew();
        var pending = new List<TorNode>(started);
        while (pending.Count > 0 && sw.ElapsedMilliseconds < readyTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = new List<TorNode>(pending);
            var ready = await Task.Run(() =>
            {
                var done = new List<TorNode>();
                foreach (var n in snapshot)
                    if (ProgressFor(n.Port) >= 100 && PortOpen(n.Port)) done.Add(n);
                return done;
            }, ct);

            foreach (var n in ready)
            {
                Log?.Invoke($"Tor [{n.Label}] circuit ready.");
                live.Add(n);
                pending.Remove(n);
            }
            if (pending.Count == 0) break;
            await Task.Delay(400, ct);
        }

        foreach (var p in pending)
            Log?.Invoke($"Tor [{p.Label}] did not come up in time \u2014 skipped.");

        return live;
    }

    private bool Spawn(string country, int port)
    {
        try
        {
            string dir = Path.Combine(RootDir, country);
            Directory.CreateDirectory(dir);
            WaitPortFree(port, 600);

            string torrc = Path.Combine(dir, "torrc");
            var sb = new StringBuilder();
            sb.AppendLine($"SocksPort 127.0.0.1:{port}");
            sb.AppendLine($"DataDirectory {dir}");
            sb.AppendLine("AvoidDiskWrites 1");
            sb.AppendLine("Log notice stdout");
            sb.AppendLine("ClientOnly 1");
            if (GeoAvailable)
            {
                sb.AppendLine($"GeoIPFile {GeoIp}");
                sb.AppendLine($"GeoIPv6File {GeoIp6}");
                sb.AppendLine($"ExitNodes {{{country}}}");
                sb.AppendLine("StrictNodes 0");
            }
            else
            {
                Log?.Invoke($"Tor [{country.ToUpperInvariant()}] no geoip files in libs \u2014 exit country cannot be pinned.");
            }
            File.WriteAllText(torrc, sb.ToString());

            var psi = new ProcessStartInfo
            {
                FileName = TorPath,
                Arguments = $"-f \"{torrc}\"",
                WorkingDirectory = Path.GetDirectoryName(TorPath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            string tag = country.ToUpperInvariant();
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not { Length: > 0 }) return;
                int pct = ParseBootstrap(e.Data);
                if (pct >= 0)
                {
                    lock (_progress) _progress[port] = pct;
                    Bootstrap?.Invoke(country, pct);
                }
                Log?.Invoke($"[tor {tag}] {e.Data}");
            };
            proc.Exited += (_, _) => Log?.Invoke($"[tor {tag}] process exited.");
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is { Length: > 0 }) Log?.Invoke($"[tor {tag}] {e.Data}");
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            _procs.Add(proc);
            return true;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Tor [{country}]: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StartOne(string country, int port, int readyTimeoutMs, CancellationToken ct)
    {
        if (!Available)
        {
            Log?.Invoke($"Tor needs this file (not found): {TorPath}");
            return false;
        }
        bool spawned = await Task.Run(() =>
        {
            if (ProgressFor(port) >= 100 && PortOpen(port)) return true;
            return PortOpen(port) || Spawn(country, port);
        }, ct);
        if (!spawned) return false;

        Log?.Invoke($"Tor [{country.ToUpperInvariant()}] building circuit on 127.0.0.1:{port} \u2026");
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < readyTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();
            bool ok = await Task.Run(() => ProgressFor(port) >= 100 && PortOpen(port), ct);
            if (ok)
            {
                Log?.Invoke($"Tor [{country.ToUpperInvariant()}] circuit ready.");
                return true;
            }
            await Task.Delay(400, ct);
        }
        Log?.Invoke($"Tor [{country.ToUpperInvariant()}] did not finish bootstrapping in time.");
        return false;
    }

    public Task StopAsync()
    {
        var procs = new List<Process>(_procs);
        _procs.Clear();
        lock (_progress) _progress.Clear();
        if (procs.Count == 0) return Task.CompletedTask;
        return Task.Run(() => KillAll(procs));
    }

    public void Stop()
    {
        foreach (var p in _procs)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(2500);
                }
            }
            catch
            {
            }
            finally
            {
                try { p.Dispose(); } catch { }
            }
        }
        if (_procs.Count > 0) Log?.Invoke("Tor location nodes stopped.");
        _procs.Clear();
        lock (_progress) _progress.Clear();
    }

    private void KillAll(List<Process> procs)
    {
        foreach (var p in procs)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(2500);
                }
            }
            catch
            {
            }
            finally
            {
                try { p.Dispose(); } catch { }
            }
        }
        Log?.Invoke("Tor location nodes stopped.");
    }

    private static int ParseBootstrap(string line)
    {
        int i = line.IndexOf("Bootstrapped ", StringComparison.Ordinal);
        if (i < 0) return -1;
        i += 13;
        int j = i;
        while (j < line.Length && char.IsDigit(line[j])) j++;
        if (j == i) return -1;
        return int.TryParse(line.Substring(i, j - i), out var pct) ? pct : -1;
    }

    private static void WaitPortFree(int port, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs && PortOpen(port))
            Thread.Sleep(60);
    }

    private static bool PortOpen(int port)
    {
        try
        {
            using var c = new TcpClient();
            var ar = c.BeginConnect("127.0.0.1", port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(120)) return false;
            c.EndConnect(ar);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
