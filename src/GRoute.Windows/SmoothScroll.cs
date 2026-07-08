using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GRoute.Windows;

public static class SmoothScroll
{
    private sealed class State
    {
        public double Target;
        public bool Running;
    }

    private static readonly Dictionary<ScrollViewer, State> Map = new();
    private static readonly List<ScrollViewer> Active = new();
    private static bool _hooked;

    public static void Attach(ScrollViewer sv)
    {
        sv.PreviewMouseWheel -= OnWheel;
        sv.PreviewMouseWheel += OnWheel;
    }

    public static void Wheel(ScrollViewer sv, int delta)
    {
        if (sv.ScrollableHeight <= 0) return;
        if (!Map.TryGetValue(sv, out var st))
        {
            st = new State { Target = sv.VerticalOffset };
            Map[sv] = st;
        }
        if (!st.Running) st.Target = sv.VerticalOffset;
        int lines = SystemParameters.WheelScrollLines;
        double step = lines < 0 ? sv.ViewportHeight : lines * 20.0;
        double t = st.Target - step * (delta / 120.0);
        if (t < 0) t = 0;
        if (t > sv.ScrollableHeight) t = sv.ScrollableHeight;
        st.Target = t;
        if (!st.Running)
        {
            st.Running = true;
            Active.Add(sv);
            if (!_hooked)
            {
                CompositionTarget.Rendering += OnRender;
                _hooked = true;
            }
        }
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        if (sv.ScrollableHeight <= 0) return;
        if (InsidePopup(e.OriginalSource as DependencyObject, sv)) return;
        e.Handled = true;

        if (!Map.TryGetValue(sv, out var st))
        {
            st = new State { Target = sv.VerticalOffset };
            Map[sv] = st;
        }
        if (!st.Running) st.Target = sv.VerticalOffset;

        int lines = SystemParameters.WheelScrollLines;
        double step = lines < 0 ? sv.ViewportHeight : lines * 20.0;
        double t = st.Target - step * (e.Delta / 120.0);
        if (t < 0) t = 0;
        if (t > sv.ScrollableHeight) t = sv.ScrollableHeight;
        st.Target = t;

        if (!st.Running)
        {
            st.Running = true;
            Active.Add(sv);
            if (!_hooked)
            {
                CompositionTarget.Rendering += OnRender;
                _hooked = true;
            }
        }
    }

    private static bool InsidePopup(DependencyObject? d, ScrollViewer sv)
    {
        while (d != null && !ReferenceEquals(d, sv))
        {
            if (d is System.Windows.Controls.Primitives.Popup) return true;
            if (d.GetType().Name == "PopupRoot") return true;
            d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
                ? VisualTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    private static void OnRender(object? sender, EventArgs e)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var sv = Active[i];
            var st = Map[sv];
            double cur = sv.VerticalOffset;
            double diff = st.Target - cur;
            if (Math.Abs(diff) < 0.5)
            {
                sv.ScrollToVerticalOffset(st.Target);
                st.Running = false;
                Active.RemoveAt(i);
            }
            else
            {
                sv.ScrollToVerticalOffset(cur + diff * 0.28);
            }
        }
        if (Active.Count == 0)
        {
            CompositionTarget.Rendering -= OnRender;
            _hooked = false;
        }
    }
}
