# Current Architecture Deep Dive - What We Have

**Understanding the Existing System Before Refactoring**

---

## System Overview

RobloxGuard is a **Windows parental control** that blocks Roblox games by `placeId`. Current implementation uses **time-based scheduling** to enforce restrictions.

```
┌──────────────────────────────────────────────────────────────┐
│                    RobloxGuard System                         │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  Entry Point: Program.cs                                      │
│    ├─ No args → HandleAutoStartMode() [Monitor]              │
│    ├─ --watch → LogMonitor.Start()                           │
│    ├─ --handle-uri "%1" → Protocol handler                   │
│    ├─ --check-monitor → Watchdog health check                │
│    └─ --uninstall → Clean removal                            │
│                                                               │
│  Core Components:                                             │
│    ├─ LogMonitor.cs [MAIN LOOP]                              │
│    │  ├─ Tail Roblox log file                                │
│    │  ├─ Detect placeId from game joins                      │
│    │  ├─ Check if blocked (RobloxGuardConfig)                │
│    │  └─ Execute blocking action (PlaytimeTracker/Kill)      │
│    │                                                           │
│    ├─ PlaytimeTracker.cs [⭐ WILL REMOVE]                    │
│    │  ├─ Track elapsed time in game                          │
│    │  ├─ Schedule kill at: join + limit + randomDelay        │
│    │  └─ Execute scheduled kills                             │
│    │                                                           │
│    ├─ RobloxRestarter.cs [GRACEFUL KILL]                     │
│    │  ├─ Send WM_CLOSE to Roblox window                      │
│    │  ├─ Force kill if timeout                               │
│    │  └─ Auto-restart to home                                │
│    │                                                           │
│    ├─ SessionStateManager.cs [STATE PERSISTENCE]             │
│    │  ├─ Save session to .session.json on game join          │
│    │  ├─ Update heartbeat every ~1s                          │
│    │  ├─ Detect stale sessions (>30s old)                    │
│    │  └─ Restore session on monitor restart                  │
│    │                                                           │
│    └─ RobloxGuardConfig.cs [CONFIGURATION]                   │
│       ├─ Load config.json                                    │
│       ├─ Validate blocked games                              │
│       ├─ Check whitelist/blacklist mode                      │
│       └─ Apply PIN verification                              │
│                                                               │
│  Infrastructure:                                              │
│    ├─ RegistryHelper.cs [PROTOCOL HANDLER]                   │
│    │  └─ Register/restore roblox-player: protocol            │
│    │                                                           │
│    ├─ TaskSchedulerHelper.cs [BACKGROUND TASKS]              │
│    │  ├─ RobloxGuardLogonTask [On user logon]                │
│    │  └─ RobloxGuardWatchdog [Every 1 minute]                │
│    │                                                           │
│    ├─ MonitorStateHelper.cs [HEALTH CHECKS]                  │
│    │  └─ Verify monitor alive + responsive                   │
│    │                                                           │
│    ├─ HeartbeatHelper.cs [LIVENESS]                          │
│    │  └─ Update .heartbeat file every ~1s                    │
│    │                                                           │
│    ├─ PidLockHelper.cs [SINGLE-INSTANCE]                     │
│    │  └─ Enforce only one monitor running                    │
│    │                                                           │
│    └─ PlaceIdParser.cs [EXTRACTION]                          │
│       ├─ Extract from protocol URIs                          │
│       ├─ Extract from CLI args                               │
│       └─ Extract from log lines                              │
│                                                               │
│  UI:                                                           │
│    └─ AlertForm.cs [BLOCKING ALERT]                          │
│       ├─ Red window with X icon                              │
│       ├─ "BRAINDEAD CONTENT DETECTED"                        │
│       └─ "Request Unlock" + PIN entry                        │
│                                                               │
└──────────────────────────────────────────────────────────────┘
```

---

## Detailed Component Analysis

### 1. LogMonitor.cs - The Heart of the System

**Purpose:** Main monitoring loop that watches Roblox activity

