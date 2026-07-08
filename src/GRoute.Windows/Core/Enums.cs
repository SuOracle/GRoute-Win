namespace GRoute.Windows.Core;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public enum ProxyMode
{
    SystemProxy,
    Tun
}
