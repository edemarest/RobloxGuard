# RobloxGuard

Windows parental control for Roblox. Blocks specific games by `placeId`.

## Installation

1. Download `RobloxGuard.exe` from [releases](https://github.com/edemarest/RobloxGuard/releases)
2. Double-click to run (auto-installs on first run)
3. Edit `%LOCALAPPDATA%\RobloxGuard\config.json` to change blocked games

Done! The log monitor runs automatically in the background.

## Commands

Run from PowerShell in the Desktop folder where `RobloxGuard.exe` is located:

```powershell
# First run (auto-starts monitoring)
.\RobloxGuard.exe

# Enable pre-launch blocking (optional)
.\RobloxGuard.exe --register-protocol

# Uninstall (removes app, restores original handler)
.\RobloxGuard.exe --uninstall
```

**Note:** 
- Use `.\` prefix in PowerShell (required in current directory)
- Use the command line to uninstall, not Windows Settings
- Settings may show an error about a missing uninstaller file (ignore it)

## How It Works

- **Log Monitor** (always): Watches Roblox logs and terminates blocked games automatically
- **Alert Window**: Shows red "🧠❌ BRAINDEAD CONTENT DETECTED" for 20 seconds when a game is blocked
- **Config**: Edit `%LOCALAPPDATA%\RobloxGuard\config.json` to manage blocked games

Example config:

```json
{
  "blockedGames": [
    {"placeId": 15532962292, "name": "BRAINDEAD CONTENT DETECTED"}
  ],
  "overlayEnabled": true,
  "whitelistMode": false
}
```

## Features

- 🎯 Simple JSON config (no UI needed)
- 🚫 Blocks by game ID (placeId)
- 📝 Pre-configured with sensible defaults
- 💻 Single-file EXE (154 MB, no dependencies)
- 🔒 Per-user install (no admin needed)

## Troubleshooting

### Monitor stops after game is blocked

If the monitor shuts down silently after detecting a blocked game:

1. **Check the debug log**:
   ```
   %LOCALAPPDATA%\RobloxGuard\launcher.log
   ```
   This file contains detailed diagnostic information about what happened.

2. **Look for specific errors**:
   - `[LogMonitor] DETECTED GAME JOIN: placeId=...` — Game was detected ✓
   - `[LogMonitor] Game X IS BLOCKED` — Game was marked as blocked ✓
   - `[LogMonitor.TerminateRobloxProcess]` — Process termination logs
   - `[AlertWindow]` — Alert window display logs
   - `CRITICAL ERROR` — If you see this, include it in a bug report

3. **Common issues**:
   - **Alert window fails to appear**: Check `[AlertWindow]` entries in launcher.log
   - **Monitor crashes after game detected**: Look for stack traces after `EXCEPTION:` in logs
   - **Game wasn't terminated**: Check `[LogMonitor.TerminateRobloxProcess]` logs to see if process was found

### Getting help

If the monitor stops unexpectedly:
1. Copy the content of `%LOCALAPPDATA%\RobloxGuard\launcher.log`
2. Open a GitHub issue with:
   - The log content
   - The blocked game's `placeId`
   - What you saw (did the alert appear? did the game close?)

`````