**Current Flow:**
```csharp
// Startup
LogMonitor.Start()
  ├─ Load RobloxGuardConfig
  ├─ Initialize SessionStateManager
  ├─ Initialize PlaytimeTracker  [⭐ WILL REMOVE]
  ├─ Initialize RobloxRestarter
  ├─ Setup scheduled tasks (via TaskSchedulerHelper)
  ├─ Create PID lockfile
  ├─ Setup heartbeat file
  └─ Enter main monitoring loop

// Main Loop (every 100ms)
while (running)
{
    // 1. Read new Roblox log lines
    var newLines = ReadNewLogLines();
    
    // 2. Process each line
    foreach (var line in newLines)
    {
        var placeId = PlaceIdParser.Extract(line);
        
        if (placeId != null)
        {
            // 3. Check if game is blocked
            var blocked = RobloxGuardConfig.IsBlocked(placeId);
            
            if (blocked)
            {
                // 4. Record session (for recovery)
                SessionStateManager.SaveSession(placeId);
                
                // 5. Notify PlaytimeTracker
                PlaytimeTracker.RecordGameJoin(placeId);  // ⭐ WILL REMOVE
                
                // 6. Execute blocking action
                if (config.killBlockedGameImmediately)
                {
                    RobloxRestarter.KillAndRestartToHome("Blocked game");
                }
            }
        }
    }
    
    // 7. Check if kill should fire [⭐ WILL REMOVE]
    PlaytimeTracker.CheckPlaytimeLimit();
    
    // 8. Detect game exit
    if (RobloxProcessNotRunning())
    {
        SessionStateManager.ClearSession();
        PlaytimeTracker.Reset();  // ⭐ WILL REMOVE
    }
    
    // 9. Update heartbeat (for watchdog monitoring)
    HeartbeatHelper.Update();
    
    // 10. Sleep before next iteration
    Thread.Sleep(100);
}
```

**What We're Keeping:**
- Lines 1-6 (startup)
- Lines 2, 3, 4, 6, 8-10 (core monitoring)
- Session persistence
- Heartbeat updates

**What We're Removing:**
- PlaytimeTracker initialization/polling (step 5, 7, 8)
- All `PlaytimeTracker.CheckPlaytimeLimit()` logic

**New Code We'll Add:**
- DiscordNotificationListener initialization
- Event handler for Discord notifications

---

### 2. PlaytimeTracker.cs - Schedule-Based Enforcement [⭐ TO REMOVE]

**Purpose:** Track elapsed time and schedule delayed kills

**Current Behavior:**
```csharp
class PlaytimeTracker
{
    private DateTime _joinTimeUtc;
    private long _placeId;
    private DateTime? _scheduledKillTimeUtc;
    
    public void RecordGameJoin(long placeId)
    {
        _placeId = placeId;
        _joinTimeUtc = DateTime.UtcNow;
        
        // Calculate kill time
        var playtimeLimitMs = config.playtimeLimitMinutes * 60_000;
        var randomDelayMs = random.Next(
            config.blockedGameKillDelayMinutesMin * 60_000,
            config.blockedGameKillDelayMinutesMax * 60_000
        );
        
        _scheduledKillTimeUtc = _joinTimeUtc
            .AddMilliseconds(playtimeLimitMs)
            .AddMilliseconds(randomDelayMs);
        
        // Log it
        Logger.Info($"Kill scheduled: {playtimeLimitMs}ms + {randomDelayMs}ms → {_scheduledKillTimeUtc}");
    }
    
    public void CheckPlaytimeLimit()
    {
        if (_scheduledKillTimeUtc == null)
            return;
        
        var elapsedSeconds = (DateTime.UtcNow - _joinTimeUtc).TotalSeconds;
        var scheduledSeconds = (_scheduledKillTimeUtc.Value - _joinTimeUtc).TotalSeconds;
        
        if (elapsedSeconds >= scheduledSeconds)
        {
            Logger.Info($"Playtime limit reached! ({elapsedSeconds}s >= {scheduledSeconds}s)");
            ExecuteScheduledKill();
        }
    }
    
    private void ExecuteScheduledKill()
    {
        RobloxRestarter.KillAndRestartToHome("Playtime limit");
        _scheduledKillTimeUtc = null;  // Reset
    }
}
```

