using System.Net.NetworkInformation;

namespace GRoute.Windows.Core;

public static class SystemTraffic
{
    public static (long Up, long Down) Read()
    {
        long up = 0, down = 0;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var type = ni.NetworkInterfaceType;
                if (type == NetworkInterfaceType.Loopback) continue;
                if (type == NetworkInterfaceType.Tunnel) continue;
                if (ni.Description.Contains("wintun", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (ni.Name.Contains("GRoute", System.StringComparison.OrdinalIgnoreCase)) continue;
                var st = ni.GetIPStatistics();
                up += st.BytesSent;
                down += st.BytesReceived;
            }
        }
        catch
        {
        }
        return (up, down);
    }
}
