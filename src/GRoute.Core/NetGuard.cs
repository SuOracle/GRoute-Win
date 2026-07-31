using System;
using System.IO;

namespace GRoute.Windows.Core;

public static class NetGuard
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute");

    private static string Marker(string what) => Path.Combine(Dir, $"dirty-{what}");

    public static void MarkDirty(string what)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Marker(what), DateTime.UtcNow.ToString("O"));
        }
        catch
        {
        }
    }

    public static void MarkClean(string what)
    {
        try
        {
            string p = Marker(what);
            if (File.Exists(p)) File.Delete(p);
        }
        catch
        {
        }
    }

    public static bool IsDirty(string what)
    {
        try { return File.Exists(Marker(what)); }
        catch { return false; }
    }

    public static void ClearAll()
    {
        foreach (var what in new[] { "tun", "killswitch", "proxy" })
            MarkClean(what);
    }
}
