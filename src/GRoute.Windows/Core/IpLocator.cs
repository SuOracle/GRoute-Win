using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace GRoute.Windows;

public sealed record IpLocation(string Ip, string City, string Country, string CountryCode, double Lat, double Lon);

public static class IpLocator
{
    public static IpLocation Fallback { get; } =
        new IpLocation("\u2014", "Tehran", "Iran", "IR", 35.6892, 51.3890);

    public static async Task<IpLocation?> FetchAsync(bool throughProxy)
    {
        try
        {
            using var handler = new HttpClientHandler();
            if (throughProxy)
            {
                handler.Proxy = new WebProxy("http://127.0.0.1:10626");
                handler.UseProxy = true;
            }
            else
            {
                handler.UseProxy = false;
            }
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.Add("User-Agent", "GRoute");

            string? ip = await FetchIpAsync(http, "https://api4.ipify.org")
                      ?? await FetchIpAsync(http, "https://api6.ipify.org");

            return await TryIpWho(http, ip)
                ?? await TryIpApi(http, ip)
                ?? await TryIpApiCo(http, ip);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IpLocation?> TryIpWho(HttpClient http, string? ip)
    {
        try
        {
            string body = await http.GetStringAsync(ip != null ? "https://ipwho.is/" + ip : "https://ipwho.is/");
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            if (r.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False) return null;
            return new IpLocation(
                ip ?? Str(r, "ip", "\u2014"), Str(r, "city", "\u2014"),
                Str(r, "country", "\u2014"), Str(r, "country_code", ""),
                Num(r, "latitude", Fallback.Lat), Num(r, "longitude", Fallback.Lon));
        }
        catch { return null; }
    }

    private static async Task<IpLocation?> TryIpApi(HttpClient http, string? ip)
    {
        try
        {
            string url = "http://ip-api.com/json/" + (ip ?? "") + "?fields=status,country,countryCode,city,lat,lon,query";
            string body = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            if (Str(r, "status", "") != "success") return null;
            return new IpLocation(
                ip ?? Str(r, "query", "\u2014"), Str(r, "city", "\u2014"),
                Str(r, "country", "\u2014"), Str(r, "countryCode", ""),
                Num(r, "lat", Fallback.Lat), Num(r, "lon", Fallback.Lon));
        }
        catch { return null; }
    }

    private static async Task<IpLocation?> TryIpApiCo(HttpClient http, string? ip)
    {
        try
        {
            string url = ip != null ? "https://ipapi.co/" + ip + "/json/" : "https://ipapi.co/json/";
            string body = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            if (r.TryGetProperty("error", out var er) && er.ValueKind == JsonValueKind.True) return null;
            return new IpLocation(
                ip ?? Str(r, "ip", "\u2014"), Str(r, "city", "\u2014"),
                Str(r, "country_name", "\u2014"), Str(r, "country_code", ""),
                Num(r, "latitude", Fallback.Lat), Num(r, "longitude", Fallback.Lon));
        }
        catch { return null; }
    }

    private static async Task<string?> FetchIpAsync(HttpClient http, string url)
    {
        try
        {
            string s = (await http.GetStringAsync(url)).Trim();
            if (s.Length > 0 && s.Length <= 45 && !s.Any(char.IsWhiteSpace) && (s.Contains('.') || s.Contains(':')))
                return s;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string Str(JsonElement o, string key, string def) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;

    private static double Num(JsonElement o, string key, double def)
    {
        if (!o.TryGetProperty(key, out var v)) return def;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out var d)) return d;
        return def;
    }
}
