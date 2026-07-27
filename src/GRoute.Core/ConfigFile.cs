using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GRoute.Windows.Core;

public static class ConfigFile
{
    public const string Extension = "grt";

    private static readonly byte[] Magic = { (byte)'G', (byte)'R', (byte)'T', (byte)'1' };
    private const int KeyBits = 256;
    private const int IvLen = 12;
    private const int TagLen = 16;
    private const int SaltLen = 16;
    private const int Pbkdf2Iters = 210_000;

    private const int FlagPw = 0x01;
    private const int FlagCert = 0x02;
    private const int FlagLock = 0x04;

    private static readonly int[] P1 = { 0x2A, 0x3F, 0x02, 0x18, 0x19, 0x08, 0x40, 0x0A };
    private static readonly int[] P2 = { 0x1F, 0x19, 0x40, 0x1B, 0x5C, 0x40, 0x06, 0x08 };
    private static readonly int[] P3 = { 0x14, 0x40, 0x09, 0x02, 0x00, 0x0C, 0x04, 0x03 };
    private static readonly int[] P4 = { 0x40, 0x1E, 0x08, 0x15, 0x0C, 0x1F, 0x0C, 0x19 };
    private const int Mask = 0x6D;

    private static string StaticSecret()
    {
        var sb = new StringBuilder(32);
        foreach (var part in new[] { P1, P2, P3, P4 })
            foreach (var b in part)
                sb.Append((char)(b ^ Mask));
        return sb.ToString();
    }

    public sealed class WrongPasswordException : Exception { }
    public sealed class NeedsPasswordException : Exception { }
    public sealed class BadFileException : Exception { }

    public static bool IsPasswordProtected(byte[] bytes)
    {
        if (bytes.Length < 5) throw new BadFileException();
        for (int i = 0; i < 4; i++)
            if (bytes[i] != Magic[i]) throw new BadFileException();
        return (bytes[4] & FlagPw) != 0;
    }

    public static bool IsLocked(byte[] bytes)
    {
        if (bytes.Length < 5) throw new BadFileException();
        for (int i = 0; i < 4; i++)
            if (bytes[i] != Magic[i]) throw new BadFileException();
        return (bytes[4] & FlagLock) != 0;
    }

    public static byte[] Encode(IEnumerable<ProxyConfig> configs, string? password, bool locked = false)
    {
        var arr = new JsonArray();
        foreach (var c in configs)
            arr.Add(ToJson(c));
        var root = new JsonObject { ["v"] = 1, ["locked"] = locked, ["configs"] = arr };
        byte[] plain = Encoding.UTF8.GetBytes(root.ToJsonString());

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLen);
        byte[] iv = RandomNumberGenerator.GetBytes(IvLen);
        bool hasPw = !string.IsNullOrEmpty(password);

        byte[] key = DeriveKey(password, salt);

        byte[] ct = new byte[plain.Length];
        byte[] tag = new byte[TagLen];
        using (var gcm = new AesGcm(key, TagLen))
            gcm.Encrypt(iv, plain, ct, tag);

        int flags = 0;
        if (hasPw) flags |= FlagPw;
        if (locked) flags |= FlagLock;

