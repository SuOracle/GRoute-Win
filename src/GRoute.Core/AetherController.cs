using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace GRoute.Windows.Core;

public sealed class AetherController
{
    private static bool Alive(Process? p)
    {
        if (p is null) return false;
        try { return !p.HasExited; }
        catch { return false; }
    }

    public const int SocksPort = 1819;
    public const int ReadyTimeoutMs = 180000;

    private readonly string _path;
    private Process? _process;
    private bool _stopping;

    public event Action<string>? LogReceived;

    public AetherController()
    {
        _path = Bin.Path("aether");
    }

    public string BinaryPath => _path;

    public bool Available => File.Exists(_path);

    public string WorkDir => Path.GetDirectoryName(_path)!;

    public string IdentityPath => Path.Combine(WorkDir, "aether-masque.toml");

    public bool HasIdentity => File.Exists(IdentityPath);

    public bool IsRunning => Alive(_process);

    public static string Args(ProxyConfig config)
    {
        var sb = new StringBuilder();
        sb.Append($"--bind 127.0.0.1:{SocksPort}");

        string mode = string.IsNullOrWhiteSpace(config.AetherMode) ? "masque" : config.AetherMode;
        sb.Append(mode switch
        {
            "wg" => " --wg",
            "gool" => " --gool",
            _ => " --masque"
        });

        if (config.AetherHttp2)
        {
            sb.Append(" --h2");
            if (config.AetherFragment) sb.Append(" --fragment");
        }

        string scan = string.IsNullOrWhiteSpace(config.AetherScan) ? "balanced" : config.AetherScan;
        sb.Append($" --scan {scan}");

        if (!string.IsNullOrWhiteSpace(config.AetherNoise))
            sb.Append($" --noize {config.AetherNoise}");

        sb.Append(" --quick-reconnect -4");
        return sb.ToString();
    }

    private static void ApplyEnv(System.Collections.Generic.IDictionary<string, string?> env, ProxyConfig config)
    {
        string mode = string.IsNullOrWhiteSpace(config.AetherMode) ? "masque" : config.AetherMode;
        string scan = string.IsNullOrWhiteSpace(config.AetherScan) ? "balanced" : config.AetherScan;

        env["AETHER_SOCKS"] = $"127.0.0.1:{SocksPort}";
        env["AETHER_PROTOCOL"] = mode;
        env["AETHER_SCAN"] = scan;
        env["AETHER_QUICK_RECONNECT"] = "1";

        if (config.AetherHttp2) env["AETHER_MASQUE_HTTP2"] = "1";
        if (!string.IsNullOrWhiteSpace(config.AetherNoise)) env["AETHER_NOIZE"] = config.AetherNoise;
    }

    public void Start(ProxyConfig config)
    {
        if (IsRunning) return;
        if (!File.Exists(_path))
            throw new FileNotFoundException(
                $"aether not found. Expected it here:\n\n{_path}", _path);

        KillStray();
        _stopping = false;

        LogReceived?.Invoke($"aether: working directory {WorkDir}");
        if (!HasIdentity)
        {
            LogReceived?.Invoke(
                "aether: no aether-masque.toml here, so it must register with Cloudflare first. " +
                "If registration is blocked on this network, copy an existing aether-masque.toml into " +
                WorkDir + " and reconnect.");
        }

        string args = Args(config);
        LogReceived?.Invoke($"aether {args}");

        var wrap = Platform.BypassSpawn?.Invoke(_path);
        string spawnFile = wrap?.File ?? _path;
        string spawnArgs = wrap is { Prefix.Length: > 0 }
            ? string.Join(' ', wrap.Value.Prefix) + " " + args
            : args;

        var psi = new ProcessStartInfo
        {
            FileName = spawnFile,
            Arguments = spawnArgs,
            WorkingDirectory = Path.GetDirectoryName(_path)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            RedirectStandardInput = true
        };

        ApplyEnv(psi.Environment, config);

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => Emit(e.Data);
        _process.ErrorDataReceived += (_, e) => Emit(e.Data);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        try
        {
            _process.StandardInput.Close();
        }
        catch
        {
        }
    }

    public bool WaitReady(int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (_stopping) return false;
            if (_process is { } p && !Alive(p))
            {
                LogReceived?.Invoke($"aether exited early with code {p.ExitCode}.");
                return false;
            }

            try
            {
                using var c = new TcpClient();
                var task = c.ConnectAsync("127.0.0.1", SocksPort);
                if (task.Wait(700) && c.Connected) return true;
            }
            catch
            {
            }

            System.Threading.Thread.Sleep(400);
        }
        return false;
    }

    private static readonly string[] Noise =
    {
        "tls verification: pin-based",
        "tls verification: system",
        "probing gateway",
        "handshake ok",
    };

    private string _lastLine = "";
    private int _repeat;

    private void Emit(string? line)
    {
        if (line is not { Length: > 0 }) return;

        foreach (var n in Noise)
            if (line.Contains(n, StringComparison.Ordinal)) return;

        if (line == _lastLine)
        {
            _repeat++;
            if (_repeat == 5) LogReceived?.Invoke("  (repeating, further copies hidden)");
            if (_repeat >= 5) return;
        }
        else
        {
            _lastLine = line;
            _repeat = 0;
        }

        LogReceived?.Invoke(line);
    }

    public void Stop()
    {
        if (_process is null) return;

        _stopping = true;
        try
        {
            if (Alive(_process))
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    private void KillStray()
    {
        Process[] procs;
        try { procs = Process.GetProcessesByName("aether"); }
        catch { return; }
        foreach (var p in procs)
        {
            try
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(2000);
            }
            catch
            {
            }
            finally
            {
                p.Dispose();
            }
        }
    }
}
