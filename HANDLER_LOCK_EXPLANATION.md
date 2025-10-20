# Handler Lock: How It Solves (And Doesn't Solve) App Launch Blocking

**Date:** October 20, 2025  
**Issue:** Handler Lock prevents Roblox hijacking, but doesn't know WHICH game user selected

---

## The Problem You Identified

You're absolutely right. Here's the flow:

```
User clicks "Play" on game X in Roblox app
    ↓
Roblox app internally resolves: "Game X has placeId=12345"
    ↓
Roblox app calls: roblox-player://...placeId=12345...
    ↓
Registry lookup: HKCU\Software\Classes\roblox-player\shell\open\command
    ↓
[WITHOUT Handler Lock]
  Registry says: RobloxPlayerBeta.exe (Roblox hijacked it)
  Result: Game launches without our check ❌

[WITH Handler Lock]
  Registry says: RobloxGuard.exe --handle-uri
  Result: RobloxGuard is CALLED with the URI ✅
```

### Handler Lock Does This ✅
- Ensures registry points to RobloxGuard
- Prevents Roblox from hijacking back
- Makes sure our protocol handler is INVOKED

### Handler Lock Does NOT Do This ❌
- Does NOT change how Roblox calls us
- If Roblox doesn't pass placeId in the URI when calling from app, we can't see it
- The app might call with URI or might call directly

---

## How Roblox App Actually Calls Games

There are several possibilities:

### Scenario A: App Calls Protocol Handler with PlaceId ✅ (BEST CASE)

```
User clicks "Play" in app on game (placeId=93978595733734)
    ↓
Roblox app calls: roblox-player://...?placeId=93978595733734...
    ↓
Windows registry → RobloxGuard.exe --handle-uri (WITH Handler Lock!)
    ↓
RobloxGuard extracts placeId from URI
    ↓
Check blocklist: 93978595733734 is BLOCKED ✅
    ↓
Show Block UI, game doesn't launch ✅
```

**This is what Handler Lock enables!**

---

### Scenario B: App Calls Protocol Handler WITHOUT PlaceId ❌ (WORST CASE)

```
User clicks "Play" in app on game (placeId=93978595733734)
    ↓
Roblox app calls: roblox-player://play  (no placeId!)
    ↓
Windows registry → RobloxGuard.exe --handle-uri
    ↓
RobloxGuard tries to extract placeId: NOT FOUND ❌
    ↓
No placeId to check, so we FORWARD to upstream handler
    ↓
Game launches ❌ (we can't block what we can't identify)
```

**Handler Lock doesn't help here - we need more info**

---

### Scenario C: App Calls Directly Without Protocol ❌ (BYPASS)

```
User clicks "Play" in app
    ↓
Roblox app directly spawns: RobloxPlayerBeta.exe (no protocol URI at all!)
    ↓
Windows registry NEVER CONSULTED
    ↓
Our handler NEVER CALLED
    ↓
Game launches ❌ (we're completely bypassed)
```

**Handler Lock doesn't help here either - we're not even called**

---

## How to Determine Which Scenario We're In

We need to TEST what the Roblox app actually passes. Let me create a test:

### Test Approach

1. **Create a test handler that logs what it receives**
2. **Set it as the protocol handler**
3. **Click "Play" from Roblox app**
4. **Check the log to see what URI was passed**

---

## Easy Test to Determine the Scenario

### Step 1: Create a Test Handler Script

Create a batch file that logs incoming URIs:

```batch
@echo off
echo [%date% %time%] Received URI: %1 >> C:\Users\ellaj\AppData\Local\RobloxGuard\handler_test.log
echo URI: %1
pause
```

### Step 2: Register Test Handler

