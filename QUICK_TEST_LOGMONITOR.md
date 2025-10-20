# Quick Start: Test LogMonitor (10 minutes)

## Prerequisites
- ✅ Build complete
- ✅ Installed to `%LOCALAPPDATA%\RobloxGuard`
- ✅ Roblox app installed on system

---

## Step 1: Close Roblox Completely (1 min)

```powershell
# Force close any Roblox processes
Get-Process Roblox* -ErrorAction SilentlyContinue | Stop-Process -Force

# Verify they're gone
Get-Process RobloxPlayerBeta -ErrorAction SilentlyContinue
# Should return nothing
```

---

## Step 2: Open Terminal 1 - Start LogMonitor (1 min)

```powershell
# Open PowerShell or CMD
cd C:\Users\ellaj\Desktop\RobloxGuard
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs
```

**Expected Output:**
```
=== Log Monitor Mode ===
Monitoring Roblox player logs for game joins...
Log directory: C:\Users\ellaj\AppData\Local\Roblox\logs
Press Ctrl+C to stop

```

✅ **Keep this terminal OPEN** - it will show game detections

---

## Step 3: Open Roblox App (Terminal 2) (2 min)

```powershell
# In a new PowerShell window
Start-Process roblox.exe
```

Wait ~30 seconds for Roblox app to fully load

---

## Step 4A: Test BLOCKED Game (2 min)

**In Roblox App:**
1. Search for game ID: **1818** (or **93978595733734**)
2. Click "PLAY"
3. Watch Terminal 1

**Expected in Terminal 1:**
```
[HH:MM:SS] ❌ BLOCKED: Game 1818
```

**Expected in Roblox App:**
- Game process attempts to launch
- Game closes/fails to start
- Back to Roblox home page

✅ If you see `❌ BLOCKED` message → **SUCCESS!**

---

## Step 4B: Test ALLOWED Game (2 min)

**In Roblox App:**
1. Search for a safe game (e.g., "Adopt Me" - ID: 2)
2. Click "PLAY"  
3. Watch Terminal 1

**Expected in Terminal 1:**
```
[HH:MM:SS] ✅ ALLOWED: Game 2
```

**Expected in Roblox App:**
- Game launches successfully
- You see the game's initial loading screen

✅ If you see `✅ ALLOWED` message → **SUCCESS!**

---

## Step 5: Stop LogMonitor (1 min)

```powershell
# In Terminal 1 where monitor is running:
# Press Ctrl+C

# Expected:
# Log monitor stopped.
```

---

## Success Criteria

| Test | Expected | Status |
|------|----------|--------|
| LogMonitor starts | Shows "Monitoring..." message | ☐ |
| Blocked game detected | `❌ BLOCKED: Game 1818` | ☐ |
| Blocked game closes | Game fails to launch | ☐ |
| Allowed game detected | `✅ ALLOWED: Game XXXX` | ☐ |
| Allowed game launches | Game starts normally | ☐ |
| LogMonitor stops | `Log monitor stopped.` | ☐ |

**If ALL ☑️ → READY TO COMMIT!**

---

## Troubleshooting

### "LogMonitor doesn't detect any game"
- Check Roblox logs exist:
  ```powershell
  ls "$env:LOCALAPPDATA\Roblox\logs" | tail -3
  ```
- If empty, Roblox may not be writing logs
- Try: Close Roblox, delete old logs, restart Roblox

### "Game 1818 not found in Roblox"
- Use any game ID that's in blocklist
- Or search for a test game like "Adopt Me"
- Current blocklist: 1818, 93978595733734

### "Terminal shows nothing after clicking Play"
- Roblox may not have launched yet (wait 5 sec)
- Or Roblox app is not registered as handler
- Try: Check `%LOCALAPPDATA%\Roblox\logs` manually

### "RobloxGuard.exe not found"
```powershell
# Verify installation
ls "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe"

# If missing, republish:
cd C:\Users\ellaj\Desktop\RobloxGuard\src
dotnet publish RobloxGuard.UI -c Release -r win-x64 -o "$env:LOCALAPPDATA\RobloxGuard" --self-contained -p:PublishSingleFile=true
```

---

## What Happens Behind the Scenes

1. **LogMonitor starts** → Subscribes to `$env:LOCALAPPDATA\Roblox\logs\*_Player_*_last.log`
2. **You click "Play"** → Roblox writes to log: `! Joining game 'UUID' place <PLACEID> at <IP>`
3. **LogMonitor detects** → Regex extracts placeId
4. **Check blocklist** → If in blocklist, proceed to block
5. **Terminate process** → Kills `RobloxPlayerBeta.exe` if blocked
6. **Show UI** → (Optional) Block UI appears explaining action

All within ~500ms of game join attempt!

---

## Next Steps After Testing

### If ALL Tests Pass ✅
```bash
cd C:\Users\ellaj\Desktop\RobloxGuard
git add -A
git commit -m "feat: Add --monitor-logs command for real-time game join detection

- Integrates LogMonitor into Program.cs
- Detects blocked games via player logs (500ms response time)
- Completes three-layer blocking:
  1. Protocol Handler (website)
  2. Log Monitor (app) ← NEW
  3. Handler Lock (registry)
- All 36 tests passing, zero errors"
git push origin main
```

### If Tests Fail ❌
1. Check terminal output for error messages
2. Verify config.json has blocklist items
3. Check LogMonitor code handles edge cases
4. Review Roblox log format with:
   ```powershell
   Get-Content "$env:LOCALAPPDATA\Roblox\logs\*_Player_*_last.log" -Tail 20
   ```

---

## Time Estimate
- Setup: 2 min
- Test blocked game: 2 min
- Test allowed game: 2 min
- Teardown: 1 min
- **Total: ~10 minutes**

Go! 🚀