        using var ms = new MemoryStream();
        ms.Write(Magic, 0, 4);
        ms.WriteByte((byte)flags);
        ms.Write(salt, 0, salt.Length);
        ms.Write(iv, 0, iv.Length);
        ms.Write(ct, 0, ct.Length);
        ms.Write(tag, 0, tag.Length);
        return ms.ToArray();
    }

    public static List<ProxyConfig> Decode(byte[] bytes, string? password)
    {
        if (bytes.Length < 5 + SaltLen + IvLen + TagLen) throw new BadFileException();
        for (int i = 0; i < 4; i++)
            if (bytes[i] != Magic[i]) throw new BadFileException();

        int flags = bytes[4];
        bool hasPw = (flags & FlagPw) != 0;
        bool locked = (flags & FlagLock) != 0;
        if (hasPw && string.IsNullOrEmpty(password)) throw new NeedsPasswordException();

        int off = 5;
        byte[] salt = new byte[SaltLen];
        Array.Copy(bytes, off, salt, 0, SaltLen); off += SaltLen;
        byte[] iv = new byte[IvLen];
        Array.Copy(bytes, off, iv, 0, IvLen); off += IvLen;

        int ctLen = bytes.Length - off - TagLen;
        if (ctLen < 0) throw new BadFileException();
        byte[] ct = new byte[ctLen];
        Array.Copy(bytes, off, ct, 0, ctLen); off += ctLen;
        byte[] tag = new byte[TagLen];
        Array.Copy(bytes, off, tag, 0, TagLen);

        byte[] key = DeriveKey(hasPw ? password : null, salt);

        byte[] plain = new byte[ctLen];
        try
        {
            using var gcm = new AesGcm(key, TagLen);
            gcm.Decrypt(iv, ct, tag, plain);
        }
        catch
        {
            if (hasPw) throw new WrongPasswordException();
            throw new BadFileException();
        }

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(Encoding.UTF8.GetString(plain));
        }
        catch
        {
            throw new BadFileException();
        }
        var arr = rootNode?["configs"] as JsonArray ?? throw new BadFileException();

        var result = new List<ProxyConfig>();
        foreach (var node in arr)
        {
            if (node is JsonObject obj)
            {
                var cfg = FromJson(obj);
                cfg.Id = Guid.NewGuid().ToString();
                cfg.SubId = "";
                cfg.Source = ConfigSource.Community;
                cfg.Locked = locked;
                result.Add(cfg);
            }
        }
        return result;
    }

    private static byte[] DeriveKey(string? password, byte[] salt)
    {
        string material = (password ?? "") + "\u0000" + StaticSecret() + "\u0000" + "";
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(material), salt, Pbkdf2Iters, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeyBits / 8);
    }

    private static string S(JsonObject o, string key, string dflt = "")
    {
        var n = o[key];
        return n is null ? dflt : n.GetValue<string>();
    }

    private static int I(JsonObject o, string key, int dflt = 0)
    {
        var n = o[key];
        if (n is null) return dflt;
        try { return n.GetValue<int>(); } catch { return dflt; }
    }

    private static JsonObject ToJson(ProxyConfig c) => new()
    {
        ["name"] = c.Name,
        ["protocol"] = c.Protocol,
        ["address"] = c.Address,
        ["port"] = c.Port,
        ["uuid"] = c.Uuid,
        ["password"] = c.Password,
        ["method"] = c.Method,
        ["alterId"] = c.AlterId,
        ["encryption"] = c.Encryption,
        ["flow"] = c.Flow,
        ["network"] = c.Network,
        ["security"] = c.Security,
        ["sni"] = c.Sni,
        ["publicKey"] = c.PublicKey,
        ["shortId"] = c.ShortId,
        ["fingerprint"] = c.Fingerprint,
        ["path"] = c.Path,
        ["host"] = c.Host,
        ["serviceName"] = c.ServiceName,
        ["mode"] = c.Mode,
        ["alpn"] = c.Alpn,
        ["headerType"] = c.HeaderType,
        ["privateKey"] = c.PrivateKey,
        ["mtu"] = c.Mtu,
        ["reserved"] = c.Reserved
    };

    private static ProxyConfig FromJson(JsonObject o) => new()
    {
        Name = S(o, "name"),
        Protocol = S(o, "protocol"),
        Address = S(o, "address"),
        Port = I(o, "port"),
        Uuid = S(o, "uuid"),
        Password = S(o, "password"),
        Method = S(o, "method"),
        AlterId = I(o, "alterId"),
        Encryption = S(o, "encryption", "none"),
        Flow = S(o, "flow"),
        Network = S(o, "network", "tcp"),
        Security = S(o, "security", "none"),
        Sni = S(o, "sni"),
        PublicKey = S(o, "publicKey"),
        ShortId = S(o, "shortId"),
        Fingerprint = S(o, "fingerprint", "chrome"),
        Path = S(o, "path"),
        Host = S(o, "host"),
        ServiceName = S(o, "serviceName"),
        Mode = S(o, "mode"),
        Alpn = S(o, "alpn"),
        HeaderType = S(o, "headerType", "none"),
        PrivateKey = S(o, "privateKey"),
        Mtu = I(o, "mtu", 1420),
        Reserved = S(o, "reserved")
    };

    public static string Sanitize(string name)
    {
        var sb = new StringBuilder();
        foreach (char ch in name.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '.' || ch == '_' || ch == '-')
                sb.Append(ch);
            else
                sb.Append('_');
        }
        string cleaned = sb.ToString();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "configs";
        if (cleaned.Length > 60) cleaned = cleaned.Substring(0, 60);
        return cleaned;
    }
}
