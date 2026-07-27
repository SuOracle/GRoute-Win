namespace GRoute.Windows.Core;

public interface ISystemProxy
{
    void Enable(string proxyAddress);
    void Disable();
}

public interface IElevation
{
    bool IsAdministrator();
    bool RelaunchAsAdmin(string? args = null);
}

public interface IKillSwitch
{
    bool Engaged { get; }
    void Engage();
    void Release();
}

public interface IStartupTask
{
    bool Supported { get; }
    bool Enabled { get; }
    void Set(bool enabled);
}

public interface ITunService
{
    event Action<string>? Log;
    bool BinariesPresent { get; }
    string? MissingBinary();
    Task<bool> Start(ProxyConfig config);
    void Stop();
}

public static class Platform
{
    public static ISystemProxy Proxy { get; set; } = new NullSystemProxy();
    public static IElevation Elevation { get; set; } = new NullElevation();
    public static IKillSwitch KillSwitch { get; set; } = new NullKillSwitch();
    public static Func<ITunService>? CreateTun { get; set; }
    public static IStartupTask Startup { get; set; } = new NullStartupTask();

    private sealed class NullSystemProxy : ISystemProxy
    {
        public void Enable(string proxyAddress)
        {
        }

        public void Disable()
        {
        }
    }

    private sealed class NullStartupTask : IStartupTask
    {
        public bool Supported => false;

        public bool Enabled => false;

        public void Set(bool enabled)
        {
        }
    }

    private sealed class NullElevation : IElevation
    {
        public bool IsAdministrator() => false;

        public bool RelaunchAsAdmin(string? args = null) => false;
    }

    private sealed class NullKillSwitch : IKillSwitch
    {
        public bool Engaged => false;

        public void Engage()
        {
        }

        public void Release()
        {
        }
    }
}
