# Auto-Start LogMonitor Implementation Plan

**Goal:** When user clicks RobloxGuard.exe (no arguments), auto-start the LogMonitor in the background without showing console window.

**Complexity:** ⭐⭐ (Medium - 2-3 hours of implementation)

**User Experience After Implementation:**
```
1. User installs RobloxGuard
2. User double-clicks RobloxGuard.exe shortcut (from Start menu or Desktop)
3. Small notification: "RobloxGuard monitoring is running"
4. EXE closes/minimizes to tray (no window)
5. LogMonitor runs silently in background
6. Game blocking happens seamlessly
7. Block UI appears when needed
```

---

## Part 1: CURRENT STATE ANALYSIS

### Current Behavior (v1.0.0)

```csharp
static void Main(string[] args)
{
    if (args.Length == 0)
    {
        ShowHelp();  // ❌ Shows help and exits
        return;
    }
    
    // ... handle CLI modes
}
```

**Problem:** When user clicks EXE with no args, shows console help and exits.

**Current Workaround:** User must run terminal command:
```powershell
RobloxGuard.exe --monitor-logs
```

---

## Part 2: IMPLEMENTATION STRATEGY

### Option A: RECOMMENDED - Hybrid Approach (Best UX)

When EXE is clicked with no arguments:

```csharp
static void Main(string[] args)
{
    if (args.Length == 0)
    {
        // NEW LOGIC: Auto-detect what to do
        
        // 1. Check if already installed
        if (!InstallerHelper.IsInstalled())
        {
            // First time - show setup
            PerformInstall();
            return;
        }
        
        // 2. Check if monitor is already running
        if (IsMonitorAlreadyRunning())
        {
            // Monitor already active - show settings
            ShowSettingsUI();
            return;
        }
        
        // 3. Monitor not running - start it in background
        StartMonitorInBackground();
        return;
    }
    
    // ... existing CLI mode handling
}
```

**Behavior:**
- ✅ First install: Show Settings UI for PIN setup
- ✅ Already running: Show Settings UI for management
- ✅ Not running: Start monitor silently in background

---

### Option B: Pure Auto-Start (Simpler)

Just always start monitor in background:

```csharp
static void Main(string[] args)
{
    if (args.Length == 0)
    {
        // Check if already running
        if (!IsMonitorAlreadyRunning())
            StartMonitorInBackground();
        
        // Exit immediately (no console window)
        return;
    }
    
    // ... existing CLI mode handling
}
```

**Pros:** Simple, clean
**Cons:** Users won't know if it's running

---

## Part 3: DETAILED IMPLEMENTATION

### Step 1: Detect If Monitor Already Running

Create a new helper class: `MonitorStateHelper.cs`

```csharp
namespace RobloxGuard.Core;

/// <summary>
/// Helpers to detect if LogMonitor is already running.
/// </summary>
public static class MonitorStateHelper
{
    private static readonly string MutexName = "Global\\RobloxGuardLogMonitor";
    
    /// <summary>
    /// Check if LogMonitor is currently running in background.
    /// </summary>
    public static bool IsMonitorRunning()
    {
        try
        {
            // Try to open the mutex that LogMonitor creates
            // If it exists and is held, monitor is running
            using var mutex = Mutex.OpenExisting(MutexName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Mutex doesn't exist - monitor not running
            return false;
        }
        catch
        {
            // Error checking - assume not running
            return false;
        }
    }
}
```

**Why This Works:**
- LogMonitor already creates a `Mutex` to enforce single instance
- We can check if mutex exists without acquiring it
- No filesystem access needed
- Cross-process detection works

---

### Step 2: Start Monitor in Background (No Console)

```csharp
/// <summary>
/// Starts LogMonitor in a background process (no console window).
/// </summary>
private static void StartMonitorInBackground()
{
    try
    {
        string appExePath = GetApplicationPath();
        
        // Create process info
        var psi = new ProcessStartInfo
        {
            FileName = appExePath,
            Arguments = "--monitor-logs",
            UseShellExecute = true,
            CreateNoWindow = true,  // ✅ Hide console window
            WindowStyle = ProcessWindowStyle.Hidden,  // ✅ No window
        };
        
        // Start process
        var process = Process.Start(psi);
        
        // Don't wait - fire and forget
        // Process will run independently
        
        Console.WriteLine("✓ RobloxGuard monitoring started in background");
        System.Threading.Thread.Sleep(500);  // Brief pause so user sees message
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Failed to start monitor: {ex.Message}");
        System.Threading.Thread.Sleep(2000);  // Show error message briefly
    }
}

private static string GetApplicationPath()
{
    string appExePath = Path.Combine(AppContext.BaseDirectory, "RobloxGuard.exe");
    if (!File.Exists(appExePath))
    {
        appExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
    }
    if (string.IsNullOrEmpty(appExePath) || !File.Exists(appExePath))
    {
        throw new InvalidOperationException("Could not determine application path");
    }
    return appExePath;
}
```

