using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.Versioning;

namespace RobloxGuard.Core;

/// <summary>
/// Robust playtime tracking and enforcement with two configurable features:
/// 
/// Feature A: Playtime Limit (AFK Detection)
///   - Kills blocked games after 2+ continuous hours of play
///   - Applies random 0-60 minute delay to prevent pattern exploitation
///   - Perfect for enforcing daily playtime limits
/// 
/// Feature B: After-Hours Enforcement (Bedtime)
///   - Kills any blocked game joined after 3 AM local time
///   - Applies random 0-60 minute delay to prevent workarounds
///   - Perfect for enforcing bedtime cutoffs
/// 
/// Design Principles:
///   - Stateless per check: All decisions made in CheckAndApplyLimits()
///   - Windows-native: Uses DateTime, no external dependencies
///   - Robust error handling: All exceptions caught, logged, recovery attempted
///   - Durable monitoring: Cohesive with watchdog/heartbeat architecture
///   - Comprehensive logging: Every decision logged for parent visibility
///   - Timezone-aware: Local time for after-hours, UTC for tracking
/// </summary>
[SupportedOSPlatform("windows")]
public class PlaytimeTracker : IDisposable
{
    // ========== Configuration & Logging ==========
    
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    private const string LOG_PREFIX = "[PlaytimeTracker]";
    private const string ERROR_PREFIX = "[PlaytimeTracker.ERROR]";

    // ========== State Management ==========
    
    /// <summary>
    /// Current active game session. Null if not tracking.
    /// </summary>
    private GameSession? _activeSession;

    /// <summary>
    /// Lock for thread-safe session updates
    /// </summary>
    private readonly object _sessionLock = new object();

    /// <summary>
    /// Lazy initialization of config
    /// </summary>
    private readonly Func<dynamic> _getConfig;

    /// <summary>
    /// Callback to execute when game kill is needed
    /// </summary>
    private readonly Action<string> _onTerminate;

    /// <summary>
    /// Callback for testing/diagnostics
    /// </summary>
    private readonly Action<string>? _onDiagnostic;

    /// <summary>
    /// Last error state (to prevent log spam)
    /// </summary>
    private string? _lastErrorMessage;
    private DateTime _lastErrorTime = DateTime.MinValue;
    private const int ERROR_SUPPRESSION_SECONDS = 30;

    public PlaytimeTracker(Func<dynamic> getConfig, Action<string> onTerminate, Action<string>? onDiagnostic = null)
    {
        _getConfig = getConfig ?? throw new ArgumentNullException(nameof(getConfig));
        _onTerminate = onTerminate ?? throw new ArgumentNullException(nameof(onTerminate));
        _onDiagnostic = onDiagnostic;

        LogToFile($"{LOG_PREFIX} Initialized successfully");
        LogConfigSnapshot();
    }

