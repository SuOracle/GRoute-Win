using System.Net;
using System.Text;
using System.Text.Json;

namespace GRoute.Windows.Core;

public static class ConfigParser
{
    public static ProxyConfig? Parse(string uri, ConfigSource source = ConfigSource.Personal)
    {
        if (uri.StartsWith("vless://", StringComparison.Ordinal)) return ParseVless(uri, source);
        if (uri.StartsWith("vmess://", StringComparison.Ordinal)) return ParseVmess(uri, source);
        if (uri.StartsWith("trojan://", StringComparison.Ordinal)) return ParseTrojan(uri, source);
        if (uri.StartsWith("ss://", StringComparison.Ordinal)) return ParseShadowsocks(uri, source);
        if (uri.StartsWith("wireguard://", StringComparison.Ordinal) || uri.StartsWith("wg://", StringComparison.Ordinal)) return ParseWireguard(uri, source);
        return null;
    }

    private static ProxyConfig? ParseWireguard(string raw, ConfigSource source)
    {
        try
        {
            string prefix = raw.StartsWith("wireguard://", StringComparison.Ordinal) ? "wireguard://" : "wg://";
            var (name, uhp, p) = SplitUserUri(raw.Substring(prefix.Length), "WireGuard");
            var (secret, address, port) = SplitUserHostPort(uhp);
            return new ProxyConfig
            {
                Name = name,
                Protocol = "wireguard",
                Address = address,
                Port = port,
                PrivateKey = WebUtility.UrlDecode(secret),
                PublicKey = WebUtility.UrlDecode(Get(p, "publickey", Get(p, "peer", ""))),
                PresharedKey = WebUtility.UrlDecode(Get(p, "presharedkey", Get(p, "psk", ""))),
                WgAddress = WebUtility.UrlDecode(Get(p, "address", Get(p, "ip", ""))),
                Mtu = int.TryParse(Get(p, "mtu", "1420"), out var m) ? m : 1420,
                Reserved = Get(p, "reserved", ""),
                Source = source
            };
        }
        catch
        {
            return null;
        }
    }

    private static ProxyConfig? ParseVless(string raw, ConfigSource source)
    {
        try
        {
            var (name, uhp, p) = SplitUserUri(raw.Substring("vless://".Length), "VLESS");
            var (uuid, address, port) = SplitUserHostPort(uhp);
            return new ProxyConfig
            {
                Name = name,
                Protocol = "vless",
                Address = address,
                Port = port,
                Uuid = uuid,
                Encryption = Get(p, "encryption", "none"),
                Flow = Get(p, "flow", ""),
                Network = Get(p, "type", "tcp"),
                Security = Get(p, "security", "none"),
                Sni = Get(p, "sni", ""),
                PublicKey = Get(p, "pbk", ""),
                ShortId = Get(p, "sid", ""),
                Fingerprint = Get(p, "fp", "chrome"),
                Path = Get(p, "path", ""),
                Host = Get(p, "host", ""),
                Source = source
            };
        }
        catch
        {
            return null;
        }
    }

    private static ProxyConfig? ParseTrojan(string raw, ConfigSource source)
    {
        try
        {
            var (name, uhp, p) = SplitUserUri(raw.Substring("trojan://".Length), "Trojan");
            var (password, address, port) = SplitUserHostPort(uhp);
            return new ProxyConfig
            {
                Name = name,
                Protocol = "trojan",
                Address = address,
                Port = port,
                Password = WebUtility.UrlDecode(password),
                Flow = Get(p, "flow", ""),
                Network = Get(p, "type", "tcp"),
                Security = Get(p, "security", "tls"),
                Sni = Get(p, "sni", ""),
                PublicKey = Get(p, "pbk", ""),
                ShortId = Get(p, "sid", ""),
                Fingerprint = Get(p, "fp", "chrome"),
                Path = Get(p, "path", ""),
                Host = Get(p, "host", ""),
                Source = source
            };
        }
        catch
        {
            return null;
        }
    }

