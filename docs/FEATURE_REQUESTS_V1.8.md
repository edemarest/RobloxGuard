# Feature Requests & Implementation Strategy (v1.8+)

## Overview
This document outlines feature requests, architectural challenges, and feasible implementation strategies based on RobloxGuard's current out-of-process design.

---

## Feature Request 1: After-Hours Soft Disconnect (Ty's Bedtime)

### Requirements
- **Time Window**: 3:00 AM - 3:30 AM PST (randomized within 30-min window)
- **Frequency**: Daily
- **Probability**: 65% chance to trigger (randomized)
- **Constraint**: Cannot disconnect more than 2 days in a row
  - Day 1: Possible disconnect (65%)
  - Day 2: Possible disconnect (65%)
  - Day 3: **GUARANTEED NO DISCONNECT** (force allow)
  - Day 4: Reset counter, back to 65%

### Current State
- ✅ After-hours enforcement exists: `AfterHoursEnforcementEnabled`, `AfterHoursStartTime` (3 = 3:00 AM)
- ✅ Kill scheduling with random delays: `BlockedGameKillDelayMinutesMin/Max`
- ❌ Soft disconnect (graceful game exit) NOT implemented
- ❌ Consecutive disconnect counter NOT implemented
- ❌ Time-window randomization NOT implemented (only single hour check)

### Challenge: Soft Disconnect vs. Hard Kill

**Current Implementation (Hard Kill):**
```csharp
RobloxRestarter.KillAndRestartToHome()
{
  1. Send WM_CLOSE to game window
  2. Wait 2 seconds (graceful timeout)
  3. If still alive: Force kill process
  4. Auto-restart Roblox to home screen
}
```

**Requested: Soft Disconnect (Graceful Game Exit)**
- User should see "Disconnected from game" message
- Return to Roblox home/games list screen
- NOT force-kill the entire RobloxPlayerBeta.exe process
- Must happen via Roblox internal state, not process termination

**Feasibility Analysis:**

| Method | Feasibility | Risk | Notes |
|--------|-------------|------|-------|
| **WM_CLOSE on Game Window** | Medium | Low | Sends graceful close signal; works sometimes but Roblox may ignore |
| **Simulated Input (Alt+F4)** | Medium | Low | Similar to WM_CLOSE; DLL injection forbidden |
| **Roblox Client API/IPC** | Low | High | No public API; reverse engineering required; fragile |
| **Memory Patching** | Very Low | Very High | DLL injection violation; breaks on updates; unreliable |
| **Custom Protocol Handler** | Low | Medium | Requires intercepting game state; complex integration |
| **Disconnect via LaunchUrl** | Unknown | Medium | May work if Roblox client respects disconnect commands |

**Recommended Approach: Enhanced WM_CLOSE**
```csharp
public void SendGracefulDisconnect()
{
  // 1. Find game window (not main RobloxPlayerBeta window)
  IntPtr gameWindow = FindWindowEx(mainRobloxWindow, IntPtr.Zero, "RobloxWindowClass", null);
  
  // 2. Send WM_CLOSE to game window specifically (not entire process)
  SendMessage(gameWindow, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
  
  // 3. Wait 3-5 seconds for graceful disconnect
  // If window closes: Success (user sees disconnect message)
  // If window persists: Fall back to hard kill
  
  // 4. Monitor for game exit event (via log monitoring)
  // If exit detected: Don't restart; user is now at home screen
}
```

**Fallback:** If WM_CLOSE doesn't trigger disconnect, fall back to hard kill (existing behavior).

---

### Implementation: Consecutive Disconnect Counter

**Storage:**
- Add to `~/.config.json`:
  ```json
  {
    "consecutiveDisconnectDays": 0,
    "lastDisconnectDate": "2025-10-26"
  }
  ```
- Or persist in SessionStateManager

**Logic (PlaytimeTracker.cs):**
```csharp
public bool ShouldTriggerAfterHoursDisconnect()
{
  // 1. Check if we're in 3:00 AM - 3:30 AM window
  var now = DateTime.Now;  // Local time for bedtime
  if (now.Hour == 3 && now.Minute >= 0 && now.Minute < 30)
  {
    // 2. Check consecutive counter
    if (_config.ConsecutiveDisconnectDays >= 2)
    {
      // Force allow today
      LogToFile("After-hours disconnect suppressed (2 days consecutive)");
      _config.LastDisconnectDate = DateTime.Today;
      _config.ConsecutiveDisconnectDays = 0;  // Reset
      return false;
    }
    
    // 3. Roll probability (65%)
    if (Random.Shared.Next(100) < 65)
    {
      // 4. Check if different day from last disconnect
      if (DateTime.Today != _config.LastDisconnectDate)
      {
        _config.ConsecutiveDisconnectDays++;
      }
      _config.LastDisconnectDate = DateTime.Today;
      return true;
    }
  }
  return false;
}
```

