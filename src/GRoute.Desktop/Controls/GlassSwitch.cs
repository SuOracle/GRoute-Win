using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace GRoute.Desktop.Controls;

public sealed class GlassSwitch : ToggleButton
{
    private const double TrackW = 40.0;
    private const double TrackH = 24.0;
    private const double KnobD = 18.0;
    private const double Inset = 3.0;
    private const double WideW = 34.0;
    private const double OnX = TrackW - KnobD - Inset;
    private const double DragSlop = 3.0;

    private static readonly ImmutableSolidColorBrush TrackOff = new(Color.FromRgb(0x22, 0x34, 0x5B));
    private static readonly ImmutableSolidColorBrush TrackOffHot = new(Color.FromRgb(0x2A, 0x42, 0x70));
    private static readonly ImmutableSolidColorBrush TrackOn = new(Color.FromRgb(0x3D, 0x6A, 0xD6));
    private static readonly ImmutableSolidColorBrush TrackOnHot = new(Color.FromRgb(0x4A, 0x78, 0xE0));
    private static readonly ImmutableSolidColorBrush KnobOff = new(Color.FromRgb(0xED, 0xF3, 0xFF));
    private static readonly ImmutableSolidColorBrush KnobOn = new(Color.FromRgb(0xFF, 0xFF, 0xFF));

    private Border? _track;
    private Border? _knob;
    private bool _dragging;
    private bool _moved;
    private bool _hot;
    private double _startX;
    private double _startKnobX;
    private double _knobX = Inset;
    private int _dragGen;

    public GlassSwitch()
    {
        Width = TrackW;
        Height = TrackH;
        MinWidth = 0;
        MinHeight = 0;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);
        Template = new FuncControlTemplate<GlassSwitch>((_, ns) =>
        {
            var knob = new Border
            {
                Name = "Knob",
                Width = KnobD,
                Height = KnobD,
                CornerRadius = new CornerRadius(KnobD / 2.0),
                Background = KnobOff,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = TransformOperations.Parse($"translateX({Inset}px)")
            };
            var track = new Border
            {
                Name = "Track",
                Width = TrackW,
                Height = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2.0),
                Background = TrackOff,
                Child = knob
            };
            ns.Register("Knob", knob);
            ns.Register("Track", track);
            return track;
        });
    }

    private static Transitions KnobTransitions() => new()
    {
        new DoubleTransition
        {
            Property = WidthProperty,
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new CubicEaseInOut()
        },
        new TransformOperationsTransition
        {
            Property = RenderTransformProperty,
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new CubicEaseInOut()
        },
        new BrushTransition
        {
            Property = BackgroundProperty,
            Duration = TimeSpan.FromMilliseconds(300)
        }
    };

    private static Transitions DragTransitions() => new()
    {
        new DoubleTransition
        {
            Property = WidthProperty,
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

    private static Transitions TrackTransitions() => new()
    {
        new BrushTransition
        {
            Property = BackgroundProperty,
            Duration = TimeSpan.FromMilliseconds(300)
        }
    };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _track = e.NameScope.Find<Border>("Track");
        _knob = e.NameScope.Find<Border>("Knob");
        if (_track is not null) _track.Transitions = TrackTransitions();
        if (_knob is not null) _knob.Transitions = KnobTransitions();
        Settle();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty && !_dragging) Settle();
    }

    private void PaintTrack()
    {
        if (_track is null) return;
        bool on = IsChecked == true;
        _track.Background = on
            ? (_hot ? TrackOnHot : TrackOn)
            : (_hot ? TrackOffHot : TrackOff);
    }

    private void PlaceKnob(double width, double x)
    {
        if (_knob is null) return;
        _knob.Width = width;
        _knob.RenderTransform = TransformOperations.Parse($"translateX({x.ToString(System.Globalization.CultureInfo.InvariantCulture)}px)");
    }

    private void Settle()
    {
        bool on = IsChecked == true;
        _knobX = on ? OnX : Inset;
        if (_knob is not null)
        {
            _knob.Transitions ??= KnobTransitions();
            _knob.Background = on ? KnobOn : KnobOff;
        }
        PlaceKnob(KnobD, _knobX);
        PaintTrack();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hot = true;
        PaintTrack();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hot = false;
        PaintTrack();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        _dragging = true;
        _moved = false;
        _startX = e.GetPosition(this).X;
        _startKnobX = IsChecked == true ? OnX : Inset;
        _knobX = _startKnobX;
        PlaceKnob(WideW, Inset);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        double dx = e.GetPosition(this).X - _startX;
        if (!_moved)
        {
            if (Math.Abs(dx) < DragSlop) return;
            _moved = true;
            int gen = ++_dragGen;
            if (_knob is not null) _knob.Transitions = DragTransitions();
            DispatcherTimer.RunOnce(() =>
            {
                if (_dragging && gen == _dragGen && _knob is not null) _knob.Transitions = null;
            }, TimeSpan.FromMilliseconds(165));
        }
        _knobX = Math.Clamp(_startKnobX + dx, Inset, OnX);
        PlaceKnob(KnobD, _knobX);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            base.OnPointerReleased(e);
            return;
        }
        e.Handled = true;
        _dragging = false;
        e.Pointer.Capture(null);
        if (_knob is not null) _knob.Transitions = KnobTransitions();

        bool next = _moved ? _knobX > (Inset + OnX) / 2.0 : IsChecked != true;
        _dragging = false;
        if (IsChecked != next) IsChecked = next;
        Settle();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_dragging) return;
        _dragging = false;
        if (_knob is not null) _knob.Transitions = KnobTransitions();
        Settle();
    }
}
