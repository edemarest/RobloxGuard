# Next Implementation Phase - Strategic Analysis

**Date:** October 26, 2025  
**Phase:** Post-Force Kill Implementation  
**Status:** Ready for Feature Development

---

## Executive Summary

With the process control foundation complete (force kill + cleanup), we now select the most logical next deliverables from the roadmap. Based on:
- **Time-based features** (leverage config system)
- **Testability with simulated intervals** (can compress time in config for testing)
- **User-facing value** (visible behavior changes)
- **Low interdependencies** (can work in parallel)

**Recommendation:** Implement **Feature 1 (Soft Disconnect for After-Hours)** and **Feature 2 (Inactivity Monitoring)** in parallel.

---

## Deliverables Inventory (From Roadmap)

### Phase 1 ✅ COMPLETE
- [x] Force kill + graceful close attempt
- [x] Crash handler cleanup
- [x] Comprehensive logging
- [x] Zero-injection architecture

### Phase 2: Next (3 features available)

| Feature | Epic | Priority | Est. Effort | Risk | Testability |
|---------|------|----------|-------------|------|-------------|
| **Feature 1** | Soft Disconnect (After-Hours) | High | 8-12 hrs | Low | HIGH ✅ |
| **Feature 2** | Inactivity Monitoring | High | 6-9 hrs | Medium | MEDIUM ⚠️ |
| **Feature 3** | Process Obfuscation | Low | 8-11 hrs | High | LOW ❌ |

---

## Feature 1: Soft Disconnect for After-Hours (Ty's Bedtime)

### Overview
**Goal:** Between 3:00-3:30 AM, randomly trigger game disconnect with 65% probability  
**Behavior:** Graceful close attempt → force kill if needed → soft restart to home  
**Smart Logic:** Max 2 days in a row, day 3 forced allow  

### Why First?
1. ✅ **Time-based** - Easily testable with config-driven time overrides
2. ✅ **Reuses existing code** - KillRobloxProcess + SoftDisconnect already done
3. ✅ **Visible behavior** - Easy to test and verify
4. ✅ **No external dependencies** - Pure config + scheduler

### Testing Strategy
**Simulate real behavior in compressed time:**
```json
{
  "softDisconnectTestMode": true,
  "afterHoursSoftDisconnectTimeMinutes": 1,  // Check every 1 minute instead of 3 AM
  "afterHoursSoftDisconnectWindowMinutes": 1, // 1-2 minutes window
  "afterHoursSoftDisconnectProbability": 100  // Always trigger in test mode
}
```

**Test Sequence:**
1. Set test time to "1 minute from now"
2. Launch game on blocked placeId
3. Wait for disconnect
4. Verify logs show trigger
5. Repeat with consecutive day counter (test day 1, 2, 3 logic)

### Implementation Tasks

**Task 1.1: Add Config Fields**
- File: `Config.cs`
- Fields:
  - `AfterHoursSoftDisconnectEnabled` (bool, default: true)
  - `AfterHoursSoftDisconnectTime` (string, default: "03:00")
  - `AfterHoursSoftDisconnectWindowMinutes` (int, default: 30)
  - `AfterHoursSoftDisconnectProbability` (int 0-100, default: 65)
  - `AfterHoursSoftDisconnectMaxConsecutiveDays` (int, default: 2)
  - `ConsecutiveDisconnectDays` (int, default: 0)
  - `LastDisconnectDate` (DateTime, default: null)
  - `SoftDisconnectTestMode` (bool, default: false)
  - `SoftDisconnectTestMinutesOffset` (int, default: 1)

**Task 1.2: Implement PlaytimeTracker Logic**
- File: `PlaytimeTracker.cs`
- New method: `ShouldTriggerAfterHoursDisconnect()`
  - Check if after-hours enabled
  - Check if current time in randomized window
  - Check consecutive counter (skip day 3)
  - Roll RNG (65% default)
  - Return true/false with logging
- New method: `UpdateAfterHoursState(bool disconnected)`
  - Increment counter if disconnected
  - Update last disconnect date
  - Reset if day 3 passed

**Task 1.3: Add Scheduler Check**
- File: `LogMonitor.cs`
- Location: Main check loop (already exists)
- Call: `_playtimeTracker.ShouldTriggerAfterHoursDisconnect()`
- If true: Call `KillAndRestartToHome("After-hours soft disconnect")`
- Log: All decision points (time window, RNG roll, counter state)

### Effort Breakdown
- Config fields: 30 min
- PlaytimeTracker logic: 2-3 hours
- LogMonitor integration: 1 hour
- Testing & debugging: 2 hours
- **Total: 5.5-6.5 hours**

