# Discord Notification Monitor Refactor - Executive Summary

**Status:** ✅ Branch created and ready for implementation
**Branch:** `feature/discord-notification-monitor`
**Base:** Current HEAD (c928a00)

---

## What You Have Now

You've successfully switched to a new branch that will transform RobloxGuard from a **schedule-driven** system to an **event-driven** system.

### Current Architecture (What We're Replacing)
- **PlaytimeTracker:** Schedules game kills based on elapsed time (120 minutes + random delay)
- **TaskScheduler:** Runs watchdog checks every 1 minute
- **AfterHours Logic:** Enforces restrictions during specific hours (e.g., after 3 AM)
- **Complexity:** ~650 lines of playtime tracking + scheduling logic

### New Architecture (What We're Building)
- **DiscordNotificationListener:** Subscribes to Windows Notification Handler
- **Event-Driven:** Game closes when parent sends a Discord DM with trigger keyword
- **Simplicity:** ~200-300 lines for notification listener, ~500 LOC net reduction
- **Durability:** Process watching + graceful kill/restart remain intact

---

## Key Architectural Insights

### ✅ Components to KEEP (Fully Intact)
These are the backbone and require zero changes:

1. **PlaceIdParser.cs** - Still needed for URI/CLI parsing
2. **RobloxRestarter.cs** - Still needed for graceful kill + restart
3. **RegistryHelper.cs** - Still needed for protocol handler registration
4. **LogMonitor.cs** - Core monitoring loop (but simplify logic)
5. **SessionStateManager.cs** - Track active sessions
6. **AlertForm.cs** - Show block UI when game killed
7. **HeartbeatHelper.cs** - Monitor liveness detection
8. **PidLockHelper.cs** - Single-instance enforcement
9. **MonitorStateHelper.cs** - Health checks

**Benefit:** 80% of your codebase remains unchanged = low risk of regression

### ❌ Components to DELETE
These are playtime-specific and won't be needed:

1. **PlaytimeTracker.cs** (~650 lines) - No more time-based scheduling
2. **PlaytimeTrackerTests.cs** - Tests for deleted component
3. Playtime/AfterHours config settings - Remove from RobloxGuardConfig

### ⚠️ Components to SIGNIFICANTLY MODIFY
These need refactoring but not deletion:

1. **LogMonitor.cs**
   - Remove: PlaytimeTracker polling loop
   - Remove: Playtime kill execution logic
   - Add: DiscordNotificationListener initialization
   - Add: Event handler for notification triggers
   - Result: Much simpler main loop (~50 lines shorter)

2. **RobloxGuardConfig.cs**
   - Remove: 8 playtime/after-hours settings
   - Add: 4 Discord notification settings
   - Result: Cleaner, more focused config

3. **TaskSchedulerHelper.cs** (Optional)
   - Can keep the 1-minute watchdog (good for reliability)
   - Or remove it if notification listener is reliable enough
   - Keep: Logon startup task

4. **Program.cs**
   - Update CLI help text
   - Remove playtime-related validation
   - Update config loading

### ✨ NEW: DiscordNotificationListener.cs
This is the star component that powers the new event-driven system:

```csharp
// Listens to Windows notifications
// Filters for Discord messages from specific user
// Detects trigger keyword (e.g., "close game")
// Fires NotificationReceived event → triggers game kill
```

---

## The Refactor Flow

### Step 1: Configuration Cleanup
```
Current Config:
{
  "playtimeLimitEnabled": true,
  "playtimeLimitMinutes": 120,
  "blockedGameKillDelayMinutesMin": 0,
  "blockedGameKillDelayMinutesMax": 60,
  "afterHoursEnforcementEnabled": false,
  "afterHoursStartTime": 3,
  ...
}

↓ REMOVE all of those ↓

New Config:
{
  "discordNotificationEnabled": true,
  "discordSourceUserId": "123456789",
  "discordTriggerKeyword": "close game",
  "notificationMonitorEnabled": true,
  ...
}
```

### Step 2: LogMonitor Simplification
```
BEFORE (Complex):
  Load config
  ↓
  Initialize PlaytimeTracker
  ↓
  Main loop:
    - Read logs
    - Detect game
    - Check PlaytimeTracker.CheckPlaytimeLimit() EVERY ITERATION
    - Sleep 100ms
    
AFTER (Simple):
  Load config
  ↓
  Initialize DiscordNotificationListener
  ↓
  Main loop:
    - Read logs
    - Detect game → Save to session
    - Sleep 100ms
  
  Separately:
    - Discord notification arrives
    - Check session (is game running?)
    - Kill game if active
```

### Step 3: New Event Handler
```
DiscordNotificationListener detects message:
  ↓
  Check source = target Discord user ID
  ↓
  Check message contains trigger keyword
  ↓
  Fire NotificationReceived event
  ↓
  LogMonitor's event handler:
    - Get current session (which game is running)
    - Call RobloxRestarter.KillAndRestartToHome()
    - Log the event
```

