using System.Text.Json.Serialization;

namespace GRoute.Windows.Core;

public sealed class Subscription
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public long Used { get; set; }
    public long Total { get; set; }
    public long Expire { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public string UsageText
    {
        get
        {
            if (Total <= 0 && Expire <= 0) return "";
            var parts = new List<string>();
            if (Total > 0)
            {
                var remaining = Math.Max(Total - Used, 0);
                parts.Add($"{FormatBytes(remaining)} of {FormatBytes(Total)} left");
            }
            if (Expire > 0)
            {
                var daysLeft = (Expire * 1000L - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 86_400_000L;
                if (daysLeft >= 0) parts.Add($"expires in {daysLeft}d");
            }
            return string.Join("  \u2022  ", parts);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.#} MB";
        return $"{mb / 1024.0:0.##} GB";
    }
}
