using System.IO;

namespace GRoute.Windows.Core;

public static class Assets
{
    public static string AssetDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute", "assets");

    private static string LibsDir => Path.Combine(AppContext.BaseDirectory, "libs");

    public static bool GeoipPresent => File.Exists(Path.Combine(AssetDir, "geoip.dat"));
    public static bool GeositePresent => File.Exists(Path.Combine(AssetDir, "geosite.dat"));

    public static void EnsureSeeded()
    {
        try
        {
            Directory.CreateDirectory(AssetDir);
            SeedOne("geoip.dat");
            SeedOne("geosite.dat");
        }
        catch
        {
        }
    }

    public static void Import(string sourcePath, string targetName)
    {
        Directory.CreateDirectory(AssetDir);
        File.Copy(sourcePath, Path.Combine(AssetDir, targetName), true);
    }

    public static bool ResetToBundled(string name)
    {
        var src = Path.Combine(LibsDir, name);
        if (!File.Exists(src)) return false;
        Directory.CreateDirectory(AssetDir);
        File.Copy(src, Path.Combine(AssetDir, name), true);
        return true;
    }

    private static void SeedOne(string name)
    {
        var dest = Path.Combine(AssetDir, name);
        if (File.Exists(dest)) return;
        var src = Path.Combine(LibsDir, name);
        if (File.Exists(src)) File.Copy(src, dest, false);
    }
}
