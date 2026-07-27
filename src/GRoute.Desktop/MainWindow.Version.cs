using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace GRoute.Desktop;

public partial class MainWindow
{
    private bool _verProbed;
    private DispatcherTimer? _verPulse;
    private DateTime _verPulseStart;

    private void VersionDotsLoaded(object? sender, RoutedEventArgs e)
    {
        if (_verProbed) return;
        _verProbed = true;
        StartVersionPulse();
        _ = ProbeVersionState();
    }

    private void StartVersionPulse()
    {
        if (_verPulse is not null) return;
        _verPulseStart = DateTime.UtcNow;
        _verPulse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _verPulse.Tick += (_, _) => TickVersionPulse();
        _verPulse.Start();
    }

    private void TickVersionPulse()
    {
        double t = (DateTime.UtcNow - _verPulseStart).TotalMilliseconds % 2600.0 / 2600.0;
        double wave = 0.5 + 0.5 * Math.Cos(t * Math.PI * 2.0);

        double core = 0.88 + 0.12 * wave;
        double bloom = 0.34 + 0.16 * wave;
        double halo = 0.11 + 0.13 * wave;

        ApplyPulse(VerDotL, VerGlowL, VerHaloL, core, bloom, halo);
        ApplyPulse(VerDotR, VerGlowR, VerHaloR, core, bloom, halo);
    }

    private static void ApplyPulse(Ellipse dot, Ellipse glow, Ellipse halo,
        double core, double bloom, double haloOpacity)
    {
        if (dot is not null) dot.Opacity = core;
        if (glow is not null) glow.Opacity = bloom;
        if (halo is not null) halo.Opacity = haloOpacity;
    }

    private async Task ProbeVersionState()
    {
        try
        {
            string current = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "GRoute");
            string json = await client.GetStringAsync(UpdateUrl);
            using var doc = JsonDocument.Parse(json);
            string latest = doc.RootElement.GetProperty("version").GetString() ?? current;
            bool outdated = IsNewer(latest, current);
            await Dispatcher.UIThread.InvokeAsync(() => SetVersionDots(outdated));
        }
        catch
        {
        }
    }

    private void SetVersionDots(bool outdated)
    {
        var coreBrush = new SolidColorBrush(Color.Parse(outdated ? "#FFDCA6" : "#B4FFDD"));
        var neonBrush = new SolidColorBrush(Color.Parse(outdated ? "#FF9F2E" : "#2FDF8F"));

        if (VerDotL is not null) VerDotL.Fill = coreBrush;
        if (VerDotR is not null) VerDotR.Fill = coreBrush;
        if (VerGlowL is not null) VerGlowL.Fill = neonBrush;
        if (VerGlowR is not null) VerGlowR.Fill = neonBrush;
        if (VerHaloL is not null) VerHaloL.Fill = neonBrush;
        if (VerHaloR is not null) VerHaloR.Fill = neonBrush;
    }
}
