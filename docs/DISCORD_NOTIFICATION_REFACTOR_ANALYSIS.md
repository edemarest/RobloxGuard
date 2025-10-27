# Discord Notification Monitor Refactor Analysis

**Branch:** `feature/discord-notification-monitor`
**Date:** October 26, 2025
**Objective:** Replace schedule-based game closing with Discord notification trigger

---

## Executive Summary

The current RobloxGuard uses complex scheduling logic to enforce game restrictions. This refactor replaces that with a **notification-driven** architecture:

- **Remove:** PlaytimeTracker, playtime limits, after-hours enforcement, randomized delays, TaskSchedulerHelper (most of it)
- **Keep:** Core blocking logic, session management, graceful kill/restart, process watching, logging infrastructure
- **Add:** Windows Notification Handler integration to listen for Discord DM notifications from a specific user
- **Result:** Remote, event-driven game closure instead of time-based automation

---

## What to Keep ✅

### 1. **Core Blocking & Detection (90% intact)**
   - `PlaceIdParser.cs` - ✅ Keep (unchanged - still needed for URI/CLI parsing)
   - `RobloxGuardConfig.cs` - ✅ Keep (simplified config, remove playtime/after-hours settings)
   - `LogMonitor.cs` - ✅ Keep (main loop + process watching, but remove playtime logic)
   - `RobloxRestarter.cs` - ✅ Keep (graceful kill/restart is still core)
   - `RegistryHelper.cs` - ✅ Keep (unchanged)
   - `AlertForm.cs` - ✅ Keep (block alert still useful)

### 2. **Session & State Management**
   - `SessionStateManager.cs` - ✅ Keep (track which game is running for notification-triggered kills)
   - `MonitorStateHelper.cs` - ✅ Keep (health checks still needed)
   - `PidLockHelper.cs` - ✅ Keep (single-instance enforcement)
   - `HeartbeatHelper.cs` - ✅ Keep (liveness detection)

### 3. **Infrastructure**
   - `LogMonitor` main loop - ✅ Keep (but simplify to remove scheduler polling)
   - `Program.cs` - ✅ Keep (entry point, command routing)
   - `InstallerHelper.cs` - ✅ Keep (install/uninstall still needed)
   - Logging system - ✅ Keep (comprehensive audit trail)

---

## What to Remove ❌

### 1. **PlaytimeTracker.cs** - ❌ REMOVE
   - **Why:** No longer scheduling kills based on elapsed time
   - **Impact:** Removes ~650 lines
   - **Replaced by:** Discord notification listener

### 2. **TaskSchedulerHelper.cs** - ⚠️ SIMPLIFY (not remove)
   - **Current:** Creates 2 scheduled tasks (logon task + 1-minute watchdog)
   - **New:** Only needs logon task (RobloxGuardLogonTask to start monitor)
   - **Remove:** 1-minute health-check task (can use event-driven heartbeat instead, or keep for reliability)
   - **Actually:** Keep but make optional - the watchdog is still valuable for monitor health

### 3. **Config Settings to Remove:**
   ```json
   ❌ playtimeLimitEnabled
   ❌ playtimeLimitMinutes
   ❌ blockedGameKillDelayMinutesMin
   ❌ blockedGameKillDelayMinutesMax
   ❌ afterHoursEnforcementEnabled
   ❌ afterHoursStartTime
   ❌ showBlockUIOnPlaytimeKill
   ❌ showBlockUIOnAfterHoursKill
   ```

### 4. **Config Settings to Add:**
   ```json
   ✅ discordNotificationEnabled: boolean
   ✅ discordSourceUserId: string (Discord User ID to listen for)
   ✅ discordKeyword: string (optional - e.g., "close game" or "kill roblox")
   ✅ notificationMonitorEnabled: boolean (fallback to keep process watching active)
   ```

---

## Architecture Refactor Plan

### Current Flow (Schedule-Based)
```
LogMonitor starts
  ↓
LoadConfig
  ↓
Initialize PlaytimeTracker
  ↓
Main loop:
  1. Read Roblox logs
  2. Detect game join
  3. Record session
  4. PlaytimeTracker.CheckPlaytimeLimit() every iteration
     ↓
     If kill time reached → Execute kill
  5. Sleep 100ms
```

### New Flow (Notification-Based)
```
LogMonitor starts
  ↓
LoadConfig
  ↓
Initialize NotificationListener
  ↓
Subscribe to Windows Notification Handler:
  - Filter for Discord notifications
  - Match source user (Discord UserId)
  - Listen for keyword trigger (e.g., "close game")
  ↓
Main loop (simplified):
  1. Read Roblox logs
  2. Detect game join → Record session
  3. Sleep 100ms
  
Separate event handler (async):
  - Notification received from Discord
  - Extract trigger keyword
  - If session active: Call RobloxRestarter.KillAndRestartToHome()
  - Log notification event
```

