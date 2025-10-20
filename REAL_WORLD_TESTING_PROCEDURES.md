# RobloxGuard - Real-World Testing Before Release

**Status**: Ready for Real-World Testing  
**Next Step**: Manual validation with actual Roblox client  
**Estimated Time**: 2-3 hours  

---

## 🎯 Testing Objectives

Before releasing to production, we need to verify:

1. ✅ Protocol handler **actually intercepts** Roblox URIs
2. ✅ Block UI **appears correctly** when game is blocked
3. ✅ PIN **prevents unauthorized** game launch
4. ✅ Process watcher **detects and terminates** blocked processes
5. ✅ Registry changes **persist correctly**
6. ✅ Uninstall **completely removes** all traces
7. ✅ Installer **works on clean VM** (no .NET pre-installed)

---

## 🏗️ Test Environment Setup

### Recommended: Virtual Machine

**Why VM?**
- Isolate testing from main system
- Can revert to clean state
- Can test installer on fresh Windows
- Safe to test edge cases

**VM Specifications**:
- OS: Windows 10 or 11 (64-bit)
- Disk: 40GB
- RAM: 4GB minimum
- Network: Internet connection (for Roblox and .NET)

**Setup Steps**:
1. Create VM snapshot "Clean Install"
2. Install Roblox client
3. Create snapshot "With Roblox"
4. Proceed with testing

### Alternative: Secondary Physical Windows Machine

If VM not available:
- Use spare laptop/desktop
- Or test on primary machine (but requires uninstall/reinstall)
- Less ideal, but workable

---

## 📋 Test Scenario 1: Basic Installation & Configuration

### Duration: 15 minutes

```
Pre-requisites:
✓ Fresh Windows with Roblox installed
✓ PowerShell open as user (not admin)

Step 1: Run First-Run Setup
  Command: RobloxGuard.exe --install-first-run
  Expected: Output shows "Installation completed successfully!"
  Check: 
    - No errors
    - Config created: %LOCALAPPDATA%\RobloxGuard\config.json
    - Scheduled task created: schtasks /query /tn RobloxGuard\Watcher

Step 2: Verify Registry Changes
  Command: Get-ItemProperty 'HKCU:\Software\Classes\roblox-player\shell\open\command'
  Expected: Value shows path to RobloxGuard.exe with --handle-uri
  Check:
    - Path contains RobloxGuard.exe
    - Command includes --handle-uri
    - Not admin user's HKLM

Step 3: Verify Scheduled Task
  Command: schtasks /query /tn RobloxGuard\Watcher /v
  Expected: Task details displayed
  Check:
    - Status: Ready
    - Trigger: At logon
    - Action: Runs RobloxGuard.exe --watch

Step 4: Open Settings UI
  Command: RobloxGuard.exe --ui
  Expected: SettingsWindow opens
  Check:
    - 4 tabs visible (PIN, Blocklist, Settings, About)
    - No errors in console
    - Window responds to interaction

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 2: Configuration & PIN Setup

### Duration: 10 minutes

```
Pre-requisites:
✓ RobloxGuard installed (from Scenario 1)
✓ Settings UI open

Step 1: Set Parent PIN
  Action: PIN tab → "Set PIN" button
  Input: Enter PIN (e.g., "1234")
  Expected: PIN entry dialog, then save
  Check:
    - PIN accepted
    - Config saved
    - No error messages

Step 2: Verify PIN in Config
  Command: type %LOCALAPPDATA%\RobloxGuard\config.json
  Expected: JSON with "parentPINHash" field
  Check:
    - Hash starts with "pbkdf2:"
    - No plaintext "1234"

Step 3: Find and Add Blocked Game
  Action: Blocklist tab → "Add Game" button
  Input: Enter placeId (e.g., 12345)
  Expected: Game added to list
  Check:
    - Game appears in list
    - Checkmark shows it's active

Step 4: Verify Blocklist Persistence
  Action: Close Settings UI
  Action: Reopen: RobloxGuard.exe --ui
  Expected: Blocklist still contains the game
  Check:
    - Game ID visible
    - Settings persisted

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 3: Protocol Handler - Allowed Game

### Duration: 10 minutes

