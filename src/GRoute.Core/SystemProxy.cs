using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GRoute.Windows.Core;

public static class SystemProxy
{
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    public static void Enable(string proxyAddress)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
        if (key is null) return;
        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", proxyAddress, RegistryValueKind.String);
        key.SetValue("ProxyOverride",
            "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*;<local>",
            RegistryValueKind.String);
        Refresh();
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
        if (key is null) return;
        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        Refresh();
    }

    private static void Refresh()
    {
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }
}
