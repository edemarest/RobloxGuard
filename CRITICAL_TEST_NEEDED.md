# RobloxGuard: App Launch Blocking - Current State & Critical Test

**Date:** October 20, 2025  
**Status:** Awaiting test results - this determines the solution path

---

## What We've Built So Far

### ✅ Protocol Handler (Website Clicks) - WORKS
- When you click "Play" on website
- Browser sends `roblox-player://...placeId=XXXX...`
- Our handler is called
- We extract placeId and block if needed

### ✅ Handler Lock (NEW) - Prevents Hijacking
- Monitors registry every 5 seconds
- Roblox tries to register its own handler
- Handler Lock detects change and restores ours
- Ensures we stay in control of the protocol

### ❓ App Launch Blocking - UNKNOWN
- When you click "Play" in Roblox app
- Game still launches (didn't block)
- **We don't know WHY yet**

---

## The Critical Question

**When the Roblox app calls the protocol handler, WHAT URI does it pass?**

### Three Possible Scenarios:

**Scenario A: App Sends Full URI with PlaceId** ✅ BEST
```
roblox-player://play?placeId=93978595733734&...otherParams...
```
**Result:** Handler Lock works! Blocking the app works!

**Scenario B: App Sends Generic URI** ⚠️ NEEDS WORK
```
roblox-player://play
```
**Result:** Handler Lock called, but we can't identify which game → can't block

**Scenario C: App Bypasses Protocol** ❌ WORST
```
(No protocol call at all - direct launch)
```
**Result:** Our handler never called at all → can't block

---

## The Diagnostic Test

To find out which scenario is true, we set up a test handler that **logs what URI it receives**.

### What We Did:
1. Created a test batch file: `C:\temp\test_uri_handler.bat`
2. Registered it as the protocol handler
3. When you click "Play" in the app, it will:
   - Show you a CMD window with the URI
   - Log the URI to a file
   - Pause so you can read it

---

## What You Need to Do

### 1. Click "Play" on Blocked Game from Roblox App
- Open Roblox app
- Find game with ID: 93978595733734 (the one we added to blocklist)
- Click "Play"

### 2. Look at the CMD Window
- A black window will pop up
- It will show: "Received URI: [something]"
- **Copy that full URI**

### 3. Tell Me What You See

Paste the exact URI you see. For example:
```
roblox-player://play?placeId=93978595733734&someParam=value
```

OR

```
roblox-player://play
```

OR

```
(nothing happens - no window appears)
```

---

## What Each Result Means

### If You See: `roblox-player://...placeId=93978595733734...`

✅ **HANDLER LOCK IS THE COMPLETE SOLUTION!**

**Why:**
- App passes placeId in the URI
- Handler Lock ensures we're called
- We extract placeId and block
- **App launch blocking WORKS**

**What to do:**
- Keep Handler Lock running (via scheduled task)
- Test that blocking actually prevents launch
- Deploy!

---

### If You See: `roblox-player://play` (No PlaceId)

⚠️ **Handler Lock Called, But Can't Identify Game**

**Why:**
- App calls our handler
- But doesn't pass game identification
- We can't tell which game to block

**What we need:**
- Find where Roblox stores the game ID (before launching)
- Monitor registry or config files
- Extract game ID from there
- Implement an alternative blocking mechanism

---

### If Nothing Happens (No Window)

❌ **Roblox Bypasses Protocol Completely**

**Why:**
- App doesn't call protocol handler at all
- Direct spawn of RobloxPlayerBeta.exe
- Our handler is never invoked

**What we need:**
- Process watcher that works without admin
- Read game ID from process or memory
- Intercept after launch starts
- Graceful termination approach

---

## After the Test

### When Handler Lock is Restored:

```powershell
$regPath = "HKCU:\Software\Classes\roblox-player\shell\open\command"
Set-ItemProperty $regPath -Name "(Default)" -Value `
  "`"C:\Users\ellaj\AppData\Local\RobloxGuard\RobloxGuard.exe`" --handle-uri `"%1`""
```

Then verify:
```powershell
(Get-ItemProperty $regPath).'(Default)'
# Should show: "C:\Users\ellaj\AppData\Local\RobloxGuard\RobloxGuard.exe" --handle-uri "%1"
```

---

## Handler Lock Technology Explained

### How It Works (For Scenario A)

```
Timeline:
─────────────────────────────────────────────────

T0: User clicks "Play" in Roblox app
    └─ Game: 93978595733734

T1: Roblox app prepares launch
    └─ Internally resolves: placeId = 93978595733734
    
T2: Roblox calls protocol handler
    ├─ Checks registry: HKCU\Software\Classes\roblox-player\shell\open\command
    ├─ Handler Lock KEPT it pointing to: RobloxGuard.exe
    └─ Calls: RobloxGuard.exe --handle-uri "roblox-player://...placeId=93978595733734..."

T3: RobloxGuard receives the call
    ├─ Parses URI
    ├─ Extracts placeId = 93978595733734
    ├─ Loads config
    ├─ Checks blocklist: [1818, 93978595733734]
    ├─ Found! Game is blocked
    └─ Shows Block UI, terminates

T4: Game does NOT launch ✅
```

### What Handler Lock Prevents (Without It)

```
T2 (Without Handler Lock):
    ├─ Roblox app runs in background
    ├─ Roblox changes registry to: RobloxPlayerBeta.exe
    └─ Our handler reference gets hijacked

T3 (Without Handler Lock):
    ├─ Game launches with RobloxPlayerBeta directly
    └─ Our RobloxGuard never gets called ❌
```

---

## Current Code Status

### Handler Lock Implementation
- ✅ Created: `RobloxGuard.Core\HandlerLock.cs`
- ✅ Integrated: `Program.cs` with `--lock-handler` command
- ✅ Tested: Successfully detects and restores hijacked handler
- ✅ Built & Published: Available in `out\publish\RobloxGuard.exe`

### Test Mode
- ✅ Created: Test handler batch file
- ✅ Registered: Protocol handler pointing to test
- ⏳ Awaiting: Your test click

---

## Timeline After Test

### If Scenario A (Plausible) ✅
```
Today: Get test results
Next: Verify blocking actually works in practice
Then: Implement scheduled task to keep lock running
Then: Update installer
Done: Release v1.0.2 with app launch blocking
```

### If Scenario B (Generic URI) ⚠️
```
Today: Diagnose that app sends no placeId
Next: Research where placeId is stored (registry/files)
Next: Implement alternative detection
Next: Test alternative approach
Next: Release v1.0.2 with workaround
```

### If Scenario C (Bypassed) ❌
```
Today: Diagnose complete bypass
Next: Investigate if worth solving
Next: Consider giving up on app launches
OR: Implement complex process monitoring
```

---

## Important Notes

### Why This Test Matters
- **Handler Lock alone doesn't guarantee blocking**
- The placeId **must** be in the URI we receive
- This test determines if Roblox provides it

### No Risk
- Test handler just logs URIs, doesn't block anything
- Easy rollback to RobloxGuard handler
- Safe diagnostic process

### This Could Be The Answer
If Scenario A is true, Handler Lock is a complete and elegant solution to app launch blocking without requiring admin access.

---

## Ready?

When you're ready, click "Play" on the blocked game from the Roblox app, and tell me exactly what appears in the CMD window!
