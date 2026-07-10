# GRoute for Windows

A Windows desktop client for GRoute, built in C# / WPF on .NET 8. It controls a bundled `xray.exe` core and routes traffic through it, mirroring the Android app.

This is **Phase 1** of a phased build.

## Status

- **Phase 1 (this) — skeleton + system proxy.** Solution, project, tray + main window, Xray process controller, and Windows system-proxy plumbing. Connecting launches `xray.exe` under app control and points the Windows system proxy at it, end to end.
- **Phase 2 — config port.** `ProxyConfig`, `ConfigParser`, `ConfigBuilder`, `Subscription`, `SubscriptionFetcher` ported from the Kotlin app, so you can paste a link or subscription and connect to a real server.
- **Phase 3 — TUN mode.** `wintun.dll` + `tun2socks.exe`, TUN adapter creation, route management, and UAC self-elevation for full-device VPN.
- **Phase 4 — WiX MSI installer** bundling the app and native binaries.

## Requirements

- .NET 8 SDK
- Visual Studio 2022 (17.8+) or `dotnet` CLI
- Windows 10 1809+ / Windows 11

## Getting started

1. Add `xray.exe` to `src/GRoute.Windows/libs/` (see `libs/README.md`).
2. Open `GRoute.Windows.sln` in Visual Studio, or run:
   ```
   dotnet run --project src/GRoute.Windows
   ```
3. Leave the mode on **System Proxy**, click **Connect**. Xray starts, the system proxy is set to `127.0.0.1:10809`, and the log panel shows Xray output. Click **Disconnect** to stop and clear the proxy.

Closing the window minimises to the tray; the app keeps running. Right-click the tray icon and choose **Exit** to quit fully (which disconnects and clears the system proxy).

## Notes

- Phase 1 ships a minimal test config (SOCKS on 10626, HTTP on 10809, direct outbound) purely to verify the control path. Phase 2 replaces it with configs built from your real servers.
- **TUN** is present in the UI but disabled until Phase 3; it needs admin rights, which the app will request by self-elevating at that point.
- The app runs `asInvoker` (no admin) for the system-proxy path, which writes only to the current user's proxy settings.

## Project layout

```
GRoute.Windows.sln
src/GRoute.Windows/
  GRoute.Windows.csproj
  app.manifest
  App.xaml(.cs)
  MainWindow.xaml(.cs)
  Core/
    Enums.cs
    XrayController.cs
    SystemProxy.cs
    TunController.cs      (stub until Phase 3)
    TestConfig.cs
  libs/                   (drop xray.exe here)
```
