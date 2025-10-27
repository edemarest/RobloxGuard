# Discord Notification Monitor - Detailed Implementation Plan

**Status:** Ready for Execution  
**Date:** October 26, 2025  
**Branch:** `feature/discord-notification-monitor`

---

## 🎯 Executive Summary

We will **dramatically simplify** RobloxGuard by:

1. **Keep**: Durable monitor infrastructure (watchdog, heartbeat, session persistence, crash recovery)
2. **Keep**: Game detection (detect when user is in blocked game)
3. **Keep**: Graceful kill/restart logic
4. **Remove**: ALL scheduling logic (PlaytimeTracker, playtime limits, after-hours enforcement, random delays)
5. **Add**: Discord notification listener (Windows Notification Handler API)
6. **Add**: User ID detection from Discord notifications

**Result:** Event-driven game closure on Discord notification vs. time-based automation

---

## 🔍 Scanning Results - What We'll Remove

### Files/Code to DELETE COMPLETELY

#### 1. `PlaytimeTracker.cs` (656 lines)
- ALL playtime tracking logic
- ALL scheduling/delay logic  
- ALL state management for scheduled kills
- Session resumption logic (we'll keep session state, just not for scheduling)

**Lines to remove:**
- 150+: Playtime limit calculations
- 200+: After-hours enforcement logic
- 300+: Random delay generation
- 400+: CheckAndApplyLimits() method (entire execution engine)
- Entire `GameSession` class

#### 2. `PlaytimeTrackerTests.cs`
- All playtime tracking tests
- All scheduling tests
- All after-hours tests

#### 3. Config settings in `RobloxGuardConfig.cs`
```json
❌ "playtimeLimitEnabled"              (Line 67)
❌ "playtimeLimitMinutes"              (Line 73)
❌ "showBlockUIOnPlaytimeKill"         (Line 78)
❌ "afterHoursEnforcementEnabled"      (Line 87)
❌ "afterHoursStartTime"               (Line 94)
❌ "blockedGameKillDelayMinutesMin"    (Line 100)
❌ "blockedGameKillDelayMinutesMax"    (Line 107)
❌ "showBlockUIOnAfterHoursKill"       (Line 113)
❌ "killBlockedGameImmediately"        (Line 119)
```

---

### LogMonitor.cs - Specific Lines to Remove

#### Remove from Line 74:
```csharp
// PlaytimeTracker for Feature A (playtime limit) and Feature B (after-hours)
private PlaytimeTracker? _playtimeTracker;
```

#### Remove from Line 288-305 (~17 lines):
```csharp
// Initialize PlaytimeTracker for Feature A (playtime limit) and Feature B (after-hours)
if (config.PlaytimeLimitEnabled || config.AfterHoursEnforcementEnabled)
{
    _playtimeTracker = new PlaytimeTracker(
        () => configManager.Load(),
        (reason) =>
        {
            LogToFile($"[LogMonitor.PlaytimeTracker] KILL SCHEDULED: {reason}");
            // Kill execution happens here
        }
    );
}
```

#### Remove from Main Loop (Line ~664):
```csharp
// Check if any scheduled kills need to be executed
_playtimeTracker?.CheckAndApplyLimits(currentPlaceId, GameExitPattern.IsMatch(line));
```

#### Remove from TryBackfillSessionFromRecentLogs():
- Any logic that checks playtime or calculates elapsed times for scheduling

---

### DayCounterManager.cs
- `GetRandomizedAfterHoursStartTime()` method (scheduling specific)
- May keep if it's used elsewhere, but review carefully

---

## ✅ What We KEEP - Core Durability

### Session Management (SessionStateManager.cs) - KEEP INTACT
- `.session.json` file tracking
- Heartbeat updates
- Crash recovery mechanism
- Session restoration logic (but NOT for scheduling)

**Why:** We need to know "is the user currently in a blocked game?" to trigger kills on Discord notifications

### LogMonitor.cs Main Monitoring Loop - KEEP CORE, SIMPLIFY
**Keep:**
```csharp
// 1. Read Roblox logs
// 2. Detect game joins via PlaceIdParser
// 3. Check if blocked via RobloxGuardConfig
// 4. Save session state (but don't schedule kills)
// 5. Heartbeat updates (watchdog monitoring)
// 6. Game exit detection
```

**Remove:**
```csharp
// ❌ PlaytimeTracker polling
// ❌ Playtime limit checks
// ❌ After-hours enforcement checks
// ❌ Random delay calculations
```

### RobloxRestarter.cs - KEEP INTACT
- Graceful kill sequence (WM_CLOSE → force kill → restart home)
- Process finding logic
- Auto-restart mechanism

### Watchdog/Health Checks - KEEP INTACT
- `MonitorStateHelper.cs` - Process health checks
- `HeartbeatHelper.cs` - Liveness detection
- `PidLockHelper.cs` - Single-instance enforcement
- `TaskSchedulerHelper.cs` - Background task management

### RegistryHelper.cs - KEEP INTACT
- Protocol handler registration
- Original handler restoration

### AlertForm.cs - KEEP INTACT
- Block UI display
- Alert window

---

## ✨ What We ADD - Discord Listener

### New Component: `DiscordNotificationListener.cs` (~300 lines)

```csharp
public class DiscordNotificationListener : IDisposable
{
    private string _targetDiscordUserId;
    private string _triggerKeyword;
    
    // Event fired when matching Discord notification detected
    public event EventHandler<DiscordNotificationEventArgs>? TriggerReceived;
    
    public DiscordNotificationListener(string targetUserId, string triggerKeyword)
    {
        _targetDiscordUserId = targetUserId;
        _triggerKeyword = triggerKeyword;
    }
    
    public async Task Start()
    {
        // Subscribe to Windows Notification Handler
        // Listen for Discord notifications
        // Parse for trigger keyword
        // Verify source user ID
        // Fire TriggerReceived event if all conditions met
    }
    
    public void Stop()
    {
        // Cleanup notification subscriptions
    }
}
```

### New Config Settings (4 settings):
```json
{
  "discordNotificationEnabled": false,
  "discordSourceUserId": "",              // e.g., "123456789"
  "discordTriggerKeyword": "close game",  // e.g., "kill" or "stop"
  "notificationMonitorEnabled": true      // Fallback: keep process watching active
}
```

### Integration Points:

**In LogMonitor.cs - New Startup Logic:**
```csharp
// Initialize Discord notification listener
if (config.DiscordNotificationEnabled)
{
    var listener = new DiscordNotificationListener(
        config.DiscordSourceUserId,
        config.DiscordTriggerKeyword
    );
    
    listener.TriggerReceived += (sender, args) =>
    {
        // Check if user currently in blocked game (via session)
        var session = SessionStateManager.LoadActiveSession();
        if (session?.IsBlocked == true)
        {
            LogToFile($"[Discord] Notification trigger received. Killing game {session.PlaceId}");
            RobloxRestarter.KillAndRestartToHome("Discord notification trigger");
            SessionStateManager.ClearSession();
        }
    };
    
    await listener.Start();
}
```

---

## 📊 Config Migration

### Remove from config.json:
```json
{
  "playtimeLimitEnabled": false,                   // ❌
  "playtimeLimitMinutes": 120,                    // ❌
  "showBlockUIOnPlaytimeKill": true,              // ❌
  "afterHoursEnforcementEnabled": false,          // ❌
  "afterHoursStartTime": 3,                       // ❌
  "blockedGameKillDelayMinutesMin": 0,            // ❌
  "blockedGameKillDelayMinutesMax": 60,           // ❌
  "showBlockUIOnAfterHoursKill": true,            // ❌
  "killBlockedGameImmediately": true              // ❌
}
```

### Add to config.json:
```json
{
  "discordNotificationEnabled": false,
  "discordSourceUserId": "",
  "discordTriggerKeyword": "close game",
  "notificationMonitorEnabled": true
}
```

### Example Final Config:
```json
{
  "blockedGames": [
    {"placeId": 15532962292, "name": "Blocked Game 1"}
  ],
  "parentPINHash": "pbkdf2:...",
  "upstreamHandlerCommand": "C:\\...\\handler.exe \"%1\"",
  "overlayEnabled": true,
  "whitelistMode": false,
  "silentMode": true,
  "autoRestartOnKill": true,
  
  "discordNotificationEnabled": true,
  "discordSourceUserId": "987654321",
  "discordTriggerKeyword": "close",
  "notificationMonitorEnabled": true
}
```

---

## 🔄 Execution Flow Comparison

### BEFORE (Schedule-Based)
```
Loop every 100ms:
  1. Read logs
  2. Detect game join
  3. PlaytimeTracker.CheckAndApplyLimits()  ← ❌ REMOVE
     ├─ Calculate elapsed time
     ├─ Check if > limit
     ├─ Apply random delay
     └─ Execute kill if scheduled time reached
  4. Game exit check
  5. Heartbeat update
  6. Sleep
```

### AFTER (Event-Driven)
```
Startup:
  1. Initialize DiscordNotificationListener
  2. Subscribe to notification events
  3. Start monitoring

Main Loop (MUCH SIMPLER):
  1. Read logs
  2. Detect game join
  3. Save session (is user in blocked game?)
  4. Game exit check
  5. Heartbeat update
  6. Sleep

Async (Discord):
  Notification received event:
  1. Check if user in blocked game (from session)
  2. If yes: Kill immediately
  3. If no: Ignore notification
```

---

## 🎯 Reliable Implementation Approach

### Phase 1: Clean Code Removal (2-3 hours)

**Step 1.1: Remove Config Settings**
- Open `RobloxGuardConfig.cs`
- Delete 9 playtime/after-hours properties (~30 lines)
- Add 4 Discord properties
- Run config tests

**Step 1.2: Remove PlaytimeTracker Initialization**
- Open `LogMonitor.cs` line 74
- Delete PlaytimeTracker field
- Delete initialization code (line 288-305)
- Run compile check

**Step 1.3: Remove PlaytimeTracker from Main Loop**
- Open `LogMonitor.cs` main loop
- Delete playtime checking code
- Delete after-hours checking code
- Run compile check (will have errors about _playtimeTracker)

**Step 1.4: Delete PlaytimeTracker Files**
- Delete `PlaytimeTracker.cs`
- Delete `PlaytimeTrackerTests.cs`
- Run full build (should compile)

**Step 1.5: Run Tests**
```bash
dotnet test src/RobloxGuard.Core.Tests
# Expect: Most tests pass, PlaytimeTrackerTests removed
```

### Phase 2: Add Discord Listener (2-3 hours)

**Step 2.1: Create DiscordNotificationListener.cs**
- New file: `src/RobloxGuard.Core/DiscordNotificationListener.cs`
- Implement Windows Notification Handler subscription
- Implement notification parsing
- Implement user ID + keyword filtering
- Event emission

**Step 2.2: Update RobloxGuardConfig.cs**
- Add Discord properties (already done in Phase 1)
- Update ConfigManager validation (allow missing playtime settings)

**Step 2.3: Update LogMonitor.cs Startup**
- Add Discord listener initialization
- Subscribe to notification events
- Handle trigger: Check session → Kill if blocked

**Step 2.4: Run Tests**
```bash
dotnet test src/RobloxGuard.Core.Tests
# Expect: All tests pass
```

### Phase 3: Manual Testing (1-2 hours)

**Test 1: Verify game detection still works**
- Start monitor
- Join blocked game
- Check: Session saved, log shows game join
- Exit game
- Check: Session cleared

**Test 2: Verify Discord notification trigger**
- Start monitor with Discord enabled
- Join blocked game
- Send Discord message from target user with keyword
- Check: Game killed, alert shown, restarted to home

**Test 3: Verify fallback (notification fails)**
- Disable Discord listener
- Join blocked game
- Check: Notification monitor message appears in log
- Monitor continues watching (can join other games)

---

## 🛡️ Reliability Guarantees

### What Won't Break:
✅ **Durability**: Watchdog still monitors process health  
✅ **Crash Recovery**: Heartbeat + session persistence intact  
✅ **Game Detection**: Log monitoring unchanged  
✅ **Kill Sequence**: Graceful kill/restart unchanged  
✅ **Single Instance**: PID locking intact  
✅ **Background Task**: Scheduled task setup intact  

### New Reliability Considerations:
⚠️ **Windows Notification Handler** - May fail on some systems (graceful fallback to log-based monitoring)  
⚠️ **Discord Message Parsing** - Must handle format variations (comprehensive error handling)  
⚠️ **User ID Matching** - Must correctly extract Discord user ID from notification (extensive testing)

---

## 📋 Code Removal Checklist

### RobloxGuardConfig.cs
- [ ] Line 61-62: Remove `// ========== FEATURE A: Playtime Limit ==========` section header
- [ ] Line 64-78: Remove all playtime limit properties
- [ ] Line 82-114: Remove all after-hours enforcement properties  
- [ ] Line 119-127: Remove killBlockedGameImmediately property
- [ ] Verify 4 Discord properties added (done in Phase 1 Step 1.1)

### LogMonitor.cs
- [ ] Line 74: Remove `private PlaytimeTracker? _playtimeTracker;`
- [ ] Line 288-305: Remove PlaytimeTracker initialization block
- [ ] Main loop (~line 664): Remove `_playtimeTracker?.CheckAndApplyLimits(...)`
- [ ] Verify TryBackfillSessionFromRecentLogs() doesn't reference playtime
- [ ] Add Discord listener initialization
- [ ] Add Discord event handler

### Delete Files
- [ ] `PlaytimeTracker.cs` (entire file)
- [ ] `PlaytimeTrackerTests.cs` (entire file)

### DayCounterManager.cs (Review)
- [ ] Check if `GetRandomizedAfterHoursStartTime()` used elsewhere
- [ ] Remove if only used for scheduling
- [ ] Keep if used for other purposes

---

## 🧪 Testing Strategy

### Unit Tests to Update:
```
ConfigurationNewPropertiesTests.cs
  ├─ Remove playtime config tests
  ├─ Add Discord config tests
  └─ Update config validation tests

RobloxRestarterTests.cs
  └─ No changes (grace kill logic unchanged)

PlaceIdParserTests.cs
  └─ No changes (game detection unchanged)
```

### New Tests to Create:
```
DiscordNotificationListenerTests.cs
  ├─ Test notification parsing
  ├─ Test user ID filtering
  ├─ Test keyword matching
  ├─ Test event emission
  └─ Test edge cases (malformed messages, wrong user, wrong keyword)
```

### Integration Test Script:
```bash
# Test 1: Game detection
- Start monitor
- Join blocked game
- Verify session saved

# Test 2: Discord trigger
- Join blocked game
- Send Discord message
- Verify game killed

# Test 3: Notification failure
- Disable Discord listener
- Verify system still responsive
```

---

## ⚠️ Edge Cases & Mitigations

### Edge Case 1: Discord Notification Unavailable
**What:** Windows Notification Handler not available on system  
**Symptom:** DiscordNotificationListener.Start() throws exception  
**Mitigation:** Try-catch, log warning, continue with process watching  

### Edge Case 2: Multiple Discord Messages
**What:** User sends 3+ messages quickly  
**Symptom:** 3+ kill signals received  
**Mitigation:** Kill is idempotent (can't kill dead process)  

### Edge Case 3: Notification Without Active Session
**What:** Discord notification received but user NOT in game  
**Symptom:** Session is null when event fires  
**Mitigation:** Check `session?.IsBlocked == true` before kill  

### Edge Case 4: Malformed Discord Notification
**What:** Discord message format changes in future Windows update  
**Symptom:** Parsing fails, exception thrown  
**Mitigation:** Comprehensive try-catch, log raw notification for debugging  

### Edge Case 5: Wrong User ID Format
**What:** Config has wrong Discord user ID format  
**Symptom:** Notifications never match  
**Mitigation:** Extensive logging of ID comparison, validation on startup  

---

## 🚀 Quick Reference - What Files Change

| File | Action | Impact |
|------|--------|--------|
| `RobloxGuardConfig.cs` | Remove 9 settings, add 4 settings | Config simplification |
| `LogMonitor.cs` | Remove scheduling logic, add Discord init | Main loop simplification |
| `PlaytimeTracker.cs` | DELETE | -656 lines |
| `PlaytimeTrackerTests.cs` | DELETE | -200 lines |
| `DiscordNotificationListener.cs` | CREATE | +300 lines |
| `DiscordNotificationListenerTests.cs` | CREATE | +150 lines |
| `Program.cs` | Minor updates | CLI help text |
| All other files | NO CHANGES | Unchanged |

**Total LOC Change:** -656 - 200 + 300 + 150 = -406 LOC (net reduction)

---

## 🎉 Success Criteria

✅ All config playtime/after-hours settings removed  
✅ PlaytimeTracker.cs completely deleted  
✅ LogMonitor main loop no longer has scheduling logic  
✅ DiscordNotificationListener working and tested  
✅ Discord notifications trigger game kills  
✅ All unit tests pass (except deleted ones)  
✅ Manual end-to-end test passes  
✅ No regressions in game detection or kill sequence  
✅ Codebase is simpler (~400 LOC reduction)  
✅ Durable monitor still works (watchdog, heartbeat, session persistence)

---

## 🎯 Next Steps

1. **Start Phase 1:** Remove config settings + PlaytimeTracker
2. **Compile & Fix:** Resolve remaining compiler errors
3. **Run Tests:** Verify existing functionality intact
4. **Start Phase 2:** Add DiscordNotificationListener
5. **Integration:** Wire up Discord events to LogMonitor
6. **Manual Test:** End-to-end testing with Discord
7. **Commit:** Push to GitHub with clear commit messages

**Estimated Total Time:** 5-8 hours (2 phases, implementation + testing)

---

## 📖 Implementation References

- **Current Architecture:** `docs/CURRENT_ARCHITECTURE_ANALYSIS.md`
- **Technical Spec:** `docs/DISCORD_NOTIFICATION_REFACTOR_ANALYSIS.md`
- **Quick Reference:** `QUICK_REFERENCE.md`

**You're ready to implement! 🚀**

