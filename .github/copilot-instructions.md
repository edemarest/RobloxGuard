# Project: RobloxGuard (Windows parental control for Roblox)

**Goal:** Per-user protocol shim + process watcher + optional topmost overlay to block specific Roblox `placeId`s.

## What to build
- **Primary:** Per-user protocol handler for `roblox-player:` to parse `%1` URI and allow/deny **before** Roblox launches.
- **Fallback:** Process watcher that terminates `RobloxPlayerBeta.exe` if its command line targets a blocked `placeId`.
- **Optional:** Non-injection overlay that tracks the Roblox window and hides content via a friendly Block UI.

## Non‑negotiables
- ❌ No DLL injection / graphics hooking. Out-of-process only.
- ✅ Windows-only, .NET 8, single-file publish. WPF accepted for UI.
- ✅ Per-user install (HKCU + `%LOCALAPPDATA%`), no admin.
- ✅ Clean uninstall: restore upstream `roblox-player` handler; remove scheduled task & app dir.

## Identifiers & parsing
We block on **placeId** (numeric). It may appear in:
- Protocol URI: `roblox://... ?placeId=12345` or embedded `...PlaceLauncher.ashx?...placeId=...`
- Client CLI: `--id 12345` or `--play -j https://assetgame.roblox.com/...placeId=12345`
Use these regexes (case-insensitive) and test fixtures in `docs/parsing_tests.md`:

- `/[?&]placeId=(\d+)/`
- `/PlaceLauncher\.ashx.*?[?&]placeId=(\d+)/`
- `/--id\s+(\d+)/`

## Runtime layout
- App dir: `%LOCALAPPDATA%\RobloxGuard\`
- Files: `RobloxGuard.exe` (single EXE), `config.json`, `logs/attempts.log`
- Config example:
```json
{
  "blocklist": [12345, 67890],
  "parentPINHash": "pbkdf2:...",
  "upstreamHandlerCommand": "C:\\Path\\To\\OriginalHandler \"%1\"",
  "overlayEnabled": true
}
```

## Registry & startup (per-user)
- Protocol: `HKCU\Software\Classes\roblox-player\shell\open\command = "<app>\RobloxGuard.exe" --handle-uri "%1"`
- Backup upstream: `HKCU\Software\RobloxGuard\Upstream`
- Scheduled Task (logon): runs `RobloxGuard.exe --watch` with restart on failure.

## App modes (single EXE)
- `--handle-uri "%1"`: parse URI; if blocked → show Block UI and **do not** forward; else spawn upstream handler with the **same** `%1`.
- `--watch`: subscribe to WMI `Win32_ProcessStartTrace`; on `RobloxPlayerBeta.exe`, parse placeId and block if needed (`WM_CLOSE` then kill). Show Block UI.
- `--ui`: Settings UI (set parent PIN, edit blocklist, view logs).
- `--install-first-run` (optional): perform registration & scheduled task creation; persist upstream handler.

## UX rules
- Protocol block: Roblox never starts; show Block UI immediately.
- Watcher block: attempt graceful close; create Block UI right away; force terminate if still alive ~700ms later.
- Block UI: explain why; show place name (resolved via API if online); provide “Back to favorites,” “Request unlock,” and PIN entry.

## CI/CD
- Build/test on PR + `main` push.
- Publish single-file, self-contained `win-x64` build.
- Package installer with Inno Setup.
- Compute SHA256 checksums.
- On tag `v*`, create a GitHub Release and upload EXE + installer + checksums.

## Issues/PR template (summary)
- Include acceptance criteria, manual test steps, and screenshots of the Block UI when relevant.

## Files Copilot should rely on
- `docs/protocol_behavior.md` — URI/CLI samples
- `docs/registry_map.md` — exact registry keys/values
- `docs/ux_specs.md` — UI wireframes + copy
- `docs/parsing_tests.md` — regex fixtures & edge cases
