using System;
using System.Collections.Generic;
using System.Text;

namespace GRoute.Windows.Core;

public static class WireGuardConf
{
    public static bool LooksLikeConf(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 512 * 1024) return false;
        string text;
        try { text = Encoding.UTF8.GetString(bytes); }
        catch { return false; }
        return LooksLikeConf(text);
    }

    public static bool LooksLikeConf(string text)
    {
        return text.Contains("[Interface]", StringComparison.OrdinalIgnoreCase)
            && text.Contains("[Peer]", StringComparison.OrdinalIgnoreCase);
    }

    public static List<ProxyConfig> Parse(string text, string fallbackName)
    {
        var result = new List<ProxyConfig>();

        string privateKey = "";
        string wgAddress = "";
        int mtu = 1420;

        string peerPublic = "";
        string peerPsk = "";
        string endpoint = "";
        bool inPeer = false;
        bool sawPeer = false;

        void Flush()
        {
            if (!sawPeer) return;
            var (host, port) = SplitEndpoint(endpoint);
            if (host.Length == 0) return;

            result.Add(new ProxyConfig
            {
                Name = result.Count == 0 ? fallbackName : $"{fallbackName} {result.Count + 1}",
                Protocol = "wireguard",
                Address = host,
                Port = port,
                PrivateKey = privateKey,
                PublicKey = peerPublic,
                PresharedKey = peerPsk,
                WgAddress = wgAddress,
                Mtu = mtu,
                Source = ConfigSource.Personal
            });

            peerPublic = "";
            peerPsk = "";
            endpoint = "";
        }

        foreach (var rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line[0] == '#' || line[0] == ';') continue;

            if (line[0] == '[')
            {
                if (line.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
                {
                    Flush();
                    inPeer = true;
                    sawPeer = true;
                }
                else
                {
                    Flush();
                    inPeer = false;
                    sawPeer = false;
                }
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            int hash = val.IndexOf('#');
            if (hash >= 0) val = val.Substring(0, hash).Trim();
            if (val.Length == 0) continue;

            if (!inPeer)
            {
                if (key.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase)) privateKey = val;
                else if (key.Equals("Address", StringComparison.OrdinalIgnoreCase)) wgAddress = val;
                else if (key.Equals("MTU", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(val, out var m) && m > 0) mtu = m;
            }
            else
            {
                if (key.Equals("PublicKey", StringComparison.OrdinalIgnoreCase)) peerPublic = val;
                else if (key.Equals("PresharedKey", StringComparison.OrdinalIgnoreCase)) peerPsk = val;
                else if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase)) endpoint = val;
            }
        }
        Flush();

        return result;
    }

    private static (string Host, int Port) SplitEndpoint(string endpoint)
    {
        if (endpoint.Length == 0) return ("", 0);

        if (endpoint[0] == '[')
        {
            int close = endpoint.IndexOf(']');
            if (close < 0) return ("", 0);
            string h6 = endpoint.Substring(1, close - 1);
            int p6 = 51820;
            if (close + 2 < endpoint.Length && endpoint[close + 1] == ':')
                int.TryParse(endpoint.Substring(close + 2), out p6);
            return (h6, p6 <= 0 ? 51820 : p6);
        }

        int colon = endpoint.LastIndexOf(':');
        if (colon <= 0) return (endpoint, 51820);
        string host = endpoint.Substring(0, colon);
        return int.TryParse(endpoint.Substring(colon + 1), out var port) && port > 0
            ? (host, port)
            : (host, 51820);
    }
}