---

## Component Breakdown: Keep vs. Remove vs. Modify

### KEEP - No Changes Required

| Component | File | Status | Notes |
|-----------|------|--------|-------|
| PlaceIdParser | `PlaceIdParser.cs` | ✅ Keep | URI/CLI parsing unchanged |
| RegistryHelper | `RegistryHelper.cs` | ✅ Keep | Protocol registration unchanged |
| RobloxRestarter | `RobloxRestarter.cs` | ✅ Keep | Graceful kill still core |
| HeartbeatHelper | `HeartbeatHelper.cs` | ✅ Keep | Liveness detection |
| PidLockHelper | `PidLockHelper.cs` | ✅ Keep | Single-instance enforcement |
| SessionStateManager | `SessionStateManager.cs` | ✅ Keep | Track active session |
| MonitorStateHelper | `MonitorStateHelper.cs` | ✅ Keep | Health checks |
| AlertForm | `AlertForm.cs` | ✅ Keep | Block UI still useful |

### MODIFY - Significant Changes

| Component | File | Changes |
|-----------|------|---------|
| **RobloxGuardConfig** | `RobloxGuardConfig.cs` | Remove playtime/after-hours config; add discord notification settings |
| **LogMonitor** | `LogMonitor.cs` | Remove PlaytimeTracker polling; simplify main loop; add notification listener setup |
| **Program** | `Program.cs` | Update config validation; remove playtime CLI args (if any) |
| **TaskSchedulerHelper** | `TaskSchedulerHelper.cs` | Remove or simplify 1-minute watchdog task (optional) |

### REMOVE - Delete Entirely

| Component | File | Reason |
|-----------|------|--------|
| **PlaytimeTracker** | `PlaytimeTracker.cs` | No longer scheduling kills based on elapsed time |
| **Tests** | `PlaytimeTrackerTests.cs` `SilentModeTests.cs` | Playtime testing no longer relevant |

---

## Notification Listener Implementation Strategy

### Option A: Windows Notification Handler (WinRT/UWP)
**Pros:**
- Native Windows 10/11 integration
- Async subscription model
- Real-time events

**Cons:**
- Requires WinRT APIs (complex for .NET 8)
- May need Windows SDK references
- Experimental (may have edge cases)

**Implementation:**
```csharp
// Pseudo-code
class DiscordNotificationListener
{
    private UserNotificationListener _listener;
    private string _targetUserId;
    private string _triggerKeyword;
    
    async Task Start()
    {
        _listener = UserNotificationListener.GetDefault();
        _listener.NotificationChanged += OnNotificationChanged;
    }
    
    async void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        var notification = _listener.GetNotification(args.ChangeId);
        
        // Check if Discord notification
        if (notification.AppInfo?.AppUserModelId?.Contains("Discord") ?? false)
        {
            // Extract message, check for source user + keyword
            // If match: Raise NotificationReceived event
        }
    }
}
```

### Option B: Discord.NET Client (HTTP Polling)
**Pros:**
- Explicit Discord API control
- Well-documented library
- More reliable for specific user filtering

**Cons:**
- Requires Discord bot token
- HTTP polling (not ideal for real-time)
- Network dependency
- Out-of-process communication

### Option C: Hybrid (Recommended for MVP)
1. Use **Windows Notification Handler** for general Discord notification detection
2. Fall back to **process/log monitoring** if notification filtering fails
3. Add **optional Discord bot** for future cloud-based control

**Recommendation:** Start with Option C, implementing robust Windows notification interception.

---

## File-by-File Refactor Checklist

### Delete
- [ ] `src/RobloxGuard.Core/PlaytimeTracker.cs`
- [ ] `src/RobloxGuard.Core.Tests/PlaytimeTrackerTests.cs`
- [ ] `src/RobloxGuard.Core.Tests/SilentModeTests.cs` (can be retained if relevant to current features)

### Modify (Major Changes)
- [ ] `src/RobloxGuard.Core/RobloxGuardConfig.cs`
  - Remove playtime/after-hours config properties
  - Add discord notification properties
  - Update ConfigManager validation logic

- [ ] `src/RobloxGuard.Core/LogMonitor.cs`
  - Remove `PlaytimeTracker` initialization
  - Remove playtime polling loop logic
  - Add `DiscordNotificationListener` initialization
  - Add event handler for notification triggers
  - Simplify main loop (game detection only)

- [ ] `src/RobloxGuard.Core/TaskSchedulerHelper.cs` (Optional)
  - Remove 1-minute watchdog task creation OR keep for reliability
  - Keep logon startup task

