using System.Globalization;
using System.IO;
using System.Text.Json;

namespace GRoute.Windows.Core;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

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
        Configs.RemoveAll(c => c.SubId == sub.Id);
        Configs.AddRange(fetched);
        Subscriptions.RemoveAll(s => s.Id == sub.Id);
        Subscriptions.Add(sub);
        Save();
    }

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

    public void SetLogLevel(string level)
    {
        LogLevel = level;
        Save();
    }

    public void Save()
    {
        try
        {
            var data = new StoreData
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
                LogLevel = LogLevel
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(data, Options));
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
    }
}
