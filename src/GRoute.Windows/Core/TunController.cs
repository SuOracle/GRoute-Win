using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public sealed class TunController
{
    private const string AdapterName = "GRoute";
    private const string TunAddress = "10.10.0.2";
    private const string TunMask = "255.255.255.0";
    private const string TunGateway = "10.10.0.1";
    private const string TunDns = "1.1.1.1";

    private static readonly string Tun2socksPath =
        Path.Combine(AppContext.BaseDirectory, "libs", "tun2socks.exe");
    private static readonly string WintunPath =
        Path.Combine(AppContext.BaseDirectory, "libs", "wintun.dll");

    public event Action<string>? Log;

    private Process? _proc;
    private string? _serverIp;
    private string? _gateway;
    private bool _routesAdded;

    public static bool BinariesPresent => File.Exists(Tun2socksPath) && File.Exists(WintunPath);

    public static string? MissingBinary()
    {
        if (!File.Exists(Tun2socksPath)) return Tun2socksPath;
        if (!File.Exists(WintunPath)) return WintunPath;
        return null;
    }

    public async Task<bool> Start(ProxyConfig config)
    {
        var missing = MissingBinary();
        if (missing is not null)
        {
            Log?.Invoke($"TUN needs this file (not found): {missing}");
            return false;
        }

        _serverIp = await ResolveIp(config.Address);
        if (_serverIp is null)
        {
            Log?.Invoke($"Couldn't resolve server address {config.Address}.");
            return false;
        }

        _gateway = GetDefaultGateway();
        if (_gateway is null)
        {
            Log?.Invoke("Couldn't determine the default gateway.");
            return false;
        }
        Log?.Invoke($"Server {_serverIp} will bypass the tunnel via {_gateway}.");

        StartTun2socks();

        if (!await WaitForAdapter(6000))
        {
            var names = string.Join(", ",
                NetworkInterface.GetAllNetworkInterfaces().Select(n => n.Name));
            Log?.Invoke("TUN adapter did not come up. Adapters: " + names);
            Stop();
            return false;
        }

        ConfigureAdapter();
        AddRoutes();
        Log?.Invoke("TUN mode active — all traffic routed through the tunnel.");
        return true;
    }

    public void Stop()
    {
        if (_routesAdded)
        {
            RemoveRoutes();
            _routesAdded = false;
        }
        try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); } catch { }
        _proc?.Dispose();
        _proc = null;
    }

    private void StartTun2socks()
    {
        var psi = new ProcessStartInfo
        {
            FileName = Tun2socksPath,
            Arguments = $"--device tun://{AdapterName} --proxy socks5://127.0.0.1:{ConfigBuilder.SocksPort}",
            WorkingDirectory = Path.GetDirectoryName(Tun2socksPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _proc = Process.Start(psi);
        if (_proc is not null)
        {
            _proc.OutputDataReceived += (_, e) => { if (e.Data is not null) Log?.Invoke("[tun2socks] " + e.Data); };
            _proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) Log?.Invoke("[tun2socks] " + e.Data); };
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
        }
    }

    private async Task<bool> WaitForAdapter(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (NetworkInterface.GetAllNetworkInterfaces()
                    .Any(n => n.Name.Equals(AdapterName, StringComparison.OrdinalIgnoreCase)))
                return true;
            await Task.Delay(200);
        }
        return false;
    }

    private void ConfigureAdapter()
    {
        Run("netsh", $"interface ip set address name=\"{AdapterName}\" static {TunAddress} {TunMask}");
        Run("netsh", $"interface ip set dns name=\"{AdapterName}\" static {TunDns}");
    }

    private void AddRoutes()
    {
        Run("route", $"add {_serverIp} mask 255.255.255.255 {_gateway} metric 1");
        Run("route", $"add 0.0.0.0 mask 128.0.0.0 {TunGateway} metric 5");
        Run("route", $"add 128.0.0.0 mask 128.0.0.0 {TunGateway} metric 5");
        _routesAdded = true;
    }

    private void RemoveRoutes()
    {
        Run("route", "delete 0.0.0.0 mask 128.0.0.0");
        Run("route", "delete 128.0.0.0 mask 128.0.0.0");
        if (_serverIp is not null) Run("route", $"delete {_serverIp}");
    }

    private async Task<string?> ResolveIp(string host)
    {
        try
        {
            if (IPAddress.TryParse(host, out var direct)) return direct.ToString();
            var addrs = await Dns.GetHostAddressesAsync(host);
            var v4 = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            return v4?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetDefaultGateway()
    {
        try
        {
            var output = Capture("route", "print -4");
            string? best = null;
            int bestMetric = int.MaxValue;
            foreach (var raw in output.Split('\n'))
            {
                var parts = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5 && parts[0] == "0.0.0.0" && parts[1] == "0.0.0.0"
                    && IPAddress.TryParse(parts[2], out _) && int.TryParse(parts[4], out var metric))
                {
                    if (metric < bestMetric)
                    {
                        bestMetric = metric;
                        best = parts[2];
                    }
                }
            }
            return best;
        }
        catch
        {
            return null;
        }
    }

    private void Run(string file, string args)
    {
        var output = Capture(file, args).Trim();
        if (output.Length > 0) Log?.Invoke($"[{file}] {output}");
    }

    private static string Capture(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p is null) return "";
            var o = p.StandardOutput.ReadToEnd();
            var e = p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            return o + e;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
