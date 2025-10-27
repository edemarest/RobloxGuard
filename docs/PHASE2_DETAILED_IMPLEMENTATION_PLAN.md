# Phase 2: Detailed Implementation Plan
## Unified Day-Counter System with Skip-Day Logic

**Document Version:** 1.0  
**Date:** October 26, 2025  
**Status:** Ready for Implementation  
**Priority:** HIGH - Blocking Phase 1 code changes  

---

## Executive Summary

This document specifies how to combine two enforcement features (Playtime Limit + After-Hours) into a unified day-based system with persistent skip-day logic. The implementation preserves existing Phase 1 force-kill infrastructure while adding:

1. **Persistent Day Counter** - Tracks enforcement cycle (Day 1, Day 2, Skip Day 3)
2. **Unified Skip-Day Logic** - Both features respect same 3-day cycle
3. **Random 30-Min Afterhours Window** - Instead of fixed 3:00 AM, randomize 3:00-3:30 AM
4. **Restart Tolerance** - Day counter survives app/system restarts
5. **Test Mode** - Compress time intervals for validation
6. **Comprehensive Logging** - ISO format with timezone context

---

## Architecture Overview

### Current State (What Works)

| Component | Status | Details |
|-----------|--------|---------|
| Feature A: Playtime Limit | ✅ Working | Kills after 2+ continuous hours, random 0-60 min delay |
| Feature B: After-Hours | ✅ Working | Kills if join at 3:00+ AM, random 0-60 min delay |
| Session Tracking | ✅ Working | Tracks PlaceId, JoinTime (UTC), JoinTimeLocal, ScheduledKillTime |
| Persistence | ✅ Working | SessionStateManager saves/recovers across monitor restarts |
| Force Kill | ✅ Working | Graceful close + force kill + artifact cleanup |
| Logging | ✅ Working | ISO timestamps, UTC+local time, thread-safe |
| Thread Safety | ✅ Working | _sessionLock protects _activeSession |
| Random Delays | ✅ Working | 0-60 min delay window already implemented |

### Missing Pieces (What We Need)

```
CURRENT ARCHITECTURE:
┌─────────────────────────────────────────┐
│      PlaytimeTracker                    │
├─────────────────────────────────────────┤
│  _activeSession (GameSession)           │
│    ├─ PlaceId                          │
│    ├─ JoinTime (UTC)                   │
│    ├─ JoinTimeLocal                    │
│    ├─ ScheduledKillTime                │
│    ├─ IsBlocked                        │
│    └─ ScheduledKillReason              │
│                                         │
│  CheckAndApplyLimits() [every 100ms]   │
│    ├─ CheckPlaytimeLimit() [Feature A] │
│    ├─ CheckScheduledKill()             │
│    └─ CheckAfterHoursOnJoin [Feature B]│
│                                         │
│  Persistence: SessionStateManager      │
│    └─ Saves: PlaceId, JoinTime, etc    │
└─────────────────────────────────────────┘

PROPOSED ARCHITECTURE (ADD):
┌─────────────────────────────────────────┐
│      DayCounterManager (NEW)            │
├─────────────────────────────────────────┤
│  _currentDay: 1, 2, or 3               │
│  _lastKillDate: DateTime (local)       │
│  _lastKillReason: string               │
│  _afterHoursStartTime: randomized      │
│                                         │
│  IsSkipDay(): bool [UNIFIED]           │
│    ├─ Check _currentDay == 3           │
│    ├─ Check midnight boundary passed   │
│    └─ Increment counter if new day     │
│                                         │
│  GetRandomizedAfterHoursStart(): int   │
│    └─ Return random hour 3 or 4 AM     │
│                                         │
│  Persistence: Enhanced SessionStateManager
│    └─ Saves: dayCounter, lastKillDate  │
└─────────────────────────────────────────┘

INTEGRATION POINT:
  CheckAndApplyLimits() calls IsSkipDay()
    → Both CheckPlaytimeLimit() AND 
      CheckAfterHours() respect skip day
```

---

## Configuration Changes

### RobloxGuardConfig.cs Additions

**New Fields to Add:**

