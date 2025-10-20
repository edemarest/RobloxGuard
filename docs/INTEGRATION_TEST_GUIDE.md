# RobloxGuard Integration Test Guide

## Overview
This document provides step-by-step integration testing procedures for RobloxGuard on Windows.

## Phase 1: Core Logic Validation

### 1.1 Parse Test - URI Parsing
```
dotnet run --project src\RobloxGuard.UI -- --test-parse "roblox://placeId=12345"
```
**Expected**: Extracts placeId 12345

### 1.2 Parse Test - CLI Arguments
```
dotnet run --project src\RobloxGuard.UI -- --test-parse "RobloxPlayerBeta.exe --id 67890"
```
**Expected**: Extracts placeId 67890

### 1.3 Config System Test
```
dotnet run --project src\RobloxGuard.UI -- --test-config
```
**Expected**: 
- Config loads/saves successfully
- PIN hashing works (salt changes each time)
- Blocklist defaults applied

### 1.4 Run All Unit Tests
```
dotnet test src\RobloxGuard.sln
```
**Expected**: All 36 tests pass (24 parsing + 9 config + 3 scheduler)

---

## Phase 2: UI & Blocking

### 2.1 Block UI Display
```
dotnet run --project src\RobloxGuard.UI -- --show-block-ui 12345
```
**Expected**:
- Window appears with "Place 12345 is blocked"
- Fetches game name from Roblox API (e.g., "Adoption Me!")
- Shows 3 buttons: "Back to Favorites", "Request Unlock", "Enter PIN"
- If PIN not set: "Back to Favorites" closes, others disabled
- Buttons work (dialogs appear)

### 2.2 PIN Entry Dialog (via Settings)
```
dotnet run --project src\RobloxGuard.UI -- --ui
```
**Expected**:
- Settings window opens
- "PIN" tab visible
- "Set PIN" button available
- Entering PIN and confirming works
- PIN is securely hashed (PBKDF2)

### 2.3 Blocklist Management (via Settings)
**In Settings UI → Blocklist tab:**
- Add placeId 12345 → appears in list
- Toggle it (checkmark/unchecked)
- Remove it → gone
- Mode switch (blocklist/whitelist) works

### 2.4 Settings Tab
**In Settings UI → Settings tab:**
- "Overlay enabled" toggle (future feature)
- "Watcher enabled" toggle (future feature)
- "App data folder" button opens folder

---

## Phase 3: Installation & Registry

### 3.1 First-Run Installation (Simulated)
**Manual test** (requires knowledge of current Roblox handler):
```powershell
# Check current Roblox handler
Get-ItemProperty 'HKCU:\Software\Classes\roblox-player\shell\open\command'

# Run installation
dotnet run --project src\RobloxGuard.UI -- --install-first-run

# Verify protocol handler is now RobloxGuard
Get-ItemProperty 'HKCU:\Software\Classes\roblox-player\shell\open\command'

# Verify backup exists
Get-ItemProperty 'HKCU:\Software\RobloxGuard\Upstream'
```

### 3.2 Uninstall
```powershell
dotnet run --project src\RobloxGuard.UI -- --uninstall

# Verify original handler is restored
Get-ItemProperty 'HKCU:\Software\Classes\roblox-player\shell\open\command'
```

### 3.3 Scheduled Task Verification
```powershell
# After installation, verify task exists
schtasks /query /tn "RobloxGuard\Watcher" /v

# Verify it's set to run at logon
# Should see: Trigger = "At logon"
# Command = "[path to RobloxGuard.exe] --watch"
```

---

## Phase 4: Protocol Handler Integration

### 4.1 URI Interception Test (Manual)
**Setup:**
1. Run `--install-first-run` to register handler
2. Add placeId 12345 to blocklist via `--ui`
3. Set a PIN via `--ui`

**Test:**
```powershell
# Trigger Roblox protocol URI
Start-Process "roblox-player://placeId=12345"
```

**Expected:**
- Block UI appears immediately
- Game never launches
- Block UI shows place 12345 blocked
- "Back to Favorites" closes the dialog
- "Enter PIN" allows unlock if correct PIN entered

### 4.2 Process Watcher Fallback (Manual)
**Setup:**
1. Run `--watch` in one terminal
2. Simulate RobloxPlayerBeta.exe launch with blocked placeId

**Expected:**
- Watcher detects process
- Block UI appears
- Process terminated if "Back to Favorites" clicked

---

## Phase 5: Real Roblox Testing (End-to-End)

### 5.1 Fresh Windows VM Test
1. Install RobloxGuard via installer
2. Launch Roblox normally → should work
3. Click on a game in blocklist → Block UI appears
4. Try to unlock with wrong PIN → error message
5. Try to unlock with correct PIN → game launches
6. Verify process watcher catches blocked attempts

### 5.2 Clean Uninstall Test
1. Uninstall RobloxGuard
2. Verify original Roblox handler restored
3. Launch Roblox normally → should work without RobloxGuard interference
4. Check Registry and Task Scheduler are clean

---

## Phase 6: Performance & Reliability

### 6.1 Parse Performance
- Parsing should be <1ms per URI
- Regex should handle edge cases (whitespace, encoding, etc.)

### 6.2 Config Performance
- Load/save should be <100ms
- PIN verification should take ~100-200ms (PBKDF2 intentionally slow)

### 6.3 WMI Watcher Performance
- Should monitor without excessive CPU usage
- Process detection should be near-real-time

### 6.4 Block UI Performance
- Should appear within 500ms of trigger
- API call timeout after 5 seconds (fallback to generic name)

---

## Troubleshooting

### Issue: "Could not determine application path"
- Ensure RobloxGuard.exe is in %LOCALAPPDATA%\RobloxGuard\
- Or run from dotnet: `dotnet run --project src\RobloxGuard.UI -- --install-first-run`

### Issue: "Access denied" during registry operations
- Ensure running as normal user (not admin - creates per-user keys)
- Check that %LOCALAPPDATA% is writable

### Issue: Scheduled task not created
- Verify schtasks command is available (should be on all Windows)
- Check that task doesn't already exist (delete manually if needed)
- Verify RobloxGuard.exe path has no special characters

### Issue: Block UI doesn't appear
- Check that config.json exists in %LOCALAPPDATA%\RobloxGuard\
- Verify Roblox API is reachable (offline fallback should still work)
- Ensure --show-block-ui works first to isolate issue

---

## Success Criteria

✅ **Phase 1**: All 36 unit tests pass
✅ **Phase 2**: Block UI displays correctly with real API data
✅ **Phase 3**: Registry + scheduled task creation works
✅ **Phase 4**: Protocol handler intercepts and blocks correctly
✅ **Phase 5**: Full end-to-end Roblox blocking works
✅ **Phase 6**: Performance meets requirements

---

## CI/CD Integration

These tests should be automated:
- Unit tests: Run on every PR + push
- Parsing validation: Run on documentation changes
- Registry/Task operations: Run on Windows CI (if available)
- Full end-to-end: Manual on VM or canary release

---

## Notes for Future Enhancement

- Add logging to all phases for debugging
- Create mock Roblox API server for testing without internet
- Implement stress testing for watcher (1000+ process spawns/sec)
- Add telemetry for real-world deployment monitoring