### Testable Outputs
- ✅ Logs show time window checks
- ✅ Logs show RNG roll result
- ✅ Logs show counter state
- ✅ Game disconnects at right time
- ✅ Counter increments/resets correctly

---

## Feature 2: Inactivity Monitoring (Sol's RNG)

### Overview
**Goal:** Monitor keyboard/mouse input; disconnect after 1-2 hours inactivity on specific game  
**Behavior:** Low-level hooks → track activity → trigger disconnect if idle too long  
**Smart Logic:** Skip during quiet hours (3:30-9:00 AM) to avoid interfering with after-hours  

### Why Second?
1. ✅ **Independent from Feature 1** - Can work in parallel
2. ✅ **Time-based testing** - Configurable inactivity threshold (test with 1 minute)
3. ⚠️ **Slightly complex** - Needs Windows hooks but uses no DLL injection
4. ✅ **Reuses soft disconnect** - Feature 1 provides the mechanism

### Testing Strategy
**Compress inactivity testing:**
```json
{
  "inactivityDisconnectTestMode": true,
  "inactivityDisconnectMinutes": 1,  // Test after 1 minute instead of 60
  "inactivityQuietHoursStart": "03:30",
  "inactivityQuietHoursEnd": "09:00"
}
```

**Test Sequence:**
1. Launch specific game (Sol's RNG)
2. Don't move mouse/keyboard for 1 minute
3. Game should disconnect
4. Verify logging shows inactivity detection
5. Test quiet hours override (set fake time to 5:00 AM, verify no disconnect)

### Implementation Tasks

**Task 2.1: Create InputMonitor Class**
- File: `src/RobloxGuard.Core/InputMonitor.cs` (new)
- Uses: Windows API hooks (user32.dll)
- Methods:
  - `Start()` - Register WH_MOUSE_LL and WH_KEYBOARD_LL hooks
  - `Stop()` - Unregister hooks safely
  - `GetInactivityDuration()` - Return TimeSpan since last activity
  - `_OnInputActivity()` - Update timestamp on any input
  - Properties: `IsRunning`, `LastActivityTime`
- Logging: At start/stop, errors on hook failure

**Task 2.2: Add Inactivity Config Fields**
- File: `Config.cs`
- Fields:
  - `InactivityDisconnectEnabled` (bool, default: true)
  - `InactivityDisconnectGamePlaceId` (uint, default: 0)
  - `InactivityDisconnectMinutes` (int, default: 60)
  - `InactivityQuietHoursStart` (string, default: "03:30")
  - `InactivityQuietHoursEnd` (string, default: "09:00")
  - `InactivityDetectionMethod` (string, default: "input", alt: "focus")
  - `InactivityTestMode` (bool, default: false)

**Task 2.3: Add Inactivity Logic to PlaytimeTracker**
- File: `PlaytimeTracker.cs`
- New method: `ShouldTriggerInactivityDisconnect(uint currentPlaceId)`
  - Check if enabled and correct game
  - Check if in quiet hours (if so, return false)
  - Get inactivity duration from InputMonitor
  - If >= threshold: Return true
  - Log decision point

**Task 2.4: LogMonitor Integration**
- File: `LogMonitor.cs`
- Initialize: `InputMonitor` on startup
- Main loop: Check `ShouldTriggerInactivityDisconnect()`
- If true: Call `KillAndRestartToHome("Inactivity disconnect")`
- Cleanup: Stop InputMonitor on exit

### Effort Breakdown
- InputMonitor class: 2-3 hours
- Config fields: 30 min
- PlaytimeTracker logic: 1-1.5 hours
- LogMonitor integration: 1 hour
- Testing & debugging: 1-1.5 hours
- **Total: 6-7.5 hours**

### Testable Outputs
- ✅ Logs show inactivity duration
- ✅ Logs show quiet hours check
- ✅ Game disconnects after inactivity
- ✅ Quiet hours properly suppress disconnect

---

## Implementation Strategy (Recommended)

### Parallelization
```
Week 1:
  Monday - Config fields for both features (30 min total)
  Tuesday - Feature 1 PlaytimeTracker + LogMonitor (3-4 hours)
  Wednesday - Feature 1 testing (2 hours)
  Thursday - Feature 2 InputMonitor class (2-3 hours)
  Friday - Feature 2 integration + testing (3-4 hours)

Result: Both features ready by Friday evening
```

### Dependency Graph
```
Config Fields (1 task)
├─ Feature 1: Time Logic (depends on config)
│  └─ Feature 1: Integration (depends on logic)
│     └─ Feature 1 Testing
└─ Feature 2: InputMonitor (independent)
   ├─ Feature 2: Time Logic (depends on config + InputMonitor)
   └─ Feature 2: Integration (depends on logic)
      └─ Feature 2 Testing
```

### Git/PR Strategy
- **PR #1:** Config changes for both features
- **PR #2:** Feature 1 (PlaytimeTracker + LogMonitor)
- **PR #3:** Feature 2 (InputMonitor + integration)
- **PR #4:** Test cases for both features

---

## Config File Examples

### After-Hours (Feature 1)
```json
{
  "afterHoursSoftDisconnectEnabled": true,
  "afterHoursSoftDisconnectTime": "03:00",
  "afterHoursSoftDisconnectWindowMinutes": 30,
  "afterHoursSoftDisconnectProbability": 65,
  "afterHoursSoftDisconnectMaxConsecutiveDays": 2,
  "consecutiveDisconnectDays": 0,
  "lastDisconnectDate": null,
  "softDisconnectTestMode": false,
  "softDisconnectTestMinutesOffset": 1
}
```

### Inactivity (Feature 2)
```json
{
  "inactivityDisconnectEnabled": true,
  "inactivityDisconnectGamePlaceId": 0,
  "inactivityDisconnectMinutes": 60,
  "inactivityQuietHoursStart": "03:30",
  "inactivityQuietHoursEnd": "09:00",
  "inactivityDetectionMethod": "input",
  "inactivityTestMode": false
}
```

### Test Mode (Compress Time)
```json
{
  "softDisconnectTestMode": true,
  "softDisconnectTestMinutesOffset": 1,
  "afterHoursSoftDisconnectWindowMinutes": 1,
  "afterHoursSoftDisconnectProbability": 100,
  
  "inactivityTestMode": true,
  "inactivityDisconnectMinutes": 1
}
```

---

## Testing Checklist

### Feature 1: After-Hours
- [ ] Config loads with new fields
- [ ] Time window randomizes each day
- [ ] RNG rolls work at configured probability
- [ ] Consecutive counter increments
- [ ] Day 3 blocks disconnect
- [ ] After day 3, counter resets
- [ ] Test mode works (1-minute window)
- [ ] Logs show all decision points
- [ ] Soft disconnect triggers correctly

### Feature 2: Inactivity
- [ ] InputMonitor hooks register
- [ ] Activity timestamp updates on input
- [ ] Inactivity duration calculates correctly
- [ ] Quiet hours suppress disconnect
- [ ] Only triggers on configured placeId
- [ ] Test mode works (1-minute inactivity)
- [ ] Logs show inactivity check
- [ ] Soft disconnect triggers correctly

---

## Risk Assessment

### Feature 1: Low Risk ✅
- Pure logic, no system APIs
- Config-driven, easy to disable
- Reuses existing code paths
- Extensive logging possible

### Feature 2: Medium Risk ⚠️
- Windows API hooks (user32.dll)
- Could interfere with system input (unlikely)
- Requires graceful cleanup
- Mitigation: Careful error handling, hook unregistration

### Mitigation Strategies
1. **Feature Flag:** Both features have boolean enable/disable
2. **Test Mode:** Time/thresholds can be compressed for testing
3. **Logging:** Every decision point logged for debugging
4. **Gradual Rollout:** Deploy Feature 1 first, then Feature 2
5. **User Control:** Config easily accessible for parent tweaks

---

## Decision Record

### Selected: Feature 1 + Feature 2 (Parallel Implementation)

**Rationale:**
1. ✅ Both are time-based (leverage existing scheduler)
2. ✅ Both are testable with config overrides
3. ✅ Provide comprehensive schedule-based enforcement
4. ✅ After-Hours covers bedtime, Inactivity covers daytime idle
5. ✅ Together they create robust parental control

### Deferred: Feature 3 (Process Obfuscation)
- Reason: Can be added later without changing core features
- Risk: Too high for current phase
- Value: Nice-to-have, not essential

### Next After These Two:
1. Block UI improvements (visual feedback)
2. Statistics & reporting dashboard
3. Advanced unlock PIN system
4. Multi-device sync (future)

---

## Conclusion

**Ready to start:** Both Feature 1 and Feature 2  
**Estimated completion:** 5-7 business days  
**Testing complexity:** Medium (time-based testing with config overrides)  
**Deployment risk:** Low (both have kill switches, Feature 1 is fully safe, Feature 2 has graceful cleanup)

**Recommendation:** Begin with configuration schema update, then split implementation:
- **Dev A:** Feature 1 (PlaytimeTracker + integration)
- **Dev B:** Feature 2 (InputMonitor + integration)
- **Both:** Testing phase (1 day, parallel)
- **Lead:** Code review + merge (1 day)

**Next milestone:** Both features in production by end of week.