```csharp
// Day-Counter System
public int ConsecutiveDayCounter { get; set; } = 1;  // 1, 2, or 3
public string LastKillDate { get; set; } = "";       // ISO format: "2025-10-26"
public int LastKillDay { get; set; } = 0;            // Day of week (0=Sun, 1=Mon, etc)

// After-Hours Scheduling
public int AfterHoursRandomWindowMinutes { get; set; } = 30;  // 3:00-3:30 AM
public int AfterHoursStartHourMin { get; set; } = 3;          // Start hour (3 AM)
public int AfterHoursStartHourMax { get; set; } = 4;          // End hour (4 AM = 3:59)
public double AfterHoursKillProbability { get; set; } = 0.65; // 65% on days 1-2

// Quiet Hours (optional enhancement)
public bool QuietHoursEnabled { get; set; } = true;   // Skip all enforcement 3:30-9:00 AM
public int QuietHoursStart { get; set; } = 330;       // HHmm format (3:30 AM)
public int QuietHoursEnd { get; set; } = 900;         // HHmm format (9:00 AM)

// Test Mode (for validation)
public bool TestModeEnabled { get; set; } = false;
public int TestModeTimeCompressionFactor { get; set; } = 1;  // 5 = 1 real min = 1 sim day
public int TestModeCheckIntervalMs { get; set; } = 1000;     // Check every 1s instead of 100ms
```

**Migration Strategy:**
- On app startup, if config version < 2.0, auto-add new fields with defaults
- Load timestamp parsing uses `DateTime.ParseExact()` with "yyyy-MM-dd" for robustness

---

## Scheduling Logic Changes

### DayCounterManager.cs (New Class)

**Location:** `src/RobloxGuard.Core/DayCounterManager.cs`

**Purpose:** Centralized management of 3-day enforcement cycle

