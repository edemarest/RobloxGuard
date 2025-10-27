# RobloxGuard Architecture & Implementation Guide

Complete reference for understanding RobloxGuard's design, codebase structure, configuration, and all features.

## Table of Contents
1. [Project Overview](#project-overview)
2. [Codebase Structure](#codebase-structure)
3. [Core Components](#core-components)
4. [Configuration Reference](#configuration-reference)
5. [Features](#features)
6. [Runtime Architecture](#runtime-architecture)
7. [Game Detection & Blocking](#game-detection--blocking)
8. [Advanced Features](#advanced-features)
9. [Backlog & Future Ideas](#backlog--future-ideas)

---

## Project Overview

**RobloxGuard** is a Windows parental control system for Roblox that blocks specific games by `placeId`.

### Goals
- ✅ Per-user install (no admin required)
- ✅ Transparent operation (runs in background)
- ✅ Configurable blocking rules (blacklist/whitelist)
- ✅ Comprehensive logging for audit trail
- ✅ Graceful game termination with auto-restart

### Key Philosophy
- **Out-of-process only**: No DLL injection, no graphics hooking
- **Session persistence**: Survives monitor crashes
- **Robust watchdog**: Auto-restarts failed monitors within 60s
- **Silent operation**: Comprehensive logging, no UI popups (unless configured)

---

## Codebase Structure

```
src/
├── RobloxGuard.Core/
│   ├── PlaceIdParser.cs           # Extract placeId from URIs/CLI
│   ├── RegistryHelper.cs           # Manage Windows Registry for protocol handler
│   ├── RobloxGuardConfig.cs        # Config model + ConfigManager (load/save/verify)
│   ├── LogMonitor.cs               # Main monitoring loop (watches Roblox logs)
│   ├── PlaytimeTracker.cs          # Track playtime, schedule delayed kills
│   ├── SessionStateManager.cs      # Persist session state across restarts
│   ├── RobloxRestarter.cs          # Graceful kill + auto-restart to home
│   ├── MonitorStateHelper.cs       # Detect if monitor is running/responsive
│   ├── PidLockHelper.cs            # Lockfile-based single-instance enforcement
│   ├── HeartbeatHelper.cs          # Monitor liveness detection
│   └── TaskSchedulerHelper.cs      # Create Windows scheduled tasks
│
├── RobloxGuard.UI/
│   ├── Program.cs                  # Entry point (all command modes)
│   ├── AlertForm.cs                # Red blocking alert window
│   └── InstallerHelper.cs          # Installation/uninstall logic
│
├── RobloxGuard.Core.Tests/
│   ├── PlaceIdParserTests.cs       # URI/CLI parsing tests
│   ├── ConfigManagerTests.cs       # Config loading/validation tests
│   └── RobloxRestarterTests.cs     # Kill/restart sequence tests
│
└── RobloxGuard.Installers/
    └── (Inno Setup installer packaging - future)
```

---

## Core Components

### 1. **PlaceIdParser.cs**
**Purpose:** Extract game `placeId` from various input formats

**Inputs:**
- Protocol URIs: `roblox://experiences/start?placeId=1818`
- CLI args: `RobloxPlayerBeta.exe --id 519015469`
- Launcher URLs: `PlaceLauncher.ashx?...placeId=1416690850`

**Regexes Used:**
```csharp
/[?&]placeId=(\d+)/                           // Direct placeId param
/PlaceLauncher\.ashx.*?[?&]placeId=(\d+)/    // Embedded launcher URL
/--id\s+(\d+)/                                 // CLI --id flag
```

**Key Method:** `long? Extract(string input)`

---

### 2. **RobloxGuardConfig.cs & ConfigManager**
**Purpose:** Load, save, and validate configuration

**Config File Location:** `%LOCALAPPDATA%\RobloxGuard\config.json`

**Supported Modes:**
- **Blacklist mode** (default): Block only listed games, allow everything else
- **Whitelist mode**: Allow only listed games, block everything else

**Example Config:**
```json
{
  "blockedGames": [
    {"placeId": 15532962292, "name": "BRAINDEAD CONTENT DETECTED"},
    {"placeId": 1818, "name": "Adult Game"}
  ],
  "blocklist": [15532962292, 1818],
  "parentPINHash": "pbkdf2:sha256:iterations:salt:hash",
  "upstreamHandlerCommand": "C:\\original\\roblox\\handler.exe \"%1\"",
  "overlayEnabled": true,
  "whitelistMode": false,
  "silentMode": true,
  "autoRestartOnKill": true,
  "playtimeLimitEnabled": false,
  "playtimeLimitMinutes": 120,
  "afterHoursEnforcementEnabled": false,
  "afterHoursStartTime": 3,
  "blockedGameKillDelayMinutesMin": 0,
  "blockedGameKillDelayMinutesMax": 60
}
```

**Key Methods:**
- `ConfigManager.Load()` - Load config (create default if missing)
- `ConfigManager.Save(config)` - Write config to disk
- `ConfigManager.IsBlocked(placeId, config)` - Check if game is blocked
- `ConfigManager.HashPIN(pin)` - Hash PIN with PBKDF2
- `ConfigManager.VerifyPIN(pin, hash)` - Verify PIN against hash

---

### 3. **LogMonitor.cs**
**Purpose:** Main monitoring loop - watches Roblox log file for game joins

**How It Works:**
1. Reads `%APPDATA%\Roblox\logs\Player.log` continuously (tail-like approach)
2. Extracts `placeId` from each log line using PlaceIdParser
3. On new game join:
   - Checks if game is blocked
   - Records session state (SessionStateManager)
   - Calls PlaytimeTracker if configured
   - Optionally shows Block UI (if not silent)
   - Optionally kills immediately (if `killBlockedGameImmediately=true`)
4. Updates heartbeat every ~1s (for watchdog monitoring)
5. On monitor exit: Clears heartbeat, clears session

**Key Methods:**
- `LogMonitor.Start()` - Begin monitoring loop
- `LogMonitor.Stop()` - Graceful shutdown
- `LogMonitor.OnGameDetected(...)` - Handler for game join

**Session Persistence:**
- On startup: Loads `.session.json` (if active session exists)
- Resumes PlaytimeTracker from persisted state
- Ensures kills fire at correct time even after monitor restart

---

### 4. **PlaytimeTracker.cs**
**Purpose:** Track playtime and schedule delayed game kills

**Features:**
- Records when blocked game is joined
- Calculates elapsed time from join
- Schedules kill at: `joinTime + (playtimeLimitMinutes + randomDelay)`
- Can resume from persisted session state

**Configuration:**
```json
"playtimeLimitEnabled": true,           // Enable feature
"playtimeLimitMinutes": 120,            // Kill after 2 hours
"blockedGameKillDelayMinutesMin": 0,    // Minimum random delay
"blockedGameKillDelayMinutesMax": 60    // Maximum random delay (0-60 min)
```

**Example Timeline:**
```
09:59:57 - Game joined, PlaytimeTracker initialized
10:01:57 - Playtime: 2 minutes
10:01:57 - Kill scheduled: random delay (e.g., 2 min) → kill at ~10:03:57
10:03:57 - PlaytimeTracker.CheckPlaytimeLimit() fires
          - Elapsed time matches threshold
          - Calls LogMonitor.KillAndRestartToHome()
```

**Key Methods:**
- `PlaytimeTracker.RecordGameJoin(placeId)` - Track game join
- `PlaytimeTracker.RecordGameExit()` - Clean up on game exit
- `PlaytimeTracker.ResumeSession(SessionState)` - Resume from saved state
- `PlaytimeTracker.CheckPlaytimeLimit()` - Verify if kill should fire (called every loop iteration)

---

### 5. **SessionStateManager.cs**
**Purpose:** Persist session state across monitor restarts

**Behavior:**
- On game join: Writes `.session.json` with `{placeId, sessionGuid, joinTimeUtc, lastHeartbeatUtc}`
- Updates heartbeat every ~1s (proves session is active)
- On startup: Loads session if heartbeat is fresh (<30s stale)
- On game exit: Deletes `.session.json`

**Stale Detection:**
- If monitor dies and stays dead >30s: Session becomes "stale" and is abandoned
- If monitor restarts within 30s: Session is resumed (kill fires at correct elapsed time)

**File Location:** `%LOCALAPPDATA%\RobloxGuard\.session.json`

**Key Methods:**
- `SessionStateManager.SaveSession(placeId, sessionGuid, joinTimeUtc)`
- `SessionStateManager.LoadActiveSession()` - Returns null if stale
- `SessionStateManager.UpdateHeartbeat()`
- `SessionStateManager.ClearSession()`

---

### 6. **RobloxRestarter.cs**
**Purpose:** Kill Roblox process gracefully and restart to home screen

**Kill Sequence:**
1. Find RobloxPlayerBeta.exe process
2. Send graceful close signal (WM_CLOSE) with 2-second timeout
3. If timeout, force kill process with children
4. Wait 500ms for cleanup
5. Restart Roblox executable to home screen

**Roblox Path Discovery:**
- Searches `%APPDATA%\Roblox\Versions\version-XXXX\RobloxPlayerBeta.exe`
- Selects **latest version** by modification time
- Falls back to Program Files paths
- Finally tries registry lookup (HKCU then HKLM)

**Configuration:**
```json
"autoRestartOnKill": true,        // Enable auto-restart (default)
"GracefulCloseTimeoutMs": 2000,   // Timeout before force kill
"KillRestartDelayMs": 500         // Delay between kill and restart
```

**Key Methods:**
- `RobloxRestarter.KillAndRestartToHome(reason)`
- `RobloxRestarter.FindRobloxExecutable()` - Locate Roblox binary

---

### 7. **MonitorStateHelper.cs & PidLockHelper.cs**
**Purpose:** Single-instance enforcement and health checks

**Mechanism:**
- PidLockHelper: Writes monitor PID to lockfile at `%LOCALAPPDATA%\RobloxGuard\.monitor.lock`
- MonitorStateHelper: Checks if process still exists + heartbeat is fresh
- Watchdog: Verifies monitor every 1 minute

**Watchdog Flow:**
1. Scheduled task runs `RobloxGuard.exe --check-monitor` every 1 minute
2. Checks if monitor process alive AND heartbeat fresh (<30s)
3. If dead/hung: Kills process + restarts monitor

**Key Methods:**
- `PidLockHelper.CreateLock(processId)` - Create lockfile
- `PidLockHelper.IsMonitorRunning()` - Check if process exists
- `MonitorStateHelper.IsMonitorResponsive()` - Process exists AND heartbeat fresh
- `HeartbeatHelper.IsHeartbeatFresh(maxAgeSeconds)` - Check last update time

---

### 8. **RegistryHelper.cs**
**Purpose:** Manage Windows Registry for protocol handler registration

**Registry Structure (per-user):**
```
HKCU\Software\Classes\roblox-player\
  ├─ (Default) = "Roblox Player URL"
  ├─ URL Protocol = "" (empty)
  ├─ DefaultIcon\(Default) = "<path>\RobloxGuard.exe",0
  └─ shell\open\command\(Default) = "<path>\RobloxGuard.exe" --handle-uri "%1"

HKCU\Software\RobloxGuard\
  └─ Upstream = "<original roblox handler command>"
```

**Key Methods:**
- `RegistryHelper.InstallProtocolHandler(exePath)` - Register RobloxGuard
- `RegistryHelper.RestoreProtocolHandler()` - Restore original handler
- `RegistryHelper.BackupCurrentProtocolHandler()` - Save original command
- `RegistryHelper.IsBootstrapEntryRegistered()` - Verify bootstrap entry

---

### 9. **AlertForm.cs**
**Purpose:** Red blocking alert window (Windows Forms)

**UI:**
- Red background (#1a1a1a with red accents)
- Large X icon and "BRAINDEAD CONTENT DETECTED" text
- "Request unlock" and "Back to favorites" buttons
- Pin entry field (optional)
- 20-second timeout (auto-close)

**Configuration:**
```json
"overlayEnabled": true,         // Enable alert window
"silentMode": false             // Show alert on block (vs. silent)
```

---

### 10. **TaskSchedulerHelper.cs**
**Purpose:** Create Windows scheduled tasks for auto-start and watchdog

**Tasks Created:**
1. **RobloxGuardLogonTask** - Restart monitor on user logon
   - Trigger: At logon (current user)
   - Action: `RobloxGuard.exe --watch`
   - Restart on failure: 3 times, 1-minute interval

2. **RobloxGuardWatchdog** - Monitor health checks every minute
   - Trigger: Recurring every 1 minute
   - Action: `RobloxGuard.exe --check-monitor`
   - Auto-restart failed health checks

**Note:** Task creation may fail without admin. System falls back to watchdog-only mode (startup task not critical for core functionality).

---

## Configuration Reference

### Top-Level Settings

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `blockedGames` | Array | `[]` | List of blocked games with placeId + name |
| `blocklist` | Array | `[]` | Legacy: Auto-synced from blockedGames |
| `parentPINHash` | String | null | PBKDF2 hash of parent PIN (for PIN verification) |
| `upstreamHandlerCommand` | String | null | Original Roblox handler (for restoration) |
| `overlayEnabled` | Boolean | true | Show alert window when game blocked |
| `whitelistMode` | Boolean | false | Whitelist-only mode vs. default blacklist |

### Feature: Silent Mode

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `silentMode` | Boolean | true | Suppress Block UI (comprehensive logging instead) |

### Feature: Auto-Restart on Kill

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `autoRestartOnKill` | Boolean | true | Restart Roblox to home after kill |
| `GracefulCloseTimeoutMs` | Integer | 2000 | Timeout for graceful close before force kill |
| `KillRestartDelayMs` | Integer | 500 | Delay between kill and restart |

### Feature A: Playtime Limit

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `playtimeLimitEnabled` | Boolean | false | Enable playtime-based kill scheduling |
| `playtimeLimitMinutes` | Integer | 120 | Kill after N minutes of gameplay (2 hours) |
| `showBlockUIOnPlaytimeKill` | Boolean | true | Show alert when playtime kill fires |
| `blockedGameKillDelayMinutesMin` | Integer | 0 | Minimum random delay (0 min) |
| `blockedGameKillDelayMinutesMax` | Integer | 60 | Maximum random delay (up to 60 min) |

**Example:** `playtimeLimitMinutes=120, delayMin=0, delayMax=60`
- Game joined at 09:00
- PlaytimeTracker schedules kill at 09:00 + 120 min + random(0-60) min
- Kill fires between 11:00 and 12:00

### Feature B: After-Hours Enforcement

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `afterHoursEnforcementEnabled` | Boolean | false | Enable time-of-day-based enforcement |
| `afterHoursStartTime` | Integer | 3 | Start hour (0-23) for enforcement window |
| `showBlockUIOnAfterHoursKill` | Boolean | true | Show alert on after-hours kill |
| `blockedGameKillDelayMinutesMin` | Integer | 0 | Min delay before after-hours kill |
| `blockedGameKillDelayMinutesMax` | Integer | 60 | Max delay before after-hours kill |

**Example:** `afterHoursStartTime=3` means enforcement starts at 3:00 AM

### Blocked Game Kill Behavior

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `killBlockedGameImmediately` | Boolean | true | Kill on join vs. apply delay settings |

When `false`: Blocked games are only tracked by PlaytimeTracker (useful for "AFK mode" where you want delays instead of instant blocking).

---

## Features

### 1. **Game Blocking by PlaceId**
- Extract placeId from protocol URIs, CLI args, or log entries
- Match against blocklist (or whitelist if enabled)
- Immediate block action (kill + alert + restart)
- Comprehensive logging

### 2. **Silent Mode**
- When enabled: No Block UI popups
- All blocking decisions logged to `launcher.log`
- Better for stealth operation or minimizing disruption

### 3. **Playtime Limit Enforcement**
- Track how long player stays in blocked game
- Schedule kill at `joinTime + limit + randomDelay`
- Random delay prevents predictable enforcement pattern
- Useful for: Time-limited game access (e.g., 2 hours max playtime)

### 4. **After-Hours Enforcement** (Feature B)
- Enforce access restrictions during specific hours (e.g., after 3 AM)
- Kill any blocked game joined during enforcement window
- Can be combined with playtime limits

### 5. **Session Persistence**
- Survives monitor process crashes
- Resumes playtime kills at correct elapsed time
- Automatic restart via watchdog within ~60 seconds
- 30-second stale detection prevents orphaned sessions

### 6. **Graceful Kill + Auto-Restart**
- Send WM_CLOSE to Roblox main window
- 2-second timeout before force kill
- Auto-restart to home screen (user can pick new game immediately)
- Configurable timeouts and delays

### 7. **Watchdog Health Checks**
- Scheduled task runs every 1 minute
- Checks: Process alive AND heartbeat fresh (<30s)
- Auto-restarts monitor if hung or dead
- Ensures continuous monitoring even after crashes

### 8. **PIN-Protected Settings**
- Parent can set PIN hash
- Child must enter PIN to unlock games
- PBKDF2 hashing (secure)
- (UI not yet implemented - backend ready)

### 9. **Blacklist vs. Whitelist Modes**
- **Blacklist** (default): Block specific games, allow everything else
- **Whitelist**: Allow specific games, block everything else

---

## Runtime Architecture

### Startup Flow

```
1. RobloxGuard.exe launched (or auto-started by watchdog)
   ↓
2. Program.Main() routes to handler based on args:
   - No args → HandleAutoStartMode()
   - --watch → LogMonitor.Start() (main monitoring loop)
   - --handle-uri "%1" → Protocol handler mode (future enhancement)
   - --check-monitor → Watchdog health check
   - --uninstall → PerformUninstall()
   ↓
3. HandleAutoStartMode():
   - Check if monitor already running (PidLockHelper)
   - If yes: Exit (single-instance enforcement)
   - If no: Start LogMonitor
   ↓
4. LogMonitor.Start():
   - Load config
   - Load persisted session (if exists and fresh)
   - Initialize PlaytimeTracker (resume if needed)
   - Initialize RobloxRestarter
   - Setup scheduled tasks (if possible, fail gracefully)
   - Begin tail-reading Roblox log file
   - Update heartbeat every ~1s (for watchdog)
```

### Monitoring Loop

```
while (monitor running):
  1. Read new log lines from Roblox Player.log
  2. Extract placeId from each line
  3. If new game detected:
     - Check if blocked
     - Record session (SessionStateManager)
     - Call PlaytimeTracker.RecordGameJoin()
     - If KillBlockedGameImmediately: Kill now
     - Else: Wait for playtime/after-hours trigger
  4. Update session heartbeat
  5. Check PlaytimeTracker.CheckPlaytimeLimit() every iteration
     - If playtime threshold reached: Execute kill
  6. Check for Roblox exit (cleanup session)
  7. Sleep 100ms before next iteration
```

---

## Crash Recovery & Session Backfill (v1.7+)

### Problem
When the monitor crashes **before** saving the current session, the game is still running but no persisted state exists. Upon restart, the monitor has no knowledge that the game is active, so playtime kills don't execute.

### Solution: Session Backfill from Roblox Logs

**Architecture:**
1. Monitor starts → attempts `LoadActiveSession()` from `~/.session.json`
2. If file not found or stale: `TryBackfillSessionFromRecentLogs()` scans Roblox logs
3. Backfill looks for most recent game join within the last 5 minutes
4. If found: Creates synthetic SessionState and calls `PlaytimeTracker.ResumeSession()`
5. PlaytimeTracker recalculates elapsed time and reschedules kill

**Implementation Details (LogMonitor.cs):**
```csharp
/// Scans Roblox logs for recent game joins (last 5 minutes)
/// Returns synthetic SessionState if user still in-game
private SessionStateManager.SessionState? TryBackfillSessionFromRecentLogs()
{
  // 1. Check if RobloxPlayerBeta.exe is still running (no point backfilling if not)
  if (!Process.GetProcessesByName("RobloxPlayerBeta").Any())
    return null;
  
  // 2. Find most recent Roblox log file in %LOCALAPPDATA%\Roblox\logs\
  var logFile = Directory.GetFiles(logDir, "*_Player_*_last.log")
    .OrderByDescending(f => /* parse timestamp from filename */)
    .FirstOrDefault();
  
  // 3. Scan log in REVERSE order for most recent game join
  // Pattern: "[2025-10-26T15:21:38.037Z] ... GameJoinLoadTime ... placeId:15532962292"
  foreach (var line in File.ReadAllLines(logFile).Reverse())
  {
    if (line.Contains("placeid:") && line.Contains("GameJoinLoadTime"))
    {
      // 4. Extract placeId and timestamp
      var placeId = Regex.Match(line, @"placeid:(\d+)").Groups[1].Value;
      var timeStr = line.Substring(0, 24);  // ISO8601: 2025-10-26T15:21:38.037Z
      
      // 5. CRITICAL: Parse with RoundtripKind to ensure DateTime.Kind = Utc
      DateTime.TryParseExact(timeStr, "yyyy-MM-ddTHH:mm:ss.fffZ", 
        CultureInfo.InvariantCulture, 
        DateTimeStyles.RoundtripKind,  // Respects Z suffix → Kind=Utc
        out var joinTime);
      
      // 6. Safety check: Only backfill if join is within 5 minutes
      if (joinTime > DateTime.UtcNow.AddMinutes(-5))
      {
        return new SessionState { PlaceId = placeId, JoinTimeUtc = joinTime };
      }
    }
  }
  return null;  // No recent join found
}
```

**Timezone Handling (CRITICAL FIX):**
- **Before Fix**: Used `DateTimeStyles.AssumeUniversal | AdjustToUniversal`
  - Result: UTC timestamps treated as LOCAL time → 4-hour mismatch on EDT/EST systems
  - Example: Join at 15:21:38 UTC reported as 240+ minutes old (4+ hours)
  
- **After Fix**: Use `DateTimeStyles.RoundtripKind`
  - Respects Z suffix in ISO8601 string → correctly preserves Kind=Utc
  - Added defensive check: `if (ts.Kind != DateTimeKind.Utc) ts = DateTime.SpecifyKind(ts, DateTimeKind.Utc);`
  - Result: Elapsed time calculated correctly (23.3 sec instead of 240 min)

**All DateTime Logging Updated (v1.7+):**
- Changed all `DateTime.Now` → `DateTime.UtcNow` with Z suffix
- Files updated: LogMonitor, PlaytimeTracker, SessionStateManager, HeartbeatHelper, AppDataHelper, PidLockHelper, TaskSchedulerHelper, MonitorStateHelper, RobloxRestarter, Program.cs
- Logging format: `[HH:mm:ss.fff Z]` for consistency

**Test Results:**
```
Scenario: Monitor crashes while user playing blocked game
  1. Game joined at 15:21:38Z (while monitor dead)
  2. Monitor auto-restart at 15:22:01Z
  3. Backfill detects join: Kind=Utc ✓, Elapsed=23.3sec ✓
  4. Session resumed with correct elapsed time
  5. PlaytimeTracker re-evaluates: 2min ≥ 2min limit → KILL SCHEDULED
  6. Kill executes at scheduled time → GAME TERMINATED ✓
```

---

### Watchdog Health Check (Every 1 Minute)

```
1. RobloxGuard.exe --check-monitor (triggered by scheduled task)
   ↓
2. Program.HandleCheckMonitor():
   - Verify registry bootstrap entry exists (recreate if missing)
   - Call MonitorStateHelper.IsMonitorResponsive()
     • Check if PID lock exists
     • Check if heartbeat fresh (<30s)
   - If responsive: Exit (monitor is healthy)
   ↓
3. If dead/hung:
   - Kill any orphaned process
   - Call HandleAutoStartMode() to restart monitor
```

---

## Game Detection & Blocking

### Protocol Handler Path (Optional)

```
1. User clicks roblox:// link
   ↓
2. OS calls: RobloxGuard.exe --handle-uri "roblox://experiences/start?placeId=1818"
   ↓
3. RobloxGuard extracts placeId=1818
   ↓
4. Config check:
   - If blocked: Show Block UI, DON'T launch Roblox
   - If allowed: Forward to upstream handler (original Roblox launcher)
```

**Note:** Protocol handler mode is implemented but not required. Game detection can work via log monitoring alone.

### Log Monitor Path (Primary)

```
1. LogMonitor continuously reads %APPDATA%\Roblox\logs\Player.log
   ↓
2. Detects pattern: "[<timestamp>] [Player] Game 15532962292 loaded"
   ↓
3. Extracts placeId=15532962292
   ↓
4. Config check:
   - If blocked:
     • Record session
     • If killBlockedGameImmediately=true: Kill immediately
     • Else: Track in PlaytimeTracker (schedule delayed kill)
   - If allowed: Continue monitoring
```

### Blind Spots
- **In-game teleports**: Not intercepted (no injection)
- **Private servers**: May use different placeId tracking
- **Unavailable placeIds**: If Roblox API unreachable, name resolution skipped

---

## Advanced Features

### 1. Playtime Tracking with Random Delays

**Use Case:** "2-hour limit with unpredictable enforcement"

```json
{
  "playtimeLimitEnabled": true,
  "playtimeLimitMinutes": 120,
  "blockedGameKillDelayMinutesMin": 2,     // Minimum delay: 2 min
  "blockedGameKillDelayMinutesMax": 3      // Maximum delay: 3 min
}
```

Timeline:
- 09:59:57 - Game joined
- Random delay selected (e.g., 2 min)
- Kill scheduled for: 09:59:57 + 120 min + 2 min = 12:01:57
- 12:01:57 - Kill executes

**Why randomize?** Prevents predictable enforcement (e.g., child knows kill always happens at exactly 2 hours).

### 2. Silent Mode (No UI)

```json
{
  "silentMode": true,
  "overlayEnabled": false
}
```

- No Block UI popup appears
- All actions logged to `%LOCALAPPDATA%\RobloxGuard\launcher.log`
- Useful for: Stealth operation, minimizing disruption, parental monitoring without confrontation

### 3. Whitelist Mode

```json
{
  "whitelistMode": true,
  "blockedGames": [
    {"placeId": 1, "name": "Allowed Game 1"},
    {"placeId": 2, "name": "Allowed Game 2"}
  ]
}
```

- **Only** placeId 1 and 2 are allowed
- Everything else is blocked
- Reverse of default (blacklist) behavior

### 4. Auto-Restart with Custom Delays

```json
{
  "autoRestartOnKill": true,
  "GracefulCloseTimeoutMs": 3000,    // Wait 3s for graceful close
  "KillRestartDelayMs": 1000         // Wait 1s before restarting
}
```

- Roblox has 3 seconds to close gracefully
- If still alive: Force kill
- Wait 1 second for cleanup
- Launch Roblox to home screen

---

## Backlog & Future Ideas

### High Priority (Feasible, Not Yet Implemented)

1. **Protocol Handler Mode (Partial)**
   - ✅ URI parsing implemented
   - ⚠️ Block logic works, but UI popup not fully integrated
   - TODO: Route Block UI to protocol handler path
   - TODO: Forward to upstream handler on allow

2. **Settings UI**
   - Currently: Manual JSON editing only
   - TODO: WPF/Forms UI for:
     - Add/remove blocked games
     - Set parent PIN
     - Toggle features (playtime, after-hours, silent mode)
     - View logs with filtering

3. **Telemetry & Reporting**
   - Currently: Logs only to local file
   - TODO: Optional cloud sync of:
     - Enforcement events (timestamp, placeId, action)
     - Playtime summaries
     - After-hours violations

### Medium Priority (More Complex)

4. **Process Renaming/Anti-Cheating**
   - **Idea:** Rename RobloxGuard.exe to masquerade as system process
   - **Benefit:** Child can't easily spot/kill the monitor
   - **Complexity:** Needs code signing, scheduled task updates
   - **Status:** Backlog - defer until core features stable

5. **Graceful Disconnect Instead of Kill**
   - **Idea:** Use Roblox API to disconnect player instead of killing process
   - **Problem:** Roblox doesn't expose public disconnect API
   - **Status:** Research needed - may not be feasible

6. **In-Game Teleport Interception**
   - **Idea:** Monitor in-game teleports to catch blocked games
   - **Problem:** Requires DLL injection (explicitly out of scope)
   - **Status:** Backlog - would violate core principle (no injection)

7. **Machine Learning-Based Time Enforcement**
   - **Idea:** Learn typical playtime patterns, predict violations
   - **Benefit:** More intelligent enforcement ("you usually stop at 11pm")
   - **Complexity:** High - requires telemetry + analytics backend
   - **Status:** Future enhancement, not in MVP

### Low Priority (Nice-to-Have)

8. **Mobile App Integration**
   - Receive enforcement alerts on parent's phone
   - Remotely add/remove blocked games
   - View child's playtime summary
   - **Status:** Future, requires cloud backend

9. **Cross-Device Sync**
   - Sync blocklist across multiple devices
   - Unified enforcement policy
   - **Status:** Future, requires cloud backend

10. **Uninstall Protection**
    - Prevent child from uninstalling
    - Require parent PIN to uninstall
    - **Status:** Tricky - can be subverted by formatting drive
    - Current: Clean uninstall available (requires command line knowledge)

---

## Logging

### Log File
Location: `%LOCALAPPDATA%\RobloxGuard\launcher.log`

### Key Log Patterns

```
[HH:MM:SS.fff] [Component] Event description

// Game detection
[09:59:57.420] [LogMonitor] Game 15532962292 IS BLOCKED
[09:59:57.421] [OnGameDetected] Game is blocked, showing alert

// Playtime tracking
[10:01:57.478] [PlaytimeTracker].CheckPlaytimeLimit() Kill scheduled: 2min delay → 14:03:57Z
[10:03:57.488] [PlaytimeTracker].ExecuteScheduledKill() Kill time reached!

// Graceful kill + restart
[10:03:57.496] [RobloxRestarter.KillAndRestartToHome] ===== Kill + Restart Sequence =====
[10:03:59.944] [RobloxRestarter.KillRobloxProcess] PID 21304: ✓ Force kill successful
[10:04:00.452] [RobloxRestarter.FindRobloxExecutable] ✓ Found: C:\Users\...\RobloxPlayerBeta.exe
[10:04:00.454] [RobloxRestarter.RestartToHome] ✓ Roblox restarted successfully

// Session persistence
[HH:MM:SS] [LogMonitor.Start] Resuming persisted session: placeId=15532962292, elapsed=0.5min

// Watchdog
[HH:MM:SS] === CHECK-MONITOR HEALTH CHECK (Watchdog) ===
[HH:MM:SS] ✓ Monitor is healthy and running
```

---

## Installation & Startup

### Manual Installation
```powershell
.\RobloxGuard.exe
# First run creates:
# - %LOCALAPPDATA%\RobloxGuard\config.json
# - %LOCALAPPDATA%\RobloxGuard\launcher.log
# - Windows scheduled tasks (if admin possible)
```

### Command-Line Modes
```powershell
.\RobloxGuard.exe                    # Auto-start (monitor or skip if running)
.\RobloxGuard.exe --watch            # Start log monitor
.\RobloxGuard.exe --handle-uri "%1"  # Protocol handler (for roblox:// links)
.\RobloxGuard.exe --check-monitor    # Health check (for watchdog)
.\RobloxGuard.exe --uninstall        # Clean uninstall (restore original handler)
```

---

## Performance & Reliability

### Resource Usage
- **Memory:** ~50-100 MB (depends on log file size)
- **CPU:** <1% idle, <5% during active monitoring
- **Disk:** Minimal (JSON config + text log appending)

### Reliability
- **Single-instance enforcement:** PID lockfile prevents duplicate monitors
- **Crash recovery:** Watchdog restarts within ~60s
- **Session persistence:** Resumes playtime kills even after multi-minute downtime
- **Graceful degradation:** Scheduled tasks optional (fallback to watchdog-only)

---

## Troubleshooting

### Monitor stops unexpectedly
1. Check `%LOCALAPPDATA%\RobloxGuard\launcher.log` for CRITICAL ERROR
2. Look for exception stack traces
3. Verify config.json syntax (use JSON validator)
4. Restart monitor: `.\RobloxGuard.exe --watch`

### Kill doesn't fire at scheduled time
1. Verify `playtimeLimitEnabled: true` in config
2. Check elapsed time calculation in logs
3. Confirm Roblox process still running (may have exited naturally)
4. Look for PlaytimeTracker logs in launcher.log

### Roblox doesn't restart after kill
1. Check for "Roblox executable not found" in logs
2. Verify Roblox is installed
3. Check version folders exist: `%APPDATA%\Roblox\Versions\version-XXXX\RobloxPlayerBeta.exe`
4. Try manual restart of Roblox

### Watchdog task creation fails
1. Normal if running without admin
2. System falls back to watchdog-only mode
3. Monitor auto-restarts on next user action (startup, protocol handler, etc.)
4. Core functionality (session persistence, playtime kills) unaffected

---

## Code Quality Notes

- **Testing:** Unit tests cover PlaceIdParser, ConfigManager, RobloxRestarter
- **Platform:** Windows-only (no cross-platform support planned)
- **Dependencies:** .NET 8, no external NuGet packages (intentional - simplify deployment)
- **Logging:** Comprehensive to `launcher.log` for audit trail
- **Error Handling:** Try-catch blocks with detailed logging (fail gracefully)

---

## File Manifest

### Core Business Logic
- `PlaceIdParser.cs` - URI/CLI parsing (~150 lines)
- `RobloxGuardConfig.cs` - Config model + manager (~370 lines)
- `LogMonitor.cs` - Main monitoring loop (~730 lines)
- `PlaytimeTracker.cs` - Playtime scheduling (~650 lines)
- `SessionStateManager.cs` - Session persistence (~145 lines)
- `RobloxRestarter.cs` - Graceful kill/restart (~440 lines)

### Infrastructure
- `MonitorStateHelper.cs` - Health checks (~150 lines)
- `PidLockHelper.cs` - Single-instance (~200 lines)
- `HeartbeatHelper.cs` - Liveness detection (~100 lines)
- `RegistryHelper.cs` - Registry management (~140 lines)
- `TaskSchedulerHelper.cs` - Scheduled tasks (~230 lines)

### UI & Installation
- `AlertForm.cs` - Block alert window (~400 lines)
- `InstallerHelper.cs` - Install/uninstall (~400 lines)
- `Program.cs` - Entry point + command handlers (~650 lines)

### Tests
- `PlaceIdParserTests.cs` - URI parsing tests
- `ConfigManagerTests.cs` - Config validation tests
- `RobloxRestarterTests.cs` - Kill/restart tests

**Total:** ~5,500 lines of core logic + 1,000+ lines of tests

---

## Summary

RobloxGuard is a modular, reliable Windows parental control system built on:
1. **Continuous log monitoring** for game detection
2. **Session persistence** for crash recovery
3. **Watchdog-based health checks** for auto-restart
4. **Graceful termination** with configurable delays and auto-restart
5. **Comprehensive logging** for transparency and debugging

The architecture prioritizes reliability and clarity over complexity, with every major component independently testable and documented.
