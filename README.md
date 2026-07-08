<div align="center">

# GRoute for Windows

**A fast, private VPN client for Windows — built on [Xray-core](https://github.com/XTLS/Xray-core).**

Split routing, WireGuard, and a clean native desktop UI.


[**Download**](https://github.com/SuOracle/GRoute-Win/releases/latest/download/GRoute-Setup.exe) · [Report a bug](https://github.com/SuOracle/GRoute-Win/issues) · [Releases](https://github.com/SuOracle/GRoute-Win/releases)

</div>

---

## Overview

GRoute is a lightweight desktop VPN client that helps you pass through network restrictions. It wraps the Xray-core engine in a native Windows interface with a one-tap connect experience, per-app-free split routing that keeps local traffic direct, and a set of built-in tools for finding and testing fast servers.

## Features

- **Multiple protocols** — VLESS, VMess, Trojan, Shadowsocks, WireGuard, and HTTP/SOCKS.
- **Two connection modes** — System proxy or full TUN (system-wide) tunneling.
- **Split routing** — send Iran-bound traffic directly while everything else goes through the tunnel.
- **Fragment (anti-DPI)** and configurable **traffic sniffing** (TLS / HTTP / QUIC / FakeDNS).
- **Cloudflare clean-IP scanner** — find responsive Cloudflare edges and apply them to your config.
- **Internet quality test** — download, upload, idle/loaded ping, jitter, and an overall rating.
- **Usage stats** — hourly and daily charts, per-config totals, and your most-used server.
- **System tray** — quick connect/disconnect and connection-mode switching from the notification area.
- **Built-in updater** — checks GitHub for new versions.
- **Bilingual** — English and Persian (فارسی) with full RTL support.

## Requirements

- Windows 10 or 11 (64-bit)
- Administrator rights (required for the TUN adapter and system-proxy changes)

The installer is self-contained — no separate .NET runtime installation is needed.

## Installation

1. Download **`GRoute-Setup.exe`** from the [latest release](https://github.com/SuOracle/GRoute-Win/releases/latest).
2. Run the installer and follow the prompts.
3. Launch GRoute and add a server (paste a config link, import from clipboard, or add one manually).

> **First-run SmartScreen warning:** GRoute isn't code-signed yet, so Windows may show a blue **"Windows protected your PC"** screen. Click **More info → Run anyway** to continue. This is expected for new, unsigned apps and does not indicate a problem with the download.

## Usage

- **Connect:** pick a server from the list and press the power button on the home screen.
- **Connection mode:** tap the mode selector on the home screen (or the tray icon) to switch between Just Proxy, System Proxy, and VPN Tunnel.
- **Add servers:** paste a `vless://`, `vmess://`, `trojan://`, `ss://`, or `wireguard://` link, subscribe to a URL, or fill in the manual form.
- **Tools:** open the Tools tab for the Cloudflare scanner and the internet-quality test.
- **Settings:** adjust split routing, fragment, sniffing, mixed port, and log level under Connection Settings.

## Building from source

```bash
# Requires the .NET 8 SDK
git clone https://github.com/SuOracle/GRoute-Win.git
cd GRoute-Win/src/GRoute.Windows

# Run in development
dotnet run

# Produce a distributable, self-contained build
dotnet publish GRoute_Windows.csproj -c Release -r win-x64 --self-contained true -o bin/Release/net8.0-windows/win-x64/publish
```

The installer is built from `GRoute.iss` with [Inno Setup](https://jrsoftware.org/isinfo.php).

## Tech stack

- **.NET 8** · **WPF** (C#) for the desktop UI
- **[Xray-core](https://github.com/XTLS/Xray-core)** as the proxy engine
- **Inno Setup** for packaging

## Updates

GRoute checks for new versions from within the app (menu → **Check for updates**). When an update is available, it links to the latest release here on GitHub.

## Disclaimer

GRoute is a tool for protecting privacy and accessing an open internet. You are responsible for complying with the laws and regulations that apply to you. The developers provide this software as-is, without warranty of any kind.

## License

See the [LICENSE](LICENSE) file for details.

---

<div align="center">
Made with care for a free and open internet.
</div>