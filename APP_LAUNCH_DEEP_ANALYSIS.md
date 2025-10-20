# RobloxGuard: App Launch Blocking - Deep Analysis & Solution

**Date:** October 20, 2025  
**Issue:** Games launched from Roblox app are NOT being blocked  
**Root Cause:** Roblox re-registers protocol handler, bypassing our interception

---

## The Real Problem

### What We Discovered

1. **Registry Approach Failed:**
   - We changed: `HKCU\Software\Classes\roblox-player\shell\open\command` to point to RobloxGuard.exe
   - User clicked "Play" in Roblox app
   - **Game still launched** ❌
   - When we checked registry → it REVERTED to original RobloxPlayerBeta.exe ❌

2. **Why Registry Reverted:**
   - The Roblox app keeps a background process running (`RobloxCrashHandler`, main app process)
   - When user clicks "Play", Roblox likely re-registers its protocol handler
   - This overwrites our change
   - By the time we check, our handler is gone

3. **Roblox Is Deliberately Bypassing:**
   - Even if we control the registry, Roblox can change it back
   - This is an arms race we can't win with just registry changes

### Current Architecture Limitations

```
Website Click → roblox:// URI → Registry lookup → Protocol handler
    ✅ WORKS (we intercept here)

App Click → Roblox internal IPC/message → Direct launch (BYPASSES registry?)
    ❌ DOESN'T WORK (app launches directly without protocol)
```

OR:

```
App Click → roblox:// URI → Registry lookup → Roblox re-registers itself
    ✅ We change registry
    ⚠️ Game launches
    ❌ Registry reverts to Roblox handler
```

---

## Two Blocking Mechanisms We Built

### 1. Protocol Handler (Website Clicks) ✅ WORKS

```
User on website → Click "Play" 
→ Browser launches: roblox-player://...placeId=XXXX
→ OS routes to: roblox-player protocol handler in registry
→ Executes: RobloxGuard.exe --handle-uri "%1"
→ RobloxGuard checks blocklist
→ If blocked: Shows Block UI, doesn't forward ✅
→ If allowed: Forwards to Roblox via upstream handler ✅
```

**Why it works:** Website protocol handler is a one-time call. Doesn't matter if Roblox re-registers after.

---

### 2. Process Watcher (App Launches) ⚠️ NEEDS FIX

```
User in Roblox app → Click "Play"
→ Roblox spawns: RobloxPlayerBeta.exe
→ Our ProcessWatcher detects process creation (via WMI)
→ Extracts placeId from process command line
→ If blocked: Terminates process, shows Block UI ✅
→ If allowed: Lets process run ✅
```

**Why it doesn't work:** 
- ProcessWatcher uses WMI (Win32_ProcessStartTrace)
- WMI requires admin permissions
- RobloxPlayerBeta launches WITHOUT command line args (placeId not in CLI)

---

## The Real Issue: PlaceId Not in Command Line

### When App Launches RobloxPlayerBeta

Current observed command line:
```
"C:\Users\ellaj\AppData\Local\Roblox\Versions\version-d34359a5577645e2\RobloxPlayerBeta.exe"
```

**No placeId!** ❌

This means:
- ProcessWatcher can't extract placeId from command line
- Even if it detects the process, it doesn't know which game is being launched
- Can't determine if it should be blocked

### How Roblox Passes the PlaceId Instead

Possibilities:
1. **Registry key update** - Roblox writes placeId to a registry key before launching
2. **Environment variables** - Roblox sets env var before launching
3. **Shared config file** - Roblox writes to a file in AppData
4. **Named pipes/IPC** - Roblox communicates via inter-process communication
5. **Shared memory** - Roblox uses shared memory buffer

---

## Solutions

### Option 1: Monitor Registry for PlaceId (BEST for app launches)

**Approach:**
1. Hook registry writes to detect when Roblox sets the target game
2. Extract placeId from registry change
3. Prevent process launch BEFORE it happens

**Advantages:**
- Blocks BEFORE process creates
- No placeId in command line needed
- Works without admin (can use HKCU registry monitoring)

**Disadvantages:**
- Need to find which registry key stores placeId
- Requires real-time registry monitoring
- More complex implementation

---

### Option 2: Force Registry Handler + Monitor Registry Changes

**Approach:**
1. Set protocol handler to RobloxGuard.exe
2. Monitor for registry changes
3. If Roblox changes it, immediately restore our handler
4. This creates a "lock" that Roblox can't bypass

**Advantages:**
- Uses existing protocol handler mechanism
- User-friendly (no new code)
- Works with current architecture

**Disadvantages:**
- Registry change monitoring adds overhead
- Still a race condition (brief window when Roblox handler is active)
- Constant fighting with Roblox updates

---

### Option 3: Scheduled Task Keeps Handler Locked

