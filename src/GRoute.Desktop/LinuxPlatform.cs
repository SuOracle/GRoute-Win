using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GRoute.Windows.Core;

namespace GRoute.Desktop;

public static class LinuxPlatform
{

    public static void CleanupStaleState()
    {
        try
        {
            Run("ip", "route del 0.0.0.0/1");
            Run("ip", "route del 128.0.0.0/1");
            foreach (var r in new[] { "1.1.1.1", "1.0.0.1", "8.8.8.8", "8.8.4.4", "9.9.9.9" })
                Run("ip", $"route del {r}/32");
            RemoveBypassRoute();
            Run("ip", "link del groute0");
            Run("iptables", "-D OUTPUT -j GROUTE_KILL");
            Run("iptables", "-F GROUTE_KILL");
            Run("iptables", "-X GROUTE_KILL");
            Run("nft", "delete table inet groute");
            Run("pkill", "-f tun2socks");
        }
        catch
        {
        }
    }

    public static void Register()
    {
        CleanupStaleState();

        Platform.Proxy = new LinuxSystemProxy();
        Platform.Elevation = new LinuxElevation();
        Platform.KillSwitch = new LinuxKillSwitch();
        Platform.Startup = new LinuxStartupTask();
        Platform.CreateTun = () => new LinuxTun();
        Platform.BypassSpawn = BypassWrapper;
        Platform.AllowWrite = AllowBypassWrite;
    }