**Configuration Example:**
```json
{
  "playtimeLimitEnabled": true,
  "playtimeLimitMinutes": 120,           // Kill after 2 hours
  "blockedGameKillDelayMinutesMin": 0,   // Min random delay
  "blockedGameKillDelayMinutesMax": 60   // Max random delay (0-60 min)
}
```

**Why We're Removing It:**
- No more time-based scheduling needed
- Discord notifications replace schedule
- Reduces complexity significantly
- Removes ~650 lines of code

---

### 3. RobloxGuardConfig.cs - Configuration Management

**Current Configuration:**
```json
{
  // Game blocking
  "blockedGames": [
    {"placeId": 15532962292, "name": "Game 1"},
    {"placeId": 1818, "name": "Game 2"}
  ],
  "blocklist": [15532962292, 1818],      // Legacy sync
  
  // Security
  "parentPINHash": "pbkdf2:...",
  "upstreamHandlerCommand": "C:\\...\\handler.exe \"%1\"",
  
  // UI & Behavior
  "overlayEnabled": true,
  "silentMode": true,
  
  // Whitelist mode
  "whitelistMode": false,
  
  // Auto-restart on kill
  "autoRestartOnKill": true,
  "GracefulCloseTimeoutMs": 2000,
  "KillRestartDelayMs": 500,
  
  // ⭐ PLAYTIME SETTINGS (TO REMOVE)
  "playtimeLimitEnabled": false,
  "playtimeLimitMinutes": 120,
  "showBlockUIOnPlaytimeKill": true,
  "blockedGameKillDelayMinutesMin": 0,
  "blockedGameKillDelayMinutesMax": 60,
  
  // ⭐ AFTER-HOURS SETTINGS (TO REMOVE)
  "afterHoursEnforcementEnabled": false,
  "afterHoursStartTime": 3,
  "showBlockUIOnAfterHoursKill": true,
  
  // ⭐ KILL TIMING (TO REMOVE)
  "killBlockedGameImmediately": true
}
```

**Changes for Discord Refactor:**

Remove:
```json
"playtimeLimitEnabled"
"playtimeLimitMinutes"
"showBlockUIOnPlaytimeKill"
"blockedGameKillDelayMinutesMin"
"blockedGameKillDelayMinutesMax"
"afterHoursEnforcementEnabled"
"afterHoursStartTime"
"showBlockUIOnAfterHoursKill"
"killBlockedGameImmediately"
```

Add:
```json
"discordNotificationEnabled": false,
"discordSourceUserId": "",
"discordTriggerKeyword": "close game",
"notificationMonitorEnabled": true
```

---

### 4. SessionStateManager.cs - Crash Recovery [KEEP]

**Purpose:** Persist session state so kills fire correctly even after monitor crashes

**Current Behavior:**
```csharp
// File: %LOCALAPPDATA%\RobloxGuard\.session.json
{
  "placeId": 15532962292,
  "sessionGuid": "12345678-1234-...",
  "joinTimeUtc": "2025-10-26T14:23:45.123Z",
  "lastHeartbeatUtc": "2025-10-26T14:24:01.456Z"
}

// Logic:
SaveSession(placeId) → Create .session.json
UpdateHeartbeat() → Update lastHeartbeatUtc (every ~1s)
LoadActiveSession() → Read .session.json if heartbeat fresh (<30s)
ClearSession() → Delete .session.json on game exit
```

**Why This Stays:**
- Still need to track which game is running
- Can recover if monitor crashes
- Discord listener will need to know current session to trigger kill
- Core crash-recovery mechanism

---

### 5. RobloxRestarter.cs - Graceful Kill [KEEP]

**Purpose:** Kill Roblox process gracefully, then auto-restart to home

