using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GRoute.Windows.Core;

namespace GRoute.Windows;

public sealed record IpLocation(string Ip, string City, string Country, string CountryCode, double Lat, double Lon);

public static class IpLocator
{
    public static IpLocation Fallback { get; } =
        new IpLocation("\u2014", "Tehran", "Iran", "IR", 35.6892, 51.3890);

    public static async Task<IpLocation?> FetchAsync(bool throughProxy, CancellationToken ct = default)
    {
        try
        {
            using var handler = new HttpClientHandler { UseCookies = false };
            if (throughProxy)
            {
                handler.Proxy = new WebProxy($"http://127.0.0.1:{ConfigBuilder.HttpPort}");
                handler.UseProxy = true;
            }
            else
            {
                handler.Proxy = null;
                handler.UseProxy = false;
            }

            using var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(throughProxy ? 25 : 8)
            };
            http.DefaultRequestHeaders.Add("User-Agent", "GRoute");
            http.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };
            http.DefaultRequestHeaders.Add("Pragma", "no-cache");

            string? ip = await FetchIpAsync(http, "https://api4.ipify.org", ct)
                      ?? await FetchIpAsync(http, "https://api6.ipify.org", ct);

            ct.ThrowIfCancellationRequested();

            return await TryIpWho(http, ip, ct)
                ?? await TryIpApi(http, ip, ct)
                ?? await TryIpApiCo(http, ip, ct);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IpLocation?> TryIpWho(HttpClient http, string? ip, CancellationToken ct)
    {
        try
        {
            string body = await http.GetStringAsync(
                ip != null ? "https://ipwho.is/" + ip : "https://ipwho.is/", ct);
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

    private static async Task<IpLocation?> TryIpApi(HttpClient http, string? ip, CancellationToken ct)
    {
        try
        {
            string url = "http://ip-api.com/json/" + (ip ?? "") + "?fields=status,country,countryCode,city,lat,lon,query";
            string body = await http.GetStringAsync(url, ct);
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

    private static async Task<IpLocation?> TryIpApiCo(HttpClient http, string? ip, CancellationToken ct)
    {
        try
        {
            string url = ip != null ? "https://ipapi.co/" + ip + "/json/" : "https://ipapi.co/json/";
            string body = await http.GetStringAsync(url, ct);
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

    private static async Task<string?> FetchIpAsync(HttpClient http, string url, CancellationToken ct)
    {
        try
        {
            string s = (await http.GetStringAsync(url, ct)).Trim();
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
