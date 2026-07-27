using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace GRoute.Desktop.Controls;

public static class SmoothScroll
{
    private sealed class State
    {
        public double Target;
        public bool Running;
    }

    private static readonly Dictionary<ScrollViewer, State> Map = new();
    private static readonly List<ScrollViewer> Active = new();
    private static DispatcherTimer? _timer;

    public static void Hook(InputElement root)
    {
        root.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
        root.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
    }

    public static void Attach(ScrollViewer sv)
    {
        sv.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
        sv.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
    }

    private static void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (Math.Abs(e.Delta.Y) < 0.01) return;
        if (e.Source is not Visual src) return;
        var tb = src.FindAncestorOfType<TextBox>(includeSelf: true);
        if (tb is not null && tb.AcceptsReturn) return;

        ScrollViewer? sv = src.FindAncestorOfType<ScrollViewer>(includeSelf: true);
        while (sv is not null && sv.Extent.Height - sv.Viewport.Height <= 1)
            sv = sv.FindAncestorOfType<ScrollViewer>();
        if (sv is null) return;

        double scrollable = sv.Extent.Height - sv.Viewport.Height;
        if (scrollable <= 0) return;
        e.Handled = true;

        if (!Map.TryGetValue(sv, out var st))
        {
            st = new State { Target = sv.Offset.Y };
            Map[sv] = st;
        }
        if (!st.Running) st.Target = sv.Offset.Y;

        double t = st.Target - e.Delta.Y * 60.0;
        if (t < 0) t = 0;
        if (t > scrollable) t = scrollable;
        st.Target = t;

        if (!st.Running)
        {
            st.Running = true;
            Active.Add(sv);
            _timer ??= CreateTimer();
            if (!_timer.IsEnabled) _timer.Start();
        }
    }

    private static DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var sv = Active[i];
                var st = Map[sv];
                double scrollable = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
                if (st.Target > scrollable) st.Target = scrollable;
                double cur = sv.Offset.Y;
                double diff = st.Target - cur;
                if (Math.Abs(diff) < 0.5)
                {
                    sv.Offset = new Vector(sv.Offset.X, st.Target);
                    st.Running = false;
                    Active.RemoveAt(i);
                }
                else
                {
                    sv.Offset = new Vector(sv.Offset.X, cur + diff * 0.24);
                }
            }
            if (Active.Count == 0) timer.Stop();
        };
        return timer;
    }
}
