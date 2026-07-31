using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public sealed class PsiphonController
{
    private static bool Alive(Process? p)
    {
        if (p is null) return false;
        try { return !p.HasExited; }
        catch { return false; }
    }

    public const int SocksPort = 1080;
    public const int HttpPort = 1081;

    private static readonly string[] BinaryNames = Bin.IsWindows
        ? new[] { "psiphon-tunnel-core.exe", "psiphon-tunnel-core-windows-x86_64.exe", "psiphon.exe" }
        : new[] { "psiphon-tunnel-core", "psiphon-tunnel-core-x86_64", "psiphon" };

    private static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute", "psiphon");

    public event Action<string>? Log;

    public event Action<int>? TunnelCount;

    private Process? _proc;
    private volatile int _tunnels;

    public static string? BinaryPath
    {
        get
        {
            foreach (var name in BinaryNames)
            {
                string p = Path.Combine(AppContext.BaseDirectory, "libs", name);
                if (File.Exists(p)) return p;
            }
            return null;
        }
    }

    public static bool Available => BinaryPath is not null;

    public static string ExpectedPath =>
        Path.Combine(AppContext.BaseDirectory, "libs", BinaryNames[0]);

    public bool IsRunning => Alive(_proc);

    public int Tunnels => _tunnels;

    public async Task<bool> Start(string? egressRegion, int readyTimeoutMs, CancellationToken ct)
    {
        string? exe = BinaryPath;
        if (exe is null)
        {
            Log?.Invoke($"Psiphon needs this file (not found): {ExpectedPath}");
            return false;
        }

        Stop();
        _tunnels = 0;

        bool spawned = await Task.Run(() => Spawn(exe, egressRegion), ct);
        if (!spawned) return false;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < readyTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();
            if (!Alive(_proc))
            {
                Log?.Invoke("Psiphon exited before a tunnel came up.");
                return false;
            }
            bool ready = await Task.Run(() => _tunnels > 0 && PortOpen(SocksPort), ct);
            if (ready)
            {
                Log?.Invoke($"Psiphon tunnel up on 127.0.0.1:{SocksPort}.");
                return true;
            }
            await Task.Delay(400, ct);
        }

        Log?.Invoke("Psiphon did not establish a tunnel in time.");
        return false;
    }

    private bool Spawn(string exe, string? egressRegion)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            WaitPortFree(SocksPort, 800);

            var cfg = new JsonObject
            {
                ["PropagationChannelId"] = "FFFFFFFFFFFFFFFF",
                ["SponsorId"] = "FFFFFFFFFFFFFFFF",
                ["LocalSocksProxyPort"] = SocksPort,
                ["LocalHttpProxyPort"] = HttpPort,
                ["DataRootDirectory"] = DataDir,
                ["UseIndistinguishableTLS"] = true,
                ["EmitDiagnosticNotices"] = true
            };

            string region = (egressRegion ?? "").Trim().ToUpperInvariant();
            if (region.Length == 2)
            {
                cfg["EgressRegion"] = region;
                Log?.Invoke($"Psiphon exit region {region}.");
            }

            string userCfg = Path.Combine(DataDir, "psiphon.config.user");
            if (File.Exists(userCfg))
            {
                Log?.Invoke($"Psiphon using your config: {userCfg}");
                return SpawnWith(exe, userCfg);
            }

            string cfgPath = Path.Combine(DataDir, "psiphon.config");
            File.WriteAllText(cfgPath, cfg.ToJsonString(), new UTF8Encoding(false));

            return SpawnWith(exe, cfgPath);
        }
        catch (Exception ex)
        {
            Log?.Invoke("Psiphon: " + ex.Message);
            Stop();
            return false;
        }
    }

    private bool SpawnWith(string exe, string cfgPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--config \"{cfgPath}\"",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => Notice(e.Data);
            _proc.ErrorDataReceived += (_, e) => Notice(e.Data);
            _proc.Exited += (_, _) => Log?.Invoke("[psiphon] process exited.");
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            Log?.Invoke("Psiphon starting \u2026");
            return true;
        }
        catch (Exception ex)
        {
            Log?.Invoke("Psiphon: " + ex.Message);
            Stop();
            return false;
        }
    }

    private void Notice(string? line)
    {
        if (line is not { Length: > 0 }) return;

        string type = "";
        try
        {
            if (JsonNode.Parse(line) is JsonObject o)
            {
                type = o["noticeType"]?.GetValue<string>() ?? "";
                if (type == "Tunnels")
                {
                    int count = o["data"]?["count"]?.GetValue<int>() ?? 0;
                    _tunnels = count;
                    TunnelCount?.Invoke(count);
                }
            }
        }
        catch
        {
        }

        switch (type)
        {
            case "BytesTransferred":
            case "ClientVerificationRequired":
            case "Homepage":
                return;
        }
        Log?.Invoke("[psiphon] " + line);
    }

    public void Stop()
    {
        var p = _proc;
        _proc = null;
        _tunnels = 0;
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
        Log?.Invoke("Psiphon stopped.");
    }

    public Task StopAsync()
    {
        var p = _proc;
        _proc = null;
        _tunnels = 0;
        if (p is null) return Task.CompletedTask;
        return Task.Run(() =>
        {
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
            Log?.Invoke("Psiphon stopped.");
        });
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
