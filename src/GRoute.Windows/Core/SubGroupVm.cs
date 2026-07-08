using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using GRoute.Windows.Core;

namespace GRoute.Windows;

public sealed class SubGroupVm : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsManual { get; set; }
    public string IconGlyph { get; set; } = "";
    public bool SubActionsVisible => !IsManual;
    public string UsageText { get; set; } = "";
    public bool HasUsageText => !string.IsNullOrEmpty(UsageText);
    public string CountText { get; set; } = "";
    public bool HasUsageBar { get; set; }
    public GridLength RemainingStar { get; set; } = new GridLength(1, GridUnitType.Star);
    public GridLength UsedStar { get; set; } = new GridLength(0, GridUnitType.Star);
    public ObservableCollection<ProxyConfig> Configs { get; } = new();

    private bool _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
