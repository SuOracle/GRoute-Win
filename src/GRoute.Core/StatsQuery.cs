using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GRoute.Windows.Core;

public static class StatsQuery
{
    private static readonly string XrayPath =
        Path.Combine(AppContext.BaseDirectory, "libs", "xray.exe");

    public static (long Up, long Down) Query(int apiPort)
    {
        long up = 0;
        long down = 0;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = XrayPath,
                Arguments = $"api statsquery --server=127.0.0.1:{apiPort} -pattern \"outbound>>>proxy>>>traffic\"",
                WorkingDirectory = Path.GetDirectoryName(XrayPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            if (p is null) return (0, 0);
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(output) ? "{}" : output);
            if (doc.RootElement.TryGetProperty("stat", out var stat) && stat.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in stat.EnumerateArray())
                {
                    var name = s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    long value = 0;
                    if (s.TryGetProperty("value", out var v))
                    {
                        value = v.ValueKind == JsonValueKind.String
                            ? (long.TryParse(v.GetString(), out var lv) ? lv : 0)
                            : (v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0);
                    }
                    if (name.EndsWith("uplink")) up = value;
                    else if (name.EndsWith("downlink")) down = value;
                }
            }
        }
        catch
        {
        }
        return (up, down);
    }
}
