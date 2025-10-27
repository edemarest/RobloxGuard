# RobloxGuard v1.8+ Implementation Roadmap

## Structured Deliverables & Architecture Integration

### Feature 1: Soft Disconnect for After-Hours (Ty's Bedtime)

**EPIC:** After-hours bedtime enforcement with graceful disconnect
**Status:** Not Started
**Priority:** High (user-facing feature)

---

#### Task 1.1: Research WM_CLOSE Effectiveness
**Effort:** 2-3 hours
**Owner:** Architecture investigation
**Description:**
- Test if `WM_CLOSE` message to game window triggers Roblox disconnect
- Determine fallback behavior (does it hard-kill? Ignore message?)
- Identify Roblox window class names and message routing
- Document findings in `docs/roblox_disconnect_research.md`

**Acceptance Criteria:**
- [ ] WM_CLOSE behavior tested on Roblox game
- [ ] Window class name documented
- [ ] Fallback behavior defined
- [ ] Decision: Implement WM_CLOSE or explore alternatives

---

#### Task 1.2: Implement Soft Disconnect Method
**Effort:** 3-4 hours
**Owner:** RobloxRestarter.cs
**Depends on:** Task 1.1
**Description:**
- Create new method: `RobloxRestarter.SendSoftDisconnect()`
- Find game window (not main process window)
- Send WM_CLOSE message
- Wait 5 seconds for graceful disconnect
- Fallback to hard kill if no disconnect

**Files to Modify:**
- `src/RobloxGuard.Core/RobloxRestarter.cs` (add SoftDisconnect method)

**Code Location:**
```csharp
namespace RobloxGuard.Core
{
  public class RobloxRestarter
  {
    // NEW:
    public static void SendSoftDisconnect()
    {
      // Implementation here
    }
    
    // MODIFY:
    public static void KillAndRestartToHome()
    {
      // Currently hard-kills; should call SoftDisconnect first
    }
  }
}
```

**Acceptance Criteria:**
- [ ] SoftDisconnect method compiles
- [ ] Finds game window correctly
- [ ] Sends WM_CLOSE without errors
- [ ] Fallback to hard kill after 5s timeout
- [ ] Logging shows disconnect attempt

---

#### Task 1.3: Implement Consecutive Disconnect Counter
**Effort:** 2-3 hours
**Owner:** Config + PlaytimeTracker.cs
**Depends on:** Nothing (independent)
**Description:**
- Add config fields: `consecutiveDisconnectDays`, `lastDisconnectDate`
- Implement counter logic in PlaytimeTracker
- Enforce rule: Max 2 days in a row, 3rd day forced allow
- Reset counter on successful disconnect

**Files to Modify:**
- `src/RobloxGuard.Core/Models/Config.cs` (add fields)
- `src/RobloxGuard.Core/PlaytimeTracker.cs` (add ShouldTriggerAfterHoursDisconnect method)

**Code Location:**
```csharp
// Config.cs - NEW fields:
public int ConsecutiveDisconnectDays { get; set; } = 0;
public DateTime LastDisconnectDate { get; set; }

// PlaytimeTracker.cs - NEW method:
public bool ShouldTriggerAfterHoursDisconnect()
{
  // Check if in 3:00-3:30 AM window
  // Check consecutive counter (block on day 3)
  // Roll 65% probability
  // Update counter and date
  // Return decision
}
```

**Acceptance Criteria:**
- [ ] Config fields serialize/deserialize correctly
- [ ] Counter increments on disconnect
- [ ] Day 3 is guaranteed no-disconnect
- [ ] Logging shows counter state each check
- [ ] Counter resets after day 3 pass

---

#### Task 1.4: Add Randomized Time Window
**Effort:** 1-2 hours
**Owner:** PlaytimeTracker.cs
**Depends on:** Task 1.3
**Description:**
- Instead of checking at exactly 3:00 AM, randomize within 3:00-3:30 AM
- Randomize once per day at midnight
- Store randomized time in memory