    private static ProxyConfig? ParseVmess(string raw, ConfigSource source)
    {
        try
        {
            using var doc = JsonDocument.Parse(DecodeBase64ToString(raw.Substring("vmess://".Length).Trim()));
            var o = doc.RootElement;
            string tls = OptString(o, "tls", "");
            return new ProxyConfig
            {
                Name = OptString(o, "ps", "VMess"),
                Protocol = "vmess",
                Address = OptString(o, "add", ""),
                Port = ParseIntOr(OptString(o, "port", ""), 0),
                Uuid = OptString(o, "id", ""),
                AlterId = ParseIntOr(OptString(o, "aid", ""), 0),
                Encryption = Ne(OptString(o, "scy", "auto"), "auto"),
                Network = OptString(o, "net", "tcp"),
                Security = tls.Length > 0 && tls != "none" ? "tls" : "none",
                Sni = OptString(o, "sni", ""),
                Fingerprint = Ne(OptString(o, "fp", "chrome"), "chrome"),
                Path = OptString(o, "path", ""),
                Host = OptString(o, "host", ""),
                Source = source
            };
        }
        catch
        {
            return null;
        }
    }

    private static ProxyConfig? ParseShadowsocks(string raw, ConfigSource source)
    {
        try
        {
            string body = raw.Substring("ss://".Length);
            int hash = body.IndexOf('#');
            string name = hash >= 0 ? WebUtility.UrlDecode(body.Substring(hash + 1)) : "Shadowsocks";
            string main = hash >= 0 ? body.Substring(0, hash) : body;
            int q = main.IndexOf('?');
            if (q >= 0) main = main.Substring(0, q);

            string method, password, address;
            int port;
            if (main.Contains('@'))
            {
                int at = main.LastIndexOf('@');
                string info = DecodeBase64ToString(main.Substring(0, at));
                string hp = main.Substring(at + 1);
                int colon = hp.LastIndexOf(':');
                address = hp.Substring(0, colon);
                port = int.Parse(hp.Substring(colon + 1));
                int mc = info.IndexOf(':');
                method = info.Substring(0, mc);
                password = info.Substring(mc + 1);
            }
            else
            {
                string dec = DecodeBase64ToString(main);
                int at = dec.LastIndexOf('@');
                string mp = dec.Substring(0, at);
                string hp = dec.Substring(at + 1);
                int colon = hp.LastIndexOf(':');
                address = hp.Substring(0, colon);
                port = int.Parse(hp.Substring(colon + 1));
                int mc = mp.IndexOf(':');
                method = mp.Substring(0, mc);
                password = mp.Substring(mc + 1);
            }

            return new ProxyConfig
            {
                Name = name,
                Protocol = "shadowsocks",
                Address = address,
                Port = port,
                Method = method,
                Password = password,
                Source = source
            };
        }
        catch
        {
            return null;
        }
    }

    private static (string Name, string Uhp, Dictionary<string, string> Params) SplitUserUri(string body, string fallback)
    {
        int hash = body.IndexOf('#');
        string name = hash >= 0 ? WebUtility.UrlDecode(body.Substring(hash + 1)) : fallback;
        string main = hash >= 0 ? body.Substring(0, hash) : body;
        int q = main.IndexOf('?');
        string uhp = q >= 0 ? main.Substring(0, q) : main;
        var query = q >= 0 ? main.Substring(q + 1) : "";
        return (name, uhp, ParseQuery(query));
    }

    private static (string User, string Address, int Port) SplitUserHostPort(string uhp)
    {
        int at = uhp.IndexOf('@');
        string hostPort = uhp.Substring(at + 1);
        int colon = hostPort.LastIndexOf(':');
        return (uhp.Substring(0, at), hostPort.Substring(0, colon), int.Parse(hostPort.Substring(colon + 1)));
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var map = new Dictionary<string, string>();
        if (query.Length == 0) return map;
        foreach (var part in query.Split('&'))
        {
            int eq = part.IndexOf('=');
            if (eq < 0) continue;
            map[part.Substring(0, eq)] = WebUtility.UrlDecode(part.Substring(eq + 1));
        }
        return map;
    }

    private static string Get(Dictionary<string, string> p, string key, string fallback) =>
        p.TryGetValue(key, out var v) ? v : fallback;

    private static string Ne(string value, string fallback) => value.Length == 0 ? fallback : value;

    private static int ParseIntOr(string s, int fallback) => int.TryParse(s, out var v) ? v : fallback;

    private static string OptString(JsonElement o, string key, string fallback)
    {
        if (o.ValueKind != JsonValueKind.Object) return fallback;
        if (!o.TryGetProperty(key, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? fallback,
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    private static string DecodeBase64ToString(string s)
    {
        string t = s.Trim().Replace("-", "+").Replace("_", "/").Replace("\n", "").Replace("\r", "");
        int pad = t.Length % 4;
        if (pad == 2) t += "==";
        else if (pad == 3) t += "=";
        return Encoding.UTF8.GetString(Convert.FromBase64String(t));
    }
}
