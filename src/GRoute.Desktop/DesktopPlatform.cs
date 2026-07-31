using GRoute.Windows.Core;

namespace GRoute.Desktop;

public static class DesktopPlatform
{

    public static void CleanupStaleState()
    {
        try
        {
            if (NetGuard.IsDirty("killswitch"))
            {
                Netsh("advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound");
                for (int i = 0; i < 8; i++)
                    Netsh($"advfirewall firewall delete rule name=\"GRoute Kill Switch Allow {i}\"");
                Netsh("advfirewall firewall delete rule name=\"GRoute Kill Switch Loopback\"");
                Netsh("advfirewall firewall delete rule name=\"GRoute Kill Switch LocalNet\"");
                Netsh("advfirewall firewall delete rule name=\"GRoute Kill Switch\"");
            }

            if (NetGuard.IsDirty("tun"))
            {
                Cmd("route", "delete 0.0.0.0 mask 128.0.0.0");
                Cmd("route", "delete 128.0.0.0 mask 128.0.0.0");
                Netsh("interface ipv6 delete route ::/1 \"GRoute\"");
                Netsh("interface ipv6 delete route 8000::/1 \"GRoute\"");
            }

            if (NetGuard.IsDirty("proxy"))
            {
                try { new WindowsSystemProxy().Disable(); } catch { }
            }

            foreach (var name in new[] { "tun2socks-windows-amd64", "tor", "xray", "sing-box", "aether" })
            {
                try
                {
                    foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                    {
                        try
                        {
                            string? path = p.MainModule?.FileName;
                            if (path is not null &&
                                path.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                                p.Kill(entireProcessTree: true);
                        }
                        catch { }
                        finally { try { p.Dispose(); } catch { } }
                    }
                }
                catch { }
            }

            NetGuard.ClearAll();
        }
        catch
        {
        }
    }

    private static void Netsh(string args) => Cmd("netsh", args);

    private static void Cmd(string file, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(6000);
        }
        catch
        {
        }
    }

    public static void Register()
    {
        CleanupStaleState();

        if (!OperatingSystem.IsWindows()) return;

        Platform.Proxy = new WindowsSystemProxy();
        Platform.Elevation = new WindowsElevation();
        Platform.KillSwitch = new WindowsKillSwitch();
        Platform.CreateTun = () => new WindowsTun();
        Platform.Startup = new WindowsStartup();
    }

    private sealed class WindowsSystemProxy : ISystemProxy
    {
        public void Enable(string proxyAddress) => SystemProxy.Enable(proxyAddress);

        public void Disable() => SystemProxy.Disable();
    }

    private sealed class WindowsElevation : IElevation
    {
        public bool IsAdministrator() => Elevation.IsAdministrator();

        public bool RelaunchAsAdmin(string? args = null) => Elevation.RelaunchAsAdmin(args);
    }

    private sealed class WindowsKillSwitch : IKillSwitch
    {
        public bool Engaged => KillSwitch.Engaged;

        public void Engage() => KillSwitch.Engage();

        public void Release() => KillSwitch.Release();
    }

    private sealed class WindowsStartup : IStartupTask
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "GRoute";

        public bool Supported => OperatingSystem.IsWindows();

        public bool Enabled
        {
            get
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is string s && s.Length > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void Set(bool enabled)
        {
            if (enabled)
            {
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return;
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
                key?.SetValue(ValueName, $"\"{exe}\" --autostart");
            }
            else
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }

    private sealed class WindowsTun : ITunService
    {
        private readonly TunController _inner = new();

        public WindowsTun()
        {
            _inner.Log += line => Log?.Invoke(line);
        }

        public event Action<string>? Log;

        public bool BinariesPresent => TunController.BinariesPresent;

        public string? MissingBinary() => TunController.MissingBinary();

        public Task<bool> Start(ProxyConfig config) => _inner.Start(config);

        public void Stop() => _inner.Stop();
    }
}