**Code Location:**
```csharp
// PlaytimeTracker.cs - NEW:
private DateTime _randomizedAfterHoursTime = null;

public void InitializeRandomizedAfterHours()
{
  var randomMinute = Random.Shared.Next(0, 30);
  var now = DateTime.Now;
  _randomizedAfterHoursTime = new DateTime(now.Year, now.Month, now.Day, 3, randomMinute, 0);
}
```

**Acceptance Criteria:**
- [ ] Time randomized within 3:00-3:30 AM window
- [ ] New random time each day
- [ ] Logging shows selected time
- [ ] Disconnect check uses randomized time

---

#### Task 1.5: Config Schema Update
**Effort:** 30 minutes
**Owner:** Config.cs + config.json template
**Depends on:** Task 1.3
**Description:**
- Add new config fields to schema
- Update `config.example.json` with documentation
- Add defaults to Config.cs

**New Config Fields:**
```json
{
  "afterHoursSoftDisconnectEnabled": true,
  "afterHoursSoftDisconnectTime": "03:00",
  "afterHoursSoftDisconnectWindowMinutes": 30,
  "afterHoursSoftDisconnectProbability": 65,
  "afterHoursSoftDisconnectMaxConsecutiveDays": 2,
  "softDisconnectGracefulTimeoutMs": 5000
}
```

**Acceptance Criteria:**
- [ ] Config schema updated
- [ ] All new fields have defaults
- [ ] Example config includes documentation

---

### Feature 2: Inactivity-Based Disconnect (Sol's RNG)

**EPIC:** Auto-disconnect after extended inactivity on specific game
**Status:** Not Started
**Priority:** High (user-facing feature)

---

#### Task 2.1: Implement Input Monitoring (Low-Level Hooks)
**Effort:** 4-5 hours
**Owner:** New file: `src/RobloxGuard.Core/InputMonitor.cs`
**Depends on:** Nothing
**Description:**
- Register Windows low-level keyboard and mouse hooks
- Track last activity time (any input updates timestamp)
- No DLL injection (uses Windows API directly)
- Safe cleanup on process exit

**Files to Create:**
- `src/RobloxGuard.Core/InputMonitor.cs` (new class)

**Code Skeleton:**
```csharp
namespace RobloxGuard.Core
{
  public class InputMonitor
  {
    private DateTime _lastActivityTime = DateTime.UtcNow;
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);
    
    public void Start()
    {
      // Register mouse and keyboard hooks
      // Hooks call OnInputDetected()
    }
    
    private void OnInputDetected()
    {
      _lastActivityTime = DateTime.UtcNow;
    }
    
    public TimeSpan GetInactivityDuration()
    {
      return DateTime.UtcNow - _lastActivityTime;
    }
  }
}
```

**Acceptance Criteria:**
- [ ] Hooks register without errors
- [ ] _lastActivityTime updates on mouse/keyboard input
- [ ] No DLL injection used
- [ ] Graceful cleanup on exit
- [ ] Logging shows inactivity duration periodically

---

