using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GRoute.Desktop.Controls;

public sealed class VText : TextBlock
{
    private double _shift;
    private double _shiftKey = double.NaN;

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        UpdateShift(size.Height);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        ApplyShift();
        return size;
    }

    private void ApplyShift()
    {
        if (_shift == 0)
        {
            if (RenderTransform is TranslateTransform) RenderTransform = null;
            return;
        }
        if (RenderTransform is TranslateTransform current && current.Y.Equals(_shift)) return;
        RenderTransform = new TranslateTransform(0, _shift);
    }

    private void UpdateShift(double boxHeight)
    {
        double key = boxHeight * 31.0 + FontSize + (Text?.Length ?? 0) * 0.017;
        if (key.Equals(_shiftKey)) return;
        _shiftKey = key;
        _shift = 0;

        if (TextWrapping != TextWrapping.NoWrap || double.IsNaN(boxHeight) || boxHeight <= 0) return;

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphs)) return;

        var metrics = glyphs.Metrics;
        if (metrics.DesignEmHeight == 0) return;

        double scale = FontSize / metrics.DesignEmHeight;
        double descent = metrics.Descent * scale;
        if (descent < 0) return;

        double baseline = -metrics.Ascent * scale;
        var lines = TextLayout.TextLines;
        if (lines.Count > 0 && lines[0].Baseline > 0) baseline = lines[0].Baseline;
        if (baseline <= 0) return;

        double cap = InkHeight(glyphs, scale);
        double inkCentre = baseline - cap * 0.5;
        double shift = boxHeight * 0.5 - inkCentre;

        double limit = descent * 0.75;
        if (shift > limit) shift = limit;
        if (shift < -limit) shift = -limit;
        _shift = shift;
    }

    private double InkHeight(IGlyphTypeface glyphs, double scale)
    {
        double tallest = 0;
        string text = Text ?? string.Empty;
        int limit = Math.Min(text.Length, 48);
        for (int i = 0; i < limit; i++)
        {
            char ch = text[i];
            if (char.IsWhiteSpace(ch)) continue;
            ushort glyph = glyphs.GetGlyph((uint)ch);
            if (glyph == 0) continue;
            if (!glyphs.TryGetGlyphMetrics(glyph, out var gm)) continue;
            if (gm.Height > tallest) tallest = gm.Height;
        }
        if (tallest > 0) return tallest * scale;

        foreach (char probe in new[] { 'H', 'E', 'X', '0' })
        {
            ushort glyph = glyphs.GetGlyph((uint)probe);
            if (glyph == 0) continue;
            if (!glyphs.TryGetGlyphMetrics(glyph, out var gm)) continue;
            if (gm.Height > 0) return gm.Height * scale;
        }
        return glyphs.Metrics.DesignEmHeight * 0.72 * scale;
    }
}
