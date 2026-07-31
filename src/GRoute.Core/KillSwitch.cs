using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GRoute.Windows.Core;

public static class KillSwitch
{
    private const string RulePrefix = "GRoute Kill Switch";

    private static readonly string[] AllowedBinaries =
    {
        "xray",
        "sing-box",
        "aether",
        "tor",
        "tun2socks-windows-amd64"
    };

    public static bool Engaged { get; private set; }

    public static void Engage()
    {
        if (Engaged) return;
        NetGuard.MarkDirty("killswitch");
        Clear();

        int i = 0;
        foreach (var name in AllowedBinaries)
        {
            string path = Bin.Path(name);
            if (!File.Exists(path)) continue;
            RunNetsh($"advfirewall firewall add rule name=\"{RulePrefix} Allow {i}\" " +
                     $"dir=out action=allow enable=yes profile=any program=\"{path}\"");
            i++;
        }

        string self = Environment.ProcessPath ?? "";
        if (self.Length > 0)
        {
            RunNetsh($"advfirewall firewall add rule name=\"{RulePrefix} Allow {i}\" " +
                     $"dir=out action=allow enable=yes profile=any program=\"{self}\"");
            i++;
        }

        RunNetsh($"advfirewall firewall add rule name=\"{RulePrefix} Loopback\" " +
                 "dir=out action=allow enable=yes profile=any remoteip=127.0.0.1,::1");
        RunNetsh($"advfirewall firewall add rule name=\"{RulePrefix} LocalNet\" " +
                 "dir=out action=allow enable=yes profile=any remoteip=LocalSubnet protocol=udp localport=68 remoteport=67");

        RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound");
        Engaged = true;
    }

    public static void Release()
    {
        RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound");
        Clear();
        Engaged = false;
        NetGuard.MarkClean("killswitch");
    }

    public static void ReleaseIfOrphaned()
    {
        if (Engaged) return;
        Release();
    }

    private static void Clear()
    {
        for (int i = 0; i < AllowedBinaries.Length + 2; i++)
            RunNetsh($"advfirewall firewall delete rule name=\"{RulePrefix} Allow {i}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{RulePrefix} Loopback\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{RulePrefix} LocalNet\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{RulePrefix}\"");
    }

    private static void RunNetsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(6000);
        }
        catch
        {
        }
    }
}
