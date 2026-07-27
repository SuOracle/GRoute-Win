using System;
using System.Diagnostics;
using System.Security.Principal;

namespace GRoute.Windows.Core;

public static class Elevation
{
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool RelaunchAsAdmin(string? args = null)
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return false;
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas"
            };
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