```
Pre-requisites:
✓ RobloxGuard installed
✓ Game blocklist configured with specific placeId (e.g., 12345)
✓ Roblox client available

Step 1: Find an ALLOWED Game
  Action: Browse Roblox client
  Select: Game NOT in blocklist (e.g., something popular)
  Note: Take note of the placeId

Step 2: Launch Game via Protocol URI
  Command: Start-Process "roblox-player://placeId=67890" -Wait
  Expected: Roblox launches game normally
  Check:
    - No Block UI appears
    - Game loads in Roblox client
    - Can play normally

Step 3: Verify Process Running
  Command: tasklist | findstr RobloxPlayerBeta
  Expected: RobloxPlayerBeta.exe listed
  Check:
    - Process is running
    - Game is playable

Step 4: Close Game
  Action: Close Roblox client
  Expected: RobloxPlayerBeta.exe terminates
  Check:
    - Process gone (tasklist no longer shows it)

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 4: Protocol Handler - Blocked Game (No PIN)

### Duration: 10 minutes

```
Pre-requisites:
✓ RobloxGuard installed
✓ Game 12345 in blocklist
✓ PIN set to "1234"
✓ Roblox client available

Step 1: Attempt to Launch BLOCKED Game
  Command: Start-Process "roblox-player://placeId=12345" -Wait
  Expected: Block UI appears (not Roblox)
  Check:
    - BlockWindow visible
    - Window title shows block message
    - Game does NOT launch

Step 2: Verify Block UI Content
  Expected: UI shows:
    - Place ID: 12345
    - Game name (fetched from API or "Unknown Game")
    - Message: "[Game Name] is blocked"
    - Three buttons: "Back to Favorites", "Request Unlock", "Enter PIN"
  Check:
    - All elements visible
    - Buttons responsive

Step 3: Click "Back to Favorites"
  Action: Click "Back to Favorites" button
  Expected: Window closes, RobloxPlayerBeta.exe stays gone
  Check:
    - Block UI disappears
    - tasklist shows no RobloxPlayerBeta.exe

Step 4: Attempt Again with Wrong PIN
  Command: Start-Process "roblox-player://placeId=12345" -Wait
  Action: Block UI appears → Click "Enter PIN"
  Input: Wrong PIN (e.g., "9999")
  Expected: Error message "Incorrect PIN"
  Check:
    - Error message displayed
    - Game still does not launch
    - Can try again

Step 5: Attempt with Correct PIN
  Action: Block UI → Click "Enter PIN"
  Input: Correct PIN (1234)
  Expected: Game launches
  Check:
    - Block UI closes
    - Roblox client opens game
    - RobloxPlayerBeta.exe running

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 5: Process Watcher Fallback

### Duration: 15 minutes

```
Pre-requisites:
✓ RobloxGuard installed
✓ Game 12345 in blocklist
✓ PowerShell open

Step 1: Start Watcher in Background
  Command: Start-Process RobloxGuard.exe -ArgumentList "--watch" -NoNewWindow
  Expected: Watcher runs silently in background
  Check:
    - No window appears
    - Process started (tasklist | findstr RobloxGuard)

Step 2: Prepare to Launch RobloxPlayerBeta with Blocked PlaceId
  Note: This simulates Roblox client launching with blocked placeId
  Note: Requires either:
    - Option A: Bypass protocol handler (direct CLI launch)
    - Option B: Use Roblox DevTools to craft command
  
  Command (simulated): RobloxPlayerBeta.exe --id 12345
  (Note: May not work in practice if Roblox validates params)

Step 3: Verify Watcher Detected Process
  Expected: Block UI appears (from watcher, not handler)
  Check:
    - Block UI shows (event-driven detection)
    - Message indicates blocked placeId

Step 4: Allow Unlock via PIN
  Action: Enter correct PIN
  Expected: Process allowed to continue OR
           Message shows "Application blocked"
  Check:
    - Watcher callback worked

Step 5: Stop Watcher
  Command: Stop-Process -Name RobloxGuard -Force
  Expected: Watcher stops
  Check:
    - tasklist shows RobloxGuard gone
    - No running processes

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 6: System Logon & Auto-Start

### Duration: 20 minutes (real system restart required)

```
Pre-requisites:
✓ RobloxGuard installed
✓ Scheduled task created

