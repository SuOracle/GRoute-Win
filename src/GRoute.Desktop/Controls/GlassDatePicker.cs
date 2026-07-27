using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace GRoute.Desktop.Controls;

public sealed class GlassDatePicker : Border
{
    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<GlassDatePicker, DateTime?>(nameof(SelectedDate));

    public static readonly RoutedEvent<RoutedEventArgs> SelectedDateChangedEvent =
        RoutedEvent.Register<GlassDatePicker, RoutedEventArgs>(nameof(SelectedDateChanged), RoutingStrategies.Bubble);

    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public event EventHandler<RoutedEventArgs> SelectedDateChanged
    {
        add => AddHandler(SelectedDateChangedEvent, value);
        remove => RemoveHandler(SelectedDateChangedEvent, value);
    }

    private static readonly ImmutableSolidColorBrush FieldBg = new(Color.FromRgb(0x10, 0x1E, 0x3E));
    private static readonly ImmutableSolidColorBrush FieldEdge = new(Color.FromRgb(0x35, 0x54, 0x8C));
    private static readonly ImmutableSolidColorBrush FieldEdgeHot = new(Color.FromRgb(0x44, 0x68, 0xB0));
    private static readonly ImmutableSolidColorBrush PanelBg = new(Color.FromRgb(0x0C, 0x18, 0x30));
    private static readonly ImmutableSolidColorBrush PanelEdge = new(Color.FromRgb(0x2A, 0x3C, 0x64));
    private static readonly ImmutableSolidColorBrush Accent = new(Color.FromRgb(0x3D, 0x6A, 0xD6));
    private static readonly ImmutableSolidColorBrush Hover = new(Color.FromRgb(0x1A, 0x27, 0x43));
    private static readonly ImmutableSolidColorBrush TextMain = new(Color.FromRgb(0xDC, 0xE6, 0xF5));
    private static readonly ImmutableSolidColorBrush TextField = new(Color.FromRgb(0xE3, 0xEB, 0xFA));
    private static readonly ImmutableSolidColorBrush TextDim = new(Color.FromRgb(0x4E, 0x5E, 0x82));
    private static readonly ImmutableSolidColorBrush TextHead = new(Color.FromRgb(0x8F, 0xA3, 0xC8));
    private static readonly ImmutableSolidColorBrush TextIcon = new(Color.FromRgb(0xC9, 0xD6, 0xEE));
    private static readonly string[] DayHeads = { "S", "M", "T", "W", "T", "F", "S" };

    private readonly TextBlock _text;
    private Flyout? _flyout;
    private TextBlock? _header;
    private readonly Border[] _cells = new Border[42];
    private readonly TextBlock[] _cellText = new TextBlock[42];
    private readonly DateTime[] _cellDate = new DateTime[42];
    private DateTime _month;

    static GlassDatePicker()
    {
        SelectedDateProperty.Changed.AddClassHandler<GlassDatePicker>((p, _) => p.UpdateFieldText());
    }

    public GlassDatePicker()
    {
        Background = FieldBg;
        BorderBrush = FieldEdge;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(12, 0);
        MinHeight = 34;
        Cursor = new Cursor(StandardCursorType.Hand);
        Transitions = new Transitions
        {
            new BrushTransition { Property = BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(140) }
        };

        _text = new TextBlock
        {
            Foreground = TextField,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center
        };

        var icon = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M4.5 3.5 H15.5 C16.6 3.5 17.5 4.4 17.5 5.5 V15.5 C17.5 16.6 16.6 17.5 15.5 17.5 H4.5 C3.4 17.5 2.5 16.6 2.5 15.5 V5.5 C2.5 4.4 3.4 3.5 4.5 3.5 Z M2.5 8 H17.5 M6.5 1.5 V5 M13.5 1.5 V5"),
            Stroke = TextIcon,
            StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,Auto") };
        grid.Children.Add(_text);
        Grid.SetColumn(icon, 2);
        grid.Children.Add(icon);
        Child = grid;

        PointerEntered += (_, _) => BorderBrush = FieldEdgeHot;
        PointerExited += (_, _) => BorderBrush = FieldEdge;
        PointerPressed += (_, e) =>
        {
            e.Handled = true;
            OpenFlyout();
        };

        UpdateFieldText();
    }

    private void UpdateFieldText() =>
        _text.Text = SelectedDate?.ToString("M/d/yyyy") ?? "Select date";

