using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GRoute.Windows.Core;

public sealed class TorController
{
    private static bool Alive(Process? p)
    {
        if (p is null) return false;
        try { return !p.HasExited; }
        catch { return false; }
    }

    public const int SocksPort = 9150;
    public const int ControlPort = 9151;

    private static readonly string TorPath =
        Path.Combine(AppContext.BaseDirectory, "libs", "tor.exe");

    private static readonly string GeoIp =
        Path.Combine(AppContext.BaseDirectory, "libs", "geoip");

    private static readonly string GeoIp6 =
        Path.Combine(AppContext.BaseDirectory, "libs", "geoip6");

    public static bool GeoAvailable => File.Exists(GeoIp) && File.Exists(GeoIp6);

    private static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute", "tor");

    public event Action<string>? Log;

    private Process? _proc;

    public static bool Available => File.Exists(TorPath);

    public static string BinaryPath => TorPath;

    public bool IsRunning => Alive(_proc);

    public bool Start(bool throughVpn, string? exitCountry = null)
    {
        if (IsRunning) return true;
        if (!Available)
        {
            Log?.Invoke($"Tor needs this file (not found): {TorPath}");
            return false;
        }

        try
        {
            Directory.CreateDirectory(DataDir);
            KillStray();
            WaitPortFree(SocksPort, 3000);

            string torrc = Path.Combine(DataDir, "torrc");
            var sb = new StringBuilder();
            sb.AppendLine($"SocksPort 127.0.0.1:{SocksPort}");
            sb.AppendLine($"ControlPort 127.0.0.1:{ControlPort}");
            sb.AppendLine($"DataDirectory {DataDir}");
            sb.AppendLine("AvoidDiskWrites 1");
            sb.AppendLine("ClientOnly 1");
            string cc = (exitCountry ?? "").Trim().ToLowerInvariant();
            if (cc.Length == 2 && GeoAvailable)
            {
                sb.AppendLine($"GeoIPFile {GeoIp}");
                sb.AppendLine($"GeoIPv6File {GeoIp6}");
                sb.AppendLine($"ExitNodes {{{cc}}}");
                sb.AppendLine("StrictNodes 1");
            }
            else if (cc.Length == 2)
            {
                Log?.Invoke("Tor: no geoip files in libs \u2014 exit country ignored.");
            }
            if (throughVpn)
                sb.AppendLine($"Socks5Proxy 127.0.0.1:{ConfigBuilder.SocksPort}");
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

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 }) Log?.Invoke("[tor] " + e.Data); };
            _proc.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 }) Log?.Invoke("[tor] " + e.Data); };
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            Log?.Invoke(throughVpn
                ? $"Tor starting on 127.0.0.1:{SocksPort} (through the tunnel)."
                : $"Tor starting on 127.0.0.1:{SocksPort}.");
            return true;
        }
        catch (Exception ex)
        {
            Log?.Invoke("Tor: " + ex.Message);
            Stop();
            return false;
        }
    }

    public bool WaitReady(int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (!Alive(_proc)) return false;
            if (PortOpen(SocksPort)) return true;
            Thread.Sleep(200);
        }
        return false;
    }

    public void Stop()
    {
        var p = _proc;
        _proc = null;
        if (p is null) return;
        try
        {
            if (Alive(p))
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(3000);
            }
        }
        catch
        {
        }
        finally
        {
            try { p.Dispose(); } catch { }
        }
        Log?.Invoke("Tor stopped.");
    }

    private static void KillStray()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("tor"))
            {
                try
                {
                    string? path = p.MainModule?.FileName;
                    if (path is not null && string.Equals(path, TorPath, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(2000);
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
        }
        catch
        {
        }
    }

    private static void WaitPortFree(int port, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs && PortOpen(port))
            Thread.Sleep(150);
    }

    private static bool PortOpen(int port)
    {
        try
        {
            using var c = new TcpClient();
            var ar = c.BeginConnect("127.0.0.1", port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(250)) return false;
            c.EndConnect(ar);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