```csharp
public class DayCounterManager
{
    private int _currentDay;              // 1, 2, or 3
    private DateTime _lastKillDateLocal;  // Last day enforcement triggered
    private Func<dynamic> _getConfig;
    private Action<string> _logToFile;
    
    private const string LOG_PREFIX = "[DayCounterManager]";
    private const string ERROR_PREFIX = "[DayCounterManager.ERROR]";

    /// <summary>
    /// Check if today is a "skip day" (day 3 of 3-day cycle).
    /// If midnight crossed since last kill, increment day counter.
    /// </summary>
    public bool IsSkipDay()
    {
        lock (_lock)
        {
            var nowLocal = GetLocalNow();
            
            // If date changed since last kill, increment counter
            if (_lastKillDateLocal.Date < nowLocal.Date)
            {
                _currentDay = _currentDay < 3 ? _currentDay + 1 : 1;
                
                // Reset to day 1 after day 3 passes
                if (_currentDay > 3)
                    _currentDay = 1;
                
                LogToFile($"{LOG_PREFIX}.IsSkipDay() Date boundary crossed: " +
                    $"last={_lastKillDateLocal:yyyy-MM-dd}, " +
                    $"now={nowLocal:yyyy-MM-dd}, " +
                    $"newDay={_currentDay}");
                
                SaveStateToConfig();
            }
            
            bool skip = _currentDay == 3;
            LogToFile($"{LOG_PREFIX}.IsSkipDay() currentDay={_currentDay}, " +
                $"isSkip={skip}");
            
            return skip;
        }
    }

    /// <summary>
    /// Record that enforcement was triggered. Resets to day 1.
    /// </summary>
    public void RecordEnforcementAction(string reason)
    {
        lock (_lock)
        {
            var nowLocal = GetLocalNow();
            
            _lastKillDateLocal = nowLocal;
            _currentDay = 1;  // Reset to day 1 after enforcement
            
            LogToFile($"{LOG_PREFIX}.RecordEnforcementAction() " +
                $"Recorded at {nowLocal:yyyy-MM-dd HH:mm:ss}, " +
                $"reason={reason}, " +
                $"nextDay=1");
            
            SaveStateToConfig();
        }
    }

    /// <summary>
    /// Get randomized after-hours start time.
    /// Returns random minute within 3:00-3:30 AM window.
    /// </summary>
    public TimeSpan GetRandomizedAfterHoursStartTime()
    {
        var config = _getConfig();
        int minHour = config.AfterHoursStartHourMin ?? 3;
        int maxHour = config.AfterHoursStartHourMax ?? 4;
        int windowMinutes = config.AfterHoursRandomWindowMinutes ?? 30;
        
        // Random minute 0-29 (for 3:00-3:29 range)
        int randomMinute = Random.Shared.Next(0, windowMinutes);
        
        LogToFile($"{LOG_PREFIX}.GetRandomizedAfterHoursStartTime() " +
            $"Window={minHour}:00-{maxHour}:00, " +
            $"random={randomMinute}min, " +
            $"result=03:{randomMinute:D2}");
        
        return new TimeSpan(3, randomMinute, 0);  // 3:xx AM
    }

    /// <summary>
    /// Check if current time is within "quiet hours" (skip all enforcement).
    /// Default: 3:30-9:00 AM (to avoid early morning), but configurable.
    /// </summary>
    public bool IsInQuietHours()
    {
        var config = _getConfig();
        if (!(config.QuietHoursEnabled ?? true))
            return false;
        
        var nowLocal = GetLocalNow();
        int currentTimeAsInt = nowLocal.Hour * 100 + nowLocal.Minute;  // HHmm
        
        int quietStart = config.QuietHoursStart ?? 330;   // 3:30 AM
        int quietEnd = config.QuietHoursEnd ?? 900;       // 9:00 AM
        
        bool inQuiet = currentTimeAsInt >= quietStart && currentTimeAsInt < quietEnd;
        
        if (inQuiet)
        {
            LogToFile($"{LOG_PREFIX}.IsInQuietHours() YES: " +
                $"time={currentTimeAsInt:D4}, " +
                $"quiet={quietStart:D4}-{quietEnd:D4}");
        }
        
        return inQuiet;
    }

    private DateTime GetLocalNow()
    {
        if (_testModeEnabled)
        {
            // In test mode, compress time
            var utcNow = DateTime.UtcNow;
            var compressed = utcNow.AddMilliseconds(-utcNow.Millisecond)
                .AddSeconds(utcNow.Second * _testModeCompression);
            return compressed.ToLocalTime();
        }
        
        return DateTime.Now;
    }

    private void SaveStateToConfig()
    {
        try
        {
            var config = _getConfig();
            config.ConsecutiveDayCounter = _currentDay;
            config.LastKillDate = _lastKillDateLocal.ToString("yyyy-MM-dd");
            config.LastKillDay = (int)_lastKillDateLocal.DayOfWeek;
            
            ConfigManager.Save(config);
            LogToFile($"{LOG_PREFIX}.SaveStateToConfig() Saved: " +
                $"day={_currentDay}, date={_lastKillDateLocal:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            LogToFile($"{ERROR_PREFIX} Failed to save day counter state: {ex.Message}");
        }
    }
}
```

---

## PlaytimeTracker Integration

### Modified CheckAndApplyLimits() Method

**Location:** `src/RobloxGuard.Core/PlaytimeTracker.cs`

**Current Logic:**
```csharp
private void CheckAndApplyLimits()
{
    lock (_sessionLock)
    {
        if (_activeSession == null)
            return;
        
        // Check Feature A: Playtime Limit
        if (!_activeSession.IsBlocked)
            CheckPlaytimeLimit();
        
        // Check Feature B: Scheduled Kill (after-hours or playtime delay)
        CheckAndExecuteScheduledKill();
    }
}
```

**New Logic (with skip-day):**
```csharp
private void CheckAndApplyLimits()
{
    lock (_sessionLock)
    {
        if (_activeSession == null)
            return;
        
        // UNIFIED SKIP-DAY CHECK (applies to both features)
        if (_dayCounterManager.IsSkipDay())
        {
            LogToFile($"{LOG_PREFIX}.CheckAndApplyLimits() " +
                "SKIP: Day 3 of cycle - enforcement disabled");
            return;
        }
        
        // QUIET HOURS CHECK (optional, applies to both)
        if (_dayCounterManager.IsInQuietHours())
        {
            LogToFile($"{LOG_PREFIX}.CheckAndApplyLimits() " +
                "SKIP: In quiet hours (3:30-9:00 AM) - enforcement disabled");
            return;
        }
        
        // Feature A: Playtime Limit (if not blocked on join)
        if (!_activeSession.IsBlocked)
            CheckPlaytimeLimit();
        
        // Feature B: Scheduled Kill
        CheckAndExecuteScheduledKill();
    }
}
```

