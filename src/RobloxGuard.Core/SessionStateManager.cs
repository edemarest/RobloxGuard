using System.Text.Json;
using System.Runtime.Versioning;
using System.IO;

namespace RobloxGuard.Core;

/// <summary>
/// Manages persistent session state across monitor restarts.
/// Allows PlaytimeTracker to resume counting from the original game join time
/// even if the monitor crashes and restarts while the user is still in-game.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SessionStateManager
{
    private static readonly string _sessionFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        ".session.json"
    );

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Logs to launcher.log for debugging.
    /// </summary>
    private static void LogToFile(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RobloxGuard",
                "launcher.log"
            );
            var dir = Path.GetDirectoryName(logPath);
            if (dir != null)
                Directory.CreateDirectory(dir);
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}Z] [SessionStateManager] {message}\n");
        }
        catch { }
    }

    /// <summary>
    /// Represents an active game session with timing information.
    /// </summary>
    public class SessionState
    {
        public long PlaceId { get; set; }
        public string SessionGuid { get; set; } = "";
        public DateTime JoinTimeUtc { get; set; }
        public DateTime LastHeartbeatUtc { get; set; }

        // ========== Day Counter State (Phase 2) ==========
        
        /// <summary>
        /// Current day in the 3-day enforcement cycle (1, 2, or 3).
        /// Persisted to survive app/system restart.
        /// </summary>
        public int CurrentDayCounter { get; set; } = 1;

        /// <summary>
        /// Date of last enforcement action (ISO format: yyyy-MM-dd).
        /// Used to detect midnight boundary.
        /// </summary>
        public string LastKillDate { get; set; } = "";

        /// <summary>
        /// Reason for last enforcement (e.g., "PlaytimeLimit", "AfterHours").
        /// Diagnostic information for logging.
        /// </summary>
        public string LastKillReason { get; set; } = "";

        /// <summary>
        /// Calculates elapsed time in the current session.
        /// </summary>
        public TimeSpan ElapsedTime => DateTime.UtcNow - JoinTimeUtc;

        /// <summary>
        /// Checks if session is stale (heartbeat older than max age).
        /// Used to detect if user has actually left the game since monitor crashed.
        /// </summary>
        public bool IsStale(int maxAgeSeconds = 30)
        {
            return (DateTime.UtcNow - LastHeartbeatUtc).TotalSeconds > maxAgeSeconds;
        }

        /// <summary>
        /// Updates the heartbeat to now, proving the session is still active.
        /// </summary>
        public void UpdateHeartbeat()
        {
            LastHeartbeatUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Saves or updates the current session state to disk.
    /// Optionally includes day counter state from DayCounterManager.
    /// </summary>
    public static void SaveSession(long placeId, string sessionGuid, DateTime joinTimeUtc, 
        int dayCounter = 1, string lastKillDate = "", string lastKillReason = "")
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_sessionFile)!);

            var session = new SessionState
            {
                PlaceId = placeId,
                SessionGuid = sessionGuid,
                JoinTimeUtc = joinTimeUtc,
                LastHeartbeatUtc = DateTime.UtcNow,
                CurrentDayCounter = dayCounter,
                LastKillDate = lastKillDate,
                LastKillReason = lastKillReason
            };

            var json = JsonSerializer.Serialize(session, _jsonOptions);
            File.WriteAllText(_sessionFile, json);
            LogToFile($"✓ SaveSession: placeId={placeId}, dayCounter={dayCounter}, file={_sessionFile}");
        }
        catch (Exception ex)
        {
            LogToFile($"✗ SaveSession ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the persisted session state if it exists and is not stale.
    /// Returns null if no session, or session is stale (>30s without heartbeat).
    /// </summary>
    public static SessionState? LoadActiveSession()
    {
        try
        {
            if (!File.Exists(_sessionFile))
            {
                LogToFile("LoadActiveSession: No session file found");
                return null;
            }

            var json = File.ReadAllText(_sessionFile);
            var session = JsonSerializer.Deserialize<SessionState>(json, _jsonOptions);

            if (session == null)
            {
                LogToFile("LoadActiveSession: Failed to deserialize session JSON");
                return null;
            }

            // Check if session is stale
            if (session.IsStale())
            {
                var staleAge = (DateTime.UtcNow - session.LastHeartbeatUtc).TotalSeconds;
                LogToFile($"⚠ LoadActiveSession: Session is stale ({staleAge:F0}s old, threshold 30s) - discarding");
                ClearSession();
                return null;
            }

            var elapsed = session.ElapsedTime.TotalMinutes;
            LogToFile($"✓ LoadActiveSession: Loaded placeId={session.PlaceId}, elapsed={elapsed:F1}min");
            return session;
        }
        catch (Exception ex)
        {
            LogToFile($"✗ LoadActiveSession ERROR: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Updates the heartbeat of the current session, proving it's still active.
    /// Should be called frequently (every ~100ms) while monitoring active game.
    /// </summary>
    public static void UpdateHeartbeat()
    {
        try
        {
            if (!File.Exists(_sessionFile))
                return;

            var json = File.ReadAllText(_sessionFile);
            var session = JsonSerializer.Deserialize<SessionState>(json, _jsonOptions);

            if (session != null)
            {
                session.UpdateHeartbeat();
                var updated = JsonSerializer.Serialize(session, _jsonOptions);
                File.WriteAllText(_sessionFile, updated);
                // Only log occasionally to avoid log spam
                if (DateTime.UtcNow.Second % 10 == 0)
                    LogToFile($"UpdateHeartbeat: placeId={session.PlaceId}");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"✗ UpdateHeartbeat ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the session state (called when game exits or is killed).
    /// </summary>
    public static void ClearSession()
    {
        try
        {
            if (File.Exists(_sessionFile))
            {
                File.Delete(_sessionFile);
                LogToFile("✓ ClearSession: Session file deleted");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"✗ ClearSession ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the file path for testing purposes.
    /// </summary>
    public static string GetSessionFilePath() => _sessionFile;
}