#### Task 2.2: Implement Inactivity-Based Disconnect
**Effort:** 2-3 hours
**Owner:** PlaytimeTracker.cs
**Depends on:** Task 2.1
**Description:**
- Monitor specific game (Sol's RNG by placeId)
- After configured inactivity (1-2 hours), trigger soft disconnect
- Skip during quiet hours (3:30-9:00 AM)
- Reuse soft disconnect from Feature 1

**Code Location:**
```csharp
// PlaytimeTracker.cs - NEW method:
public bool ShouldTriggerInactivityDisconnect()
{
  // Skip during quiet hours (3:30-9:00 AM)
  // Check if current game is Sol's RNG (placeId match)
  // Get inactivity duration from InputMonitor
  // If >= configured minutes: Return true
}
```

**Acceptance Criteria:**
- [ ] Inactivity duration calculated correctly
- [ ] Quiet hours (3:30-9:00 AM) properly skipped
- [ ] Only triggers on configured placeId
- [ ] Uses soft disconnect from Feature 1
- [ ] Logging shows inactivity check state

---

#### Task 2.3: Config Schema Update
**Effort:** 30 minutes
**Owner:** Config.cs + config.json template
**Depends on:** Task 2.2
**Description:**
- Add new config fields for inactivity disconnect
- Document quiet hours rationale
- Add defaults

**New Config Fields:**
```json
{
  "inactivityDisconnectEnabled": true,
  "inactivityDisconnectGamePlaceId": 0,
  "inactivityDisconnectMinutes": 60,
  "inactivityQuietHoursStart": "03:30",
  "inactivityQuietHoursEnd": "09:00",
  "inactivityDetectionMethod": "focus_or_input"
}
```

**Acceptance Criteria:**
- [ ] Config schema updated
- [ ] All new fields have defaults
- [ ] Quiet hours documented in comments

---

### Feature 3: Process Name Obfuscation (Advanced)

**EPIC:** Periodically rename executable to blend in with system processes
**Status:** Not Started
**Priority:** Low (nice-to-have; can defer to v1.9)
**Risk:** High (complex restart logic)

---

#### Task 3.1: Research Process Cloning Strategy
**Effort:** 2-3 hours
**Owner:** Architecture investigation
**Description:**
- Investigate hard link vs. file copy approach
- Test PID-based detection robustness
- Document restart sequence safety
- Identify edge cases (antivirus, file permissions)

**Output:** `docs/process_obfuscation_research.md`

**Acceptance Criteria:**
- [ ] Cloning approach evaluated
- [ ] Restart sequence documented
- [ ] Edge cases identified
- [ ] Decision: Proceed or defer

---

#### Task 3.2: Implement Process Cloning & Restart
**Effort:** 5-6 hours
**Owner:** New file: `src/RobloxGuard.Core/ProcessObfuscation.cs`
**Depends on:** Task 3.1 + Phase 1 completion
**Description:**
- Daily rotation of process name (24-hour interval)
- Clone .exe to new name in same directory
- Restart monitor under new name
- Clean up old clones
- Ensure PidLockHelper still detects new process

**Files to Create:**
- `src/RobloxGuard.Core/ProcessObfuscation.cs` (new class)

**Acceptance Criteria:**
- [ ] Process clones successfully
- [ ] Restart under new name works
- [ ] PidLockHelper detects new process via PID
- [ ] Old clones cleaned up after 24 hours
- [ ] No race conditions during restart

---

#### Task 3.3: Update PidLockHelper Robustness
**Effort:** 1-2 hours
**Owner:** PidLockHelper.cs
**Depends on:** Task 3.2
**Description:**
- Ensure PID-based detection works even if process name changes
- Remove any name-based assertions
- Test with obfuscated names

**Code Location:**
```csharp
// PidLockHelper.cs - UPDATE:
public static bool IsMonitorRunning()
{
  // Should use PID, not name
  // Accept any process name under that PID
}
```

**Acceptance Criteria:**
- [ ] Detection works with any process name
- [ ] No name-based assertions fail
- [ ] Logging shows PID matches (ignores name)

---

## Cross-Feature Dependencies

```
Feature 1 (After-Hours)
├── Task 1.1 (WM_CLOSE Research) [MUST COMPLETE FIRST]
├── Task 1.2 (Soft Disconnect Implementation) [Depends: 1.1]
├── Task 1.3 (Consecutive Counter) [Independent]
├── Task 1.4 (Time Randomization) [Depends: 1.3]
└── Task 1.5 (Config Update) [Depends: 1.3]

Feature 2 (Inactivity)
├── Task 2.1 (Input Monitoring) [Independent]
├── Task 2.2 (Inactivity Logic) [Depends: 2.1, 1.2 (soft disconnect)]
└── Task 2.3 (Config Update) [Depends: 2.2]

Feature 3 (Process Obfuscation)
├── Task 3.1 (Research) [Independent, CAN DEFER]
├── Task 3.2 (Implementation) [Depends: 3.1, Phase 1 complete]
└── Task 3.3 (PidLockHelper Update) [Depends: 3.2]
```

**Critical Path:** 1.1 → 1.2 → 2.2 → Feature 1 Complete
**Parallel Track:** 2.1 (can run during Feature 1)
**Deferrable:** Feature 3 (v1.9+)

---

## Implementation Phases

### Phase 1: After-Hours Soft Disconnect (Week 1)
- [ ] Complete Task 1.1: WM_CLOSE research
- [ ] Complete Task 1.2: Soft disconnect implementation
- [ ] Complete Task 1.3: Consecutive counter logic
- [ ] Complete Task 1.4: Time randomization
- [ ] Complete Task 1.5: Config schema update
- **Milestone:** After-hours enforcement with soft disconnect

### Phase 2: Inactivity Detection (Week 2)
- [ ] Complete Task 2.1: Input monitoring (parallel with Phase 1)
- [ ] Complete Task 2.2: Inactivity logic
- [ ] Complete Task 2.3: Config schema update
- **Milestone:** Sol's RNG inactivity detection

### Phase 3: Process Obfuscation (Week 3 - OPTIONAL)
- [ ] Complete Task 3.1: Research strategy
- [ ] Complete Task 3.2: Implementation
- [ ] Complete Task 3.3: PidLockHelper robustness
- **Milestone:** Daily process name rotation
- **Note:** Can be deferred to v1.9 if time/resources constrained

---

## Testing & Validation

### Phase 1 Test Plan
```
[Test Case 1.1] After-Hours Disconnect Trigger
Setup: Current time = 3:15 AM
Trigger: PlaytimeTracker checks for after-hours disconnect
Expected: ShouldTriggerAfterHoursDisconnect() returns true/false based on 65% probability

[Test Case 1.2] Consecutive Day Counter
Setup: Simulate 3 consecutive days @ 3:15 AM with disconnect enabled
Day 1: Expect disconnect (65% chance)
Day 2: Expect disconnect (65% chance)
Day 3: Expect NO disconnect (forced allow)
Day 4: Expect reset counter, 65% chance
Expected: Counter correctly blocks day 3, resets day 4

[Test Case 1.3] Randomized Time Window
Setup: Initialize PlaytimeTracker at midnight
Expected: Random time selected between 3:00-3:30 AM
Expected: Same time used all day, new time at next midnight

[Test Case 1.4] Soft Disconnect Graceful
Setup: Game running, trigger soft disconnect
Expected: WM_CLOSE sent to game window
Expected: Game exits gracefully (shows disconnect message)
Expected: If no exit after 5s, fallback to hard kill
```

### Phase 2 Test Plan
```
[Test Case 2.1] Input Monitoring
Setup: InputMonitor running, no user input
Expected: _lastActivityTime remains constant
Expected: Any mouse/keyboard input updates _lastActivityTime
Expected: GetInactivityDuration() returns accurate duration

[Test Case 2.2] Inactivity Disconnect on Sol's RNG
Setup: Game = Sol's RNG, no input for 60+ minutes, time = 10:00 AM
Expected: ShouldTriggerInactivityDisconnect() returns true
Expected: Soft disconnect triggers

[Test Case 2.3] Quiet Hours Skip
Setup: Game = Sol's RNG, no input for 60+ minutes, time = 4:00 AM
Expected: ShouldTriggerInactivityDisconnect() returns false (quiet hours)
Expected: No disconnect even though inactivity threshold met

[Test Case 2.4] Quiet Hours Boundary
Setup: Time transitions from 3:29 AM to 3:30 AM
Expected: Quiet hours check starts
Setup: Time transitions from 8:59 AM to 9:00 AM
Expected: Quiet hours check ends
```

### Phase 3 Test Plan (Optional)
```
[Test Case 3.1] Process Cloning
Setup: Monitor running as RobloxGuard.exe
Trigger: 24-hour interval reached
Expected: New .exe created (e.g., svchost.exe)
Expected: Monitor restarts under new name
Expected: No downtime (~5-10 seconds acceptable)

[Test Case 3.2] PID Detection Robustness
Setup: Monitor under new name
Expected: PidLockHelper.IsMonitorRunning() returns true
Expected: Detection based on PID, not name

[Test Case 3.3] Old Clone Cleanup
Setup: Multiple clones exist from previous days
Expected: Only current + 1 backup clone retained
Expected: Old clones deleted
```

---

## Success Criteria

### Phase 1: After-Hours Bedtime
- ✅ Soft disconnect sends WM_CLOSE message
- ✅ Consecutive counter blocks day 3
- ✅ Time randomization within 3:00-3:30 AM
- ✅ Config schema updated
- ✅ Logging shows all state changes

### Phase 2: Inactivity Detection
- ✅ Input monitoring tracks mouse/keyboard
- ✅ Inactivity disconnect triggers after 60 min
- ✅ Quiet hours (3:30-9:00 AM) properly skip
- ✅ Only targets Sol's RNG (configurable placeId)
- ✅ Reuses soft disconnect from Phase 1

### Phase 3: Process Obfuscation (Optional)
- ✅ Process name rotates daily
- ✅ New name picked from legitimate process list
- ✅ PID-based detection still works
- ✅ Old clones cleaned up
- ✅ Restart time < 10 seconds

---

## Open Questions & Clarifications Needed

1. **WM_CLOSE Effectiveness:**
   - Has this been tested with Roblox? Success rate?
   - What happens on unsupported Roblox versions?

2. **Inactivity Definition for Sol's RNG:**
   - Is 1-2 hours exact, or configurable range?
   - Should range be (60, 120) minutes? Or fixed 60?

3. **Quiet Hours Duration:**
   - 3:30-9:00 AM sufficient to avoid conflicts?
   - Or should it extend to first activity detection?

4. **Process Naming Risk Tolerance:**
   - Acceptable to show different .exe names in task manager over time?
   - Should names rotate weekly instead of daily (less suspicious)?

5. **Soft Disconnect vs. Hard Kill:**
   - User preference on display? ("Disconnected" message vs. instant kill?)
   - Any game-specific disconnect methods to explore?

---

## Files to Create/Modify

### Files to Create
- [ ] `src/RobloxGuard.Core/InputMonitor.cs` (Task 2.1)
- [ ] `src/RobloxGuard.Core/ProcessObfuscation.cs` (Task 3.2)
- [ ] `docs/roblox_disconnect_research.md` (Task 1.1)
- [ ] `docs/process_obfuscation_research.md` (Task 3.1)

### Files to Modify
- [ ] `src/RobloxGuard.Core/RobloxRestarter.cs` (Task 1.2)
- [ ] `src/RobloxGuard.Core/PlaytimeTracker.cs` (Tasks 1.3, 1.4, 2.2)
- [ ] `src/RobloxGuard.Core/Models/Config.cs` (Tasks 1.5, 2.3)
- [ ] `src/RobloxGuard.Core/PidLockHelper.cs` (Task 3.3)
- [ ] `config.example.json` (Tasks 1.5, 2.3)

---

## Effort Estimation

| Phase | Tasks | Effort | Timeline |
|-------|-------|--------|----------|
| **Phase 1** | 1.1, 1.2, 1.3, 1.4, 1.5 | 9-11 hours | Week 1 (1-2 days) |
| **Phase 2** | 2.1, 2.2, 2.3 | 6-8 hours | Week 2 (1-2 days parallel) |
| **Phase 3** | 3.1, 3.2, 3.3 | 8-11 hours | Week 3 (can defer) |
| **Total** | All | 23-30 hours | 3-4 weeks (with parallelization) |

**Recommended Schedule:**
- **Week 1:** Phase 1 complete (after-hours bedtime)
- **Week 2:** Phase 2 complete (inactivity detection)
- **Week 3+:** Phase 3 optional (process obfuscation)