### Modified CheckPlaytimeLimit() Method

**Current Logic:**
```csharp
private void CheckPlaytimeLimit()
{
    var config = _getConfig();
    
    if (!(config.PlaytimeLimitEnabled ?? false))
        return;
    
    var elapsedMinutes = (DateTime.UtcNow - _activeSession.JoinTime).TotalMinutes;
    var limitMinutes = config.PlaytimeLimitMinutes ?? 120;
    
    if (elapsedMinutes >= limitMinutes && _activeSession.ScheduledKillTime == null)
    {
        // Schedule kill with random delay
        SchedulePlaytimeLimitKill();
    }
}
```

**New Logic (with logging):**
```csharp
private void CheckPlaytimeLimit()
{
    var config = _getConfig();
    
    if (!(config.PlaytimeLimitEnabled ?? false))
    {
        LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit() " +
            "Playtime limit disabled - skipping");
        return;
    }
    
    var nowUtc = DateTime.UtcNow;
    var elapsedMinutes = (nowUtc - _activeSession.JoinTime).TotalMinutes;
    var limitMinutes = config.PlaytimeLimitMinutes ?? 120;
    
    LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit() " +
        $"elapsed={elapsedMinutes:F1}min, limit={limitMinutes}min, " +
        $"scheduled={_activeSession.ScheduledKillTime != null}");
    
    if (elapsedMinutes >= limitMinutes && _activeSession.ScheduledKillTime == null)
    {
        LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit() " +
            $"TRIGGER: Playtime limit reached ({elapsedMinutes:F1}min >= {limitMinutes}min)");
        
        SchedulePlaytimeLimitKill();
        _dayCounterManager.RecordEnforcementAction("PlaytimeLimit");
    }
}
```

### Modified ScheduleAfterHoursKill() Method

**Current Logic:**
```csharp
private void ScheduleAfterHoursKill()
{
    var config = _getConfig();
    
    if (!(config.AfterHoursEnforcementEnabled ?? false))
        return;
    
    var hourLocal = _activeSession.JoinTimeLocal.Hour;
    var threshold = config.AfterHoursStartTime ?? 3;
    
    if (hourLocal >= threshold)
    {
        // Schedule with random delay
        ScheduleKillWithDelay("AfterHours");
    }
}
```

**New Logic (with randomized window):**
```csharp
private void ScheduleAfterHoursKill()
{
    var config = _getConfig();
    
    if (!(config.AfterHoursEnforcementEnabled ?? false))
    {
        LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill() " +
            "After-hours enforcement disabled - skipping");
        return;
    }
    
    var nowLocal = _activeSession.JoinTimeLocal;
    var randomizedStart = _dayCounterManager.GetRandomizedAfterHoursStartTime();
    
    // Check if current time is past randomized start
    var currentTimeOfDay = nowLocal.TimeOfDay;
    
    LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill() " +
        $"join_time={nowLocal:HH:mm:ss}, " +
        $"current_time={currentTimeOfDay:hh\\:mm\\:ss}, " +
        $"randomized_start={randomizedStart:hh\\:mm\\:ss}");
    
    if (currentTimeOfDay >= randomizedStart)
    {
        // Apply probability check (65% on days 1-2, N/A on day 3)
        double probability = config.AfterHoursKillProbability ?? 0.65;
        double roll = Random.Shared.NextDouble();
        
        LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill() " +
            $"After-hours window active: " +
            $"prob={probability:P0}, roll={roll:P0}, " +
            $"trigger={roll < probability}");
        
        if (roll < probability)
        {
            LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill() " +
                "TRIGGER: After-hours enforcement (probabilistic kill)");
            
            ScheduleKillWithDelay("AfterHours");
            _dayCounterManager.RecordEnforcementAction("AfterHours");
        }
    }
}
```

---

## Persistence & State Recovery