**Key Points:**
- `CreateNoWindow = true` ✅ Hides console
- `WindowStyle = Hidden` ✅ Suppresses window creation
- `UseShellExecute = true` ✅ Runs independently
- Process detaches and runs separately
- Parent process exits immediately

---

### Step 3: Updated Main Method

```csharp
[STAThread]
static void Main(string[] args)
{
    if (args.Length == 0)
    {
        // Auto-detect mode
        
        // First time setup?
        if (!InstallerHelper.IsInstalled())
        {
            Console.WriteLine("First run - performing installation...");
            PerformInstall();
            
            // After install, ask if they want to start monitoring
            Console.WriteLine("\nWould you like to start RobloxGuard monitoring? (Y/n)");
            var response = Console.ReadLine()?.ToLowerInvariant() ?? "y";
            
            if (response != "n")
            {
                StartMonitorInBackground();
            }
            return;
        }
        
        // Monitor already running?
        if (MonitorStateHelper.IsMonitorRunning())
        {
            Console.WriteLine("✓ RobloxGuard is already monitoring in the background");
            Console.WriteLine("Opening settings...");
            System.Threading.Thread.Sleep(1500);
            ShowSettingsUI();
            return;
        }
        
        // Start monitor in background
        Console.WriteLine("Starting RobloxGuard monitoring...");
        StartMonitorInBackground();
        return;
    }

    var command = args[0].ToLowerInvariant();

    switch (command)
    {
        case "--handle-uri":
            HandleProtocolUri(args.Length > 1 ? args[1] : null);
            break;
        
        // ... rest of CLI modes unchanged
        
        default:
            Console.WriteLine($"Unknown command: {command}");
            ShowHelp();
            break;
    }
}
```

---

## Part 4: USER EXPERIENCE FLOW

### Scenario 1: First Install

```
User double-clicks RobloxGuard.exe
        ↓
App detects: Not installed
        ↓
Shows: "First run - performing installation..."
        ↓
Registers protocol handler
Creates config.json
        ↓
Shows: "Would you like to start RobloxGuard monitoring? (Y/n)"
        ↓
User types: Y
        ↓
App shows: "Starting RobloxGuard monitoring..."
        ↓
Launches monitor in background (hidden)
        ↓
App exits (no console visible)
        ↓
Monitor runs silently in background ✅
        ↓
If user clicks blocked game:
Block UI appears immediately ✅
```

---

### Scenario 2: Already Installed, Monitor Not Running

```
User double-clicks RobloxGuard.exe
        ↓
App detects: Installed, monitor NOT running
        ↓
Shows: "Starting RobloxGuard monitoring..."
        ↓
Launches monitor in background (hidden)
        ↓
App exits (no console visible)
        ↓
Monitor runs silently in background ✅
```

---

### Scenario 3: Already Installed, Monitor Already Running

```
User double-clicks RobloxGuard.exe
        ↓
App detects: Installed, monitor ALREADY running
        ↓
Shows: "✓ RobloxGuard is already monitoring"
Shows: "Opening settings..."
        ↓
Settings window appears (WPF, professional)
        ↓
User can: Edit blocklist, change PIN, view status
        ↓
User closes settings
        ↓
App exits
```

---

## Part 5: TECHNICAL REQUIREMENTS

### New Code to Add

1. **MonitorStateHelper.cs** (New Class)
   - Lines: ~40
   - Dependencies: System.Threading
   - Function: Detect if monitor is running

2. **Program.cs** (Modifications)
   - Add: `StartMonitorInBackground()` method (~50 lines)
   - Modify: `Main()` method (~40 lines added)
   - Add: Imports for ProcessStartInfo, Mutex

### Changes Required

```csharp
// Top of Program.cs - add imports
using System.Diagnostics;
using System.Threading;

// New method (~50 lines)
private static void StartMonitorInBackground() { ... }

// New method (~20 lines)
private static string GetApplicationPath() { ... }

// Modify Main() - replace args.Length == 0 block (~40 lines)
```

**Total lines of code:** ~150 lines added/modified
**Time to implement:** 1-2 hours
**Testing time:** 30 minutes

---

## Part 6: IMPLEMENTATION CHECKLIST