**Approach:**
1. Create a scheduled task that runs every minute
2. Task checks if protocol handler still points to RobloxGuard
3. If changed, restore it
4. User can enable "Lock Handler" in settings

**Advantages:**
- Simple to implement
- No admin required for first-run check (just registry)
- Can toggle on/off

**Disadvantages:**
- Not real-time
- Adds overhead
- Still depends on us winning the registry race

---

### Option 4: Hook Parent Process (Roblox App Process)

**Approach:**
1. Monitor when Roblox app calls "Play"
2. Intercept BEFORE it spawns RobloxPlayerBeta
3. Extract game ID from app memory or IPC

**Advantages:**
- Intercepts at source
- Guaranteed to catch all launches

**Disadvantages:**
- Requires DLL injection or AppHooking
- Violates our "no injection" principle
- Very complex

---

## Recommended Immediate Fix

### Best Short-Term Solution: Force Registry Handler + Watchdog

**Implementation:**

1. **Create a "Handler Lock" service**
   ```
   RobloxGuard.exe --lock-handler
   ```
   - Runs in background
   - Monitors registry every 5 seconds
   - If protocol handler changed, restores it
   - Shows notification to user if Roblox tries to hijack

2. **Add to Settings UI**
   - "Enable Handler Protection" toggle
   - Shows status: "Handler locked", "Handler hijacked (fixing)", etc.

3. **Auto-start with app**
   - Scheduled task or startup shortcut
   - Keeps handler in place at all times

---

## Recommended Long-Term Solution

### Find and Monitor PlaceId Registry Key

**Investigation needed:**
1. When user clicks "Play" in Roblox app
2. Monitor `HKCU\Software\Roblox\*` for changes
3. Look for any key/value containing the placeId
4. Once found, hook that key
5. Extract placeId and block BEFORE process launch

**Expected location:** Something like:
```
HKCU\Software\Roblox\CurrentGame\PlaceId
HKCU\Software\Roblox\Launcher\TargetPlace
HKCU\Software\Roblox\AppData\SelectedPlace
```

---

## Current Status

### What Works ✅
- Website clicks with protocol handler → BLOCKED
- Configuration system → WORKS
- PIN security → WORKS
- Blocklist management → WORKS

### What Doesn't Work ❌
- Roblox app "Play" button → BYPASSES (registry hijacking)
- ProcessWatcher via WMI → Requires admin + missing placeId in CLI

### Workaround Applied Now
None yet - need to implement one of the solutions above

---

## Technical Investigation Plan

### To Implement PlaceId Registry Key Monitoring:

1. **Set up test environment**
   ```powershell
   # Monitor registry for changes while clicking Play
   reg query HKCU\Software\Roblox /s /v placeId
   # OR use PowerShell:
   Get-Item HKCU:\Software\Roblox -recurse | where{$_.Name -like "*place*"} 
   ```

2. **Find the key**
   - User clicks "Play" in Roblox app
   - Check registry changes in real-time
   - Look for any numeric value that matches the game ID

3. **Hook and monitor**
   - Once key found, create registry change watcher
   - Fire event when value changes
   - Extract placeId and trigger block check

---

## Code Changes Needed

### New Class: RegistryPlaceIdMonitor

```csharp
public class RegistryPlaceIdMonitor
{
    private RegistryKey? _hkcu;
    private uint _regNotifyFilter;
    private SafeHandle? _regKey;
    
    public event EventHandler<PlaceIdDetectedEventArgs>? PlaceIdDetected;
    
    public void StartMonitoring(string registryPath)
    {
        // Use RegNotifyChangeKeyValue to monitor registry changes
        // When placeId registry key changes, extract value
        // Trigger PlaceIdDetected event
    }
}
```

### Alternative: Process Command Line Injection

Modify ProcessWatcher to:
1. Detect RobloxPlayerBeta process
2. Query registry for current placeId
3. Check blocklist
4. Terminate if needed

---

## Summary

### The Fundamental Issue

Roblox re-registers its protocol handler, defeating our registry-based approach. The app also doesn't pass placeId via command line, defeating our process watcher approach.

### The Real Solution

**We need to find where Roblox stores the target game ID** (likely in registry) and monitor that instead of the protocol handler.

### Next Steps

1. **Investigate:** Find the registry key where Roblox stores target placeId
2. **Implement:** Create RegistryPlaceIdMonitor class
3. **Test:** Verify blocking works for app launches
4. **Deploy:** Update installer to include handler lock mechanism

---

## Files to Update

- `ProcessWatcher.cs` - Add fallback to registry placeId detection
- `Program.cs` - Add `--lock-handler` mode
- `SettingsWindow.xaml/cs` - Add "Handler Protection" toggle
- `RobloxGuard.csproj` - May need additional Win32 registry APIs