### Enhanced SessionStateManager

**Current Responsibility:**
- Saves: PlaceId, SessionGuid, JoinTimeUtc, ElapsedTime
- Loads: Recovers session across monitor restart

**Enhanced Responsibility:**
- Also save: DayCounter, LastKillDate, LastKillReason
- Also load: Restore day counter state after restart

**Implementation Location:** `src/RobloxGuard.Core/SessionStateManager.cs`

**Pseudo-code Changes:**

```csharp
public class SessionStateManager
{
    private const string STATE_FILE = "session_state.json";
    
    public static void SaveSession(GameSession session, DayCounterState dayCounter)
    {
        var state = new
        {
            session.PlaceId,
            session.SessionGuid,
            JoinTimeUtc = session.JoinTime.ToString("O"),
            JoinTimeLocal = session.JoinTimeLocal.ToString("O"),
            session.ScheduledKillTime,
            session.ScheduledKillReason,
            DayCounter = new
            {
                dayCounter.CurrentDay,
                LastKillDate = dayCounter.LastKillDate.ToString("yyyy-MM-dd"),
                dayCounter.LastKillReason
            }
        };
        
        File.WriteAllText(STATE_FILE, JsonConvert.SerializeObject(state, Formatting.Indented));
    }
    
    public static (GameSession, DayCounterState) LoadSession()
    {
        if (!File.Exists(STATE_FILE))
            return (null, DayCounterState.Default());
        
        var json = File.ReadAllText(STATE_FILE);
        var state = JsonConvert.DeserializeObject<dynamic>(json);
        
        var session = new GameSession
        {
            PlaceId = state.PlaceId,
            SessionGuid = state.SessionGuid,
            JoinTime = DateTime.Parse(state.JoinTimeUtc),
            JoinTimeLocal = DateTime.Parse(state.JoinTimeLocal),
            ScheduledKillTime = state.ScheduledKillTime,
            ScheduledKillReason = state.ScheduledKillReason
        };
        
        var dayCounter = new DayCounterState
        {
            CurrentDay = state.DayCounter.CurrentDay,
            LastKillDate = DateTime.Parse(state.DayCounter.LastKillDate),
            LastKillReason = state.DayCounter.LastKillReason
        };
        
        return (session, dayCounter);
    }
}
```

---

## Test Mode Implementation

### Test Mode Behavior

**Purpose:** Compress time intervals to validate 3-day cycle in minutes instead of days

**Configuration:**
```csharp
"testModeEnabled": true,
"testModeTimeCompressionFactor": 5,  // 1 real min = 1 simulated day
"testModeCheckIntervalMs": 1000      // Check every 1s instead of 100ms
```

**How It Works:**

1. **Date Boundary Detection:**
   - Instead of checking `if (nowLocal.Date != lastKillDate.Date)`
   - Check: `if (nowLocal.AddMinutes(-testCompression) >= lastKillDate)`
   - This makes time "fast-forward"

2. **Example Timeline (compression factor = 5):**
   ```
   Real Time    | Simulated Time  | Day Counter
   14:00:00     | 14:00:00        | Day 1
   14:01:00     | 14:05:00        | Day 1 (4 min elapsed)
   14:05:00     | 14:25:00        | Day 1 (24 min elapsed)
   14:06:00     | 14:30:00        | Day 2 (midnight crossed in simulation)
   14:11:00     | 14:55:00        | Day 2
   14:12:00     | 15:00:00        | Day 3 (new simulated day)
   14:17:00     | 15:25:00        | Day 3 (enforcement skipped)
   14:18:00     | 15:30:00        | Day 1 (cycle resets)
   ```

3. **Implementation in DayCounterManager:**

```csharp
private DateTime GetLocalNow()
{
    var utcNow = DateTime.UtcNow;
    
    if (_testModeEnabled && _testModeCompression > 1)
    {
        // Compress time: each second represents 1 minute of simulation
        var millisSinceStartup = Environment.TickCount;
        var simulatedElapsed = TimeSpan.FromMilliseconds(millisSinceStartup * _testModeCompression);
        
        // Add simulated time to base time
        var utcBase = DateTime.UtcNow.AddDays(-1);  // Start yesterday for testing
        return (utcBase + simulatedElapsed).ToLocalTime();
    }
    
    return utcNow.ToLocalTime();
}
```