```
Phase 1: Create MonitorStateHelper
[ ] Create RobloxGuard.Core/MonitorStateHelper.cs
[ ] Add IsMonitorRunning() method with mutex detection
[ ] Add unit tests (optional)
[ ] Build and verify no errors

Phase 2: Update Program.cs
[ ] Add StartMonitorInBackground() method
[ ] Add GetApplicationPath() helper
[ ] Update Main() entry point logic
[ ] Remove old ShowHelp() when args.Length == 0
[ ] Build and verify no errors

Phase 3: Testing
[ ] Test: First install flow
[ ] Test: Monitor already running flow
[ ] Test: Monitor not running flow
[ ] Test: Settings UI opens when monitor running
[ ] Test: Block UI appears during actual game block
[ ] Verify: No console windows appear

Phase 4: Documentation & Commit
[ ] Update README with new user flow
[ ] Update help text
[ ] Commit with clear message
[ ] Push to main
```

---

## Part 7: EDGE CASES TO HANDLE

### 1. Monitor Process Crashes

**Problem:** Monitor dies, but app thinks it's still running

**Solution:**
```csharp
public static bool IsMonitorRunning()
{
    try
    {
        // Check mutex
        using var mutex = Mutex.OpenExisting(MutexName);
        
        // Also check: Is process actually running?
        var processes = Process.GetProcessesByName("RobloxGuard");
        if (processes.Length == 0)
            return false;
        
        return true;
    }
    catch
    {
        return false;
    }
}
```

---

### 2. Multiple Instances of App Launched

**Problem:** User double-clicks EXE twice, starts monitor twice

**Solution:**
LogMonitor already has mutex enforcement - only one can run.
If second monitor tries to start, mutex acquisition fails gracefully.

---

### 3. Monitor Locked Log Files

**Problem:** If Roblox is actively writing logs, LogMonitor might fail

**Solution:**
Already handled in LogMonitor - uses FileShare.ReadWrite for file access.

---

### 4. User on System Without Roblox Installed

**Problem:** No %LOCALAPPDATA%\Roblox\logs directory

**Solution:**
LogMonitor creates directory if needed. Already implemented in code.

---

## Part 8: ROLLBACK PLAN

If auto-start causes issues, easy to revert:

```csharp
// Temporary revert to original behavior
static void Main(string[] args)
{
    if (args.Length == 0)
    {
        ShowHelp();  // Original behavior
        return;
    }
    // ... rest unchanged
}
```

Or provide CLI override:
```csharp
// Allow --help even with auto-start
if (args.Length == 0 && !ShouldAutoStart())
{
    ShowHelp();
    return;
}
```

---

## Part 9: COMPARISON: CURRENT vs AFTER

### Current v1.0.0 (Before Auto-Start)

```
User: "How do I use RobloxGuard?"

Answer:
1. Install via EXE
2. Set PIN via settings
3. Open terminal
4. Run: RobloxGuard.exe --monitor-logs
5. Keep terminal open
6. Games will be blocked
```

**Complexity:** HIGH ❌ (Requires terminal knowledge)

---

### After Auto-Start Implementation

```
User: "How do I use RobloxGuard?"

Answer:
1. Double-click installer
2. Press Y to start monitoring
3. Done!

To manage blocklist:
- Double-click RobloxGuard.exe shortcut
- Edit blocklist in settings
- Close settings
```

**Complexity:** LOW ✅ (Parent-friendly)

---

## Part 10: SUCCESS CRITERIA

✅ **Implementation Complete When:**

1. Clicking EXE with no args starts monitor in background
2. No console window appears
3. Monitor runs independently after app exits
4. Already-running detection works
5. Settings UI opens when monitor running
6. Block UI appears for blocked games
7. All tests pass (33/33)
8. No build errors
9. Documentation updated
10. Commit pushed to main

---

## Part 11: ESTIMATED TIMELINE

| Task | Time | Difficulty |
|------|------|-----------|
| Create MonitorStateHelper | 30 min | Easy |
| Update Program.cs Main() | 45 min | Medium |
| Handle edge cases | 30 min | Medium |
| Testing & debugging | 45 min | Medium |
| Documentation | 20 min | Easy |
| **TOTAL** | **~3 hours** | ⭐⭐ |

---

## Part 12: NEXT STEPS

1. Approve implementation approach
2. Create MonitorStateHelper.cs
3. Update Program.cs
4. Test all three scenarios
5. Commit and push
6. Verify GitHub Actions passes
7. Move to v2.0 Sprint 2: Tray Icon

---

## Summary

**What:** Auto-start LogMonitor when EXE clicked with no arguments

**How:** 
- Detect if monitor already running (via mutex)
- Launch monitor in background with hidden window
- Automatically route user to settings if monitoring active

**Result:**
- ✅ Parent installs → Monitor auto-starts
- ✅ No terminal commands needed
- ✅ Seamless, invisible operation
- ✅ Professional user experience

**Effort:** ~3 hours implementation + 30 min testing

**Ready to proceed?**

