using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace GRoute.Windows;

public sealed class EarthWindow : System.Windows.Window
{
    public EarthWindow(EarthGlobe globe)
    {
        Width = 340;
        Height = 340;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;

        var shadow = new Ellipse
        {
            Width = 214,
            Height = 214,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            RenderTransform = new TranslateTransform(0, 5),
            Fill = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 0.62),
                    new GradientStop(Color.FromArgb(0x14, 0x00, 0x00, 0x00), 0.80),
                    new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0)
                })
        };

        var root = new Grid();
        root.Children.Add(shadow);
        root.Children.Add(globe);
        Content = root;
    }
}