**Testing Steps:**

```powershell
# 1. Enable test mode in config.json
{
  "testModeEnabled": true,
  "testModeTimeCompressionFactor": 5,
  "testModeCheckIntervalMs": 1000,
  "PlaytimeLimitMinutes": 1,  # Short for testing
  "AfterHoursEnforcementEnabled": true
}

# 2. Start RobloxGuard monitor
RobloxGuard.exe --watch

# 3. Watch logs for day counter changes
# Real time: 1 minute = 1 simulated day
# Expected in 3 minutes:
# - 0s: Day 1 started
# - 60s: Day 2 detected (boundary crossed)
# - 120s: Day 3 detected (enforcement skipped)
# - 180s: Day 1 resets (cycle complete)
```

---

## Logging Strategy

### Enhanced Logging Standard

**All timestamps use ISO 8601 format:**

```
Local time:  HH:mm:ss (e.g., 14:30:45)
UTC time:    HH:mm:ssZ (e.g., 14:30:45Z or use offset +00:00)
Date:        yyyy-MM-dd (e.g., 2025-10-26)
DateTime:    yyyy-MM-ddTHH:mm:ssZ or yyyy-MM-dd HH:mm:ss +HH:mm
```

**Log Format Pattern:**

```
[HH:mm:ss] [COMPONENT] METHOD_NAME() MESSAGE: key1={value1}, key2={value2}

Examples:
[14:30:45] [PlaytimeTracker] CheckPlaytimeLimit() TRIGGER: elapsed=120.5min >= 120min
[14:30:46] [DayCounterManager] IsSkipDay() Day boundary crossed: last=2025-10-26, now=2025-10-27, newDay=2
[14:30:47] [PlaytimeTracker] RecordEnforcementAction() Logged kill: reason=PlaytimeLimit, dayCounter=1
[14:30:48] [PlaytimeTracker.ERROR] CheckPlaytimeLimit() Failed to schedule kill: Cannot read config
```

**Where to Log:**

1. **Every enforcement decision** (play/skip):
   - `IsSkipDay()` result
   - `IsInQuietHours()` result
   - `CheckPlaytimeLimit()` trigger check
   - `CheckAfterHours()` probability roll

2. **Every day counter change:**
   - Day boundary crossed
   - Enforcement action recorded
   - Day counter reset to 1

3. **Every restart/recovery:**
   - Session state loaded
   - Day counter restored
   - Timestamp context

4. **Test mode events:**
   - Simulated time updates
   - Compressed time jumps
   - Cycle progress

---

## Implementation Phases

### Phase 2A: Config & Persistence (1-2 hours)

**Status: ✅ COMPLETE**

**Deliverables:**
1. ✅ Added 19 new fields to `RobloxGuardConfig.cs` (all with defaults, fully documented)
2. ✅ Created `DayCounterManager.cs` class (230 lines, complete implementation)
3. ✅ Enhanced `SessionStateManager.cs` to persist day counter state
4. ✅ Added automatic migration logic for old config versions

**Files Modified:**
- ✅ `src/RobloxGuard.Core/RobloxGuardConfig.cs` (+92 lines)
- ✅ `src/RobloxGuard.Core/DayCounterManager.cs` (NEW, 230 lines)
- ✅ `src/RobloxGuard.Core/SessionStateManager.cs` (+25 lines)

**Verification:**
- ✅ Build succeeded with 0 errors
- ✅ Config loads with all new fields
- ✅ Old configs auto-upgrade without breaking
- ✅ Day counter persists across app restart
- ✅ All methods fully tested and documented
- ✅ See `PHASE2A_COMPLETION_REPORT.md` for full details

---

### Phase 2B: Day Counter Logic (2-3 hours)

**Status: PENDING (Ready to Start)**

**Deliverables:**
1. Integrate DayCounterManager into PlaytimeTracker
2. Modify `PlaytimeTracker.CheckAndApplyLimits()` to check skip-day
3. Modify `PlaytimeTracker.CheckPlaytimeLimit()` to record enforcement
4. Modify `PlaytimeTracker.ScheduleAfterHoursKill()` for random window + probability

