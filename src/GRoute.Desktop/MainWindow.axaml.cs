using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Transformation;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using GRoute.Desktop.Controls;
using GRoute.Windows;
using GRoute.Windows.Core;
using AvPath = Avalonia.Controls.Shapes.Path;

namespace GRoute.Desktop;

public partial class MainWindow : Window
{
    private readonly ConfigStore _store = new();
    private readonly CoreController _core = new();
    private readonly StringBuilder _log = new();
    private readonly ObservableCollection<SubGroupVm> _groups = new();
    private readonly HashSet<string> _expandedIds = new();
    private string _searchQuery = "";
    private const string ManualId = "__manual__";
    private enum SortMode { None, Fastest, Alpha, Added }
    private SortMode _sortMode = SortMode.Added;
    private bool _selectMode;
    private DispatcherTimer? _holdTimer;
    private bool _rowDragging;
    private bool _rowDragValue;
    private DispatcherTimer? _dragScrollTimer;
    private double _dragScrollSpeed;
    private Point _lastDragPos;
    private IPointer? _pressPointer;
    private bool _renameActive;
    private readonly Dictionary<Grid, DispatcherTimer> _marqueeTimers = new();
    private const int LocationSettleMs = 700;
    private const int WarpScanTarget = 8;
    private const int WarpScanMax = 160;
    private const int WarpScanConcurrency = 8;
    private const int WarpScanBudgetMs = 60000;
    private string? _holdId;
    private bool _busy;
    private readonly SemaphoreSlim _powerLock = new(1, 1);
    private int _powerGen;
    private bool _proxyOn;
    private bool _loading = true;
    private bool _projRunning;
    private CancellationTokenSource? _projCts;
    private CancellationTokenSource? _statsCts;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _qualityCts;
    private readonly List<ProxyConfig> _exportConfigs = new();
    private byte[]? _pendingImport;
    private CancellationTokenSource? _autoSelectCts;
    private DispatcherTimer? _autoRefreshTimer;
    private TrayIcon? _tray;
    private NativeMenuItem? _trayConnect;
    private NativeMenuItem? _trayModeProxy;
    private NativeMenuItem? _trayModeSystem;
    private NativeMenuItem? _trayModeTun;
    private bool _exiting;
    private int _blurMaskHash;
    private string _tab = "connect";
    private readonly Stack<string> _tabHistory = new();
    private bool _tabBack;
    private double _connectDesignH = -1;
    private static readonly string[] TabOrder =
    {
        "connect", "picker", "manual", "projects", "scanner", "quality",
        "export", "usage", "tools", "logs", "settings", "prefs", "about"
    };
    private int _prevTabIdx;
    private DispatcherTimer? _slideTimer;
    private DispatcherTimer? _heightTimer;
    private Lang _lang = Lang.En;

    private static readonly string[] StrKeys =
    {
        "system_proxy", "tun", "fragment", "split", "sniffing", "takes_effect", "add_hint", "add",
        "subscriptions", "refresh", "remove", "servers", "fastest", "delete", "route_only",
        "nav_connect", "nav_usage", "nav_tools", "nav_about", "check_update", "theme", "nav_logs",
        "export_lock", "export_lock_note", "startup_title", "startup_hint",
        "tor_title", "tor_hint", "dns_title", "dns_hint",
        "adblock_title", "about_version", "kill_admin_prompt", "manual_configs", "title_admin", "warp_blocked",
        "logs_clear",
        "err_title", "err_parse", "err_timeout", "err_network", "err_empty_sub", "err_clipboard",
        "loc_unknown",
        "unit_ms", "ping_fail",
        "advanced", "add_and_fetch", "proj_aether_desc", "proj_warp_desc", "cf_stopped", "legacy_warp",
        "fragment_sub", "split_sub", "sniffing_sub", "adblock_sub", "usage_vpn", "sub_url",
        "tor_exit", "tor_exit_any", "tor_exit_warn", "tor_nodes", "tor_nodes_hint",
        "psiphon_hint", "psiphon_region",
        "err_no_server", "tor_pick_title", "tor_pick_open", "tor_pick_some", "tor_pick_all", "tor_pick_none",
        "net_off", "tun_admin_prompt", "tun_admin_ok", "nav_prefs", "skin_title",
        "f_transport", "f_security", "f_encryption", "sec_none", "net_tcp",
        "f_protocol", "f_name", "f_address", "f_port", "f_uuid", "f_password", "f_cipher",
        "f_flow", "f_privkey", "f_peerkey", "f_psk", "f_wgaddr", "f_mtu", "f_reserved",
        "f_path", "f_hosthdr", "f_grpc", "f_mode", "f_header", "f_sni", "f_alpn",
        "f_fingerprint", "f_pbk", "f_sid", "f_insecure", "f_alterid", "save", "cancel",
        "f_username", "f_obfs", "f_obfs_pw", "copied", "edit_config", "copy", "edit",
        "manual_basics", "manual_auth", "manual_transport", "manual_tls", "skin_hint", "skin_dots", "skin_stars", "skin_vein", "skin_plain", "language",
        "window_buttons", "window_buttons_hint", "exit_button", "minimize_button",
        "act_exit", "act_hide", "act_minimize",
        "status_disconnected", "status_connected", "coming_soon",
        "usage_all_time", "range_custom", "usage_from", "usage_to", "total", "usage_other", "logs_empty", "range_today", "range_7d", "range_30d",
        "usage_hint", "usage_no_data", "usage_download", "usage_upload", "usage_most_used",
        "auto_refresh", "auto_off", "cf_title", "cf_scan", "cf_use", "cf_hint", "cf_pool", "cf_stop", "cf_results",
        "about_tagline", "about_engine", "about_support", "about_source",
        "q_title", "q_test", "q_hint", "q_ping", "q_jitter", "q_latency",
        "conn_settings", "conn_mode", "mixed_port", "port_invalid", "log_level",
        "log_none", "log_error", "log_warning", "log_info", "log_debug",
        "copy", "net_off", "selected_server", "tap_choose", "choose_server", "paste_clipboard",
        "add_manually", "save", "cancel", "back", "test_all", "testing",
        "edit", "rename", "search", "sort", "sort_added", "sort_alpha", "sort_fastest",
        "quick_title", "mode_proxy", "mode_system", "mode_tun",
        "kill_switch", "kill_switch_sub", "mux", "mux_sub", "mux_concurrency",
        "conn_mode_desc", "export_grt", "import_grt", "grt_pw_hint", "share_as_file",
        "import_from_file", "import_hint", "import_password",
        "show", "add_server", "add_warp", "auto_fastest", "auto_fastest_sub", "test_sub",
        "free_projects", "legacy_warp", "see_logs", "share_selected", "selected_count",
        "del_all_group", "del_timed_out", "export_continue", "export_encrypted_note",
        "export_file_name", "export_locked_note", "export_password",
        "q_download", "q_upload", "q_rating", "q_ping_idle", "q_ping_down", "q_ping_up",
        "change", "geo_files", "geo_hint", "missing", "present", "reset",
        "mode_proxy_d", "mode_system_d", "mode_tun_d"
    };
    private const int AutoSelectIntervalMs = 60000;
    private const int AutoSelectConcurrency = 4;
    private const int AutoSelectMarginMs = 40;
    private const string UpdateUrl = "https://raw.githubusercontent.com/SuOracle/GRoute-Win/main/version.json";
    private readonly ObservableCollection<Subscription> _subs = new();
    private readonly ObservableCollection<ScanResult> _scanResults = new();
    private int _scanPool = 300;
    private TaskCompletionSource<bool>? _dlgTcs;

