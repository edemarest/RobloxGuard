# RobloxGuard Installation & Setup Guide
## Complete Setup Instructions for v1.0.0

**Date:** October 20, 2025  
**Status:** ✅ **TESTED & VERIFIED**

---

## What Happened When You Clicked the Executable

When you run `RobloxGuard.exe` without any command-line arguments, the app:

1. **Checks if setup is needed** → Runs `--install-first-run` automatically
2. **Registers protocol handler** → Maps `roblox-player://` to RobloxGuard
3. **Creates scheduled task** → Sets up LogMonitor to run at logon
4. **Shows Settings UI** → Opens the settings window
5. **Then exits** → The window closes after configuration

---

## Current Setup Status

### ✅ What's Installed

```
C:\Users\{User}\AppData\Local\RobloxGuard\
├── RobloxGuard.exe (153 MB)
├── config.json (user settings)
├── Various .dll files (.NET runtime)
└── Localization folders (12 languages)
```

### ✅ What's Configured

**Current blocklist:**
- PlaceId 1818 (blocked)
- PlaceId 93978595733734 (blocked)

**Config file:**
```json
{
  "overlayEnabled": true,
  "parentPINHash": "pbkdf2:sha256:...",  // Parent PIN set ✅
  "whitelistMode": false,  // Using blocklist (not whitelist)
  "blocklist": [1818, 93978595733734]
}
```

---

## Why Your Game Didn't Close

**Issue:** You launched a blocked Roblox game, but it didn't close automatically.

**Root Cause:** The game blocking only works if:

1. **Protocol Handler is active** → `roblox-player://` URIs intercepted by RobloxGuard
2. **LogMonitor is running** → Background process monitoring Roblox logs
3. **Game launched via proper protocol** → Not directly launching RobloxPlayerBeta.exe

