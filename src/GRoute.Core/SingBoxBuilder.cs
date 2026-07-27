using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;

namespace GRoute.Windows.Core;

public static class SingBoxBuilder
{
    public static string LogLevel = "warn";
    public const int ClashApiPort = 10621;

    private static bool? _clashApi;

    public static bool SupportsClashApi => _clashApi ??= ProbeClashApi();

    private static bool ProbeClashApi()
    {
        try
        {
            string exe = Path.Combine(AppContext.BaseDirectory, "libs", "sing-box.exe");
            if (!File.Exists(exe)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "version",
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            if (p is null) return false;
            string text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(3000);
            return text.Contains("with_clash_api", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool Supports(ProxyConfig config) =>
        config.Protocol is "hysteria2" or "hysteria" or "tuic";

    public static string Build(ProxyConfig config)
    {
        var root = new JsonObject
        {
            ["log"] = new JsonObject
            {
                ["level"] = LogLevel,
                ["timestamp"] = false
            },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "socks",
                    ["tag"] = "socks-in",
                    ["listen"] = "127.0.0.1",
                    ["listen_port"] = ConfigBuilder.SocksPort
                },
                new JsonObject
                {
                    ["type"] = "http",
                    ["tag"] = "http-in",
                    ["listen"] = "127.0.0.1",
                    ["listen_port"] = ConfigBuilder.HttpPort
                }
            },
            ["outbounds"] = new JsonArray
            {
                Outbound(config),
                new JsonObject { ["type"] = "direct", ["tag"] = "direct" }
            }
        };

        if (SupportsClashApi)
        {
            root["experimental"] = new JsonObject
            {
                ["clash_api"] = new JsonObject
                {
                    ["external_controller"] = $"127.0.0.1:{ClashApiPort}"
                }
            };
        }

        return root.ToJsonString();
    }

    public static string BuildForTest(ProxyConfig config, int httpPort)
    {
        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "error", ["timestamp"] = false },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "http",
                    ["tag"] = "http-in",
                    ["listen"] = "127.0.0.1",
                    ["listen_port"] = httpPort
                }
            },
            ["outbounds"] = new JsonArray { Outbound(config) }
        };
        return root.ToJsonString();
    }

    private static JsonObject Outbound(ProxyConfig config)
    {
        var tls = new JsonObject
        {
            ["enabled"] = true,
            ["insecure"] = config.AllowInsecure
        };

        string sni = config.Sni.Trim();
        if (sni.Length == 0 && !IsIpLiteral(config.Address)) sni = config.Address;
        if (sni.Length > 0) tls["server_name"] = sni;

        var alpn = SplitAlpn(config.Alpn);
        if (alpn.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var a in alpn) arr.Add(a);
            tls["alpn"] = arr;
        }

        if (config.Protocol == "tuic")
        {
            var tuic = new JsonObject
            {
                ["type"] = "tuic",
                ["tag"] = "proxy",
                ["server"] = config.Address,
                ["server_port"] = config.Port,
                ["uuid"] = config.Uuid,
                ["password"] = config.Password,
                ["tls"] = tls
            };
            return tuic;
        }

        var outbound = new JsonObject
        {
            ["type"] = "hysteria2",
            ["tag"] = "proxy",
            ["server"] = config.Address,
            ["server_port"] = config.Port,
            ["password"] = config.Password,
            ["tls"] = tls
        };

        if (!string.IsNullOrWhiteSpace(config.Method) && !string.IsNullOrWhiteSpace(config.ObfsPassword))
        {
            outbound["obfs"] = new JsonObject
            {
                ["type"] = config.Method,
                ["password"] = config.ObfsPassword
            };
        }

        if (config.UpMbps > 0) outbound["up_mbps"] = config.UpMbps;
        if (config.DownMbps > 0) outbound["down_mbps"] = config.DownMbps;

        return outbound;
    }

    public static bool IsIpLiteral(string host) =>
        System.Net.IPAddress.TryParse(host.Trim('[', ']'), out _);

    private static List<string> SplitAlpn(string alpn)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(alpn)) return list;
        foreach (var part in alpn.Split(','))
        {
            var t = part.Trim();
            if (t.Length > 0) list.Add(t);
        }
        return list;
    }
}
