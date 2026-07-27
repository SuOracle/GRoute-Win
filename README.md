<p align="center">
  <img src="Docs/GRoute%20Transparent%20Black.png" width="220" alt="GRoute">
</p>

# GRoute for Windows (جی‌روت)

A Windows VPN client built on [Xray-core](https://github.com/XTLS/Xray-core) for bypassing internet restrictions, with covering many of the free features for accessing to the free internet and the UI created by Avalonia in English, Persian, Russian, and Chinese.

*GRoute — access to the free internet with just one click*

The Android client lives at [SuOracle/GRoute](https://github.com/SuOracle/GRoute).

## Features

- **Multiple engines** — Xray for VLESS, VMess, Trojan, Shadowsocks, and WireGuard; sing-box for Hysteria2; Aether for WARP over MASQUE.
- **Free projects intended to bypass censorship** — Included with [Aether Project](https://github.com/CluvexStudio/Aether), Classic WARP and Tor to hand the user a way to connect to the internet at all locations for free.
- **Modern transports** — REALITY, TLS, WebSocket, gRPC, HTTPUpgrade, XHTTP, and plain TCP.
- **Three routing modes** — local proxy only, system proxy, or full **TUN** mode that routes every application through the tunnel.
- **Encrypted DNS** — resolves domains over DoH inside the tunnel instead of your system resolver, with domestic domains kept on the local resolver when split routing is on.
- **Kill switch** — blocks all outbound traffic if the tunnel drops unexpectedly, so nothing leaks while you are unprotected.
- **Split routing** — Iranian and local traffic goes direct, outside the tunnel.
- **TLS fragmentation (anti-DPI)** — splits the TLS handshake to slip past SNI filtering.
- **Tor for `.onion`** — reach onion services with Tor running inside your tunnel.
- **Tor locations** — run a country-pinned Tor exit locally and use it as a server, so you can browse as though you were somewhere else. Slow, but it works when little else does.
- **Cloudflare WARP** — register an account and scan for endpoints that respond on your network.
- **Cloudflare clean-IP scanner** — samples Cloudflare's ranges and ranks the reachable ones by latency.
- **Subscriptions** — import every server from a subscription link, with auto-refresh and remaining-data display.
- **Internet quality test** — download, upload, ping, jitter, and idle/loaded latency, with a rating for gaming, browsing, streaming, and calls.
- **Share as file** — export selected servers to an encrypted `.grt` file, optionally password-protected, with a lock flag that hides everything but the server name.
- **Usage history** — live speed, session totals, and hourly/daily/custom ranges, with off-VPN traffic tracked separately.
- **Four languages** — English, Persian, Russian, and Chinese, with right-to-left layout for Persian.

## Download

Grab the latest installer or portable zip from the [Releases](https://github.com/SuOracle/GRoute-Win/releases) page.

Every release ships a `SHA256SUMS.txt`. **Verify what you downloaded before running it** — this is a censorship-circumvention tool, and a tampered build is the worst thing that could happen to you:

```powershell
certutil -hashfile GRoute-2.0.0-setup.exe SHA256
```

Compare the result with the line in `SHA256SUMS.txt`.

GRoute is not code-signed yet, so SmartScreen will warn on first run. That warning is expected for an unsigned installer; verify the checksum rather than relying on the prompt.

**Windows 10 version 1809 or later, 64-bit.** No .NET runtime needed — the build is self-contained.

## How to use

**1. Add servers.** Open the server list from the card on the main screen. You can paste config links (`vless://`, `vmess://`, `trojan://`, `ss://`) one per line, paste a subscription URL, import a `.grt` or WireGuard `.conf` file, or add a server manually field by field.

**2. Pick a server.** Tap one to select it. **Test all** pings every server; sort by fastest to bring the quickest to the top.

**3. Choose a routing mode.** The button at the top-left of the main screen switches between:

- **Local proxy** — GRoute listens on `127.0.0.1` and nothing else changes. Point individual apps at it.
- **System proxy** — the above, plus Windows is told to use it. Covers most browsers and proxy-aware apps.
- **TUN** — creates a virtual network adapter and routes *everything* through the tunnel, including apps that ignore proxy settings. **Requires administrator rights**; GRoute offers to restart elevated.

**4. Connect.** Press the power button. You will see live speed, session total, and your apparent location on the globe.

**5. Tune it (Connection settings).**

- **Split routing** — keeps Iranian traffic outside the tunnel.
- **Fragment (anti-DPI)** — splits the TLS hello to slip past filtering.
- **Sniffing** — routes by the domain read from traffic rather than the raw IP.
- **Mux** — multiplexes connections; leave it off with REALITY/Vision.
- **Kill switch** — blocks all traffic when the tunnel drops. Needs administrator rights.
- **Auto-select fastest** — tests servers every minute and switches to the best.

**6. Tools.** Ad and tracker blocking, encrypted DNS, Tor for `.onion` addresses, the Cloudflare scanner, and the internet quality test.

**7. Free projects.** One-tap setup for Aether (WARP over MASQUE), classic WARP, and Tor locations.

## Building from source

```bash
git clone https://github.com/SuOracle/GRoute-Win
cd GRoute-Win
dotnet publish src/GRoute.Desktop -c Release
```

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). Output lands in `src/GRoute.Desktop/bin/Release/net10.0/win-x64/publish`.

**Engine binaries are not committed to this repository.** They are large third-party downloads, and shipping someone else's compiled binary through a mirror is exactly the supply-chain risk our users cannot afford. Download them yourself and put them in `src/GRoute.Desktop/libs/`. The build fails with a clear message if any are missing.

To build the installer, install [Inno Setup 6](https://jrsoftware.org/isdl.php) and run:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" src\GRoute.Desktop\GRoute.iss
```

## Project layout

```
src/
  GRoute.Core/      engine control, config generation, storage — no UI dependencies
  GRoute.Desktop/   Avalonia UI, Windows platform services
libs/               third-party engine binaries (not committed)
```

`GRoute.Core` is deliberately free of any UI framework reference, with platform-specific work behind the interfaces in `PlatformServices.cs`.

## Tech stack

C# · .NET 10 · Avalonia 11 · FluentAvaloniaUI · Xray-core · sing-box · tun2socks · wintun · Tor.

## Privacy

GRoute has no accounts, analytics, advertising, or tracking. Server configurations and usage statistics stay on your machine and are never transmitted.

Two things worth being precise about:

- A `.grt` file exported **without** a password is obfuscated, not securely encrypted. Use a password for anything you would mind a stranger reading.
- **Tor mode gives you reachability, not anonymity.** Traffic still exits through an ordinary browser with ordinary fingerprinting. For anything where anonymity genuinely matters, use Tor Browser.

## Support

Questions or issues? Reach the developer on Telegram at [@OracleVPNsupport](https://t.me/OracleVPNsupport).

## License

[GPL-3.0](LICENSE). Bundled engines remain under their own licenses — Xray-core under MPL-2.0, sing-box under GPL-3.0, Tor under BSD-3-Clause.

Developed by Oracle VPN.
