using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using GRoute.Windows.Core;

namespace GRoute.Desktop;

public sealed class SubGroupVm : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    private string _name = "";

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            Raise(nameof(Name));
        }
    }
    public bool IsManual { get; set; }
    public string IconGlyph { get; set; } = "";
    public bool SubActionsVisible => !IsManual;
    private string _usageText = "";
    private string _countText = "";
    private bool _hasUsageBar;
    private GridLength _remainingStar = new(1, GridUnitType.Star);
    private GridLength _usedStar = new(0, GridUnitType.Star);

    public string UsageText
    {
        get => _usageText;
        set
        {
            if (_usageText == value) return;
            _usageText = value;
            Raise(nameof(UsageText));
            Raise(nameof(HasUsageText));
        }
    }

    public bool HasUsageText => !string.IsNullOrEmpty(UsageText);

    public string CountText
    {
        get => _countText;
        set
        {
            if (_countText == value) return;
            _countText = value;
            Raise(nameof(CountText));
        }
    }

    public bool HasUsageBar
    {
        get => _hasUsageBar;
        set
        {
            if (_hasUsageBar == value) return;
            _hasUsageBar = value;
            Raise(nameof(HasUsageBar));
        }
    }

    public GridLength RemainingStar
    {
        get => _remainingStar;
        set
        {
            if (_remainingStar.Equals(value)) return;
            _remainingStar = value;
            Raise(nameof(RemainingStar));
        }
    }

    public GridLength UsedStar
    {
        get => _usedStar;
        set
        {
            if (_usedStar.Equals(value)) return;
            _usedStar = value;
            Raise(nameof(UsedStar));
        }
    }
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
                Raise(nameof(IsExpanded));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
