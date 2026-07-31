using System.Diagnostics;
using System.IO;
using System.Text;

namespace GRoute.Windows.Core;

public sealed class XrayController
{
    private static bool Alive(Process? p)
    {
        if (p is null) return false;
        try { return !p.HasExited; }
        catch { return false; }
    }

    private readonly string _xrayPath;
    private Process? _process;
    private string? _configPath;
    private bool _stopping;

    public event Action<string>? LogReceived;
    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public XrayController()
    {
        _xrayPath = Bin.Path("xray");
    }

    public bool IsRunning => Alive(_process);

    public void Start(string configJson)
    {
        if (IsRunning) return;
        if (!File.Exists(_xrayPath))
            throw new FileNotFoundException(
                $"xray not found. Expected it here:\n\n{_xrayPath}", _xrayPath);

        KillStray();
        WaitPortFree(ConfigBuilder.SocksPort, 1500);
        WaitPortFree(ConfigBuilder.HttpPort, 1500);

        _stopping = false;
        SetState(ConnectionState.Connecting);

        _configPath = Path.Combine(Path.GetTempPath(), "groute-config.json");
        File.WriteAllText(_configPath, configJson, new UTF8Encoding(false));

        var libsDir = Path.GetDirectoryName(_xrayPath)!;
        Assets.EnsureSeeded();

        var psi = new ProcessStartInfo
        {
            FileName = _xrayPath,
            Arguments = $"run -c \"{_configPath}\"",
            WorkingDirectory = libsDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["XRAY_LOCATION_ASSET"] = Assets.AssetDir;

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) LogReceived?.Invoke(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) LogReceived?.Invoke(e.Data); };
        _process.Exited += (_, _) =>
        {
            if (!_stopping) SetState(ConnectionState.Error);
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        SetState(ConnectionState.Connected);
    }

    public void Stop()
    {
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
            SetState(ConnectionState.Disconnected);
        }
    }

    private static readonly string[] CoreNames = { "xray", "sing-box" };

    private bool KillStray()
    {
        bool killed = false;
        foreach (var name in CoreNames)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch { continue; }
            foreach (var p in procs)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(2000);
                    killed = true;
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
        return killed;
    }

    private static void WaitPortFree(int port, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            try
            {
                var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                l.Start();
                l.Stop();
                return;
            }
            catch
            {
                System.Threading.Thread.Sleep(60);
            }
        }
    }

    private void SetState(ConnectionState s)
    {
        State = s;
        StateChanged?.Invoke(s);
    }
}
