using System.Runtime.Versioning;

namespace RobloxGuard.Core;

/// <summary>
/// Manages the 3-day enforcement cycle with persistent state.
/// 
/// Cycle: Day 1 (enforced) → Day 2 (enforced) → Day 3 (skip) → Day 1 (enforced again)
/// 
/// Features:
/// - Persistent day counter across app restarts
/// - Automatic day increment at midnight (local time)
/// - Reset to Day 1 when enforcement is triggered
/// - Quiet hours support (e.g., 3:30-9:00 AM skip all enforcement)
/// - Test mode with time compression for validation
/// - Comprehensive logging with ISO timestamps
/// </summary>
[SupportedOSPlatform("windows")]
public class DayCounterManager
{
    private int _currentDay;                    // 1, 2, or 3
    private DateTime _lastKillDateLocal;        // Local time of last enforcement
    private string _lastKillReason = "";        // Reason for last enforcement
    private Func<dynamic>? _getConfig;          // Lazy config loader
    private Action<string>? _logToFile;         // Logging callback
    private readonly object _lock = new();      // Thread safety

    private const string LOG_PREFIX = "[DayCounterManager]";
    private const string ERROR_PREFIX = "[DayCounterManager.ERROR]";

    /// <summary>
    /// Initializes a new DayCounterManager.
    /// </summary>
    /// <param name="dayCounter">Starting day (1, 2, or 3)</param>
    /// <param name="lastKillDate">ISO date of last enforcement (yyyy-MM-dd)</param>
    /// <param name="lastKillReason">Reason for last enforcement</param>
    /// <param name="getConfig">Callback to get current config</param>
    /// <param name="logToFile">Callback for logging</param>
    public DayCounterManager(
        int dayCounter = 1,
        string lastKillDate = "",
        string lastKillReason = "",
        Func<dynamic>? getConfig = null,
        Action<string>? logToFile = null)
    {
        _currentDay = Math.Clamp(dayCounter, 1, 3);
        _lastKillReason = lastKillReason ?? "";
        _getConfig = getConfig;
        _logToFile = logToFile;

        // Parse last kill date
        if (!string.IsNullOrEmpty(lastKillDate) && DateTime.TryParse(lastKillDate, out var parsed))
        {
            _lastKillDateLocal = parsed;
        }
        else
        {
            _lastKillDateLocal = DateTime.Now.AddDays(-1);
        }
    }

    /// <summary>
    /// Checks if today is a "skip day" (day 3 of the 3-day cycle).
    /// Also automatically increments day counter if midnight boundary crossed.
    /// </summary>
    /// <returns>True if day 3 (enforcement disabled), false if day 1-2 (enforcement enabled)</returns>
    public bool IsSkipDay()
    {
        lock (_lock)
        {
            var nowLocal = GetLocalNow();

            // Check if date changed since last kill
            if (_lastKillDateLocal.Date < nowLocal.Date)
            {
                _currentDay = _currentDay < 3 ? _currentDay + 1 : 1;
                LogToFile($"{LOG_PREFIX}.IsSkipDay() Midnight boundary crossed: " +
                    $"lastKillDate={_lastKillDateLocal:yyyy-MM-dd}, " +
                    $"today={nowLocal:yyyy-MM-dd}, " +
                    $"newDay={_currentDay}");
            }

            bool skip = _currentDay == 3;
            LogToFile($"{LOG_PREFIX}.IsSkipDay() currentDay={_currentDay}, " +
                $"isSkip={skip}, time={nowLocal:HH:mm:ss}");

            return skip;
        }
    }

    /// <summary>
    /// Records that enforcement was triggered. Resets day counter to 1.
    /// Should be called when either Playtime or After-Hours enforcement fires.
    /// </summary>
    /// <param name="reason">Reason for enforcement (e.g., "PlaytimeLimit", "AfterHours")</param>
    public void RecordEnforcementAction(string reason)
    {
        lock (_lock)
        {
            var nowLocal = GetLocalNow();

            _lastKillDateLocal = nowLocal;
            _lastKillReason = reason ?? "";
            _currentDay = 1;  // Reset to day 1 after enforcement

            LogToFile($"{LOG_PREFIX}.RecordEnforcementAction() " +
                $"Recorded at {nowLocal:yyyy-MM-dd HH:mm:ss}, " +
                $"reason={reason}, " +
                $"nextDay=1");

            SaveStateToConfig();
        }
    }

