using System;
using System.Security.Cryptography;

namespace GRoute.Windows.Core;

public static class Curve25519
{
    private static void Car25519(long[] o)
    {
        for (int i = 0; i < 16; i++)
        {
            o[i] += 1L << 16;
            long c = o[i] >> 16;
            o[(i + 1) * (i < 15 ? 1 : 0)] += c - 1 + 37 * (c - 1) * (i == 15 ? 1L : 0L);
            o[i] -= c << 16;
        }
    }

    private static void Sel25519(long[] p, long[] q, int b)
    {
        long c = ~((long)b - 1L);
        for (int i = 0; i < 16; i++)
        {
            long t = c & (p[i] ^ q[i]);
            p[i] ^= t;
            q[i] ^= t;
        }
    }

    private static byte[] Pack25519(long[] n)
    {
        long[] t = (long[])n.Clone();
        Car25519(t); Car25519(t); Car25519(t);
        for (int j = 0; j < 2; j++)
        {
            long[] m = new long[16];
            m[0] = t[0] - 0xffed;
            for (int i = 1; i < 15; i++)
            {
                m[i] = t[i] - 0xffff - ((m[i - 1] >> 16) & 1);
                m[i - 1] &= 0xffff;
            }
            m[15] = t[15] - 0x7fff - ((m[14] >> 16) & 1);
            int b = (int)((m[15] >> 16) & 1);
            m[14] &= 0xffff;
            Sel25519(t, m, 1 - b);
        }
        byte[] o = new byte[32];
        for (int i = 0; i < 16; i++)
        {
            o[2 * i] = (byte)(t[i] & 0xff);
            o[2 * i + 1] = (byte)((t[i] >> 8) & 0xff);
        }
        return o;
    }

    private static long[] Unpack25519(byte[] n)
    {
        long[] o = new long[16];
        for (int i = 0; i < 16; i++)
            o[i] = n[2 * i] + ((long)n[2 * i + 1] << 8);
        o[15] &= 0x7fff;
        return o;
    }

    private static long[] Add(long[] a, long[] b)
    {
        long[] o = new long[16];
        for (int i = 0; i < 16; i++) o[i] = a[i] + b[i];
        return o;
    }

    private static long[] Sub(long[] a, long[] b)
    {
        long[] o = new long[16];
        for (int i = 0; i < 16; i++) o[i] = a[i] - b[i];
        return o;
    }

    private static long[] Mul(long[] a, long[] b)
    {
        long[] t = new long[31];
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 16; j++)
                t[i + j] += a[i] * b[j];
        for (int i = 0; i < 15; i++) t[i] += 38 * t[i + 16];
        long[] o = new long[16];
        Array.Copy(t, o, 16);
        Car25519(o); Car25519(o);
        return o;
    }

    private static long[] Sqr(long[] a) => Mul(a, a);

    private static long[] Inv25519(long[] i)
    {
        long[] c = (long[])i.Clone();
        for (int a = 253; a >= 0; a--)
        {
            c = Sqr(c);
            if (a != 2 && a != 4) c = Mul(c, i);
        }
        return c;
    }

    private static readonly long[] N121665 = BuildN121665();

    private static long[] BuildN121665()
    {
        long[] v = new long[16];
        v[0] = 0xDB41L;
        v[1] = 1L;
        return v;
    }

    private static byte[] ScalarMult(byte[] n, byte[] p)
    {
        byte[] z = new byte[32];
        Array.Copy(n, z, Math.Min(n.Length, 32));
        z[31] = (byte)((z[31] & 127) | 64);
        z[0] = (byte)(z[0] & 248);

        long[] x = Unpack25519(p);
        long[] a = new long[16];
        long[] b = (long[])x.Clone();
        long[] c = new long[16];
        long[] d = new long[16];
        a[0] = 1L;
        d[0] = 1L;

        for (int i = 254; i >= 0; i--)
        {
            int r = ((z[i >> 3] & 0xff) >> (i & 7)) & 1;
            Sel25519(a, b, r); Sel25519(c, d, r);
            long[] e = Add(a, c); a = Sub(a, c); c = Add(b, d); b = Sub(b, d);
            d = Sqr(e); long[] f = Sqr(a); a = Mul(c, a); c = Mul(b, e); e = Add(a, c); a = Sub(a, c);
            b = Sqr(a); c = Sub(d, f); a = Mul(c, N121665); a = Add(a, d); c = Mul(c, a); a = Mul(d, f); d = Mul(b, x); b = Sqr(e);
            Sel25519(a, b, r); Sel25519(c, d, r);
        }

        c = Inv25519(c);
        a = Mul(a, c);
        return Pack25519(a);
    }

    private static byte[] ScalarMultBase(byte[] n)
    {
        byte[] nine = new byte[32];
        nine[0] = 9;
        return ScalarMult(n, nine);
    }

    public static (string PrivateKey, string PublicKey) GenerateKeyPair()
    {
        byte[] priv = new byte[32];
        RandomNumberGenerator.Fill(priv);
        priv[0] = (byte)(priv[0] & 248);
        priv[31] = (byte)((priv[31] & 127) | 64);
        byte[] pub = ScalarMultBase(priv);
        return (Convert.ToBase64String(priv), Convert.ToBase64String(pub));
    }
}
