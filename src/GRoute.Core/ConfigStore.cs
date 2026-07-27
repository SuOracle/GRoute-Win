using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _saveLock = new();
    private CancellationTokenSource? _saveCts;

    public List<ProxyConfig> Configs { get; private set; } = new();
    public List<Subscription> Subscriptions { get; private set; } = new();
    public string? SelectedId { get; set; }
    public bool Fragment { get; private set; }
    public bool SplitRouting { get; private set; }
    public bool SniffEnabled { get; private set; } = true;
    public List<string> SniffProtocols { get; private set; } = new() { "http", "tls", "quic" };
    public bool SniffRouteOnly { get; private set; } = true;
    public Lang Lang { get; private set; }
    public int AutoRefreshHours { get; private set; } = 1;
    public int MixedPort { get; private set; } = 10626;
    public string LogLevel { get; private set; } = "warning";
    public string? LastQualityAt { get; private set; }
    public string? LastQualityServer { get; private set; }
    public bool AutoSelect { get; private set; }
    public bool DarkTheme { get; private set; } = true;
    public bool KillSwitch { get; private set; }
    public bool RunAtStartup { get; private set; }
    public bool TorOnion { get; private set; }
    public string TorExit { get; private set; } = "";
    public string PsiphonRegion { get; private set; } = "";
    public bool SecureDns { get; private set; } = true;
    public bool Mux { get; private set; }
    public bool AdBlock { get; private set; }
    public int MuxConcurrency { get; private set; } = 8;
    public string Mode { get; private set; } = "system";

    public string ExitAction { get; private set; } = "exit";

    public string MinimizeAction { get; private set; } = "minimize";

    public string BackgroundSkin { get; private set; } = "plain";

    public ConfigStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "configs.json");
        Lang = SystemDefaultLang();
        Load();
    }

    public void Add(ProxyConfig config)
    {
        Configs.Add(config);
        Save();
    }

    public void Delete(string id)
    {
        Configs.RemoveAll(c => c.Id == id);
        if (SelectedId == id) SelectedId = null;
        Save();
    }

    public ProxyConfig? Selected() => Configs.FirstOrDefault(c => c.Id == SelectedId);

    public void UpsertSubscription(Subscription sub, List<ProxyConfig> fetched)
    {
        foreach (var c in fetched) c.SubId = sub.Id;

        var selected = Configs.FirstOrDefault(c => c.Id == SelectedId);
        string? selectedKey = selected is not null && selected.SubId == sub.Id ? MatchKey(selected) : null;

        Configs.RemoveAll(c => c.SubId == sub.Id);
        Configs.AddRange(fetched);
        Subscriptions.RemoveAll(s => s.Id == sub.Id);
        Subscriptions.Add(sub);

        if (selectedKey is not null)
        {
            var match = fetched.FirstOrDefault(c => MatchKey(c) == selectedKey);
            if (match is not null) SelectedId = match.Id;
        }

        Save();
    }

    private static string MatchKey(ProxyConfig c) =>
        $"{c.Protocol}|{c.Address}|{c.Port}|{c.Uuid}|{c.Password}|{c.Network}|{c.Security}";

    public void DeleteSubscription(string id)
    {
        Configs.RemoveAll(c => c.SubId == id);
        Subscriptions.RemoveAll(s => s.Id == id);
        Save();
    }

    public void SetFragment(bool value)
    {
        Fragment = value;
        Save();
    }

    public void SetKillSwitch(bool value)
    {
        KillSwitch = value;
        Save();
    }

    public void SetRunAtStartup(bool value)
    {
        RunAtStartup = value;
        Save();
    }

    public void SetTorOnion(bool value)
    {
        TorOnion = value;
        Save();
    }

    public void SetTorExit(string value)
    {
        TorExit = value ?? "";
        Save();
    }

    public void SetPsiphonRegion(string value)
    {
        PsiphonRegion = value ?? "";
        Save();
    }

    public void SetSecureDns(bool value)
    {
        SecureDns = value;
        Save();
    }

    public void SetAdBlock(bool value)
    {
        AdBlock = value;
        Save();
    }

    public void SetMux(bool value)
    {
        Mux = value;
        Save();
    }

    public void SetMuxConcurrency(int value)
    {
        MuxConcurrency = Math.Clamp(value, 1, 128);
        Save();
    }

    public void SetSplitRouting(bool value)
    {
        SplitRouting = value;
        Save();
    }

    public void SetSniffEnabled(bool value)
    {
        SniffEnabled = value;
        Save();
    }

    public void SetSniffProtocols(List<string> protocols)
    {
        SniffProtocols = protocols;
        Save();
    }

    public void SetSniffRouteOnly(bool value)
    {
        SniffRouteOnly = value;
        Save();
    }

    public void SetLang(Lang value)
    {
        Lang = value;
        Save();
    }

    public void SetAutoRefreshHours(int hours)
    {
        AutoRefreshHours = hours;
        Save();
    }

    public void SetMixedPort(int port)
    {
        MixedPort = port;
        Save();
    }

    public void SetExitAction(string value)
    {
        ExitAction = value;
        Save();
    }

    public void SetMinimizeAction(string value)
    {
        MinimizeAction = value;
        Save();
    }

    public void SetBackgroundSkin(string value)
    {
        BackgroundSkin = value;
        Save();
    }

    public void SetLogLevel(string level)
    {
        LogLevel = level;
        Save();
    }

    public void SetDarkTheme(bool dark)
    {
        DarkTheme = dark;
        Save();
    }

    public void SetMode(string mode)
    {
        Mode = mode;
        Save();
    }

    public void SetAutoSelect(bool value)
    {
        AutoSelect = value;
        Save();
    }

    public void SetLastQuality(string at, string server)
    {
        LastQualityAt = at;
        LastQualityServer = server;
        Save();
    }

    public void Save()
    {
        _saveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _saveCts = cts;
        var token = cts.Token;
        Task.Run(async () =>
        {
            try { await Task.Delay(400, token); } catch { return; }
            if (token.IsCancellationRequested) return;
            SaveNow();
        });
    }

    public void SaveNow()
    {
        try
        {
            StoreData data;
            lock (_saveLock)
            {
                data = new StoreData
            {
                Configs = Configs,
                Subscriptions = Subscriptions,
                SelectedId = SelectedId,
                Fragment = Fragment,
                SplitRouting = SplitRouting,
                SniffEnabled = SniffEnabled,
                SniffProtocols = SniffProtocols,
                SniffRouteOnly = SniffRouteOnly,
                Lang = Lang,
                AutoRefreshHours = AutoRefreshHours,
                MixedPort = MixedPort,
                LogLevel = LogLevel,
                DarkTheme = DarkTheme,
                Mode = Mode,
                ExitAction = ExitAction,
                MinimizeAction = MinimizeAction,
                BackgroundSkin = BackgroundSkin,
                KillSwitch = KillSwitch,
                Mux = Mux,
                AdBlock = AdBlock,
                    MuxConcurrency = MuxConcurrency,
                    LastQualityAt = LastQualityAt,
                    LastQualityServer = LastQualityServer,
                    AutoSelect = AutoSelect
                };
            }
            var json = JsonSerializer.Serialize(data, Options);
            lock (_saveLock)
            {
                File.WriteAllText(_path, json);
            }
        }
        catch
        {
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(_path));
            if (data is not null)
            {
                Configs = data.Configs ?? new();
                Subscriptions = data.Subscriptions ?? new();
                SelectedId = data.SelectedId;
                Fragment = data.Fragment;
                SplitRouting = data.SplitRouting;
                if (data.SniffEnabled.HasValue) SniffEnabled = data.SniffEnabled.Value;
                if (data.SniffProtocols is not null) SniffProtocols = data.SniffProtocols;
                if (data.SniffRouteOnly.HasValue) SniffRouteOnly = data.SniffRouteOnly.Value;
                if (data.Lang.HasValue) Lang = data.Lang.Value;
                AutoRefreshHours = data.AutoRefreshHours;
                if (data.MixedPort.HasValue && data.MixedPort.Value > 0) MixedPort = data.MixedPort.Value;
                if (!string.IsNullOrEmpty(data.LogLevel)) LogLevel = data.LogLevel;
                if (data.DarkTheme.HasValue) DarkTheme = data.DarkTheme.Value;
                if (!string.IsNullOrEmpty(data.Mode)) Mode = data.Mode;
                if (!string.IsNullOrEmpty(data.ExitAction)) ExitAction = data.ExitAction;
                if (!string.IsNullOrEmpty(data.MinimizeAction)) MinimizeAction = data.MinimizeAction;
                if (!string.IsNullOrEmpty(data.BackgroundSkin)) BackgroundSkin = data.BackgroundSkin;
                LastQualityAt = data.LastQualityAt;
                LastQualityServer = data.LastQualityServer;
                if (data.AutoSelect.HasValue) AutoSelect = data.AutoSelect.Value;
                if (data.KillSwitch.HasValue) KillSwitch = data.KillSwitch.Value;
                if (data.Mux.HasValue) Mux = data.Mux.Value;
                if (data.AdBlock.HasValue) AdBlock = data.AdBlock.Value;
                if (data.MuxConcurrency.HasValue) MuxConcurrency = data.MuxConcurrency.Value;
            }
        }
        catch
        {
            Configs = new();
            Subscriptions = new();
            SelectedId = null;
        }
    }

    private static Lang SystemDefaultLang() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? Lang.Fa : Lang.En;

    private sealed class StoreData
    {
        public List<ProxyConfig> Configs { get; set; } = new();
        public List<Subscription> Subscriptions { get; set; } = new();
        public string? SelectedId { get; set; }
        public bool Fragment { get; set; }
        public bool SplitRouting { get; set; }
        public bool? SniffEnabled { get; set; }
        public List<string>? SniffProtocols { get; set; }
        public bool? SniffRouteOnly { get; set; }
        public Lang? Lang { get; set; }
        public int AutoRefreshHours { get; set; }
        public int? MixedPort { get; set; }
        public string? LogLevel { get; set; }
        public bool? DarkTheme { get; set; }
        public string? Mode { get; set; }
        public string? ExitAction { get; set; }
        public string? MinimizeAction { get; set; }
        public string? BackgroundSkin { get; set; }
        public bool? KillSwitch { get; set; }
        public bool? Mux { get; set; }
        public bool? AdBlock { get; set; }
        public int? MuxConcurrency { get; set; }
        public string? LastQualityAt { get; set; }
        public string? LastQualityServer { get; set; }
        public bool? AutoSelect { get; set; }
    }
}
