using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GRoute.Windows.Core;

namespace GRoute.Windows;

public partial class MainWindow : Window
{
    private static readonly System.Windows.Media.Brush Accent =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0x6A, 0xD6));
    private static readonly System.Windows.Media.Brush BarHot =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x9C, 0xF0));
    private static readonly System.Windows.Media.Brush Muted =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9F, 0xB3, 0xD6));
    private static readonly System.Windows.Media.Brush NavActive =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCA, 0xE2, 0xFD));

    private static readonly System.Windows.Media.Brush[] CfgPalette =
    {
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x5B, 0x83, 0xD6)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8C, 0x6B, 0xE6)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0x8A, 0x3D)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x57, 0x5C)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x46, 0xB7, 0xC9)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0x6A, 0xD6)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC9, 0xA1, 0x3D)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x52, 0xC0, 0x8A)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB2, 0x5F, 0xD6)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD6, 0x68, 0x3D))
    };
    private static readonly System.Windows.Media.Brush CfgOther =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x5A, 0x80));
    private readonly System.Collections.Generic.Dictionary<string, System.Windows.Media.Brush> _cfgColors = new();

    private readonly XrayController _xray = new();
    private readonly TunController _tun = new();
    private readonly ConfigStore _store = new();
    private readonly ObservableCollection<SubGroupVm> _groups = new();
    private readonly System.Collections.Generic.HashSet<string> _expandedIds = new();
    private const string ManualId = "__manual__";
    private CancellationTokenSource? _statsCts;
    private Lang _lang;
    private enum SortMode { None, Fastest, Alpha, Added }
    private SortMode _sortMode = SortMode.Added;
    private bool _tunActive;
    private bool _reallyClose;
    private int _prevTabIndex;
    private string _tab = "connect";
    private string _connectView = "connection";
    private readonly EarthGlobe _globe = new();
    private readonly ParticleField _particles = new();

    private enum UsageRange { Today, Week, Month, Custom }
    private UsageRange _range = UsageRange.Today;
    private readonly ObservableCollection<ScanResult> _scanResults = new();
    private System.Windows.Threading.DispatcherTimer? _autoRefreshTimer;
    private int _sortClosedTick;
    private int _comboOpenedTick;
    private string _searchQuery = "";

    public MainWindow()
    {
        InitializeComponent();
        _lang = _store.Lang;
        Assets.EnsureSeeded();
        _xray.StateChanged += OnStateChanged;
        _xray.LogReceived += OnLog;
        _tun.Log += OnLog;

        GroupList.ItemsSource = _groups;
        ManualScroll.PreviewMouseWheel += FormWheelRedirect;
        PreviewMouseDown += SortOutsideClick;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (_, _) => UpdateNetworkWarning();
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += (_, _) => UpdateNetworkWarning();
        StartNetworkMonitor();
        SetupTray();
        System.AppDomain.CurrentDomain.ProcessExit += (_, _) => SafeCleanup();
        System.AppDomain.CurrentDomain.UnhandledException += (_, _) => SafeCleanup();
        if (System.Windows.Application.Current is System.Windows.Application app)
            app.DispatcherUnhandledException += (_, _) => SafeCleanup();
        SmoothScroll.Attach(GroupScroll);
        SmoothScroll.Attach(UsageScroll);
        SmoothScroll.Attach(ToolsScroll);
        SmoothScroll.Attach(ScannerScroll);
        SmoothScroll.Attach(QualityScroll);
        SmoothScroll.Attach(MenuScroll);
        SmoothScroll.Attach(SettingsScroll);
        SmoothScroll.Attach(ManualScroll);

        SortPopup.Closed += (_, _) => { _sortClosedTick = System.Environment.TickCount; };
        foreach (var cb in new[] { MProtocol, MMethod, MNetwork, MHeaderType, MSecurity, MFingerprint, MAlpn })
            cb.DropDownOpened += (s, _) =>
            {
                _comboOpenedTick = System.Environment.TickCount;
                if (s is System.Windows.Controls.ComboBox box && box.Template?.FindName("DropScroll", box) is System.Windows.Controls.ScrollViewer sv)
                {
                    sv.PreviewMouseWheel -= ComboWheel;
                    sv.PreviewMouseWheel += ComboWheel;
                }
            };
        StartCursorBlink();

        FragmentToggle.IsChecked = _store.Fragment;
        SplitToggle.IsChecked = _store.SplitRouting;
        SniffMaster.IsChecked = _store.SniffEnabled;
        SniffTls.IsChecked = _store.SniffProtocols.Contains("tls");
        SniffHttp.IsChecked = _store.SniffProtocols.Contains("http");
        SniffQuic.IsChecked = _store.SniffProtocols.Contains("quic");
        SniffFakeDns.IsChecked = _store.SniffProtocols.Contains("fakedns");
        RouteOnlyToggle.IsChecked = _store.SniffRouteOnly;
        SetSniffControlsEnabled(_store.SniffEnabled);

        Core.ConfigBuilder.HttpPort = _store.MixedPort;
        Core.ConfigBuilder.LogLevel = _store.LogLevel;
        MixedPortBox.Text = _store.MixedPort.ToString();
        StyleLogLevel();

        ApplyLanguage();
        ScanResults.ItemsSource = _scanResults;
        SetupAutoRefresh();
        StyleAuto();
        RebuildGroups();
        ParticleHost.Children.Add(_particles);
        EarthHost.Children.Add(_globe);
        Loaded += (_, _) => AnimateWindowHeight();
        SelectTab("connect");
        FetchEarthLocation();
    }

    private void SelectTab(string tab)
    {
        _tab = tab;
        UpdateTabIndicator(tab);
        QuickWrap.Visibility = tab == "connect" ? Visibility.Visible : Visibility.Collapsed;

        int newIndex = PanelIndex(tab);
        int dir = Math.Sign(newIndex - _prevTabIndex);
        _prevTabIndex = newIndex;
        ShowActivePanel(dir);

        if (tab == "usage") RebuildUsage();
    }

    private bool _firstHeight = true;

    private System.Windows.Threading.DispatcherTimer? _netTimer;

    private void StartNetworkMonitor()
    {
        UpdateNetworkWarning();
        _netTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _netTimer.Tick += (_, _) => UpdateNetworkWarning();
        _netTimer.Start();
    }

    private bool _netWarnShown;
    private double _netWarnH;

    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ToolStripMenuItem? _trayConnect;
    private System.Windows.Forms.ToolStripMenuItem? _trayModeProxy;
    private System.Windows.Forms.ToolStripMenuItem? _trayModeSystem;
    private System.Windows.Forms.ToolStripMenuItem? _trayModeTun;

    private void SetupTray()
    {
        if (_tray is not null) return;
        var menu = new System.Windows.Forms.ContextMenuStrip();

        _trayConnect = new System.Windows.Forms.ToolStripMenuItem(Strings.Get(_lang, "connect"), null,
            (_, _) => Dispatcher.Invoke(() => ConnectButton_Click(this, new RoutedEventArgs())));
        menu.Items.Add(_trayConnect);

        var mode = new System.Windows.Forms.ToolStripMenuItem(Strings.Get(_lang, "quick_title"));
        _trayModeProxy = new System.Windows.Forms.ToolStripMenuItem(Strings.Get(_lang, "mode_proxy"), null,
            (_, _) => Dispatcher.Invoke(() => SetMode("proxy")));
        _trayModeSystem = new System.Windows.Forms.ToolStripMenuItem(Strings.Get(_lang, "mode_system"), null,
            (_, _) => Dispatcher.Invoke(() => SetMode("system")));
        _trayModeTun = new System.Windows.Forms.ToolStripMenuItem(Strings.Get(_lang, "mode_tun"), null,
            (_, _) => Dispatcher.Invoke(() => SetMode("tun")));
        mode.DropDownItems.Add(_trayModeProxy);
        mode.DropDownItems.Add(_trayModeSystem);
        mode.DropDownItems.Add(_trayModeTun);
        menu.Items.Add(mode);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(Strings.Get(_lang, "tray_show"), null,
            (_, _) => Dispatcher.Invoke(ShowFromTray)));
        menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(Strings.Get(_lang, "tray_exit"), null,
            (_, _) => Dispatcher.Invoke(ShutdownAndClose)));

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = TrayIcon(),
            Visible = true,
            Text = "GRoute"
        };
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        UpdateTrayMenu(_xray.State);
        UpdateTrayModeChecks();
    }

    private static System.Drawing.Icon TrayIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/GRoute.ico"));
            if (info is not null) return new System.Drawing.Icon(info.Stream);
        }
        catch { }
        try
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exe is not null)
            {
                var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (ico is not null) return ico;
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void UpdateTrayMenu(ConnectionState state)
    {
        _ = state;
        if (_trayConnect is not null)
            _trayConnect.Text = Strings.Get(_lang, _xray.IsRunning ? "disconnect" : "connect");
    }

    private void UpdateTrayModeChecks()
    {
        if (_trayModeProxy is null || _trayModeSystem is null || _trayModeTun is null) return;
        bool tun = ModeTun.IsChecked == true;
        _trayModeTun.Checked = tun;
        _trayModeProxy.Checked = !tun && _justProxy;
        _trayModeSystem.Checked = !tun && !_justProxy;
    }

    private const string UpdateUrl = "https://raw.githubusercontent.com/SuOracle/GRoute-Win/main/version.json";

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CloseMainMenu();
        string current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "GRoute");
            var json = await client.GetStringAsync(UpdateUrl);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            string latest = doc.RootElement.GetProperty("version").GetString() ?? current;
            string url = doc.RootElement.TryGetProperty("url", out var u) ? (u.GetString() ?? "") : "";
            if (IsNewer(latest, current))
                ShowDialog(Strings.LocalizeDigits($"{Strings.Get(_lang, "update_available")} {latest}", _lang), string.IsNullOrEmpty(url) ? null : url);
            else
                ShowDialog(Strings.Get(_lang, "update_none"), null);
        }
        catch
        {
            ShowDialog(Strings.Get(_lang, "update_failed"), null);
        }
    }

    private string? _dialogUrl;

    private void ShowDialog(string message, string? url)
    {
        _dialogUrl = url;
        DialogText.Text = message;
        DialogCancelBtn.Visibility = string.IsNullOrEmpty(url) ? Visibility.Collapsed : Visibility.Visible;
        DialogOkBtn.Content = string.IsNullOrEmpty(url) ? "OK" : Strings.Get(_lang, "update_download");
        DialogOverlay.Visibility = Visibility.Visible;
        DialogDim.BeginAnimation(System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.5, new System.Windows.Duration(TimeSpan.FromMilliseconds(200))));
        var ease = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.4 };
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(260));
        DialogScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1, dur) { EasingFunction = ease });
        DialogScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1, dur) { EasingFunction = ease });
    }

    private void HideDialog()
    {
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, new System.Windows.Duration(TimeSpan.FromMilliseconds(180)));
        fade.Completed += (_, _) => DialogOverlay.Visibility = Visibility.Collapsed;
        DialogDim.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(180));
        DialogScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.9, dur));
        DialogScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.9, dur));
    }

    private void Dialog_Ok(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_dialogUrl))
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_dialogUrl) { UseShellExecute = true }); } catch { }
        HideDialog();
    }

    private void Dialog_Cancel(object sender, RoutedEventArgs e) => HideDialog();

    private void DialogDim_Up(object sender, System.Windows.Input.MouseButtonEventArgs e) => HideDialog();

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
        catch { }
        return false;
    }

    private async void UpdateNetworkWarning()
    {
        bool online = await ProbeInternet();
        Dispatcher.Invoke(() =>
        {
            bool show = !online;
            if (_netWarnShown == show) return;
            _netWarnShown = show;
            ShowNetWarn(show);
        });
    }

    private void ShowNetWarn(bool show)
    {
        if (show && _netWarnH <= 0)
        {
            NetWarnContent.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            _netWarnH = NetWarnContent.DesiredSize.Height + 23;
        }
        if (show) NetWarnBox.Visibility = Visibility.Visible;

        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(340));
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };

        var hAnim = new System.Windows.Media.Animation.DoubleAnimation(show ? 0 : _netWarnH, show ? _netWarnH : 0, dur) { EasingFunction = ease };
        if (!show)
            hAnim.Completed += (_, _) =>
            {
                NetWarnBox.BeginAnimation(System.Windows.FrameworkElement.HeightProperty, null);
                NetWarnBox.Visibility = Visibility.Collapsed;
            };
        NetWarnBox.BeginAnimation(System.Windows.FrameworkElement.HeightProperty, hAnim);

        if (_tab == "connect" && _connectView == "connection" && IsLoaded)
        {
            double delta = _netWarnH + 10;
            double winTo = show ? ActualHeight + delta : ActualHeight - delta;
            if (winTo < 320) winTo = 320;
            if (winTo > 700) winTo = 700;
            var wAnim = new System.Windows.Media.Animation.DoubleAnimation(winTo, dur) { EasingFunction = ease };
            wAnim.Completed += (_, _) =>
            {
                BeginAnimation(HeightProperty, null);
                Height = winTo;
            };
            BeginAnimation(HeightProperty, wAnim);
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

    private void AnimateWindowHeight()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded) return;
            var panel = ActivePanel();
            double w = ActualWidth - 32;
            if (w < 100) return;
            HeaderRow.InvalidateMeasure();
            HeaderRow.Measure(new System.Windows.Size(w, double.PositiveInfinity));
            double hh = HeaderRow.DesiredSize.Height;
            double ph;
            panel.InvalidateMeasure();
            panel.Measure(new System.Windows.Size(w, double.PositiveInfinity));
            ph = panel.DesiredSize.Height;
            double target = 42 + 8 + hh + ph + 12 + 0;
            if (target < 320) target = 320;
            if (target > 700) target = 700;
            if (Math.Abs(target - ActualHeight) < 3) return;
            if (_firstHeight)
            {
                _firstHeight = false;
                Height = target;
                return;
            }
            var anim = new System.Windows.Media.Animation.DoubleAnimation(target,
                new System.Windows.Duration(TimeSpan.FromMilliseconds(280)))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            anim.Completed += (_, _) =>
            {
                BeginAnimation(HeightProperty, null);
                Height = target;
            };
            BeginAnimation(HeightProperty, anim);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void UpdateTabIndicator(string tab)
    {
        string data;
        string key;
        switch (tab)
        {
            case "usage": data = "M2,2.5 V13.5 H14 M4.3,10.6 L7.3,7.4 L9.4,9.2 L13.2,4.6"; key = "Str.nav_usage"; break;
            case "tools": data = "M14.7,3.3 C15.1,4.8 14.7,6.4 13.6,7.5 C12.5,8.6 10.9,9 9.4,8.6 L4,14 L2,12 L7.4,6.6 C7,5.1 7.4,3.5 8.5,2.4 C9.6,1.3 11.2,0.9 12.7,1.3 L10.2,3.8 L12.2,5.8 Z"; key = "Str.nav_tools"; break;
            case "about": data = "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 M8,7.2 V11.3 M8,4.6 V4.9"; key = "Str.nav_about"; break;
            case "logs": data = "M1.5,3.5 H14.5 V12.5 H1.5 Z M4,6.3 L6.6,8.2 L4,10.1 M8.2,10.3 H12"; key = "Str.nav_logs"; break;
            case "settings": data = "M2.5,4.3 H13.5 M8.9,4.3 A1.7,1.7 0 1 0 12.3,4.3 A1.7,1.7 0 1 0 8.9,4.3 M2.5,8 H13.5 M3.8,8 A1.7,1.7 0 1 0 7.2,8 A1.7,1.7 0 1 0 3.8,8 M2.5,11.7 H13.5 M8.3,11.7 A1.7,1.7 0 1 0 11.7,11.7 A1.7,1.7 0 1 0 8.3,11.7"; key = "Str.conn_settings"; break;
            default: data = "M5.1,3.2 A6.3,6.3 0 1 0 10.9,3.2 M8,1.2 V7.6"; key = "Str.nav_connect"; break;
        }
        CurrentTabIcon.Data = System.Windows.Media.Geometry.Parse(data);
        CurrentTabText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, key);
        HighlightMenu(tab);
    }

    private void HighlightMenu(string tab)
    {
        var sel = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x23, 0x4A, 0x8E));
        MenuItemConnect.Background = tab == "connect" ? sel : System.Windows.Media.Brushes.Transparent;
        MenuItemUsage.Background = tab == "usage" ? sel : System.Windows.Media.Brushes.Transparent;
        MenuItemTools.Background = tab == "tools" ? sel : System.Windows.Media.Brushes.Transparent;
        MenuItemAbout.Background = tab == "about" ? sel : System.Windows.Media.Brushes.Transparent;
        MenuItemSettings.Background = tab == "settings" ? sel : System.Windows.Media.Brushes.Transparent;
        MenuItemLogs.Background = tab == "logs" ? sel : System.Windows.Media.Brushes.Transparent;
    }

    private FrameworkElement ActivePanel()
    {
        if (_tab == "connect")
            return _connectView == "picker" ? PickerPanel
                 : _connectView == "manual" ? ManualPanel
                 : ConnectPanel;
        if (_tab == "tools")
            return _toolsView == "scanner" ? ScannerPanel
                 : _toolsView == "quality" ? QualityPanel
                 : ToolsPanel;
        return _tab switch
        {
            "usage" => UsagePanel,
            "tools" => ToolsPanel,
            "about" => AboutPanel,
            "logs" => LogsPanel,
            "settings" => SettingsPanel,
            _ => ConnectPanel
        };
    }

    private void ShowActivePanel(int dir)
    {
        var shown = ActivePanel();
        ConnectPanel.Visibility = shown == ConnectPanel ? Visibility.Visible : Visibility.Collapsed;
        PickerPanel.Visibility = shown == PickerPanel ? Visibility.Visible : Visibility.Collapsed;
        ManualPanel.Visibility = shown == ManualPanel ? Visibility.Visible : Visibility.Collapsed;
        UsagePanel.Visibility = shown == UsagePanel ? Visibility.Visible : Visibility.Collapsed;
        ToolsPanel.Visibility = shown == ToolsPanel ? Visibility.Visible : Visibility.Collapsed;
        ScannerPanel.Visibility = shown == ScannerPanel ? Visibility.Visible : Visibility.Collapsed;
        QualityPanel.Visibility = shown == QualityPanel ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = shown == AboutPanel ? Visibility.Visible : Visibility.Collapsed;
        LogsPanel.Visibility = shown == LogsPanel ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = shown == SettingsPanel ? Visibility.Visible : Visibility.Collapsed;
        AnimateIn(shown, dir);
        ResetHover(this);
        AnimateWindowHeight();
    }

    private static void AnimateIn(FrameworkElement shown, int dir)
    {
        var tt = shown.RenderTransform as System.Windows.Media.TranslateTransform;
        if (tt is null)
        {
            tt = new System.Windows.Media.TranslateTransform();
            shown.RenderTransform = tt;
        }
        var slide = new System.Windows.Media.Animation.DoubleAnimation(dir * 34.0, 0,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(230)))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        };
        tt.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(200)));
        shown.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
    }

    private void OpenPicker(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _connectView = "picker";
        ShowActivePanel(1);
    }

    private void ClosePicker(object sender, RoutedEventArgs e)
    {
        _connectView = "connection";
        RefreshSelectedBox();
        ShowActivePanel(-1);
    }

    private string? _editingConfigId;

    private void OpenManual_Click(object sender, RoutedEventArgs e)
    {
        _editingConfigId = null;
        ManualTitle.Text = Resources["Str.add_manually"] as string ?? "Add manually";
        ClearManualFields();
        _connectView = "manual";
        ShowActivePanel(1);
    }

    private void EditConfig_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string id) return;
        var c = _store.Configs.FirstOrDefault(x => x.Id == id);
        if (c is null) return;
        _editingConfigId = id;
        MName.Text = c.Name; MAddress.Text = c.Address; MPort.Text = c.Port.ToString();
        MUuid.Text = c.Uuid; MPassword.Text = c.Password; MFlow.Text = c.Flow; MUser.Text = c.Username;
        MSni.Text = c.Sni;  MPbk.Text = c.PublicKey; MSid.Text = c.ShortId;
        MPath.Text = c.Path; MHost.Text = c.Host; MServiceName.Text = c.ServiceName; MMode.Text = c.Mode; MError.Text = "";
        MWgPrivate.Text = c.PrivateKey; MWgPublic.Text = c.PublicKey; MWgPsk.Text = c.PresharedKey;
        MWgAddress.Text = c.WgAddress; MWgMtu.Text = c.Mtu > 0 ? c.Mtu.ToString() : "1420"; MWgReserved.Text = c.Reserved;
        SetCombo(MProtocol, c.Protocol); SetCombo(MMethod, string.IsNullOrEmpty(c.Method) ? "aes-256-gcm" : c.Method);
        SetCombo(MNetwork, string.IsNullOrEmpty(c.Network) ? "tcp" : c.Network);
        SetCombo(MSecurity, string.IsNullOrEmpty(c.Security) ? "none" : c.Security);
        SetCombo(MHeaderType, string.IsNullOrEmpty(c.HeaderType) ? "none" : c.HeaderType);
        SetCombo(MFingerprint, string.IsNullOrEmpty(c.Fingerprint) ? "chrome" : c.Fingerprint);
        SetCombo(MAlpn, string.IsNullOrEmpty(c.Alpn) ? "none" : c.Alpn);
        UpdateFieldVisibility();
        ManualTitle.Text = Resources["Str.edit_config"] as string ?? "Edit config";
        _connectView = "manual";
        ShowActivePanel(1);
    }

    private void ClearManualFields()
    {
        MName.Text = ""; MAddress.Text = ""; MPort.Text = ""; MUuid.Text = ""; MPassword.Text = ""; MUser.Text = "";
        MFlow.Text = ""; MSni.Text = ""; MPbk.Text = ""; MSid.Text = "";
        MPath.Text = ""; MHost.Text = ""; MServiceName.Text = ""; MMode.Text = ""; MError.Text = "";
        MWgPrivate.Text = ""; MWgPublic.Text = ""; MWgPsk.Text = ""; MWgAddress.Text = ""; MWgMtu.Text = "1420"; MWgReserved.Text = "";
        SetCombo(MProtocol, "vless"); SetCombo(MMethod, "aes-256-gcm"); SetCombo(MNetwork, "tcp");
        SetCombo(MSecurity, "none"); SetCombo(MHeaderType, "none"); SetCombo(MFingerprint, "chrome");
        SetCombo(MAlpn, "none");
        UpdateFieldVisibility();
    }

    private void FieldSel_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsLoaded) UpdateFieldVisibility();
    }

    private void UpdateFieldVisibility()
    {
        string proto = ComboVal(MProtocol, "vless");
        bool wg = proto == "wireguard";
        bool httpSocks = proto is "http" or "socks";
        bool plain = wg || httpSocks;
        string net = ComboVal(MNetwork, "tcp");
        string sec = ComboVal(MSecurity, "none");
        void Show(System.Windows.FrameworkElement f, bool on) => f.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        bool hostNet = !plain && net is "ws" or "http" or "httpupgrade" or "xhttp";
        bool pathNet = hostNet || (!plain && net == "kcp");

        Show(Fld_Wg, wg);
        Show(TransportLabel, !plain);
        Show(MNetwork, !plain);
        Show(SecurityLabel, !plain);
        Show(MSecurity, !plain);

        Show(Fld_Username, httpSocks);
        Show(Fld_Uuid, !plain && proto is "vless" or "vmess");
        Show(Fld_Password, proto is "trojan" or "shadowsocks" or "http" or "socks");
        Show(Fld_Method, proto == "shadowsocks");
        Show(Fld_Flow, proto == "vless");
        Show(Fld_HeaderType, !plain && net is "tcp" or "kcp" or "quic");
        Show(Fld_Path, pathNet);
        Show(Fld_Host, hostNet);
        Show(Fld_ServiceName, !plain && net == "grpc");
        Show(Fld_Mode, !plain && net is "grpc" or "xhttp");
        Show(Fld_Sni, !plain && sec is "tls" or "reality");
        Show(Fld_Alpn, !plain && sec == "tls");
        Show(Fld_Fingerprint, !plain && sec is "tls" or "reality");
        Show(Fld_Pbk, !plain && sec == "reality");
        Show(Fld_Sid, !plain && sec == "reality");
    }

    private void ManualScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, ManualScroll)) return;
        if (System.Math.Abs(e.VerticalChange) < 0.5) return;
        if (System.Environment.TickCount - _comboOpenedTick < 350) return;
        foreach (var c in new[] { MProtocol, MMethod, MNetwork, MHeaderType, MSecurity, MFingerprint, MAlpn })
            c.IsDropDownOpen = false;
    }

    private void RowPressDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (OverButton(e.OriginalSource)) return;
        if (sender is System.Windows.FrameworkElement fe) ScalePress(fe, 0.98, false);
    }

    private void RowPressUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe) ScalePress(fe, 1.0, true);
    }

    private void RowLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.RenderTransform is System.Windows.Media.ScaleTransform st && st.ScaleX < 0.999)
            ScalePress(fe, 1.0, true);
    }

    private static void ScalePress(System.Windows.FrameworkElement fe, double to, bool release)
    {
        if (fe.RenderTransform is not System.Windows.Media.ScaleTransform st)
        {
            st = new System.Windows.Media.ScaleTransform(1, 1);
            fe.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            fe.RenderTransform = st;
        }
        var dur = System.TimeSpan.FromMilliseconds(release ? 170 : 80);
        var a1 = new System.Windows.Media.Animation.DoubleAnimation(to, dur);
        var a2 = new System.Windows.Media.Animation.DoubleAnimation(to, dur);
        if (release)
        {
            a1.EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.4 };
            a2.EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.4 };
        }
        st.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, a1);
        st.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, a2);
    }

    private static bool OverButton(object src)
    {
        var d = src as System.Windows.DependencyObject;
        while (d != null)
        {
            if (d is System.Windows.Controls.Primitives.ButtonBase) return true;
            d = (d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D)
                ? System.Windows.Media.VisualTreeHelper.GetParent(d)
                : System.Windows.LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    private void FormWheelRedirect(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        foreach (var cb in new[] { MProtocol, MMethod, MNetwork, MHeaderType, MSecurity, MFingerprint, MAlpn })
        {
            if (!cb.IsDropDownOpen) continue;
            if (cb.Template?.FindName("DropScroll", cb) is System.Windows.Controls.ScrollViewer sv)
            {
                WheelScroll(sv, e.Delta);
                e.Handled = true;
            }
            return;
        }
    }

    private void ComboWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.ScrollViewer sv)
        {
            WheelScroll(sv, e.Delta);
            e.Handled = true;
        }
    }

    private static void WheelScroll(System.Windows.Controls.ScrollViewer sv, int delta)
    {
        double lines = System.Windows.SystemParameters.WheelScrollLines <= 0 ? 3 : System.Windows.SystemParameters.WheelScrollLines;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - (delta / 120.0) * lines * 18.0);
    }

    private void CloseManual(object sender, RoutedEventArgs e)
    {
        _connectView = "picker";
        ShowActivePanel(-1);
    }

    private async void PasteClipboard_Click(object sender, RoutedEventArgs e)
    {
        string txt;
        try { txt = System.Windows.Clipboard.GetText(); } catch { txt = ""; }
        if (string.IsNullOrWhiteSpace(txt))
        {
            AppendLine("Clipboard is empty.");
            return;
        }
        await AddFromText(txt);
    }




    private void RefreshSelectedBox()
    {
        var c = _store.Selected();
        if (c is null)
        {
            SelectedAddr.Visibility = Visibility.Collapsed;
            SelectedFlag.Visibility = Visibility.Collapsed;
            SelectedName.Text = Resources["Str.tap_choose"] as string ?? "Tap to choose a server";
            StartTypewriter(false);
            return;
        }
        SelectedAddr.Visibility = Visibility.Visible;
        var selectedFlag = Flags.ForName(c.Name);
        if (selectedFlag is null)
        {
            SelectedFlag.Visibility = Visibility.Collapsed;
        }
        else
        {
            SelectedFlagBrush.ImageSource = selectedFlag;
            SelectedFlag.Visibility = Visibility.Visible;
        }
        SelectedName.Text = string.IsNullOrWhiteSpace(c.Name) ? c.Address : Flags.DisplayName(c.Name);
        SelectedAddr.Text = c.Address + ":" + c.Port;
        StartTypewriter(true);
    }

    private void StartTypewriter(bool animate)
    {
        if (!animate)
        {
            TypeClip.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
            TypeClip.Width = double.NaN;
            return;
        }
        var ft = new System.Windows.Media.FormattedText(SelectedName.Text ?? "", System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Consolas"),
            System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
            SelectedName.FontSize, System.Windows.Media.Brushes.White, 1.0);
        double w = System.Math.Min(System.Math.Ceiling(ft.Width) + 2, 240);
        TypeClip.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
        var a = new System.Windows.Media.Animation.DoubleAnimation(0, w, new System.Windows.Duration(System.TimeSpan.FromMilliseconds(700)));
        a.Completed += (_, _) => { TypeClip.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null); TypeClip.Width = double.NaN; };
        TypeClip.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, a);
    }

    private void StartCursorBlink()
    {
        var a = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
        {
            Duration = new System.Windows.Duration(System.TimeSpan.FromSeconds(1)),
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        a.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(1, System.Windows.Media.Animation.KeyTime.FromPercent(0)));
        a.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromPercent(0.5)));
        TypeCursor.BeginAnimation(System.Windows.UIElement.OpacityProperty, a);
    }

    private void ManualSave_Click(object sender, RoutedEventArgs e)
    {
        MError.Text = "";
        var addr = MAddress.Text.Trim();
        if (string.IsNullOrEmpty(addr)) { MError.Text = "Address is required."; return; }
        if (!int.TryParse(MPort.Text.Trim(), out int port) || port < 1 || port > 65535)
        {
            MError.Text = "Port must be between 1 and 65535.";
            return;
        }
        var protocol = ComboVal(MProtocol, "vless");
        if (protocol == "wireguard" && (string.IsNullOrWhiteSpace(MWgPrivate.Text) || string.IsNullOrWhiteSpace(MWgPublic.Text)))
        {
            MError.Text = "Private key and peer public key are required.";
            return;
        }
        var target = _editingConfigId is string editId
            ? (_store.Configs.FirstOrDefault(x => x.Id == editId) ?? new ProxyConfig())
            : new ProxyConfig();
        target.Name = string.IsNullOrWhiteSpace(MName.Text) ? addr : MName.Text.Trim();
        target.Protocol = protocol;
        target.Address = addr;
        target.Port = port;
        target.Uuid = MUuid.Text.Trim();
        target.Username = MUser.Text.Trim();
        target.Password = MPassword.Text.Trim();
        target.Method = ComboVal(MMethod, "aes-256-gcm");
        target.Flow = MFlow.Text.Trim();
        target.Network = ComboVal(MNetwork, "tcp");
        target.Security = ComboVal(MSecurity, "none");
        target.HeaderType = ComboVal(MHeaderType, "none");
        target.Fingerprint = ComboVal(MFingerprint, "chrome");
        target.Sni = MSni.Text.Trim();
        var alpnV = ComboVal(MAlpn, "none");
        target.Alpn = alpnV == "none" ? "" : alpnV;
        target.PublicKey = protocol == "wireguard" ? MWgPublic.Text.Trim() : MPbk.Text.Trim();
        target.ShortId = MSid.Text.Trim();
        target.Path = MPath.Text.Trim();
        target.Host = MHost.Text.Trim();
        target.ServiceName = MServiceName.Text.Trim();
        target.Mode = MMode.Text.Trim();
        target.PrivateKey = MWgPrivate.Text.Trim();
        target.PresharedKey = MWgPsk.Text.Trim();
        target.WgAddress = MWgAddress.Text.Trim();
        target.Mtu = int.TryParse(MWgMtu.Text.Trim(), out var wgMtu) && wgMtu > 0 ? wgMtu : 1420;
        target.Reserved = MWgReserved.Text.Trim();
        target.Encryption = protocol == "vmess" ? "auto" : "none";
        if (_editingConfigId is null) _store.Add(target);
        else _store.Save();
        _editingConfigId = null;
        RebuildGroups();
        _connectView = "picker";
        ShowActivePanel(-1);
    }

    private static string ComboVal(System.Windows.Controls.ComboBox c, string fallback)
    {
        var v = c.SelectedItem as string;
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    private static void SetCombo(System.Windows.Controls.ComboBox c, string? value)
    {
        foreach (var it in c.Items)
            if (it is string s && string.Equals(s, value, System.StringComparison.OrdinalIgnoreCase)) { c.SelectedItem = it; return; }
        if (c.Items.Count > 0) c.SelectedIndex = 0;
    }

    private static int PanelIndex(string tab) => tab switch
    {
        "connect" => 0,
        "usage" => 1,
        "tools" => 2,
        "about" => 3,
        "logs" => 4,
        "settings" => 5,
        _ => 0
    };

    private static void StyleNav(System.Windows.Controls.Button b, bool active)
    {
        b.Tag = active ? "on" : "off";
        b.Foreground = active ? NavActive : Muted;
    }

    private void Nav_Connect(object sender, RoutedEventArgs e) { _connectView = "connection"; RefreshSelectedBox(); SelectTab("connect"); }
    private void Nav_Usage(object sender, RoutedEventArgs e) => SelectTab("usage");
    private void Nav_Tools(object sender, RoutedEventArgs e) { _toolsView = "tools"; SelectTab("tools"); }
    private void Nav_About(object sender, RoutedEventArgs e) => SelectTab("about");
    private void OpenSettings(object sender, RoutedEventArgs e) => SelectTab("settings");

    private void SetPowerVisual(ConnectionState state)
    {
        if (ConnectButton.IsMouseOver) return;
        System.Windows.Media.Color main;
        System.Windows.Media.Color icon;
        double glowO;
        int ms;
        switch (state)
        {
            case ConnectionState.Connected:
                main = System.Windows.Media.Color.FromRgb(0x4B, 0xF0, 0xA4);
                icon = System.Windows.Media.Color.FromRgb(0x5C, 0xF5, 0xB0);
                glowO = 0.85; ms = 450;
                break;
            case ConnectionState.Connecting:
                main = System.Windows.Media.Color.FromRgb(0xFF, 0xA9, 0x4D);
                icon = System.Windows.Media.Color.FromRgb(0xFF, 0xB8, 0x6B);
                glowO = 0.7; ms = 280;
                break;
            default:
                main = System.Windows.Media.Color.FromRgb(0xE0, 0x4A, 0x50);
                icon = System.Windows.Media.Color.FromRgb(0xE0, 0x4A, 0x50);
                glowO = 0.0; ms = 350;
                break;
        }
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(ms));
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        PowerStroke.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(main, dur) { EasingFunction = ease });
        PowerIconFill.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(icon, dur) { EasingFunction = ease });
        PowerCircleGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(main, dur) { EasingFunction = ease });
        PowerCircleGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(glowO, dur) { EasingFunction = ease });
        PowerIconGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(main, dur) { EasingFunction = ease });
        PowerIconGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(glowO, dur) { EasingFunction = ease });
    }

    private bool _pinging;

    private async void PingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pinging) return;
        if (_store.Selected() is not ProxyConfig cfg) return;
        _pinging = true;
        PingText.Text = "...";
        cfg.Ping = -2;
        try
        {
            var ms = await DelayTester.Measure(cfg);
            cfg.Ping = ms >= 0 ? ms : -1;
            PingText.Text = ms >= 0 ? Strings.LocalizeDigits($"{ms} ms", _lang) : "\u2014";
        }
        catch
        {
            cfg.Ping = -1;
            PingText.Text = "\u2014";
        }
        finally
        {
            _pinging = false;
        }
    }

    private static void FlyEnter(System.Windows.Media.TranslateTransform iconT, System.Windows.Media.TranslateTransform textT, FrameworkElement panel, FrameworkElement icon)
    {
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
        };
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(300));
        double shift = (panel.ActualWidth - icon.ActualWidth) / 2.0;
        if (shift < 0) shift = 0;
        double away = panel.ActualWidth + 40;
        iconT.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(shift, dur) { EasingFunction = ease });
        textT.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(away, dur) { EasingFunction = ease });
    }

    private static void FlyLeave(System.Windows.Media.TranslateTransform iconT, System.Windows.Media.TranslateTransform textT)
    {
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(260));
        iconT.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, dur) { EasingFunction = ease });
        textT.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, dur) { EasingFunction = ease });
    }

    private void PasteHoverEnter(object sender, System.Windows.Input.MouseEventArgs e) => FlyEnter(PasteIconT, PasteTextT, PasteStack, PasteIcon);

    private void PasteHoverLeave(object sender, System.Windows.Input.MouseEventArgs e) => FlyLeave(PasteIconT, PasteTextT);

    private void ManualHoverEnter(object sender, System.Windows.Input.MouseEventArgs e) => FlyEnter(ManualIconT, ManualTextT, ManualStack, ManualIcon);

    private void ManualHoverLeave(object sender, System.Windows.Input.MouseEventArgs e) => FlyLeave(ManualIconT, ManualTextT);

    private void PingPress(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        PingRootS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.95, new System.Windows.Duration(TimeSpan.FromMilliseconds(70))));
        PingRootS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.95, new System.Windows.Duration(TimeSpan.FromMilliseconds(70))));
    }

    private void PingRelease(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        PingRootS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(120))));
        PingRootS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(120))));
    }

    private void PowerHoverEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var orange = System.Windows.Media.Color.FromRgb(0xFF, 0xA9, 0x4D);
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(350));
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        PowerStroke.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(orange, dur) { EasingFunction = ease });
        PowerIconFill.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(orange, dur) { EasingFunction = ease });
        PowerCircleGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(orange, dur) { EasingFunction = ease });
        PowerIconGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(orange, dur) { EasingFunction = ease });
        PowerCircleGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.55, dur) { EasingFunction = ease });
    }

    private void PowerHoverLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        PowerRelease(sender, e);
        SetPowerVisual(_xray.State);
    }

    private void PowerPress(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(90));
        PowerPressS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new System.Windows.Media.Animation.DoubleAnimation(0.965, dur));
        PowerPressS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(0.965, dur));
    }

    private void PowerRelease(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var back = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.6 };
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(320));
        PowerPressS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new System.Windows.Media.Animation.DoubleAnimation(1, dur) { EasingFunction = back });
        PowerPressS.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(1, dur) { EasingFunction = back });
    }

    private void TitleMin_Click(object sender, RoutedEventArgs e) => WindowState = System.Windows.WindowState.Minimized;

    private void ToggleMainMenu(object sender, RoutedEventArgs e)
    {
        if (MenuOverlay.Visibility == System.Windows.Visibility.Visible)
        {
            CloseMainMenu();
            return;
        }
        MenuOverlay.Visibility = System.Windows.Visibility.Visible;
        double from = FlowDirection == System.Windows.FlowDirection.RightToLeft ? 260 : -260;
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        DrawerT.X = from;
        DrawerT.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, new System.Windows.Duration(TimeSpan.FromMilliseconds(240))) { EasingFunction = ease });
        MenuScrim.BeginAnimation(System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.45, new System.Windows.Duration(TimeSpan.FromMilliseconds(240))));
    }

    private void BtnHoverEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Template is null) return;
        if (b.Template.FindName("Hv", b) is System.Windows.Controls.Border hv)
            hv.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0.45, new System.Windows.Duration(TimeSpan.FromMilliseconds(150))));
    }

    private void BtnHoverLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Template is null) return;
        if (b.Template.FindName("Hv", b) is System.Windows.Controls.Border hv)
            hv.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, new System.Windows.Duration(TimeSpan.FromMilliseconds(180))));
        if (b.Template.FindName("Sc", b) is System.Windows.Media.ScaleTransform sc)
        {
            sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(120))));
            sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(120))));
        }
    }

    private void BtnPressDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Template is null) return;
        if (b.Template.FindName("Sc", b) is System.Windows.Media.ScaleTransform sc)
        {
            sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0.95, new System.Windows.Duration(TimeSpan.FromMilliseconds(70))));
            sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0.95, new System.Windows.Duration(TimeSpan.FromMilliseconds(70))));
        }
        if (b.Template.FindName("Hv", b) is System.Windows.Controls.Border hv)
            hv.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0.85, new System.Windows.Duration(TimeSpan.FromMilliseconds(50))));
    }

    private void BtnPressUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Template is null) return;
        if (b.Template.FindName("Sc", b) is System.Windows.Media.ScaleTransform sc)
        {
            sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(120))));
            sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(120))));
        }
        if (b.Template.FindName("Hv", b) is System.Windows.Controls.Border hv)
            hv.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(b.IsMouseOver ? 0.45 : 0.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(120))));
    }

    private static void ResetHover(System.Windows.DependencyObject root)
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var ch = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (ch is System.Windows.Controls.Button b && b.Template?.FindName("Hv", b) is System.Windows.Controls.Border hv)
                hv.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, new System.Windows.Duration(TimeSpan.FromMilliseconds(100))));
            ResetHover(ch);
        }
    }

    private void CloseMainMenu()
    {
        ResetHover(MenuDrawer);
        double to = FlowDirection == System.Windows.FlowDirection.RightToLeft ? 260 : -260;
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        var slide = new System.Windows.Media.Animation.DoubleAnimation(to, new System.Windows.Duration(TimeSpan.FromMilliseconds(200))) { EasingFunction = ease };
        slide.Completed += (_, _) => MenuOverlay.Visibility = System.Windows.Visibility.Collapsed;
        DrawerT.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        MenuScrim.BeginAnimation(System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, new System.Windows.Duration(TimeSpan.FromMilliseconds(200))));
    }

    private void CloseMenu_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CloseMainMenu();

    private void MenuGo_Connect(object sender, RoutedEventArgs e) { CloseMainMenu(); Nav_Connect(sender, e); }

    private void MenuGo_Usage(object sender, RoutedEventArgs e) { CloseMainMenu(); Nav_Usage(sender, e); }

    private void MenuGo_Tools(object sender, RoutedEventArgs e) { CloseMainMenu(); Nav_Tools(sender, e); }

    private void MenuGo_About(object sender, RoutedEventArgs e) { CloseMainMenu(); Nav_About(sender, e); }

    private void MenuGo_Logs(object sender, RoutedEventArgs e) { CloseMainMenu(); SelectTab("logs"); }

    private void MenuGo_Settings(object sender, RoutedEventArgs e) { CloseMainMenu(); SelectTab("settings"); }

    private bool _justProxy;
    private bool _justProxyActive;





    private void OpenQuickSettings(object sender, RoutedEventArgs e)
    {
        bool tun = ModeTun.IsChecked == true;
        QSCheckSystem.Visibility = !tun && !_justProxy ? Visibility.Visible : Visibility.Collapsed;
        QSCheckProxy.Visibility = !tun && _justProxy ? Visibility.Visible : Visibility.Collapsed;
        QSCheckTun.Visibility = tun ? Visibility.Visible : Visibility.Collapsed;
        QuickOverlay.Visibility = Visibility.Visible;
        QuickDim.BeginAnimation(System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.55, System.TimeSpan.FromMilliseconds(200)));
        var up = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        QuickSheetT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, up);
    }

    private void CloseQuickSettings()
    {
        QuickDim.BeginAnimation(System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(200)));
        var down = new System.Windows.Media.Animation.DoubleAnimation(300, System.TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
        };
        down.Completed += (_, _) => QuickOverlay.Visibility = Visibility.Collapsed;
        QuickSheetT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, down);
    }

    private void QuickDim_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CloseQuickSettings();

    private bool _sheetDragging;
    private double _sheetDragStartY;
    private double _sheetStartOffset;

    private void QuickSheet_DragStart(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
        _sheetDragging = true;
        _sheetDragStartY = e.GetPosition(this).Y;
        _sheetStartOffset = QuickSheetT.Y;
        QuickSheetT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        QuickSheetT.Y = _sheetStartOffset;
        QuickSheet.CaptureMouse();
    }

    private void QuickSheet_DragMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_sheetDragging) return;
        double dy = e.GetPosition(this).Y - _sheetDragStartY;
        QuickSheetT.Y = System.Math.Max(0, _sheetStartOffset + dy);
    }

    private void QuickSheet_DragEnd(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_sheetDragging) return;
        _sheetDragging = false;
        QuickSheet.ReleaseMouseCapture();
        if (QuickSheetT.Y > QuickSheet.ActualHeight * 0.32)
        {
            CloseQuickSettings();
        }
        else
        {
            var up = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            QuickSheetT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, up);
        }
    }

    private void QuickMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string mode) return;
        SetMode(mode);
        CloseQuickSettings();
    }

    private void SetMode(string mode)
    {
        switch (mode)
        {
            case "tun":
                ModeTun.IsChecked = true;
                _justProxy = false;
                break;
            case "proxy":
                ModeProxy.IsChecked = true;
                _justProxy = true;
                break;
            default:
                ModeProxy.IsChecked = true;
                _justProxy = false;
                break;
        }
        UpdateTrayModeChecks();
    }

    private void TitleClose_Click(object sender, RoutedEventArgs e) => Close();

    private async void FetchEarthLocation()
    {
        bool connected = _xray.State == ConnectionState.Connected;
        if (connected) await Task.Delay(600);
        var loc = await IpLocator.FetchAsync(connected);
        _globe.SetLocation(loc ?? IpLocator.Fallback);
    }

    private void ApplyLanguage()
    {
        string[] keys =
        {
            "system_proxy", "tun", "fragment", "split", "sniffing", "takes_effect", "add_hint", "add",
            "subscriptions", "refresh", "remove", "servers", "fastest", "delete", "route_only",
            "nav_connect", "nav_usage", "nav_tools", "nav_about", "check_update", "theme", "nav_logs", "status_disconnected", "status_connected", "coming_soon",
            "geo_files", "change", "reset", "geo_hint",
            "usage_all_time", "range_today", "range_7d", "range_30d", "range_custom",
            "usage_from", "usage_to", "usage_hint", "usage_no_data", "usage_download", "usage_upload", "usage_most_used",
            "auto_refresh", "auto_off", "cf_title", "cf_scan", "cf_use", "cf_hint", "cf_pool", "cf_stop", "cf_results",
            "about_tagline", "about_engine", "about_support", "about_source", "q_title", "q_test", "q_hint", "q_rating", "q_download", "q_upload", "q_ping", "q_jitter", "q_latency", "q_ping_idle", "q_ping_down", "q_ping_up", "conn_settings", "conn_mode", "f_alpn", "f_wgaddr", "f_address", "f_cipher", "f_fingerprint", "f_flow", "f_header", "f_hosthdr", "f_mtu", "f_mode", "f_name", "f_password", "f_path", "f_peerkey", "f_port", "f_psk", "f_privkey", "f_protocol", "f_pbk", "f_reserved", "f_sni", "f_sid", "f_uuid", "f_username", "f_grpc", "mixed_port", "port_invalid", "log_level", "log_none", "log_error", "log_warning", "log_info", "log_debug",
            "copy", "net_off", "selected_server", "tap_choose", "choose_server", "paste_clipboard", "add_manually", "save", "cancel", "back", "test_all", "testing",
            "edit", "rename", "search", "sort", "sort_added", "sort_alpha", "sort_fastest",
            "quick_title", "mode_proxy", "mode_proxy_d", "mode_system", "mode_system_d", "mode_tun", "mode_tun_d"
        };
        foreach (var k in keys) Resources["Str." + k] = Strings.Get(_lang, k);

        FlowDirection = _lang == Lang.Fa
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;

        PingLabelConverter.Farsi = _lang == Lang.Fa;

        FontFamily = _lang == Lang.Fa
            ? new System.Windows.Media.FontFamily("Vazirmatn, Tahoma, Segoe UI")
            : new System.Windows.Media.FontFamily("pack://application:,,,/GRoute;component/Fonts/#Lexend, Segoe UI");

        LogBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;

        LangToggle.IsChecked = _lang == Lang.Fa;
        LangLabel.Text = _lang == Lang.Fa ? "فارسی" : "English";

        ApplyState(_xray.State);
        RefreshGeoStatus();
        if (UsagePanel.Visibility == Visibility.Visible) RebuildUsage();
        RefreshSelectedBox();
        StyleAuto();

        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var vtext = ver is null ? "" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
        AboutVersion.Text = Strings.LocalizeDigits($"{Strings.Get(_lang, "about_version")} {vtext}", _lang);
    }

    private void LangToggle_Click(object sender, RoutedEventArgs e)
    {
        _lang = LangToggle.IsChecked == true ? Lang.Fa : Lang.En;
        _store.SetLang(_lang);
        ApplyLanguage();
        RebuildGroups();
    }

    private void ApplyState(ConnectionState state)
    {
        SetPowerVisual(state);
        _particles.SetTint(state switch
        {
            ConnectionState.Connected => System.Windows.Media.Color.FromRgb(0x4B, 0xF0, 0xA4),
            ConnectionState.Connecting => System.Windows.Media.Color.FromRgb(0xFF, 0xA9, 0x4D),
            _ => System.Windows.Media.Color.FromRgb(0x5B, 0x83, 0xD6)
        });
        SetStatsCardVisual(state);
        AnimateWindowHeight();
    }

    private void SetStatsCardVisual(ConnectionState state)
    {
        bool on = state == ConnectionState.Connected;
        ConnBox.IsHitTestVisible = on;
        DiscoBox.IsHitTestVisible = !on;
        if (on && _store.Selected() is ProxyConfig cfg)
        {
            string method = cfg.Protocol.ToUpperInvariant() + " \u00b7 " + cfg.Network.ToUpperInvariant();
            if (!string.IsNullOrEmpty(cfg.Security) && cfg.Security != "none")
                method += " \u00b7 " + cfg.Security.ToUpperInvariant();
            MethodText.Text = method;
        }
        DiscoBox.BeginAnimation(System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(on ? 0.0 : 1.0,
                new System.Windows.Duration(TimeSpan.FromMilliseconds(260))));
        FrameworkElement[] chips = { Chip1, Chip2, Chip3, Chip4, Chip5, PingButton };
        System.Windows.Media.TranslateTransform[] shifts = { Chip1T, Chip2T, Chip3T, Chip4T, Chip5T, PingShift };
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        for (int i = 0; i < chips.Length; i++)
        {
            if (on)
            {
                chips[i].BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
                shifts[i].BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
                chips[i].Opacity = 0;
                shifts[i].Y = 18;
                var d = new System.Windows.Duration(TimeSpan.FromMilliseconds(320));
                var delay = TimeSpan.FromMilliseconds(90 * i);
                chips[i].BeginAnimation(System.Windows.UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(1.0, d) { BeginTime = delay });
                shifts[i].BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0.0, d) { BeginTime = delay, EasingFunction = ease });
            }
            else
            {
                var d = new System.Windows.Duration(TimeSpan.FromMilliseconds(220));
                var delay = TimeSpan.FromMilliseconds(70 * i);
                chips[i].BeginAnimation(System.Windows.UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0.0, d) { BeginTime = delay });
                shifts[i].BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(18.0, d) { BeginTime = delay, EasingFunction = ease });
            }
        }
    }

    private void OnStateChanged(ConnectionState state) => Dispatcher.Invoke(() =>
    {
        ApplyState(state);
        UpdateTrayMenu(state);
        if (state is ConnectionState.Disconnected or ConnectionState.Error)
            StopStatsPolling();
        if (state is ConnectionState.Connected or ConnectionState.Disconnected)
            FetchEarthLocation();
    });

    private void OnLog(string line) => Dispatcher.Invoke(() => AppendLine(line));

    private async Task AddFromText(string input)
    {
        var text = input.Trim();
        if (text.Length == 0)
        {
            AppendLine("Paste a server link or subscription URL first.");
            return;
        }

        bool isUrl =
            (text.StartsWith("http://", StringComparison.Ordinal) ||
             text.StartsWith("https://", StringComparison.Ordinal)) &&
            !text.Contains('\n') && !text.Contains('\r');

        if (isUrl)
        {
            await AddSubscription(text);
            return;
        }

        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int added = 0;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var cfg = ConfigParser.Parse(line);
            if (cfg is null) continue;
            _store.Add(cfg);
            added++;
        }

        if (added == 0)
        {
            AppendLine("Couldn't read any links. Supported: vless, vmess, trojan, ss.");
            return;
        }

        RebuildGroups();
        AppendLine($"Added {added} server{(added == 1 ? "" : "s")}.");
    }

    private bool _addingSub;

    private async Task AddSubscription(string url)
    {
        if (_addingSub) return;
        _addingSub = true;
        AppendLine("Fetching subscription...");
        try
        {
            var result = await SubscriptionFetcher.FetchFull(url);
            if (result.Configs.Count == 0)
            {
                AppendLine("No servers found in that subscription.");
                return;
            }

            var name = "Subscription";
            try { name = new Uri(url).Host; } catch { }

            var info = result.UserInfo;
            var sub = new Subscription
            {
                Name = name,
                Url = url,
                Used = info?.Used ?? 0,
                Total = info?.Total ?? 0,
                Expire = info?.Expire ?? 0
            };

            _store.UpsertSubscription(sub, result.Configs);
            RebuildGroups();
            AppendLine($"Added subscription {name} ({result.Configs.Count} servers).");
        }
        catch (Exception ex)
        {
            AppendLine("Fetch failed: " + ex.Message);
        }
        finally
        {
            _addingSub = false;
        }
    }

    private async void RefreshSub_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string id) return;
        var existing = _store.Subscriptions.FirstOrDefault(s => s.Id == id);
        if (existing is null) return;
        AppendLine($"Refreshing {existing.Name}...");
        await RefreshOne(existing);
    }

    private async Task RefreshOne(Subscription existing)
    {
        try
        {
            var result = await SubscriptionFetcher.FetchFull(existing.Url);
            var info = result.UserInfo;
            var updated = new Subscription
            {
                Id = existing.Id,
                Name = existing.Name,
                Url = existing.Url,
                Used = info?.Used ?? existing.Used,
                Total = info?.Total ?? existing.Total,
                Expire = info?.Expire ?? existing.Expire
            };

            _store.UpsertSubscription(updated, result.Configs);
            RebuildGroups();
            AppendLine($"{existing.Name}: {result.Configs.Count} servers.");
        }
        catch (Exception ex)
        {
            AppendLine("Refresh failed: " + ex.Message);
        }
    }

    private async Task RefreshAllSubs()
    {
        foreach (var sub in _store.Subscriptions.ToList())
            await RefreshOne(sub);
    }

    private void SetupAutoRefresh()
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer = null;
        int h = _store.AutoRefreshHours;
        if (h <= 0) return;
        _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(h) };
        _autoRefreshTimer.Tick += async (_, _) => await RefreshAllSubs();
        _autoRefreshTimer.Start();
    }

    private void Auto_Off(object sender, RoutedEventArgs e) => SetAuto(0);
    private void Auto_1(object sender, RoutedEventArgs e) => SetAuto(1);
    private void Auto_6(object sender, RoutedEventArgs e) => SetAuto(6);
    private void Auto_12(object sender, RoutedEventArgs e) => SetAuto(12);
    private void Auto_24(object sender, RoutedEventArgs e) => SetAuto(24);

    private void MixedPort_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (MixedPortBox is null) return;
        if (int.TryParse(MixedPortBox.Text, out int p) && p >= 1 && p <= 65535)
        {
            _store.SetMixedPort(p);
            Core.ConfigBuilder.HttpPort = p;
            MixedPortHint.Visibility = Visibility.Collapsed;
        }
        else
        {
            MixedPortHint.Visibility = Visibility.Visible;
        }
    }

    private void LogLevel_Set(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string level)
        {
            _store.SetLogLevel(level);
            Core.ConfigBuilder.LogLevel = level;
        }
    }

    private void StyleLogLevel()
    {
        string lv = _store.LogLevel;
        LogNone.IsChecked = lv == "none";
        LogError.IsChecked = lv == "error";
        LogWarning.IsChecked = lv == "warning";
        LogInfo.IsChecked = lv == "info";
        LogDebug.IsChecked = lv == "debug";
    }

    private void SetAuto(int hours)
    {
        _store.SetAutoRefreshHours(hours);
        SetupAutoRefresh();
        StyleAuto();
    }

    private void StyleAuto()
    {
        int h = _store.AutoRefreshHours;
        AutoOff.IsChecked = h == 0;
        Auto1.IsChecked = h == 1;
        Auto6.IsChecked = h == 6;
        Auto12.IsChecked = h == 12;
        Auto24.IsChecked = h == 24;
    }

    private string _toolsView = "tools";
    private int _scanPool = 300;
    private CancellationTokenSource? _scanCts;

    private void OpenScanner(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _toolsView = "scanner";
        ShowActivePanel(1);
    }

    private void CloseScanner(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        _toolsView = "tools";
        ShowActivePanel(-1);
    }

    private void Pool_Set(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string s && int.TryParse(s, out int n))
            _scanPool = n;
    }

    private void StopScan(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

    private async void ScanCloudflare(object sender, RoutedEventArgs e)
    {
        if (_scanCts is not null) return;
        _scanCts = new CancellationTokenSource();
        ScanButton.IsEnabled = false;
        ScanStopButton.Visibility = Visibility.Visible;
        ScanResultsTitle.Visibility = Visibility.Collapsed;
        _scanResults.Clear();
        int total = _scanPool;
        ScanProgressTrack.Visibility = Visibility.Visible;
        ScanProgressBar.Width = 0;
        var progress = new Progress<int>(d =>
        {
            ScanStatus.Text = Strings.LocalizeDigits($"{Strings.Get(_lang, "cf_scanning")} {d}/{total}", _lang);
            double trackW = ScanProgressTrack.ActualWidth;
            if (trackW > 0) ScanProgressBar.Width = trackW * d / total;
        });
        try
        {
            int port = 443;
            var results = await CloudflareScanner.Scan(total, port, 50, progress, _scanCts.Token);
            foreach (var r in results) _scanResults.Add(r);
            ScanStatus.Text = results.Count == 0
                ? Strings.Get(_lang, "cf_none")
                : Strings.LocalizeDigits($"{results.Count} {Strings.Get(_lang, "cf_found")}", _lang);
            ScanResultsTitle.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            ScanStatus.Text = Strings.Get(_lang, "cf_stopped");
            ScanResultsTitle.Visibility = _scanResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ScanStatus.Text = ex.Message;
        }
        finally
        {
            ScanButton.IsEnabled = true;
            ScanStopButton.Visibility = Visibility.Collapsed;
            ScanProgressTrack.Visibility = Visibility.Collapsed;
            _scanCts.Dispose();
            _scanCts = null;
        }
    }

    private void UseCleanIp(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string ip) return;
        if (_store.Selected() is not ProxyConfig cfg)
        {
            AppendLine("Select a server first, then Use a clean IP to set its address.");
            return;
        }
        if (string.IsNullOrEmpty(cfg.Sni) && string.IsNullOrEmpty(cfg.Host))
            cfg.Sni = cfg.Address;
        cfg.Address = ip;
        _store.Save();
        RebuildGroups();
        AppendLine($"Applied {ip} to {cfg.Name}.");
    }

    private void OpenTelegram(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://t.me/OracleVPNSupport") { UseShellExecute = true }); } catch { }
    }

    private void OpenSource(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/SuOracle/GRoute-Win") { UseShellExecute = true }); } catch { }
    }

    private void OpenQuality(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _toolsView = "quality";
        ShowActivePanel(1);
    }

    private void CloseQuality(object sender, RoutedEventArgs e)
    {
        _qualityCts?.Cancel();
        _toolsView = "tools";
        ShowActivePanel(-1);
    }

    private CancellationTokenSource? _qualityCts;

    private static double QBarFraction(double mbps)
    {
        const double ceiling = 1000.0;
        double frac = Math.Log(1 + Math.Max(0, mbps)) / Math.Log(1 + ceiling);
        return frac > 1 ? 1 : frac;
    }

    private static void QSetBar(System.Windows.Controls.Border bar, System.Windows.Controls.Border track, double mbps)
    {
        double w = track.ActualWidth * QBarFraction(mbps);
        if (w < 0) w = 0;
        var anim = new System.Windows.Media.Animation.DoubleAnimation(w, new System.Windows.Duration(TimeSpan.FromMilliseconds(450)))
        { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
        bar.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, anim);
    }

    private async void RunQuality(object sender, RoutedEventArgs e)
    {
        if (_qualityCts is not null) return;
        if (!_xray.IsRunning)
        {
            QualityStatus.Text = Strings.Get(_lang, "q_not_connected");
            return;
        }
        _qualityCts = new CancellationTokenSource();
        QualityButton.IsEnabled = false;
        QRatingText.Text = "\u2026";
        QRatingText.Foreground = Muted;
        QScoreBar.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
        QDownBar.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
        QUpBar.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
        QScoreBar.Width = 0;
        QDownVal.Text = "\u2014";
        QUpVal.Text = "\u2014";
        QIdleVal.Text = "\u2014";
        QDownPingVal.Text = "\u2014";
        QUpPingVal.Text = "\u2014";
        QJitterVal.Text = "\u2014";
        QDownBar.Width = 0;
        QUpBar.Width = 0;

        var stage = new Progress<string>(st =>
            QualityStatus.Text = Strings.Get(_lang, st switch
            {
                "latency" => "q_testing_latency",
                "upload" => "q_testing_upload",
                _ => "q_testing_speed"
            }));
        var live = new Progress<(string Phase, double Mbps)>(t =>
        {
            if (t.Phase == "download")
            {
                QDownVal.Text = Strings.LocalizeDigits($"{t.Mbps:0.00}", _lang);
                QSetBar(QDownBar, QDownTrack, t.Mbps);
            }
            else
            {
                QUpVal.Text = Strings.LocalizeDigits($"{t.Mbps:0.00}", _lang);
                QSetBar(QUpBar, QUpTrack, t.Mbps);
            }
        });
        try
        {
            var r = await QualityTest.Run(ConfigBuilder.HttpPort, stage, live, _qualityCts.Token);
            ShowQuality(r);
            QualityStatus.Text = "";
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
            _qualityCts.Dispose();
            _qualityCts = null;
        }
    }

    private void ShowQuality(QualityResult r)
    {
        QDownVal.Text = r.DownMbps.HasValue ? Strings.LocalizeDigits($"{r.DownMbps.Value:0.00}", _lang) : "\u2014";
        QUpVal.Text = r.UpMbps.HasValue ? Strings.LocalizeDigits($"{r.UpMbps.Value:0.00}", _lang) : "\u2014";
        QIdleVal.Text = r.IdlePing.HasValue ? Strings.LocalizeDigits($"{r.IdlePing.Value}", _lang) : "\u2014";
        QDownPingVal.Text = r.DownPing.HasValue ? Strings.LocalizeDigits($"{r.DownPing.Value}", _lang) : "\u2014";
        QUpPingVal.Text = r.UpPing.HasValue ? Strings.LocalizeDigits($"{r.UpPing.Value}", _lang) : "\u2014";
        QJitterVal.Text = r.Jitter.HasValue ? Strings.LocalizeDigits($"{r.Jitter.Value}", _lang) : "\u2014";

        AnimateBarTo(QDownBar, r.DownMbps.HasValue ? QDownTrack.ActualWidth * QBarFraction(r.DownMbps.Value) : 0);
        AnimateBarTo(QUpBar, r.UpMbps.HasValue ? QUpTrack.ActualWidth * QBarFraction(r.UpMbps.Value) : 0);

        int score = QualityTest.Score(r);
        string key;
        System.Windows.Media.Color col;
        if (score >= 82) { key = "q_rate_excellent"; col = System.Windows.Media.Color.FromRgb(0x3C, 0xCB, 0x7F); }
        else if (score >= 62) { key = "q_rate_great"; col = System.Windows.Media.Color.FromRgb(0x5B, 0xB8, 0xF0); }
        else if (score >= 42) { key = "q_rate_good"; col = System.Windows.Media.Color.FromRgb(0x6E, 0x9C, 0xF0); }
        else if (score >= 22) { key = "q_rate_fair"; col = System.Windows.Media.Color.FromRgb(0xE5, 0xA6, 0x3C); }
        else { key = "q_rate_poor"; col = System.Windows.Media.Color.FromRgb(0xE0, 0x57, 0x5C); }

        var brush = new System.Windows.Media.SolidColorBrush(col);
        QRatingText.Text = Strings.Get(_lang, key);
        QRatingText.Foreground = brush;
        QScoreBar.Background = brush;
        double trackW = QScoreTrack.ActualWidth > 0 ? QScoreTrack.ActualWidth : 220;
        var scoreAnim = new System.Windows.Media.Animation.DoubleAnimation(0, trackW * Math.Min(100, score) / 100.0,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(900)))
        { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
        QScoreBar.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, scoreAnim);
    }

    private static void AnimateBarTo(System.Windows.Controls.Border bar, double to)
    {
        var anim = new System.Windows.Media.Animation.DoubleAnimation(to, new System.Windows.Duration(TimeSpan.FromMilliseconds(600)))
        { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
        bar.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, anim);
    }

    private void RemoveSub_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string id) return;
        var row = FindAncestor<System.Windows.Controls.StackPanel>(b, "GroupItem");
        if (row is null)
        {
            _store.DeleteSubscription(id);
            RebuildGroups();
            return;
        }
        var tt = new System.Windows.Media.TranslateTransform();
        row.RenderTransform = tt;
        var slide = new System.Windows.Media.Animation.DoubleAnimation(90, System.TimeSpan.FromMilliseconds(230))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
        };
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(230));
        fade.Completed += (_, _) => { _store.DeleteSubscription(id); RebuildGroups(); };
        tt.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        row.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
    }

    private void DeleteServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string id) return;
        var row = FindAncestor<System.Windows.Controls.Border>(b, "CfgBox");
        if (row is null)
        {
            _store.Delete(id);
            RebuildGroups();
            return;
        }
        var tt = new System.Windows.Media.TranslateTransform();
        row.RenderTransform = tt;
        var slide = new System.Windows.Media.Animation.DoubleAnimation(90, System.TimeSpan.FromMilliseconds(230))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
        };
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(230));
        fade.Completed += (_, _) => { _store.Delete(id); RebuildGroups(); };
        tt.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        row.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
    }

    private static T? FindAncestor<T>(System.Windows.DependencyObject start, string name) where T : System.Windows.FrameworkElement
    {
        var d = start;
        while (d is not null)
        {
            if (d is T fe && fe.Name == name) return fe;
            d = (d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D)
                ? System.Windows.Media.VisualTreeHelper.GetParent(d)
                : System.Windows.LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    private static T? FindDescendant<T>(System.Windows.DependencyObject root, string name) where T : System.Windows.FrameworkElement
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T fe && fe.Name == name) return fe;
            var r = FindDescendant<T>(child, name);
            if (r != null) return r;
        }
        return null;
    }

    private bool _renameActive;

    private void RenameSub_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string id) return;
        var row = FindAncestor<System.Windows.Controls.Border>(b, "HeaderBox");
        if (row is null) return;
        var sub = _store.Subscriptions.FirstOrDefault(s => s.Id == id);
        StartRename(FindDescendant<System.Windows.Controls.TextBlock>(row, "SubName"),
            FindDescendant<System.Windows.Controls.TextBox>(row, "SubNameEdit"), "sub", id, sub?.Name);
    }

    private void StartRename(System.Windows.Controls.TextBlock? name, System.Windows.Controls.TextBox? edit, string kind, string id, string? current)
    {
        if (name is null || edit is null) return;
        edit.Text = current ?? "";
        edit.Tag = kind + "|" + id;
        name.Visibility = Visibility.Collapsed;
        edit.Visibility = Visibility.Visible;
        _renameActive = true;
        edit.Focus();
        edit.SelectAll();
    }

    private void RenameKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox edit) return;
        if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; FinishRename(edit, true); }
        else if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; FinishRename(edit, false); }
    }

    private void RenameCommit(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox edit) FinishRename(edit, true);
    }

    private void FinishRename(System.Windows.Controls.TextBox edit, bool commit)
    {
        if (!_renameActive) return;
        _renameActive = false;
        if (commit && edit.Tag is string tag)
        {
            var parts = tag.Split('|', 2);
            var id = parts.Length > 1 ? parts[1] : "";
            var newName = (edit.Text ?? "").Trim();
            if (newName.Length > 0)
            {
                if (parts[0] == "cfg")
                {
                    var c = _store.Configs.FirstOrDefault(x => x.Id == id);
                    if (c != null) c.Name = newName;
                }
                else
                {
                    var s = _store.Subscriptions.FirstOrDefault(x => x.Id == id);
                    if (s != null) s.Name = newName;
                }
                _store.Save();
            }
        }
        RebuildGroups();
    }

    private void NameClip_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Grid clip) return;
        StartMarquee(clip);
    }

    private void NameText_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBlock tb && tb.Parent is System.Windows.Controls.Canvas cv
            && cv.Parent is System.Windows.Controls.Grid clip)
            StartMarquee(clip);
    }

    private static void StartMarquee(System.Windows.Controls.Grid clip)
    {
        if (clip.Children.Count == 0 || clip.Children[0] is not System.Windows.Controls.Canvas cv) return;
        if (cv.RenderTransform is not System.Windows.Media.TranslateTransform shift) return;
        if (cv.Children.Count < 2 || cv.Children[0] is not System.Windows.Controls.TextBlock tb
            || cv.Children[1] is not System.Windows.Controls.TextBlock tb2) return;
        double avail = clip.ActualWidth;
        if (avail <= 1) return;

        double textW = tb.ActualWidth;
        if (textW <= 1)
        {
            tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            textW = tb.DesiredSize.Width;
        }

        shift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        if (textW - avail <= 2)
        {
            tb2.Visibility = Visibility.Collapsed;
            shift.X = 0;
            return;
        }

        double cycle = textW + 48;
        System.Windows.Controls.Canvas.SetLeft(tb2, cycle);
        tb2.Visibility = Visibility.Visible;
        double secs = cycle / 50.0;
        var a = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
        a.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.Zero)));
        a.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(1.2))));
        a.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-cycle, System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(1.2 + secs))));
        shift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, a);
    }

    private void SelectConfig(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border b && b.Tag is string id)
        {
            SetSelected(id);
            _connectView = "connection";
            ShowActivePanel(-1);
        }
    }

    private void SetSelected(string id)
    {
        _store.SelectedId = id;
        _store.Save();
        foreach (var g in _groups)
            foreach (var c in g.Configs)
                c.IsSelected = c.Id == id;
        RefreshSelectedBox();
    }

    private void ToggleGroup(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border b && b.Tag is string id)
        {
            var g = _groups.FirstOrDefault(x => x.Id == id);
            if (g is null || g.IsManual) return;
            g.IsExpanded = !g.IsExpanded;
            if (g.IsExpanded) _expandedIds.Add(id);
            else _expandedIds.Remove(id);
            AnimateWindowHeight();
            if (g.IsExpanded)
                Dispatcher.BeginInvoke(new Action(() => StaggerConfigs(b)),
                    System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void StaggerConfigs(System.Windows.DependencyObject header)
    {
        var root = header;
        while (root != null && root is not System.Windows.Controls.ItemsControl)
            root = System.Windows.Media.VisualTreeHelper.GetParent(root);
        var container = FindByName(System.Windows.Media.VisualTreeHelper.GetParent(header) ?? header, "CfgContainer")
                        ?? FindSiblingContainer(header);
        if (container is null) return;
        var boxes = new System.Collections.Generic.List<System.Windows.FrameworkElement>();
        CollectByName(container, "CfgBox", boxes);
        for (int i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var tt = new System.Windows.Media.TranslateTransform(0, 16);
            box.RenderTransform = tt;
            box.Opacity = 0;
            var begin = TimeSpan.FromMilliseconds(45 * i);
            box.BeginAnimation(System.Windows.UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, new System.Windows.Duration(TimeSpan.FromMilliseconds(280))) { BeginTime = begin });
            tt.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, new System.Windows.Duration(TimeSpan.FromMilliseconds(320)))
                { BeginTime = begin, EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        }
    }

    private static System.Windows.FrameworkElement? FindSiblingContainer(System.Windows.DependencyObject header)
    {
        var p = System.Windows.Media.VisualTreeHelper.GetParent(header);
        while (p != null && p is not System.Windows.Controls.StackPanel)
            p = System.Windows.Media.VisualTreeHelper.GetParent(p);
        if (p is null) return null;
        return FindByName(p, "CfgContainer");
    }

    private static System.Windows.FrameworkElement? FindByName(System.Windows.DependencyObject root, string name)
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var ch = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (ch is System.Windows.FrameworkElement fe && fe.Name == name) return fe;
            var found = FindByName(ch, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void CollectByName(System.Windows.DependencyObject root, string name, System.Collections.Generic.List<System.Windows.FrameworkElement> outList)
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var ch = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (ch is System.Windows.FrameworkElement fe && fe.Name == name) outList.Add(fe);
            CollectByName(ch, name, outList);
        }
    }

    private void TestAllHoverEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Template is null) return;
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(300));
        if (b.Template.FindName("OuterA", b) is System.Windows.Media.GradientStop a)
            a.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation(System.Windows.Media.Color.FromArgb(0xC8, 0x6E, 0x9C, 0xF0), dur));
        if (b.Template.FindName("OuterB", b) is System.Windows.Media.GradientStop bs)
            bs.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation(System.Windows.Media.Color.FromArgb(0x8A, 0x6E, 0x9C, 0xF0), dur));
        if (b.Template.FindName("TestGlow", b) is System.Windows.Media.Effects.DropShadowEffect gl)
            gl.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0.6, dur));
    }

    private void TestAllHoverLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Template is null) return;
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(300));
        if (b.Template.FindName("OuterA", b) is System.Windows.Media.GradientStop a)
            a.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation(System.Windows.Media.Color.FromArgb(0x6E, 0x6E, 0x9C, 0xF0), dur));
        if (b.Template.FindName("OuterB", b) is System.Windows.Media.GradientStop bs)
            bs.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation(System.Windows.Media.Color.FromArgb(0x38, 0x6E, 0x9C, 0xF0), dur));
        if (b.Template.FindName("TestGlow", b) is System.Windows.Media.Effects.DropShadowEffect gl)
            gl.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, dur));
    }

    private System.Windows.Controls.TextBlock? TestLabel()
        => TestAllButton.Template?.FindName("TestAllText", TestAllButton) as System.Windows.Controls.TextBlock;

    private async void TestAll_Click(object sender, RoutedEventArgs e)
    {
        var items = _store.Configs.ToList();
        if (items.Count == 0)
        {
            AppendLine("No servers to test.");
            return;
        }

        if (!DelayTester.XrayAvailable)
        {
            AppendLine("Place xray.exe in the libs folder to measure delay.");
            return;
        }

        TestAllButton.IsEnabled = false;
        if (TestLabel() is { } tl1) tl1.Text = Strings.Get(_lang, "testing");
        foreach (var c in items) { c.PingText = "..."; c.Ping = -2; }

        using var sem = new SemaphoreSlim(8);
        var tasks = items.Select(async c =>
        {
            await sem.WaitAsync();
            try
            {
                var ms = await DelayTester.Measure(c);
                c.PingText = ms >= 0 ? $"{ms} ms" : "fail";
                c.Ping = ms >= 0 ? ms : -1;
            }
            finally
            {
                sem.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);
        TestAllButton.IsEnabled = true;
        if (TestLabel() is { } tl2) tl2.Text = Strings.Get(_lang, "test_all");
        if (_sortMode == SortMode.Fastest) RebuildGroups();
    }

    private void OpenSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchToggle.Visibility = Visibility.Collapsed;
        SearchBox.Visibility = Visibility.Visible;
        var a = new System.Windows.Media.Animation.DoubleAnimation(200, System.TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        SearchBox.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, a);
        SearchInput.Focus();
    }

    private void CloseSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        var a = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
        };
        a.Completed += (_, _) =>
        {
            SearchBox.Visibility = Visibility.Collapsed;
            SearchToggle.Visibility = Visibility.Visible;
        };
        SearchBox.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, a);
    }

    private void SearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchQuery = SearchInput.Text ?? "";
        RebuildGroups();
    }


    private void SortButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
    }

    private void SortOutsideClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!SortPopup.IsOpen) return;
        var d = e.OriginalSource as System.Windows.DependencyObject;
        while (d != null)
        {
            if (ReferenceEquals(d, SortButton)) return;
            d = (d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D)
                ? System.Windows.Media.VisualTreeHelper.GetParent(d)
                : System.Windows.LogicalTreeHelper.GetParent(d);
        }
        SortPopup.IsOpen = false;
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        SortPopup.IsOpen = !SortPopup.IsOpen;
    }

    private void CopySub_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string id) return;
        var sub = _store.Subscriptions.FirstOrDefault(s => s.Id == id);
        if (sub is null || !CopyToClipboard(sub.Url)) return;
        ShowCopied(b);
    }

    private void CopyScanIp(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string ip) return;
        if (!CopyToClipboard(ip)) return;
        var original = b.ToolTip;
        b.ToolTip = Strings.Get(_lang, "copied");
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            b.ToolTip = original;
        };
        timer.Start();
    }

    private void CopyConfig_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || b.Tag is not string id) return;
        var cfg = _groups.SelectMany(g => g.Configs).FirstOrDefault(c => c.Id == id);
        if (cfg is null) return;
        var link = !string.IsNullOrEmpty(cfg.Raw) ? cfg.Raw : ShareLink(cfg);
        if (!CopyToClipboard(link)) return;
        ShowCopied(b);
    }

    private static bool CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < 4; i++)
        {
            try { System.Windows.Clipboard.SetText(text); return true; }
            catch { System.Threading.Thread.Sleep(30); }
        }
        return false;
    }

    private void ShowCopied(System.Windows.Controls.Button b)
    {
        var original = b.ToolTip;
        b.ToolTip = Strings.Get(_lang, "copied");
        if (b.IsMouseOver) AnimateActionWidth(b, MeasureActionWidth(b.ToolTip as string));
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            b.ToolTip = original;
            AnimateActionWidth(b, b.IsMouseOver ? MeasureActionWidth(b.ToolTip as string) : 34);
        };
        timer.Start();
    }

    private void IconActionEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b)
            AnimateActionWidth(b, MeasureActionWidth(b.ToolTip as string));
    }

    private void IconActionLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b)
            AnimateActionWidth(b, b.MinWidth);
    }

    private static void AnimateActionWidth(System.Windows.Controls.Button b, double to)
    {
        var a = new System.Windows.Media.Animation.DoubleAnimation(to, System.TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        b.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, a);
    }

    private static double MeasureActionWidth(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 34;
        var ft = new System.Windows.Media.FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, new System.Windows.Media.Typeface("Segoe UI Semibold"),
            12, System.Windows.Media.Brushes.White, 1.0);
        return System.Math.Ceiling(ft.Width) + 32;
    }

    private static string ShareLink(ProxyConfig c)
    {
        string name = System.Uri.EscapeDataString(c.Name ?? "");
        string Q(string k, string v) => string.IsNullOrEmpty(v) ? "" : "&" + k + "=" + System.Uri.EscapeDataString(v);
        switch (c.Protocol)
        {
            case "vless":
            {
                var q = "encryption=" + System.Uri.EscapeDataString(string.IsNullOrEmpty(c.Encryption) ? "none" : c.Encryption)
                        + Q("security", c.Security) + Q("type", c.Network) + Q("sni", c.Sni)
                        + Q("fp", c.Fingerprint) + Q("pbk", c.PublicKey) + Q("sid", c.ShortId)
                        + Q("flow", c.Flow) + Q("host", c.Host) + Q("path", c.Path);
                return "vless://" + c.Uuid + "@" + c.Address + ":" + c.Port + "?" + q + "#" + name;
            }
            case "trojan":
            {
                var q = "security=" + System.Uri.EscapeDataString(string.IsNullOrEmpty(c.Security) ? "tls" : c.Security)
                        + Q("type", c.Network) + Q("sni", c.Sni) + Q("fp", c.Fingerprint)
                        + Q("pbk", c.PublicKey) + Q("sid", c.ShortId) + Q("flow", c.Flow)
                        + Q("host", c.Host) + Q("path", c.Path);
                return "trojan://" + System.Uri.EscapeDataString(c.Password) + "@" + c.Address + ":" + c.Port + "?" + q + "#" + name;
            }
            case "vmess":
            {
                var o = new System.Collections.Generic.Dictionary<string, string>
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
                var json = System.Text.Json.JsonSerializer.Serialize(o);
                return "vmess://" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
            }
            case "ss":
            case "shadowsocks":
            {
                var userinfo = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(c.Method + ":" + c.Password));
                return "ss://" + userinfo + "@" + c.Address + ":" + c.Port + "#" + name;
            }
            default:
                return "";
        }
    }

    private void SortFastest_Click(object sender, RoutedEventArgs e)
    {
        _sortMode = _sortMode == SortMode.Fastest ? SortMode.Added : SortMode.Fastest;
        UpdateSortChecks();
        SortPopup.IsOpen = false;
        RebuildGroups();
    }

    private void SortAlpha_Click(object sender, RoutedEventArgs e)
    {
        _sortMode = _sortMode == SortMode.Alpha ? SortMode.Added : SortMode.Alpha;
        UpdateSortChecks();
        SortPopup.IsOpen = false;
        RebuildGroups();
    }

    private void SortAdded_Click(object sender, RoutedEventArgs e)
    {
        _sortMode = SortMode.Added;
        UpdateSortChecks();
        SortPopup.IsOpen = false;
        RebuildGroups();
    }

    private void UpdateSortChecks()
    {
        FastestCheck.Opacity = _sortMode == SortMode.Fastest ? 1 : 0;
        AlphaCheck.Opacity = _sortMode == SortMode.Alpha ? 1 : 0;
        AddedCheck.Opacity = _sortMode == SortMode.Added ? 1 : 0;
    }

    private void FragmentToggle_Click(object sender, RoutedEventArgs e)
    {
        _store.SetFragment(FragmentToggle.IsChecked == true);
    }

    private void SplitToggle_Click(object sender, RoutedEventArgs e)
    {
        bool on = SplitToggle.IsChecked == true;
        _store.SetSplitRouting(on);
        if (on && !ConfigBuilder.GeoFilesPresent)
            AppendLine("Split routing needs geoip.dat and geosite.dat in the libs folder.");
    }

    private void SniffMaster_Click(object sender, RoutedEventArgs e)
    {
        bool on = SniffMaster.IsChecked == true;
        _store.SetSniffEnabled(on);
        SetSniffControlsEnabled(on);
    }

    private void SetSniffControlsEnabled(bool on)
    {
        SniffTls.IsEnabled = on;
        SniffHttp.IsEnabled = on;
        SniffQuic.IsEnabled = on;
        SniffFakeDns.IsEnabled = on;
        RouteOnlyToggle.IsEnabled = on;
    }

    private void SniffChanged(object sender, RoutedEventArgs e)
    {
        var list = new System.Collections.Generic.List<string>();
        if (SniffTls.IsChecked == true) list.Add("tls");
        if (SniffHttp.IsChecked == true) list.Add("http");
        if (SniffQuic.IsChecked == true) list.Add("quic");
        if (SniffFakeDns.IsChecked == true) list.Add("fakedns");
        _store.SetSniffProtocols(list);
    }

    private void RouteOnly_Click(object sender, RoutedEventArgs e)
    {
        _store.SetSniffRouteOnly(RouteOnlyToggle.IsChecked == true);
    }

    private void ChangeGeoip(object sender, RoutedEventArgs e) => ImportGeo("geoip.dat");
    private void ChangeGeosite(object sender, RoutedEventArgs e) => ImportGeo("geosite.dat");
    private void ResetGeoip(object sender, RoutedEventArgs e) => ResetGeo("geoip.dat");
    private void ResetGeosite(object sender, RoutedEventArgs e) => ResetGeo("geosite.dat");

    private void ImportGeo(string name)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = name,
            Filter = "Xray data (*.dat)|*.dat|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                Assets.Import(dlg.FileName, name);
                AppendLine($"Imported {name}.");
            }
            catch (Exception ex)
            {
                AppendLine($"Import failed: {ex.Message}");
            }
            RefreshGeoStatus();
        }
    }

    private void ResetGeo(string name)
    {
        if (Assets.ResetToBundled(name)) AppendLine($"{name} reset to the bundled copy.");
        else AppendLine($"No bundled {name} found in libs to reset from.");
        RefreshGeoStatus();
    }

    private void RefreshGeoStatus()
    {
        GeoipStatus.Text = Assets.GeoipPresent ? Strings.Get(_lang, "present") : Strings.Get(_lang, "missing");
        GeositeStatus.Text = Assets.GeositePresent ? Strings.Get(_lang, "present") : Strings.Get(_lang, "missing");
    }

    private void Range_Today(object sender, RoutedEventArgs e) { _range = UsageRange.Today; RebuildUsage(); }
    private void Range_Week(object sender, RoutedEventArgs e) { _range = UsageRange.Week; RebuildUsage(); }
    private void Range_Month(object sender, RoutedEventArgs e) { _range = UsageRange.Month; RebuildUsage(); }

    private void Range_Custom(object sender, RoutedEventArgs e)
    {
        _range = UsageRange.Custom;
        FromDate.SelectedDate ??= DateTime.Today.AddDays(-6);
        ToDate.SelectedDate ??= DateTime.Today;
        RebuildUsage();
    }

    private void CustomDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_range == UsageRange.Custom) RebuildUsage();
    }

    private void RebuildUsage()
    {
        var all = UsageStore.TotalAll();
        AllTimeText.Text = Strings.LocalizeDigits(FormatBytes(all[0] + all[1]), _lang);
        var top = UsageStore.TopConfig();
        if (top is { } tc && tc.Total > 0)
        {
            var flagImg = Flags.ForName(tc.Name);
            if (flagImg is null)
            {
                TopConfigFlag.Visibility = Visibility.Collapsed;
            }
            else
            {
                TopConfigFlagBrush.ImageSource = flagImg;
                TopConfigFlag.Visibility = Visibility.Visible;
            }
            TopConfigName.Text = Flags.DisplayName(tc.Name);
            TopConfigUsage.Text = Strings.LocalizeDigits(FormatBytes(tc.Total), _lang);
            TopConfigBox.Visibility = Visibility.Visible;
        }
        else
        {
            TopConfigBox.Visibility = Visibility.Collapsed;
        }

        System.Collections.Generic.List<UsageStore.StackBar> bars = _range switch
        {
            UsageRange.Today => UsageStore.HourlyTodayStacked(),
            UsageRange.Week => UsageStore.DailyStacked(7),
            UsageRange.Month => UsageStore.DailyStacked(30),
            _ => CustomStacked()
        };

        AssignConfigColors(bars);

        var sum = UsageStore.SumStacked(bars);
        UsageDownText.Text = Strings.LocalizeDigits(FormatBytes(sum[1]), _lang);
        UsageUpText.Text = Strings.LocalizeDigits(FormatBytes(sum[0]), _lang);

        NoDataText.Visibility = bars.Any(b => b.Total > 0) ? Visibility.Collapsed : Visibility.Visible;
        BuildChart(bars);
        BuildLabels(bars);

        RangeToday.IsChecked = _range == UsageRange.Today;
        RangeWeek.IsChecked = _range == UsageRange.Week;
        RangeMonth.IsChecked = _range == UsageRange.Month;
        RangeCustom.IsChecked = _range == UsageRange.Custom;
        CustomDates.Visibility = _range == UsageRange.Custom ? Visibility.Visible : Visibility.Collapsed;
    }

    private System.Collections.Generic.List<UsageStore.StackBar> CustomStacked()
    {
        var from = FromDate.SelectedDate ?? DateTime.Today.AddDays(-6);
        var to = ToDate.SelectedDate ?? DateTime.Today;
        int span = Math.Abs((to.Date - from.Date).Days);
        return span <= 2 ? UsageStore.HourlyStackedRange(from, to) : UsageStore.DailyStackedRange(from, to);
    }

    private void AssignConfigColors(System.Collections.Generic.List<UsageStore.StackBar> bars)
    {
        _cfgColors.Clear();
        var totals = new System.Collections.Generic.Dictionary<string, long>();
        foreach (var b in bars)
            foreach (var s in b.Segments)
            {
                if (string.IsNullOrEmpty(s.Config)) continue;
                totals.TryGetValue(s.Config, out var t);
                totals[s.Config] = t + s.Total;
            }
        var ordered = new System.Collections.Generic.List<string>(totals.Keys);
        ordered.Sort((a, b) => totals[b].CompareTo(totals[a]));
        for (int i = 0; i < ordered.Count; i++)
            _cfgColors[ordered[i]] = CfgPalette[i % CfgPalette.Length];
    }

    private System.Windows.Media.Brush ColorFor(string cfg)
        => string.IsNullOrEmpty(cfg)
            ? CfgOther
            : (_cfgColors.TryGetValue(cfg, out var b) ? b : CfgOther);

    private void BuildChart(System.Collections.Generic.List<UsageStore.StackBar> bars)
    {
        ChartGrid.Children.Clear();
        ChartGrid.ColumnDefinitions.Clear();
        if (bars.Count == 0) return;
        long max = 1;
        foreach (var b in bars) if (b.Total > max) max = b.Total;
        for (int i = 0; i < bars.Count; i++)
        {
            ChartGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            var b = bars[i];
            if (b.Total <= 0) continue;
            double fullH = Math.Max(3.0, (double)b.Total / max * 150.0);

            var stack = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Margin = new System.Windows.Thickness(2, 0, 2, 0)
            };

            var active = b.Segments.Where(s => s.Total > 0).ToList();
            for (int j = 0; j < active.Count; j++)
            {
                var s = active[j];
                double segH = Math.Max(1.0, (double)s.Total / b.Total * fullH);
                stack.Children.Add(new System.Windows.Controls.Border
                {
                    Height = segH,
                    Background = ColorFor(s.Config),
                    CornerRadius = j == 0
                        ? new System.Windows.CornerRadius(3, 3, 0, 0)
                        : new System.Windows.CornerRadius(0)
                });
            }

            stack.ToolTip = new System.Windows.Controls.ToolTip
            {
                Style = (System.Windows.Style)Resources["ChartTip"],
                Content = BuildUsageTip(b)
            };
            System.Windows.Controls.ToolTipService.SetInitialShowDelay(stack, 120);
            stack.MouseEnter += (snd, _) => ((System.Windows.Controls.StackPanel)snd!).Opacity = 0.82;
            stack.MouseLeave += (snd, _) => ((System.Windows.Controls.StackPanel)snd!).Opacity = 1.0;

            System.Windows.Controls.Grid.SetColumn(stack, i);
            ChartGrid.Children.Add(stack);
        }
    }

    private System.Windows.FrameworkElement BuildUsageTip(UsageStore.StackBar b)
    {
        var root = new System.Windows.Controls.StackPanel { MinWidth = 188 };

        root.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = Strings.LocalizeDigits(b.Label, _lang),
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            FontWeight = System.Windows.FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            FlowDirection = System.Windows.FlowDirection.LeftToRight
        });
        root.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = Strings.LocalizeDigits($"{Strings.Get(_lang, "total")}: {FormatBytes(b.Total)}", _lang),
            Foreground = Muted,
            FontSize = 11.5,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new System.Windows.Thickness(0, 1, 0, 8),
            FlowDirection = System.Windows.FlowDirection.LeftToRight
        });

        foreach (var s in b.Segments)
        {
            if (s.Total <= 0) continue;
            var row = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new System.Windows.Thickness(0, 4, 0, 0),
                FlowDirection = System.Windows.FlowDirection.LeftToRight
            };
            row.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = ColorFor(s.Config),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 7, 0)
            });
            string name = string.IsNullOrEmpty(s.Config)
                ? Strings.Get(_lang, "usage_other")
                : Flags.DisplayName(s.Config);
            row.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = name,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0xE6, 0xF5)),
                FontSize = 12,
                FontWeight = System.Windows.FontWeights.SemiBold,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                MaxWidth = 150
            });
            root.Children.Add(row);
            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = Strings.LocalizeDigits($"\u2191 {FormatBytes(s.Up)}    \u2193 {FormatBytes(s.Down)}", _lang),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8F, 0xA3, 0xC8)),
                FontSize = 11,
                Margin = new System.Windows.Thickness(16, 1, 0, 0),
                FlowDirection = System.Windows.FlowDirection.LeftToRight
            });
        }
        return root;
    }

    private System.Collections.Generic.List<UsageStore.StackBar>? _labelBars;

    private void BuildLabels(System.Collections.Generic.List<UsageStore.StackBar> bars)
    {
        _labelBars = bars;
        RenderLabels();
    }

    private void LabelCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e) => RenderLabels();

    private void RenderLabels()
    {
        LabelCanvas.Children.Clear();
        if (_labelBars is null || _labelBars.Count == 0) return;
        double w = LabelCanvas.ActualWidth;
        if (w <= 1) return;
        int count = _labelBars.Count;
        int every = Math.Max(1, count / 6);
        for (int i = 0; i < count; i++)
        {
            if (i % every != 0) continue;
            var t = new System.Windows.Controls.TextBlock
            {
                Text = Strings.LocalizeDigits(_labelBars[i].Short, _lang),
                FontSize = 10,
                Foreground = Muted
            };
            t.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            double cx = (i + 0.5) / count * w;
            double left = cx - t.DesiredSize.Width / 2;
            if (left < 0) left = 0;
            if (left > w - t.DesiredSize.Width) left = w - t.DesiredSize.Width;
            System.Windows.Controls.Canvas.SetLeft(t, left);
            System.Windows.Controls.Canvas.SetTop(t, 2);
            LabelCanvas.Children.Add(t);
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_xray.IsRunning)
        {
            Disconnect();
            return;
        }

        var config = _store.Selected();
        if (config is null)
        {
            AppendLine("Select a server from the list first.");
            return;
        }
        UsageStore.CurrentConfigKey = string.IsNullOrWhiteSpace(config.Name) ? config.Address : Flags.DisplayName(config.Name);

        bool tun = ModeTun.IsChecked == true;
        if (tun)
        {
            var missing = TunController.MissingBinary();
            if (missing is not null)
            {
                AppendLine($"TUN needs this file (not found): {missing}");
                return;
            }
            if (!Elevation.IsAdministrator())
            {
                AppendLine("TUN mode needs administrator rights. Relaunching GRoute elevated...");
                if (Elevation.RelaunchAsAdmin()) System.Windows.Application.Current.Shutdown();
                else AppendLine("Elevation was cancelled.");
                return;
            }
        }

        try
        {
            var json = ConfigBuilder.Build(config, _store.Fragment, _store.SplitRouting, _store.SniffEnabled, _store.SniffProtocols, _store.SniffRouteOnly);
            await System.Threading.Tasks.Task.Run(() => _xray.Start(json));
            AppendLine($"Connecting to {config.Name} ({config.Address}:{config.Port})");
            StartStatsPolling();

            if (tun)
            {
                _tunActive = await _tun.Start(config);
                if (!_tunActive)
                {
                    AppendLine("TUN setup failed — stopping.");
                    StopStatsPolling();
                    _xray.Stop();
                }
            }
            else
            {
                _justProxyActive = _justProxy;
                if (_justProxy)
                {
                    AppendLine($"Local proxy running on 127.0.0.1:{ConfigBuilder.HttpPort} (system proxy untouched)");
                }
                else
                {
                    SystemProxy.Enable($"127.0.0.1:{ConfigBuilder.HttpPort}");
                    AppendLine($"System proxy enabled on 127.0.0.1:{ConfigBuilder.HttpPort}");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLine("Error: " + ex.Message);
        }
    }

    private void Disconnect()
    {
        StopStatsPolling();
        if (_tunActive)
        {
            _tun.Stop();
            _tunActive = false;
        }
        else
        {
            if (!_justProxyActive)
                try { SystemProxy.Disable(); } catch { }
        }
        _justProxyActive = false;
        _xray.Stop();
        UsageStore.Flush();
        UsageStore.CurrentConfigKey = null;
        AppendLine("Disconnected.");
    }

    private void StartStatsPolling()
    {
        StopStatsPolling();
        var cts = new CancellationTokenSource();
        _statsCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            long lastUp = 0;
            long lastDown = 0;
            bool first = true;
            while (!token.IsCancellationRequested)
            {
                var (up, down) = StatsQuery.Query(ConfigBuilder.ApiPort);
                long upSpeed = first ? 0 : Math.Max(up - lastUp, 0);
                long downSpeed = first ? 0 : Math.Max(down - lastDown, 0);
                lastUp = up;
                lastDown = down;
                first = false;

                UsageStore.Add(upSpeed, downSpeed);

                if (!token.IsCancellationRequested)
                    Dispatcher.Invoke(() => UpdateStats(up, down, upSpeed, downSpeed));

                try { await Task.Delay(1000, token); }
                catch { break; }
            }
        }, token);
    }

    private void StopStatsPolling()
    {
        _statsCts?.Cancel();
        _statsCts = null;
    }

    private void UpdateStats(long totalUp, long totalDown, long upSpeed, long downSpeed)
    {
        SpeedDownText.Text = Strings.LocalizeDigits($"{FormatBytes(downSpeed)}/s", _lang);
        SpeedUpText.Text = Strings.LocalizeDigits($"{FormatBytes(upSpeed)}/s", _lang);
        TotalText.Text = Strings.LocalizeDigits(
            $"{Strings.Get(_lang, "total")} \u2193\u2009{FormatBytes(totalDown)} \u2191\u2009{FormatBytes(totalUp)}", _lang);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.#} MB";
        return $"{mb / 1024.0:0.##} GB";
    }

    private static int PingRank(string pingText)
    {
        if (int.TryParse(pingText.Replace(" ms", ""), out var ms)) return ms;
        return pingText switch
        {
            "..." => 1_000_000,
            "fail" => 3_000_000,
            _ => 2_000_000
        };
    }

    private void RebuildGroups()
    {
        _groups.Clear();
        string q = _searchQuery.Trim();
        bool has = q.Length > 0;
        bool M(string? s) => (s ?? "").IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;
        foreach (var sub in _store.Subscriptions)
        {
            var all = _store.Configs.Where(c => c.SubId == sub.Id);
            var configs = (!has || M(sub.Name)) ? all.ToList() : all.Where(c => M(c.Name)).ToList();
            if (has && configs.Count == 0 && !M(sub.Name)) continue;
            var g = new SubGroupVm
            {
                Id = sub.Id,
                Name = sub.Name,
                IsManual = false,
                IconGlyph = "\uE753",
                IsExpanded = has || _expandedIds.Contains(sub.Id),
                UsageText = sub.UsageText,
                CountText = Strings.LocalizeDigits(configs.Count.ToString(), _lang)
            };
            if (sub.Total > 0)
            {
                double remaining = Math.Max(sub.Total - sub.Used, 0);
                double frac = Math.Clamp(remaining / (double)sub.Total, 0.0, 1.0);
                g.HasUsageBar = true;
                g.RemainingStar = new System.Windows.GridLength(frac, System.Windows.GridUnitType.Star);
                g.UsedStar = new System.Windows.GridLength(1 - frac, System.Windows.GridUnitType.Star);
            }
            AddConfigs(g, configs);
            _groups.Add(g);
        }

        var manualAll = _store.Configs.Where(c => string.IsNullOrEmpty(c.SubId));
        var manual = has ? manualAll.Where(c => M(c.Name)).ToList() : manualAll.ToList();
        if (manual.Count > 0)
        {
            var g = new SubGroupVm
            {
                Id = ManualId,
                Name = Strings.Get(_lang, "manual_configs"),
                IsManual = true,
                IconGlyph = "\uE8B7",
                IsExpanded = true,
                CountText = Strings.LocalizeDigits(manual.Count.ToString(), _lang)
            };
            AddConfigs(g, manual);
            _groups.Add(g);
        }
        AnimateWindowHeight();
    }

    private void AddConfigs(SubGroupVm g, System.Collections.Generic.List<ProxyConfig> configs)
    {
        var ordered = _sortMode switch
        {
            SortMode.Fastest => configs.OrderBy(c => PingRank(c.PingText)),
            SortMode.Alpha => configs.OrderBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase),
            _ => configs.AsEnumerable()
        };
        foreach (var c in ordered)
        {
            c.IsSelected = c.Id == _store.SelectedId;
            g.Configs.Add(c);
        }
    }

    private void AppendLine(string s)
    {
        LogBox.AppendText(s + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    public void ShutdownAndClose()
    {
        try { Disconnect(); } catch { }
        _reallyClose = true;
        Close();
    }

    private bool _cleaned;

    private void SafeCleanup()
    {
        if (_cleaned) return;
        _cleaned = true;
        if (!_justProxyActive)
            try { SystemProxy.Disable(); } catch { }
        try { _tun.Stop(); } catch { }
        try { _xray.Stop(); } catch { }
        try { if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        SafeCleanup();
        base.OnClosing(e);
    }
}