    public MainWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsWindows()) DesktopPlatform.Register();
        else if (OperatingSystem.IsLinux()) LinuxPlatform.Register();

        _appIcon = LoadAppIcon();
        if (_appIcon is not null) Icon = _appIcon;

        _core.StateChanged += OnState;
        _core.LogReceived += OnLog;

        ApplyBackgroundSkin();
        Loaded += OnWindowLoaded;
        Loaded += (_, _) => StartNetWatch();
        if (TestRing is not null) TestRing.Background = TestSweepBrush();
        AttachMenuPress(SortAddedItem);
        AttachMenuPress(SortAlphaItem);
        AttachMenuPress(SortFastestItem);
        AddHandler(PointerPressedEvent, DropTextFocus, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, VeinStrike, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, PillPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, PillReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AttachCardButton(ToolCf, () => ShowTab("scanner"), 0.965, bounce: true);
        AttachCardButton(ToolQuality, () => OpenQuality(ToolQuality, new RoutedEventArgs()), 0.965, bounce: true);
        AttachCardButton(SelectedBox, () => ShowTab("picker"), 1.0, bounce: false);
        QualityButton.PointerEntered += (_, _) => QualityGlow.Cell = 10.0;
        QualityButton.PointerExited += (_, _) =>
        {
            if (QualityButton.IsEnabled) QualityGlow.Cell = 15.0;
        };
        QuickSheet.RenderTransform = _sheetT;
        AttachWrapBounce(QuickButton, QuickWrap);
        AttachWrapBounce(KillQuickButton, KillWrap);

        QuickSheet.PointerPressed += SheetDragStart;
        QuickSheet.PointerMoved += SheetDragMove;
        QuickSheet.PointerReleased += SheetDragEnd;
        QuickSheet.PointerCaptureLost += (_, _) =>
        {
            if (!_sheetDragging) return;
            _sheetDragging = false;
            AnimateSheet(0, 1, 180, easeIn: false);
        };
        PingButton.AddHandler(PointerPressedEvent, PingPress,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        PingButton.AddHandler(PointerReleasedEvent, PingRelease,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        PingButton.AddHandler(PointerExitedEvent, PingExit,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Earth.MarkerFrame += OnMarkerFrame;
        LayoutUpdated += (_, _) => UpdateBlurMask();
        LabelCanvas.SizeChanged += (_, _) => RenderLabels();
    }

    private void AnimateIn(Control? shown, int dir)
    {
        if (shown is null) return;
        _slideTimer?.Stop();
        shown.Opacity = 0;
        shown.RenderTransform = new TranslateTransform(dir * 34.0, 0);
        var t0 = DateTime.UtcNow;
        var slide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _slideTimer = slide;
        slide.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_slideTimer, slide))
            {
                slide.Stop();
                return;
            }
            double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
            double ps = Math.Min(1, ms / 240.0);
            double pf = Math.Min(1, ms / 190.0);
            double ease = 1 - Math.Pow(1 - ps, 3);
            shown.RenderTransform = new TranslateTransform(dir * 34.0 * (1 - ease), 0);
            shown.Opacity = pf;
            if (ps >= 1 && pf >= 1)
            {
                shown.RenderTransform = null;
                shown.Opacity = 1;
                slide.Stop();
                _slideTimer = null;
            }
        };
        slide.Start();
    }

    private ScrollViewer? ScrollerFor(string tab) => tab switch
    {
        "picker" => GroupScroll,
        "torpick" => TorPickScroll,
        "manual" => ManualPanel,
        "settings" => SettingsPanel,
        "tools" => ToolsPanel,
        "usage" => UsagePanel,
        "quality" => QualityPanel,
        "prefs" => PrefsPanel,
        "about" => AboutPanel,
        _ => null
    };

    private Control? ActivePanel() => _tab switch
    {
        "picker" => PickerPanel,
        "torpick" => TorPickPanel,
        "manual" => ManualPanel,
        "projects" => ProjectsPanel,
        "scanner" => ScannerPanel,
        "quality" => QualityPanel,
        "usage" => UsagePanel,
        "tools" => ToolsPanel,
        "prefs" => PrefsPanel,
        "about" => AboutPanel,
        "logs" => LogsPanel,
        "settings" => SettingsPanel,
        "export" => ExportPanel,
        _ => ConnectPanel
    };

    private bool _heightQueued;
    private bool _surfaceDriven;
    private bool _userDisconnect;

    private bool _drawerOpen;
    private double _drawerH;
    private bool _drawerMeasured;
    private bool _netWarnShown;
    private double _netWarnH;
    private DispatcherTimer? _netTimer;
    private DispatcherTimer? _netWarnAnim;
    private DispatcherTimer? _netPulse;

    private void StartNetWatch()
    {
        if (_netTimer is not null) return;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (_, _) => UpdateNetworkWarning();
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += (_, _) => UpdateNetworkWarning();
        UpdateNetworkWarning();
        _netTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _netTimer.Tick += (_, _) => UpdateNetworkWarning();
        _netTimer.Start();
    }

    private static async Task<bool> TryConnect(string host, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(2200);
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ProbeInternet()
    {
        string[] hosts = { "1.1.1.1", "8.8.8.8", "9.9.9.9" };
        var tasks = new List<Task<bool>>();
        foreach (var h in hosts) tasks.Add(TryConnect(h, 443));
        var results = await Task.WhenAll(tasks);
        foreach (var r in results) if (r) return true;
        return false;
    }

    private async void UpdateNetworkWarning()
    {
        if (_store.KillSwitch && GRoute.Windows.Core.KillSwitch.Engaged)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_netWarnShown) return;
                _netWarnShown = false;
                ShowNetWarn(false);
            });
            return;
        }

        bool online = await ProbeInternet();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            bool show = !online;
            if (_netWarnShown == show) return;
            _netWarnShown = show;
            ShowNetWarn(show);
        });
    }

    private void ShowNetWarn(bool show)
    {
        if (NetWarnBox is null || NetWarnContent is null) return;

        if (_netWarnH <= 0)
        {
            NetWarnContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _netWarnH = NetWarnContent.DesiredSize.Height + 23;
        }

        if (show)
        {
            NetWarnBox.IsVisible = true;
            StartNetPulse();
        }
        else
        {
            StopNetPulse();
        }

        _netWarnAnim?.Stop();
        double from = double.IsNaN(NetWarnBox.Height) ? (show ? 0 : _netWarnH) : NetWarnBox.Height;
        double to = show ? _netWarnH : 0;
        NetWarnBox.Height = from;

        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _netWarnAnim = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_netWarnAnim, timer))
            {
                timer.Stop();
                return;
            }
            double p = Math.Min(1, (DateTime.UtcNow - t0).TotalMilliseconds / 340.0);
            double e = p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;
            NetWarnBox.Height = from + (to - from) * e;
            if (p < 1) return;
            timer.Stop();
            _netWarnAnim = null;
            NetWarnBox.Height = double.NaN;
            if (!show) NetWarnBox.IsVisible = false;
        };
        timer.Start();

        QueueWindowHeight();
    }

    private void StartNetPulse()
    {
        if (_netPulse is not null || NetWarnContent is null) return;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _netPulse = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_netPulse, timer) || NetWarnContent is null)
            {
                timer.Stop();
                return;
            }
            double t = (DateTime.UtcNow - t0).TotalMilliseconds / 1300.0;
            double wave = 0.5 + 0.5 * Math.Cos(t * Math.PI * 2);
            NetWarnContent.Opacity = 0.72 + 0.28 * wave;
        };
        timer.Start();
    }

    private void StopNetPulse()
    {
        _netPulse?.Stop();
        _netPulse = null;
        if (NetWarnContent is not null) NetWarnContent.Opacity = 1;
    }

    private void QueueWindowHeight(int retries = 25)
    {
        if (_heightQueued) return;
        _heightQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _heightQueued = false;
            // another animation owns the surface right now; try again shortly
            // rather than dropping the resize on the floor
            if (_surfaceDriven && retries > 0)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                t.Tick += (_, _) => { t.Stop(); QueueWindowHeight(retries - 1); };
                t.Start();
                return;
            }
            AnimateWindowHeight();
        }, DispatcherPriority.Loaded);
    }

    private void AnimateWindowHeight(double durMs = 240.0)
    {
        if (_surfaceDriven) return;
        if (_drawerOpen && _drawerH > 100)
        {
            double cd = double.IsNaN(AppSurface.Height) ? AppSurface.Bounds.Height : AppSurface.Height;
            if (Math.Abs(_drawerH - cd) >= 3) AnimateHeightTo(_drawerH);
            return;
        }

        var panel = ActivePanel();
        if (panel is null || HeaderRow is null) return;
        double cur = double.IsNaN(AppSurface.Height) ? AppSurface.Bounds.Height : AppSurface.Height;
        if (double.IsNaN(AppSurface.Height) && cur > 100) AppSurface.Height = cur;
        if (_tab == "connect" && _connectDesignH > 100)
        {
            double want = _connectDesignH + (_netWarnShown ? _netWarnH + 10 : 0);
            if (Math.Abs(want - cur) >= 3) AnimateHeightTo(want);
            return;
        }
        double w = AppSurface.Bounds.Width - 32;
        if (w < 100) return;

        UpdateLayout();

        HeaderRow.InvalidateMeasure();
        HeaderRow.Measure(new Size(w, double.PositiveInfinity));
        double hh = HeaderRow.DesiredSize.Height;

        panel.InvalidateMeasure();
        panel.Measure(new Size(w, double.PositiveInfinity));
        double ph = panel.DesiredSize.Height;

        HeaderRow.InvalidateMeasure();
        panel.InvalidateMeasure();

        double target = 42 + hh + ph + 4;
        if (target < 320) target = 320;
        if (target > 700) target = 700;
        if (_tab == "connect" && _connectDesignH <= 100 && target > 100)
            _connectDesignH = target;
        if (Math.Abs(target - cur) < 3) return;
        AnimateHeightTo(target, durMs);
    }

    private const double SurfaceChrome = 48.0;

    private void FitWindowToContent()
    {
        UpdateLayout();
        double h = AppSurface.Bounds.Height;
        if (h <= 100) h = AppSurface.DesiredSize.Height;
        if (h <= 100) return;
        AppSurface.Height = h;
        _connectDesignH = h;
        SyncWindowHeight(h);
    }

    private void SyncWindowHeight(double surface)
    {
        if (surface <= 0) return;
        double want = Math.Ceiling(surface + SurfaceChrome);
        if (Math.Abs(Height - want) < 1) return;
        Height = want;
    }

    private void AnimateHeightTo(double target, double durMs = 240.0, bool easeIn = false)
    {
        _heightTimer?.Stop();
        double from = double.IsNaN(AppSurface.Height) ? AppSurface.Bounds.Height : AppSurface.Height;
        if (from <= 0) from = target;

        AppSurface.Height = from;
        SyncWindowHeight(from);
        double t = 0;
        var last = DateTime.UtcNow;
        var height = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _heightTimer = height;
        height.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_heightTimer, height))
            {
                height.Stop();
                return;
            }
            var now = DateTime.UtcNow;
            double dt = (now - last).TotalMilliseconds;
            last = now;
            if (dt > 32) dt = 32;
            t += dt;
            double p = t / durMs;
            if (p >= 1)
            {
                AppSurface.Height = target;
                SyncWindowHeight(target);
                height.Stop();
                _heightTimer = null;
                return;
            }
            double e = easeIn ? p * p * p : 1 - Math.Pow(1 - p, 3);
            double h = from + (target - from) * e;
            AppSurface.Height = h;
            SyncWindowHeight(h);
        };
        height.Start();
    }

    private void UpdateBlurMask()
    {
        if (BlurLayer is null) return;

        var rects = new List<(Rect R, double Radius)>();
        foreach (var visual in this.GetVisualDescendants())
        {
            if (visual is not Border b) continue;
            if (!b.IsEffectivelyVisible) continue;
            if (b.Bounds.Width < 4 || b.Bounds.Height < 4) continue;

            bool glass = false;
            foreach (var cls in b.Classes)
            {
                if (cls == "card" || cls.StartsWith("glass", StringComparison.Ordinal)) { glass = true; break; }
            }
            if (!glass) continue;

            var origin = b.TranslatePoint(new Point(0, 0), BlurLayer);
            if (origin is null) continue;
            rects.Add((new Rect(origin.Value, b.Bounds.Size), b.CornerRadius.TopLeft));
        }

        int hash = rects.Count;
        foreach (var (r, _) in rects)
            hash = HashCode.Combine(hash, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
        if (hash == _blurMaskHash) return;
        _blurMaskHash = hash;

        if (rects.Count == 0)
        {
            BlurLayer.Clip = new RectangleGeometry(new Rect(0, 0, 0, 0));
            return;
        }

        var group = new GeometryGroup();
        foreach (var (r, radius) in rects)
            group.Children.Add(new RectangleGeometry { Rect = r, RadiusX = radius, RadiusY = radius });
        BlurLayer.Clip = group;
    }

    private void RevealWindow()
    {
        Opacity = 1;
        if (ZoomHost is null) return;

        ZoomHost.RenderTransform = null;
        ZoomHost.Opacity = 0;
        ZoomHost.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(280),
                Easing = new CubicEaseOut()
            }
        };

        DispatcherTimer.RunOnce(() => ZoomHost.Opacity = 1, TimeSpan.FromMilliseconds(60));
    }

    private void PrewarmTemplates()
    {
        if (PickerPanel is null || GroupList is null) return;

        bool wasVisible = PickerPanel.IsVisible;
        PickerPanel.IsVisible = true;

        var reopen = new List<SubGroupVm>();
        foreach (var g in _groups)
        {
            if (g.IsExpanded) continue;
            g.IsExpanded = true;
            reopen.Add(g);
        }

        GroupList.UpdateLayout();
        PickerPanel.Measure(new Size(452, 4000));
        PickerPanel.UpdateLayout();

        foreach (var g in reopen) g.IsExpanded = false;
        GroupList.UpdateLayout();
        PickerPanel.IsVisible = wasVisible;

        _lastRevealAt = DateTime.UtcNow;
        _lastRevealShow = false;

        WarmupBackground();
    }

    private void WarmupBackground()
    {
        double w = 484;
        double h = _connectDesignH > 100 ? _connectDesignH : 640;
        Veins?.Warmup(w, h);
        VeinsBlur?.Warmup(w, h);

        foreach (var panel in new Control?[]
                 { ConnectPanel, PickerPanel, SettingsPanel, ToolsPanel, PrefsPanel, AboutPanel })
        {
            if (panel is null) continue;
            bool was = panel.IsVisible;
            panel.IsVisible = true;
            panel.Measure(new Size(452, 2000));
            panel.IsVisible = was;
        }
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (Environment.GetCommandLineArgs().Any(
                a => string.Equals(a, "--autoconnect", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(900);
                if (!_core.IsRunning && _store.Selected() is not null)
                    Power_Click(this, new RoutedEventArgs());
            }, DispatcherPriority.Background);
        }

        Loaded -= OnWindowLoaded;
        Controls.SmoothScroll.Hook(this);

        if (GroupList is not null)
        {
            GroupList.ItemsSource = _groups;
            RebuildGroups();
        }
        if (SubList is not null) SubList.ItemsSource = _subs;
        if (ScanResults is not null) ScanResults.ItemsSource = _scanResults;
        ReloadSubs();
        if (AboutEngine is not null)
            AboutEngine.Text = _core.SingBoxAvailable
                ? Strings.Get(_lang, "about_engine") + " + sing-box"
                : Strings.Get(_lang, "about_engine");

        RefreshSelected();
        LoadSettings();
        ShowTab("connect");
        LoadTitleLogo();
        LoadAboutLogo();

        PrewarmTemplates();
        RevealWindow();
        _lang = _store.Lang;
        ApplyLanguage();
        RefreshQuickChecks();
        RefreshKillVisual();
        if (_store.KillSwitch && Platform.Elevation.IsAdministrator())
        {
            try
            {
                GRoute.Windows.Core.KillSwitch.Engage();
                Append("Kill switch engaged.");
            }
            catch (Exception ex)
            {
                Append("Kill switch: " + ex.Message);
            }
        }
        FitWindowToContent();
        SetPowerVisual(_core.IsRunning ? ConnectionState.Connected : ConnectionState.Disconnected);
        if (ConnectButton is not null)
        {
            ConnectButton.PointerEntered += PowerHoverEnter;
            ConnectButton.PointerExited += PowerHoverLeave;
            ConnectButton.AddHandler(PointerPressedEvent, PowerHoldStart,
                RoutingStrategies.Tunnel, true);
            ConnectButton.AddHandler(PointerReleasedEvent, PowerHoldEnd,
                RoutingStrategies.Tunnel, true);
            ConnectButton.PointerCaptureLost += PowerCaptureLost;
        }
        StartCursorBlink();
        StyleAuto();
        RefreshGeoStatus();
        if (AboutVersion is not null)
            AboutVersion.Text = Strings.Get(_lang, "about_version") + " " +
                (System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString(3) ?? "1.0.0");
        if (AboutCopyright is not null)
            AboutCopyright.Text = $"\u00A9 {DateTime.Now.Year} Oracle VPN";
        ApplyAutoSelect();
        SetupAutoRefresh();
        SetupTray();
        if (!_core.IsRunning) StartDirectPolling();
        _ = RefreshLocation(false);
    }

    private string? _lastSelectedId;

    private void RefreshSelected()
    {
        var sel = _store.Selected();
        if (PingFx is not null) PingFx.Ping = sel?.Ping ?? int.MinValue;
        ApplyPingTint(sel?.Ping ?? int.MinValue);
        if (SelectedName is null) return;
        SelectedName.Text = sel is null ? "No server" : Flags.DisplayName(sel.Name);
        var flag = Flags.ForName(sel?.Name);
        if (SelectedFlag is not null && SelectedFlagBox is not null)
        {
            SelectedFlag.Source = flag;
            SelectedFlagBox.IsVisible = flag is not null;
        }
        SelectedAddr.Text = sel is null ? "Add a server to begin" : $"{sel.Address}:{sel.Port}";
        bool changed = sel?.Id != _lastSelectedId;
        bool hadPrevious = _lastSelectedId is not null;
        _lastSelectedId = sel?.Id;
        StartTypewriter(changed && sel is not null);
        if (!changed) return;
        ResetPingLabel(sel);
        if (hadPrevious && _core.IsRunning) _ = ReconnectForAutoSelect();
    }

    private void ResetPingLabel(ProxyConfig? sel)
    {
        int p = sel?.Ping ?? int.MinValue;
        if (PingFx is not null) PingFx.Busy = false;
        _lastPingMs = p;
        SetPingLabel(p >= 0 ? PingMs(p) : Strings.Get(_lang, "q_ping"));
    }

    private static void SetVis(Control? c, bool visible)
    {
        if (c is not null) c.IsVisible = visible;
    }

    private void ShowTab(string tab)
    {
        bool home = tab == "connect";
        SetVis(QuickWrap, home);
        SetVis(KillWrap, false);
        SetVis(ConnectPanel, tab == "connect");
        SetVis(PickerPanel, tab == "picker");
        SetVis(ManualPanel, tab == "manual");
        SetVis(SettingsPanel, tab == "settings");
        SetVis(UsagePanel, tab == "usage");
        SetVis(SubsPanel, tab == "subs");
        SetVis(ScannerPanel, tab == "scanner");
        SetVis(QualityPanel, tab == "quality");
        SetVis(ExportPanel, tab == "export");
        int idx = Array.IndexOf(TabOrder, tab);
        int prev = _prevTabIdx;
        if (idx >= 0) _prevTabIdx = idx;
        int dir = idx >= 0 ? Math.Sign(idx - prev) : 1;
        if (dir == 0) dir = 1;
        if (!string.Equals(_tab, tab, StringComparison.Ordinal)) StarField.Reflow();
        if (!_tabBack && !string.Equals(_tab, tab, StringComparison.Ordinal))
        {
            if (_tabHistory.Contains(tab))
            {
                while (_tabHistory.Count > 0
                       && !string.Equals(_tabHistory.Pop(), tab, StringComparison.Ordinal))
                {
                }
            }
            else if (!_tabHistory.Contains(_tab))
            {
                _tabHistory.Push(_tab);
            }
        }
        _tabBack = false;
        _tab = tab;
        if (tab == "logs") FlushLog();
        SetVis(ProjectsPanel, tab == "projects");
        SetVis(TorPickPanel, tab == "torpick");
        SetVis(ToolsPanel, tab == "tools");
        SetVis(LogsPanel, tab == "logs");
        SetVis(PrefsPanel, tab == "prefs");
        SetVis(AboutPanel, tab == "about");
        if (tab == "prefs") RefreshPrefs();
        UpdateMenuSelection();
        UpdateTabIndicator(HeaderTabOf(tab));
        AnimateIn(ActivePanel(), dir);
        _drawerOpen = false;
        if (PanelScroll is not null) PanelScroll.Target = ScrollerFor(tab);
        AnimateWindowHeight();
        SetVis(MenuOverlay, false);
        if (tab != "picker")
        {
            SetAddMenu(false);
            if (_selectMode) ExitSelectMode();
        }
    }

    private static string HeaderTabOf(string tab) => tab switch
    {
        "picker" or "manual" or "projects" or "subs" or "export" => "connect",
        "scanner" or "quality" => "tools",
        _ => tab
    };

    private void UpdateTabIndicator(string tab)
    {
        string data;
        string key;
        switch (tab)
        {
            case "usage":
                data = "M2,2.5 V13.5 H14 M4.3,10.6 L7.3,7.4 L9.4,9.2 L13.2,4.6";
                key = "nav_usage";
                break;
            case "tools":
                data = "M14.7,3.3 C15.1,4.8 14.7,6.4 13.6,7.5 C12.5,8.6 10.9,9 9.4,8.6 L4,14 L2,12 L7.4,6.6 C7,5.1 7.4,3.5 8.5,2.4 C9.6,1.3 11.2,0.9 12.7,1.3 L10.2,3.8 L12.2,5.8 Z";
                key = "nav_tools";
                break;
            case "about":
                data = "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 M8,7.2 V11.3 M8,4.6 V4.9";
                key = "nav_about";
                break;
            case "logs":
                data = "M1.5,3.5 H14.5 V12.5 H1.5 Z M4,6.3 L6.6,8.2 L4,10.1 M8.2,10.3 H12";
                key = "nav_logs";
                break;
            case "settings":
                data = "M2.5,4.3 H13.5 M8.9,4.3 A1.7,1.7 0 1 0 12.3,4.3 A1.7,1.7 0 1 0 8.9,4.3 M2.5,8 H13.5 M3.8,8 A1.7,1.7 0 1 0 7.2,8 A1.7,1.7 0 1 0 3.8,8 M2.5,11.7 H13.5 M8.3,11.7 A1.7,1.7 0 1 0 11.7,11.7 A1.7,1.7 0 1 0 8.3,11.7";
                key = "conn_settings";
                break;
            default:
                data = "M2.2,7.4 L8,2.2 L13.8,7.4 M3.7,6.1 V13.6 H12.3 V6.1 M6.5,13.6 V9.5 H9.5 V13.6";
                key = "nav_connect";
                break;
        }
        if (CurrentTabIcon is not null) CurrentTabIcon.Data = Geometry.Parse(data);
        var title = this.FindControl<TextBlock>("CurrentTabText") ?? this.FindControl<TextBlock>("TabTitle");
        if (title is null) return;
        string text = Strings.Get(_lang, key);
        title.Text = text;
        title.Width = MeasureTitleWidth(title, text);
        Dispatcher.UIThread.Post(() =>
        {
            if (title.GetVisualParent() is Control p)
            {
                p.InvalidateMeasure();
                p.InvalidateArrange();
            }
            if (HeaderRow is not null)
            {
                HeaderRow.InvalidateMeasure();
                HeaderRow.InvalidateArrange();
            }
        }, DispatcherPriority.Loaded);
    }

    private static double MeasureTitleWidth(TextBlock title, string text)
    {
        if (string.IsNullOrEmpty(text)) return double.NaN;
        var typeface = new Typeface(title.FontFamily, title.FontStyle, title.FontWeight);
        var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, typeface, title.FontSize, null);
        return Math.Ceiling(ft.Width) + 2;
    }

    private void ReloadServers() => RebuildGroups();

    private void RebuildGroups()
    {
        UpdateSortChecks();
        if (GroupList is null) return;
        QueueWindowHeight();

        string q = _searchQuery.Trim();
        bool has = q.Length > 0;
        bool M(string? v) => (v ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);

        var wanted = new List<SubGroupVm>();

        foreach (var sub in _store.Subscriptions)
        {
            var all = _store.Configs.Where(c => c.SubId == sub.Id);
            var configs = (!has || M(sub.Name)) ? all.ToList() : all.Where(c => M(c.Name)).ToList();
            if (has && configs.Count == 0 && !M(sub.Name)) continue;

            var g = _groups.FirstOrDefault(x => x.Id == sub.Id) ?? new SubGroupVm { Id = sub.Id };
            g.Name = sub.Name;
            g.IsManual = false;
            g.IsExpanded = has || _expandedIds.Contains(sub.Id);
            g.UsageText = sub.UsageText;
            g.CountText = configs.Count.ToString();
            g.HasUsageBar = sub.Total > 0;
            if (g.HasUsageBar)
            {
                double remaining = Math.Max(sub.Total - sub.Used, 0);
                double frac = Math.Clamp(remaining / (double)sub.Total, 0.0, 1.0);
                g.RemainingStar = new GridLength(frac, GridUnitType.Star);
                g.UsedStar = new GridLength(1 - frac, GridUnitType.Star);
            }
            SyncConfigs(g, configs);
            wanted.Add(g);
        }

        var manualAll = _store.Configs.Where(c => string.IsNullOrEmpty(c.SubId));
        var manual = has ? manualAll.Where(c => M(c.Name)).ToList() : manualAll.ToList();
        if (manual.Count > 0)
        {
            var g = _groups.FirstOrDefault(x => x.Id == ManualId) ?? new SubGroupVm { Id = ManualId };
            g.Name = Strings.Get(_lang, "manual_configs");
            g.IsManual = true;
            g.IsExpanded = true;
            g.CountText = manual.Count.ToString();
            SyncConfigs(g, manual);
            wanted.Add(g);
        }

        for (int i = _groups.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(_groups[i])) _groups.RemoveAt(i);
        }
        for (int i = 0; i < wanted.Count; i++)
        {
            int at = _groups.IndexOf(wanted[i]);
            if (at < 0) _groups.Insert(i, wanted[i]);
            else if (at != i) _groups.Move(at, i);
        }
    }

    private void SyncConfigs(SubGroupVm g, List<ProxyConfig> configs)
    {
        var ordered = (_sortMode switch
        {
            SortMode.Fastest => configs.OrderBy(c => PingRank(c.PingText)),
            SortMode.Alpha => configs.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            _ => configs.AsEnumerable()
        }).ToList();

        for (int i = g.Configs.Count - 1; i >= 0; i--)
        {
            if (!ordered.Contains(g.Configs[i])) g.Configs.RemoveAt(i);
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            int at = g.Configs.IndexOf(ordered[i]);
            if (at < 0) g.Configs.Insert(i, ordered[i]);
            else if (at != i) g.Configs.Move(at, i);
        }
    }

    private void Search_Changed(object? sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchInput.Text ?? "";
        RebuildGroups();
        QueueWindowHeight();
    }

    private void AddConfigs(SubGroupVm g, List<ProxyConfig> configs)
    {
        var ordered = _sortMode switch
        {
            SortMode.Fastest => configs.OrderBy(c => PingRank(c.PingText)),
            SortMode.Alpha => configs.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            _ => configs.AsEnumerable()
        };
        foreach (var c in ordered) g.Configs.Add(c);
    }

    private static int PingRank(string? pingText)
    {
        string t = pingText ?? "";
        if (int.TryParse(t.Replace(" ms", ""), out int ms)) return ms;
        return t switch
        {
            "..." => 1_000_000,
            "fail" => 3_000_000,
            _ => 2_000_000
        };
    }

    private void SortButton_Click(object? sender, RoutedEventArgs e)
    {
    }

    private void SortFastest_Click(object? sender, RoutedEventArgs e)
    {
        _sortMode = _sortMode == SortMode.Fastest ? SortMode.Added : SortMode.Fastest;
        UpdateSortChecks();
        RebuildGroups();
    }

    private void SortAlpha_Click(object? sender, RoutedEventArgs e)
    {
        _sortMode = _sortMode == SortMode.Alpha ? SortMode.Added : SortMode.Alpha;
        UpdateSortChecks();
        RebuildGroups();
    }

    private void SortAdded_Click(object? sender, RoutedEventArgs e)
    {
        _sortMode = SortMode.Added;
        UpdateSortChecks();
        RebuildGroups();
    }

    private DispatcherTimer? _testSweepTimer;
    private ConicGradientBrush? _testSweep;

    private ConicGradientBrush TestSweepBrush()
    {
        if (_testSweep is not null) return _testSweep;
        _testSweep = new ConicGradientBrush { Center = RelativePoint.Center, Angle = 293 };
        _testSweep.GradientStops.Add(new GradientStop(Color.Parse("#002E8EFF"), 0.0));
        _testSweep.GradientStops.Add(new GradientStop(Color.Parse("#FF2E8EFF"), 0.06));
        _testSweep.GradientStops.Add(new GradientStop(Color.Parse("#002E8EFF"), 0.28));
        _testSweep.GradientStops.Add(new GradientStop(Color.Parse("#002E8EFF"), 1.0));
        return _testSweep;
    }

    private void StartTestSweep()
    {
        if (TestRing is null) return;
        var brush = TestSweepBrush();
        if (!ReferenceEquals(TestRing.Background, brush)) TestRing.Background = brush;
        _testSweepTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _testSweepTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_testSweepTimer, timer))
            {
                timer.Stop();
                return;
            }
            brush.Angle = (brush.Angle + 4.0) % 360.0;
        };
        timer.Start();
    }

    private void StopTestSweep()
    {
        _testSweepTimer?.Stop();
        _testSweepTimer = null;
    }

    private static void AttachMenuPress(MenuItem? item)
    {
        if (item is null) return;
        item.RenderTransformOrigin = RelativePoint.Center;
        item.RenderTransform = TransformOperations.Parse("scale(1)");
        item.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };
        item.AddHandler(InputElement.PointerPressedEvent,
            (_, _) => item.RenderTransform = TransformOperations.Parse("scale(0.95)"),
            RoutingStrategies.Tunnel, true);
        item.AddHandler(InputElement.PointerReleasedEvent,
            (_, _) => item.RenderTransform = TransformOperations.Parse("scale(1)"),
            RoutingStrategies.Tunnel, true);
        item.PointerExited += (_, _) => item.RenderTransform = TransformOperations.Parse("scale(1)");
    }

    private void UpdateSortChecks()
    {
        if (SortAddedItem is not null) SortAddedItem.IsChecked = _sortMode == SortMode.Added;
        if (SortAlphaItem is not null) SortAlphaItem.IsChecked = _sortMode == SortMode.Alpha;
        if (SortFastestItem is not null) SortFastestItem.IsChecked = _sortMode == SortMode.Fastest;
    }

    private void ShareConfig_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        var cfg = _store.Configs.FirstOrDefault(c => c.Id == id);
        if (cfg is null || cfg.Locked) return;
        _exportConfigs.Clear();
        _exportConfigs.Add(cfg);
        OpenExportPage();
    }

    private static string ShareLink(ProxyConfig c)
    {
        string name = Uri.EscapeDataString(c.Name ?? "");
        string Q(string k, string v) => string.IsNullOrEmpty(v) ? "" : "&" + k + "=" + Uri.EscapeDataString(v);
        switch (c.Protocol)
        {
            case "vless":
            {
                var q = "encryption=" + Uri.EscapeDataString(string.IsNullOrEmpty(c.Encryption) ? "none" : c.Encryption)
                        + Q("security", c.Security) + Q("type", c.Network) + Q("sni", c.Sni)
                        + Q("fp", c.Fingerprint) + Q("pbk", c.PublicKey) + Q("sid", c.ShortId)
                        + Q("flow", c.Flow) + Q("host", c.Host) + Q("path", c.Path);
                return "vless://" + c.Uuid + "@" + c.Address + ":" + c.Port + "?" + q + "#" + name;
            }
            case "trojan":
            {
                var q = "security=" + Uri.EscapeDataString(string.IsNullOrEmpty(c.Security) ? "tls" : c.Security)
                        + Q("type", c.Network) + Q("sni", c.Sni) + Q("fp", c.Fingerprint)
                        + Q("pbk", c.PublicKey) + Q("sid", c.ShortId) + Q("flow", c.Flow)
                        + Q("host", c.Host) + Q("path", c.Path);
                return "trojan://" + Uri.EscapeDataString(c.Password) + "@" + c.Address + ":" + c.Port + "?" + q + "#" + name;
            }
            case "vmess":
            {
                var o = new Dictionary<string, string>
                {
                    ["v"] = "2",
                    ["ps"] = c.Name ?? "",
                    ["add"] = c.Address,
                    ["port"] = c.Port.ToString(),
                    ["id"] = c.Uuid,
                    ["aid"] = c.AlterId.ToString(),
                    ["scy"] = string.IsNullOrEmpty(c.Encryption) ? "auto" : c.Encryption,
                    ["net"] = c.Network,
                    ["type"] = "none",
                    ["host"] = c.Host,
                    ["path"] = c.Path,
                    ["tls"] = c.Security == "tls" ? "tls" : "",
                    ["sni"] = c.Sni
                };
                var json = JsonSerializer.Serialize(o);
                return "vmess://" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            }
            case "ss":
            case "shadowsocks":
            {
                var userinfo = Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Method + ":" + c.Password));
                return "ss://" + userinfo + "@" + c.Address + ":" + c.Port + "#" + name;
            }
            default:
                return "";
        }
    }

    private async void CopyConfig_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctl || ctl.Tag is not string id) return;
        e.Handled = true;
        var cfg = _store.Configs.FirstOrDefault(c => c.Id == id);
        if (cfg is null || cfg.Locked) return;
        string link = !string.IsNullOrEmpty(cfg.Raw) ? cfg.Raw : ShareLink(cfg);
        if (link.Length == 0) return;
        var clip = GetTopLevel(this)?.Clipboard;
        if (clip is not null) await clip.SetTextAsync(link);
        FlashCopied(ctl as Button);
        Append($"Copied {cfg.Name}.");
    }

    private async void DeleteGroupDead_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctl || ctl.Tag is not string id) return;
        var group = _groups.FirstOrDefault(g => g.Id == id);
        if (group is null) return;

        var dead = group.Configs.Where(c => c.Ping == -1).Select(c => c.Id).ToList();
        if (dead.Count == 0)
        {
            Append("No timed-out servers in this group.");
            return;
        }

        bool cleared = dead.Contains(_store.SelectedId ?? "");
        foreach (var cid in dead) _store.Delete(cid);
        if (cleared) _store.SelectedId = null;
        _store.SaveNow();
        RebuildGroups();
        RefreshSelected();
        await Ask("success", "Removed", $"{dead.Count} timed-out servers deleted.");
    }

    private async void DeleteGroupAll_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctl || ctl.Tag is not string id) return;
        var group = _groups.FirstOrDefault(g => g.Id == id);
        if (group is null) return;

        if (!await Ask("warning", "Delete all servers?",
                $"Every server in {group.Name} will be removed.", withCancel: true)) return;

        if (id == ManualId)
        {
            var ids = group.Configs.Select(c => c.Id).ToList();
            if (ids.Contains(_store.SelectedId ?? "")) _store.SelectedId = null;
            foreach (var cid in ids) _store.Delete(cid);
            _store.SaveNow();
        }
        else
        {
            _store.DeleteSubscription(id);
        }
        RebuildGroups();
        RefreshSelected();
    }

    private static readonly ITransform CardDown = TransformOperations.Parse("scale(0.985)");
    private static readonly ITransform CardUp = TransformOperations.Parse("scale(1)");

    private void CardRelease(object? sender, RoutedEventArgs e)
    {
        if (sender is Control c) c.RenderTransform = CardUp;
    }

    private readonly Dictionary<ItemsControl, DispatcherTimer> _revealTimers = new();
    private readonly Dictionary<ItemsControl, bool> _revealDir = new();
    private readonly Dictionary<ItemsControl, DateTime> _revealAt = new();
    private DateTime _lastRevealAt = DateTime.MinValue;
    private bool _lastRevealShow;
    private bool _revealActive;
    private bool _revealActiveShow;

    private void ConfigListLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not ItemsControl list) return;
        list.PropertyChanged -= ConfigListPropertyChanged;
        list.PropertyChanged += ConfigListPropertyChanged;
    }

    private void ConfigListPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.IsVisibleProperty) return;
        if (sender is not ItemsControl list || !list.IsVisible) return;
        list.Opacity = 0;
        QueueConfigReveal(list);
    }

    private void QueueConfigReveal(ItemsControl list)
    {
        Dispatcher.UIThread.Post(() => AnimateConfigRows(list, true, null), DispatcherPriority.Loaded);
    }

    private static List<Control> ConfigRowsOf(ItemsControl list)
    {
        var items = new List<Control>();
        foreach (var child in list.GetVisualChildren()) CollectRows(child, items);
        return items;
    }

    private void AnimateConfigRows(ItemsControl list, bool show, Action? done)
    {
        var items = ConfigRowsOf(list);
        if (items.Count == 0)
        {
            list.Opacity = 1;
            done?.Invoke();
            return;
        }
        if (_revealActive && _revealActiveShow == show)
        {
            list.Opacity = 1;
            return;
        }
        if (!_revealActive && _lastRevealShow == show
            && (DateTime.UtcNow - _lastRevealAt).TotalMilliseconds < 400)
        {
            list.Opacity = 1;
            return;
        }

        foreach (var kv in _revealTimers)
        {
            kv.Value.Stop();
            kv.Key.Height = double.NaN;
            kv.Key.Opacity = 1;
        }
        _revealTimers.Clear();
        _revealDir.Clear();

        const double slide = 26.0;
        double dur = show ? 130.0 : 180.0;
        double gap = show ? 22.0 : 0.0;
        double total = dur + gap * (items.Count - 1);
        double heightDur = show ? 240.0 : 180.0;

        double full = list.Bounds.Height;
        if (full <= 0) full = list.DesiredSize.Height;

        foreach (var c in items)
        {
            c.Opacity = show ? 0 : 1;
            c.RenderTransform = show ? new TranslateTransform(-slide, 0) : null;
        }
        list.Opacity = 1;

        _surfaceDriven = false;
        list.Height = show ? full : 0;
        AnimateWindowHeight(heightDur);
        _surfaceDriven = true;

        list.Height = show ? 0 : full;

        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _revealTimers[list] = timer;
        _revealDir[list] = show;
        _revealAt[list] = DateTime.UtcNow;
        _lastRevealAt = DateTime.UtcNow;
        _lastRevealShow = show;
        _revealActive = true;
        _revealActiveShow = show;
        timer.Tick += (_, _) =>
        {
            if (!_revealTimers.TryGetValue(list, out var cur) || !ReferenceEquals(cur, timer))
            {
                timer.Stop();
                _surfaceDriven = false;
                list.Height = double.NaN;
                return;
            }
            double ms = (DateTime.UtcNow - t0).TotalMilliseconds;

            double hp = Math.Clamp(ms / heightDur, 0, 1);
            double he = 1 - Math.Pow(1 - hp, 3);
            list.Height = show ? full * he : full * (1 - he);

            for (int i = 0; i < items.Count; i++)
            {
                double p = Math.Clamp((ms - i * gap) / dur, 0, 1);
                double e2 = 1 - Math.Pow(1 - p, 3);
                double k = show ? e2 : 1 - e2;
                items[i].Opacity = k;
                items[i].RenderTransform = k >= 0.999 ? null : new TranslateTransform(-slide * (1 - k), 0);
            }

            if (ms < total) return;
            timer.Stop();
            _revealTimers.Remove(list);
            _revealDir.Remove(list);
            _revealActive = false;
            _lastRevealAt = DateTime.UtcNow;
            list.Height = double.NaN;
            list.Opacity = 1;
            foreach (var c in items)
            {
                c.Opacity = show ? 1 : 0;
                if (show) c.RenderTransform = null;
            }
            _surfaceDriven = false;
            done?.Invoke();
            QueueWindowHeight();
        };
        timer.Start();
    }

    private static ItemsControl? FindGroupList(object? header)
    {
        if (header is not Visual v) return null;
        var parent = v.GetVisualParent();
        while (parent is not null)
        {
            if (parent is Panel or ContentControl)
            {
                var found = new List<ItemsControl>();
                FindLists(parent, found);
                if (found.Count > 0) return found[0];
            }
            parent = parent.GetVisualParent();
            if (parent is ScrollViewer) break;
        }
        return null;
    }

    private static void FindLists(Visual v, List<ItemsControl> into)
    {
        foreach (var child in v.GetVisualChildren())
        {
            if (child is ItemsControl ic && ConfigRowsOf(ic).Count > 0)
            {
                into.Add(ic);
                return;
            }
            FindLists(child, into);
            if (into.Count > 0) return;
        }
    }

    private static void CollectRows(Visual v, List<Control> into)
    {
        if (v is Border b && b.Classes.Contains("glassRow"))
        {
            into.Add(b);
            return;
        }
        foreach (var child in v.GetVisualChildren()) CollectRows(child, into);
    }

    private void ToggleGroup(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control card) card.RenderTransform = CardDown;
        if (sender is not Control c || c.Tag is not string id) return;
        var g = _groups.FirstOrDefault(x => x.Id == id);
        if (g is null || g.IsManual) return;

        if (g.IsExpanded && FindGroupList(sender) is ItemsControl list)
        {
            AnimateConfigRows(list, false, () =>
            {
                g.IsExpanded = false;
                _expandedIds.Remove(id);
            });
            return;
        }

        g.IsExpanded = !g.IsExpanded;
        if (g.IsExpanded) _expandedIds.Add(id); else _expandedIds.Remove(id);
    }

    private static bool OverButton(object? source)
    {
        var v = source as Visual;
        while (v is not null)
        {
            if (v is Button or RepeatButton or MenuItem) return true;
            if (v is Border b && b.Name == "CfgBox") return false;
            v = v.GetVisualParent();
        }
        return false;
    }

    private void SelectConfig(object? sender, PointerPressedEventArgs e)
    {
        ClearConnectError();

        if (OverButton(e.Source)) return;
        if (!e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed) return;
        if (sender is Control card) card.RenderTransform = CardDown;
        if (sender is not Control c || c.Tag is not string id) return;
        e.Handled = true;

        _pressPointer = e.Pointer;
        if (_selectMode)
        {
            var cfg = _store.Configs.FirstOrDefault(x => x.Id == id);
            if (cfg is not null)
            {
                _rowDragging = true;
                _rowDragValue = !cfg.IsChecked;
                cfg.IsChecked = _rowDragValue;
                UpdateSelectionBar();
                e.Pointer.Capture(GroupScroll);
            }
            return;
        }

        _holdId = id;
        _holdTimer?.Stop();
        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _holdTimer.Tick += HoldElapsed;
        _holdTimer.Start();
    }

    private void Row_Released(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control card) card.RenderTransform = CardUp;
        if (OverButton(e.Source)) return;
        bool held = _holdTimer is null;
        _holdTimer?.Stop();
        _holdTimer = null;
        if (_selectMode || held) { _holdId = null; return; }

        if (sender is not Control c || c.Tag is not string id) return;
        _holdId = null;
        _store.SelectedId = id;
        _store.SaveNow();
        RefreshSelected();
        RebuildGroups();
        ShowTab("connect");
    }

    private void HoldElapsed(object? sender, EventArgs e)
    {
        _holdTimer?.Stop();
        _holdTimer = null;
        if (_holdId is null) return;

        EnterSelectMode();
        var cfg = _store.Configs.FirstOrDefault(c => c.Id == _holdId);
        if (cfg is not null)
        {
            cfg.IsChecked = true;
            _rowDragging = true;
            _rowDragValue = true;
        }
        _holdId = null;
        UpdateSelectionBar();
        _pressPointer?.Capture(GroupScroll);
    }

    private void EnterSelectMode()
    {
        if (_selectMode) return;
        _selectMode = true;
        SetVis(SelectionBar, true);
        UpdateSelectionBar();
    }

    private void ExitSelectMode()
    {
        _selectMode = false;
        _rowDragging = false;
        _holdTimer?.Stop();
        _holdTimer = null;
        _holdId = null;
        StopDragScroll();
        _pressPointer?.Capture(null);
        _pressPointer = null;
        foreach (var c in _store.Configs) c.IsChecked = false;
        SetVis(SelectionBar, false);
    }

    private void UpdateSelectionBar()
    {
        if (SelectionCount is null) return;
        int n = _store.Configs.Count(c => c.IsChecked);
        SelectionCount.Text = string.Format(Strings.Get(_lang, "selected_count"), n);
    }

    private List<ProxyConfig> CheckedConfigs() => _store.Configs.Where(c => c.IsChecked).ToList();

    private void GroupScroll_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_selectMode || !_rowDragging) return;
        var pt = e.GetPosition(GroupScroll);
        _lastDragPos = pt;
        HitTestRowAt(pt);
        UpdateDragScroll(pt);
    }

    private void GroupScroll_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDrag();
        if (_selectMode && _store.Configs.All(c => !c.IsChecked)) ExitSelectMode();
    }

    private void EndDrag()
    {
        _holdTimer?.Stop();
        _holdTimer = null;
        _holdId = null;
        if (_rowDragging)
        {
            _rowDragging = false;
            StopDragScroll();
        }
        _pressPointer?.Capture(null);
        _pressPointer = null;
    }

    private void HitTestRowAt(Point pt)
    {
        if (GroupScroll.InputHitTest(pt) is not Visual hit) return;
        Visual? d = hit;
        while (d is not null)
        {
            if (d is Control fe && fe.Name == "CfgBox" && fe.Tag is string id)
            {
                var cfg = _store.Configs.FirstOrDefault(c => c.Id == id);
                if (cfg is not null && cfg.IsChecked != _rowDragValue)
                {
                    cfg.IsChecked = _rowDragValue;
                    UpdateSelectionBar();
                }
                return;
            }
            d = d.GetVisualParent();
        }
    }

    private void UpdateDragScroll(Point pos)
    {
        if (!_rowDragging) { StopDragScroll(); return; }
        double h = GroupScroll.Bounds.Height;
        const double zone = 48.0;
        const double maxSpeed = 22.0;
        if (pos.Y < zone)
        {
            double t = Math.Max(0.0, Math.Min(1.0, (zone - pos.Y) / zone));
            _dragScrollSpeed = -maxSpeed * t;
            StartDragScroll();
        }
        else if (pos.Y > h - zone)
        {
            double t = Math.Max(0.0, Math.Min(1.0, (pos.Y - (h - zone)) / zone));
            _dragScrollSpeed = maxSpeed * t;
            StartDragScroll();
        }
        else
        {
            StopDragScroll();
        }
    }

    private void StartDragScroll()
    {
        if (_dragScrollTimer is not null) return;
        _dragScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dragScrollTimer.Tick += DragScrollTick;
        _dragScrollTimer.Start();
    }

    private void StopDragScroll()
    {
        _dragScrollTimer?.Stop();
        _dragScrollTimer = null;
        _dragScrollSpeed = 0;
    }

    private void DragScrollTick(object? sender, EventArgs e)
    {
        if (!_rowDragging || _dragScrollSpeed == 0)
        {
            StopDragScroll();
            return;
        }
        double max = Math.Max(0, GroupScroll.Extent.Height - GroupScroll.Viewport.Height);
        double target = Math.Max(0, Math.Min(max, GroupScroll.Offset.Y + _dragScrollSpeed));
        GroupScroll.Offset = new Vector(GroupScroll.Offset.X, target);
        HitTestRowAt(_lastDragPos);
        double h = GroupScroll.Bounds.Height;
        const double zone = 48.0;
        if (_lastDragPos.Y >= zone && _lastDragPos.Y <= h - zone) StopDragScroll();
    }

    private void SelectionCancel_Click(object? sender, RoutedEventArgs e) => ExitSelectMode();

    private void SelectionShare_Click(object? sender, RoutedEventArgs e)
    {
        var list = CheckedConfigs();
        if (list.Count == 0)
        {
            ExitSelectMode();
            return;
        }
        _exportConfigs.Clear();
        _exportConfigs.AddRange(list);
        ExitSelectMode();
        OpenExportPage();
    }

    private async void SelectionDelete_Click(object? sender, RoutedEventArgs e)
    {
        var list = CheckedConfigs();
        if (list.Count == 0)
        {
            ExitSelectMode();
            return;
        }
        if (!await Ask("warning", "Delete selected?",
                $"{list.Count} servers will be removed.", withCancel: true)) return;

        var ids = list.Select(x => x.Id).ToHashSet();
        var rows = GroupList is null
            ? new List<Control>()
            : GroupList.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Name == "CfgBox" && b.Tag is string t && ids.Contains(t))
                .Cast<Control>().ToList();
        if (rows.Count > 0)
            await Task.WhenAll(rows.Select(SlideOutRow));

        foreach (var c in list) _store.Delete(c.Id);
        _store.SaveNow();
        ExitSelectMode();
        RebuildGroups();
        RefreshSelected();
    }

    private async void PingSub_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string id) return;
        e.Handled = true;
        if (!DelayTester.XrayAvailable)
        {
            await Ask("warning", "xray not found", "Place the xray binary in the libs folder to test servers.");
            return;
        }
        var list = _store.Configs.Where(x => x.SubId == id).ToList();
        if (list.Count == 0) return;
        if (!_subBusy.Add(id)) return;

        foreach (var cfg in list)
        {
            cfg.Ping = -2;
            cfg.PingText = "...";
        }

        try
        {
            using var sem = new SemaphoreSlim(8);
            var tasks = list.Select(async cfg =>
            {
                await sem.WaitAsync();
                try
                {
                    int ms = await DelayTester.Measure(cfg);
                    cfg.Ping = ms;
                    cfg.PingText = ms > 0 ? ms + " ms" : "timeout";
                }
                finally
                {
                    sem.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
        }
        finally
        {
            _subBusy.Remove(id);
        }

        _store.SaveNow();
    }

    private void RenameSub_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string id) return;
        e.Handled = true;
        if (_renameActive) return;
        var sub = _store.Subscriptions.FirstOrDefault(x => x.Id == id);
        if (sub is null) return;
        var row = FindAncestorNamed(c, "HeaderBox");
        if (row is null) return;
        StartRename(FindDescendantNamed<TextBlock>(row, "SubName"),
            FindDescendantNamed<TextBox>(row, "SubNameEdit"), "sub", id, sub.Name);
    }

    private void AnimateRenameWidth(TextBox edit, double to, double ms, bool easeOut, Action? done)
    {
        int gen = ++_renameAnimGen;
        double from = edit.Width;
        if (double.IsNaN(from)) from = edit.Bounds.Width;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            if (gen != _renameAnimGen)
            {
                timer.Stop();
                return;
            }
            double p = Math.Min(1.0, (DateTime.UtcNow - t0).TotalMilliseconds / ms);
            double e = easeOut ? 1 - Math.Pow(1 - p, 3) : p * p * p;
            edit.Width = from + (to - from) * e;
            if (p < 1) return;
            timer.Stop();
            edit.Width = to;
            done?.Invoke();
        };
        timer.Start();
    }

    private static double RenameTargetWidth(TextBlock name)
    {
        double w = name.Bounds.Width;
        if (w <= 0) w = name.DesiredSize.Width;
        return Math.Max(120, Math.Min(240, w + 46));
    }

    private void StartRename(TextBlock? name, TextBox? edit, string kind, string id, string? current)
    {
        if (name is null || edit is null) return;
        if (_renameActive) return;
        edit.Text = current ?? "";
        edit.Tag = kind + "|" + id;

        double target = RenameTargetWidth(name);
        name.IsVisible = false;
        name.Opacity = 1;
        edit.Opacity = 1;
        edit.RenderTransform = null;
        edit.Width = 0;
        edit.IsVisible = true;
        _renameActive = true;
        edit.Focus();
        edit.SelectAll();
        AnimateRenameWidth(edit, target, 200, easeOut: true, done: null);
    }

    private void RenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox edit) return;
        if (e.Key == Key.Enter) { e.Handled = true; FinishRename(edit, true); }
        else if (e.Key == Key.Escape) { e.Handled = true; FinishRename(edit, false); }
    }

    private void RenameCommit(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox edit) FinishRename(edit, true);
    }

    private void HideRenameBox(TextBox edit, TextBlock? name)
    {
        _renameAnimGen++;
        edit.IsVisible = false;
        edit.Width = double.NaN;
        if (name is not null) name.IsVisible = true;
    }

    private void FinishRename(TextBox edit, bool commit)
    {
        if (!_renameActive) return;
        _renameActive = false;
        var row = FindAncestorNamed(edit, "HeaderBox") ?? FindAncestorNamed(edit, "CfgBox");
        var label = row is null ? null : FindDescendantNamed<TextBlock>(row, "SubName");
        HideRenameBox(edit, label);
        if (commit && edit.Tag is string tag)
        {
            var parts = tag.Split('|', 2);
            var id = parts.Length > 1 ? parts[1] : "";
            var newName = (edit.Text ?? "").Trim();
            if (newName.Length > 0)
            {
                if (parts[0] == "cfg")
                {
                    var cfg = _store.Configs.FirstOrDefault(x => x.Id == id);
                    if (cfg is not null) cfg.Name = newName;
                }
                else
                {
                    var s = _store.Subscriptions.FirstOrDefault(x => x.Id == id);
                    if (s is not null) s.Name = newName;
                    var vm = _groups.FirstOrDefault(x => x.Id == id);
                    if (vm is not null) vm.Name = newName;
                }
                _store.SaveNow();
            }
        }
        DispatcherTimer.RunOnce(RebuildGroups, TimeSpan.FromMilliseconds(60));
    }

    private async Task SlideOutRow(Control? row)
    {
        if (row is null || !row.IsVisible) return;
        row.ZIndex = 1;

        double h = row.Bounds.Height;
        if (h <= 0) h = row.DesiredSize.Height;
        if (h <= 0) { row.IsVisible = false; return; }

        var tcs = new TaskCompletionSource();
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += (_, _) =>
        {
            double p = Math.Min(1.0, (DateTime.UtcNow - t0).TotalMilliseconds / 190.0);
            double e = 1 - Math.Pow(1 - p, 3);
            row.Opacity = 1 - e;
            row.RenderTransform = new TranslateTransform(-34.0 * e, 0);
            row.Height = h * (1 - e);
            if (p < 1) return;
            timer.Stop();
            row.IsVisible = false;
            row.Opacity = 1;
            row.RenderTransform = null;
            row.Height = double.NaN;
            tcs.TrySetResult();
        };
        timer.Start();
        await tcs.Task;
    }

    private static Control? FindAncestorNamed(Control start, string name)
    {
        Visual? d = start;
        while (d is not null)
        {
            if (d is Control c && c.Name == name) return c;
            d = d.GetVisualParent();
        }
        return null;
    }

    private static T? FindDescendantNamed<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.Name == name);

    private void NameClip_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Grid clip) StartMarquee(clip);
    }

    private void NameText_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is TextBlock tb && tb.GetVisualParent() is Canvas cv && cv.GetVisualParent() is Grid clip)
            StartMarquee(clip);
    }

    private void StartMarquee(Grid clip)
    {
        if (clip.Children.Count == 0 || clip.Children[0] is not Canvas cv) return;
        if (cv.RenderTransform is not TranslateTransform shift) return;
        if (cv.Children.Count < 2 || cv.Children[0] is not TextBlock tb || cv.Children[1] is not TextBlock tb2) return;

        if (_marqueeTimers.TryGetValue(clip, out var old))
        {
            old.Stop();
            _marqueeTimers.Remove(clip);
        }

        double avail = clip.Bounds.Width;
        if (avail <= 1) return;
        double textW = tb.Bounds.Width;
        if (textW <= 1)
        {
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            textW = tb.DesiredSize.Width;
        }

        if (textW - avail <= 2)
        {
            tb2.IsVisible = false;
            shift.X = 0;
            return;
        }

        double cycle = textW + 48;
        Canvas.SetLeft(tb2, cycle);
        tb2.IsVisible = true;
        double secs = cycle / 50.0;
        double total = 1.2 + secs;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _marqueeTimers[clip] = timer;
        timer.Tick += (_, _) =>
        {
            if (clip.GetVisualRoot() is null)
            {
                timer.Stop();
                _marqueeTimers.Remove(clip);
                return;
            }
            double t = (DateTime.UtcNow - t0).TotalSeconds % total;
            shift.X = t < 1.2 ? 0 : -cycle * ((t - 1.2) / secs);
        };
        timer.Start();
    }

    private async void CopySub_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string id) return;
        e.Handled = true;
        var sub = _store.Subscriptions.FirstOrDefault(x => x.Id == id);
        if (sub is null) return;
        var clip = GetTopLevel(this)?.Clipboard;
        if (clip is not null) await clip.SetTextAsync(sub.Url);
        FlashCopied(c as Button);
        Append("Subscription link copied.");
    }

    private void GoBack()
    {
        string t = _tabHistory.Count > 0 ? _tabHistory.Pop() : "connect";
        _tabBack = true;
        ShowTab(t);
    }

    private void ClosePicker_Click(object? sender, RoutedEventArgs e) => GoBack();

    private int _searchAnimGen;
    private int _renameAnimGen;

    private void AnimateSearchWidth(double to, double ms, bool easeOut, Action? done)
    {
        int gen = ++_searchAnimGen;
        double from = SearchWrap.Width;
        if (double.IsNaN(from)) from = SearchWrap.Bounds.Width;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            if (gen != _searchAnimGen)
            {
                timer.Stop();
                return;
            }
            double p = Math.Min(1.0, (DateTime.UtcNow - t0).TotalMilliseconds / ms);
            double e = easeOut ? 1 - Math.Pow(1 - p, 3) : p * p * p;
            SearchWrap.Width = from + (to - from) * e;
            if (p >= 1)
            {
                timer.Stop();
                SearchWrap.Width = to;
                done?.Invoke();
            }
        };
        timer.Start();
    }

    private void OpenSearch_Click(object? sender, RoutedEventArgs e)
    {
        SearchToggle.IsVisible = false;
        SearchWrap.IsVisible = true;
        AnimateSearchWidth(200, 200, easeOut: true, done: null);
        SearchInput.Focus();
    }

    private void CloseSearch_Click(object? sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        AnimateSearchWidth(0, 180, easeOut: false, done: () =>
        {
            SearchWrap.IsVisible = false;
            SearchToggle.IsVisible = true;
        });
    }

    private void SearchClear_Enter(object? sender, PointerEventArgs e)
    {
        if (SearchClearIcon is not null) SearchClearIcon.Stroke = new SolidColorBrush(Color.Parse("#E0575C"));
    }

    private void SearchClear_Exit(object? sender, PointerEventArgs e)
    {
        if (SearchClearIcon is not null) SearchClearIcon.Stroke = new SolidColorBrush(Color.Parse("#7E93B8"));
    }

    private void ToggleAddMenu_Click(object? sender, RoutedEventArgs e) =>
        SetAddMenu(!AddMenu.IsVisible);

    private DispatcherTimer? _addMenuTimer;
    private double _addMenuH;

    private void SetAddMenu(bool open)
    {
        if (AddMenu is null) return;

        if (AddIcon is not null)
            AddIcon.RenderTransform = TransformOperations.Parse(open ? "rotate(180deg)" : "rotate(0deg)");
        if (AddBarV is not null)
        {
            AddBarV.Opacity = open ? 0 : 1;
            AddBarV.RenderTransform = TransformOperations.Parse(open ? "scaleY(0.1)" : "scaleY(1)");
        }

        if (open == AddMenu.IsVisible && _addMenuTimer is null) return;

        _addMenuTimer?.Stop();
        _addMenuTimer = null;

        if (open)
        {
            AddMenu.IsVisible = true;
            AddMenu.Height = double.NaN;
            AddMenu.Measure(new Size(AppSurface.Bounds.Width - 32, double.PositiveInfinity));
            _addMenuH = AddMenu.DesiredSize.Height;
            if (_addMenuH <= 0) _addMenuH = 108;
        }
        else if (_addMenuH <= 0)
        {
            _addMenuH = AddMenu.Bounds.Height;
        }

        double from = open ? 0 : (double.IsNaN(AddMenu.Height) ? AddMenu.Bounds.Height : AddMenu.Height);
        double to = open ? _addMenuH : 0;
        AddMenu.Height = from;
        AddMenu.Opacity = open ? 0 : 1;

        bool syncSurface = _tab == "picker";
        double surfAtZero = 0;
        if (syncSurface)
        {
            _heightTimer?.Stop();
            _heightTimer = null;
            _surfaceDriven = true;
            double surf = double.IsNaN(AppSurface.Height) ? AppSurface.Bounds.Height : AppSurface.Height;
            surfAtZero = surf - from;
        }

        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _addMenuTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_addMenuTimer, timer))
            {
                timer.Stop();
                if (syncSurface) _surfaceDriven = false;
                return;
            }
            double p = Math.Min(1.0, (DateTime.UtcNow - t0).TotalMilliseconds / 210.0);
            double e = open ? 1 - Math.Pow(1 - p, 3) : p * p * p;
            double mh = from + (to - from) * e;
            AddMenu.Height = mh;
            AddMenu.Opacity = open ? e : 1 - e;
            if (syncSurface)
            {
                double sh = Math.Clamp(surfAtZero + mh, 320.0, 700.0);
                AppSurface.Height = sh;
                SyncWindowHeight(sh);
            }
            if (p < 1) return;

            timer.Stop();
            _addMenuTimer = null;
            _surfaceDriven = false;
            if (open)
            {
                AddMenu.Height = double.NaN;
                AddMenu.Opacity = 1;
            }
            else
            {
                AddMenu.IsVisible = false;
                AddMenu.Height = double.NaN;
                AddMenu.Opacity = 1;
            }
        };
        timer.Start();
    }

    private void CloseManual_Click(object? sender, RoutedEventArgs e) => GoBack();

    private async void PasteAdd_Click(object? sender, RoutedEventArgs e)
    {
        SetAddMenu(false);
        var clip = GetTopLevel(this)?.Clipboard;
        if (clip is null) return;
#pragma warning disable CS0618
        string text = await clip.GetTextAsync() ?? "";
#pragma warning restore CS0618
        int added = 0;
        foreach (var raw in text.Split(new[] { '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var cfg = ConfigParser.Parse(raw.Trim());
            if (cfg is null) continue;
            _store.Add(cfg);
            added++;
        }
        if (added > 0)
        {
            _store.SaveNow();
            ReloadServers();
            Append($"Added {added} server(s) from clipboard.");
            return;
        }

        string one = text.Trim();
        if (one.Length > 0 && one.IndexOfAny(new[] { '\r', '\n', ' ' }) < 0 &&
            (one.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             one.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            await AddSubscription(one, null);
            return;
        }

        Append("No valid share links found in the clipboard.");
        await Ask("warning", Strings.Get(_lang, "err_title"),
            Strings.Get(_lang, "err_clipboard"));
    }

    private static void SelectCombo(ComboBox? box, string? value)
    {
        if (box is null) return;
        foreach (var obj in box.Items)
        {
            if (obj is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private static string ComboText(ComboBox? box) =>
        (box?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

    private async void DeleteServer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctl || ctl.Tag is not string id) return;
        e.Handled = true;
        _expandedIds.Add(_store.Configs.FirstOrDefault(x => x.Id == id)?.SubId ?? ManualId);
        await SlideOutRow(FindAncestorNamed(ctl, "CfgBox"));
        _store.Delete(id);
        _store.SaveNow();
        RebuildGroups();
        RefreshSelected();
    }

    private async void TestAll_Click(object? sender, RoutedEventArgs e)
    {
        if (!DelayTester.XrayAvailable)
        {
            Append("xray not found — cannot test.");
            return;
        }
        StartTestSweep();
        var list = _store.Configs.ToList();
        if (list.Count == 0)
        {
            StopTestSweep();
            Append("No servers to test.");
            return;
        }
        foreach (var c in list)
        {
            c.Ping = -2;
            c.PingText = "...";
        }

        using var sem = new SemaphoreSlim(8);
        var tasks = list.Select(async c =>
        {
            await sem.WaitAsync();
            try
            {
                int ms = await DelayTester.Measure(c);
                c.Ping = ms >= 0 ? ms : -1;
                c.PingText = ms >= 0 ? ms + " ms" : "fail";
            }
            finally
            {
                sem.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);
        _store.SaveNow();
        StopTestSweep();
        Append("Ping test finished.");
    }

    private void Nav_Connect(object? sender, RoutedEventArgs e) => ShowTab("connect");

    private void Nav_Logs(object? sender, RoutedEventArgs e) => ShowTab("logs");

    private void Nav_Tools(object? sender, RoutedEventArgs e) => ShowTab("tools");

    private async Task<bool> Ask(string kind, string title, string message,
        bool withCancel = false, bool withPassword = false)
    {
        if (DialogOverlay is null) return false;

        var (color, path) = kind switch
        {
            "success" => (Color.FromRgb(0x3C, 0xCB, 0x7F), "M3.5,9.5 L7.5,13.5 L14.5,4.5"),
            "warning" => (Color.FromRgb(0xE5, 0xA6, 0x3C), "M9,2.5 L16,15.5 H2 Z M9,7.2 V10.4 M9,12.9 h0.01"),
            "error" => (Color.FromRgb(0xE0, 0x57, 0x5C), "M4,4 L14,14 M14,4 L4,14"),
            "admin" => (Color.FromRgb(0xE5, 0xA6, 0x3C),
                        "M9,2 L15,4.4 V9.2 C15,12.6 12.5,15.3 9,16 C5.5,15.3 3,12.6 3,9.2 V4.4 Z "
                        + "M9,6.6 V9.8 M9,12.1 h0.01"),
            _ => (Color.FromRgb(0x6E, 0x9C, 0xF0),
                  "M9,2 A7,7 0 1 0 9,16 A7,7 0 1 0 9,2 Z M9,8 V12.8 M9,5.4 h0.01")
        };

        DlgIcon.Stroke = new SolidColorBrush(color);
        DlgIcon.Data = Geometry.Parse(path);
        DlgIconBg.Fill = new SolidColorBrush(Color.FromArgb(0x2E, color.R, color.G, color.B));
        DlgTitle.Text = title;
        DlgMessage.Text = message;
        DlgMessage.IsVisible = message.Length > 0;
        DlgPassword.IsVisible = withPassword;
        DlgPassword.Text = "";
        DlgCancel.IsVisible = withCancel || withPassword;

        _dlgTcs = new TaskCompletionSource<bool>();
        DialogOverlay.IsVisible = true;
        AnimatePromptIn(DialogOverlay, DlgCard);
        if (withPassword) DlgPassword.Focus();
        bool ok = await _dlgTcs.Task;
        AnimatePromptOut(DialogOverlay, DlgCard);
        return ok;
    }

    private void DlgOk_Click(object? sender, RoutedEventArgs e) => _dlgTcs?.TrySetResult(true);

    private void DlgCancel_Click(object? sender, RoutedEventArgs e) => _dlgTcs?.TrySetResult(false);

    private void ReloadSubs()
    {
        _subs.Clear();
        foreach (var sub in _store.Subscriptions) _subs.Add(sub);
    }

    private void OpenSubs_Click(object? sender, RoutedEventArgs e)
    {
        SetAddMenu(false);
        ReloadSubs();
        ShowTab("subs");
    }

    private void CloseSubs_Click(object? sender, RoutedEventArgs e) => GoBack();

    private async void AddSub_Click(object? sender, RoutedEventArgs e)
    {
        if (SubUrl is null)
        {
            Append("Subscription box is not available on this screen.");
            return;
        }

        string url = (SubUrl.Text ?? "").Trim();
        if (url.Length == 0)
        {
            await Ask("warning", Strings.Get(_lang, "err_title"),
                Strings.Get(_lang, "err_clipboard"));
            return;
        }

        if (await AddSubscription(url, SubNameBox?.Text))
        {
            SubUrl.Text = "";
            if (SubNameBox is not null) SubNameBox.Text = "";
        }
    }

    private string FriendlyError(Exception ex)
    {
        var e = ex.GetBaseException();
        if (e is TaskCanceledException or TimeoutException)
            return Strings.Get(_lang, "err_timeout");
        if (e is System.Net.Sockets.SocketException or System.Net.Http.HttpRequestException)
            return Strings.Get(_lang, "err_network");
        if (e is FormatException or System.Text.Json.JsonException or UriFormatException)
            return Strings.Get(_lang, "err_parse");
        return e.Message;
    }

    private async Task<bool> AddSubscription(string url, string? name)
    {
        try
        {
            Append($"Fetching subscription {HostOf(url)} \u2026");
            var result = await SubscriptionFetcher.FetchFull(url);
            if (result.Configs.Count == 0)
            {
                await Ask("warning", Strings.Get(_lang, "err_title"),
                    Strings.Get(_lang, "err_empty_sub"));
                return false;
            }

            var sub = new Subscription
            {
                Name = string.IsNullOrWhiteSpace(name) ? HostOf(url) : name!.Trim(),
                Url = url
            };
            if (result.UserInfo is not null)
            {
                sub.Used = result.UserInfo.Used;
                sub.Total = result.UserInfo.Total;
                sub.Expire = result.UserInfo.Expire;
            }

            _store.UpsertSubscription(sub, result.Configs);
            ReloadSubs();
            ReloadServers();
            await Ask("success", "Subscription added", $"{result.Configs.Count} servers imported.");
            return true;
        }
        catch (Exception ex)
        {
            Append("Subscription failed: " + ex.GetBaseException().Message);
            await Ask("error", Strings.Get(_lang, "err_title"), FriendlyError(ex));
            return false;
        }
    }

    private static string HostOf(string url)
    {
        try { return new Uri(url).Host; } catch { return "Subscription"; }
    }

    private readonly HashSet<string> _subBusy = new();

    private async void RefreshSub_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctl || ctl.Tag is not string id) return;
        var sub = _store.Subscriptions.FirstOrDefault(x => x.Id == id);
        if (sub is null) return;
        if (!_subBusy.Add(id)) return;

        try
        {
            var result = await SubscriptionFetcher.FetchFull(sub.Url);
            if (result.UserInfo is not null)
            {
                sub.Used = result.UserInfo.Used;
                sub.Total = result.UserInfo.Total;
                sub.Expire = result.UserInfo.Expire;
            }
            _store.UpsertSubscription(sub, result.Configs);
            ReloadSubs();
            ReloadServers();
            Append($"{sub.Name}: {result.Configs.Count} servers.");
        }
        catch (Exception ex)
        {
            await Ask("error", "Refresh failed", ex.Message);
        }
        finally
        {
            _subBusy.Remove(id);
        }
    }

    private async void DeleteSub_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctl || ctl.Tag is not string id) return;
        var sub = _store.Subscriptions.FirstOrDefault(x => x.Id == id);
        if (sub is null) return;
        if (!await Ask("warning", "Delete subscription?",
                $"{sub.Name} and its servers will be removed.", withCancel: true)) return;
        await SlideOutRow(FindAncestorNamed(ctl, "SubRow"));
        _store.DeleteSubscription(id);
        ReloadSubs();
        ReloadServers();
        RefreshSelected();
    }

    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        SetAddMenu(false);
        if (_store.Configs.Count == 0)
        {
            _ = Ask("warning", "Nothing to export", "Add a server first.");
            return;
        }
        _exportConfigs.Clear();
        _exportConfigs.AddRange(_store.Configs);
        OpenExportPage();
    }

    private void OpenExportPage()
    {
        ExportFileName.Text = _exportConfigs.Count == 1
            ? ConfigFile.Sanitize(_exportConfigs[0].Name)
            : "GRoute-configs";
        ExportPwBox.Text = "";
        SetExportPwVisible(false);
        ExportPwBox.PasswordChar = '*';
        ExportCountText.Text = $"{_exportConfigs.Count} servers";
        SetVis(ExportCountText, _exportConfigs.Count > 1);
        ShowTab("export");
    }

    private const string EyeOpenPath =
        "M1.2 8 C3.2 4.2 5.5 2.6 8 2.6 C10.5 2.6 12.8 4.2 14.8 8 C12.8 11.8 10.5 13.4 8 13.4 C5.5 13.4 3.2 11.8 1.2 8 Z M8 5.7 A2.3 2.3 0 1 0 8 10.3 A2.3 2.3 0 0 0 8 5.7 Z";

    private const string EyeShutPath =
        "M1.2 8 C3.2 4.2 5.5 2.6 8 2.6 C10.5 2.6 12.8 4.2 14.8 8 C12.8 11.8 10.5 13.4 8 13.4 C5.5 13.4 3.2 11.8 1.2 8 Z M8 5.7 A2.3 2.3 0 1 0 8 10.3 A2.3 2.3 0 0 0 8 5.7 Z M2.6 2.6 L13.4 13.4";

    private bool _exportPwVisible;

    private void SetExportPwVisible(bool visible)
    {
        _exportPwVisible = visible;
        if (ExportPwBox is not null) ExportPwBox.PasswordChar = visible ? '\0' : '*';
        if (ExportPwEye is not null)
            ExportPwEye.Data = Geometry.Parse(visible ? EyeShutPath : EyeOpenPath);
    }

    private void ExportShowPw_Click(object? sender, RoutedEventArgs e)
    {
        SetExportPwVisible(!_exportPwVisible);
    }

    private void ExportCancel_Click(object? sender, RoutedEventArgs e)
    {
        ExportPwBox.Text = "";
        GoBack();
    }

    private async void ExportContinue_Click(object? sender, RoutedEventArgs e)
    {
        if (_exportConfigs.Count == 0)
        {
            GoBack();
            return;
        }
        string name = ConfigFile.Sanitize(ExportFileName.Text ?? "");
        if (name.Length == 0) name = "GRoute-configs";

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export configs",
            SuggestedFileName = name + ".grt",
            DefaultExtension = "grt"
        });
        if (file is null) return;

        try
        {
            string? pw = string.IsNullOrEmpty(ExportPwBox.Text) ? null : ExportPwBox.Text;
            byte[] data = ConfigFile.Encode(_exportConfigs, pw, ExportLock.IsChecked == true);
            await using (var stream = await file.OpenWriteAsync())
                await stream.WriteAsync(data);
            Append($"Exported {_exportConfigs.Count} server{(_exportConfigs.Count == 1 ? "" : "s")} to {file.Name}.");
            ExportPwBox.Text = "";
            GoBack();
        }
        catch (Exception ex)
        {
            await Ask("error", "Export failed", ex.Message);
        }
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        SetAddMenu(false);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import servers",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("GRoute / WireGuard")
                {
                    Patterns = new[] { "*.grt", "*.conf" }
                },
                FilePickerFileTypes.All
            }
        });
        if (files.Count == 0) return;

        try
        {
            byte[] data;
            await using (var stream = await files[0].OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                data = ms.ToArray();
            }

            if (WireGuardConf.LooksLikeConf(data))
            {
                await ImportWireGuard(files);
                return;
            }

            if (ConfigFile.IsPasswordProtected(data))
            {
                _pendingImport = data;
                ImportPwInput.Text = "";
                SetVis(ImportPwPanel, true);
                AnimatePromptIn(ImportPwPanel, ImportPwCard);
                ImportPwInput.Focus();
                return;
            }
            ApplyImport(data, null);
        }
        catch (Exception ex)
        {
            await Ask("error", "Import failed", ex.Message);
        }
    }

    private async Task ImportWireGuard(IReadOnlyList<IStorageFile> files)
    {
        int added = 0;
        int failed = 0;

        foreach (var f in files)
        {
            string text;
            try
            {
                await using var stream = await f.OpenReadAsync();
                using var reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync();
            }
            catch
            {
                failed++;
                continue;
            }

            if (!WireGuardConf.LooksLikeConf(text))
            {
                failed++;
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(f.Name);
            if (string.IsNullOrWhiteSpace(name)) name = "WireGuard";

            List<ProxyConfig> parsed;
            try { parsed = WireGuardConf.Parse(text, name); }
            catch { parsed = new List<ProxyConfig>(); }

            if (parsed.Count == 0)
            {
                failed++;
                continue;
            }

            foreach (var cfg in parsed)
            {
                if (cfg.PrivateKey.Length == 0 || cfg.PublicKey.Length == 0)
                {
                    failed++;
                    continue;
                }
                _store.Add(cfg);
                added++;
            }
        }

        RebuildGroups();
        RefreshSelected();

        if (added > 0)
            Append($"Imported {added} WireGuard peer{(added == 1 ? "" : "s")}.");
        if (failed > 0)
            Append($"Skipped {failed} file{(failed == 1 ? "" : "s")} (missing keys or endpoint).");
        if (added == 0 && failed > 0)
            await Ask("error", "Import failed", "No usable WireGuard peers were found.");
    }

    private void ImportPwCancel_Click(object? sender, RoutedEventArgs e)
    {
        _pendingImport = null;
        ImportPwInput.Text = "";
        AnimatePromptOut(ImportPwPanel, ImportPwCard);
    }

    private void ImportPwDim_Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, ImportPwPanel)) ImportPwCancel_Click(sender, e);
    }

    private void ImportPwConfirm_Click(object? sender, RoutedEventArgs e)
    {
        if (_pendingImport is null) return;
        var data = _pendingImport;
        string pw = ImportPwInput.Text ?? "";
        ApplyImport(data, pw);
    }

    private async void ApplyImport(byte[] data, string? pw)
    {
        try
        {
            var configs = ConfigFile.Decode(data, pw);
            foreach (var c in configs) _store.Add(c);
            _store.SaveNow();
            _pendingImport = null;
            ImportPwInput.Text = "";
            AnimatePromptOut(ImportPwPanel, ImportPwCard);
            RebuildGroups();
            await Ask("success", "Imported", $"{configs.Count} servers added.");
        }
        catch (Exception ex)
        {
            AnimatePromptOut(ImportPwPanel, ImportPwCard);
            _pendingImport = null;
            await Ask("error", "Import failed", ex.Message);
        }
    }

    private void Nav_Scanner(object? sender, RoutedEventArgs e) => ShowTab("scanner");

    private void SlideText(TextBlock tb, string next)
    {
        if (tb.Text == next) return;
        var t = tb.RenderTransform as TranslateTransform;
        if (t is null)
        {
            t = new TranslateTransform();
            tb.RenderTransform = t;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        bool swapped = false;
        timer.Tick += (_, _) =>
        {
            double ms = sw.Elapsed.TotalMilliseconds;
            if (ms < 120.0)
            {
                double p = ms / 120.0;
                tb.Opacity = 1.0 - p;
                t.Y = -8.0 * p;
                return;
            }
            if (!swapped)
            {
                swapped = true;
                tb.Text = next;
                t.Y = 8.0;
            }
            double q = Math.Min(1.0, (ms - 120.0) / 170.0);
            double e = 1.0 - Math.Pow(1.0 - q, 3.0);
            tb.Opacity = e;
            t.Y = 8.0 * (1.0 - e);
            if (q >= 1.0)
            {
                timer.Stop();
                tb.Opacity = 1.0;
                t.Y = 0;
            }
        };
        timer.Start();
    }

    private void AnimateNumber2(TextBlock tb, long to)
    {
        tb.Text = Bytes1(to);
    }

    private void AnimateNumber(TextBlock tb, double to)
    {
        double from = double.TryParse(tb.Text, out var f) ? f : 0.0;
        if (Math.Abs(to - from) < 0.005)
        {
            tb.Text = $"{to:0.00}";
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            double p = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / 260.0);
            double e = 1.0 - Math.Pow(1.0 - p, 3.0);
            tb.Text = $"{from + (to - from) * e:0.00}";
            if (p >= 1.0) timer.Stop();
        };
        timer.Start();
    }

    private static void AttachCardButton(Border card, Action click, double pressScale, bool bounce)
    {
        static Transitions T(double ms, Easing e) => new()
        {
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(ms),
                Easing = e
            }
        };
        var pressT = T(130, new CubicEaseOut());
        var popT = T(110, new CubicEaseOut());
        var dipT = T(85, new CubicEaseInOut());
        var settleT = T(130, new CubicEaseOut());
        var plainT = T(160, new CubicEaseOut());

        card.RenderTransformOrigin = RelativePoint.Center;
        card.Transitions = pressT;
        card.RenderTransform = TransformOperations.Parse("scale(1)");
        bool down = false;
        int gen = 0;
        string pressedScale = $"scale({pressScale.ToString(System.Globalization.CultureInfo.InvariantCulture)})";

        void SpringBack()
        {
            int g = ++gen;
            if (!bounce)
            {
                card.Transitions = plainT;
                card.RenderTransform = TransformOperations.Parse("scale(1)");
                return;
            }
            card.Transitions = popT;
            card.RenderTransform = TransformOperations.Parse("scale(1)");
            DispatcherTimer.RunOnce(() =>
            {
                if (g != gen) return;
                card.Transitions = dipT;
                card.RenderTransform = TransformOperations.Parse("scale(0.988)");
                DispatcherTimer.RunOnce(() =>
                {
                    if (g != gen) return;
                    card.Transitions = settleT;
                    card.RenderTransform = TransformOperations.Parse("scale(1)");
                }, TimeSpan.FromMilliseconds(90));
            }, TimeSpan.FromMilliseconds(115));
        }

        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            down = true;
            gen++;
            card.Transitions = pressT;
            card.RenderTransform = TransformOperations.Parse(pressedScale);
            e.Pointer.Capture(card);
        };
        card.PointerReleased += (_, e) =>
        {
            if (!down) return;
            e.Handled = true;
            down = false;
            e.Pointer.Capture(null);
            SpringBack();
            var p = e.GetPosition(card);
            if (p.X >= 0 && p.Y >= 0 && p.X <= card.Bounds.Width && p.Y <= card.Bounds.Height)
                click();
        };
        card.PointerCaptureLost += (_, _) =>
        {
            if (!down) return;
            down = false;
            SpringBack();
        };
    }

    private void SelectedBox_Enter(object? sender, PointerEventArgs e)
    {
        if (SelectedHover is not null) SelectedHover.Opacity = 1.0;
    }

    private void SelectedBox_Exit(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured == SelectedBox) return;
        if (SelectedHover is not null) SelectedHover.Opacity = 0.0;
    }

    private void SelectedBox_CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (SelectedHover is null) return;
        if (SelectedBox is not null && SelectedBox.IsPointerOver) return;
        SelectedHover.Opacity = 0.0;
    }

    private void CloseScanner_Click(object? sender, RoutedEventArgs e) => GoBack();

    private void Pool_Set(object? sender, RoutedEventArgs e)
    {
        if (sender is Control c && c.Tag is string t && int.TryParse(t, out int n)) _scanPool = n;
    }

    private void StopScan(object? sender, RoutedEventArgs e) => _scanCts?.Cancel();

    private async void ScanCloudflare(object? sender, RoutedEventArgs e)
    {
        if (_scanCts is not null) return;
        _scanCts = new CancellationTokenSource();
        ScanButton.IsEnabled = false;
        SetVis(ScanStopButton, true);
        SetVis(ScanResultsTitle, false);
        _scanResults.Clear();
        int total = _scanPool;
        SetVis(ScanProgressTrack, true);
        ScanProgressBar.Width = 0;
        var progress = new Progress<int>(d =>
        {
            ScanStatus.Text = $"Scanning {d}/{total}";
            double trackW = ScanProgressTrack.Bounds.Width;
            if (trackW > 0) ScanProgressBar.Width = trackW * d / total;
        });
        try
        {
            int port = 443;
            var results = await CloudflareScanner.Scan(total, port, 50, progress, _scanCts.Token);
            foreach (var r in results) _scanResults.Add(r);
            ScanStatus.Text = results.Count == 0
                ? "No responsive IPs found."
                : $"{results.Count} responsive IPs, sorted by ping";
            SetVis(ScanResultsTitle, results.Count > 0);
        }
        catch (OperationCanceledException)
        {
            ScanStatus.Text = Strings.Get(_lang, "cf_stopped");
            SetVis(ScanResultsTitle, _scanResults.Count > 0);
        }
        catch (Exception ex)
        {
            ScanStatus.Text = ex.Message;
        }
        finally
        {
            ScanButton.IsEnabled = true;
            SetVis(ScanStopButton, false);
            SetVis(ScanProgressTrack, false);
            _scanCts.Dispose();
            _scanCts = null;
        }
    }

    private async void CopyScanIp(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string ip) return;
        var clip = GetTopLevel(this)?.Clipboard;
        if (clip is not null) await clip.SetTextAsync(ip);
    }

    private void UseCleanIp(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string ip) return;
        if (_store.Selected() is not ProxyConfig cfg)
        {
            Append("Select a server first, then Use a clean IP to set its address.");
            return;
        }
        if (string.IsNullOrEmpty(cfg.Sni) && string.IsNullOrEmpty(cfg.Host))
            cfg.Sni = cfg.Address;
        cfg.Address = ip;
        _store.SaveNow();
        ReloadServers();
        RefreshSelected();
        Append($"Applied {ip} to {cfg.Name}.");
    }

    private void Nav_Usage(object? sender, RoutedEventArgs e)
    {
        RefreshUsage();
        ShowTab("usage");
    }

    private static readonly string[] UnitKeys = { "unit_b", "unit_kb", "unit_mb", "unit_gb", "unit_tb" };
    private static readonly string[] RateUnits = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };

    private string Unit(int i) => Strings.Get(_lang, UnitKeys[Math.Min(i, UnitKeys.Length - 1)]);

    private static string Rate(int i) => RateUnits[Math.Min(i, RateUnits.Length - 1)];

    private static int Scale(ref double d)
    {
        int i = 0;
        while (d >= 1024 && i < 4) { d /= 1024; i++; }
        return i;
    }

    private string Bytes1(long v)
    {
        double d = v;
        int i = Scale(ref d);
        string n = i == 0 ? $"{d:0}" : $"{d:0.#}";
        return Strings.LocalizeDigits($"{n} {Unit(i)}", _lang);
    }

    private string Speed(long v)
    {
        double d = v;
        int i = Scale(ref d);
        string n = i == 0 ? $"{d:0}" : $"{d:0.#}";
        return Strings.LocalizeDigits($"{n} {Rate(i)}", _lang);
    }

    private string Bytes(long v)
    {
        double d = v;
        int i = Scale(ref d);
        string n = i == 0 ? $"{d:0}" : $"{d:0.##}";
        return Strings.LocalizeDigits($"{n} {Unit(i)}", _lang);
    }

    private enum UsageRange { Today, Week, Month, Custom }

    private UsageRange _range = UsageRange.Today;

    private static readonly ImmutableSolidColorBrush[] CfgPalette =
    {
        new(Color.FromRgb(0x5B, 0x83, 0xD6)),
        new(Color.FromRgb(0x8C, 0x6B, 0xE6)),
        new(Color.FromRgb(0xE6, 0x8A, 0x3D)),
        new(Color.FromRgb(0xE0, 0x57, 0x5C)),
        new(Color.FromRgb(0x46, 0xB7, 0xC9)),
        new(Color.FromRgb(0x3D, 0x6A, 0xD6)),
        new(Color.FromRgb(0xC9, 0xA1, 0x3D)),
        new(Color.FromRgb(0x52, 0xC0, 0x8A)),
        new(Color.FromRgb(0xB2, 0x5F, 0xD6)),
        new(Color.FromRgb(0xD6, 0x68, 0x3D))
    };
    private static readonly ImmutableSolidColorBrush CfgOther = new(Color.FromRgb(0x44, 0x5A, 0x80));
    private static readonly ImmutableSolidColorBrush CfgDirect = new(Color.FromRgb(0x77, 0x80, 0x8F));
    private static readonly ImmutableSolidColorBrush ChartMuted = new(Color.FromRgb(0x9F, 0xB3, 0xD6));
    private readonly Dictionary<string, ImmutableSolidColorBrush> _cfgColors = new();
    private List<UsageStore.StackBar>? _labelBars;

    private string LocBytes(long v) => Bytes(v);

    private void RefreshUsage()
    {
        var all = UsageStore.TotalAll();
        var direct = UsageStore.TotalDirect();
        long allTotal = all[0] + all[1];
        long directTotal = direct[0] + direct[1];
        AllTimeText.Text = LocBytes(allTotal);
        if (VpnTotalText is not null)
            VpnTotalText.Text = LocBytes(Math.Max(0, allTotal - directTotal));

        var top = UsageStore.TopConfig();
        if (top is { } tc && tc.Total > 0)
        {
            var flagImg = Flags.ForName(tc.Name);
            TopConfigFlag.Source = flagImg;
            TopConfigFlagBox.IsVisible = flagImg is not null;
            TopConfigName.Text = Flags.DisplayName(tc.Name);
            TopConfigUsage.Text = LocBytes(tc.Total);
            TopConfigBox.IsVisible = true;
        }
        else
        {
            TopConfigBox.IsVisible = false;
        }

        List<UsageStore.StackBar> bars = _range switch
        {
            UsageRange.Today => UsageStore.HourlyTodayStacked(),
            UsageRange.Week => UsageStore.DailyStacked(7),
            UsageRange.Month => UsageStore.DailyStacked(30),
            _ => CustomStacked()
        };

        AssignConfigColors(bars);

        var sum = UsageStore.SumStacked(bars);
        UsageDownText.Text = LocBytes(sum[1]);
        UsageUpText.Text = LocBytes(sum[0]);

        NoDataText.IsVisible = !bars.Any(b => b.Total > 0);
        BuildChart(bars);
        BuildLabels(bars);

        _loading = true;
        RangeToday.IsChecked = _range == UsageRange.Today;
        RangeWeek.IsChecked = _range == UsageRange.Week;
        RangeMonth.IsChecked = _range == UsageRange.Month;
        RangeCustom.IsChecked = _range == UsageRange.Custom;
        _loading = false;
        CustomDates.IsVisible = _range == UsageRange.Custom;
    }

    private void Range_Today(object? sender, RoutedEventArgs e) { _range = UsageRange.Today; RefreshUsage(); }

    private void Range_Week(object? sender, RoutedEventArgs e) { _range = UsageRange.Week; RefreshUsage(); }

    private void Range_Month(object? sender, RoutedEventArgs e) { _range = UsageRange.Month; RefreshUsage(); }

    private void Range_Custom(object? sender, RoutedEventArgs e)
    {
        _range = UsageRange.Custom;
        FromDate.SelectedDate ??= DateTime.Today.AddDays(-6);
        ToDate.SelectedDate ??= DateTime.Today;
        RefreshUsage();
    }

    private void CustomDateChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (_range == UsageRange.Custom) RefreshUsage();
    }

    private List<UsageStore.StackBar> CustomStacked()
    {
        var from = FromDate.SelectedDate ?? DateTime.Today.AddDays(-6);
        var to = ToDate.SelectedDate ?? DateTime.Today;
        int span = Math.Abs((to.Date - from.Date).Days);
        return span <= 2 ? UsageStore.HourlyStackedRange(from, to) : UsageStore.DailyStackedRange(from, to);
    }

    private void AssignConfigColors(List<UsageStore.StackBar> bars)
    {
        _cfgColors.Clear();
        var totals = new Dictionary<string, long>();
        foreach (var b in bars)
            foreach (var s in b.Segments)
            {
                if (string.IsNullOrEmpty(s.Config)) continue;
                if (s.Config == UsageStore.DirectKey) continue;
                totals.TryGetValue(s.Config, out var t);
                totals[s.Config] = t + s.Total;
            }
        var ordered = new List<string>(totals.Keys);
        ordered.Sort((a, b) => totals[b].CompareTo(totals[a]));
        for (int i = 0; i < ordered.Count; i++)
            _cfgColors[ordered[i]] = CfgPalette[i % CfgPalette.Length];
    }

    private ImmutableSolidColorBrush ColorFor(string cfg)
        => string.IsNullOrEmpty(cfg)
            ? CfgOther
            : cfg == UsageStore.DirectKey
                ? CfgDirect
                : (_cfgColors.TryGetValue(cfg, out var b) ? b : CfgOther);

    private void BuildChart(List<UsageStore.StackBar> bars)
    {
        ChartGrid.Children.Clear();
        ChartGrid.ColumnDefinitions.Clear();
        if (bars.Count == 0) return;
        long max = 1;
        foreach (var b in bars) if (b.Total > max) max = b.Total;
        for (int i = 0; i < bars.Count; i++)
        {
            ChartGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var b = bars[i];
            if (b.Total <= 0) continue;
            double fullH = Math.Max(3.0, (double)b.Total / max * 150.0);

            var stack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 2, 0),
                Background = Brushes.Transparent,
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(130),
                        Easing = new CubicEaseOut()
                    }
                }
            };

            var active = b.Segments.Where(s => s.Total > 0).ToList();
            var heights = new double[active.Count];
            double total = 0;
            for (int j = 0; j < active.Count; j++)
            {
                heights[j] = Math.Max(1.0, (double)active[j].Total / b.Total * fullH);
                total += heights[j];
            }
            if (total > fullH)
            {
                double scale = fullH / total;
                for (int j = 0; j < active.Count; j++) heights[j] *= scale;
            }
            for (int j = 0; j < active.Count; j++)
            {
                stack.Children.Add(new Border
                {
                    Height = heights[j],
                    Background = ColorFor(active[j].Config),
                    CornerRadius = j == 0 ? new CornerRadius(3, 3, 0, 0) : default
                });
            }

            ToolTip.SetTip(stack, BuildUsageTip(b));
            ToolTip.SetShowDelay(stack, 120);
            stack.SetValue(ToolTip.PlacementProperty, PlacementMode.Pointer);
            stack.SetValue(ToolTip.HorizontalOffsetProperty, 8.0);
            stack.SetValue(ToolTip.VerticalOffsetProperty, 10.0);
            stack.PointerEntered += (snd, _) => ((StackPanel)snd!).Opacity = 0.82;
            stack.PointerExited += (snd, _) => ((StackPanel)snd!).Opacity = 1.0;

            Grid.SetColumn(stack, i);
            ChartGrid.Children.Add(stack);
        }
    }

    private Control BuildUsageTip(UsageStore.StackBar b)
    {
        var root = new StackPanel { MinWidth = 216 };

        root.Children.Add(new TextBlock
        {
            Text = Strings.LocalizeDigits(b.Label, _lang),
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });
        root.Children.Add(new TextBlock
        {
            Text = Strings.LocalizeDigits($"{Strings.Get(_lang, "total")}: {Bytes(b.Total)}", _lang),
            Foreground = ChartMuted,
            FontSize = 11.5,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 7)
        });

        foreach (var s in b.Segments)
        {
            if (s.Total <= 0) continue;
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,8,*,14,Auto"),
                Margin = new Thickness(0, 5, 0, 0)
            };
            var dot = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = ColorFor(s.Config),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            string name = string.IsNullOrEmpty(s.Config)
                ? Strings.Get(_lang, "usage_other")
                : s.Config == UsageStore.DirectKey
                    ? "VPN off"
                    : Flags.DisplayName(s.Config);
            var label = new TextBlock
            {
                Text = name,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xE6, 0xF5)),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            var stats = new TextBlock
            {
                Text = Strings.LocalizeDigits(Bytes(s.Total), _lang),
                Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xC8)),
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            Grid.SetColumn(label, 2);
            Grid.SetColumn(stats, 4);
            row.Children.Add(dot);
            row.Children.Add(label);
            row.Children.Add(stats);
            root.Children.Add(row);
        }
        AttachTipFade(root);
        return root;
    }

    private static void AttachTipFade(Control content)
    {
        content.AttachedToVisualTree += (_, _) =>
        {
            Control host = content.FindAncestorOfType<ToolTip>() as Control ?? content;
            host.Transitions = null;
            host.Opacity = 0;
            host.RenderTransform = TransformOperations.Parse("translateY(6px)");
            Dispatcher.UIThread.Post(() =>
            {
                host.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(180),
                        Easing = new CubicEaseOut()
                    },
                    new TransformOperationsTransition
                    {
                        Property = RenderTransformProperty,
                        Duration = TimeSpan.FromMilliseconds(210),
                        Easing = new CubicEaseOut()
                    }
                };
                host.Opacity = 1;
                host.RenderTransform = TransformOperations.Parse("translateY(0px)");
            }, DispatcherPriority.Render);
        };
    }

    private void BuildLabels(List<UsageStore.StackBar> bars)
    {
        _labelBars = bars;
        RenderLabels();
    }

    private void RenderLabels()
    {
        LabelCanvas.Children.Clear();
        if (_labelBars is null || _labelBars.Count == 0) return;
        double w = LabelCanvas.Bounds.Width;
        if (w <= 1) return;
        int count = _labelBars.Count;
        int every = Math.Max(1, count / 6);
        for (int i = 0; i < count; i++)
        {
            if (i % every != 0) continue;
            var t = new TextBlock
            {
                Text = Strings.LocalizeDigits(_labelBars[i].Short, _lang),
                FontSize = 10,
                Foreground = ChartMuted
            };
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double cx = (i + 0.5) / count * w;
            double left = cx - t.DesiredSize.Width / 2;
            if (left < 0) left = 0;
            if (left > w - t.DesiredSize.Width) left = w - t.DesiredSize.Width;
            Canvas.SetLeft(t, left);
            Canvas.SetTop(t, 2);
            LabelCanvas.Children.Add(t);
        }
    }

    private void OpenProjects_Click(object? sender, RoutedEventArgs e)
    {
        ProjOrb.SetText("START", true);
        // Fill the card BEFORE switching tabs. Project_Changed writes the title
        // and the wrapped description, and it does not resize while another tab
        // is active — so ShowTab then measures real content and animates once.
        // With the old order the first measure saw an empty card, jumped short,
        // and a second pass had to correct it.
        Project_Changed(this, e);
        UpdateLayout();
        ShowTab("projects");
        QueueWindowHeight();
    }

    private void CloseProjects_Click(object? sender, RoutedEventArgs e) => GoBack();

    private static readonly (string Cc, string Name)[] TorCountries =
    {
        ("de", "Germany"), ("nl", "Netherlands"), ("fr", "France"),
        ("se", "Sweden"), ("ch", "Switzerland"), ("fi", "Finland"),
        ("ro", "Romania"), ("gb", "United Kingdom"), ("us", "United States"),
        ("ca", "Canada"), ("at", "Austria"), ("pl", "Poland"),
        ("es", "Spain"), ("it", "Italy"), ("cz", "Czechia"),
        ("dk", "Denmark"), ("no", "Norway"), ("be", "Belgium"),
        ("lu", "Luxembourg"), ("ie", "Ireland"), ("pt", "Portugal"),
        ("bg", "Bulgaria"), ("hu", "Hungary"), ("sk", "Slovakia"),
        ("lv", "Latvia"), ("lt", "Lithuania"), ("ee", "Estonia"),
        ("md", "Moldova"), ("ua", "Ukraine"), ("is", "Iceland"),
        ("gr", "Greece"), ("jp", "Japan"), ("sg", "Singapore"),
        ("hk", "Hong Kong"), ("au", "Australia"), ("br", "Brazil"),
        ("za", "South Africa"),
        ("hr", "Croatia"), ("si", "Slovenia"), ("rs", "Serbia"),
        ("ba", "Bosnia"), ("mk", "North Macedonia"), ("al", "Albania"),
        ("me", "Montenegro"), ("cy", "Cyprus"), ("mt", "Malta"),
        ("li", "Liechtenstein"), ("by", "Belarus"), ("ge", "Georgia"),
        ("am", "Armenia"), ("az", "Azerbaijan"), ("kz", "Kazakhstan"),
        ("ru", "Russia"), ("tr", "Turkey"), ("il", "Israel"),
        ("ae", "United Arab Emirates"), ("sa", "Saudi Arabia"), ("in", "India"),
        ("kr", "South Korea"), ("tw", "Taiwan"), ("th", "Thailand"),
        ("vn", "Vietnam"), ("my", "Malaysia"), ("id", "Indonesia"),
        ("ph", "Philippines"), ("nz", "New Zealand"), ("mx", "Mexico"),
        ("ar", "Argentina"), ("cl", "Chile"), ("co", "Colombia"),
        ("pa", "Panama"), ("cr", "Costa Rica"), ("eg", "Egypt"),
        ("ma", "Morocco"), ("ng", "Nigeria"), ("ke", "Kenya"),
        ("uy", "Uruguay")
    };

    private readonly TorNodes _torNodes = new();
    private readonly HashSet<string> _torPicked = new(StringComparer.OrdinalIgnoreCase);
    private bool _torNodesBuilt;

    private readonly List<CheckBox> _torBoxes = new();
    private readonly List<Border> _torCards = new();

    private void BuildTorNodeList()
    {
        if (_torNodesBuilt || TorPickList is null) return;
        _torNodesBuilt = true;

        var rows = new List<Control>();
        foreach (var (cc, name) in TorCountries)
        {
            var box = new CheckBox
            {
                Tag = cc,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            box.IsCheckedChanged += TorPick_Changed;
            _torBoxes.Add(box);

            var art = Flags.ForIso(cc);
            var flag = new Border
            {
                Width = 26,
                Height = 19,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x2F, 0x58)),
                Child = art is null ? null : new Image { Source = art, Stretch = Stretch.UniformToFill }
            };

            var label = new TextBlock
            {
                Text = name,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xE6, 0xF5)),
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
            Grid.SetColumn(box, 0);
            Grid.SetColumn(flag, 1);
            Grid.SetColumn(label, 2);
            grid.Children.Add(box);
            grid.Children.Add(flag);
            grid.Children.Add(label);

            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x1D, 0x3F)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x36, 0x5F)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(13, 11),
                Margin = new Thickness(0, 0, 0, 8),
                Tag = cc,
                Child = grid
            };
            card.PointerPressed += TorCard_Pressed;
            _torCards.Add(card);
            rows.Add(card);
        }

        TorPickList.ItemsSource = rows;
        SyncTorBoxes();
    }

    private bool _torDragging;
    private bool _torDragTo;

    private void TorCard_Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is CheckBox) return;
        if (sender is not Border card || card.Tag is not string cc) return;
        var box = _torBoxes.FirstOrDefault(b => (b.Tag as string) == cc);
        if (box is null) return;

        _torDragTo = box.IsChecked != true;
        _torDragging = true;
        box.IsChecked = _torDragTo;
        e.Pointer.Capture(TorPickList);
        e.Handled = true;
    }

    private void TorPickList_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_torDragging || TorPickList is null) return;
        var p = e.GetPosition(TorPickList);

        foreach (var obj in _torCards)
        {
            var b = obj.Bounds;
            if (p.Y < b.Y || p.Y > b.Y + b.Height) continue;
            if (obj.Tag is not string cc) continue;
            var box = _torBoxes.FirstOrDefault(x => (x.Tag as string) == cc);
            if (box is not null && box.IsChecked != _torDragTo) box.IsChecked = _torDragTo;
            break;
        }
    }

    private void TorPickList_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_torDragging) return;
        _torDragging = false;
        e.Pointer.Capture(null);
    }

    private void SyncTorBoxes()
    {
        _loading = true;
        foreach (var b in _torBoxes)
            b.IsChecked = b.Tag is string cc && _torPicked.Contains(cc);
        _loading = false;
        UpdateTorPickCount();
    }

    private void TorPickOpen_Click(object? sender, RoutedEventArgs e)
    {
        BuildTorNodeList();
        SyncTorBoxes();
        ShowTab("torpick");
    }

    private void TorPickClose_Click(object? sender, RoutedEventArgs e) => GoBack();

    private const int MaxTorNodes = 12;

    private void TorPickAll_Click(object? sender, RoutedEventArgs e)
    {
        _torPicked.Clear();
        foreach (var (cc, _) in TorCountries)
        {
            if (_torPicked.Count >= MaxTorNodes) break;
            _torPicked.Add(cc);
        }
        SyncTorBoxes();
        Append($"Selected the first {MaxTorNodes} countries. Each one runs its own Tor process.");
    }

    private void TorPickNone_Click(object? sender, RoutedEventArgs e)
    {
        _torPicked.Clear();
        SyncTorBoxes();
    }

    private void TorPick_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not CheckBox cb || cb.Tag is not string cc) return;
        if (cb.IsChecked == true) _torPicked.Add(cc);
        else _torPicked.Remove(cc);
        UpdateTorPickCount();
    }

    private void UpdateTorPickCount()
    {
        string n = Strings.LocalizeDigits(_torPicked.Count.ToString(), _lang);
        if (TorNodeCount is not null) TorNodeCount.Text = n;
        if (TorPickCount is not null) TorPickCount.Text = n;
        if (TorPickSummary is not null)
            TorPickSummary.Text = _torPicked.Count == 0
                ? Strings.Get(_lang, "tor_pick_open")
                : Strings.Get(_lang, "tor_pick_some").Replace("{0}", n);
    }

    private readonly PsiphonController _psiphon = new();

    private void PsiphonRegion_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (PsiphonRegion?.SelectedItem is not ComboBoxItem item) return;
        string rc = item.Tag as string ?? "";
        if (rc == _store.PsiphonRegion) return;
        _store.SetPsiphonRegion(rc);
    }

    private async Task RunPsiphon(CancellationToken ct)
    {
        if (!PsiphonController.Available)
            throw new InvalidOperationException(
                "Psiphon needs this file (not found):\n\n" + PsiphonController.ExpectedPath);

        _psiphon.Log -= OnTorNodeLog;
        _psiphon.Log += OnTorNodeLog;

        ProjOrb.SetText("STARTING");
        bool up = await _psiphon.Start(_store.PsiphonRegion, 120000, ct);
        if (!up)
            throw new InvalidOperationException("Psiphon could not establish a tunnel. Try again or pick another region.");

        const string name = "Psiphon";
        var existing = _store.Configs.FirstOrDefault(x =>
            x.Protocol == "socks" && x.Address == "127.0.0.1" && x.Port == PsiphonController.SocksPort);

        if (existing is null)
        {
            _store.Add(new ProxyConfig
            {
                Name = name,
                Protocol = "socks",
                Address = "127.0.0.1",
                Port = PsiphonController.SocksPort,
                Source = ConfigSource.Personal
            });
        }
        else
        {
            existing.Name = name;
        }

        _store.SaveNow();
        RebuildGroups();
        RefreshSelected();
        Append("Psiphon ready. Select it in your server list to use it.");
    }

    private async Task RunTorNodes(CancellationToken ct)
    {
        if (!TorNodes.Available)
            throw new InvalidOperationException($"Tor needs this file (not found): {TorNodes.BinaryPath}");
        if (_torPicked.Count == 0)
            throw new InvalidOperationException("Pick at least one country first.");
        if (!TorNodes.GeoAvailable)
            throw new InvalidOperationException(
                "Tor needs geoip and geoip6 to pin exit countries. Looked for:\n\n" +
                TorNodes.GeoIpExpected);

        if (_torPicked.Count > MaxTorNodes)
            throw new InvalidOperationException(
                $"Pick at most {MaxTorNodes} countries. Each one runs a separate Tor process.");

        var baseCfg = _store.Selected();
        if (baseCfg is null || !string.IsNullOrEmpty(baseCfg.TorBase))
            throw new InvalidOperationException("Select a normal server first \u2014 the Tor locations ride on it.");
        if (!_core.IsRunning)
            throw new InvalidOperationException("Connect to that server first. Tor builds its circuits through the tunnel.");

        _torNodes.Log -= OnTorNodeLog;
        _torNodes.Log += OnTorNodeLog;

        var wanted = new List<(string, string)>();
        foreach (var (cc, name) in TorCountries)
            if (_torPicked.Contains(cc)) wanted.Add((cc, name));

        ProjOrb.SetText("BUILDING");
        var live = await _torNodes.Start(wanted, 90000, ct);
        if (live.Count == 0)
            throw new InvalidOperationException("No Tor node came up. Try fewer countries or check the log.");

        int added = 0;
        foreach (var node in live)
        {
            string name = $"Tor \u2014 {node.Label}";
            var existing = _store.Configs.FirstOrDefault(x =>
                x.Protocol == "socks" && x.Address == "127.0.0.1" && x.Port == node.Port);

            if (existing is not null)
            {
                existing.Name = name;
                existing.TorBase = baseCfg.Id;
                existing.TorCountry = node.Country;
                continue;
            }

            _store.Add(new ProxyConfig
            {
                Name = name,
                Protocol = "socks",
                Address = "127.0.0.1",
                Port = node.Port,
                TorBase = baseCfg.Id,
                TorCountry = node.Country,
                Source = ConfigSource.Personal
            });
            added++;
        }

        RebuildGroups();
        RefreshSelected();
        _store.SaveNow();
        Append($"Tor locations ready over {Flags.DisplayName(baseCfg.Name)}: " +
               $"{live.Count} node{(live.Count == 1 ? "" : "s")}, {added} new.");
    }

    private void OnTorNodeLog(string line) => Dispatcher.UIThread.Post(() => Append(line));

    private int _torPct = -1;

    private void OnTorBootstrap(string country, int pct) => Dispatcher.UIThread.Post(() =>
    {
        if (!_torPending) return;
        int p = Math.Clamp(pct, 0, 100);
        if (p == _torPct) return;
        _torPct = p;
        Append($"Tor circuit {p}%");
    });

    private void Project_Changed(object? sender, RoutedEventArgs e)
    {
        bool psiphon = ProjPsiphon.IsChecked == true;
        bool torNodes = ProjTor.IsChecked == true;
        SetVis(PsiphonBox, psiphon);
        SetVis(TorNodeBox, torNodes);
        SetVis(ProjBox, !torNodes && !psiphon);

        if (psiphon)
        {
            if (PsiphonRegion is not null)
            {
                _loading = true;
                SelectByTag(PsiphonRegion, _store.PsiphonRegion);
                _loading = false;
            }
            ProjOrb.SetText("START", true);
            if (_tab == "projects") AnimateWindowHeight();
            return;
        }

        if (torNodes)
        {
            BuildTorNodeList();
            ProjOrb.SetText("START", true);
            if (_tab == "projects") AnimateWindowHeight();
            return;
        }

        bool aether = ProjAether.IsChecked == true;
        double fromH = _tab == "projects" && ProjBox is not null ? ProjBox.Bounds.Height : 0;

        ProjTitle.Text = aether ? "Aether" : Strings.Get(_lang, "legacy_warp");
        ProjDesc.Text = Strings.Get(_lang, aether ? "proj_aether_desc" : "proj_warp_desc");
        if (_tab != "projects" || ProjBox is null) return;

        _projBoxTimer?.Stop();
        _projBoxTimer = null;
        ProjBox.Height = double.NaN;
        UpdateLayout();
        double toH = ProjBox.Bounds.Height;

        AnimateWindowHeight();
        AnimateProjBox(fromH, toH);
    }

    private DispatcherTimer? _projBoxTimer;

    private void AnimateProjBox(double from, double to)
    {
        _projBoxTimer?.Stop();
        _projBoxTimer = null;
        if (ProjBox is null) return;
        if (from <= 1 || to <= 1 || Math.Abs(to - from) < 1)
        {
            ProjBox.Height = double.NaN;
            return;
        }

        ProjBox.Height = from;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _projBoxTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_projBoxTimer, timer))
            {
                timer.Stop();
                return;
            }
            double p = Math.Min(1.0, (DateTime.UtcNow - t0).TotalMilliseconds / 240.0);
            double e = 1 - Math.Pow(1 - p, 3);
            ProjBox.Height = from + (to - from) * e;
            if (p < 1) return;
            timer.Stop();
            _projBoxTimer = null;
            ProjBox.Height = double.NaN;
        };
        timer.Start();
    }

    private async void ProjectStart_Click(object? sender, RoutedEventArgs e)
    {
        if (_projRunning)
        {
            _projCts?.Cancel();
            ProjOrb.SetText("STOPPING");
            return;
        }

        _projRunning = true;
        ProjHint.IsVisible = false;
        _projCts = new CancellationTokenSource();
        var ct = _projCts.Token;
        ProjOrb.SetBusy(true);
        string? error = null;
        bool canceled = false;

        try
        {
            if (ProjPsiphon.IsChecked == true) await RunPsiphon(ct);
            else if (ProjTor.IsChecked == true) await RunTorNodes(ct);
            else if (ProjAether.IsChecked == true) await RunAether(ct);
            else await RunWarp(ct);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Append(ex.Message);
        }
        finally
        {
            _projRunning = false;
            _projCts?.Dispose();
            _projCts = null;
            ProjOrb.SetBusy(false);
            if (canceled)
            {
                ProjOrb.SetText("START");
            }
            else
            {
                ProjOrb.SetText(error is null ? "DONE" : "FAILED");
                if (error is not null)
                {
                    ProjHint.Text = error;
                    ProjHint.IsVisible = true;
                }
                ReloadServers();
                await Task.Delay(1900);
                if (!_projRunning) ProjOrb.SetText("START");
            }
        }
    }

    private async Task RunAether(CancellationToken ct)
    {
        if (!_core.AetherAvailable)
            throw new InvalidOperationException($"aether.exe not found in {_core.AetherPath}");

        var probe = new ProxyConfig
        {
            Name = "WARP (MASQUE)",
            Protocol = "aether",
            Address = "127.0.0.1",
            Port = AetherController.SocksPort,
            AetherMode = "masque",
            AetherScan = "balanced",
            AetherHttp2 = true,
            Source = ConfigSource.Personal
        };

        ProjOrb.SetText("SCANNING");
        Append("Starting aether and scanning for a MASQUE gateway...");
        bool ok;
        using (ct.Register(() => _core.StopAether()))
            ok = await Task.Run(() => _core.StartAetherOnly(probe, AetherController.ReadyTimeoutMs));
        ct.ThrowIfCancellationRequested();

        if (!ok)
        {
            _core.StopAether();
            throw new InvalidOperationException("No working MASQUE gateway was found on this network.");
        }

        ProjOrb.SetText("CREATING");
        if (_store.Configs.Any(c => CoreController.UsesAether(c)))
        {
            Append("A WARP (MASQUE) entry already exists.");
            return;
        }
        _store.Add(probe);
        _store.SaveNow();
        Append("Added WARP (MASQUE).");
    }

    private async Task RunWarp(CancellationToken ct)
    {
        ProjOrb.SetText("REGISTERING");
        int regProxy = _core.IsRunning ? ConfigBuilder.HttpPort : 0;
        Append("Registering with Cloudflare...");
        var result = await Warp.Register(regProxy);
        ct.ThrowIfCancellationRequested();

        if (!result.Ok || result.Account is null)
            throw new InvalidOperationException(result.Message);

        if (!DelayTester.XrayAvailable)
        {
            ProjOrb.SetText("CREATING");
            foreach (var c in result.Configs) _store.Add(c);
            _store.SaveNow();
            Append($"Added {result.Configs.Count} default endpoints.");
            return;
        }

        ProjOrb.SetText("SCANNING");
        var progress = new Progress<(int Done, int Total, int Found)>(pr =>
        {
            if (pr.Done % 8 == 0) Append($"probed {pr.Done}/{pr.Total}, {pr.Found} responding");
        });
        var hits = await WarpScanner.Scan(result.Account, 8, 160, 8, 60000, progress, ct);
        ct.ThrowIfCancellationRequested();

        if (hits.Count == 0)
            throw new InvalidOperationException("No WARP endpoint responded on this network.");

        ProjOrb.SetText("CREATING");
        int keep = Math.Min(8, hits.Count);
        for (int i = 0; i < keep; i++)
        {
            var h = hits[i];
            var cfg = Warp.BuildConfig(result.Account, h.Host, h.Port, $"WARP {i + 1}");
            cfg.Ping = h.Latency;
            cfg.PingText = $"{h.Latency} ms";
            _store.Add(cfg);
        }
        _store.SaveNow();
        Append($"Added {keep} working endpoints, best {hits[0].Latency} ms.");
    }

    private void Nav_Settings(object? sender, RoutedEventArgs e) => ShowTab("settings");

    private void LoadSettings()
    {
        _loading = true;
        FragmentToggle.IsChecked = _store.Fragment;
        SplitToggle.IsChecked = _store.SplitRouting;
        SniffToggle.IsChecked = _store.SniffEnabled;
        SniffTls.IsChecked = _store.SniffProtocols.Contains("tls");
        SniffHttp.IsChecked = _store.SniffProtocols.Contains("http");
        SniffQuic.IsChecked = _store.SniffProtocols.Contains("quic");
        SniffFakeDns.IsChecked = _store.SniffProtocols.Contains("fakedns");
        RouteOnlyToggle.IsChecked = _store.SniffRouteOnly;
        if (_store.SniffEnabled)
        {
            SniffReveal.Height = double.NaN;
            SniffInner.Opacity = 1;
        }
        else
        {
            SniffReveal.Height = 0;
            SniffInner.Opacity = 0;
        }
        KillToggle.IsChecked = _store.KillSwitch;
        if (DnsToggle is not null)
            DnsToggle.IsChecked = _store.SecureDns;
        if (TorToggle is not null)
            TorToggle.IsChecked = _store.TorOnion && CoreController.TorAvailable;
        if (TorExitBox is not null) SelectByTag(TorExitBox, _store.TorExit);
        SetVis(TorExitWrap, _store.TorOnion && CoreController.TorAvailable);
        if (StartupToggle is not null)
        {
            StartupToggle.IsEnabled = Platform.Startup.Supported;
            StartupToggle.IsChecked = Platform.Startup.Supported
                ? Platform.Startup.Enabled
                : _store.RunAtStartup;
        }
        AutoSelectToggle.IsChecked = _store.AutoSelect;
        MuxToggle.IsChecked = _store.Mux;
        MuxBox.Text = _store.MuxConcurrency.ToString();
        if (_store.Mux)
        {
            MuxReveal.Height = double.NaN;
            MuxInner.Opacity = 1;
        }
        else
        {
            MuxReveal.Height = 0;
            MuxInner.Opacity = 0;
        }
        PortBox.Text = _store.MixedPort.ToString();
        SetLogLevelChecked(_store.LogLevel);
        AdBlockToggle.IsChecked = _store.AdBlock;
        _loading = false;
    }

    private static void SelectByTag(ComboBox? box, string? tag)
    {
        if (box is null) return;
        foreach (var obj in box.Items)
        {
            if (obj is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        if (box.ItemCount > 0) box.SelectedIndex = 0;
    }

    private static string TagOf(ComboBox? box) =>
        (box?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private void Fragment_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _store.SetFragment(FragmentToggle.IsChecked == true);
    }

    private void Split_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _store.SetSplitRouting(SplitToggle.IsChecked == true);
    }

    private void Sniff_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool on = SniffToggle.IsChecked == true;
        AnimateReveal(SniffReveal, SniffInner, on, true);
        _store.SetSniffEnabled(on);
    }

    private readonly Dictionary<Border, int> _revGen = new();
    private readonly Dictionary<Border, DispatcherTimer> _revTimer = new();

    private void AnimateReveal(Border reveal, Control inner, bool on, bool syncSurface = false)
    {
        if (reveal is null || inner is null) return;
        int gen = _revGen.TryGetValue(reveal, out var g0) ? g0 + 1 : 1;
        _revGen[reveal] = gen;
        if (_revTimer.TryGetValue(reveal, out var prev)) prev.Stop();

        double fromH = double.IsNaN(reveal.Height) ? reveal.Bounds.Height : reveal.Height;
        if (double.IsNaN(fromH) || fromH < 0) fromH = 0;
        double fromO = inner.Opacity;
        double toO = on ? 1 : 0;
        double toH = 0;
        if (on)
        {
            double w = reveal.Bounds.Width > 1 ? reveal.Bounds.Width
                     : (inner.Bounds.Width > 1 ? inner.Bounds.Width : 420);
            try
            {
                inner.Measure(new Size(w, double.PositiveInfinity));
                toH = inner.DesiredSize.Height;
            }
            catch
            {
                toH = 0;
            }
            if (double.IsNaN(toH) || toH < 1) toH = 80;
        }

        double surfAtZero = 0;
        if (syncSurface)
        {
            _heightTimer?.Stop();
            _heightTimer = null;
            _surfaceDriven = true;
            double surf = double.IsNaN(AppSurface.Height) ? AppSurface.Bounds.Height : AppSurface.Height;
            surfAtZero = surf - fromH;
        }

        reveal.Height = fromH;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        _revTimer[reveal] = timer;
        timer.Tick += (_, _) =>
        {
            if (_revGen[reveal] != gen)
            {
                timer.Stop();
                if (syncSurface) _surfaceDriven = false;
                return;
            }
            double p = (DateTime.UtcNow - t0).TotalMilliseconds / 230.0;
            if (p >= 1)
            {
                timer.Stop();
                inner.Opacity = toO;
                reveal.Height = on ? double.NaN : 0;
                if (syncSurface)
                {
                    double fin = Math.Clamp(surfAtZero + toH, 320.0, 700.0);
                    _surfaceDriven = false;
                    AppSurface.Height = fin;
                    SyncWindowHeight(fin);
                }
                return;
            }
            double e = 1 - Math.Pow(1 - p, 3);
            double h = fromH + (toH - fromH) * e;
            reveal.Height = h < 0 ? 0 : h;
            inner.Opacity = fromO + (toO - fromO) * e;
            if (syncSurface)
            {
                double sh = Math.Clamp(surfAtZero + h, 320.0, 700.0);
                AppSurface.Height = sh;
                SyncWindowHeight(sh);
            }
        };
        timer.Start();
    }

    private void RouteOnly_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _store.SetSniffRouteOnly(RouteOnlyToggle.IsChecked == true);
    }

    private void SniffProto_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var list = new List<string>();
        if (SniffTls.IsChecked == true) list.Add("tls");
        if (SniffHttp.IsChecked == true) list.Add("http");
        if (SniffQuic.IsChecked == true) list.Add("quic");
        if (SniffFakeDns.IsChecked == true) list.Add("fakedns");
        _store.SetSniffProtocols(list);
    }

    private void Kill_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SetKillSwitchByUser(KillToggle.IsChecked == true);
    }

    private void AutoSelect_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _store.SetAutoSelect(AutoSelectToggle.IsChecked == true);
        ApplyAutoSelect();
    }

    private void ApplyAutoSelect()
    {
        if (_store.AutoSelect) StartAutoSelect();
        else StopAutoSelect();
    }

    private void StartAutoSelect()
    {
        if (_autoSelectCts is not null) return;
        var cts = new CancellationTokenSource();
        _autoSelectCts = cts;
        _ = AutoSelectLoop(cts.Token);
    }

    private void StopAutoSelect()
    {
        _autoSelectCts?.Cancel();
        _autoSelectCts = null;
    }

    private async Task AutoSelectLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await AutoSelectOnce(ct); } catch { }
            try { await Task.Delay(AutoSelectIntervalMs, ct); } catch { return; }
        }
    }

    private async Task AutoSelectOnce(CancellationToken ct)
    {
        if (!DelayTester.XrayAvailable) return;
        var items = _store.Configs.ToList();
        if (items.Count == 0) return;

        var results = new ConcurrentDictionary<string, int>();
        using (var sem = new SemaphoreSlim(AutoSelectConcurrency))
        {
            var tasks = items.Select(async c =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    int ms = await DelayTester.Measure(c);
                    if (ms >= 0) results[c.Id] = ms;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        c.PingText = ms >= 0 ? $"{ms} ms" : "fail";
                        c.Ping = ms >= 0 ? ms : -1;
                    });
                }
                catch
                {
                }
                finally
                {
                    sem.Release();
                }
            }).ToList();
            await Task.WhenAll(tasks);
        }

        if (ct.IsCancellationRequested || results.IsEmpty) return;

        string bestId = "";
        int bestMs = int.MaxValue;
        foreach (var kv in results)
        {
            if (kv.Value < bestMs)
            {
                bestMs = kv.Value;
                bestId = kv.Key;
            }
        }
        if (bestId.Length == 0) return;

        bool reconnect = false;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            string? current = _store.SelectedId;
            if (string.IsNullOrEmpty(current))
            {
                SetSelected(bestId);
                return;
            }
            if (current == bestId) return;

            bool known = results.TryGetValue(current, out int currentMs);
            if (known && currentMs - bestMs < AutoSelectMarginMs) return;

            SetSelected(bestId);
            var picked = _store.Configs.FirstOrDefault(c => c.Id == bestId);
            Append($"Auto-selected {picked?.Name} ({bestMs} ms)");
            reconnect = _core.IsRunning;
        });

        if (reconnect) await ReconnectForAutoSelect();
    }

    private void SetSelected(string id)
    {
        _store.SelectedId = id;
        _store.SaveNow();
        RefreshSelected();
        RebuildGroups();
    }

    private bool _reconnecting;

    private async Task ReconnectForAutoSelect()
    {
        if (_reconnecting) return;
        _reconnecting = true;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(StopAll);
            await Task.Delay(700);
            await Dispatcher.UIThread.InvokeAsync(() => Power_Click(this, new RoutedEventArgs()));
        }
        finally
        {
            _reconnecting = false;
        }
    }

    private async Task RefreshAllSubs()
    {
        foreach (var sub in _store.Subscriptions.ToList())
        {
            try
            {
                var result = await SubscriptionFetcher.FetchFull(sub.Url);
                if (result.UserInfo is not null)
                {
                    sub.Used = result.UserInfo.Used;
                    sub.Total = result.UserInfo.Total;
                    sub.Expire = result.UserInfo.Expire;
                }
                _store.UpsertSubscription(sub, result.Configs);
            }
            catch
            {
            }
        }
        RebuildGroups();
    }

    private void SetupAutoRefresh()
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer = null;
        int h = _store.AutoRefreshHours;
        if (h <= 0) return;
        _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(h) };
        _autoRefreshTimer.Tick += async (_, _) => await RefreshAllSubs();
        _autoRefreshTimer.Start();
    }

    private static bool IsNewer(string latest, string current)
    {
        try
        {
            var a = latest.Split('.');
            var b = current.Split('.');
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length && int.TryParse(a[i], out var xv) ? xv : 0;
                int y = i < b.Length && int.TryParse(b[i], out var yv) ? yv : 0;
                if (x != y) return x > y;
            }
        }
        catch
        {
        }
        return false;
    }

    private async void CheckUpdate_Click(object? sender, RoutedEventArgs e)
    {
        _drawerOpen = false;
        SetVis(MenuOverlay, false);
        QueueWindowHeight();
        string current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "GRoute");
            string json = await client.GetStringAsync(UpdateUrl);
            using var doc = JsonDocument.Parse(json);
            string latest = doc.RootElement.GetProperty("version").GetString() ?? current;
            string url = doc.RootElement.TryGetProperty("url", out var u) ? (u.GetString() ?? "") : "";
            if (IsNewer(latest, current))
            {
                if (await Ask("info", $"Update available: {latest}",
                        url.Length > 0 ? "Open the download page?" : "", withCancel: url.Length > 0)
                    && url.Length > 0)
                    OpenUrl(url);
            }
            else
            {
                await Ask("success", "You are up to date", $"Version {current}");
            }
        }
        catch
        {
            await Ask("error", "Update check failed", "Could not reach the update server.");
        }
    }

    private static readonly Type[] NeverMirrored =
    {
        typeof(EarthGlobe), typeof(StartOrb), typeof(PingRing),
        typeof(ParticleField), typeof(StarField), typeof(VeinField),
        typeof(GridGlow), typeof(WaveLoader), typeof(GlassScroll),
        typeof(GlassSwitch), typeof(AvPath),
        typeof(Image), typeof(Avalonia.Controls.Shapes.Ellipse),
    };

    private void UnmirrorVisuals()
    {
        if (!Strings.IsRtl(_lang)) return;
        try
        {
            foreach (var v in this.GetVisualDescendants().ToList())
            {
                if (v is not Control ctl) continue;
                var t = ctl.GetType();
                foreach (var n in NeverMirrored)
                {
                    if (!n.IsAssignableFrom(t)) continue;
                    ctl.FlowDirection = FlowDirection.LeftToRight;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Append("Layout: " + ex.Message);
        }
    }

    private void ApplyLanguage()
    {
        foreach (var k in StrKeys)
            Resources["Str." + k] = Strings.Get(_lang, k);

        FlowDirection = _lang == Lang.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        PingLabelConverter.Farsi = _lang == Lang.Fa;
        UpdateTabIndicator(HeaderTabOf(_tab));

        FontFamily = _lang == Lang.Fa
            ? new FontFamily("Vazirmatn, Tahoma, Segoe UI")
            : new FontFamily("avares://GRoute/Assets/Fonts#Lexend, Segoe UI");
        if (Application.Current is { } appRef) appRef.Resources["AppFont"] = FontFamily;

        if (LogText is not null) LogText.FlowDirection = FlowDirection.LeftToRight;
        UnmirrorVisuals();

        PingLabelConverter.Farsi = _lang == Lang.Fa;
        if (_lastPingMs >= 0) SetPingLabel(PingMs(_lastPingMs));
        else if (_lastPingMs == -1) SetPingLabel(PingFailText());
        if (ConnTimeText is not null)
            ConnTimeText.Text = Strings.LocalizeDigits(
                _connClock is null ? "00:00:00" : (DateTime.UtcNow - _connectedAt).ToString(@"hh\:mm\:ss"),
                _lang);
        foreach (var cfg in _store.Configs) cfg.RaisePingText();
        if (LangBox is not null)
        {
            _loading = true;
            SelectByTag(LangBox, _lang.ToString());
            _loading = false;
        }
        if (MenuLangBox is not null)
        {
            _loading = true;
            SelectByTag(MenuLangBox, _lang.ToString());
            _loading = false;
        }

        RefreshSelected();
        RebuildGroups();
        if (UsagePanel is not null && UsagePanel.IsVisible) RefreshUsage();
    }

    private void Auto_Set(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not Control c || c.Tag is not string t || !int.TryParse(t, out int hours)) return;
        _store.SetAutoRefreshHours(hours);
        SetupAutoRefresh();
    }

    private void StyleAuto()
    {
        if (AutoOff is null) return;
        _loading = true;
        int h = _store.AutoRefreshHours;
        AutoOff.IsChecked = h <= 0;
        Auto1.IsChecked = h == 1;
        Auto6.IsChecked = h == 6;
        Auto12.IsChecked = h == 12;
        Auto24.IsChecked = h == 24;
        _loading = false;
    }

    private void RefreshGeoStatus()
    {
        if (GeoipStatus is null) return;
        GeoipStatus.Text = Assets.GeoipPresent
            ? Strings.Get(_lang, "present")
            : Strings.Get(_lang, "missing");
        GeositeStatus.Text = Assets.GeositePresent
            ? Strings.Get(_lang, "present")
            : Strings.Get(_lang, "missing");
    }

    private async void ImportGeo(string name)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = name,
            AllowMultiple = false
        });
        if (files.Count == 0) return;
        try
        {
            string? path = files[0].TryGetLocalPath();
            if (path is null)
            {
                Append("Could not read that file.");
                return;
            }
            Assets.Import(path, name);
            Append($"Imported {name}.");
        }
        catch (Exception ex)
        {
            Append($"Import failed: {ex.Message}");
        }
        RefreshGeoStatus();
    }

    private void ResetGeo(string name)
    {
        try
        {
            if (Assets.ResetToBundled(name)) Append($"Reset {name}.");
            else Append($"No bundled {name} available.");
        }
        catch (Exception ex)
        {
            Append($"Reset failed: {ex.Message}");
        }
        RefreshGeoStatus();
    }

    private void ChangeGeoip(object? sender, RoutedEventArgs e) => ImportGeo("geoip.dat");

    private void ChangeGeosite(object? sender, RoutedEventArgs e) => ImportGeo("geosite.dat");

    private void ResetGeoip(object? sender, RoutedEventArgs e) => ResetGeo("geoip.dat");

    private void ResetGeosite(object? sender, RoutedEventArgs e) => ResetGeo("geosite.dat");

    private void OpenTelegram(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://t.me/OracleVPNSupport");

    private void OpenSource(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/SuOracle/GRoute-Win");

    private void Dns_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not Avalonia.Controls.Primitives.ToggleButton t) return;
        bool on = t.IsChecked == true;
        _store.SetSecureDns(on);
        Append(on
            ? "Encrypted DNS on \u2014 lookups resolve inside the tunnel. Reconnect to apply."
            : "Encrypted DNS off \u2014 lookups use your system resolver. Reconnect to apply.");
    }

    private void TorExit_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (TorExitBox?.SelectedItem is not ComboBoxItem item) return;
        string cc = item.Tag as string ?? "";
        if (cc == _store.TorExit) return;

        _store.SetTorExit(cc);
        Append(cc.Length == 2
            ? $"Tor exit pinned to {item.Content}. Reconnect to apply."
            : "Tor exit set to any country. Reconnect to apply.");
    }

    private async void Tor_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not Avalonia.Controls.Primitives.ToggleButton t) return;
        bool on = t.IsChecked == true;

        if (on && !CoreController.TorAvailable)
        {
            _loading = true;
            t.IsChecked = false;
            _loading = false;
            Append($"Tor needs this file (not found): {CoreController.TorPath}");
            await Ask("error", "Tor not found",
                $"Put tor.exe here and restart GRoute:\n\n{CoreController.TorPath}");
            return;
        }

        _store.SetTorOnion(on);
        SetVis(TorExitWrap, on);
        Append(on
            ? "Tor enabled \u2014 .onion addresses route through Tor. Reconnect to apply."
            : "Tor disabled. Reconnect to apply.");
        if (!on) _core.StopTor();
    }

    private void Startup_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not Avalonia.Controls.Primitives.ToggleButton t) return;
        bool on = t.IsChecked == true;
        try
        {
            Platform.Startup.Set(on);
            _store.SetRunAtStartup(on);
            Append(on ? "GRoute will start with Windows." : "GRoute will not start with Windows.");
        }
        catch (Exception ex)
        {
            Append("Startup: " + ex.Message);
            _loading = true;
            t.IsChecked = Platform.Startup.Enabled;
            _loading = false;
        }
    }

    private WindowIcon? _appIcon;

    private static WindowIcon? LoadAppIcon()
    {
        string[] names =
        {
            "GRoute.ico", "groute.ico", "icon.ico", "app.ico",
            "GRoute.png", "groute.png", "icon.png", "logo.png"
        };
        foreach (var n in names)
        {
            try
            {
                var uri = new Uri("avares://GRoute/Assets/" + n);
                if (!AssetLoader.Exists(uri)) continue;
                using var stream = AssetLoader.Open(uri);
                return new WindowIcon(stream);
            }
            catch
            {
            }
        }
        return null;
    }

    private void LoadAboutLogo()
    {
        if (AboutLogo is null) return;
        string[] names = { "GRouteTransparent.png", "GRoute.png", "GRouteSmall.png", "logo.png" };
        foreach (var n in names)
        {
            try
            {
                var uri = new Uri("avares://GRoute/Assets/" + n);
                if (!AssetLoader.Exists(uri)) continue;
                using var stream = AssetLoader.Open(uri);
                AboutLogo.Source = new Avalonia.Media.Imaging.Bitmap(stream);
                return;
            }
            catch
            {
            }
        }
    }

    private void LoadTitleLogo()
    {
        if (TitleLogo is null) return;
        string[] names = { "GRouteSmall.png", "GRouteTransparent.png", "GRoute.png", "logo.png" };
        foreach (var n in names)
        {
            try
            {
                var uri = new Uri("avares://GRoute/Assets/" + n);
                if (!AssetLoader.Exists(uri)) continue;
                using var stream = AssetLoader.Open(uri);
                TitleLogo.Source = new Avalonia.Media.Imaging.Bitmap(stream);
                return;
            }
            catch
            {
            }
        }
        Append("Logo not found in Assets (expected GRouteSmall.png).");
    }

    private void SetupTray()
    {
        if (_tray is not null) return;

        var appIcon = _appIcon ?? LoadAppIcon();
        if (appIcon is not null && Icon is null) Icon = appIcon;

        _trayConnect = new NativeMenuItem("Connect");
        _trayConnect.Click += (_, _) => Power_Click(this, new RoutedEventArgs());

        _trayModeProxy = new NativeMenuItem("Local proxy only") { ToggleType = NativeMenuItemToggleType.Radio };
        _trayModeProxy.Click += (_, _) => SetModeFromTray("proxy");
        _trayModeSystem = new NativeMenuItem("System proxy") { ToggleType = NativeMenuItemToggleType.Radio };
        _trayModeSystem.Click += (_, _) => SetModeFromTray("system");
        _trayModeTun = new NativeMenuItem("TUN (all traffic)") { ToggleType = NativeMenuItemToggleType.Radio };
        _trayModeTun.Click += (_, _) => SetModeFromTray("tun");

        var modeMenu = new NativeMenuItem("Mode")
        {
            Menu = new NativeMenu { Items = { _trayModeProxy, _trayModeSystem, _trayModeTun } }
        };

        var show = new NativeMenuItem("Show GRoute");
        show.Click += (_, _) => ShowFromTray();
        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => ShutdownAndClose();

        _tray = new TrayIcon
        {
            ToolTipText = "GRoute",
            IsVisible = true,
            Icon = appIcon,
            Menu = new NativeMenu
            {
                Items = { _trayConnect, modeMenu, new NativeMenuItemSeparator(), show, exit }
            }
        };
        _tray.Clicked += (_, _) => ShowFromTray();

        UpdateTrayMenu();
        UpdateTrayModeChecks();
    }

    private void HideToTray()
    {
        if (_tray is null)
        {
            WindowState = WindowState.Minimized;
            return;
        }
        Hide();
    }

    private readonly Dictionary<Button, DispatcherTimer> _iconActTimers = new();

    private DispatcherTimer? _knobTimer;
    private double _knobPhase;

    private void SetMenuHover(bool on)
    {
        if (MenuButton is null) return;
        if (on)
        {
            if (!MenuButton.Classes.Contains("hovered")) MenuButton.Classes.Add("hovered");
        }
        else
        {
            MenuButton.Classes.Remove("hovered");
        }
        AnimateKnobs(on ? 1 : 0);
    }

    private void MenuBtn_Entered(object? sender, PointerEventArgs e)
    {
        if (_pillPressed is not null) return;
        SetMenuHover(true);
    }

    private void MenuBtn_Exited(object? sender, PointerEventArgs e)
    {
        if (_pillPressed is not null) return;
        SetMenuHover(false);
    }

    private void AnimateKnobs(double toPhase)
    {
        if (MenuKnob1 is null) return;
        _knobTimer?.Stop();
        double from = _knobPhase;
        var t0 = DateTime.UtcNow;
        var knob = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _knobTimer = knob;
        knob.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_knobTimer, knob))
            {
                knob.Stop();
                return;
            }
            double p = (DateTime.UtcNow - t0).TotalMilliseconds / 300.0;
            if (p >= 1)
            {
                _knobPhase = toPhase;
                SetKnobs(toPhase);
                knob.Stop();
                _knobTimer = null;
                return;
            }
            double ease = 1 - Math.Pow(1 - p, 3);
            _knobPhase = from + (toPhase - from) * ease;
            SetKnobs(_knobPhase);
        };
        knob.Start();
    }

    private void SetKnobs(double phase)
    {
        double x = -4.5 + 9.0 * phase;
        MenuKnob1.RenderTransform = new TranslateTransform(x, 0);
        MenuKnob2.RenderTransform = new TranslateTransform(-x, 0);
        MenuKnob3.RenderTransform = new TranslateTransform(x, 0);
    }

    private readonly HashSet<Button> _iconActState = new();
    private readonly HashSet<Button> _iconHeld = new();
    private Button? _pillPressed;
    private DateTime _pillSnapUntil = DateTime.MinValue;
    private Button? _pillIntent;
    private DispatcherTimer? _pillIntentTimer;

    private void QueuePillExpand(Button b)
    {
        _pillIntentTimer?.Stop();
        _pillIntent = b;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };
        _pillIntentTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!ReferenceEquals(_pillIntentTimer, timer)) return;
            _pillIntentTimer = null;
            var target = _pillIntent;
            _pillIntent = null;
            if (target is null) return;
            if (_pillPressed is not null) return;
            if (!_iconActState.Add(target)) return;
            ExpandPill(target);
        };
        timer.Start();
    }

    private void CancelPillExpand(Button b)
    {
        if (!ReferenceEquals(_pillIntent, b)) return;
        _pillIntent = null;
        _pillIntentTimer?.Stop();
        _pillIntentTimer = null;
    }

    private Button? PillOwner(object? source)
    {
        var v = source as Visual;
        while (v is not null)
        {
            if (v is Button b && (b.Classes.Contains("iconact") || ReferenceEquals(b, MenuButton))) return b;
            v = v.GetVisualParent();
        }
        return null;
    }

    private void ExpandPill(Button b)
    {
        if (b == QuickButton || b == KillQuickButton || b == SearchToggle || b == SortBtn)
            AnimateActionWidth(b, IconActionTargetWidth(b), expanded: true);
        else
            AnimateRowIcon(b, RowIconTargetWidth(b), expanded: true);
    }

    private void CollapsePill(Button b)
    {
        if (b == QuickButton || b == KillQuickButton)
            AnimateActionWidth(b, 26, expanded: false);
        else if (b == SearchToggle || b == SortBtn)
            AnimateActionWidth(b, 32, expanded: false);
        else
            AnimateRowIcon(b, 30, expanded: false);
    }

    private static bool OnBareSurface(object? source)
    {
        var v = source as Visual;
        while (v is not null)
        {
            if (v is Button or RepeatButton or ToggleButton or RadioButton or CheckBox
                or TextBox or ComboBox or ComboBoxItem or MenuItem or Slider or Thumb)
                return false;
            if (v is Border bd && bd.Classes.Count > 0) return false;
            v = v.GetVisualParent();
        }
        return true;
    }

    private void DropTextFocus(object? sender, PointerPressedEventArgs e)
    {
        var focused = FocusManager?.GetFocusedElement();
        if (focused is not TextBox box) return;

        var v = e.Source as Visual;
        while (v is not null)
        {
            if (ReferenceEquals(v, box)) return;
            v = v.GetVisualParent();
        }

        box.ClearSelection();
        AppSurface?.Focus();
    }

    private void VeinStrike(object? sender, PointerPressedEventArgs e)
    {
        if (Veins is null || !Veins.IsVisible) return;
        if (!OnBareSurface(e.Source)) return;

        double w = Veins.Bounds.Width;
        double h = Veins.Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var p = e.GetPosition(Veins);
        if (p.X < 0 || p.Y < 0 || p.X > w || p.Y > h) return;

        VeinField.Strike(p.X / w, p.Y / w);
    }

    private void PillPressed(object? sender, PointerPressedEventArgs e)
    {
        _pillPressed = PillOwner(e.Source);
    }

    private void PillReleased(object? sender, PointerReleasedEventArgs e)
    {
        var b = _pillPressed;
        _pillPressed = null;
        if (b is null) return;
        var p = e.GetPosition(b);
        bool inside = p.X >= 0 && p.Y >= 0 && p.X <= b.Bounds.Width && p.Y <= b.Bounds.Height;
        if (ReferenceEquals(b, MenuButton))
        {
            SetMenuHover(inside);
            return;
        }
        if (inside)
        {
            if (_iconActState.Add(b)) ExpandPill(b);
        }
        else if (_iconActState.Remove(b))
        {
            CollapsePill(b);
        }
    }

    private static (AvPath? Icon, TextBlock? Label) RowIconParts(Button b)
    {
        AvPath? icon = null;
        TextBlock? label = null;
        if (b.Content is Panel p)
        {
            foreach (var child in p.Children)
            {
                if (child is AvPath path) icon = path;
                else if (child is TextBlock text) label = text;
            }
        }
        return (icon, label);
    }

    private static double RowIconTargetWidth(Button b)
    {
        var (_, label) = RowIconParts(b);
        if (label is null || string.IsNullOrEmpty(label.Text)) return 30;
        var face = new Typeface(label.FontFamily, label.FontStyle, label.FontWeight);
        var ft = new FormattedText(label.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                   face, label.FontSize, Brushes.White);
        return Math.Ceiling(ft.Width) + 24;
    }

    private void SnapRowIcon(Button b, double to)
    {
        if (_iconActTimers.TryGetValue(b, out var prev))
        {
            prev.Stop();
            _iconActTimers.Remove(b);
        }
        SetPillFill(b, true);
        var (icon, label) = RowIconParts(b);
        if (icon is not null) icon.Opacity = 0;
        if (label is not null)
        {
            label.IsVisible = true;
            label.Opacity = 1;
        }
        b.Width = to;
    }

    private string _mProto = "vless";
    private string? _editingId;
    private string _mNet = "tcp";
    private string _mSec = "none";

    private readonly HashSet<ComboBox> _comboHooked = new();

    private void HookCombos(Visual root)
    {
        foreach (var cb in root.GetVisualDescendants().OfType<ComboBox>())
        {
            if (!_comboHooked.Add(cb)) continue;
            cb.PropertyChanged += ComboOpenChanged;
        }
    }

    private void ComboOpenChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ComboBox.IsDropDownOpenProperty) return;
        if (sender is not ComboBox cb || !cb.IsDropDownOpen) return;

        var popup = cb.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        if (popup?.Child is not Control body) return;

        var saved = body.Transitions;
        body.Transitions = null;
        body.Opacity = 0;
        body.RenderTransformOrigin = RelativePoint.Parse("50%,0%");
        body.RenderTransform = TransformOperations.Parse("translateY(-10px)");

        body.Transitions = saved ?? new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };

        Dispatcher.UIThread.Post(() =>
        {
            body.Opacity = 1;
            body.RenderTransform = TransformOperations.Parse("translateY(0px)");
        }, DispatcherPriority.Loaded);
    }

    private void OpenManual_Click(object? sender, RoutedEventArgs e)
    {
        SetAddMenu(false);
        ResetManualForm();
        _editingId = null;
        if (ManualTitle is not null) ManualTitle.Text = Strings.Get(_lang, "add_manually");
        ShowTab("manual");
        Dispatcher.UIThread.Post(() =>
        {
            if (ManualPanel is not null) HookCombos(ManualPanel);
        }, DispatcherPriority.Loaded);
    }

    private void ManualBack_Click(object? sender, RoutedEventArgs e)
    {
        _editingId = null;
        GoBack();
    }

    private void EditConfig_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string id) return;
        e.Handled = true;
        var cfg = _store.Configs.FirstOrDefault(x => x.Id == id);
        if (cfg is null || cfg.Locked) return;

        ResetManualForm();
        _editingId = id;
        FillManualForm(cfg);
        if (ManualTitle is not null) ManualTitle.Text = Strings.Get(_lang, "edit_config");
        ShowTab("manual");
        Dispatcher.UIThread.Post(() =>
        {
            if (ManualPanel is not null) HookCombos(ManualPanel);
        }, DispatcherPriority.Loaded);
    }

    private void FillManualForm(ProxyConfig c)
    {
        _mProto = string.IsNullOrEmpty(c.Protocol) ? "vless" : c.Protocol;
        _mNet = string.IsNullOrEmpty(c.Network) ? "tcp" : c.Network;
        _mSec = string.IsNullOrEmpty(c.Security) ? "none" : c.Security;

        SelectByTag(MProtocol, _mProto);
        SelectByTag(MNetwork, _mNet);
        CheckSeg(_mSec, ScNone, ScTls, ScReality);

        Set(MName, c.Name);
        Set(MAddress, c.Address);
        Set(MPort, c.Port.ToString());
        Set(MUuid, c.Uuid);
        Set(MEncryption, c.Encryption);
        Set(MAlterId, c.AlterId.ToString());
        Set(MPassword, c.Password);
        Set(MMethod, c.Method);
        Set(MUsername, c.Username);
        Set(MObfs, c.Method);
        Set(MObfsPw, c.ObfsPassword);
        Set(MPrivKey, c.PrivateKey);
        Set(MPeerKey, c.PublicKey);
        Set(MPsk, c.PresharedKey);
        Set(MWgAddress, c.WgAddress);
        Set(MMtu, c.Mtu > 0 ? c.Mtu.ToString() : "");
        Set(MReserved, c.Reserved);
        Set(MPath, c.Path);
        Set(MHost, c.Host);
        Set(MServiceName, c.ServiceName);
        Set(MMode, c.Mode);
        Set(MHeaderType, c.HeaderType);
        Set(MSni, c.Sni);
        Set(MPbk, c.PublicKey);
        Set(MSid, c.ShortId);

        SelectByTag(MFlow, c.Flow);
        SelectByTag(MAlpn, c.Alpn);
        SelectByTag(MFingerprint, c.Fingerprint);
        if (MInsecure is not null) MInsecure.IsChecked = c.AllowInsecure;

        SyncManualFields();
    }

    private static void Set(TextBox? b, string? v)
    {
        if (b is not null) b.Text = v ?? "";
    }

    private void ManualProto_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox b || b.SelectedItem is not ComboBoxItem it) return;
        if (it.Tag is not string t || t == _mProto) return;
        _mProto = t;
        SyncManualFields();
    }

    private void ManualNet_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox b || b.SelectedItem is not ComboBoxItem it) return;
        if (it.Tag is not string t || t == _mNet) return;
        _mNet = t;
        SyncManualFields();
    }

    private void ManualField_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton r || r.Tag is not string t) return;
        if (r.GroupName == "Sec") _mSec = t;
        SyncManualFields();
    }

    private void ResetManualForm()
    {
        _editingId = null;
        _mProto = "vless";
        _mNet = "tcp";
        _mSec = "none";
        SelectByTag(MProtocol, _mProto);
        SelectByTag(MNetwork, _mNet);
        CheckSeg(_mSec, ScNone, ScTls, ScReality);
        foreach (var b in ManualBoxes()) b.Text = "";
        if (MPort is not null) MPort.Text = "443";
        if (MEncryption is not null) MEncryption.Text = "";
        if (MAlterId is not null) MAlterId.Text = "";
        if (MInsecure is not null) MInsecure.IsChecked = false;
        SelectByTag(MFlow, "");
        SelectByTag(MAlpn, "");
        SelectByTag(MFingerprint, "");
        SyncManualFields();
    }

    private TextBox[] ManualBoxes() => new[]
    {
        MName, MAddress, MPort, MUuid, MPassword, MMethod, MEncryption, MAlterId,
        MUsername, MObfs, MObfsPw,
        MPrivKey, MPeerKey, MPsk, MWgAddress, MMtu, MReserved,
        MPath, MHost, MServiceName, MMode, MHeaderType,
        MSni, MPbk, MSid
    };

    private static void Row(Control? row, bool on)
    {
        if (row is not null) row.IsVisible = on;
    }

    private void SyncManualFields()
    {
        bool wg = _mProto == "wireguard";
        bool vless = _mProto == "vless";
        bool vmess = _mProto == "vmess";
        bool trojan = _mProto == "trojan";
        bool ss = _mProto == "shadowsocks";
        bool hy2 = _mProto == "hysteria2";
        bool prox = _mProto is "http" or "socks";
        bool raw = wg || hy2 || prox;

        Row(MUuidRow, vless || vmess);
        Row(MFlowRow, vless || trojan);
        Row(MEncryptionRow, vless || vmess);
        Row(MAlterIdRow, vmess);
        Row(MPasswordRow, trojan || ss || hy2 || prox);
        Row(MMethodRow, ss || hy2);
        Row(MUsernameRow, prox);
        Row(MObfsRow, hy2);
        Row(MObfsPwRow, hy2);
        Row(MPrivKeyRow, wg);
        Row(MPeerKeyRow, wg);
        Row(MPskRow, wg);
        Row(MWgAddressRow, wg);
        Row(MMtuRow, wg);
        Row(MReservedRow, wg);

        Row(TransportCard, !raw);
        Row(TlsCard, !raw && !hy2);

        Row(MPathRow, !raw && _mNet is "ws" or "http" or "kcp" or "quic" or "httpupgrade" or "xhttp");
        Row(MHostRow, !raw && _mNet is "ws" or "http" or "tcp" or "httpupgrade" or "xhttp");
        Row(MServiceNameRow, !raw && _mNet == "grpc");
        Row(MModeRow, !raw && (_mNet == "grpc" || _mNet == "xhttp"));
        Row(MHeaderTypeRow, !raw && _mNet is "tcp" or "kcp" or "quic");

        bool tls = _mSec == "tls";
        bool reality = _mSec == "reality";
        Row(MSniRow, tls || reality);
        Row(MAlpnRow, tls);
        Row(MFingerprintRow, tls || reality);
        Row(MPbkRow, reality);
        Row(MSidRow, reality);
        Row(MInsecureRow, tls);
    }

    private static string Txt(TextBox? b) => (b?.Text ?? "").Trim();

    private static string ComboTag(ComboBox? b)
        => b?.SelectedItem is ComboBoxItem it && it.Tag is string t ? t : "";

    private async void ManualSave_Click(object? sender, RoutedEventArgs e)
    {
        string host = Txt(MAddress);
        if (host.Length == 0)
        {
            await Ask("warning", "Missing address", "Enter a server address.");
            return;
        }
        if (!int.TryParse(Txt(MPort), out int port) || port <= 0 || port > 65535)
        {
            await Ask("warning", "Invalid port", "Enter a port between 1 and 65535.");
            return;
        }

        var existing = _editingId is null
            ? null
            : _store.Configs.FirstOrDefault(x => x.Id == _editingId);

        var cfg = existing ?? new ProxyConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Source = ConfigSource.Personal
        };

        cfg.Name = Txt(MName).Length > 0 ? Txt(MName) : host;
        cfg.Protocol = _mProto;
        cfg.Address = host;
        cfg.Port = port;
        cfg.Uuid = "";
        cfg.Flow = "";
        cfg.Password = "";
        cfg.Method = "";
        cfg.Username = "";
        cfg.ObfsPassword = "";

        if (_mProto is "vless" or "vmess")
        {
            cfg.Uuid = Txt(MUuid);
            cfg.Encryption = Txt(MEncryption).Length > 0
                ? Txt(MEncryption)
                : (_mProto == "vless" ? "none" : "auto");
        }
        if (_mProto == "vmess" && int.TryParse(Txt(MAlterId), out int aid)) cfg.AlterId = aid;
        if (_mProto is "vless" or "trojan") cfg.Flow = ComboTag(MFlow);
        if (_mProto is "trojan" or "shadowsocks" or "hysteria2" or "http" or "socks")
            cfg.Password = Txt(MPassword);
        if (_mProto == "shadowsocks") cfg.Method = Txt(MMethod);
        if (_mProto is "http" or "socks") cfg.Username = Txt(MUsername);
        if (_mProto == "hysteria2")
        {
            cfg.Method = Txt(MObfs);
            cfg.ObfsPassword = Txt(MObfsPw);
        }

        if (_mProto is "wireguard" or "hysteria2" or "http" or "socks")
        {
            cfg.PrivateKey = Txt(MPrivKey);
            cfg.PublicKey = Txt(MPeerKey);
            cfg.PresharedKey = Txt(MPsk);
            cfg.WgAddress = Txt(MWgAddress);
            cfg.Reserved = Txt(MReserved);
            if (int.TryParse(Txt(MMtu), out int mtu)) cfg.Mtu = mtu;
            if (_mProto == "hysteria2") cfg.Sni = Txt(MSni);
        }
        else
        {
            cfg.Network = _mNet;
            cfg.Security = _mSec == "none" ? "" : _mSec;
            cfg.Path = Txt(MPath);
            cfg.Host = Txt(MHost);
            cfg.ServiceName = Txt(MServiceName);
            cfg.Mode = Txt(MMode);
            cfg.HeaderType = Txt(MHeaderType);
            cfg.Sni = Txt(MSni);
            cfg.Alpn = ComboTag(MAlpn);
            cfg.Fingerprint = ComboTag(MFingerprint);
            cfg.PublicKey = Txt(MPbk);
            cfg.ShortId = Txt(MSid);
            cfg.AllowInsecure = MInsecure?.IsChecked == true;
        }

        if (existing is null)
        {
            _store.Add(cfg);
            Append($"Added {cfg.Name}.");
        }
        else
        {
            Append($"Updated {cfg.Name}.");
        }
        _store.SaveNow();
        ReloadServers();
        _editingId = null;
        GoBack();
    }

    private readonly Dictionary<Button, DispatcherTimer> _copyFlash = new();

    private void FlashCopied(Button? b)
    {
        if (b is null) return;
        var (_, label) = RowIconParts(b);
        if (label is null) return;

        if (_copyFlash.TryGetValue(b, out var old))
        {
            old.Stop();
            _copyFlash.Remove(b);
        }

        if (label.Transitions is null)
        {
            label.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(130),
                    Easing = new CubicEaseOut()
                }
            };
        }

        SwapPillLabel(b, label, Strings.Get(_lang, "copied"));

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _copyFlash[b] = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_copyFlash.TryGetValue(b, out var cur) || !ReferenceEquals(cur, timer)) return;
            _copyFlash.Remove(b);
            SwapPillLabel(b, label, Strings.Get(_lang, "copy"));
        };
        timer.Start();
    }

    private void SwapPillLabel(Button b, TextBlock label, string text)
    {
        label.Opacity = 0;
        DispatcherTimer.RunOnce(() =>
        {
            label.Text = text;
            label.Opacity = 1;
            if (_iconActState.Contains(b)) AnimateRowIcon(b, RowIconTargetWidth(b), expanded: true);
        }, TimeSpan.FromMilliseconds(130));
    }

    private void RowIconEnter(object? sender, PointerEventArgs e)
    {
        if (_pillPressed is not null) return;
        if (sender is not Button b) return;
        if (DateTime.UtcNow < _pillSnapUntil)
        {
            if (_iconActState.Add(b)) SnapRowIcon(b, RowIconTargetWidth(b));
            return;
        }
        QueuePillExpand(b);
    }

    private void RowIconLeave(object? sender, PointerEventArgs e)
    {
        if (_pillPressed is not null) return;
        if (sender is not Button b) return;
        CancelPillExpand(b);
        if (!_iconActState.Remove(b)) return;
        AnimateRowIcon(b, 30, expanded: false);
    }

    private static void SetPillFill(Button b, bool expanded)
    {
        if (expanded)
        {
            if (!b.Classes.Contains("expanded")) b.Classes.Add("expanded");
        }
        else
        {
            b.Classes.Remove("expanded");
        }
    }

    private void AnimateRowIcon(Button b, double to, bool expanded)
    {
        SetPillFill(b, expanded);
        var (icon, label) = RowIconParts(b);
        if (_iconActTimers.TryGetValue(b, out var prev)) prev.Stop();
        if (label is not null)
        {
            label.IsVisible = true;
            if (expanded) label.Opacity = 0;
        }
        if (!expanded && icon is not null) icon.Opacity = 0;
        double from = double.IsNaN(b.Width) ? b.Bounds.Width : b.Width;
        if (from <= 0) from = 30;
        double iconFrom = icon?.Opacity ?? 1;
        double labelFrom = label?.Opacity ?? 0;
        double iconTo = expanded ? 0 : 1;
        double labelTo = expanded ? 1 : 0;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _iconActTimers[b] = timer;
        timer.Tick += (_, _) =>
        {
            if (!_iconActTimers.TryGetValue(b, out var cur) || !ReferenceEquals(cur, timer))
            {
                timer.Stop();
                return;
            }
            double p = (DateTime.UtcNow - t0).TotalMilliseconds / (expanded ? 300.0 : 180.0);
            if (p >= 1)
            {
                b.Width = to;
                if (icon is not null) icon.Opacity = iconTo;
                if (label is not null)
                {
                    label.Opacity = labelTo;
                    if (!expanded) label.IsVisible = false;
                }
                timer.Stop();
                _iconActTimers.Remove(b);
                return;
            }
            double ease = 1 - Math.Pow(1 - p, 3);
            b.Width = from + (to - from) * ease;
            double outP = Math.Min(1.0, p / 0.3);
            double inP = Math.Min(1.0, p / 0.55);
            if (expanded)
            {
                if (icon is not null) icon.Opacity = iconFrom * (1.0 - outP);
                if (label is not null) label.Opacity = labelFrom + (labelTo - labelFrom) * inP;
            }
            else
            {
                if (label is not null) label.Opacity = labelFrom * (1.0 - outP);
                if (icon is not null) icon.Opacity = iconFrom + (iconTo - iconFrom) * inP;
            }
        };
        timer.Start();
    }

    private double IconActionTargetWidth(Button b)
    {
        Control? label = b == QuickButton ? QuickLabel
            : b == KillQuickButton ? KillLabel
            : b == SearchToggle ? SearchTgLabel
            : SortTgLabel;
        string text = (label as TextBlock)?.Text ?? b.Tag as string ?? "";
        if (string.IsNullOrEmpty(text)) return 26;
        var tb = label as TextBlock;
        var face = tb is not null
            ? new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight)
            : new Typeface(FontFamily.Default, weight: FontWeight.SemiBold);
        double size = tb?.FontSize ?? 12;
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, face, size, Brushes.White);
        return Math.Ceiling(ft.Width) + 28;
    }

    private void IconActionEnter(object? sender, PointerEventArgs e)
    {
        if (_pillPressed is not null) return;
        if (sender is not Button b) return;
        if (_iconHeld.Contains(b)) return;
        QueuePillExpand(b);
    }

    private void IconActionLeave(object? sender, PointerEventArgs e)
    {
        if (_pillPressed is not null) return;
        if (sender is not Button b) return;
        if (_iconHeld.Contains(b)) return;
        CancelPillExpand(b);
        if (!_iconActState.Remove(b)) return;
        AnimateActionWidth(b, b == QuickButton || b == KillQuickButton ? 26 : 32, expanded: false);
    }

    private void SettleIconAction(Button b)
    {
        if (b != QuickButton && b != KillQuickButton && b != SearchToggle && b != SortBtn) return;
        if (b.IsPointerOver)
        {
            if (_iconActState.Add(b)) AnimateActionWidth(b, IconActionTargetWidth(b), expanded: true);
        }
        else
        {
            if (_iconActState.Remove(b)) AnimateActionWidth(b, b == QuickButton || b == KillQuickButton ? 26 : 32, expanded: false);
        }
    }

    private void AnimateActionWidth(Button b, double to, bool expanded)
    {
        SetPillFill(b, expanded);
        Control? icon = b == QuickButton ? QuickIcon
            : b == KillQuickButton ? KillIcon
            : b == SearchToggle ? SearchTgIcon
            : SortTgIcon;
        Control? label = b == QuickButton ? QuickLabel
            : b == KillQuickButton ? KillLabel
            : b == SearchToggle ? SearchTgLabel
            : SortTgLabel;
        if (_iconActTimers.TryGetValue(b, out var prev)) prev.Stop();
        if (label is not null) label.IsVisible = true;
        if (expanded && label is not null) label.Opacity = 0;
        if (!expanded && icon is not null) icon.Opacity = 0;
        double from = double.IsNaN(b.Width) ? b.Bounds.Width : b.Width;
        double iconFrom = icon?.Opacity ?? 1;
        double labelFrom = label?.Opacity ?? 0;
        double iconTo = expanded ? 0 : 1;
        double labelTo = expanded ? 1 : 0;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _iconActTimers[b] = timer;
        timer.Tick += (_, _) =>
        {
            if (!_iconActTimers.TryGetValue(b, out var cur) || !ReferenceEquals(cur, timer))
            {
                timer.Stop();
                return;
            }
            double p = (DateTime.UtcNow - t0).TotalMilliseconds / 340.0;
            if (p >= 1)
            {
                b.Width = to;
                if (icon is not null) icon.Opacity = iconTo;
                if (label is not null)
                {
                    label.Opacity = labelTo;
                    if (!expanded) label.IsVisible = false;
                }
                if (_iconActTimers.TryGetValue(b, out var t)) { t.Stop(); _iconActTimers.Remove(b); }
                return;
            }
            double ease = 1 - Math.Pow(1 - p, 3);
            b.Width = from + (to - from) * ease;

            double outP = Math.Min(1.0, p / 0.3);
            double inP = Math.Min(1.0, p / 0.55);
            if (expanded)
            {
                if (icon is not null) icon.Opacity = iconFrom * (1.0 - outP);
                if (label is not null) label.Opacity = labelFrom + (labelTo - labelFrom) * inP;
            }
            else
            {
                if (label is not null) label.Opacity = labelFrom * (1.0 - outP);
                if (icon is not null) icon.Opacity = iconFrom + (iconTo - iconFrom) * inP;
            }
        };
        timer.Start();
    }

    private DispatcherTimer? _sheetTimer;
    private readonly TranslateTransform _sheetT = new();
    private bool _sheetDragging;
    private double _sheetDragStartY;
    private double _sheetStartOffset;

    private double SheetOffscreen() =>
        QuickSheet.Bounds.Height > 40 ? QuickSheet.Bounds.Height + 24 : 300;

    private void AnimateSheet(double toY, double toDim, double ms, bool easeIn, Action? done = null)
    {
        _sheetTimer?.Stop();
        double fromY = _sheetT.Y;
        double fromDim = QuickDim.Opacity;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        _sheetTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_sheetTimer, timer))
            {
                timer.Stop();
                return;
            }
            double p = (DateTime.UtcNow - t0).TotalMilliseconds / ms;
            if (p >= 1.0)
            {
                _sheetT.Y = toY;
                QuickDim.Opacity = toDim;
                timer.Stop();
                _sheetTimer = null;
                done?.Invoke();
                return;
            }
            double ease = easeIn ? p * p * p : 1.0 - Math.Pow(1.0 - p, 3.0);
            _sheetT.Y = fromY + (toY - fromY) * ease;
            QuickDim.Opacity = fromDim + (toDim - fromDim) * ease;
        };
        timer.Start();
    }

    private void OpenQuickSettings(object? sender, RoutedEventArgs e)
    {
        RefreshQuickChecks();
        _sheetTimer?.Stop();
        _sheetTimer = null;
        _sheetT.Y = SheetOffscreen();
        QuickDim.Opacity = 0;
        SetVis(QuickOverlay, true);
        AnimateSheet(0, 1, 260, easeIn: false);
    }

    private void AttachWrapBounce(Button b, Border wrap)
    {
        wrap.RenderTransform = TransformOperations.Parse("scale(1)");
        wrap.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };
        bool down = false;
        b.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (down) return;
            if (!e.GetCurrentPoint(b).Properties.IsLeftButtonPressed) return;
            down = true;
            _iconHeld.Add(b);
            wrap.RenderTransform = TransformOperations.Parse("scale(0.94)");
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        b.AddHandler(PointerReleasedEvent, (_, _) =>
        {
            if (!down) return;
            down = false;
            _iconHeld.Remove(b);
            wrap.RenderTransform = TransformOperations.Parse("scale(1)");
            Dispatcher.UIThread.Post(() => SettleIconAction(b), DispatcherPriority.Background);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        b.PointerCaptureLost += (_, _) =>
        {
            if (!down) return;
            down = false;
            _iconHeld.Remove(b);
            wrap.RenderTransform = TransformOperations.Parse("scale(1)");
            Dispatcher.UIThread.Post(() => SettleIconAction(b), DispatcherPriority.Background);
        };
        b.PointerExited += (_, _) =>
        {
            if (down) return;
            wrap.RenderTransform = TransformOperations.Parse("scale(1)");
        };
    }

    private readonly Dictionary<Control, int> _promptGen = new();

    private void AnimatePromptOut(Control overlay, Control card)
    {
        _promptGen.TryGetValue(overlay, out int g);
        int gen = g + 1;
        _promptGen[overlay] = gen;
        overlay.Opacity = 0;
        card.RenderTransform = TransformOperations.Parse("scale(0.96) translateY(8px)");
        DispatcherTimer.RunOnce(() =>
        {
            if (_promptGen.TryGetValue(overlay, out int now) && now == gen)
                overlay.IsVisible = false;
        }, TimeSpan.FromMilliseconds(200));
    }

    private void AnimatePromptIn(Control overlay, Control card)
    {
        _promptGen.TryGetValue(overlay, out int g);
        _promptGen[overlay] = g + 1;
        overlay.Transitions = null;
        card.Transitions = null;
        overlay.Opacity = 0;
        card.RenderTransform = TransformOperations.Parse("scale(0.94) translateY(10px)");
        Dispatcher.UIThread.Post(() =>
        {
            overlay.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(170),
                    Easing = new CubicEaseOut()
                }
            };
            card.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(210),
                    Easing = new CubicEaseOut()
                }
            };
            overlay.Opacity = 1;
            card.RenderTransform = TransformOperations.Parse("scale(1) translateY(0px)");
        }, DispatcherPriority.Render);
    }

    private void SheetDragStart(object? sender, PointerPressedEventArgs e)
    {
        _sheetTimer?.Stop();
        _sheetTimer = null;
        _sheetDragging = true;
        _sheetDragStartY = e.GetPosition(this).Y;
        _sheetStartOffset = _sheetT.Y;
        e.Pointer.Capture(QuickSheet);
    }

    private void SheetDragMove(object? sender, PointerEventArgs e)
    {
        if (!_sheetDragging) return;
        double dy = e.GetPosition(this).Y - _sheetDragStartY;
        _sheetT.Y = Math.Max(0, _sheetStartOffset + dy);
    }

    private void SheetDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        if (!_sheetDragging) return;
        _sheetDragging = false;
        e.Pointer.Capture(null);
        if (_sheetT.Y > QuickSheet.Bounds.Height * 0.32) CloseQuickSettings();
        else AnimateSheet(0, 1, 180, easeIn: false);
    }

    private void RefreshQuickChecks()
    {
        if (QSCheckProxy is null) return;
        string mode = _store.Mode;
        SetVis(QSCheckProxy, mode == "proxy");
        SetVis(QSCheckSystem, mode == "system");
        SetVis(QSCheckTun, mode == "tun");
    }

    private void CloseQuickSettings() =>
        AnimateSheet(SheetOffscreen(), 0, 220, easeIn: true, () => SetVis(QuickOverlay, false));

    private void QuickDim_Click(object? sender, PointerPressedEventArgs e) => CloseQuickSettings();

    private void QuickMode_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string mode) return;
        SelectModeByUser(mode);
        CloseQuickSettings();
    }

    private async void SelectModeByUser(string mode)
    {
        if (OperatingSystem.IsLinux() && mode == "system")
            Append("On Linux most browsers ignore the system proxy setting. "
                   + "Use TUN mode, or point the browser at 127.0.0.1:"
                   + ConfigBuilder.HttpPort + " yourself.");

        if (mode == "tun" && !Platform.Elevation.IsAdministrator())
        {
            bool relaunch = await Ask("warning",
                Strings.Get(_lang, "title_admin"),
                Strings.Get(_lang, "tun_admin_prompt"),
                withCancel: true);
            RefreshQuickChecks();
            if (!relaunch) return;

            _store.SetMode("tun");
            _store.SaveNow();
            if (Platform.Elevation.RelaunchAsAdmin("--autoconnect"))
            {
                _exiting = true;
                if (Application.Current?.ApplicationLifetime
                    is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
            }
            else
            {
                _store.SetMode("system");
                Append(OperatingSystem.IsLinux()
                    ? "Could not elevate. Start GRoute with sudo to use TUN mode."
                    : "Elevation was cancelled \u2014 run GRoute as administrator to use TUN mode.");
                RefreshQuickChecks();
                UpdateTrayModeChecks();
            }
            return;
        }
        _store.SetMode(mode);
        RefreshQuickChecks();
        UpdateTrayModeChecks();
        RefreshKillVisual();
    }

    private void KillQuickButton_Click(object? sender, RoutedEventArgs e)
    {
        SetKillSwitchByUser(!_store.KillSwitch);
    }

    private async void SetKillSwitchByUser(bool on)
    {
        if (on && !Platform.Elevation.IsAdministrator())
        {
            bool relaunch = await Ask("warning",
                Strings.Get(_lang, "title_admin"),
                Strings.Get(_lang, "kill_admin_prompt"),
                withCancel: true);
            _loading = true;
            if (KillToggle is not null) KillToggle.IsChecked = _store.KillSwitch;
            _loading = false;
            RefreshKillVisual();
            if (!relaunch) return;
            _store.SetKillSwitch(true);
            _store.SaveNow();
            if (Platform.Elevation.RelaunchAsAdmin(_core.IsRunning ? "--autoconnect" : null))
            {
                _exiting = true;
                if (Application.Current?.ApplicationLifetime
                    is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
            }
            else
            {
                _store.SetKillSwitch(false);
                Append("Elevation was cancelled — run GRoute as administrator to use the kill switch.");
                RefreshKillVisual();
            }
            return;
        }
        _store.SetKillSwitch(on);
        try
        {
            if (on) GRoute.Windows.Core.KillSwitch.Engage();
            else GRoute.Windows.Core.KillSwitch.Release();
            Append(on ? "Kill switch engaged." : "Kill switch released.");
        }
        catch (Exception ex)
        {
            Append("Kill switch: " + ex.Message);
        }
        _loading = true;
        if (KillToggle is not null) KillToggle.IsChecked = on;
        _loading = false;
        RefreshKillVisual();
        Append(on ? "Kill switch on." : "Kill switch off.");
    }

    private readonly ScaleTransform _pingScale = new(1.0, 1.0);
    private DispatcherTimer? _pingBounce;
    private double _pingFrom = 1.0;
    private double _pingTo = 1.0;
    private double _pingMs;
    private DateTime _pingT0;

    private void PingPress(object? sender, PointerPressedEventArgs e) => BouncePing(0.95, 70.0);

    private void PingRelease(object? sender, PointerReleasedEventArgs e) => BouncePing(1.0, 120.0);

    private void PingExit(object? sender, PointerEventArgs e) => BouncePing(1.0, 120.0);

    private void BouncePing(double target, double ms)
    {
        if (PingButton is null) return;
        if (!ReferenceEquals(PingButton.RenderTransform, _pingScale))
            PingButton.RenderTransform = _pingScale;

        if (Math.Abs(_pingScale.ScaleX - target) < 0.001)
        {
            _pingBounce?.Stop();
            _pingBounce = null;
            return;
        }

        _pingFrom = _pingScale.ScaleX;
        _pingTo = target;
        _pingMs = ms;
        _pingT0 = DateTime.UtcNow;

        _pingBounce?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        _pingBounce = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_pingBounce, timer))
            {
                timer.Stop();
                return;
            }
            double p = (DateTime.UtcNow - _pingT0).TotalMilliseconds / _pingMs;
            if (p >= 1.0)
            {
                _pingScale.ScaleX = _pingTo;
                _pingScale.ScaleY = _pingTo;
                timer.Stop();
                _pingBounce = null;
                return;
            }
            double e2 = 1.0 - Math.Pow(1.0 - p, 3.0);
            double v = _pingFrom + (_pingTo - _pingFrom) * e2;
            _pingScale.ScaleX = v;
            _pingScale.ScaleY = v;
        };
        timer.Start();
    }

    private void ApplyPingTint(int ping)
    {
        if (PingIcon is not null) PingIcon.Stroke = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xAD));
        if (PingText is not null) PingText.Foreground = new SolidColorBrush(Color.FromRgb(0xB9, 0xC6, 0xE2));
    }

    private DispatcherTimer? _pingWidthTimer;
    private double _pingChrome = double.NaN;

    private double PingTextWidth(string text)
    {
        if (PingText is null) return 0;
        var face = new Typeface(PingText.FontFamily, PingText.FontStyle, PingText.FontWeight);
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                   face, PingText.FontSize, Brushes.White);
        return ft.Width;
    }

    private string PingFailText() => Strings.Get(_lang, "ping_fail");

    private string PingMs(int ms) =>
        Strings.LocalizeDigits(ms + " " + Strings.Get(_lang, "unit_ms"), _lang);

    private int _lastPingMs = int.MinValue;

    private void SetPingLabel(string text)
    {
        if (PingText is null) return;
        PingText.FlowDirection = FlowDirection.LeftToRight;
        if (PingButton is null || PingButton.Bounds.Width <= 0)
        {
            PingText.Text = text;
            return;
        }
        if (double.IsNaN(_pingChrome))
            _pingChrome = PingButton.Bounds.Width - PingTextWidth(PingText.Text ?? "");
        PingText.Text = text;
        AnimatePingWidth(Math.Ceiling(_pingChrome + PingTextWidth(text)));
        if (PingIcon is not null) PingIcon.Opacity = 1;
    }

    private void AnimatePingWidth(double target)
    {
        if (PingButton is null) return;
        _pingWidthTimer?.Stop();
        _pingWidthTimer = null;
        double from = double.IsNaN(PingButton.Width) ? PingButton.Bounds.Width : PingButton.Width;
        if (from <= 0 || Math.Abs(target - from) < 0.5)
        {
            PingButton.Width = target;
            if (PingText is not null) PingText.Opacity = 1;
            return;
        }
        PingButton.Width = from;
        if (PingText is not null) PingText.Opacity = 0;
        const double durMs = 260.0;
        double t = 0;
        var last = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _pingWidthTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_pingWidthTimer, timer))
            {
                timer.Stop();
                return;
            }
            var now = DateTime.UtcNow;
            double dt = (now - last).TotalMilliseconds;
            last = now;
            if (dt > 32) dt = 32;
            t += dt;
            double p = t / durMs;
            if (p >= 1)
            {
                PingButton.Width = target;
                if (PingText is not null) PingText.Opacity = 1;
                timer.Stop();
                _pingWidthTimer = null;
                return;
            }
            double e = 1 - Math.Pow(1 - p, 3);
            PingButton.Width = from + (target - from) * e;
            if (PingText is not null) PingText.Opacity = e;
        };
        timer.Start();
    }

    private void RefreshKillVisual()
    {
        if (KillWrap is null) return;
        bool on = _store.KillSwitch;
        var brush = new SolidColorBrush(on
            ? Color.FromRgb(0x4B, 0xF0, 0xA4)
            : Color.FromRgb(0xF5, 0xC8, 0x42));
        KillWrap.BorderBrush = brush;
        KillWrap.Opacity = 1.0;
        if (KillIcon is not null) KillIcon.Stroke = brush;
        if (KillLabel is not null) KillLabel.Foreground = brush;
    }

    private void SetModeFromTray(string mode)
    {
        if (mode == "tun" && !Platform.Elevation.IsAdministrator())
        {
            ShowFromTray();
            Append("TUN needs administrator rights — start GRoute as admin.");
            UpdateTrayModeChecks();
            return;
        }
        _store.SetMode(mode);
        UpdateTrayModeChecks();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void ShutdownAndClose()
    {
        if (_exiting) return;
        _exiting = true;
        try { _torNodes.Stop(); } catch { }
        try { _psiphon.Stop(); } catch { }
        StopDirectPolling();
        StopTun();
        StopAll();
        try { _core.StopAll(); } catch { }
        if (_tray is not null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
            _tray = null;
        }
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Close();
    }

    private void UpdateTrayMenu()
    {
        if (_trayConnect is not null)
            _trayConnect.Header = _core.IsRunning ? "Disconnect" : "Connect";
    }

    private void UpdateTrayModeChecks()
    {
        if (_trayModeProxy is null || _trayModeSystem is null || _trayModeTun is null) return;
        string mode = _store.Mode;
        _trayModeTun.IsChecked = mode == "tun";
        _trayModeProxy.IsChecked = mode == "proxy";
        _trayModeSystem.IsChecked = mode == "system";
    }

    private async void AddWarp_Click(object? sender, RoutedEventArgs e)
    {
        SetAddMenu(false);
        bool addedMasque = false;
        try
        {
            if (_core.AetherAvailable)
            {
                _store.Add(new ProxyConfig
                {
                    Name = "WARP (MASQUE)",
                    Protocol = "aether",
                    Address = "127.0.0.1",
                    Port = AetherController.SocksPort,
                    AetherMode = "masque",
                    AetherScan = "balanced",
                    AetherHttp2 = true,
                    Source = ConfigSource.Personal
                });
                RebuildGroups();
                addedMasque = true;
                Append("WARP: added a MASQUE entry (tunnels over TCP via aether.exe).");
            }

            int regProxy = _core.IsRunning ? ConfigBuilder.HttpPort : 0;
            Append(regProxy > 0
                ? "WARP: registering with Cloudflare through the active tunnel..."
                : "WARP: registering with Cloudflare...");
            var result = await Warp.Register(regProxy);
            if (!result.Ok)
            {
                Append("WARP: " + result.Message);
                if (!addedMasque) await Ask("warning", "WARP", "WARP: " + result.Message);
                return;
            }

            var account = result.Account;
            void AddDefaults(string why)
            {
                foreach (var c in result.Configs) _store.Add(c);
                RebuildGroups();
                Append($"WARP: added {result.Configs.Count} default endpoints ({why}).");
            }

            if (account is null || !DelayTester.XrayAvailable)
            {
                AddDefaults("scan unavailable");
                return;
            }

            Append($"WARP: probing endpoints on this network (up to {WarpScanMax})...");
            var progress = new Progress<(int Done, int Total, int Found)>(pr =>
            {
                if (pr.Done % 4 == 0 || pr.Done == pr.Total)
                    Append($"WARP: probed {pr.Done}/{pr.Total}, {pr.Found} responding");
            });

            var hits = await WarpScanner.Scan(
                account, WarpScanTarget, WarpScanMax, WarpScanConcurrency,
                WarpScanBudgetMs, progress, CancellationToken.None);

            if (hits.Count == 0)
            {
                Append("WARP: no WireGuard endpoint responded on this network.");
                if (!addedMasque) await Ask("warning", "WARP", Strings.Get(_lang, "warp_blocked"));
                return;
            }

            int keep = Math.Min(WarpScanTarget, hits.Count);
            for (int i = 0; i < keep; i++)
            {
                var h = hits[i];
                var cfg = Warp.BuildConfig(account, h.Host, h.Port, $"WARP {i + 1}");
                cfg.Ping = h.Latency;
                cfg.PingText = $"{h.Latency} ms";
                _store.Add(cfg);
            }

            RebuildGroups();
            Append($"WARP: added {keep} working endpoints, best {hits[0].Latency} ms.");
        }
        catch (Exception ex)
        {
            Append("WARP: " + ex.Message);
        }
        finally
        {
        }
    }

    private void ProjLink_Click(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/CluvexStudio/Aether");

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void Mux_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool on = MuxToggle.IsChecked == true;
        AnimateReveal(MuxReveal, MuxInner, on, true);
        _store.SetMux(on);
    }

    private void ApplyMux(int v)
    {
        if (_loading) return;
        _store.SetMuxConcurrency(v);
    }

    private void MuxBox_Changed(object? sender, TextChangedEventArgs e)
    {
        if (_loading || MuxBox is null) return;
        string txt = MuxBox.Text ?? "";
        string digits = "";
        foreach (var c in txt)
            if (c >= '0' && c <= '9') digits += c;
        if (digits != txt)
        {
            int caret = MuxBox.CaretIndex;
            MuxBox.Text = digits;
            MuxBox.CaretIndex = Math.Min(caret, digits.Length);
            return;
        }
        if (int.TryParse(digits, out var v) && v >= 1 && v <= 128)
            ApplyMux(v);
    }

    private void MuxBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (MuxField is not null) MuxField.BorderBrush = new SolidColorBrush(Color.Parse("#3D6AD6"));
    }

    private void MuxBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (MuxField is not null) MuxField.BorderBrush = new SolidColorBrush(Color.Parse("#35548C"));
        if (!int.TryParse(MuxBox.Text, out var v) || v < 1 || v > 128)
            MuxBox.Text = _store.MuxConcurrency.ToString();
    }

    private void MuxStep_Up(object? sender, RoutedEventArgs e) => StepMux(1);

    private void MuxStep_Down(object? sender, RoutedEventArgs e) => StepMux(-1);

    private void StepMux(int delta)
    {
        int cur = int.TryParse(MuxBox.Text, out var v) ? v : _store.MuxConcurrency;
        cur = Math.Clamp(cur + delta, 1, 128);
        MuxBox.Text = cur.ToString();
        ApplyMux(cur);
    }

    private void ApplyPort(int port)
    {
        if (_loading) return;
        _store.SetMixedPort(port);
    }

    private void PortBox_Changed(object? sender, TextChangedEventArgs e)
    {
        if (_loading || PortBox is null) return;
        string txt = PortBox.Text ?? "";
        string digits = "";
        foreach (var c in txt)
            if (c >= '0' && c <= '9') digits += c;
        if (digits != txt)
        {
            int caret = PortBox.CaretIndex;
            PortBox.Text = digits;
            PortBox.CaretIndex = Math.Min(caret, digits.Length);
            return;
        }
        if (int.TryParse(digits, out var p) && p >= 1024 && p <= 65535)
            ApplyPort(p);
    }

    private void PortBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (PortField is not null) PortField.BorderBrush = new SolidColorBrush(Color.Parse("#3D6AD6"));
    }

    private void PortBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (PortField is not null) PortField.BorderBrush = new SolidColorBrush(Color.Parse("#35548C"));
        if (!int.TryParse(PortBox.Text, out var p) || p < 1024 || p > 65535)
            PortBox.Text = _store.MixedPort.ToString();
    }

    private void PortStep_Up(object? sender, RoutedEventArgs e) => StepPort(1);

    private void PortStep_Down(object? sender, RoutedEventArgs e) => StepPort(-1);

    private void StepPort(int delta)
    {
        int cur = int.TryParse(PortBox.Text, out var p) ? p : _store.MixedPort;
        cur = Math.Clamp(cur + delta, 1024, 65535);
        PortBox.Text = cur.ToString();
        ApplyPort(cur);
    }

    private void LogLevelSeg_Click(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is RadioButton r && r.Tag is string lvl) _store.SetLogLevel(lvl);
    }

    private void SetLogLevelChecked(string? lvl)
    {
        LogOff.IsChecked = string.Equals(lvl, "none", StringComparison.OrdinalIgnoreCase);
        LogErr.IsChecked = string.Equals(lvl, "error", StringComparison.OrdinalIgnoreCase);
        LogWarn.IsChecked = string.Equals(lvl, "warning", StringComparison.OrdinalIgnoreCase);
        LogInfo.IsChecked = string.Equals(lvl, "info", StringComparison.OrdinalIgnoreCase);
        LogDebug.IsChecked = string.Equals(lvl, "debug", StringComparison.OrdinalIgnoreCase);
        if (LogOff.IsChecked != true && LogErr.IsChecked != true && LogWarn.IsChecked != true &&
            LogDebug.IsChecked != true) LogInfo.IsChecked = true;
    }

    private void AdBlock_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _store.SetAdBlock(AdBlockToggle.IsChecked == true);
    }

    private void OpenQuality(object? sender, RoutedEventArgs e) => ShowTab("quality");

    private void CloseQuality(object? sender, RoutedEventArgs e) => GoBack();

    private static double QBarFraction(double mbps)
    {
        const double ceiling = 1000.0;
        double frac = Math.Log(1 + Math.Max(0, mbps)) / Math.Log(1 + ceiling);
        return frac > 1 ? 1 : frac;
    }

    private static void QSetBar(Border bar, Border track, double mbps)
    {
        bar.Transitions ??= new Transitions
        {
            new DoubleTransition
            {
                Property = WidthProperty,
                Duration = TimeSpan.FromMilliseconds(320),
                Easing = new CubicEaseOut()
            }
        };
        double w = track.Bounds.Width * QBarFraction(mbps);
        bar.Width = w < 0 ? 0 : w;
    }

    private async void RunQuality(object? sender, RoutedEventArgs e)
    {
        if (_qualityCts is not null) return;
        bool direct = !_core.IsRunning;
        _qualityCts = new CancellationTokenSource();
        QualityButton.IsEnabled = false;
        SetVis(QualityWave, false);
        QualityBtnText.IsVisible = true;
        SlideText(QualityBtnText, "STARTING…");
        QualityGlow.Cell = 10.0;
        QRatingText.Text = "…";
        QRatingText.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xAD));
        QScoreBar.Width = 0;
        QDownVal.Text = "—";
        QUpVal.Text = "—";
        QIdleVal.Text = "—";
        QDownPingVal.Text = "—";
        QUpPingVal.Text = "—";
        QJitterVal.Text = "—";
        QDownBar.Width = 0;
        QUpBar.Width = 0;

        var stage = new Progress<string>(st =>
        {
            string label = st switch
            {
                "latency" => "Testing latency…",
                "upload" => "Testing upload…",
                _ => "Testing speed…"
            };
            SlideText(QualityBtnText, label.ToUpperInvariant());
        });
        var live = new Progress<(string Phase, double Mbps)>(t =>
        {
            if (t.Phase == "download")
            {
                AnimateNumber(QDownVal, t.Mbps);
                QSetBar(QDownBar, QDownTrack, t.Mbps);
            }
            else
            {
                AnimateNumber(QUpVal, t.Mbps);
                QSetBar(QUpBar, QUpTrack, t.Mbps);
            }
        });

        try
        {
            var r = await QualityTest.Run(direct ? 0 : ConfigBuilder.HttpPort, stage, live, _qualityCts.Token);
            ShowQuality(r);
            QualityStatus.Text = direct ? "Tested without VPN" : "";
            var selCfg = _store.Selected();
            string where = direct
                ? "No VPN"
                : (selCfg is null ? "VPN" : Flags.DisplayName(selCfg.Name));
            QLastRun.Text = $"Last test: {where} \u00B7 {DateTime.Now:HH:mm}";
            QLastRun.IsVisible = true;
        }
        catch (OperationCanceledException)
        {
            QualityStatus.Text = "";
        }
        catch (Exception ex)
        {
            QualityStatus.Text = ex.Message;
        }
        finally
        {
            QualityButton.IsEnabled = true;
            SetVis(QualityWave, false);
            QualityBtnText.IsVisible = true;
            SlideText(QualityBtnText, Strings.Get(_lang, "q_test").ToUpperInvariant());
            QualityGlow.Cell = 15.0;
            _qualityCts.Dispose();
            _qualityCts = null;
        }
    }

    private void ShowQuality(QualityResult r)
    {
        QDownVal.Text = r.DownMbps.HasValue ? $"{r.DownMbps.Value:0.00}" : "—";
        QUpVal.Text = r.UpMbps.HasValue ? $"{r.UpMbps.Value:0.00}" : "—";
        QIdleVal.Text = r.IdlePing.HasValue ? r.IdlePing.Value.ToString() : "—";
        QDownPingVal.Text = r.DownPing.HasValue ? r.DownPing.Value.ToString() : "—";
        QUpPingVal.Text = r.UpPing.HasValue ? r.UpPing.Value.ToString() : "—";
        QJitterVal.Text = r.Jitter.HasValue ? r.Jitter.Value.ToString() : "—";

        QDownBar.Width = r.DownMbps.HasValue ? QDownTrack.Bounds.Width * QBarFraction(r.DownMbps.Value) : 0;
        QUpBar.Width = r.UpMbps.HasValue ? QUpTrack.Bounds.Width * QBarFraction(r.UpMbps.Value) : 0;

        int score = QualityTest.Score(r);
        Color col;
        string label;
        if (score >= 82) { label = "Excellent"; col = Color.FromRgb(0x3C, 0xCB, 0x7F); }
        else if (score >= 62) { label = "Great"; col = Color.FromRgb(0x5B, 0xB8, 0xF0); }
        else if (score >= 42) { label = "Good"; col = Color.FromRgb(0x6E, 0x9C, 0xF0); }
        else if (score >= 22) { label = "Fair"; col = Color.FromRgb(0xE5, 0xA6, 0x3C); }
        else { label = "Poor"; col = Color.FromRgb(0xE0, 0x57, 0x5C); }

        var brush = new SolidColorBrush(col);
        QRatingText.Text = label;
        QRatingText.Foreground = brush;
        QScoreBar.Background = brush;
        double trackW = QScoreTrack.Bounds.Width > 0 ? QScoreTrack.Bounds.Width : 220;
        QScoreBar.Width = trackW * Math.Min(100, score) / 100.0;
    }

    private void Nav_About(object? sender, RoutedEventArgs e) => ShowTab("about");

    private DispatcherTimer? _drawerTimer;

    private void ToggleMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (MenuOverlay.IsVisible) CloseMenuDrawer();
        else OpenMenuDrawer();
    }

    private void MeasureDrawer()
    {
        if (_drawerMeasured || MenuList is null) return;

        double body = 0;
        foreach (var child in MenuList.Children)
        {
            if (child is not Control c || !c.IsVisible) continue;
            c.InvalidateMeasure();
            c.Measure(new Size(212, double.PositiveInfinity));
            body += c.DesiredSize.Height + MenuList.Spacing;
        }
        if (body <= 0) return;

        double h = body + 28 + 42 + 12;
        if (h < 320) h = 320;
        if (h > 700) h = 700;
        _drawerH = h;
        _drawerMeasured = true;
    }

    private void OpenMenuDrawer()
    {
        _drawerOpen = true;
        if (MenuLangBox is not null)
        {
            _loading = true;
            SelectByTag(MenuLangBox, _lang.ToString());
            _loading = false;
        }

        UpdateMenuSelection();
        MenuOverlay.IsVisible = true;
        MenuOverlay.Opacity = 0;
        AnimateDrawer(open: true);
        Dispatcher.UIThread.Post(() =>
        {
            if (!_drawerOpen) return;
            MeasureDrawer();
            AnimateWindowHeight();
        }, DispatcherPriority.Loaded);
    }

    private void CloseMenuDrawer()
    {
        _drawerOpen = false;
        AnimateDrawer(open: false);
        QueueWindowHeight();
    }

    private void AnimateDrawer(bool open)
    {
        _drawerOpen = open;
        if (MenuDrawer is null) return;
        _drawerTimer?.Stop();
        double fromX = open ? -240 : 0;
        double toX = open ? 0 : -240;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _drawerTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_drawerTimer, timer))
            {
                timer.Stop();
                return;
            }
            double p = Math.Min(1, (DateTime.UtcNow - t0).TotalMilliseconds / 200.0);
            double ease = 1 - Math.Pow(1 - p, 3);
            MenuDrawer.RenderTransform = new TranslateTransform(fromX + (toX - fromX) * ease, 0);
            MenuOverlay.Opacity = open ? p : 1 - p;
            if (p >= 1)
            {
                timer.Stop();
                _drawerTimer = null;
                if (!open) MenuOverlay.IsVisible = false;
            }
        };
        timer.Start();
    }

    private void UpdateMenuSelection()
    {
        var rows = new (Button? Btn, bool Sel)[]
        {
            (NavConnect, _tab is "connect" or "picker" or "manual" or "projects"),
            (NavUsage, _tab == "usage"),
            (NavTools, _tab is "tools" or "scanner" or "quality"),
            (NavSettings, _tab == "settings"),
            (NavAbout, _tab == "about"),
            (NavLogs, _tab == "logs")
        };
        foreach (var (btn, sel) in rows)
        {
            if (btn is null) continue;
            if (sel) btn.Classes.Add("selected");
            else btn.Classes.Remove("selected");
        }
    }

    private void MenuOverlay_Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (MenuDrawer is not null && e.Source is Visual v
            && (ReferenceEquals(v, MenuDrawer) || v.GetVisualAncestors().Contains(MenuDrawer)))
            return;
        CloseMenuDrawer();
    }

    private void TitleBar_Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Minimized
                ? WindowState.Normal
                : WindowState.Minimized;
            return;
        }
        BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e)
        => RunWindowAction(_store.ExitAction, "hide");

    private void RunWindowAction(string? action, string fallback)
    {
        switch (string.IsNullOrEmpty(action) ? fallback : action)
        {
            case "exit":
                ShutdownAndClose();
                break;
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            default:
                HideToTray();
                break;
        }
    }

    private void Nav_Prefs(object? sender, RoutedEventArgs e) { CloseMenuDrawer(); ShowTab("prefs"); }

    private void RefreshPrefs()
    {
        CheckSeg(_store.ExitAction, ExitExit, ExitHide, ExitMin);
        CheckSeg(_store.BackgroundSkin, SkinDots, SkinStars, SkinVein, SkinPlain);
        SelectByTag(LangBox, _lang == Lang.Fa ? "fa" : "en");
        StyleAuto();
        SetLogLevelChecked(_store.LogLevel);
    }

    private static void CheckSeg(string? tag, params RadioButton?[] segs)
    {
        foreach (var r in segs)
        {
            if (r is null) continue;
            r.IsChecked = string.Equals(r.Tag as string, tag, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ExitAct_Set(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton r && r.Tag is string t) _store.SetExitAction(t);
    }

    private void Skin_Set(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton r || r.Tag is not string t) return;
        _store.SetBackgroundSkin(t);
        ApplyBackgroundSkin();
    }

    private void SetFieldTint(Color c)
    {
        Particles?.SetTint(c);
        ParticlesBlur?.SetTint(c);
        Stars?.SetTint(c);
        StarsBlur?.SetTint(c);
        Veins?.SetTint(c);
        VeinsBlur?.SetTint(c);
    }

    private void ApplyBackgroundSkin()
    {
        string skin = _store.BackgroundSkin ?? "plain";
        bool dots = string.Equals(skin, "dots", StringComparison.OrdinalIgnoreCase);
        bool stars = string.Equals(skin, "stars", StringComparison.OrdinalIgnoreCase);
        bool vein = string.Equals(skin, "vein", StringComparison.OrdinalIgnoreCase);
        SetVis(Particles, dots);
        SetVis(ParticlesBlur, dots);
        SetVis(Stars, stars);
        SetVis(StarsBlur, stars);
        SetVis(Veins, vein);
        SetVis(VeinsBlur, vein);
    }

    private void LangBox_Changed(object? sender, SelectionChangedEventArgs e)
        => PickLanguage(MenuLangBox);

    private void PrefLang_Changed(object? sender, SelectionChangedEventArgs e)
        => PickLanguage(LangBox);

    private void PickLanguage(ComboBox? box)
    {
        if (_loading) return;
        if (box?.SelectedItem is not ComboBoxItem item) return;
        if (!Enum.TryParse<Lang>(item.Tag as string ?? "En", out var lang)) return;
        if (lang == _lang) return;

        _lang = lang;
        _store.SetLang(_lang);
        ApplyLanguage();
        if (_tab is "connect" or "prefs") QueueWindowHeight();
    }

    private async void CopyLogs_Click(object? sender, RoutedEventArgs e)
    {
        string text = LogText?.SelectedText is { Length: > 0 } sel ? sel : _log.ToString();
        if (text.Length == 0) return;
        bool wasSelection = LogText?.SelectedText is { Length: > 0 };

        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var cb = TopLevel.GetTopLevel(this)?.Clipboard;
                if (cb is null) throw new InvalidOperationException("no clipboard on this window");
                await cb.SetTextAsync(text);
            });
            Append(wasSelection ? "Selection copied." : "Log copied.");
        }
        catch (Exception ex)
        {
            Append("Copy failed: " + ex.GetBaseException().Message);
        }
    }

    private void ClearLogs_Click(object? sender, RoutedEventArgs e)
    {
        _logDirty = false;
        _log.Clear();
        if (LogText is not null) LogText.Text = "";
        SetVis(LogPlaceholder, true);
    }

    private void OnLog(string line) => Dispatcher.UIThread.Post(() => Append(line));

    private DispatcherTimer? _logFlush;
    private bool _logDirty;

    private void Append(string line)
    {
        _log.AppendLine(line);
        if (_log.Length > 60000) _log.Remove(0, 20000);
        if (LogPlaceholder is not null) LogPlaceholder.IsVisible = _log.Length == 0;

        _logDirty = true;
        if (_logFlush is not null) return;

        _logFlush = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _logFlush.Tick += (_, _) =>
        {
            if (!_logDirty) return;
            _logDirty = false;
            if (_tab != "logs" || LogText is null) return;
            LogText.Text = _log.ToString();
            LogScroll?.ScrollToEnd();
        };
        _logFlush.Start();
    }

    private void FlushLog()
    {
        if (LogText is null) return;
        LogText.Text = _log.ToString();
        _logDirty = false;
        LogScroll?.ScrollToEnd();
    }

    private async void Power_Click(object? sender, RoutedEventArgs e)
    {
        int gen = ++_powerGen;
        await _powerLock.WaitAsync();
        try
        {
            if (gen != _powerGen) return;

            if (_busy || _core.IsRunning)
            {
                _userDisconnect = true;
                ClearConnectError();
                StopAll();
                return;
            }

            _userDisconnect = false;
            var config = _store.Selected();
            if (config is null)
            {
                ShowConnectError(Strings.Get(_lang, "err_no_server"));
                Append("No server selected");
                return;
            }

            _busy = true;
            UsageStore.CurrentConfigKey = string.IsNullOrWhiteSpace(config.Name)
                ? config.Address
                : Flags.DisplayName(config.Name);
            SetPowerVisual(ConnectionState.Connecting);

            bool tor = _store.TorOnion;
            int torNodePort = 0;
            _torPending = false;
            _torPct = -1;
            _statsTag = "proxy";
            ClearConnectError();
            var dial = config;
            if (!string.IsNullOrEmpty(config.TorBase))
            {
                var baseCfg = _store.Configs.FirstOrDefault(x => x.Id == config.TorBase);
                if (baseCfg is null)
                    throw new InvalidOperationException("This Tor location's server is gone. Rebuild it from Free projects.");
                dial = baseCfg;
                torNodePort = config.Port;
                tor = false;
                _torPending = true;
                _statsTag = "tornode";
            }

            string json = _core.BuildConfig(
                dial, _store.Fragment, _store.SplitRouting, _store.SniffEnabled,
                _store.SniffProtocols, _store.SniffRouteOnly, _store.Mux, _store.MuxConcurrency,
                _store.AdBlock, tor, _store.SecureDns, torNodePort);
            await Task.Run(() => _core.Start(dial, json, tor, _store.TorExit));

            if (torNodePort > 0)
            {
                string cc = config.TorCountry;
                if (string.IsNullOrEmpty(cc))
                    throw new InvalidOperationException("This Tor location is missing its country. Rebuild it from Free projects.");

                _torNodes.Log -= OnTorNodeLog;
                _torNodes.Log += OnTorNodeLog;
                _torNodes.Bootstrap -= OnTorBootstrap;
                _torNodes.Bootstrap += OnTorBootstrap;

                bool up = await _torNodes.StartOne(cc, torNodePort, 180000, CancellationToken.None);
                _torPending = false;
                if (up)
                {
                    SetPowerVisual(ConnectionState.Connected);
                    StartStatsPolling();
                    _ = RefreshLocation(true, 800);
                }
                else
                {
                    ShowConnectError("Tor circuit did not finish bootstrapping. Disconnect and try again.");
                    SetPowerVisual(ConnectionState.Error);
                    Append("Tor circuit did not finish bootstrapping. Disconnect and try again.");
                }
            }
        }
        catch (Exception ex)
        {
            _busy = false;
            ShowConnectError(ex.Message);
            SetPowerVisual(ConnectionState.Error);
            Append(ex.Message);
        }
        finally
        {
            _powerLock.Release();
        }
    }

    private DispatcherTimer? _typeTimer;

    private void StartTypewriter(bool animate)
    {
        if (TypeClip is null || SelectedName is null || TypeCursor is null) return;
        _typeTimer?.Stop();
        _typeTimer = null;
        if (!animate)
        {
            TypeClip.Width = double.NaN;
            return;
        }
        var tf = new Typeface("Consolas", weight: FontWeight.Bold);
        var ft = new FormattedText(SelectedName.Text ?? "", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, tf, SelectedName.FontSize, Brushes.White);
        double w = Math.Min(Math.Ceiling(ft.Width) + 2, 240);
        TypeClip.Width = 0;
        var t0 = DateTime.UtcNow;
        var type = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _typeTimer = type;
        type.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_typeTimer, type))
            {
                type.Stop();
                return;
            }
            double p = (DateTime.UtcNow - t0).TotalMilliseconds / 700.0;
            if (p >= 1)
            {
                TypeClip.Width = double.NaN;
                type.Stop();
                _typeTimer = null;
                return;
            }
            TypeClip.Width = w * p;
        };
        type.Start();
    }

    private DispatcherTimer? _blinkTimer;

    private void StartCursorBlink()
    {
        if (TypeCursor is null) return;
        _blinkTimer?.Stop();
        var blink = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer = blink;
        blink.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_blinkTimer, blink))
            {
                blink.Stop();
                return;
            }
            TypeCursor.Opacity = TypeCursor.Opacity > 0.5 ? 0 : 1;
        };
        blink.Start();
    }

    private bool _ipPopupReady;

    private bool _markerPostPending;

    private void OnMarkerFrame(double mx, double my, double tz, double rad)
    {
        if (_markerPostPending) return;
        _markerPostPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _markerPostPending = false;
            if (IpPopup is null || EarthHost is null) return;

            double alpha = Math.Clamp(tz / 0.22, 0.0, 1.0);
            IpPopup.Opacity = alpha;
            IpPopup.IsVisible = _ipPopupReady && alpha > 0.01;
            if (!IpPopup.IsVisible) return;

            double w = EarthHost.Bounds.Width;
            double h = EarthHost.Bounds.Height;
            double cw = IpPopup.Bounds.Width;
            double ch = IpPopup.Bounds.Height;
            if (cw <= 1 || ch <= 1)
            {
                IpPopup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                cw = IpPopup.DesiredSize.Width;
                ch = IpPopup.DesiredSize.Height;
            }
            if (cw <= 1 || ch <= 1) return;

            double px = Math.Clamp(mx - cw / 2.0, 0.0, Math.Max(0.0, w - cw));
            double py = Math.Clamp(my - rad * 0.12 - ch, 0.0, Math.Max(0.0, h - ch));
            IpPopup.RenderTransform = new TranslateTransform(px, py);
        }, DispatcherPriority.Render);
    }

    private ConnectionState _powerState = ConnectionState.Disconnected;

    private void ApplyPowerGlow(bool checkedLook, Color glow, double alpha)
    {
        if (PowerFace is not null)
        {
            PowerFace.BoxShadow = BoxShadows.Parse(checkedLook
                ? "inset -2 -2 0 0 #2C5AA0, inset 2 2 0 0 #041E42"
                : "inset 2 2 0 0 #2C5AA0, inset -2 -2 0 0 #041E42");
        }
        if (PowerGlow is null) return;
        byte a = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
        PowerGlow.BoxShadow = BoxShadows.Parse($"0 0 30 2 #{a:X2}{glow.R:X2}{glow.G:X2}{glow.B:X2}");
    }

    private DispatcherTimer? _swapTimer;
    private DispatcherTimer? _connClock;
    private DateTime _connectedAt;

    private bool _pinging;

    private async void PingButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pinging) return;
        if (_store.Selected() is not ProxyConfig cfg) return;
        _pinging = true;
        if (PingFx is not null) PingFx.Busy = true;
        var xrayPath = Bin.Path("xray");
        if (!File.Exists(xrayPath))
        {
            _lastPingMs = int.MinValue;
            SetPingLabel("\u2014");
            if (PingFx is not null) { PingFx.Busy = false; PingFx.Ping = -1; }
            Append("Ping: xray not found at " + xrayPath);
            _pinging = false;
            return;
        }
        SetPingLabel("...");
        cfg.Ping = -2;
        try
        {
            var ms = await DelayTester.Measure(cfg);
            cfg.Ping = ms >= 0 ? ms : -1;
            if (_store.Selected()?.Id != cfg.Id) return;
            if (PingFx is not null) PingFx.Ping = cfg.Ping;
            ApplyPingTint(cfg.Ping);
            _lastPingMs = ms;
            SetPingLabel(ms >= 0 ? PingMs(ms) : PingFailText());
        }
        catch (Exception ex)
        {
            cfg.Ping = -1;
            Append("Ping failed: " + ex.Message);
            if (_store.Selected()?.Id != cfg.Id) return;
            if (PingFx is not null) PingFx.Ping = -1;
            ApplyPingTint(-1);
            _lastPingMs = -1;
            SetPingLabel(PingFailText());
        }
        finally
        {
            _pinging = false;
            if (PingFx is not null) PingFx.Busy = false;
        }
    }

    private void StartConnClock()
    {
        _connectedAt = DateTime.UtcNow;
        if (ConnTimeText is not null)
            ConnTimeText.Text = Strings.LocalizeDigits("00:00:00", _lang);
        _connClock?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _connClock = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_connClock, timer))
            {
                timer.Stop();
                return;
            }
            if (ConnTimeText is not null)
                ConnTimeText.Text = Strings.LocalizeDigits(
                    (DateTime.UtcNow - _connectedAt).ToString(@"hh\:mm\:ss"), _lang);
        };
        timer.Start();
    }

    private void StopConnClock()
    {
        _connClock?.Stop();
        _connClock = null;
        if (ConnTimeText is not null)
            ConnTimeText.Text = Strings.LocalizeDigits("00:00:00", _lang);
    }

    private DispatcherTimer? _staggerOutTimer;

    private void StaggerOut(Panel host, Action done)
    {
        _staggerOutTimer?.Stop();
        var items = new List<Control>();
        foreach (var child in host.Children)
        {
            if (child is Control ctl && ctl.IsVisible) items.Add(ctl);
        }
        if (items.Count == 0)
        {
            done();
            return;
        }
        var t0 = DateTime.UtcNow;
        const double dur = 180.0;
        const double gap = 60.0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _staggerOutTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_staggerOutTimer, timer))
            {
                timer.Stop();
                return;
            }
            double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
            bool allDone = true;
            for (int i = 0; i < items.Count; i++)
            {
                double p = (ms - i * gap) / dur;
                if (p < 0) { allDone = false; continue; }
                if (p >= 1)
                {
                    items[i].Opacity = 0;
                    continue;
                }
                allDone = false;
                double e = p * p;
                items[i].RenderTransform = new TranslateTransform(0, -12 * e);
                items[i].Opacity = 1 - e;
            }
            if (allDone)
            {
                timer.Stop();
                _staggerOutTimer = null;
                foreach (var it in items)
                {
                    it.Opacity = 1;
                    it.RenderTransform = null;
                }
                done();
            }
        };
        timer.Start();
    }

    private int _swapGen;
    private bool? _chipTarget;

    private static void ResetChip(Control? c)
    {
        if (c is null) return;
        c.Opacity = 1;
        c.RenderTransform = null;
        if (c is not Panel p) return;
        foreach (var child in p.Children)
        {
            if (child is not Control cc) continue;
            cc.Opacity = 1;
            cc.RenderTransform = null;
        }
    }

    private void CancelStatusSwap()
    {
        _swapGen++;
        _swapTimer?.Stop();
        _swapTimer = null;
        _staggerTimer?.Stop();
        _staggerTimer = null;
        _staggerOutTimer?.Stop();
        _staggerOutTimer = null;
        ResetChip(ConnBox);
        ResetChip(DiscoBox);
    }

    private void SwapStatus(bool toConn)
    {
        Control? outgoing = toConn ? DiscoBox : ConnBox;
        Control? incoming = toConn ? ConnBox : DiscoBox;
        CancelStatusSwap();
        int gen = _swapGen;

        void ShowIncoming()
        {
            if (gen != _swapGen) return;
            if (outgoing is not null)
            {
                outgoing.Opacity = 1;
                outgoing.RenderTransform = null;
                SetVis(outgoing, false);
            }
            SetVis(incoming, true);
            if (toConn) StaggerIn(ConnBox);
            else SlideChip(DiscoBox);
        }

        if (outgoing is null || !outgoing.IsVisible)
        {
            ShowIncoming();
            return;
        }

        if (outgoing is Panel op && op.Children.Count > 1)
        {
            StaggerOut(op, ShowIncoming);
            return;
        }

        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _swapTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_swapTimer, timer))
            {
                timer.Stop();
                return;
            }
            if (gen != _swapGen)
            {
                timer.Stop();
                return;
            }
            double p = Math.Min(1, (DateTime.UtcNow - t0).TotalMilliseconds / 160.0);
            double e = p * p;
            outgoing.RenderTransform = new TranslateTransform(0, -10 * e);
            outgoing.Opacity = 1 - e;
            if (p >= 1)
            {
                timer.Stop();
                _swapTimer = null;
                ShowIncoming();
            }
        };
        timer.Start();
    }

    private DispatcherTimer? _pulseTimer;
    private DispatcherTimer? _pulseDelayTimer;

    private void StartPowerPulseDelayed()
    {
        _pulseDelayTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _pulseDelayTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!ReferenceEquals(_pulseDelayTimer, timer)) return;
            _pulseDelayTimer = null;
            StartPowerPulse();
        };
        timer.Start();
    }

    private bool _powerHeld;

    private void ApplyPowerHover()
    {
        StopPowerPulse();
        var hold = Color.FromRgb(0xFF, 0xA9, 0x4D);
        ApplyPowerBrush(hold);
        ApplyPowerGlow(_powerState != ConnectionState.Disconnected, hold, 0.0);
    }

    private void PowerHoverEnter(object? sender, PointerEventArgs e)
    {
        _hoverArmed = true;
        ApplyPowerHover();
    }

    private void PowerHoverLeave(object? sender, PointerEventArgs e)
    {
        if (_powerHeld) return;
        _hoverArmed = false;
        SetPowerVisual(_powerState);
    }

    private DateTime _powerDownAt;
    private bool _hoverArmed;

    private void PowerHoldStart(object? sender, PointerPressedEventArgs e)
    {
        _powerHeld = true;
        _powerDownAt = DateTime.UtcNow;
        ApplyPowerHover();
        if (Veins is not null && Veins.IsVisible) VeinField.StrikeRoot();
    }

    private void PowerHoldEnd(object? sender, PointerReleasedEventArgs e)
    {
        _powerHeld = false;
        if (Veins is not null && Veins.IsVisible
            && (DateTime.UtcNow - _powerDownAt).TotalMilliseconds > 400)
            VeinField.StrikeRoot(force: true);
        _hoverArmed = false;
        SetPowerVisual(_powerState);
    }

    private void PowerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _powerHeld = false;
        _hoverArmed = false;
        SetPowerVisual(_powerState);
    }

    private void ApplyPowerBrush(Color col)
    {
        if (PowerFace is null || PowerIcon is null) return;
        var b = new SolidColorBrush(col);
        PowerFace.BorderBrush = b;
        PowerIcon.Fill = b;
    }

    private void StartPowerPulse()
    {
        if (PulseLayer is null) return;
        _pulseTimer?.Stop();
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _pulseTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_pulseTimer, timer))
            {
                timer.Stop();
                return;
            }
            double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
            PulseLayer.Opacity = 0.9 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * ms / 1600.0));
        };
        timer.Start();
    }

    private void StopPowerPulse()
    {
        _pulseDelayTimer?.Stop();
        _pulseDelayTimer = null;
        _pulseTimer?.Stop();
        _pulseTimer = null;
        if (PulseLayer is not null) PulseLayer.Opacity = 0;
    }

    private bool _connErrorShown;

    private void ShowConnectError(string? message)
    {
        if (DiscoText is null) return;
        bool blank = string.IsNullOrWhiteSpace(message);
        DiscoText.Text = blank ? Strings.Get(_lang, "status_disconnected") : message!.Trim();
        _connErrorShown = !blank;
        if (_tab == "connect") QueueWindowHeight();
    }

    private void ClearConnectError()
    {
        if (DiscoText is null) return;
        if (!_connErrorShown)
        {
            DiscoText.Text = Strings.Get(_lang, "status_disconnected");
            return;
        }
        _connErrorShown = false;
        DiscoText.Text = Strings.Get(_lang, "status_disconnected");
        if (_tab == "connect") QueueWindowHeight();
    }

    private void SetPowerVisual(ConnectionState state)
    {
        UpdateTrayMenu();
        _powerState = state;
        bool nowConn = state == ConnectionState.Connected;
        if (ConnBox is not null) ConnBox.IsHitTestVisible = nowConn;
        if (DiscoBox is not null) DiscoBox.IsHitTestVisible = !nowConn;
        if (_chipTarget != nowConn)
        {
            _chipTarget = nowConn;
            if (nowConn) StartConnClock();
            else StopConnClock();
            SwapStatus(nowConn);
        }
        if (PowerFace is null || PowerIcon is null) return;
        bool isChecked = state != ConnectionState.Disconnected;
        if (state == ConnectionState.Connected) StartPowerPulseDelayed();
        else StopPowerPulse();
        Color c = state switch
        {
            ConnectionState.Connected => Color.FromRgb(0x2F, 0xDF, 0x8F),
            ConnectionState.Connecting => Color.FromRgb(0xFF, 0xA9, 0x4D),
            _ => Color.FromRgb(0xE0, 0x57, 0x5C)
        };
        double glowAlpha = state switch
        {
            ConnectionState.Connected => 0.7,
            ConnectionState.Connecting => 0.0,
            _ => 0.0
        };
        ApplyPowerGlow(isChecked, c, glowAlpha);

        if (_powerHeld || (_hoverArmed && ConnectButton?.IsPointerOver == true))
        {
            var hover = Color.FromRgb(0xFF, 0xA9, 0x4D);
            StopPowerPulse();
            ApplyPowerBrush(hover);
            ApplyPowerGlow(isChecked, hover, 0.0);
            return;
        }

        Color pc = state switch
        {
            ConnectionState.Connected => Color.FromRgb(0x2F, 0xDF, 0x8F),
            ConnectionState.Connecting => Color.FromRgb(0xFF, 0xA9, 0x4D),
            _ => Color.FromRgb(0x5B, 0x83, 0xD6)
        };
        SetFieldTint(pc);
        var brush = new SolidColorBrush(c);
        PowerFace.BorderBrush = brush;
        PowerIcon.Fill = brush;
    }

    private DispatcherTimer? _staggerTimer;

    private void StaggerIn(Panel? host)
    {
        if (host is null) return;
        _staggerTimer?.Stop();
        var items = new List<Control>();
        foreach (var child in host.Children)
        {
            if (child is Control c && c.IsVisible) items.Add(c);
        }
        if (items.Count == 0) return;
        foreach (var c in items)
        {
            c.Opacity = 0;
            c.RenderTransform = new TranslateTransform(0, -12);
        }
        var t0 = DateTime.UtcNow;
        const double dur = 260.0;
        const double gap = 85.0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _staggerTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_staggerTimer, timer))
            {
                timer.Stop();
                return;
            }
            double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
            bool allDone = true;
            for (int i = 0; i < items.Count; i++)
            {
                double p = (ms - i * gap) / dur;
                if (p < 0) { allDone = false; continue; }
                if (p >= 1)
                {
                    items[i].RenderTransform =
                        ReferenceEquals(items[i], PingButton) ? _pingScale : null;
                    items[i].Opacity = 1;
                    continue;
                }
                allDone = false;
                double ease = 1 - Math.Pow(1 - p, 3);
                items[i].RenderTransform = new TranslateTransform(0, -12 * (1 - ease));
                items[i].Opacity = ease;
            }
            if (allDone)
            {
                timer.Stop();
                _staggerTimer = null;
            }
        };
        timer.Start();
    }

    private DispatcherTimer? _chipTimer;

    private void SlideChip(Control? chip)
    {
        if (chip is null) return;
        _chipTimer?.Stop();
        chip.Opacity = 0;
        chip.RenderTransform = new TranslateTransform(0, 12);
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _chipTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_chipTimer, timer))
            {
                timer.Stop();
                return;
            }
            double p = Math.Min(1, (DateTime.UtcNow - t0).TotalMilliseconds / 260.0);
            double ease = 1 - Math.Pow(1 - p, 3);
            chip.RenderTransform = new TranslateTransform(0, 12 * (1 - ease));
            chip.Opacity = ease;
            if (p >= 1)
            {
                chip.RenderTransform = null;
                chip.Opacity = 1;
                timer.Stop();
                _chipTimer = null;
            }
        };
        timer.Start();
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    private void StopAll()
    {
        try { _ = _torNodes.StopAsync(); } catch { }
        StopStatsPolling();
        DisableProxy();
        _core.Stop();
        UsageStore.Flush();
        UsageStore.CurrentConfigKey = null;
    }

    private CancellationTokenSource? _directCts;

    private void StartDirectPolling()
    {
        StopDirectPolling();
        var cts = new CancellationTokenSource();
        _directCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            var (lastUp, lastDown) = SystemTraffic.Read();
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(1000, token); }
                catch { break; }
                if (token.IsCancellationRequested) break;

                var (up, down) = SystemTraffic.Read();
                long dUp = up >= lastUp ? up - lastUp : 0;
                long dDown = down >= lastDown ? down - lastDown : 0;
                lastUp = up;
                lastDown = down;
                if (dUp <= 0 && dDown <= 0) continue;

                UsageStore.Add(dUp, dDown, UsageStore.DirectKey);
            }
        }, token);
    }

    private void StopDirectPolling()
    {
        _directCts?.Cancel();
        _directCts = null;
    }

    private string _statsTag = "proxy";
    private bool _torPending;

    private void StartStatsPolling()
    {
        StopStatsPolling();
        var cts = new CancellationTokenSource();
        _statsCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            long lastUp = 0, lastDown = 0;
            bool first = true;
            while (!token.IsCancellationRequested)
            {
                var (up, down) = _core.UsingSingBox
                    ? SingBoxStats.Query(SingBoxBuilder.ClashApiPort)
                    : StatsQuery.Query(ConfigBuilder.ApiPort, _statsTag);
                long upSpeed = first ? 0 : Math.Max(up - lastUp, 0);
                long downSpeed = first ? 0 : Math.Max(down - lastDown, 0);
                lastUp = up;
                lastDown = down;
                first = false;

                UsageStore.Add(upSpeed, downSpeed);

                if (!token.IsCancellationRequested)
                    Dispatcher.UIThread.Post(() => UpdateStats(up, down, upSpeed, downSpeed));

                try { await Task.Delay(1000, token); }
                catch { break; }
            }
        }, token);
    }

    private CancellationTokenSource? _locCts;
    private int _locGen;

    private async Task RefreshLocation(bool throughProxy, int delayMs = 0)
    {
        int gen = ++_locGen;
        _locCts?.Cancel();
        var cts = new CancellationTokenSource();
        _locCts = cts;
        var token = cts.Token;

        try
        {
            if (delayMs > 0) await Task.Delay(delayMs, token);

            var loc = await IpLocator.FetchAsync(throughProxy, token);
            if (loc is null)
            {
                if (token.IsCancellationRequested || gen != _locGen) return;
                Append($"Location lookup failed ({(throughProxy ? "through tunnel" : "direct")}): "
                       + (IpLocator.LastError ?? "no reason given"));
                Dispatcher.UIThread.Post(() =>
                {
                    if (gen != _locGen) return;
                    if (IpPopIp is not null) IpPopIp.Text = "\u2014";
                    if (IpPopCity is not null) IpPopCity.Text = Strings.Get(_lang, "loc_unknown");
                    _ipPopupReady = true;
                });
                return;
            }
            if (token.IsCancellationRequested || gen != _locGen) return;

            Dispatcher.UIThread.Post(() =>
                {
                    if (gen != _locGen || Earth is null) return;
                    Earth.SetLocation(loc);
                    if (IpPopIp is not null)
                        IpPopIp.Text = string.IsNullOrEmpty(loc.Ip) ? "\u2014" : loc.Ip;
                    if (IpPopCity is not null)
                        IpPopCity.Text = string.IsNullOrEmpty(loc.City) ? loc.Country : loc.City + ", " + loc.Country;
                    _ipPopupReady = true;
                });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Append("Location lookup failed: " + ex.GetBaseException().Message);
        }
        finally
        {
            if (ReferenceEquals(_locCts, cts)) _locCts = null;
            cts.Dispose();
        }
    }

    private void StopStatsPolling()
    {
        _statsCts?.Cancel();
        _statsCts?.Dispose();
        _statsCts = null;
    }

    private void UpdateStats(long up, long down, long upSpeed, long downSpeed)
    {
        if (SpeedDownText is null) return;
        SpeedDownText.Text = Speed(downSpeed);
        SpeedUpText.Text = Speed(upSpeed);
        AnimateNumber2(TotalDownText, down);
        AnimateNumber2(TotalUpText, up);
    }

    private ITunService? _tun;

    private void EnableProxy()
    {
        string mode = _store.Mode;
        if (mode == "tun")
        {
            _ = StartTunAsync();
            return;
        }
        Append($"Routing mode: {mode}");
        if (mode != "system") return;
        EnableSystemProxy();
    }

    private void EnableSystemProxy()
    {
        if (_proxyOn) return;
        try
        {
            Platform.Proxy.Enable($"127.0.0.1:{ConfigBuilder.HttpPort}");
            _proxyOn = true;
            Append($"System proxy on 127.0.0.1:{ConfigBuilder.HttpPort}");
        }
        catch (Exception ex)
        {
            Append("Proxy: " + ex.Message);
        }
    }

    private void DisableProxy()
    {
        StopTun();
        if (!_proxyOn) return;
        try { Platform.Proxy.Disable(); } catch { }
        _proxyOn = false;
        Append("System proxy off");
    }

    private async Task StartTunAsync()
    {
        if (_tun is not null) return;

        Append($"TUN: mode={_store.Mode}"
               + $", admin={Platform.Elevation.IsAdministrator()}"
               + $", platform={(Platform.CreateTun is null ? "no" : "yes")}");

        try
        {
            Platform.Proxy.Disable();
            _proxyOn = false;
            Append("TUN: cleared the system proxy so traffic uses the adapter.");
        }
        catch (Exception ex)
        {
            Append("TUN: could not clear the system proxy \u2014 " + ex.Message);
        }

        if (Platform.CreateTun is null)
        {
            TunFellBack("TUN mode isn't available on this platform.");
            return;
        }
        if (!Platform.Elevation.IsAdministrator())
        {

            Append("TUN needs administrator rights; asking to restart elevated.");
            bool elevate = await Ask("admin",
                Strings.Get(_lang, "tun_admin_title"),
                Strings.Get(_lang, "tun_admin_prompt"),
                withCancel: true);

            if (elevate && Platform.Elevation.RelaunchAsAdmin("--autoconnect"))
            {
                ShutdownAndClose();
                return;
            }

            TunFellBack(OperatingSystem.IsLinux()
                ? "TUN needs root. Start GRoute with sudo; using the system proxy for now."
                : "TUN needs administrator rights. Using the system proxy instead \u2014 "
                  + "right-click GRoute and choose Run as administrator.");
            return;
        }
        if (_store.Selected() is not ProxyConfig cfg)
        {
            Append("TUN: no server selected.");
            return;
        }

        var svc = Platform.CreateTun();
        svc.Log += line => Dispatcher.UIThread.Post(() => Append(line));
        Append($"TUN: binaries={(svc.BinariesPresent ? "present" : "MISSING")}");

        if (!svc.BinariesPresent)
        {
            TunFellBack($"TUN needs this file, which is missing:\n\n{svc.MissingBinary()}\n\n"
                        + "Using the system proxy instead.");
            return;
        }

        _tun = svc;
        bool ok;
        try
        {
            ok = await svc.Start(cfg);
        }
        catch (Exception ex)
        {
            Append("TUN: " + ex.Message);
            ok = false;
        }

        if (ok)
        {
            Append("TUN adapter up \u2014 all traffic routed through the tunnel.");
            LogTunState();
            return;
        }

        _tun = null;
        try { svc.Stop(); } catch { }
        TunFellBack("TUN adapter failed to start \u2014 using the system proxy instead. "
                    + "See the log for the tun2socks output.");
    }

    private void LogTunState()
    {
        try
        {
            var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            var tun = nics.FirstOrDefault(n => n.Name.Equals("GRoute", StringComparison.Ordinal));
            if (tun is null)
            {
                Append("TUN: no GRoute adapter found \u2014 tun2socks did not create it.");
                return;
            }
            var ips = tun.GetIPProperties();
            string addr = string.Join(", ",
                ips.UnicastAddresses.Select(a => a.Address.ToString()));
            Append($"TUN: adapter '{tun.Name}' present, address {addr}");
        }
        catch (Exception ex)
        {
            Append("TUN: could not read the adapter \u2014 " + ex.Message);
        }
    }

    private void TunFellBack(string why)
    {
        Append(why);
        EnableSystemProxy();
        Dispatcher.UIThread.Post(() =>
        {
            ShowConnectError(why);
            _ = Ask("warning", Strings.Get(_lang, "title_warning"), why);
        });
    }

    private void StopTun()
    {
        var svc = _tun;
        _tun = null;
        if (svc is null) return;
        _ = Task.Run(() =>
        {
            try { svc.Stop(); } catch { }
            Dispatcher.UIThread.Post(() => Append("TUN adapter down."));
        });
    }

    private void OnState(ConnectionState state)
    {
      int gen = _powerGen;
      Dispatcher.UIThread.Post(() =>
      {
      try
      {
        if (gen != _powerGen) return;
        var sel = _store.Selected();
        switch (state)
        {
            case ConnectionState.Connecting:
                _busy = true;
                SetPowerVisual(ConnectionState.Connecting);
                break;
            case ConnectionState.Connected:
                _busy = true;
                EnableProxy();
                StopDirectPolling();
                if (_torPending)
                {
                    SetPowerVisual(ConnectionState.Connecting);
                    break;
                }
                SetPowerVisual(ConnectionState.Connected);
                StartStatsPolling();
                _ = RefreshLocation(true);
                break;
            case ConnectionState.Error:
                _busy = false;
                SetPowerVisual(ConnectionState.Error);
                StopStatsPolling();
                StartDirectPolling();
                DisableProxy();
                if (_store.KillSwitch && !_userDisconnect
                    && Platform.Elevation.IsAdministrator()
                    && !GRoute.Windows.Core.KillSwitch.Engaged)
                {
                    try
                    {
                        GRoute.Windows.Core.KillSwitch.Engage();
                        Append("Tunnel dropped \u2014 kill switch engaged, traffic is blocked.");
                    }
                    catch (Exception kex)
                    {
                        Append("Kill switch: " + kex.Message);
                    }
                }
                _userDisconnect = false;
                _ = RefreshLocation(false, LocationSettleMs);
                break;
            default:
                _busy = false;
                SetPowerVisual(ConnectionState.Disconnected);
                StopStatsPolling();
                StartDirectPolling();
                DisableProxy();
                _userDisconnect = false;
                _ = RefreshLocation(false, LocationSettleMs);
                break;
        }
      }
      catch (Exception ex)
      {
        Append("State handler error: " + ex.Message);
      }
      });
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_tray is not null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
            _tray = null;
        }
        StopAutoSelect();
        _autoRefreshTimer?.Stop();
        StopStatsPolling();
        DisableProxy();
        try { _core.StopAll(); } catch { }
        base.OnClosed(e);
    }
}
