using System.Collections.Generic;

namespace GRoute.Windows.Core;

public sealed class CoreController
{
    private readonly XrayController _xray = new();
    private readonly SingBoxController _sing = new();
    private readonly AetherController _aether = new();
    private readonly TorController _tor = new();
    private bool _usingSing;

    public event Action<string>? LogReceived;
    public event Action<ConnectionState>? StateChanged;

    public CoreController()
    {
        _xray.LogReceived += s => LogReceived?.Invoke(s);
        _sing.LogReceived += s => LogReceived?.Invoke(s);
        _aether.LogReceived += s => LogReceived?.Invoke(s);
        _tor.Log += s => LogReceived?.Invoke(s);
        _xray.StateChanged += s => { if (!_usingSing) StateChanged?.Invoke(s); };
        _sing.StateChanged += s => { if (_usingSing) StateChanged?.Invoke(s); };
    }

    public bool SingBoxAvailable => _sing.Available;

    public string SingBoxPath => _sing.BinaryPath;

    public bool UsingSingBox => _usingSing;

    public bool IsRunning => _xray.IsRunning || _sing.IsRunning;

    public ConnectionState State => _usingSing ? _sing.State : _xray.State;

    public bool AetherAvailable => _aether.Available;

    public static bool TorAvailable => TorController.Available;

    public static string TorPath => TorController.BinaryPath;

    public bool TorRunning => _tor.IsRunning;

    public string AetherPath => _aether.BinaryPath;

    public static bool UsesAether(ProxyConfig config) =>
        string.Equals(config.Protocol, "aether", StringComparison.OrdinalIgnoreCase);

    public static bool UsesSingBox(ProxyConfig config) => SingBoxBuilder.Supports(config);

    public string BuildConfig(ProxyConfig config, bool fragment, bool splitRouting, bool sniffEnabled,
        IReadOnlyList<string>? sniffProtocols, bool sniffRouteOnly, bool mux, int muxConcurrency, bool adBlock,
        bool torOnion = false, bool secureDns = true, int torNodePort = 0)
    {
        if (UsesSingBox(config)) return SingBoxBuilder.Build(config);

        var target = config;
        if (UsesAether(config))
        {
            target = new ProxyConfig
            {
                Name = config.Name,
                Protocol = "socks",
                Address = "127.0.0.1",
                Port = AetherController.SocksPort
            };
        }

        return ConfigBuilder.Build(target, fragment, splitRouting, sniffEnabled, sniffProtocols,
            sniffRouteOnly, mux, muxConcurrency, adBlock, torOnion && TorController.Available, secureDns,
            torNodePort, UsesAether(config), config.Address);
    }

    public void Start(ProxyConfig config, string configJson) => Start(config, configJson, false, null);

    public void Start(ProxyConfig config, string configJson, bool torOnion, string? torExit)
    {
        bool sing = UsesSingBox(config);
        bool aether = UsesAether(config);

        if (sing) _xray.Stop();
        else _sing.Stop();
        if (!aether) _aether.Stop();

        _usingSing = sing;

        if (aether && !_aether.IsRunning)
        {
            LogReceived?.Invoke(
                "aether: scanning for a working MASQUE gateway, this can take up to two minutes...");
            _aether.Start(config);
            if (!_aether.WaitReady(AetherController.ReadyTimeoutMs))
            {
                _aether.Stop();
                throw new InvalidOperationException(
                    "aether found no working MASQUE gateway on this network.");
            }
        }

        if (sing) _sing.Start(configJson);
        else _xray.Start(configJson);

        if (torOnion && TorController.Available && !sing)
        {
            if (_tor.Start(throughVpn: true, exitCountry: torExit))
                LogReceived?.Invoke("Tor: .onion addresses will route through the Tor network.");
        }
        else
        {
            _tor.Stop();
        }
    }

    public void StopTor() => _tor.Stop();

    public bool StartAetherOnly(ProxyConfig config, int timeoutMs)
    {
        if (_aether.IsRunning) return true;
        _aether.Start(config);
        return _aether.WaitReady(timeoutMs);
    }

    public void StopAether() => _aether.Stop();

    public void Stop()
    {
        _tor.Stop();
        if (_usingSing) _sing.Stop();
        else _xray.Stop();
        _aether.Stop();
    }

    public void StopAll()
    {
        _tor.Stop();
        _xray.Stop();
        _sing.Stop();
        _aether.Stop();
    }
}