Step 1: Verify Task Before Restart
  Command: schtasks /query /tn RobloxGuard\Watcher
  Expected: Task exists
  Check:
    - Status: Ready
    - Trigger: At logon
    - NextRunTime: (scheduled)

Step 2: Restart Windows
  Action: Restart-Computer or Manual restart
  Expected: System reboots

Step 3: After Restart - Check Watcher
  Command: tasklist | findstr RobloxGuard
  Expected: RobloxGuard running (watcher mode)
  Check:
    - Process present
    - Consuming low CPU (<1%)

Step 4: Test Blocking with Auto-Started Watcher
  Command: Start-Process "roblox-player://placeId=12345" -Wait
  Expected: Block UI appears
  Check:
    - Protocol handler works
    - Watcher also running

Step 5: Verify Logon Event
  Action: Event Viewer → Windows Logs → System
  Search: "RobloxGuard\Watcher" or task-related events
  Expected: Task executed successfully
  Check:
    - Task ran at logon
    - No errors in event log

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 7: Uninstall & Cleanup

### Duration: 15 minutes

```
Pre-requisites:
✓ RobloxGuard running and installed

Step 1: Backup Original Protocol Handler (For Reference)
  Command: Get-ItemProperty 'HKCU:\Software\RobloxGuard\Upstream'
  Expected: Shows original handler path (if backed up)
  Check:
    - Upstream registry key exists
    - Contains original handler path

Step 2: Run Uninstall
  Command: RobloxGuard.exe --uninstall
  Expected: Output shows "Uninstallation completed successfully!"
  Check:
    - No errors
    - Scheduled task deleted
    - Registry cleaned

Step 3: Verify Scheduled Task Deleted
  Command: schtasks /query /tn RobloxGuard\Watcher
  Expected: ERROR: The system cannot find the file specified
  Check:
    - Task is gone (error is expected)

Step 4: Verify Registry Restored
  Command: Get-ItemProperty 'HKCU:\Software\Classes\roblox-player\shell\open\command'
  Expected: Shows original Roblox handler (not RobloxGuard)
  Check:
    - No reference to RobloxGuard.exe
    - Original handler restored
    - Path points to Roblox location

Step 5: Verify RobloxGuard Registry Cleaned
  Command: Get-ItemProperty 'HKCU:\Software\RobloxGuard'
  Expected: ERROR or empty (if folder removed)
  Check:
    - Registry entries deleted
    - Clean state

Step 6: Test Roblox Works Without RobloxGuard
  Action: Launch Roblox game normally
  Expected: Game launches without Block UI
  Check:
    - No blocking occurs
    - Roblox works normally
    - Can play blocked games (if any)

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 8: Installer on Clean Windows VM

### Duration: 30 minutes

```
Pre-requisites:
✓ Fresh Windows 10/11 VM (no RobloxGuard, no .NET SDK)
✓ Roblox client installed
✓ RobloxGuard-Setup.exe available

Step 1: Verify Clean State
  Command: Get-ChildItem "$env:LOCALAPPDATA\RobloxGuard" -ErrorAction SilentlyContinue
  Expected: ERROR - folder doesn't exist
  Command: schtasks /query /tn RobloxGuard\Watcher
  Expected: ERROR - task doesn't exist

Step 2: Run Installer
  Action: Double-click RobloxGuard-Setup.exe
  Expected: Installer wizard opens
  Check:
    - Welcome screen visible
    - License agreement shown
    - Install location options available

Step 3: Complete Installation
  Action: Accept license, choose install location, click Install
  Expected: Installation progresses
  Check:
    - Progress bar updates
    - No errors
    - "Installation Completed" message

Step 4: Verify Installation
  Command: ls "$env:LOCALAPPDATA\RobloxGuard\"
  Expected: Folder exists with files
  Check:
    - RobloxGuard.exe present
    - config.json exists
    - All required files

Step 5: Test Blocking Works
  Command: "$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe" --ui
  Action: Set PIN, add game to blocklist
  Action: Try to launch blocked game
  Expected: Block UI appears, blocking works
  Check:
    - No crashes
    - Functionality matches main test

Step 6: Run Uninstaller
  Action: Control Panel → Programs → Uninstall Program → RobloxGuard
  Action: Click Uninstall
  Expected: Installer runs uninstall
  Check:
    - Registry cleaned
    - Task removed
    - App folder deleted

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 📋 Test Scenario 9: Edge Cases & Stress Testing