- [ ] `src/RobloxGuard.UI/Program.cs`
  - Remove playtime-related config loading/validation
  - Update CLI help text
  - Remove any playtime-specific command modes

### Modify (Minor Changes)
- [ ] `src/RobloxGuard.Core/InstallerHelper.cs`
  - Update default config template to remove playtime settings
  - Add default discord notification settings (disabled by default)

- [ ] `docs/ARCHITECTURE.md`
  - Update component descriptions
  - Remove PlaytimeTracker section
  - Add NotificationListener section

### Keep (No Changes)
- [ ] All other `.cs` files (PlaceIdParser, RegistryHelper, RobloxRestarter, etc.)

---

## New Component: DiscordNotificationListener.cs

### Skeleton
```csharp
using Windows.UI.Notifications.Management;
using Windows.ApplicationModel.Calls;

/// <summary>
/// Listens to Windows notifications for Discord events.
/// Filters by source user ID and triggers game kill via keyword detection.
/// </summary>
public class DiscordNotificationListener : IDisposable
{
    private readonly string _targetUserId;
    private readonly string _triggerKeyword;
    private UserNotificationListener _listener;
    
    public event EventHandler<DiscordNotificationEventArgs> NotificationReceived;
    
    public DiscordNotificationListener(string targetUserId, string triggerKeyword)
    {
        _targetUserId = targetUserId;
        _triggerKeyword = triggerKeyword;
    }
    
    public async Task Start()
    {
        try
        {
            _listener = UserNotificationListener.GetDefault();
            if (_listener == null)
            {
                Logger.Warn("UserNotificationListener not available on this system");
                return;
            }
            
            _listener.NotificationChanged += OnNotificationChanged;
            Logger.Info($"Discord notification listener started, filtering for user: {_targetUserId}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize notification listener: {ex.Message}");
        }
    }
    
    public void Stop()
    {
        if (_listener != null)
        {
            _listener.NotificationChanged -= OnNotificationChanged;
        }
    }
    
    private async void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        try
        {
            var notification = sender.GetNotification(args.ChangeId);
            if (notification?.AppInfo?.AppUserModelId?.Contains("Discord") ?? false)
            {
                // Parse notification for trigger
                if (CheckNotificationForTrigger(notification))
                {
                    NotificationReceived?.Invoke(this, new DiscordNotificationEventArgs { Timestamp = DateTime.UtcNow });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error processing notification: {ex.Message}");
        }
    }
    
    private bool CheckNotificationForTrigger(UserNotification notification)
    {
        // Extract message text from notification
        // Check for trigger keyword
        // Verify source is target user
        return false; // TODO: Implement
    }
    
    public void Dispose()
    {
        Stop();
    }
}

public class DiscordNotificationEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; }
}
```

---

## Configuration Schema (New)

### Remove These Settings
```json
// REMOVE:
"playtimeLimitEnabled": false,
"playtimeLimitMinutes": 120,
"blockedGameKillDelayMinutesMin": 0,
"blockedGameKillDelayMinutesMax": 60,
"afterHoursEnforcementEnabled": false,
"afterHoursStartTime": 3,
"showBlockUIOnPlaytimeKill": true,
"showBlockUIOnAfterHoursKill": true,
"killBlockedGameImmediately": true
```

### Add These Settings
```json
{
  "discordNotificationEnabled": false,
  "discordSourceUserId": "",           // Discord User ID (numeric string)
  "discordTriggerKeyword": "close game", // Keyword to look for in message
  "notificationMonitorEnabled": true   // Keep process watching active
}
```

### Example New Config
```json
{
  "blockedGames": [
    {"placeId": 15532962292, "name": "BLOCKED GAME 1"},
    {"placeId": 1818, "name": "BLOCKED GAME 2"}
  ],
  "parentPINHash": "pbkdf2:sha256:iterations:salt:hash",
  "upstreamHandlerCommand": "C:\\original\\roblox\\handler.exe \"%1\"",
  "overlayEnabled": true,
  "whitelistMode": false,
  "silentMode": true,
  "autoRestartOnKill": true,
  "discordNotificationEnabled": true,
  "discordSourceUserId": "123456789",
  "discordTriggerKeyword": "close game",
  "notificationMonitorEnabled": true
}
```

---

## Behavior Change Summary

### Before (Schedule-Based)
```
Time-based enforcement:
- Playtime limits (e.g., kill after 2 hours)
- After-hours windows (e.g., block after 3 AM)
- Randomized delays to prevent predictability
- Child knows games will close at predictable times
```

### After (Notification-Based)
```
Event-driven enforcement:
- Parent sends Discord DM to monitor account: "close game"
- Monitor intercepts notification, detects trigger keyword
- Monitor checks current session (which game is running)
- If game is active: Graceful kill + restart to home
- If no game running: No action (notification ignored)
- Child doesn't know when game will close (unpredictable)
```