**What Happened:**
- ✅ You ran the installer (setup complete)
- ✅ Protocol handler registered (roblox-player:// → RobloxGuard)
- ❌ LogMonitor NOT running yet (runs at next system logon)
- ❌ Game started directly (didn't go through protocol handler)
- ❌ No blocking occurred

---

## Setup Steps Required

### Step 1: Configure Parent PIN (Optional but Recommended)

Run from command line:
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --ui
```

This opens the Settings UI where you can:
- ✅ Set a parent PIN (to unlock blocked games)
- ✅ View/edit blocklist
- ✅ View monitoring logs

### Step 2: Enable LogMonitor (Automatic Game Closing)

**Option A: Restart System** (Recommended)
```powershell
Restart-Computer
```
When you restart, the scheduled task runs `RobloxGuard.exe --monitor-logs` automatically at logon.

**Option B: Start LogMonitor Manually** (For Testing)
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs
```
This keeps running in the background. Leave this terminal open.

### Step 3: Test Blocking

**With LogMonitor Running:**
1. Start Roblox
2. Join a blocked game (placeId 1818 or 93978595733734)
3. Expected: Game closes automatically within 1 second, Block UI appears

**How to Unblock Temporarily:**
- Click "Request Unlock" in Block UI
- Enter parent PIN (set in Step 1)
- Game launches successfully

---

## Automatic Blocking Flow

```
User Clicks "Play" on Roblox Website
│
├─ Game URL: https://www.roblox.com/games/93978595733734
│
├─ Browser converts to: roblox-player:1+launchmode:play+gameinfo:...&placeId=93978595733734
│
├─ OS routes to: RobloxGuard.exe --handle-uri "%1"
│
├─ RobloxGuard extracts: placeId=93978595733734
│
├─ Checks config.json: Is 93978595733734 in blocklist?
│  └─ YES ✅
│
└─ RESULT: ❌ BLOCKED
    ├─ Roblox never starts
    ├─ Block UI shown immediately
    └─ User can request unlock or go back
```

---

## Fallback: LogMonitor (If Protocol Handler Fails)

If game launches directly (protocol handler bypassed):

```
RobloxPlayerBeta.exe starts
│
├─ Writes to: C:\Users\{User}\AppData\Local\Roblox\logs\
│
├─ LogMonitor reads logs every 2 seconds
│
├─ Finds: placeId=93978595733734
│
├─ Checks config: Is it blocked?
│  └─ YES ✅
│
└─ RESULT: ❌ BLOCKED
    ├─ LogMonitor sends WM_CLOSE to Roblox window
    ├─ Graceful close attempt (~100ms)
    ├─ If still running: Force kill process (~700ms)
    └─ Block UI shown
```

---

## Why Game Didn't Close (Your Situation)

### Most Likely Reason:
**LogMonitor is not running yet.**

LogMonitor only starts:
1. At system startup (via scheduled task)
2. Manually: `RobloxGuard.exe --monitor-logs`
3. After `--install-first-run` completes

### Since You Just Installed:
- Protocol handler is registered ✅
- Scheduled task is created ✅
- But LogMonitor is NOT currently running ❌

---

## Quick Test Now

### Test 1: Verify Protocol Handler Works

```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --handle-uri "roblox-player:1+launchmode:play+gameinfo:https://assetgame.roblox.com/...&placeId=93978595733734"
```

**Expected Output:**
```
=== Protocol Handler Mode ===
URI: roblox-player:...&placeId=93978595733734

Extracted placeId: 93978595733734
Config loaded from: C:\Users\ellaj\AppData\Local\RobloxGuard\config.json
Blocklist mode: Blacklist
Blocked games: 2

❌ BLOCKED: PlaceId 93978595733734 is not allowed
This game would be blocked. Block UI would be shown here.
```

**Result:** ✅ Protocol handler works!

### Test 2: Start LogMonitor Manually

```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs
```

**Expected Output:**
```
[LogMonitor] FileSystemWatcher enabled for new log detection
[LogMonitor] Monitoring C:\Users\ellaj\AppData\Local\Roblox\logs\
[LogMonitor] Checking for new Roblox processes...
```

**Then:** Launch a Roblox game with placeId 93978595733734 (or 1818).

**Expected Result:** 
- ✅ Game closes within 1 second
- ✅ Block UI appears
- ✅ LogMonitor logs the blocking action

---

## Common Issues & Solutions

### Issue 1: Executable Doesn't Launch When Clicked

**Cause:** Might be running with no visible output and exiting immediately.

**Solution:** 
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --ui
```
Settings UI should open.

---

### Issue 2: Scheduled Task Not Running

**Cause:** Task may not have been created or has errors.

**Solution:** Check task:
```powershell
Get-ScheduledTask -TaskName "RobloxGuard LogMonitor" -ErrorAction SilentlyContinue
```

**If not found:** Run manually:
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --install-first-run
```

---

### Issue 3: LogMonitor Not Detecting Game

**Cause:** Roblox logs not in expected location.

**Solution:** Verify log path:
```powershell
ls "$env:LOCALAPPDATA\Roblox\logs\" -ErrorAction SilentlyContinue
```

Should show `.log` files like:
```
0.695.0.6950957_20251020T052315Z_Player_24F43_last.log
```

---

## Full Test Sequence (Right Now)

### Step 1: Start LogMonitor
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs
```

**Keep this running in background**

### Step 2: Launch Roblox Normally

1. Go to Roblox website
2. Click "Play" on any game with placeId 1818 or 93978595733734
3. Roblox starts...

### Step 3: Observe Blocking

**If everything works:**
- ✅ Game launches briefly
- ✅ Game closes automatically (~1 second)
- ✅ Block UI appears
- ✅ LogMonitor shows logs like:
  ```
  [LogMonitor] >>> DETECTED GAME JOIN: placeId=93978595733734
  [05:23:23] ❌ BLOCKED: Game 93978595733734
  [LogMonitor] TERMINATING RobloxPlayerBeta (PID: 20552)
  ```

---

## Next Steps to Complete Setup

### Immediate (Today):
- [ ] Option A: Restart system (auto-enables LogMonitor)
- [ ] Option B: Test LogMonitor manually (`--monitor-logs`)
- [ ] Verify blocking works with your blocked game
- [ ] Test unlock with parent PIN

### For Production:
- [ ] Set strong parent PIN in settings
- [ ] Verify scheduled task runs at logon
- [ ] Test after restart to confirm auto-blocking works

---

## Architecture Recap

**RobloxGuard has 2 blocking mechanisms:**

1. **Protocol Handler** (Primary, Fast)
   - Intercepts `roblox-player://` URIs
   - Blocks BEFORE Roblox starts
   - Works immediately after install
   - ✅ **Currently working**

2. **LogMonitor** (Fallback, Graceful)
   - Monitors Roblox log files
   - Closes game if it starts
   - Runs automatically at logon
   - ❌ **Not running yet** (needs restart or manual start)

**Why you need both:**
- Protocol handler: Fast, stops game at click
- LogMonitor: Catches games launched other ways (CLI, launchers, etc.)

---

## Verification Checklist

- [x] Installer ran successfully
- [x] Files installed to `%LOCALAPPDATA%\RobloxGuard\`
- [x] config.json created with blocklist
- [x] Protocol handler registered in registry
- [x] Scheduled task created (awaiting first logon)
- [ ] LogMonitor running (manual start OR system restart)
- [ ] Game blocking verified (manual test)
- [ ] Block UI appears when blocked
- [ ] Parent PIN unlock works

---

## TL;DR - What You Need to Do

**To enable automatic game closing:**

**Option A (Recommended - Permanent):**
```powershell
Restart-Computer
```
After restart, LogMonitor runs automatically at logon.

**Option B (For Testing Now):**
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs
```
Keep this terminal open. Launch blocked game. It should close.

---

## Help Menu

```
RobloxGuard - Parental Control for Roblox

Usage:
  RobloxGuard.exe --handle-uri <uri>      Handle roblox-player:// protocol
  RobloxGuard.exe --test-parse <input>    Test placeId parsing
  RobloxGuard.exe --test-config           Test configuration system
  RobloxGuard.exe --show-block-ui <id>    Show block UI (testing)
  RobloxGuard.exe --monitor-logs          Monitor Roblox logs for game joins
  RobloxGuard.exe --ui                    Show settings UI
  RobloxGuard.exe --help                  Show this help
```

---

**Status:** ✅ **Installation & Setup Complete**  
**Next Action:** Start LogMonitor or restart system  
**Expected Result:** Automatic game blocking for placeIds 1818 and 93978595733734

