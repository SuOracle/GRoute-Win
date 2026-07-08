# libs — bundled native binaries

These third-party binaries are not committed. Download them and drop them here; the build copies this folder next to the app.

Required now (Phase 1, system proxy):

- `xray.exe` — Windows build of Xray-core. Download from the Xray-core releases (Xray-windows-64.zip), extract `xray.exe` here. Use the same core version you bundle on Android.

Required later (Phase 3, TUN):

- `wintun.dll` — from the official WinTun site (wintun.net). Use the amd64 build.
- `tun2socks.exe` — from xjasonlyu/tun2socks releases (Windows amd64).

Final layout after adding them:

```
libs/
  xray.exe
  wintun.dll        (Phase 3)
  tun2socks.exe     (Phase 3)
```
