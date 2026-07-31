using System.Collections.Generic;
using System.IO;
using Avalonia;

namespace GRoute.Desktop;

internal static class Program
{
    private static void RestoreNetwork()
    {
        try
        {
            if (OperatingSystem.IsWindows()) DesktopPlatform.CleanupStaleState();
            else if (OperatingSystem.IsLinux()) LinuxPlatform.CleanupStaleState();
        }
        catch
        {
        }
    }

    [STAThread]
    public static void Main(string[] args)
    {

        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreNetwork();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WriteCrash(e.ExceptionObject as Exception);
            RestoreNetwork();
        };
        Console.CancelKeyPress += (_, _) => RestoreNetwork();

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            WriteCrash(e.Exception);
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            throw;
        }
    }

    private static void WriteCrash(Exception? ex)
    {
        if (ex is null) return;
        string text = DateTime.Now + Environment.NewLine + ex;

        foreach (var dir in CrashDirs())
        {
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "crash.txt"), text);
                return;
            }
            catch
            {
            }
        }
    }

    private static IEnumerable<string> CrashDirs()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GRoute");
        yield return AppContext.BaseDirectory;
        yield return Path.GetTempPath();
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