---

## Behavior Changes

### Old Way (Schedule-Based)
```
09:00 - Join blocked game
09:00 - PlaytimeTracker records join
09:00-12:02 - System waits (2 hours + random delay)
12:02 - Timer fires, game killed
      → Child knows game closes at predictable time
      → Can log out just before timer
```

### New Way (Notification-Based)
```
09:00 - Join blocked game
09:00 - Monitor records session
...waiting...
14:37 - Parent sends Discord: "close game"
14:37 - Monitor receives notification
14:37 - Game killed immediately
      → Child has NO IDEA when game will close
      → Parent has complete control
      → Much more effective parental control
```

---

## Files You'll Modify

| File | Action | Complexity |
|------|--------|-----------|
| `RobloxGuardConfig.cs` | Remove 8 settings, add 4 settings | ⭐⭐ Low |
| `LogMonitor.cs` | Remove playtime loop, add notification init | ⭐⭐⭐ Medium |
| `Program.cs` | Update config validation | ⭐ Low |
| `TaskSchedulerHelper.cs` | Optional: simplify or keep | ⭐ Low |
| `DiscordNotificationListener.cs` | **CREATE NEW** | ⭐⭐⭐ Medium |

**Total Modification Time:** Estimated 2-4 hours for full refactor + testing

---

## Risk Assessment

### Low Risk ✅
- 80% of codebase unchanged
- Core blocking logic untouched
- Session management remains same
- Kill/restart sequence unchanged
- Tests for parser, config, restarter still valid

### Medium Risk ⚠️
- Windows Notification Handler availability (may vary by OS)
- Discord notification parsing (format may change)
- New component (DiscordNotificationListener) needs thorough testing

### Mitigation
- Keep `notificationMonitorEnabled` fallback (process watching continues)
- Comprehensive logging of all notification events
- Graceful degradation if notifications unavailable
- Easy rollback (git branch revert)

---

## What You Get

### Advantages
1. **Simpler Code** - 500 LOC reduction
2. **More Effective** - Unpredictable enforcement (event-driven)
3. **Easier Maintenance** - Fewer moving parts
4. **Better UX for Parent** - Remote game control
5. **Same Durability** - Monitor survives crashes
6. **Future Extensibility** - Can add HTTP webhooks, SMS, etc.

### Tradeoffs
1. **Dependency on Windows Notifications** - May fail on some systems
2. **Discord Account Requirement** - Need to monitor bot/account
3. **Loss of Automatic Enforcement** - Must trigger manually (feature, not bug)

---

## Next Steps (Implementation Roadmap)

### Phase 1: Configuration Refactor
- [ ] Read current RobloxGuardConfig.cs
- [ ] Remove playtime/after-hours properties
- [ ] Add Discord notification properties
- [ ] Update ConfigManager validation
- [ ] Update default config template

### Phase 2: LogMonitor Simplification
- [ ] Remove PlaytimeTracker initialization
- [ ] Remove playtime polling logic from main loop
- [ ] Add DiscordNotificationListener initialization
- [ ] Verify game detection still works

### Phase 3: Create DiscordNotificationListener
- [ ] Implement Windows Notification Handler subscription
- [ ] Implement notification parsing
- [ ] Implement user ID + keyword filtering
- [ ] Implement event emission

### Phase 4: Integration
- [ ] Subscribe LogMonitor to notification events
- [ ] Call RobloxRestarter on trigger
- [ ] Test end-to-end

### Phase 5: Cleanup & Testing
- [ ] Delete PlaytimeTracker.cs
- [ ] Delete PlaytimeTrackerTests.cs
- [ ] Update ARCHITECTURE.md
- [ ] Run all unit tests
- [ ] Manual testing with Discord DMs

---

## Questions to Answer Before Starting

1. **Discord User ID**: How will parent provide it? Discord's numeric ID or username?
2. **Trigger Keyword**: Single word, phrase, or regex pattern?
3. **Multiple Triggers**: Support multiple keywords (e.g., "close,kill,stop")?
4. **Fallback Mode**: What happens if notifications are unavailable?
5. **Cloud Integration**: Future Discord bot for better reliability?

---

## Documentation Created

I've created a detailed analysis document:
- **File:** `docs/DISCORD_NOTIFICATION_REFACTOR_ANALYSIS.md`
- **Contains:** Component breakdown, config changes, implementation strategy, testing plan, success criteria

---

## Ready to Proceed?

Your new branch is ready! You have:

✅ New branch created: `feature/discord-notification-monitor`
✅ Comprehensive analysis document
✅ Clear component roadmap
✅ Risk assessment
✅ Implementation phases

**Next Action:** Ready to start Phase 1 (Configuration Refactor) whenever you are!

Would you like me to:
1. Start implementing Phase 1 (config refactor)?
2. Create more detailed technical specifications for any component?
3. Start with DiscordNotificationListener skeleton code?
4. Something else?

