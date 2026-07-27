using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using GRoute.Windows.Core;

namespace GRoute.Desktop.Controls;

public sealed class UsageChart : Control
{
    private static readonly ImmutableSolidColorBrush DownBrush = new(Color.FromRgb(0x5B, 0x8A, 0xD6));
    private static readonly ImmutableSolidColorBrush UpBrush = new(Color.FromRgb(0x9B, 0x7B, 0xE8));
    private static readonly ImmutableSolidColorBrush GridBrush = new(Color.FromArgb(0x30, 0x9F, 0xB3, 0xD6));
    private static readonly ImmutableSolidColorBrush LabelBrush = new(Color.FromRgb(0x8A, 0x97, 0xAD));
    private static readonly Typeface Face = new(new FontFamily("Consolas, monospace"));

    private List<UsageStore.Bar> _bars = new();

    public void SetBars(List<UsageStore.Bar> bars)
    {
        _bars = bars ?? new List<UsageStore.Bar>();
        InvalidateVisual();
    }

    public override void Render(DrawingContext dc)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 20 || h < 40) return;

        double axis = 18.0;
        double plot = h - axis;
        dc.DrawLine(new ImmutablePen(GridBrush, 1), new Point(0, plot), new Point(w, plot));

        if (_bars.Count == 0)
        {
            var empty = new FormattedText("No data yet", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Face, 12, LabelBrush);
            dc.DrawText(empty, new Point((w - empty.Width) / 2, plot / 2 - empty.Height / 2));
            return;
        }

        long max = 1;
        foreach (var b in _bars)
            if (b.Total > max) max = b.Total;

        double slot = w / _bars.Count;
        double barW = Math.Max(3.0, Math.Min(18.0, slot * 0.55));

        for (int i = 0; i < _bars.Count; i++)
        {
            var b = _bars[i];
            double cx = slot * i + slot / 2.0;
            double totalH = plot * 0.92 * (b.Total / (double)max);
            if (totalH < 0.5 && b.Total > 0) totalH = 1.5;
            double downH = b.Total == 0 ? 0 : totalH * (b.Down / (double)b.Total);
            double upH = totalH - downH;

            if (downH > 0)
                dc.DrawRectangle(DownBrush, null,
                    new RoundedRect(new Rect(cx - barW / 2, plot - downH, barW, downH), 2));
            if (upH > 0)
                dc.DrawRectangle(UpBrush, null,
                    new RoundedRect(new Rect(cx - barW / 2, plot - totalH, barW, upH), 2));

            if (_bars.Count <= 12 || i % Math.Max(1, _bars.Count / 8) == 0)
            {
                var ft = new FormattedText(b.Short, System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Face, 10, LabelBrush);
                dc.DrawText(ft, new Point(cx - ft.Width / 2, plot + 4));
            }
        }
    }
}