**Current Behavior:**
```csharp
public void KillAndRestartToHome(string reason)
{
    Logger.Info($"===== Kill + Restart Sequence ({reason}) =====");
    
    // 1. Find RobloxPlayerBeta.exe process
    var proc = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
    if (proc == null)
    {
        Logger.Warn("Roblox process not found");
        return;
    }
    
    // 2. Send WM_CLOSE to main window (graceful close)
    SendWmClose(proc, timeout: 2000);
    
    // 3. If still alive, force kill
    if (!proc.HasExited)
    {
        proc.Kill(entireProcessTree: true);
        proc.WaitForExit(5000);
    }
    
    // 4. Wait for cleanup
    Thread.Sleep(500);
    
    // 5. Restart Roblox to home screen
    var robloxPath = FindRobloxExecutable();
    if (robloxPath != null)
    {
        Process.Start(robloxPath);
        Logger.Info("Roblox restarted successfully");
    }
}
```

**Why This Stays:**
- Core functionality needed by all blocking mechanisms
- Works whether kill is scheduled or event-driven
- No changes needed for Discord refactor

---

### 6. TaskSchedulerHelper.cs - Background Tasks [KEEP or SIMPLIFY]

**Current Tasks Created:**
```
1. RobloxGuardLogonTask
   ├─ Trigger: At user logon
   ├─ Action: RobloxGuard.exe --watch
   └─ Restart on failure: 3x, 1-min intervals

2. RobloxGuardWatchdog
   ├─ Trigger: Every 1 minute
   ├─ Action: RobloxGuard.exe --check-monitor
   └─ Purpose: Health check (monitor alive + responsive)
```

**Decision for Discord Refactor:**
- **Option A:** Keep both (safe, proven reliable)
- **Option B:** Remove watchdog (notification listener more efficient)
- **Recommendation:** Keep both (belt + suspenders approach)

---

### 7. MonitorStateHelper.cs - Health Checks [KEEP]

**Current Behavior:**
```
Every 1 minute (triggered by watchdog task):
  1. Check if monitor process exists (PID)
  2. Check if heartbeat is fresh (<30s old)
  3. If responsive: Continue
  4. If dead/hung:
     - Kill orphaned process
     - Restart monitor
```

**Why This Stays:**
- Ensures monitor stays alive even after crashes
- Works with or without Discord notifications
- No changes needed

---

### 8. PlaceIdParser.cs - Game Detection [KEEP]

**Current Extraction Methods:**
```csharp
// From protocol URI
Input: "roblox://experiences/start?placeId=1818"
Regex: /[?&]placeId=(\d+)/
Output: 1818

// From launcher URL
Input: "...PlaceLauncher.ashx?placeId=1416690850..."
Regex: /PlaceLauncher\.ashx.*?[?&]placeId=(\d+)/
Output: 1416690850

// From CLI args
Input: "--id 519015469"
Regex: /--id\s+(\d+)/
Output: 519015469

// From Roblox logs
Input: "[2025-10-26T14:23:45.123Z] ... GameJoinLoadTime ... placeId:15532962292"
Regex: /placeId:(\d+)/
Output: 15532962292
```

**Why This Stays:**
- Game detection doesn't change with Discord refactor
- Still needed to identify which game is running
- No changes needed

---

## Current Feature Breakdown

### Active Features ✅

1. **Game Blocking by PlaceId**
   - Extract placeId from log files
   - Check against blocklist
   - Execute immediate kill or schedule for later

2. **Graceful Kill + Auto-Restart**
   - Send WM_CLOSE to Roblox window
   - Force kill if timeout
   - Auto-restart to home screen

3. **Session Persistence**
   - Save session state on game join
   - Update heartbeat every ~1s
   - Resume session after monitor restart (if within 30s)

4. **Watchdog Health Checks**
   - Monitor process every 1 minute
   - Auto-restart if dead/hung
   - Single-instance enforcement (PID lockfile)

5. **Alert UI**
   - Red blocking window on game kill
   - Shows game name / reason
   - Pin entry for unlock

6. **Configuration**
   - JSON-based config
   - Blacklist/whitelist modes
   - Silent mode (no UI popups)

