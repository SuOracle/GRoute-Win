using System.Net.Http;
using System.Text;

namespace GRoute.Windows.Core;

public sealed class SubUserInfo
{
    public long Upload { get; set; }
    public long Download { get; set; }
    public long Total { get; set; }
    public long Expire { get; set; }
    public long Used => Upload + Download;
}

public sealed class FetchResult
{
    public List<ProxyConfig> Configs { get; set; } = new();
    public SubUserInfo? UserInfo { get; set; }
}

public static class SubscriptionFetcher
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Add("User-Agent", "GRoute");
        return client;
    }

    public static async Task<FetchResult> FetchFull(string url, ConfigSource source = ConfigSource.Personal)
    {
        using var resp = await Http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        var userInfo = ParseUserInfo(GetHeader(resp, "subscription-userinfo"));
        var text = DecodeMaybeBase64(body);

        var configs = new List<ProxyConfig>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var cfg = ConfigParser.Parse(line, source);
            if (cfg is not null) configs.Add(cfg);
        }
        return new FetchResult { Configs = configs, UserInfo = userInfo };
    }

    private static string? GetHeader(HttpResponseMessage resp, string name)
    {
        if (resp.Headers.TryGetValues(name, out var v1)) return string.Join(";", v1);
        if (resp.Content.Headers.TryGetValues(name, out var v2)) return string.Join(";", v2);
        return null;
    }

    private static SubUserInfo? ParseUserInfo(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        var map = new Dictionary<string, long>();
        foreach (var part in header.Split(';'))
        {
            int eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part.Substring(0, eq).Trim();
            long.TryParse(part.Substring(eq + 1).Trim(), out var val);
            map[key] = val;
        }
        return new SubUserInfo
        {
            Upload = map.GetValueOrDefault("upload"),
            Download = map.GetValueOrDefault("download"),
            Total = map.GetValueOrDefault("total"),
            Expire = map.GetValueOrDefault("expire")
        };
    }

    private static string DecodeMaybeBase64(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.Contains("://")) return trimmed;
        try
        {
            var t = trimmed.Replace("-", "+").Replace("_", "/").Replace("\n", "").Replace("\r", "");
            int pad = t.Length % 4;
            if (pad == 2) t += "==";
            else if (pad == 3) t += "=";
            return Encoding.UTF8.GetString(Convert.FromBase64String(t));
        }
        catch
        {
            return trimmed;
        }
    }
}