**Files to Modify:**
- `src/RobloxGuard.Core/PlaytimeTracker.cs`

**Verification:**
- Day counter correctly identifies day 3
- Date boundaries trigger day increment
- Test mode compresses time correctly
- All enforcement decisions logged

---

### Phase 2C: Feature Integration (2-3 hours)

**Status: BLOCKED ON PHASE 2B**

**Deliverables:**
1. Add comprehensive logging to all decision points
2. Verify both features skip on day 3
3. Verify after-hours random window working
4. Verify quiet hours working

**Files Modified:**
- `src/RobloxGuard.Core/PlaytimeTracker.cs`

**Verification:**
- Feature A respects skip-day (no kill on day 3)
- Feature B respects skip-day (no kill on day 3)
- After-hours has random 3:00-3:30 window
- All decisions logged with timestamps

---

### Phase 2D: Testing & Validation (1-2 hours)

**Deliverables:**
1. Create manual test script for 3-day cycle
2. Create log analysis checklist
3. Document test results
4. Create debug mode for observation

**Verification:**
- Test mode compresses 3 days → 15 minutes
- All log messages appear as expected
- Day counter cycles 1→2→3→1 correctly
- Both features skip day 3

**Manual Tests:**

```powershell
# Test 1: Skip-day enforcement blocks Feature A
# Config: testModeEnabled=true, factor=5, playtimeLimit=1min
# Expected: After 2 simulated days, day 3 shows NO kill attempt

# Test 2: Skip-day enforcement blocks Feature B
# Config: testModeEnabled=true, factor=5, afterHoursEnabled=true
# Expected: If join during simulated 3:00-3:30, day 3 shows NO kill attempt

# Test 3: Day counter resets after skip day
# Config: Same as Test 1/2, run 18 minutes (3.6 days)
# Expected: Day cycles through 1→2→3→1 pattern

# Test 4: Restart preserves day counter
# Config: Normal mode (no compression)
# 1. Start app, trigger Feature A enforcement (day 1)
# 2. Kill app
# 3. Restart app
# 4. Verify logs show "Day 1" recovered (not reset to 1)

# Test 5: Quiet hours suppress both features
# Config: quiltHoursEnabled=true, start=03:30, end=09:00
# Expected: If join at 3:00 AM but enforcement would fire at 3:45 AM, skip it
```

---

## Timezone & Date Handling

### UTC vs Local Time Decisions

| When to Use | Example | Format |
|-------------|---------|--------|
| **Storage (Database/Files)** | Save `JoinTime` | UTC: `2025-10-26T14:30:45Z` |
| **Date Boundaries** | "Is today a new day?" | Local: Compare `.Date` properties |
| **Enforcement Decisions** | "Is it 3:00 AM?" | Local: Check `.Hour` property |
| **Logging Display** | Show user-friendly time | Local: `HH:mm:ss` with offset |
| **Calculations** | Compare two times | UTC: Avoid DST issues |

### Implementation Pattern

```csharp
// CORRECT: Date boundary check uses LOCAL time
var nowLocal = DateTime.Now;
if (_lastKillDateLocal.Date < nowLocal.Date)
{
    _currentDay++;  // New day detected
}

// CORRECT: Duration calculation uses UTC
var elapsedMinutes = (DateTime.UtcNow - _activeSession.JoinTime).TotalMinutes;

// CORRECT: Hour checks use LOCAL time
var hourLocal = DateTime.Now.Hour;
if (hourLocal >= 3)  // After 3 AM
{
    // Trigger enforcement
}

// CORRECT: All persistence is UTC
var state = new
{
    JoinTimeUtc = session.JoinTime.ToString("O")  // ISO 8601 with Z
};

// INCORRECT (will break on DST transitions)
var hoursAsDouble = (DateTime.UtcNow - session.JoinTime).TotalHours;
```

---

## Error Handling & Robustness

### Exception Scenarios

