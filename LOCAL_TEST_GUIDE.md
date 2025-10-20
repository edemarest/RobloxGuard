# Local Build, Install & Test Guide for LogMonitor

**Date:** October 20, 2025  
**Status:** Ready for local testing  
**Build Output:** ✅ 0 errors, 0 warnings

---

## Phase 1: Clean Build (5 minutes)

### Step 1.1: Clean previous builds
```powershell
cd C:\Users\ellaj\Desktop\RobloxGuard\src
dotnet clean RobloxGuard.sln -c Release
```

### Step 1.2: Restore packages
```powershell
dotnet restore RobloxGuard.sln
```

### Step 1.3: Full release build
```powershell
dotnet build RobloxGuard.sln -c Release --no-restore
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Phase 2: Run All Tests (3 minutes)

### Step 2.1: Run unit tests
```powershell
dotnet test RobloxGuard.Core.Tests/RobloxGuard.Core.Tests.csproj -c Release --no-build
```

**Expected output:**
```
Passed: 36 tests ✅
```

### Step 2.2: Verify specific test categories
```powershell
# Test PlaceId parsing (existing)
dotnet run --project RobloxGuard.UI/RobloxGuard.UI.csproj -c Release -- --test-parse "roblox://placeId=12345"

# Test configuration (existing)
dotnet run --project RobloxGuard.UI/RobloxGuard.UI.csproj -c Release -- --test-config