    /// <summary>
    /// Gets the randomized after-hours start time within configured window.
    /// Returns a random minute value within the 30-minute window.
    /// </summary>
    /// <returns>TimeSpan representing the randomized start time (e.g., 3:15 AM)</returns>
    public TimeSpan GetRandomizedAfterHoursStartTime()
    {
        var config = _getConfig?.Invoke();
        if (config == null)
        {
            LogToFile($"{ERROR_PREFIX}.GetRandomizedAfterHoursStartTime() Config is null");
            return new TimeSpan(3, 0, 0);  // Default fallback: 3:00 AM
        }

        int windowMinutes = config?.AfterHoursRandomWindowMinutes ?? 30;
        windowMinutes = Math.Clamp(windowMinutes, 1, 60);

        // Random minute within window (0 to windowMinutes-1)
        int randomMinute = Random.Shared.Next(0, windowMinutes);

        LogToFile($"{LOG_PREFIX}.GetRandomizedAfterHoursStartTime() " +
            $"window={windowMinutes}min, randomMinute={randomMinute}min, " +
            $"result=03:{randomMinute:D2}:00");

        return new TimeSpan(3, randomMinute, 0);  // 3:xx AM
    }

    /// <summary>
    /// Checks if current time is within "quiet hours" (skip all enforcement).
    /// Useful for avoiding enforcement during morning routine (e.g., 3:30-9:00 AM).
    /// </summary>
    /// <returns>True if in quiet hours (enforcement disabled), false otherwise</returns>
    public bool IsInQuietHours()
    {
        lock (_lock)
        {
            var config = _getConfig?.Invoke();
            if (config == null || !(config?.QuietHoursEnabled ?? true))
                return false;

            var nowLocal = GetLocalNow();
            int currentTimeAsInt = nowLocal.Hour * 100 + nowLocal.Minute;  // HHmm format

            int quietStart = config?.QuietHoursStart ?? 330;    // 3:30 AM
            int quietEnd = config?.QuietHoursEnd ?? 900;        // 9:00 AM

            bool inQuiet = currentTimeAsInt >= quietStart && currentTimeAsInt < quietEnd;

            if (inQuiet)
            {
                LogToFile($"{LOG_PREFIX}.IsInQuietHours() YES: " +
                    $"time={currentTimeAsInt:D4}, " +
                    $"window={quietStart:D4}-{quietEnd:D4}");
            }

            return inQuiet;
        }
    }

    /// <summary>
    /// Gets the current day counter value.
    /// </summary>
    /// <returns>Current day (1, 2, or 3)</returns>
    public int GetCurrentDay()
    {
        lock (_lock)
        {
            return _currentDay;
        }
    }

    /// <summary>
    /// Gets diagnostic information about the current day counter state.
    /// </summary>
    /// <returns>Formatted string with day, date, and reason</returns>
    public string GetDiagnostics()
    {
        lock (_lock)
        {
            return $"Day={_currentDay}/3, LastKillDate={_lastKillDateLocal:yyyy-MM-dd}, " +
                   $"Reason={_lastKillReason}, Now={GetLocalNow():yyyy-MM-dd HH:mm:ss}";
        }
    }

    /// <summary>
    /// Gets local time, potentially compressed for test mode.
    /// </summary>
    private DateTime GetLocalNow()
    {
        var config = _getConfig?.Invoke();
        bool testModeEnabled = config?.TestModeEnabled ?? false;
        int testCompression = config?.TestModeTimeCompressionFactor ?? 1;

        if (!testModeEnabled || testCompression <= 1)
        {
            return DateTime.Now;
        }

        // Test mode: compress time
        // Each real second represents testCompression days
        // We use TickCount for a relative baseline that advances quickly
        try
        {
            var now = DateTime.UtcNow;
            var baseTime = now.Date;  // Start of today (UTC)

            // Calculate seconds since midnight UTC
            var secondsSinceMidnight = (long)(now - baseTime).TotalSeconds;

            // In test mode, each second = testCompression days
            var simulatedDays = secondsSinceMidnight * testCompression;
            var simulatedTime = baseTime.AddSeconds(simulatedDays);

            return simulatedTime.ToLocalTime();
        }
        catch
        {
            return DateTime.Now;
        }
    }

    /// <summary>
    /// Saves current day counter state back to config.
    /// </summary>
    private void SaveStateToConfig()
    {
        try
        {
            var config = _getConfig?.Invoke();
            if (config == null)
            {
                LogToFile($"{ERROR_PREFIX}.SaveStateToConfig() Config is null");
                return;
            }

            config.ConsecutiveDayCounter = _currentDay;
            config.LastKillDate = _lastKillDateLocal.ToString("yyyy-MM-dd");
            config.LastKillDay = (int)_lastKillDateLocal.DayOfWeek;

            ConfigManager.Save(config);
            LogToFile($"{LOG_PREFIX}.SaveStateToConfig() Saved: " +
                $"day={_currentDay}, date={_lastKillDateLocal:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            LogToFile($"{ERROR_PREFIX}.SaveStateToConfig() Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs a message using the provided callback or to console if no callback.
    /// </summary>
    private void LogToFile(string message)
    {
        try
        {
            if (_logToFile != null)
            {
                _logToFile(message);
            }
            else
            {
                // Fallback: write to console
                System.Console.WriteLine(message);
            }
        }
        catch
        {
            // Silently ignore logging errors
        }
    }
}
