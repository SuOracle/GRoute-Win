using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GRoute.Windows.Core;

public static class Warp
{
    private const string ApiVersion = "v0a2158";
    private const string ClientVersion = "a-6.10-2158";
    private const string UserAgent = "okhttp/3.12.1";
    private const int DefaultMtu = 1280;

    private static readonly (string Host, int Port)[] Endpoints =
    {
        ("162.159.192.1", 2408),
        ("188.114.96.1", 2408),
        ("188.114.97.1", 2408),
        ("188.114.98.1", 2408),
        ("188.114.99.1", 2408),
        ("188.114.96.1", 1701),
        ("188.114.97.1", 500),
        ("188.114.98.1", 4500)
    };

    private static readonly HttpClient Direct = CreateClient(0);
    private static HttpClient? _viaTunnel;
    private static int _tunnelPort;

    private static HttpClient Client(int proxyPort)
    {
        if (proxyPort <= 0) return Direct;
        if (_viaTunnel is null || _tunnelPort != proxyPort)
        {
            _viaTunnel?.Dispose();
            _viaTunnel = CreateClient(proxyPort);
            _tunnelPort = proxyPort;
        }
        return _viaTunnel;
    }

    private static HttpClient CreateClient(int proxyPort)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        if (proxyPort > 0)
        {
            handler.Proxy = new WebProxy($"http://127.0.0.1:{proxyPort}");
            handler.UseProxy = true;
        }
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }

    public sealed class Account
    {
        public string PrivateKey { get; init; } = "";
        public string PeerPublicKey { get; init; } = "";
        public string WgAddress { get; init; } = "";
        public string Reserved { get; init; } = "";
    }

    public static ProxyConfig BuildConfig(Account a, string host, int port, string name) =>
        new()
        {
            Name = name,
            Protocol = "wireguard",
            Address = host,
            Port = port,
            PrivateKey = a.PrivateKey,
            PublicKey = a.PeerPublicKey,
            WgAddress = a.WgAddress,
            Mtu = DefaultMtu,
            Reserved = a.Reserved,
            Source = ConfigSource.Personal
        };

    public static (string Host, int Port)[] DefaultEndpoints => Endpoints;

    public sealed class Result
    {
        public bool Ok { get; init; }
        public List<ProxyConfig> Configs { get; init; } = new();
        public string Message { get; init; } = "";
        public Account? Account { get; init; }
    }

    public static async Task<Result> Register(int proxyPort = 0)
    {
        try
        {
            var (privateKey, publicKey) = Curve25519.GenerateKeyPair();

            string tos = DateTime.UtcNow.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

            var body = new JsonObject
            {
                ["key"] = publicKey,
                ["install_id"] = "",
                ["fcm_token"] = "",
                ["tos"] = tos,
                ["model"] = "PC",
                ["serial_number"] = "",
                ["locale"] = "en_US"
            };

            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"https://api.cloudflareclient.com/{ApiVersion}/reg");
            req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            req.Headers.TryAddWithoutValidation("CF-Client-Version", ClientVersion);

            using var resp = await Client(proxyPort).SendAsync(req).ConfigureAwait(false);
            string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                string snippet = text.Length > 200 ? text.Substring(0, 200) : text;
                return new Result { Ok = false, Message = $"HTTP {(int)resp.StatusCode}: {snippet}" };
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(text);
            }
            catch
            {
                return new Result { Ok = false, Message = "Could not read the Cloudflare response." };
            }

            var cfg = root?["config"];
            if (cfg is null)
                return new Result { Ok = false, Message = "Cloudflare response was missing the config section." };

            string deviceId = Str(root?["id"]);
            string token = Str(root?["token"]);
            if (deviceId.Length == 0 || token.Length == 0)
                return new Result { Ok = false, Message = "Cloudflare response was missing the device id or token." };

            var enabled = await EnableWarp(deviceId, token, proxyPort).ConfigureAwait(false);
            if (enabled is null)
                return new Result { Ok = false, Message = "Could not enable WARP on the new account." };
            cfg = enabled;

            string peerPub = Str(cfg["peers"]?[0]?["public_key"]);
            if (peerPub.Length == 0)
                return new Result { Ok = false, Message = "Cloudflare response was missing the peer key." };

            var addrObj = cfg["interface"]?["addresses"];
            string v4 = Str(addrObj?["v4"]);
            string v6 = Str(addrObj?["v6"]);

            var addrs = new List<string>();
            if (v4.Length > 0) addrs.Add(v4 + "/32");
            if (v6.Length > 0) addrs.Add(v6 + "/128");
            if (addrs.Count == 0)
                return new Result { Ok = false, Message = "Cloudflare response was missing the interface address." };

            string wgAddress = string.Join(",", addrs);
            string reserved = DecodeReserved(Str(cfg["client_id"]));

            var account = new Account
            {
                PrivateKey = privateKey,
                PeerPublicKey = peerPub,
                WgAddress = wgAddress,
                Reserved = reserved
            };

            var configs = new List<ProxyConfig>();
            for (int i = 0; i < Endpoints.Length; i++)
            {
                var (host, port) = Endpoints[i];
                configs.Add(BuildConfig(account, host, port, $"WARP {i + 1}"));
            }

            return new Result { Ok = true, Configs = configs, Account = account };
        }
        catch (TaskCanceledException)
        {
            return new Result { Ok = false, Message = "Cloudflare did not respond in time." };
        }
        catch (Exception ex)
        {
            return new Result { Ok = false, Message = ex.Message };
        }
    }

    private static async Task<JsonNode?> EnableWarp(string deviceId, string token, int proxyPort)
    {
        try
        {
            using var req = new HttpRequestMessage(new HttpMethod("PATCH"),
                $"https://api.cloudflareclient.com/{ApiVersion}/reg/{deviceId}");
            req.Content = new StringContent("{\"warp_enabled\":true}", Encoding.UTF8, "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            req.Headers.TryAddWithoutValidation("CF-Client-Version", ClientVersion);
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

            using var resp = await Client(proxyPort).SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonNode.Parse(text)?["config"];
        }
        catch
        {
            return null;
        }
    }

    private static string Str(JsonNode? node)
    {
        if (node is null) return "";
        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return "";
        }
    }

    private static string DecodeReserved(string clientId)
    {
        try
        {
            if (clientId.Length == 0) return "";
            string s = clientId.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            byte[] b = Convert.FromBase64String(s);
            return b.Length >= 3 ? $"{b[0]},{b[1]},{b[2]}" : "";
        }
        catch
        {
            return "";
        }
    }
}