    internal static string Run(string file, string args, int timeoutMs = 8000)
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
            string outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit(timeoutMs);
            return outp;
        }
        catch
        {
            return "";
        }
    }

    internal static bool Has(string tool) => Run("/bin/sh", $"-c \"command -v {tool}\"").Trim().Length > 0;

    public const int BypassUid = 7391;
    public const int BypassTable = 391;

    public static (string File, string[] Prefix) BypassWrapper(string exe)
    {
        EnsureBypassUser();
        if (!Has("setpriv")) return (exe, Array.Empty<string>());

        string? dir = Path.GetDirectoryName(exe);
        if (dir is not null) AllowBypassWrite(dir);
        return ("setpriv", new[]
        {
            "--reuid", BypassUid.ToString(),
            "--regid", BypassUid.ToString(),
            "--clear-groups",
            exe
        });
    }

    private static void EnsureBypassUser()
    {
        if (Run("/bin/sh", $"-c \"id -u {BypassUid} 2>/dev/null\"").Trim().Length > 0) return;
        Run("useradd", $"--system --no-create-home --shell /usr/sbin/nologin --uid {BypassUid} groute-bypass");
    }

    internal static void AllowBypassWrite(string dir)
    {
        try
        {
            Run("chgrp", $"-R {BypassUid} \"{dir}\"");
            Run("chmod", $"-R g+rwX \"{dir}\"");
        }
        catch
        {
        }
    }

    internal static void AddBypassRoute(string gateway, string iface)
    {
        Run("ip", $"route replace default via {gateway} dev {iface} table {BypassTable}");
        Run("ip", $"rule del uidrange {BypassUid}-{BypassUid} lookup {BypassTable}");
        Run("ip", $"rule add uidrange {BypassUid}-{BypassUid} lookup {BypassTable} pref 100");
    }

    internal static void RemoveBypassRoute()
    {
        Run("ip", $"rule del uidrange {BypassUid}-{BypassUid} lookup {BypassTable}");
        Run("ip", $"route flush table {BypassTable}");
    }

    private sealed class LinuxSystemProxy : ISystemProxy
    {
        public void Enable(string proxyAddress)
        {
            var (host, port) = Split(proxyAddress);
            if (host.Length == 0) return;

            if (Has("gsettings"))
            {
                Run("gsettings", "set org.gnome.system.proxy mode 'manual'");
                foreach (var scheme in new[] { "http", "https" })
                {
                    Run("gsettings", $"set org.gnome.system.proxy.{scheme} host '{host}'");
                    Run("gsettings", $"set org.gnome.system.proxy.{scheme} port {port}");
                }
                Run("gsettings",
                    "set org.gnome.system.proxy ignore-hosts \"['localhost','127.0.0.0/8','::1']\"");
            }

            WriteEnvFile($"http://{host}:{port}");
        }

        public void Disable()
        {
            if (Has("gsettings")) Run("gsettings", "set org.gnome.system.proxy mode 'none'");
            WriteEnvFile(null);
        }

        private static void WriteEnvFile(string? url)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "proxy.env");
                if (url is null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("unset http_proxy https_proxy all_proxy");
                    sb.AppendLine("unset HTTP_PROXY HTTPS_PROXY ALL_PROXY");
                    File.WriteAllText(path, sb.ToString());
                }
                else
                {
                    var sb = new StringBuilder();
                    foreach (var v in new[] { "http_proxy", "https_proxy", "all_proxy" })
                        sb.AppendLine($"export {v}={url}");
                    foreach (var v in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" })
                        sb.AppendLine($"export {v}={url}");
                    sb.AppendLine("export no_proxy=localhost,127.0.0.1,::1");
                    File.WriteAllText(path, sb.ToString());
                }
            }
            catch
            {
            }
        }

        private static (string Host, int Port) Split(string addr)
        {
            addr = addr.Replace("http://", "").Trim();
            int i = addr.LastIndexOf(':');
            if (i <= 0) return (addr, 8080);
            return (addr[..i], int.TryParse(addr[(i + 1)..], out var p) ? p : 8080);
        }
    }

    private sealed class LinuxElevation : IElevation
    {
        public bool IsAdministrator()
        {
            try { return Run("/bin/sh", "-c \"id -u\"").Trim() == "0"; }
            catch { return false; }
        }

        public bool RelaunchAsAdmin(string? args = null)
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            string runner = Has("pkexec") ? "pkexec"
                          : Has("gksudo") ? "gksudo"
                          : Has("kdesu") ? "kdesu"
                          : "";
            if (runner.Length == 0) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = runner,
                    UseShellExecute = false
                };

                if (runner == "pkexec")
                {
                    psi.ArgumentList.Add("env");
                    foreach (var v in new[] { "DISPLAY", "WAYLAND_DISPLAY", "XAUTHORITY", "XDG_RUNTIME_DIR" })
                    {
                        string? val = Environment.GetEnvironmentVariable(v);
                        if (!string.IsNullOrEmpty(val)) psi.ArgumentList.Add($"{v}={val}");
                    }
                }
                psi.ArgumentList.Add(exe);
                if (!string.IsNullOrEmpty(args)) psi.ArgumentList.Add(args);
                return Process.Start(psi) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class LinuxKillSwitch : IKillSwitch
    {
        private const string Chain = "GROUTE_KILL";

        public bool Engaged { get; private set; }

        private static bool UseNft => Has("nft");

        public void Engage()
        {
            if (Engaged) return;
            Release();

            if (UseNft)
            {
                Run("nft", "add table inet groute");
                Run("nft", "add chain inet groute out { type filter hook output priority 0 ; policy drop ; }");
                Run("nft", "add rule inet groute out oifname lo accept");
                Run("nft", "add rule inet groute out ct state established,related accept");
                Run("nft", "add rule inet groute out meta skuid 0 accept");
                Run("nft", "add rule inet groute out udp dport 67 accept");
            }
            else
            {
                Run("iptables", $"-N {Chain}");
                Run("iptables", $"-F {Chain}");
                Run("iptables", $"-A {Chain} -o lo -j ACCEPT");
                Run("iptables", $"-A {Chain} -m state --state ESTABLISHED,RELATED -j ACCEPT");
                Run("iptables", $"-A {Chain} -p udp --dport 67 -j ACCEPT");
                Run("iptables", $"-A {Chain} -j REJECT");
                Run("iptables", $"-I OUTPUT -j {Chain}");
            }
            Engaged = true;
        }

        public void Release()
        {
            if (UseNft)
            {
                Run("nft", "delete table inet groute");
            }
            else
            {
                Run("iptables", $"-D OUTPUT -j {Chain}");
                Run("iptables", $"-F {Chain}");
                Run("iptables", $"-X {Chain}");
            }
            Engaged = false;
        }
    }

    private sealed class LinuxStartupTask : IStartupTask
    {
        private static string DesktopFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart", "groute.desktop");

        public bool Supported => true;

        public bool Enabled => File.Exists(DesktopFile);

        public void Set(bool enabled)
        {
            try
            {
                if (!enabled)
                {
                    if (File.Exists(DesktopFile)) File.Delete(DesktopFile);
                    return;
                }

                string? exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);

                var sb = new StringBuilder();
                sb.AppendLine("[Desktop Entry]");
                sb.AppendLine("Type=Application");
                sb.AppendLine("Name=GRoute");
                sb.AppendLine($"Exec=\"{exe}\" --autostart");
                sb.AppendLine("Terminal=false");
                sb.AppendLine("X-GNOME-Autostart-enabled=true");
                File.WriteAllText(DesktopFile, sb.ToString());
            }
            catch
            {
            }
        }
    }

    private sealed class LinuxTun : ITunService
    {
        private const string Dev = "groute0";
        private const string Addr = "10.10.10.1";
        private const string Peer = "10.10.10.2";

        private static readonly string[] DohResolvers =
            { "1.1.1.1", "1.0.0.1", "8.8.8.8", "8.8.4.4", "9.9.9.9" };

        private static string Tun2socks =>
            Path.Combine(AppContext.BaseDirectory, "libs", "tun2socks");

        private Process? _proc;
        private string? _serverIp;
        private string? _gateway;
        private bool _routed;

        public event Action<string>? Log;

        public bool BinariesPresent => File.Exists(Tun2socks);

        public string? MissingBinary() => BinariesPresent ? null : Tun2socks;

        public async Task<bool> Start(ProxyConfig config)
        {
            if (!BinariesPresent)
            {
                Log?.Invoke($"tun2socks not found: {Tun2socks}");
                return false;
            }

            _serverIp = await ResolveIp(config.Address);
            if (_serverIp is null)
            {
                Log?.Invoke($"could not resolve {config.Address}");
                return false;
            }

            await Task.Run(() =>
            {
                _gateway = DefaultGateway();
                Run("ip", $"link del {Dev}");
                Run("ip", $"tuntap add dev {Dev} mode tun");
                Run("ip", $"addr replace {Addr}/24 dev {Dev}");
                Run("ip", $"link set dev {Dev} mtu 1500");
                Run("ip", $"link set dev {Dev} up");
            });

            if (_gateway is null)
            {
                Log?.Invoke("no default gateway found");
                Stop();
                return false;
            }

            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Tun2socks,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                psi.ArgumentList.Add("--device");
                psi.ArgumentList.Add(Dev);
                psi.ArgumentList.Add("--proxy");
                psi.ArgumentList.Add($"socks5://127.0.0.1:{ConfigBuilder.SocksPort}");
                psi.ArgumentList.Add("--loglevel");
                psi.ArgumentList.Add("info");
                _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _proc.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 }) Log?.Invoke("[tun2socks] " + e.Data); };
                _proc.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 }) Log?.Invoke("[tun2socks] " + e.Data); };
                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
            });

            await Task.Delay(600);

            string link = "";
            for (int i = 0; i < 20; i++)
            {
                link = await Task.Run(() => Run("ip", $"link show {Dev}").Trim());
                if (link.Length > 0 && !link.Contains("NO-CARRIER", StringComparison.Ordinal)) break;
                await Task.Delay(250);
            }
            Log?.Invoke($"[{Dev}] {(link.Length > 0 ? link.Split('\n')[0] : "gone")}");
            if (link.Contains("NO-CARRIER", StringComparison.Ordinal))
            {
                Log?.Invoke($"{Dev} has no carrier \u2014 tun2socks is not attached. Not touching routes.");
                Stop();
                return false;
            }

            await Task.Run(() =>
            {
                AddBypassRoute(_gateway!, DefaultIface());
                Run("ip", $"route replace {_serverIp}/32 via {_gateway}");

                foreach (var r in DohResolvers)
                    Run("ip", $"route replace {r}/32 via {_gateway}");
                Run("ip", $"route replace 0.0.0.0/1 dev {Dev}");
                Run("ip", $"route replace 128.0.0.0/1 dev {Dev}");
                _routed = true;
            });

            Log?.Invoke($"TUN active on {Dev}, all traffic routed through the tunnel.");
            return true;
        }

        public void Stop()
        {
            if (_routed)
            {
                Run("ip", "route del 0.0.0.0/1");
                Run("ip", "route del 128.0.0.0/1");
                if (_serverIp is not null) Run("ip", $"route del {_serverIp}/32");
                foreach (var r in DohResolvers) Run("ip", $"route del {r}/32");
                _routed = false;
            }

            var p = _proc;
            _proc = null;
            if (p is not null)
            {
                try
                {
                    if (!p.HasExited)
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
            }

            RemoveBypassRoute();
            Run("ip", $"link set dev {Dev} down");
            Run("ip", $"tuntap del dev {Dev} mode tun");
        }

        private static string DefaultIface()
        {
            string outp = Run("ip", "route show default");
            var parts = outp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i] == "dev") return parts[i + 1];
            return "eth0";
        }

        private static string? DefaultGateway()
        {
            string outp = Run("ip", "route show default");
            var parts = outp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i] == "via") return parts[i + 1];
            return null;
        }

        private static async Task<string?> ResolveIp(string host)
        {
            try
            {
                if (IPAddress.TryParse(host, out var direct)) return direct.ToString();
                var addrs = await Dns.GetHostAddressesAsync(host);
                return addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
