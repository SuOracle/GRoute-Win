using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace GRoute.Windows.Core;

public enum ConfigSource
{
    Personal,
    Community,
    Premium
}

public sealed class ProxyConfig : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string Address { get; set; } = "";
    public int Port { get; set; }
    public string Uuid { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Method { get; set; } = "";
    public int AlterId { get; set; }
    public string Encryption { get; set; } = "none";
    public string Flow { get; set; } = "";
    public string Network { get; set; } = "tcp";
    public string Security { get; set; } = "none";
    public string Sni { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string ShortId { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string PresharedKey { get; set; } = "";
    public string WgAddress { get; set; } = "";
    public int Mtu { get; set; } = 1420;
    public string Reserved { get; set; } = "";
    public string Fingerprint { get; set; } = "chrome";
    public string Path { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string Mode { get; set; } = "";
    public string HeaderType { get; set; } = "none";
    public string Alpn { get; set; } = "";
    public System.TimeSpan StaggerBegin { get; set; } = System.TimeSpan.Zero;
    public string Host { get; set; } = "";
    public string SubId { get; set; } = "";
    public string Raw { get; set; } = "";
    public ConfigSource Source { get; set; } = ConfigSource.Personal;
    public string Id { get; set; } = Guid.NewGuid().ToString();

    private string _pingText = "";

    [JsonIgnore]
    public string PingText
    {
        get => _pingText;
        set
        {
            if (_pingText != value)
            {
                _pingText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PingText)));
            }
        }
    }

    private int _ping = int.MinValue;

    [JsonIgnore]
    public int Ping
    {
        get => _ping;
        set
        {
            if (_ping != value)
            {
                _ping = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ping)));
            }
        }
    }

    private bool _isSelected;

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