# Test help (including NEW --monitor-logs command)
dotnet run --project RobloxGuard.UI/RobloxGuard.UI.csproj -c Release -- --help
```

---

## Phase 3: Local Installation (5 minutes)

### Step 3.1: Create output directory
```powershell
$appDir = "$env:LOCALAPPDATA\RobloxGuard"
if (Test-Path $appDir) {
    Write-Host "⚠ RobloxGuard directory exists. Backing up..."
    Copy-Item $appDir "$appDir.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}
```

### Step 3.2: Publish the application
```powershell
cd C:\Users\ellaj\Desktop\RobloxGuard\src
dotnet publish RobloxGuard.UI/RobloxGuard.UI.csproj `
    -c Release `
    -o "$env:LOCALAPPDATA\RobloxGuard" `
    --no-build `
    --self-contained `
    -p:PublishSingleFile=true
```

**Expected output:**
```
RobloxGuard → C:\Users\ellaj\AppData\Local\RobloxGuard
```

### Step 3.3: Verify installation
```powershell
ls "$env:LOCALAPPDATA\RobloxGuard" | Select-Object Name, Length
```

**Expected files:**
```
RobloxGuard.exe
RobloxGuard.dll
(various other .dll files)
```

### Step 3.4: Test the installed executable
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --help
```

**Expected output:** (should show --monitor-logs command)
```
  RobloxGuard.exe --monitor-logs    Monitor Roblox logs for game joins
```

---

## Phase 4: Pre-Test Setup (5 minutes)

### Step 4.1: Verify blocklist includes test game
```powershell
# Check current config
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --test-config
```

**Expected output includes:**
```
Blocked games: 1818, 93978595733734
```

If not, add the game ID:
```powershell
$configPath = "$env:LOCALAPPDATA\RobloxGuard\config.json"
# Edit and add game IDs to blocklist array
notepad $configPath
```

### Step 4.2: Start monitoring Roblox for logs
```powershell
# Check if Roblox is running
Get-Process RobloxPlayerBeta -ErrorAction SilentlyContinue
# If it is, close it for clean test
Get-Process Roblox -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Step 4.3: Open Roblox app
```powershell
# Launch Roblox if not already running
Start-Process roblox.exe
```

**Wait for:** Roblox app to fully load (30 seconds)

---

## Phase 5: Real-World Test - LogMonitor (10 minutes)

### Test 5.1: Start LogMonitor in Terminal 1
```powershell
# Terminal 1: Start monitoring
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs
```

**Expected output:**
```
=== Log Monitor Mode ===
Monitoring Roblox player logs for game joins...
Log directory: C:\Users\ellaj\AppData\Local\Roblox\logs
Press Ctrl+C to stop

```

**⚠️ KEEP THIS TERMINAL OPEN**

### Test 5.2: Test BLOCKED Game (Terminal 2)

**In Roblox app:**
1. Search for a game with ID in blocklist (e.g., game 1818 or 93978595733734)
2. Click "PLAY"
3. Watch Terminal 1 for block detection

**Expected in Terminal 1 (within 500ms):**
```
[HH:MM:SS] ❌ BLOCKED: Game 1818
[or]
[HH:MM:SS] ❌ BLOCKED: Game 93978595733734
```

**Expected Roblox behavior:**
- Game process tries to start
- LogMonitor detects blocked game from logs
- LogMonitor terminates RobloxPlayerBeta.exe
- Game closes (or fails to fully launch)
- Block UI should appear (if integrated)

### Test 5.3: Test ALLOWED Game (Terminal 2)

**In Roblox app:**
1. Search for a safe game (NOT in blocklist, e.g., game 2)
2. Click "PLAY"
3. Watch Terminal 1

**Expected in Terminal 1:**
```
[HH:MM:SS] ✅ ALLOWED: Game 2
```

**Expected Roblox behavior:**
- Game process starts normally
- LogMonitor allows it
- Game launches successfully

### Test 5.4: Test Multiple Games

```
1. Click Play on allowed game → Terminal 1: ✅ ALLOWED
2. Wait for game to load fully
3. Return to Roblox and close game
4. Click Play on blocked game → Terminal 1: ❌ BLOCKED (game closes)
5. Return to Roblox 
6. Click Play on different allowed game → Terminal 1: ✅ ALLOWED
```

### Test 5.5: Stop LogMonitor
```powershell
# In Terminal 1 where monitor is running:
# Press Ctrl+C

# Expected output:
# Log monitor stopped.
```

---

## Phase 6: Verify Handler Still Works (5 minutes)

### Test 6.1: Test Protocol Handler (website)

```powershell
# In Terminal 2, test website-based blocking
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --handle-uri "roblox-player://games/placeId=1818"
```

**Expected output:**
```
=== Protocol Handler Mode ===
...
Game 1818 is BLOCKED
```

### Test 6.2: Test with allowed game
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --handle-uri "roblox-player://games/placeId=2"
```

**Expected output:**
```
...
Game 2 is ALLOWED
...
Launching upstream handler...
```

---

## Phase 7: Verify Handler Lock Still Works (5 minutes)

### Test 7.1: Start Handler Lock

```powershell
# Terminal 1
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --lock-handler
```

**Expected output:**
```
=== Handler Lock Mode ===
Monitoring protocol handler to prevent hijacking by Roblox app...
Press Ctrl+C to stop

[HH:MM:SS] ✓ Handler protection started
[HH:MM:SS] ✓ Handler verified - correctly pointing to RobloxGuard
```

### Test 7.2: Keep monitoring for 30 seconds

```
Handler Lock should show:
  - ✓ Handler verified (every 5 seconds)
  OR
  - ! Handler was hijacked - restoring to RobloxGuard (if Roblox tries to hijack)
```

### Test 7.3: Stop Handler Lock
```powershell
# In Terminal 1:
# Press Ctrl+C

# Expected:
# Handler lock stopped.
```

---

## Phase 8: Full Integration Test (15 minutes)

### Test 8.1: Concurrent Running (Handler Lock + LogMonitor)

**Optional:** Test if both can run together (they shouldn't both need to, but good to verify):

```powershell
# Terminal 1: Handler Lock
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --lock-handler

# Terminal 2: LogMonitor
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs

# Terminal 3: User action
# In Roblox app, click Play on blocked game

# Expected:
# Terminal 2 should detect: ❌ BLOCKED: Game XXXX
```

---

## Phase 9: Checklist - Complete Test Suite

```
=== UNIT TESTS ===
☐ Run: dotnet test RobloxGuard.Core.Tests
☐ Result: 36 tests passing

=== BUILD ===
☐ Run: dotnet build RobloxGuard.sln -c Release
☐ Result: 0 errors, 0 warnings

=== INSTALLATION ===
☐ Run: dotnet publish to %LOCALAPPDATA%\RobloxGuard
☐ Result: RobloxGuard.exe exists and runs

=== LOGMONITOR - BLOCKED GAME ===
☐ Run: RobloxGuard.exe --monitor-logs
☐ Click: Play on blocked game in Roblox app
☐ Result: Terminal shows ❌ BLOCKED: Game XXXX
☐ Result: Game process closes/fails to launch

=== LOGMONITOR - ALLOWED GAME ===
☐ Run: RobloxGuard.exe --monitor-logs
☐ Click: Play on allowed game in Roblox app
☐ Result: Terminal shows ✅ ALLOWED: Game XXXX
☐ Result: Game launches successfully

=== PROTOCOL HANDLER - BLOCKED ===
☐ Run: RobloxGuard.exe --handle-uri "roblox-player://games/placeId=1818"
☐ Result: Shows "Game 1818 is BLOCKED"

=== PROTOCOL HANDLER - ALLOWED ===
☐ Run: RobloxGuard.exe --handle-uri "roblox-player://games/placeId=2"
☐ Result: Shows "Game 2 is ALLOWED" and launches

=== HANDLER LOCK ===
☐ Run: RobloxGuard.exe --lock-handler
☐ Result: Shows "Handler protection started"
☐ Result: Shows "Handler verified" every ~5 seconds

=== EDGE CASES ===
☐ Click Play on blocked game, then immediately on allowed game
  → LogMonitor should handle both correctly
☐ Roblox app running in background
  → Handler Lock should prevent hijacking
☐ Roblox logs don't exist initially
  → LogMonitor should handle gracefully (not crash)
☐ Roblox is closed while monitoring
  → LogMonitor should continue waiting for next game join
```

---

## Quick Commands Reference

### Build
```powershell
cd C:\Users\ellaj\Desktop\RobloxGuard\src
dotnet clean RobloxGuard.sln -c Release
dotnet restore RobloxGuard.sln
dotnet build RobloxGuard.sln -c Release --no-restore
```

### Test
```powershell
dotnet test RobloxGuard.Core.Tests/RobloxGuard.Core.Tests.csproj -c Release --no-build
```

### Install
```powershell
dotnet publish RobloxGuard.UI/RobloxGuard.UI.csproj `
    -c Release -o "$env:LOCALAPPDATA\RobloxGuard" `
    --no-build --self-contained -p:PublishSingleFile=true
```

### Run (locally, without installing)
```powershell
dotnet run --project RobloxGuard.UI/RobloxGuard.UI.csproj -c Release -- --monitor-logs
dotnet run --project RobloxGuard.UI/RobloxGuard.UI.csproj -c Release -- --lock-handler
dotnet run --project RobloxGuard.UI/RobloxGuard.UI.csproj -c Release -- --handle-uri "<URI>"
```

### Run (after installing)
```powershell
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --monitor-logs
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --lock-handler
& "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --help
```

### View Logs
```powershell
# Roblox player logs
ls "$env:LOCALAPPDATA\Roblox\logs" | Sort-Object LastWriteTime -Descending | Select-Object -First 5

# Show latest player log
$log = (ls "$env:LOCALAPPDATA\Roblox\logs\*_Player_*_last.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
tail -f $log  # Follow in real-time
```

### Troubleshooting

**If LogMonitor doesn't detect game:**
```powershell
# Check if Roblox logs exist
Test-Path "$env:LOCALAPPDATA\Roblox\logs"

# Check latest log file
$logFile = (ls "$env:LOCALAPPDATA\Roblox\logs\*_Player_*_last.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
Write-Host "Latest log: $logFile"
Get-Content $logFile | tail -20
```

**If build fails:**
```powershell
# Check .NET version
dotnet --version  # Should be 8.0+

# Force rebuild
dotnet clean RobloxGuard.sln
dotnet build RobloxGuard.sln -c Release -v d  # Verbose output
```

**If installation fails:**
```powershell
# Check permissions
Test-Path "$env:LOCALAPPDATA"
[System.IO.Directory]::GetAccessControl("$env:LOCALAPPDATA") | Format-List

# Try manual directory creation
mkdir "$env:LOCALAPPDATA\RobloxGuard" -Force
```

---

## Success Criteria

### ✅ All Tests Passed
- [ ] 36 unit tests passing
- [ ] Build succeeds with 0 errors
- [ ] No compiler warnings

### ✅ Installation Works
- [ ] RobloxGuard.exe created in %LOCALAPPDATA%\RobloxGuard
- [ ] --help shows all commands including --monitor-logs

### ✅ LogMonitor Works
- [ ] Blocked game detected and closed
- [ ] Allowed game detected and runs
- [ ] Multiple games work correctly

### ✅ Existing Features Still Work
- [ ] Protocol handler blocks/allows correctly
- [ ] Handler lock prevents hijacking
- [ ] Config system works

### ✅ Ready to Commit
- All tests pass ✅
- No errors or warnings ✅
- LogMonitor integration verified ✅
- All 3 layers working together ✅

---

## Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| "Unknown command: --monitor-logs" | Rebuild: `dotnet build` (you need Release build) |
| LogMonitor doesn't detect game | Check Roblox logs exist: `ls $env:LOCALAPPDATA\Roblox\logs` |
| Game doesn't close after blocking | Check ProcessKill is working: Run as user (not admin needed) |
| "Access denied" on install | Check %LOCALAPPDATA% is writable: `Test-Path "$env:LOCALAPPDATA"` |
| Build takes too long | Use `--no-restore`: `dotnet build --no-restore` |
| Tests fail | Run: `dotnet test --no-build` (use Release build) |