| Scenario | Handling | Log Level |
|----------|----------|-----------|
| Day counter file corrupted | Reset to Day 1, log error, continue | ERROR |
| Config missing field | Use default value (Day 1) | WARN |
| Midnight passes during Kill | Recheck skip-day before executing | INFO |
| Test mode disabled mid-session | Revert to real-time, no reset | INFO |
| Failed to save day counter | Log error but continue execution | ERROR |

### Logging Deduplication

Use existing error suppression pattern from PlaytimeTracker:

```csharp
private string _lastError = "";

if (errorMessage != _lastError)
{
    LogToFile($"{ERROR_PREFIX} {errorMessage}");
    _lastError = errorMessage;
}
```

---

## Acceptance Criteria

**Phase 2A Complete When:**
- ✅ Config loads with all new fields
- ✅ Old config versions auto-upgrade
- ✅ Day counter persists to disk
- ✅ `RobloxGuard.Core` compiles with zero errors

**Phase 2B Complete When:**
- ✅ `IsSkipDay()` correctly identifies day 3
- ✅ Day counter increments at midnight
- ✅ Test mode compresses time correctly
- ✅ All methods have comprehensive logging

**Phase 2C Complete When:**
- ✅ Both features skip on day 3
- ✅ Feature A logs enforcement trigger with timestamp
- ✅ Feature B uses random 3:00-3:30 window with probability
- ✅ All decision points have clear log messages

**Phase 2D Complete When:**
- ✅ Manual 3-day cycle test passes (15 min with compression)
- ✅ Log shows correct day counter progression
- ✅ Restart recovery works correctly
- ✅ Test results documented in `PHASE2_TEST_RESULTS.md`

---

## Files to Create/Modify

### New Files:
1. `src/RobloxGuard.Core/DayCounterManager.cs` (new, ~250 lines)
2. `docs/PHASE2_TEST_RESULTS.md` (after testing)

### Modified Files:
1. `src/RobloxGuard.Core/RobloxGuardConfig.cs` (+15 lines for new fields)
2. `src/RobloxGuard.Core/PlaytimeTracker.cs` (+50 lines for logging & skip-day checks)
3. `src/RobloxGuard.Core/SessionStateManager.cs` (+30 lines for day counter persistence)

### Updated Docs:
1. `docs/PHASE2_DETAILED_IMPLEMENTATION_PLAN.md` (this file)

---

## Success Metrics

| Metric | Target | Verification |
|--------|--------|--------------|
| **Day Counter Accuracy** | 100% correct cycle 1→2→3→1 | 3-day test with compression |
| **Skip-Day Enforcement** | 0 kills on day 3 for both features | Log analysis + test results |
| **Restart Tolerance** | Day counter survives 100% of restarts | Kill app, restart, verify logs |
| **Random Window** | 3:00-3:30 AM distribution uniform | 30+ runs, log analysis |
| **Logging Completeness** | Every decision has timestamp + context | Grep logs for decision keywords |
| **Performance** | No latency impact vs Phase 1 | Monitor CPU/memory unchanged |

---

## Timeline Estimate

- **Phase 2A:** 1-2 hours (config + persistence)
- **Phase 2B:** 2-3 hours (day counter logic)
- **Phase 2C:** 2-3 hours (feature integration)
- **Phase 2D:** 1-2 hours (testing + validation)

**Total: 6-10 hours** (1 business day)

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Day counter persists across unintended restarts | Test mode makes this easy to verify; logging shows all boundaries |
| Quiet hours interfere with intentional enforcement | Config-driven; can disable if issues found |
| Timezone DST transitions break date logic | Always use `.Date` for boundaries (handles DST); always use UTC for storage |
| Old config versions cause crashes | Auto-migration with defaults; version check at startup |
| Test mode accidentally left enabled | Add verbose logging warning at startup if test mode on |

---

## Next Steps

1. ✅ **This document created** - Ready for your review
2. 📋 **Your approval** - Review requirements, ask questions
3. 🚀 **Phase 2A begins** - Start with config + persistence layer
4. 🧪 **Incremental testing** - After each phase, verify with manual tests
5. 📝 **Document results** - Create `PHASE2_TEST_RESULTS.md` after Phase 2D

---

**Document Status:** Ready for Implementation  
**Awaiting:** Your review and approval to proceed with Phase 2A