---

## Testing Strategy

### Unit Tests to Update/Remove
- ❌ `PlaytimeTrackerTests.cs` - Remove
- ❌ `SilentModeTests.cs` - Remove (or refactor if still relevant)
- ✅ `PlaceIdParserTests.cs` - Keep (no changes)
- ✅ `ConfigManagerTests.cs` - Update to remove playtime validation

### New Tests Needed
- `DiscordNotificationListenerTests.cs`
  - Test notification parsing
  - Test keyword matching
  - Test user ID filtering
  - Test integration with RobloxRestarter

### Manual Testing
1. Start monitor with Discord notification enabled
2. Join a blocked game
3. Send Discord message with trigger keyword from monitored account
4. Verify:
   - Notification received and logged
   - Game killed gracefully
   - Restarted to home
   - Alert UI shown (if enabled)

---

## Implementation Phases

### Phase 1: Configuration Refactor
- Remove playtime/after-hours config
- Add discord notification config
- Update default config template
- Update config validation
- Run existing unit tests (should mostly pass)

### Phase 2: LogMonitor Simplification
- Remove PlaytimeTracker initialization
- Remove playtime polling logic
- Simplify main loop
- Add DiscordNotificationListener initialization
- Test game detection still works

### Phase 3: DiscordNotificationListener Implementation
- Create notification listener class
- Implement notification parsing
- Implement keyword matching
- Implement user ID filtering
- Add event emission to LogMonitor

### Phase 4: Integration & Testing
- LogMonitor subscribes to notification events
- Call RobloxRestarter.KillAndRestartToHome() on trigger
- Manual testing with Discord DMs
- Fallback testing (if notification fails)

### Phase 5: Documentation & Cleanup
- Update ARCHITECTURE.md
- Delete PlaytimeTracker files
- Delete obsolete tests
- Create NOTIFICATION_REFACTOR_NOTES.md
- Update USER_GUIDE.md with new workflow

---

## Fallback & Reliability

### What If Notifications Don't Work?
1. **Windows Notification Handler unavailable** → Log warning, continue monitoring
2. **Discord not installed** → Feature disabled, core blocking still works
3. **Notification parsing fails** → Skip notification, log error, continue listening
4. **Monitor crashes** → Watchdog restarts within 60s, no game close (expected behavior)

### Recommended Fallback
- Keep `notificationMonitorEnabled=true` by default
- Process watching still active as safety net
- If Discord notifications fail: System still functional with core blocking

---

## Questions for Implementation

1. **Discord User ID Format**: Numeric (e.g., "123456789") or string with @ (e.g., "@username")?
2. **Keyword Matching**: Exact match, substring, or regex?
3. **Multiple Keywords**: Support comma-separated triggers (e.g., "close,kill,stop")?
4. **Notification Persistence**: Log all notifications or only filtered ones?
5. **Persistence Across Sessions**: Should notification listener start/stop on config reload, or run continuously?
6. **UI Update**: Should settings UI (if implemented) include Discord config fields?

---

## Success Criteria

✅ All existing unit tests pass (except removed PlaytimeTracker tests)
✅ Game detection still works via log monitoring
✅ Graceful kill/restart works when triggered by notification
✅ Discord notifications are intercepted and logged
✅ Trigger keyword matching works correctly
✅ No playtime/after-hours scheduling code remains
✅ Configuration properly validated without removed settings
✅ Documentation updated to reflect new architecture

---

## Files Summary

**Total Files:**
- Files to delete: 2 (`PlaytimeTracker.cs`, `PlaytimeTrackerTests.cs`)
- Files to significantly modify: 4 (`RobloxGuardConfig.cs`, `LogMonitor.cs`, `TaskSchedulerHelper.cs`, `Program.cs`)
- Files to add: 1 (`DiscordNotificationListener.cs`)
- Files to delete tests for: 2 (`PlaytimeTrackerTests.cs`, `SilentModeTests.cs`)
- Files to keep unchanged: 12+

**Lines of Code Reduction:**
- Remove: ~650 (PlaytimeTracker) + ~400 (tests) = ~1,050 LOC
- Add: ~200-300 (DiscordNotificationListener)
- Modify: ~200 LOC (config/main loop simplification)
- **Net change:** ~500-700 LOC reduction

---

## Notes

- **Durability:** Process watching remains robust; monitor survives crashes and restarts
- **User Experience:** Game closure is now unpredictable (event-driven) vs. predictable (schedule-based) - more effective parental control
- **Complexity:** Overall complexity reduces significantly (no playtime tracking, no scheduling)
- **Extensibility:** Notification listener can be extended to support other triggers (SMS, HTTP webhook, etc.)