**Time-Window Randomization:**
```csharp
// Instead of checking at exactly 3:00 AM, randomize within window
private DateTime _randomizedAfterHoursTime = null;

public void InitializeRandomizedAfterHours()
{
  // Pick random minute within 3:00-3:30 AM window
  var randomMinute = Random.Shared.Next(0, 30);
  var now = DateTime.Now;
  _randomizedAfterHoursTime = new DateTime(now.Year, now.Month, now.Day, 3, randomMinute, 0);
  
  // Re-randomize every day at midnight
}
```

---

### Config Addition
```json
{
  "afterHoursSoftDisconnectEnabled": true,
  "afterHoursSoftDisconnectTime": "03:00",              // 3:00 AM
  "afterHoursSoftDisconnectWindowMinutes": 30,          // 3:00-3:30 AM window
  "afterHoursSoftDisconnectProbability": 65,            // Percent (0-100)
  "afterHoursSoftDisconnectMaxConsecutiveDays": 2,      // Max 2 days in a row
  "softDisconnectGracefulTimeoutMs": 5000               // Wait 5s for disconnect
}
```

---

## Feature Request 2: Inactivity-Based Soft Disconnect (Sol's RNG)

### Requirements
- **Trigger**: After 1-2 hours of inactivity on specific game (RNG/Sol's game)
- **Action**: Soft disconnect (not hard kill)
- **Quiet Hours Exclusion**: NOT active during 3:30 AM - 9:00 AM
  - Prevents interference with scheduled after-hours enforcement
  - Avoids stacking multiple disconnect mechanisms
- **Clarification Needed**: Does "inactivity" mean:
  - No input from user? (Mouse/keyboard)
  - Game state unchanged? (Same location, no movement)
  - Heuristic from logs?

### Current State
- ✅ Playtime tracking: `PlaytimeTracker.cs` already tracks elapsed time
- ❌ Inactivity detection NOT implemented
- ❌ Input monitoring NOT implemented
- ❌ Quiet hours exclusion for inactivity NOT implemented

### Challenge: Inactivity Detection (Out-of-Process)

**Current Limitation:**
- RobloxGuard cannot inject code → cannot hook input events directly
- No direct access to game state (players, coordinates, animations)

**Feasible Options:**

| Method | Feasibility | Complexity | Notes |
|--------|-------------|-----------|-------|
| **Roblox Log Analysis** | Medium | Medium | Parse game state logs; may not show inactivity |
| **Input Hook (User32 API)** | Medium | Medium | Windows API without injection; register low-level keyboard/mouse hooks |
| **Game Window Focus Time** | High | Low | Track if game window has focus; simple heuristic |
| **Network Activity Monitoring** | Low | High | Requires network sniffer; fragile; may miss inactivity |
| **Combination: Focus + Time** | High | Medium | If window loses focus for 1+ hour: Disconnect |

**Recommended Approach: Game Window Focus Tracking**
```csharp
public class InactivityTracker
{
  private DateTime _lastActivityTime = DateTime.UtcNow;
  private IntPtr _lastFocusedWindow = IntPtr.Zero;
  private const int INACTIVITY_MINUTES = 60;  // 1 hour
  
  public void MonitorGameInactivity(long gameBeingTrackedPlaceId)
  {
    // Run in background thread every 10 seconds
    while (_isMonitoring)
    {
      IntPtr foregroundWindow = GetForegroundWindow();
      
      // Check if Roblox game window is in focus
      if (IsRobloxGameWindow(foregroundWindow) && GetGamePlaceId(foregroundWindow) == gameBeingTrackedPlaceId)
      {
        _lastActivityTime = DateTime.UtcNow;
      }
      else if (DateTime.UtcNow - _lastActivityTime > TimeSpan.FromMinutes(INACTIVITY_MINUTES))
      {
        // Inactivity threshold reached
        TriggerSoftDisconnect();
      }
      
      Thread.Sleep(10000);  // Check every 10 seconds
    }
  }
}
```

**Enhanced: Input Hook (No Injection)**
```csharp
// Windows API: Low-level keyboard/mouse hook (no injection required)
[DllImport("user32.dll")]
private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

private const int WH_MOUSE_LL = 14;
private const int WH_KEYBOARD_LL = 13;

public void RegisterInputHook()
{
  // Register global low-level hook for mouse/keyboard
  _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(null), 0);
  _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHookProc, GetModuleHandle(null), 0);
  
  // Any input updates _lastActivityTime
}

private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
{
  if (nCode >= 0)
    _lastActivityTime = DateTime.UtcNow;
  return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
}
```

---

### Quiet Hours Exclusion

**Logic:**
```csharp
public bool ShouldCheckInactivity()
{
  var now = DateTime.Now.TimeOfDay;
  var quietHoursStart = TimeSpan.FromHours(3.5);   // 3:30 AM
  var quietHoursEnd = TimeSpan.FromHours(9);       // 9:00 AM
  
  // Skip inactivity checks during quiet hours
  if (now >= quietHoursStart && now < quietHoursEnd)
  {
    return false;  // Don't trigger inactivity disconnect
  }
  
  return true;
}
```

**Rationale:** After-hours disconnect (3:00-3:30 AM) happens during quiet hours. Stacking inactivity checks could cause:
- Multiple disconnect attempts simultaneously
- Unpredictable behavior
- User confusion

By excluding 3:30-9:00 AM, we give breathing room after after-hours enforcement.

---

### Config Addition
```json
{
  "inactivityDisconnectEnabled": true,
  "inactivityDisconnectGamePlaceId": 0,              // Sol's RNG placeId (to be configured)
  "inactivityDisconnectMinutes": 60,                 // 1-2 hours (configurable)
  "inactivityQuietHoursStart": "03:30",              // Don't trigger 3:30 AM
  "inactivityQuietHoursEnd": "09:00",                // until 9:00 AM
  "inactivityDetectionMethod": "focus_or_input"      // "focus", "input", or "focus_or_input"
}
```

---

## Feature Request 3: Process Naming / Obfuscation

### Requirements
- **Goal**: Make RobloxGuard process name less suspicious
- **Strategy**: Periodically rename executable to match legitimate Windows processes
- **Constraints**: 
  - Cannot break code's ability to detect own process
  - Must be robust to edge cases
  - Should not confuse diagnostics/debugging

### Current State
- ✅ Process detection via PidLockHelper (stores PID, not name)
- ✅ Self-identification: `Process.GetCurrentProcess().ProcessName`
- ❌ Dynamic process renaming NOT implemented
- ❌ Name obfuscation NOT implemented

### Challenge: Dynamic Process Renaming

**Windows Constraints:**
- Process name is derived from executable filename
- Cannot rename running .exe without stopping process
- Renaming requires file system access
- Each rename requires process stop/restart → downtime

**Feasibility Analysis:**

| Approach | Feasibility | Downtime | Complexity | Risk |
|----------|-------------|----------|-----------|------|
| **Fake Process Name in Task List** | Medium | None | Medium | May be detected as spoofing |
| **Hard Link / Junction** | High | <1s | High | Complex, fragile |
| **Clone to Different Name Periodically** | High | 5-10s | High | Watchdog must handle restart |
| **Windows Service (Hidden)** | Medium | None | Very High | Requires admin; defeats per-user install goal |

**Recommended Approach: Clone + Restart Strategy**

```csharp
public class ProcessObfuscation
{
  private static readonly string[] LegitimateProcessNames = new[]
  {
    "svchost.exe",
    "SearchIndexer.exe",
    "Windows.UI.Xaml.dll", // Not executable, but plausible
    "WindowsUpdate.exe",
    "TiWorker.exe",        // Windows Update service
  };
  
  private string _currentExecutablePath;
  private string _randomizedName;
  
  public void RotateProcessName()
  {
    // 1. Every 24 hours, pick new random name
    if (DateTime.UtcNow - _lastNameRotation > TimeSpan.FromHours(24))
    {
      _randomizedName = LegitimateProcessNames[Random.Shared.Next(LegitimateProcessNames.Length)];
      _lastNameRotation = DateTime.UtcNow;
      
      // 2. Clone current executable to new name
      var newPath = Path.Combine(
        Path.GetDirectoryName(_currentExecutablePath),
        _randomizedName
      );
      
      File.Copy(_currentExecutablePath, newPath, overwrite: true);
      
      // 3. Trigger watchdog to restart under new name
      // (Watchdog detects monitor is "missing" and restarts from new path)
      ProcessObfuscation.RestartUnderNewName(newPath);
    }
  }
  
  private static void RestartUnderNewName(string newExecutablePath)
  {
    // 1. Current process starts new one under different name
    Process.Start(newExecutablePath, "--watch");
    
    // 2. Current process gracefully exits
    // (New process takes over monitoring)
    Environment.Exit(0);
  }
}
```

**Edge Cases & Robustness:**

| Edge Case | Challenge | Solution |
|-----------|-----------|----------|
| **Code detects process by name** | Self-identification breaks | Use PidLockHelper (PID-based) instead of name |
| **Multiple clones exist** | Disk space bloat | Clean up old clones; maintain only current + 1 backup |
| **Watchdog restarts during rename** | Race condition | Lock file prevents simultaneous starts |
| **User sees different .exe in Task Manager** | Suspicious pattern | Accept this; best-effort obfuscation |
| **Antivirus detects cloning** | False positive alert | Sign executable; whitelist legitimate paths |

**PID-Based Self-Detection (Already Implemented):**
```csharp
// PidLockHelper.cs - Does NOT rely on process name
public static bool IsMonitorRunning()
{
  try
  {
    var lockContent = File.ReadAllText(_pidLockPath);
    if (int.TryParse(lockContent, out int pidFromFile))
    {
      // Check if process with that PID exists
      var process = Process.GetProcessById(pidFromFile);
      return process != null && process.ProcessName.Contains("RobloxGuard") 
             || process.ProcessName == _randomizedName;  // Accept randomized name
    }
  }
  catch { }
  return false;
}
```

---

## Implementation Priority & Dependencies

### Phase 1: Foundation (v1.8a - Week 1)
- [ ] **Soft Disconnect via WM_CLOSE** (Ty's feature prerequisite)
  - Risk: Medium (may not work on all Roblox versions)
  - Effort: 3-4 hours
  - Blocks: Soft disconnect features
  
- [ ] **Inactivity Detection (Input Hooks)**
  - Risk: Medium (Windows API complexity)
  - Effort: 4-5 hours
  - Blocks: Sol's inactivity feature

### Phase 2: Features (v1.8b - Week 2)
- [ ] **Consecutive Disconnect Counter** (Ty's bedtime)
  - Risk: Low (purely config/logic)
  - Effort: 2-3 hours
  - Depends: Phase 1 soft disconnect
  
- [ ] **Inactivity-Based Disconnect** (Sol's RNG)
  - Risk: Low (uses Phase 1 detection)
  - Effort: 2 hours
  - Depends: Phase 1 inactivity detection

### Phase 3: Obfuscation (v1.8c - Week 3)
- [ ] **Process Name Rotation**
  - Risk: High (complex restart logic)
  - Effort: 5-6 hours
  - Depends: Nothing (standalone feature)
  - Can be deferred to v1.9

---

## Testing Strategy

### Test Scenarios

**Soft Disconnect:**
```
1. Game running
2. Trigger WM_CLOSE on game window
3. Verify: Game exits gracefully (not hard kill)
4. Verify: Roblox shows "Disconnected" message
5. Fallback: If no disconnect after 5s, hard kill
```

**Consecutive Disconnect Counter:**
```
Day 1 @ 3:15 AM: 65% chance disconnect (passes)
Day 2 @ 3:15 AM: 65% chance disconnect (passes)
Day 3 @ 3:15 AM: **Forced allow** (no disconnect)
Day 4 @ 3:15 AM: Reset counter, 65% chance (can disconnect again)
```

**Inactivity Detection:**
```
1. Game running on Sol's RNG
2. No input for 60+ minutes
3. Verify: Soft disconnect triggers
4. During 3:30-9:00 AM: Inactivity check disabled
5. Verify: No disconnect even after 60+ min inactivity
```

**Process Name Rotation:**
```
1. Monitor running as RobloxGuard.exe
2. Trigger name rotation
3. Verify: New clone created (e.g., svchost.exe)
4. Verify: Monitor restarted under new name
5. Verify: PidLockHelper still detects it
6. Verify: Old .exe cleaned up after 24 hours
```

---

## Open Questions & Clarifications

1. **Soft Disconnect Feasibility**: Has this been tested with Roblox? WM_CLOSE may not work on all versions.
2. **Inactivity Definition**: Mouse/keyboard input, or game state analysis?
3. **Quiet Hours 3:30-9:00 AM**: Is this window sufficient, or should it be longer?
4. **Process Naming**: Okay to accept task manager showing different process names over time?
5. **Cloning Overhead**: Is 5-10 second restart acceptable for daily name rotation?

---

## Risk Assessment

| Feature | Complexity | Risk | User Impact | Recommendation |
|---------|-----------|------|-------------|---|
| Soft Disconnect | Medium | Medium | High (more natural) | **Implement** if WM_CLOSE works |
| Consecutive Counter | Low | Low | Medium | **Implement** early |
| Inactivity Detection | Medium | Medium | Medium | **Implement** with input hooks |
| Process Naming | High | High | Low | **Defer to v1.9** (nice-to-have) |