### Schedule-Based Features (⭐ To Remove)

1. **Playtime Limits**
   - Track elapsed time in game
   - Kill after N minutes
   - Random delay (0-60 min) for unpredictability

2. **After-Hours Enforcement**
   - Block games during specific hours
   - Auto-kill if game joined during window

### Event-Based Features (✨ To Add)

1. **Discord Notifications**
   - Listen to Windows Notification Handler
   - Filter for Discord DMs
   - Detect trigger keyword (e.g., "close game")
   - Kill game immediately

---

## Execution Flow Diagrams

### Current (Schedule-Based) Flow

```
User joins blocked game
       ↓
LogMonitor detects in log
       ↓
PlaytimeTracker records join + schedules kill
       ↓
Main loop polls every 100ms: "Is it kill time?"
       ↓
... waiting 2+ hours + random delay ...
       ↓
"Yes, time to kill!"
       ↓
RobloxRestarter.KillAndRestartToHome()
       ↓
Show alert UI
       ↓
Game restarted to home
```

### New (Event-Driven) Flow

```
User joins blocked game
       ↓
LogMonitor detects in log + saves session
       ↓
[Waiting for parent action...]
       ↓
Parent sends Discord DM: "close game"
       ↓
DiscordNotificationListener receives notification
       ↓
Parses message + checks trigger keyword
       ↓
Looks up session: "Is a game running?"
       ↓
"Yes! Game is running!"
       ↓
Raises NotificationReceived event
       ↓
RobloxRestarter.KillAndRestartToHome()
       ↓
Show alert UI
       ↓
Game restarted to home
```

---

## Strengths of Current System

✅ **Reliable:** Survives monitor crashes via session persistence  
✅ **Durable:** Watchdog auto-restarts within 60s  
✅ **Non-invasive:** No DLL injection, pure out-of-process  
✅ **Configurable:** Multiple modes (silent, whitelist, etc.)  
✅ **Well-logged:** Comprehensive audit trail  
✅ **Tested:** Unit tests for core components  

---

## Weaknesses of Current System

❌ **Predictable:** Child knows game will close at scheduled time  
❌ **Complex:** Multiple scheduling mechanisms (playtime, after-hours)  
❌ **Overhead:** Polls every 100ms (though minimal)  
❌ **Hard to debug:** Schedule conflicts / edge cases  
❌ **Not parent-initiated:** Enforcement happens automatically  

---

## Why Discord Notification Makes Sense

### Current Problem
```
"I'm going to let them play for exactly 2 hours,
then my system will kill it at a predictable time."
→ Child knows when game will close
→ Can work around it (afk/log out before)
→ Less effective parental control
```

### New Solution
```
"I can monitor Discord and send a 'close game' command
whenever I want, at any time, unpredictably."
→ Child doesn't know when game will close
→ Parent has complete control
→ Much more effective parental control
→ Works seamlessly with existing system
```

---

## Summary: What Stays, What Goes, What's New

| Component | Status | Reason |
|-----------|--------|--------|
| PlaceIdParser | ✅ Keep | Game detection never changes |
| RobloxRestarter | ✅ Keep | Graceful kill is core |
| RegistryHelper | ✅ Keep | Protocol registration never changes |
| AlertForm | ✅ Keep | Alert UI still useful |
| LogMonitor | ⚠️ Modify | Remove playtime loop, add Discord init |
| SessionStateManager | ✅ Keep | Crash recovery still important |
| RobloxGuardConfig | ⚠️ Modify | Remove schedule settings, add Discord settings |
| TaskSchedulerHelper | ✅ Keep | Watchdog still valuable |
| **PlaytimeTracker** | ❌ Remove | Replaced by Discord notifications |
| **DiscordNotificationListener** | ✨ Add | New event-driven trigger |

---

## Ready for Implementation

You now have a complete understanding of:
- What the current system does
- How each component works
- What will change and why
- What stays the same
- Why the refactor makes sense

See `DISCORD_NOTIFICATION_REFACTOR_ANALYSIS.md` for detailed implementation plan.

