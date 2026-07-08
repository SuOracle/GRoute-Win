using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace GRoute.Windows.Core;

public static class ConfigBuilder
{
    public const int SocksPort = 10808;
    public static int HttpPort = 10626;
    public static string LogLevel = "warning";
    public const int ApiPort = 10620;

    public static bool GeoFilesPresent
    {
        get
        {
            Assets.EnsureSeeded();
            return Assets.GeoipPresent && Assets.GeositePresent;
        }
    }

    public static string Build(ProxyConfig config, bool fragment = false, bool splitRouting = false, bool sniffEnabled = false, IReadOnlyList<string>? sniffProtocols = null, bool sniffRouteOnly = true)
    {
        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = LogLevel },
            ["stats"] = new JsonObject(),
            ["policy"] = new JsonObject
            {
                ["system"] = new JsonObject
                {
                    ["statsOutboundUplink"] = true,
                    ["statsOutboundDownlink"] = true
                }
            },
            ["api"] = new JsonObject
            {
                ["tag"] = "api",
                ["listen"] = $"127.0.0.1:{ApiPort}",
                ["services"] = new JsonArray("StatsService")
            }
        };

        var socksIn = new JsonObject
        {
            ["tag"] = "socks-in",
            ["listen"] = "127.0.0.1",
            ["port"] = SocksPort,
            ["protocol"] = "socks",
            ["settings"] = new JsonObject { ["udp"] = true }
        };
        var httpIn = new JsonObject
        {
            ["tag"] = "http-in",
            ["listen"] = "127.0.0.1",
            ["port"] = HttpPort,
            ["protocol"] = "http"
        };
        if (sniffEnabled || splitRouting)
        {
            var protos = sniffProtocols is { Count: > 0 } ? sniffProtocols : DefaultSniff;
            bool routeOnly = sniffEnabled ? sniffRouteOnly : true;
            socksIn["sniffing"] = MakeSniff(protos, routeOnly);
            httpIn["sniffing"] = MakeSniff(protos, routeOnly);
        }
        root["inbounds"] = new JsonArray(socksIn, httpIn);

        var proxyOut = BuildOutbound(config);
        if (fragment)
        {
            ((JsonObject)proxyOut["streamSettings"]!)["sockopt"] =
                new JsonObject { ["dialerProxy"] = "fragment" };
        }

        var outbounds = new JsonArray(proxyOut);
        if (fragment)
        {
            outbounds.Add(new JsonObject
            {
                ["tag"] = "fragment",
                ["protocol"] = "freedom",
                ["settings"] = new JsonObject
                {
                    ["fragment"] = new JsonObject
                    {
                        ["packets"] = "tlshello",
                        ["length"] = "100-200",
                        ["interval"] = "10-20"
                    }
                }
            });
        }
        outbounds.Add(new JsonObject { ["tag"] = "direct", ["protocol"] = "freedom" });
        root["outbounds"] = outbounds;

        var rules = new JsonArray();
        if (splitRouting)
        {
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["ip"] = new JsonArray("geoip:private", "geoip:ir"),
                ["outboundTag"] = "direct"
            });
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["domain"] = new JsonArray("geosite:category-ir"),
                ["outboundTag"] = "direct"
            });
        }
        rules.Add(new JsonObject
        {
            ["type"] = "field",
            ["inboundTag"] = new JsonArray("socks-in", "http-in"),
            ["outboundTag"] = "proxy"
        });
        root["routing"] = new JsonObject
        {
            ["domainStrategy"] = "AsIs",
            ["rules"] = rules
        };

        return root.ToJsonString();
    }

    public static string BuildForTest(ProxyConfig config, int httpPort)
    {
        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = "none" },
            ["inbounds"] = new JsonArray(new JsonObject
            {
                ["tag"] = "http-in",
                ["listen"] = "127.0.0.1",
                ["port"] = httpPort,
                ["protocol"] = "http"
            }),
            ["outbounds"] = new JsonArray(BuildOutbound(config))
        };
        return root.ToJsonString();
    }

    private static readonly string[] DefaultSniff = { "http", "tls", "quic" };

    private static JsonObject MakeSniff(IReadOnlyList<string> protos, bool routeOnly)
    {
        var arr = new JsonArray();
        foreach (var p in protos) arr.Add(p);
        return new JsonObject
        {
            ["enabled"] = true,
            ["destOverride"] = arr,
            ["routeOnly"] = routeOnly
        };
    }

    private static JsonObject BuildOutbound(ProxyConfig config)
    {
        JsonObject settings;
        switch (config.Protocol)
        {
            case "vless":
            case "vmess":
            {
                var user = new JsonObject { ["id"] = config.Uuid };
                if (config.Protocol == "vless")
                {
                    user["encryption"] = config.Encryption;
                    if (config.Flow.Length > 0) user["flow"] = config.Flow;
                }
                else
                {
                    user["alterId"] = config.AlterId;
                    user["security"] = config.Encryption.Length == 0 ? "auto" : config.Encryption;
                }
                var vnext = new JsonObject
                {
                    ["address"] = config.Address,
                    ["port"] = config.Port,
                    ["users"] = new JsonArray(user)
                };
                settings = new JsonObject { ["vnext"] = new JsonArray(vnext) };
                break;
            }
            case "trojan":
            {
                var server = new JsonObject
                {
                    ["address"] = config.Address,
                    ["port"] = config.Port,
                    ["password"] = config.Password
                };
                if (config.Flow.Length > 0) server["flow"] = config.Flow;
                settings = new JsonObject { ["servers"] = new JsonArray(server) };
                break;
            }
            case "shadowsocks":
            {
                var server = new JsonObject
                {
                    ["address"] = config.Address,
                    ["port"] = config.Port,
                    ["method"] = config.Method,
                    ["password"] = config.Password
                };
                settings = new JsonObject { ["servers"] = new JsonArray(server) };
                break;
            }
            case "wireguard":
            {
                var peer = new JsonObject
                {
                    ["publicKey"] = config.PublicKey,
                    ["endpoint"] = $"{config.Address}:{config.Port}",
                    ["allowedIPs"] = new JsonArray("0.0.0.0/0", "::/0")
                };
                if (config.PresharedKey.Length > 0) peer["preSharedKey"] = config.PresharedKey;

                var addrArr = new JsonArray();
                foreach (var a in config.WgAddress.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    addrArr.Add(a);
                if (addrArr.Count == 0) addrArr.Add("10.0.0.2/32");

                settings = new JsonObject
                {
                    ["secretKey"] = config.PrivateKey,
                    ["address"] = addrArr,
                    ["peers"] = new JsonArray(peer),
                    ["mtu"] = config.Mtu > 0 ? config.Mtu : 1420
                };

                if (config.Reserved.Length > 0)
                {
                    var res = new JsonArray();
                    foreach (var r in config.Reserved.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        if (int.TryParse(r, out var rv)) res.Add(rv);
                    if (res.Count == 3) settings["reserved"] = res;
                }
                break;
            }
            case "http":
            case "socks":
            {
                var server = new JsonObject
                {
                    ["address"] = config.Address,
                    ["port"] = config.Port
                };
                if (config.Username.Length > 0 || config.Password.Length > 0)
                {
                    server["users"] = new JsonArray(new JsonObject
                    {
                        ["user"] = config.Username,
                        ["pass"] = config.Password
                    });
                }
                settings = new JsonObject { ["servers"] = new JsonArray(server) };
                break;
            }
            default:
                settings = new JsonObject();
                break;
        }

        var outbound = new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = config.Protocol,
            ["settings"] = settings
        };
        if (config.Protocol is not ("wireguard" or "http" or "socks"))
            outbound["streamSettings"] = BuildStream(config);
        return outbound;
    }

    private static JsonObject BuildStream(ProxyConfig config)
    {
        var net = string.IsNullOrEmpty(config.Network) ? "tcp" : config.Network;
        if (net == "h2") net = "http";
        if (net == "mkcp") net = "kcp";
        if (net == "splithttp") net = "xhttp";
        var stream = new JsonObject { ["network"] = net };
        string path = config.Path.Length == 0 ? "/" : config.Path;

        switch (net)
        {
            case "ws":
            {
                var ws = new JsonObject { ["path"] = path };
                if (config.Host.Length > 0) ws["headers"] = new JsonObject { ["Host"] = config.Host };
                stream["wsSettings"] = ws;
                break;
            }
            case "grpc":
            {
                stream["grpcSettings"] = new JsonObject
                {
                    ["serviceName"] = config.ServiceName,
                    ["multiMode"] = config.Mode == "multi"
                };
                break;
            }
            case "http":
            {
                var h2 = new JsonObject { ["path"] = path };
                if (config.Host.Length > 0) h2["host"] = new JsonArray(config.Host);
                stream["httpSettings"] = h2;
                break;
            }
            case "quic":
            {
                stream["quicSettings"] = new JsonObject
                {
                    ["security"] = "none",
                    ["key"] = "",
                    ["header"] = new JsonObject { ["type"] = config.HeaderType.Length == 0 ? "none" : config.HeaderType }
                };
                break;
            }
            case "kcp":
            {
                var kcp = new JsonObject
                {
                    ["header"] = new JsonObject { ["type"] = config.HeaderType.Length == 0 ? "none" : config.HeaderType }
                };
                if (config.Path.Length > 0) kcp["seed"] = config.Path;
                stream["kcpSettings"] = kcp;
                break;
            }
            case "httpupgrade":
            {
                var hu = new JsonObject { ["path"] = path };
                if (config.Host.Length > 0) hu["host"] = config.Host;
                stream["httpupgradeSettings"] = hu;
                break;
            }
            case "xhttp":
            {
                var xh = new JsonObject { ["path"] = path };
                if (config.Host.Length > 0) xh["host"] = config.Host;
                if (config.Mode.Length > 0) xh["mode"] = config.Mode;
                stream["xhttpSettings"] = xh;
                break;
            }
            default:
            {
                if (config.HeaderType == "http")
                {
                    var req = new JsonObject { ["path"] = new JsonArray(path) };
                    if (config.Host.Length > 0) req["headers"] = new JsonObject { ["Host"] = new JsonArray(config.Host) };
                    stream["tcpSettings"] = new JsonObject
                    {
                        ["header"] = new JsonObject { ["type"] = "http", ["request"] = req }
                    };
                }
                break;
            }
        }

        switch (config.Security)
        {
            case "reality":
                stream["security"] = "reality";
                stream["realitySettings"] = new JsonObject
                {
                    ["serverName"] = config.Sni,
                    ["publicKey"] = config.PublicKey,
                    ["shortId"] = config.ShortId,
                    ["fingerprint"] = config.Fingerprint.Length == 0 ? "chrome" : config.Fingerprint,
                    ["spiderX"] = "/"
                };
                break;
            case "tls":
                stream["security"] = "tls";
                var tls = new JsonObject
                {
                    ["serverName"] = config.Sni.Length > 0
                        ? config.Sni
                        : config.Host.Length > 0 ? config.Host : config.Address,
                    ["fingerprint"] = config.Fingerprint.Length == 0 ? "chrome" : config.Fingerprint
                };
                if (config.Alpn.Length > 0)
                {
                    var alpn = new JsonArray();
                    foreach (var a in config.Alpn.Split(','))
                        if (a.Trim().Length > 0) alpn.Add(a.Trim());
                    tls["alpn"] = alpn;
                }
                stream["tlsSettings"] = tls;
                break;
        }

        return stream;
    }
}