### Duration: 15 minutes

```
Pre-requisites:
✓ RobloxGuard installed
✓ Multiple games in blocklist

Scenario A: Rapid PIN Attempts
  Action: Block UI → "Enter PIN"
  Input: Wrong PIN → Try again immediately (5x)
  Expected: All rejected, no crash
  Check: ✓ Performance stable, ✓ No slowdown

Scenario B: Large Blocklist
  Action: Add 50+ games to blocklist
  Expected: No slowdown in Settings UI
  Check: ✓ Responsive, ✓ Saves correctly

Scenario C: Offline Mode (No Internet)
  Action: Disconnect internet, launch blocked game
  Expected: Block UI appears with "Unknown Game (offline)"
  Check: ✓ Still blocks, ✓ UI still functional

Scenario D: Very Long Game Names
  Action: Test with game name >50 characters
  Expected: UI wraps text correctly
  Check: ✓ No layout breaking, ✓ Text visible

Scenario E: Special Characters in PIN
  Action: Set PIN with special chars: "P@ssw0rd!123"
  Expected: PIN accepted and stored
  Check: ✓ Works, ✓ Hashes correctly

Scenario F: Network Timeout
  Action: Block internet during block UI game name fetch
  Expected: Falls back to "Unknown Game" after timeout
  Check: ✓ 5-second timeout works, ✓ No hang

PASS/FAIL: [ ]
Notes: _______________________________________________
```

---

## 🎯 Test Execution Checklist

```
Before Testing:
✓ All 36 unit tests passing
✓ Release build successful locally
✓ Have RobloxGuard.exe ready
✓ Have RobloxGuard-Setup.exe ready
✓ Have test Windows environment ready

Testing Order:
[ ] Scenario 1: Installation & Configuration
[ ] Scenario 2: Configuration & PIN Setup
[ ] Scenario 3: Protocol Handler - Allowed Game
[ ] Scenario 4: Protocol Handler - Blocked Game
[ ] Scenario 5: Process Watcher Fallback
[ ] Scenario 6: System Logon & Auto-Start
[ ] Scenario 7: Uninstall & Cleanup
[ ] Scenario 8: Installer on Clean VM
[ ] Scenario 9: Edge Cases & Stress Testing

After Testing:
✓ All scenarios passed
✓ No critical bugs found
✓ No performance issues
✓ Ready for release
```

---

## ⚠️ Critical Pass/Fail Criteria

### MUST PASS:
- ✅ Protocol handler intercepts and blocks correctly
- ✅ Block UI appears within 500ms
- ✅ PIN unlock works
- ✅ Uninstall cleans registry completely
- ✅ Registry restored to original Roblox handler
- ✅ Installer works on clean VM
- ✅ No crashes or exceptions
- ✅ Settings persist correctly

### SHOULD PASS:
- ✅ Process watcher detects processes
- ✅ Watcher auto-starts after logon
- ✅ Game name fetched from API
- ✅ Settings UI responsive
- ✅ Offline mode fallback works

### NICE TO HAVE:
- ✅ Performance optimizations
- ✅ Enhanced error messages
- ✅ Additional logging

---

## 📊 Test Results Summary

After completing all scenarios:

| Scenario | Status | Notes |
|----------|--------|-------|
| 1. Installation | [ ] | |
| 2. Configuration | [ ] | |
| 3. Allowed Game | [ ] | |
| 4. Blocked Game | [ ] | |
| 5. Watcher | [ ] | |
| 6. Auto-Start | [ ] | |
| 7. Uninstall | [ ] | |
| 8. Installer | [ ] | |
| 9. Edge Cases | [ ] | |

**Overall Result**: [ ] **PASS** / [ ] **FAIL**

---

## 🚀 Next Steps After Testing

**If ALL scenarios PASS**:
```powershell
git tag v1.0.0
git push origin v1.0.0
# GitHub Actions automatically creates release
```

**If ANY scenario FAILS**:
1. Document the issue
2. Fix the bug
3. Re-run failing scenario
4. Re-test dependent scenarios
5. Once all pass, proceed to release

---

**Status**: 🟡 **READY FOR REAL-WORLD TESTING**

All testing procedures documented. Proceed with systematic verification before release.
