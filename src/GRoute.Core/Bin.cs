using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GRoute.Windows.Core;

public static class Bin
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static string Path(string name)
    {
        string file = IsWindows ? name + ".exe" : name;
        return System.IO.Path.Combine(AppContext.BaseDirectory, "libs", file);
    }
}
