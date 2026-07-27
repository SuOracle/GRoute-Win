using GRoute.Windows.Core;

namespace GRoute.Desktop;

public static class DesktopPlatform
{
    public static void Register()
    {
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