```powershell
$testHandler = "C:\Users\ellaj\Desktop\test_handler.bat"
$regPath = "HKCU:\Software\Classes\roblox-player\shell\open\command"
Set-ItemProperty $regPath -Name "(Default)" -Value "`"$testHandler`" `"%1`""
```

### Step 3: Click "Play" from Roblox App

Click on the blocked game from the Roblox app.

### Step 4: Check the Log

```powershell
Get-Content "C:\Users\ellaj\AppData\Local\RobloxGuard\handler_test.log" -Tail 10
```

### Step 5: Analyze

- **If log shows URI with placeId:** Handler Lock works! ✅
- **If log shows URI without placeId:** Need alternative approach
- **If no log entry:** Roblox bypasses protocol handler completely ❌

---

## The REAL Solution We Need

Based on test results, here are possible solutions:

### Solution 1: If App Passes PlaceId in URI (Scenario A)

✅ **Handler Lock is THE solution!**

With Handler Lock running:
- App clicks "Play"
- App calls protocol with placeId
- Handler Lock ensures we're called
- We extract placeId and block if needed

**What to do:** Just keep Handler Lock running!

---

### Solution 2: If App Passes Empty/Generic URI (Scenario B)

**We need to find where Roblox stores the target placeId:**

Possibilities:
1. **Registry key** - Roblox writes placeId to registry before launching
   - Monitor for registry changes
   - Extract placeId from registry

2. **Config file** - Roblox writes to AppData JSON/XML
   - Monitor `%LOCALAPPDATA%\Roblox\*` for changes
   - Extract placeId from file

3. **Environment variable** - Roblox sets env var before launching
   - Read env vars in RobloxPlayerBeta process
   - Extract placeId

4. **IPC** - Roblox communicates via named pipes/shared memory
   - Very complex, requires hooking

**Most likely:** Registry key or config file

---

### Solution 3: If App Bypasses Completely (Scenario C)

**We need ProcessWatcher to work:**

1. **Remove WMI dependency** - Use ETW (Event Tracing for Windows) instead
2. **Extract placeId from process memory** - Read Roblox process memory for target game
3. **Use FileSystemWatcher** - Monitor AppData for game config files

**This is the nuclear option**

---

## TEST: How Handler Lock WILL Help

Assuming Scenario A is true (app passes placeId):

### Before Handler Lock
```
1. User clicks "Play" on blocked game in app
2. Roblox app calls roblox-player://placeId=93978595733734
3. Registry lookup → RobloxPlayerBeta.exe (Roblox hijacked it)
4. RobloxPlayerBeta launches game
5. Result: BLOCKED FAILS ❌
```

### After Handler Lock (Running)
```
1. User clicks "Play" on blocked game in app
2. Roblox app calls roblox-player://placeId=93978595733734
3. Registry lookup → RobloxGuard.exe (Handler Lock keeps it)
4. RobloxGuard extracts placeId, checks blocklist
5. PlaceId is blocked, shows Block UI
6. Result: BLOCKED WORKS ✅
```

---

## Easy Test Right Now

### Quick Diagnostic Test

Let me create a minimal test to see what Roblox passes:

```powershell
# 1. Create test handler that just echoes the URI
$testHandler = @"
@echo off
echo %1 > C:\temp\uri_test.txt
pause
"@

$testHandler | Set-Content "C:\temp\test_handler.bat"

# 2. Set it as protocol handler
$regPath = "HKCU:\Software\Classes\roblox-player\shell\open\command"
Set-ItemProperty $regPath -Name "(Default)" -Value "`"C:\temp\test_handler.bat`" `"%1`""

# 3. User clicks "Play" in app (YOU DO THIS)

# 4. Check what was passed
Get-Content "C:\temp\uri_test.txt"

# 5. Restore RobloxGuard handler
Set-ItemProperty $regPath -Name "(Default)" -Value "`"$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe`" --handle-uri `"%1`""
```

---

## My Prediction

Based on how web protocols work, I believe **Scenario A is most likely**:

The Roblox app will call:
```
roblox-player://...?placeId=93978595733734&...otherParams...
```

**If this is true:**
- Handler Lock = Complete Solution ✅
- App launch blocking works automatically ✅

**If this is false:**
- We need to investigate further
- Might need registry/file monitoring
- Might need process memory reading

---

## Next Steps

### Immediate: Test to Determine Scenario

1. **Run diagnostic test** (PowerShell commands above)
2. **Click "Play" from Roblox app on blocked game**
3. **Check output file** to see what URI was passed
4. **Report findings**

### Based on Findings:

**If URI has placeId:**
```
✅ Handler Lock works!
✅ App launch blocking is solved
✅ Just need to keep lock running (via scheduled task)
```

**If URI is generic/empty:**
```
❌ Need alternative approach
→ Implement registry monitoring
→ Or file system monitoring
→ Or process memory analysis
```

**If no URI at all:**
```
❌ Roblox completely bypasses protocol
→ Need ProcessWatcher improvements
→ Or DLL injection (violates our rules)
→ Or give up on app launch blocking
```

---

## Summary

**Handler Lock does NOT magically know which game the user selected.**

It only ensures that **if Roblox app calls the protocol handler**, we're the ones who get called.

**What actually blocks the game:**
- The placeId must be in the URI that Roblox passes
- Our regex patterns extract it from the URI
- We check blocklist
- We block if match found

**Handler Lock's role:**
- Prevents Roblox from hijacking the registry
- Ensures we're called at all
- Doesn't add any blocking logic

**Test determines everything:**
- If Roblox passes placeId in URI → Handler Lock = Complete Solution
- If Roblox doesn't pass placeId → Need more investigation

Would you like to run the test now?
