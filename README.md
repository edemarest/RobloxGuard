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