    private void OpenFlyout()
    {
        _flyout ??= BuildFlyout();
        var seed = SelectedDate ?? DateTime.Today;
        _month = new DateTime(seed.Year, seed.Month, 1);
        RefreshMonth();
        _flyout.ShowAt(this);
    }

    private Flyout BuildFlyout()
    {
        var header = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _header = header;

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(2, 0, 2, 10) };
        var prev = NavButton("M9 2 L4 7.5 L9 13", -1);
        var next = NavButton("M5 2 L10 7.5 L5 13", 1);
        Grid.SetColumn(header, 1);
        Grid.SetColumn(next, 2);
        head.Children.Add(prev);
        head.Children.Add(header);
        head.Children.Add(next);

        var week = new UniformGrid { Columns = 7, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var d in DayHeads)
        {
            week.Children.Add(new TextBlock
            {
                Text = d,
                Foreground = TextHead,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Height = 20
            });
        }

        var days = new UniformGrid { Columns = 7 };
        for (int i = 0; i < 42; i++)
        {
            var label = new TextBlock
            {
                FontSize = 12.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var cell = new Border
            {
                Height = 32,
                Margin = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = label,
                Transitions = new Transitions
                {
                    new BrushTransition
                    {
                        Property = BackgroundProperty,
                        Duration = TimeSpan.FromMilliseconds(120)
                    }
                }
            };
            int idx = i;
            cell.PointerEntered += (_, _) =>
            {
                if (!IsSelectedCell(idx)) cell.Background = Hover;
            };
            cell.PointerExited += (_, _) =>
            {
                if (!IsSelectedCell(idx)) cell.Background = Brushes.Transparent;
            };
            cell.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                SelectedDate = _cellDate[idx];
                RefreshMonth();
                RaiseEvent(new RoutedEventArgs(SelectedDateChangedEvent));
                _flyout?.Hide();
            };
            _cells[i] = cell;
            _cellText[i] = label;
            days.Children.Add(cell);
        }

        var root = new Border
        {
            Background = PanelBg,
            BorderBrush = PanelEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Width = 296,
            BoxShadow = BoxShadows.Parse("0 8 28 -6 #A0000A18"),
            Child = new StackPanel { Children = { head, week, days } }
        };

        root.AttachedToVisualTree += (_, _) =>
        {
            root.Transitions = null;
            root.Opacity = 0;
            root.RenderTransform = TransformOperations.Parse("translateY(-6px)");
            Dispatcher.UIThread.Post(() =>
            {
                root.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(160),
                        Easing = new CubicEaseOut()
                    },
                    new TransformOperationsTransition
                    {
                        Property = RenderTransformProperty,
                        Duration = TimeSpan.FromMilliseconds(190),
                        Easing = new CubicEaseOut()
                    }
                };
                root.Opacity = 1;
                root.RenderTransform = TransformOperations.Parse("translateY(0px)");
            }, DispatcherPriority.Render);
        };

        var flyout = new Flyout
        {
            Content = root,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            VerticalOffset = 8
        };
        flyout.FlyoutPresenterClasses.Add("gcal");
        return flyout;
    }

    private Control NavButton(string data, int delta)
    {
        var chevron = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stroke = TextIcon,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Width = 9,
            Height = 12,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var button = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = chevron,
            Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(120)
                }
            }
        };
        button.PointerEntered += (_, _) => button.Background = Hover;
        button.PointerExited += (_, _) => button.Background = Brushes.Transparent;
        button.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _month = _month.AddMonths(delta);
            RefreshMonth();
        };
        return button;
    }

    private bool IsSelectedCell(int idx) =>
        SelectedDate is DateTime sel && _cellDate[idx].Date == sel.Date;

    private void RefreshMonth()
    {
        if (_header is null) return;
        _header.Text = _month.ToString("MMMM yyyy");

        int lead = (int)_month.DayOfWeek;
        var start = _month.AddDays(-lead);
        var today = DateTime.Today;

        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            _cellDate[i] = date;
            _cellText[i].Text = date.Day.ToString();

            bool inMonth = date.Month == _month.Month;
            bool selected = SelectedDate is DateTime sel && date.Date == sel.Date;
            bool isToday = date.Date == today;

            _cells[i].Background = selected ? Accent : Brushes.Transparent;
            _cells[i].BorderBrush = !selected && isToday ? Accent : Brushes.Transparent;
            _cellText[i].Foreground = selected ? Brushes.White : inMonth ? TextMain : TextDim;
            _cellText[i].FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }
}