    /// <summary>
    /// Log current config values for debugging
    /// </summary>
    private void LogConfigSnapshot()
    {
        try
        {
            var config = _getConfig();
            var limitEnabled = config.PlaytimeLimitEnabled ?? false;
            var limitMinutes = config.PlaytimeLimitMinutes ?? 120;
            var delayMin = config.BlockedGameKillDelayMinutesMin ?? 0;
            var delayMax = config.BlockedGameKillDelayMinutesMax ?? 60;
            var afterHoursEnabled = config.AfterHoursEnforcementEnabled ?? false;
            var silentMode = config.SilentMode ?? true;

            LogToFile($"{LOG_PREFIX} === CONFIG SNAPSHOT ===");
            LogToFile($"{LOG_PREFIX}   PlaytimeLimitEnabled: {limitEnabled}");
            LogToFile($"{LOG_PREFIX}   PlaytimeLimitMinutes: {limitMinutes}");
            LogToFile($"{LOG_PREFIX}   BlockedGameKillDelayMin: {delayMin}min");
            LogToFile($"{LOG_PREFIX}   BlockedGameKillDelayMax: {delayMax}min");
            LogToFile($"{LOG_PREFIX}   AfterHoursEnforcementEnabled: {afterHoursEnabled}");
            LogToFile($"{LOG_PREFIX}   SilentMode: {silentMode}");
        }
        catch (Exception ex)
        {
            LogError("LogConfigSnapshot", $"Failed to log config: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Resumes tracking from a persisted session state (e.g., after monitor restart).
    /// Calculates elapsed time from original join and reschedules kills if needed.
    /// </summary>
    public void ResumeSession(SessionStateManager.SessionState persistedSession)
    {
        try
        {
            lock (_sessionLock)
            {
                var elapsed = persistedSession.ElapsedTime;
                LogToFile($"{LOG_PREFIX}.ResumeSession() Resuming: placeId={persistedSession.PlaceId}, elapsed={elapsed.TotalMinutes:F1}min, joinTime={persistedSession.JoinTimeUtc:HH:mm:ss}Z");

                // Create session from persisted state
                _activeSession = new GameSession
                {
                    PlaceId = persistedSession.PlaceId,
                    SessionGuid = persistedSession.SessionGuid,
                    JoinTime = persistedSession.JoinTimeUtc,
                    JoinTimeLocal = persistedSession.JoinTimeUtc.ToLocalTime(),
                    IsBlocked = ConfigManager.IsBlocked(persistedSession.PlaceId, _getConfig()),
                    LastHeartbeat = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Immediately check if kills should be scheduled based on elapsed time
                var config = _getConfig();
                if (_activeSession.IsBlocked && config.PlaytimeLimitEnabled == true)
                {
                    CheckPlaytimeLimit(_activeSession, DateTime.UtcNow, config);
                }
            }
        }
        catch (Exception ex)
        {
            LogError("ResumeSession", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    // ========== Public API ==========

    /// <summary>
    /// Call when "! Joining game 'GUID' place ID" detected in log.
    /// Thread-safe.
    /// </summary>
    public void RecordGameJoin(long placeId, string sessionGuid, DateTime timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(sessionGuid))
        {
            LogError("RecordGameJoin", "sessionGuid is null or empty");
            return;
        }

        try
        {
            lock (_sessionLock)
            {
                LogToFile($"{LOG_PREFIX}.RecordGameJoin() called: placeId={placeId}, guid={sessionGuid.Substring(0, 8)}..., time={timestampUtc:HH:mm:ss}Z");

                // Check if this is the same session continuing
                if (_activeSession?.SessionGuid == sessionGuid && _activeSession?.PlaceId == placeId)
                {
                    LogToFile($"{LOG_PREFIX}.RecordGameJoin() Same session continuing (respawn?)");
                    _activeSession.LastHeartbeat = timestampUtc;
                    return;
                }

                // Different place or different GUID = session change
                if (_activeSession != null)
                {
                    var prevDuration = timestampUtc - _activeSession.JoinTime;
                    LogToFile($"{LOG_PREFIX}.RecordGameJoin() Previous session ended: placeId={_activeSession.PlaceId}, duration={prevDuration.TotalMinutes:F1}min");
                }

                // Load config for current decisions
                var config = _getConfig();
                var isBlocked = ConfigManager.IsBlocked(placeId, config);

                // Create new session
                _activeSession = new GameSession
                {
                    PlaceId = placeId,
                    SessionGuid = sessionGuid,
                    JoinTime = timestampUtc,
                    JoinTimeLocal = timestampUtc.ToLocalTime(),
                    IsBlocked = isBlocked,
                    LastHeartbeat = timestampUtc,
                    CreatedAtUtc = DateTime.UtcNow
                };

                LogToFile($"{LOG_PREFIX}.RecordGameJoin() New session: placeId={placeId}, blocked={isBlocked}, localTime={_activeSession.JoinTimeLocal:HH:mm:ss}");

                // Persist session to disk for crash recovery
                SessionStateManager.SaveSession(placeId, sessionGuid, timestampUtc);

                // Immediately schedule kills if needed
                if (isBlocked)
                {
                    // Feature B: After-hours enforcement
                    if (config.AfterHoursEnforcementEnabled == true)
                    {
                        var hourLocal = _activeSession.JoinTimeLocal.Hour;
                        if (hourLocal >= (config.AfterHoursStartTime ?? 3))
                        {
                            ScheduleAfterHoursKill(_activeSession, config);
                        }
                        else
                        {
                            LogToFile($"{LOG_PREFIX}.RecordGameJoin() After-hours check: hour={hourLocal}, threshold={(config.AfterHoursStartTime ?? 3)} - NOT triggered");
                        }
                    }
                    else
                    {
                        LogToFile($"{LOG_PREFIX}.RecordGameJoin() After-hours enforcement disabled");
                    }

                    // Feature A: Will be checked on every CheckAndApplyLimits() call
                    if (config.PlaytimeLimitEnabled == true)
                    {
                        LogToFile($"{LOG_PREFIX}.RecordGameJoin() Playtime limit enabled - will check every iteration");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError("RecordGameJoin", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Call when "Disconnection Notification" or "Client:Disconnect" detected in log.
    /// Thread-safe.
    /// </summary>
    public void RecordGameExit(DateTime timestampUtc)
    {
        try
        {
            lock (_sessionLock)
            {
                if (_activeSession == null)
                {
                    LogToFile($"{LOG_PREFIX}.RecordGameExit() No active session, ignoring");
                    return;
                }

                var duration = timestampUtc - _activeSession.JoinTime;
                LogToFile($"{LOG_PREFIX}.RecordGameExit() Session ended: placeId={_activeSession.PlaceId}, duration={duration.TotalMinutes:F1}min, reason=normal_exit");

                // Clear any pending scheduled kills
                if (_activeSession.ScheduledKillTime.HasValue)
                {
                    LogToFile($"{LOG_PREFIX}.RecordGameExit() Pending kill cleared (was scheduled for {_activeSession.ScheduledKillTime:HH:mm:ss}Z)");
                    _activeSession.ScheduledKillTime = null;
                    _activeSession.ScheduledKillReason = null;
                }

                _activeSession = null;
                
                // Clear persisted session on normal exit
                SessionStateManager.ClearSession();
            }
        }
        catch (Exception ex)
        {
            LogError("RecordGameExit", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Call regularly from LogMonitor loop (every 100ms).
    /// Checks if action needed based on Feature A or Feature B.
    /// Thread-safe.
    /// </summary>
    public void CheckAndApplyLimits(DateTime nowUtc)
    {
        try
        {
            lock (_sessionLock)
            {
                if (_activeSession == null || !_activeSession.IsBlocked)
                {
                    return;
                }

                var config = _getConfig();

                // Feature A: Check playtime limit
                if (config.PlaytimeLimitEnabled == true)
                {
                    CheckPlaytimeLimit(_activeSession, nowUtc, config);
                }

                // Feature B: Check if scheduled kill time reached
                if (_activeSession.ScheduledKillTime.HasValue && nowUtc >= _activeSession.ScheduledKillTime.Value)
                {
                    ExecuteScheduledKill(_activeSession, nowUtc);
                }
            }
        }
        catch (Exception ex)
        {
            LogError("CheckAndApplyLimits", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Check if there's an active game session (used for startup logic)
    /// </summary>
    public bool HasActiveSession()
    {
        lock (_sessionLock)
        {
            return _activeSession != null;
        }
    }

    /// <summary>
    /// Get current session info for diagnostics/testing
    /// </summary>
    public GameSessionInfo? GetCurrentSessionInfo()
    {
        try
        {
            lock (_sessionLock)
            {
                if (_activeSession == null)
                    return null;

                return new GameSessionInfo
                {
                    PlaceId = _activeSession.PlaceId,
                    SessionGuid = _activeSession.SessionGuid,
                    IsBlocked = _activeSession.IsBlocked,
                    JoinTimeUtc = _activeSession.JoinTime,
                    JoinTimeLocal = _activeSession.JoinTimeLocal,
                    ElapsedMinutes = (DateTime.UtcNow - _activeSession.JoinTime).TotalMinutes,
                    ScheduledKillTime = _activeSession.ScheduledKillTime,
                    ScheduledKillReason = _activeSession.ScheduledKillReason,
                    PlaytimeLimitTriggered = _activeSession.PlaytimeLimitTriggered
                };
            }
        }
        catch (Exception ex)
        {
            LogError("GetCurrentSessionInfo", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            lock (_sessionLock)
            {
                _activeSession = null;
                LogToFile($"{LOG_PREFIX} Disposed successfully");
            }
        }
        catch { }
    }

    // ========== Private Implementation ==========

    /// <summary>
    /// Feature A: Check if playtime limit exceeded, schedule kill if needed
    /// </summary>
    private void CheckPlaytimeLimit(GameSession session, DateTime nowUtc, dynamic config)
    {
        try
        {
            // Already triggered?
            if (session.PlaytimeLimitTriggered)
                return;

            var limitMinutes = config.PlaytimeLimitMinutes ?? 120;
            var elapsedMinutes = (nowUtc - session.JoinTime).TotalMinutes;

            if (elapsedMinutes >= limitMinutes)
            {
                session.PlaytimeLimitTriggered = true;

                LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit() TRIGGERED!");
                LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit()   Elapsed: {elapsedMinutes:F1}min >= Limit: {limitMinutes}min");

                // Schedule kill within configured delay window
                var delayMinutesMin = (int)(config.BlockedGameKillDelayMinutesMin ?? 0);
                var delayMinutesMax = (int)(config.BlockedGameKillDelayMinutesMax ?? 60);
                
                LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit()   Using config: DelayMin={delayMinutesMin}min, DelayMax={delayMinutesMax}min");
                
                // Validate range
                if (delayMinutesMin < 0) delayMinutesMin = 0;
                if (delayMinutesMax < delayMinutesMin) delayMinutesMax = delayMinutesMin + 60;
                
                var delayMinutes = Random.Shared.Next(delayMinutesMin, delayMinutesMax + 1);
                var killTime = nowUtc.AddMinutes(delayMinutes);

                session.ScheduledKillTime = killTime;
                session.ScheduledKillReason = $"Playtime limit exceeded ({elapsedMinutes:F1}min played, kill in {delayMinutes}min)";

                LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit()   Random delay selected: {delayMinutes}min (from {delayMinutesMin}-{delayMinutesMax} range)");
                LogToFile($"{LOG_PREFIX}.CheckPlaytimeLimit()   Kill scheduled: {delayMinutes}min delay → {killTime:HH:mm:ss}Z");
            }
        }
        catch (Exception ex)
        {
            LogError("CheckPlaytimeLimit", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Feature B: Schedule kill for after-hours blocked game
    /// </summary>
    private void ScheduleAfterHoursKill(GameSession session, dynamic config)
    {
        try
        {
            var delayMinutesMin = (int)(config.BlockedGameKillDelayMinutesMin ?? 0);
            var delayMinutesMax = (int)(config.BlockedGameKillDelayMinutesMax ?? 60);

            // Validate range
            if (delayMinutesMin < 0) delayMinutesMin = 0;
            if (delayMinutesMax < delayMinutesMin) delayMinutesMax = delayMinutesMin + 60;

            var delayMinutes = Random.Shared.Next(delayMinutesMin, delayMinutesMax + 1);
            var killTime = DateTime.UtcNow.AddMinutes(delayMinutes);

            session.ScheduledKillTime = killTime;
            session.ScheduledKillReason = $"After-hours enforcement (joined {session.JoinTimeLocal:HH:mm:ss}, kill in {delayMinutes}min)";

            LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill() SCHEDULED!");
            LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill()   Joined at (local): {session.JoinTimeLocal:HH:mm:ss}");
            LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill()   Kill delay: {delayMinutes}min");
            LogToFile($"{LOG_PREFIX}.ScheduleAfterHoursKill()   Kill time (UTC): {killTime:HH:mm:ss}Z");
        }
        catch (Exception ex)
        {
            LogError("ScheduleAfterHoursKill", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Execute scheduled kill after verifying player still in game
    /// </summary>
    private void ExecuteScheduledKill(GameSession session, DateTime nowUtc)
    {
        try
        {
            LogToFile($"{LOG_PREFIX}.ExecuteScheduledKill() Kill time reached!");
            LogToFile($"{LOG_PREFIX}.ExecuteScheduledKill()   Reason: {session.ScheduledKillReason}");

            // Verify player still in game
            if (!IsStillInGame(session, nowUtc))
            {
                LogToFile($"{LOG_PREFIX}.ExecuteScheduledKill() Player left game before kill - skipping");
                session.ScheduledKillTime = null;
                session.ScheduledKillReason = null;
                return;
            }

            // Verify process still exists
            if (!IsRobloxProcessRunning())
            {
                LogToFile($"{LOG_PREFIX}.ExecuteScheduledKill() Roblox process not running - skipping");
                session.ScheduledKillTime = null;
                session.ScheduledKillReason = null;
                return;
            }

            // Check if silent mode is enabled
            var config = _getConfig();
            var silentMode = config.SilentMode ?? true;  // Default to true (silent)

            LogToFile($"{LOG_PREFIX}.ExecuteScheduledKill() EXECUTING KILL: {session.ScheduledKillReason}");
            LogToFile($"{LOG_PREFIX}.ExecuteScheduledKill()   Silent Mode: {silentMode}");

            // Execute kill - if silent mode is OFF, show Block UI (via callback)
            // If silent mode is ON, just terminate without UI
            if (!silentMode)
            {
                // Show Block UI by invoking callback
                _onTerminate($"{LOG_PREFIX} {session.ScheduledKillReason}");
            }
            else
            {
                // Silent kill - just terminate without showing UI
                LogToFile($"{LOG_PREFIX}.ExecuteScheduledKill()   Silently terminating process (no UI popup)");
                // Call terminate but with empty/silent reason to suppress UI
                _onTerminate("");
            }

            // Clear to prevent double-kill
            session.ScheduledKillTime = null;
            session.ScheduledKillReason = null;
        }
        catch (Exception ex)
        {
            LogError("ExecuteScheduledKill", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Verify player is still in game (not left recently)
    /// </summary>
    private bool IsStillInGame(GameSession session, DateTime nowUtc)
    {
        try
        {
            // If session is less than 5 hours old, assume still playing
            var ageMinutes = (nowUtc - session.JoinTime).TotalMinutes;
            return ageMinutes < 300;  // 5 hours max
        }
        catch (Exception ex)
        {
            LogError("IsStillInGame", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
            return true;  // Fail-safe: assume still playing
        }
    }

    /// <summary>
    /// Check if RobloxPlayerBeta process is running
    /// </summary>
    private bool IsRobloxProcessRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("RobloxPlayerBeta");
            return processes.Length > 0;
        }
        catch (Exception ex)
        {
            LogError("IsRobloxProcessRunning", $"Exception: {ex.GetType().Name}: {ex.Message}", ex);
            return true;  // Fail-safe: assume running
        }
    }

    // ========== Logging & Error Handling ==========

    /// <summary>
    /// Robust logging to launcher.log with automatic directory creation
    /// </summary>
    private static void LogToFile(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(_logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}Z] {message}\n");
        }
        catch
        {
            // Last resort: try to write to console
            try
            {
                Debug.WriteLine(message);
            }
            catch { }
        }
    }

    /// <summary>
    /// Robust error logging with suppression to prevent spam
    /// </summary>
    private void LogError(string method, string error, Exception? ex = null)
    {
        try
        {
            var now = DateTime.UtcNow;
            var timeSinceLastError = (now - _lastErrorTime).TotalSeconds;

            // Suppress repetitive errors (only log every 30 seconds if same error)
            if (error == _lastErrorMessage && timeSinceLastError < ERROR_SUPPRESSION_SECONDS)
            {
                return;
            }

            _lastErrorMessage = error;
            _lastErrorTime = now;

            var message = $"{ERROR_PREFIX}.{method}() {error}";
            if (ex != null)
            {
                message += $"\n{ERROR_PREFIX}.{method}() StackTrace: {ex.StackTrace}";
            }

            LogToFile(message);
            _onDiagnostic?.Invoke(message);
        }
        catch { }
    }

    // ========== Data Structures ==========

    /// <summary>
    /// Active game session tracking
    /// </summary>
    private class GameSession
    {
        public long PlaceId { get; set; }
        public string SessionGuid { get; set; } = "";
        public DateTime JoinTime { get; set; }
        public DateTime JoinTimeLocal { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        // Playtime limit tracking (Feature A)
        public bool PlaytimeLimitTriggered { get; set; } = false;

        // Scheduled kill tracking (Feature A & B)
        public DateTime? ScheduledKillTime { get; set; }
        public string? ScheduledKillReason { get; set; }
    }

    /// <summary>
    /// Public diagnostic info for testing/monitoring
    /// </summary>
    public class GameSessionInfo
    {
        public long PlaceId { get; set; }
        public string SessionGuid { get; set; } = "";
        public bool IsBlocked { get; set; }
        public DateTime JoinTimeUtc { get; set; }
        public DateTime JoinTimeLocal { get; set; }
        public double ElapsedMinutes { get; set; }
        public DateTime? ScheduledKillTime { get; set; }
        public string? ScheduledKillReason { get; set; }
        public bool PlaytimeLimitTriggered { get; set; }
    }
}
