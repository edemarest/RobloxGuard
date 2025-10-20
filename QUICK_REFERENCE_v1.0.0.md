# RobloxGuard v1.0.0 - QUICK REFERENCE

## Installation

```powershell
# Option 1: Installer (Easiest)
# Download: RobloxGuardInstaller.exe from GitHub Releases
# Double-click it
# Done - Files installed to: %LOCALAPPDATA%\RobloxGuard\

# Option 2: Manual
"C:\Path\To\RobloxGuard.exe" --install-first-run
```

---

## To Test Blocking

### Terminal 1: Start Monitor
```powershell
cd "$env:LOCALAPPDATA\RobloxGuard"
.\RobloxGuard.exe --monitor-logs

# Output:
# === Log Monitor Mode ===
# Monitoring Roblox player logs for game joins...
# Press Ctrl+C to stop
```

### Terminal 2: Launch Game
```powershell
# Go to https://www.roblox.com
# Click any game in your blocklist
# OR simulate:
.\RobloxGuard.exe --handle-uri "roblox://placeId=920587237"
```

### Terminal 1: Watch It Get Blocked
```
>>> GAME DETECTED: 920587237
[12:34:59] ❌ BLOCKED: Game 920587237
[LogMonitor] TERMINATING RobloxPlayerBeta (PID: 12340)
[LogMonitor] Successfully terminated process 12340
```

---

## UI Modes

```powershell
# Show Settings
.\RobloxGuard.exe --ui

# Show Block Window (for testing)
.\RobloxGuard.exe --show-block-ui 920587237

# Show Protocol Handler (for testing)
.\RobloxGuard.exe --handle-uri "roblox://placeId=920587237"

# Uninstall
.\RobloxGuard.exe --uninstall
```

---

## Current v1.0.0 Status

| Feature | Status | Notes |
|---------|--------|-------|
| **Block web clicks** | ✅ Fast | Intercepts protocol immediately |
| **Block app launches** | ✅ Fast | LogMonitor catches within 1-2 sec |
| **Block teleports** | ✅ Fast | LogMonitor detects and terminates |
| **Show Block UI** | ✅ | Professional WPF window with game name |
| **PIN verification** | ✅ | PBKDF2 hashing, salted |
| **Settings UI** | ✅ | Manage blocklist, set PIN |
| **Auto-start monitor** | ❌ | Manual only - must run `--monitor-logs` |
| **Tray icon** | ❌ | No status indicator yet |
| **Setup wizard** | ❌ | CLI install only |
| **Time-limited unlocks** | ❌ | PIN unlocks permanently |

---

## File Locations

```
Installation: %LOCALAPPDATA%\RobloxGuard\
├── RobloxGuard.exe (153.5 MB, self-contained)
├── config.json (settings & blocklist)
└── logs/ (optional, for log review)

Config: %LOCALAPPDATA%\RobloxGuard\config.json
Logs: %LOCALAPPDATA%\Roblox\logs\ (Roblox player logs)

Uninstall: Delete %LOCALAPPDATA%\RobloxGuard\ folder
Registry: HKCU\Software\Classes\roblox-player\ (restored on uninstall)
```

---

## Troubleshooting

### Game isn't blocking?
1. Check: Is LogMonitor running?
   - Terminal should show: `Monitoring Roblox player logs...`
2. Check: Is game placeId in blocklist?
   - Open Settings: `.\RobloxGuard.exe --ui`
3. Check: Did LogMonitor detect the game?
   - Terminal should show: `>>> GAME DETECTED: xxxxx`

### Block UI doesn't appear?
1. Restart LogMonitor
2. Try test: `.\RobloxGuard.exe --show-block-ui 920587237`
3. Check if game actually launched

### Monitor crashes?
1. Check: Logs writable? `%LOCALAPPDATA%\RobloxGuard\logs\`
2. Try: Restart with `--monitor-logs`
3. Check: Is another copy running?

### How to uninstall?
```powershell
# Method 1: Via UI
.\RobloxGuard.exe --ui
# Click [UNINSTALL] button

# Method 2: Via CLI
.\RobloxGuard.exe --uninstall

# Method 3: Manual
# Delete: %LOCALAPPDATA%\RobloxGuard\ folder
# Registry is auto-restored on uninstall
```

---

## Next Release (v2.0)

Expected improvements:
- ✅ Auto-start monitor on install
- ✅ Tray icon with status
- ✅ Setup wizard
- ✅ Time-limited unlocks
- ✅ Better block UI
- ✅ Game name search

All blocking features (100% coverage) already in v1.0.0 ✅

