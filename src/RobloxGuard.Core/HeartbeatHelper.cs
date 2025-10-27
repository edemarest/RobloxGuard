using System;
using System.IO;

namespace RobloxGuard.Core;

/// <summary>
/// Manages a heartbeat file to indicate the monitor process is alive and responsive.
/// The watchdog task reads this file to detect if the monitor has hung or crashed.
/// </summary>
public static class HeartbeatHelper
{
    private static readonly string _heartbeatPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        ".monitor.heartbeat"
    );

    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    /// <summary>
    /// Updates the heartbeat file with current timestamp.
    /// Call this periodically from the monitor to signal it's alive.
    /// </summary>
    public static void UpdateHeartbeat()
    {
        try
        {
            // Write current timestamp (ticks) to heartbeat file
            // Ticks format allows efficient comparison without parsing
            var content = DateTime.UtcNow.Ticks.ToString();
            File.WriteAllText(_heartbeatPath, content);
        }
        catch
        {
            // Silently ignore heartbeat failures - not critical to operation
            // but log it if launcher.log is available
            try
            {
                var logMsg = $"[HeartbeatHelper] Failed to update heartbeat file\n";
                File.AppendAllText(_logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}Z] {logMsg}");
            }
            catch { }
        }
    }

    /// <summary>
    /// Checks if the monitor's heartbeat is fresh (alive and responsive).
    /// Returns false if heartbeat doesn't exist or is older than maxAgeSeconds.
    /// </summary>
    public static bool IsHeartbeatFresh(int maxAgeSeconds = 30)
    {
        try
        {
            if (!File.Exists(_heartbeatPath))
                return false; // No heartbeat = not running

            var content = File.ReadAllText(_heartbeatPath).Trim();
            if (string.IsNullOrEmpty(content) || !long.TryParse(content, out var ticks))
                return false; // Invalid heartbeat

            var lastUpdate = new DateTime(ticks, DateTimeKind.Utc);
            var age = DateTime.UtcNow - lastUpdate;
            
            return age.TotalSeconds < maxAgeSeconds;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the age of the heartbeat in seconds.
    /// Returns -1 if heartbeat doesn't exist or can't be read.
    /// </summary>
    public static double GetHeartbeatAgeSeconds()
    {
        try
        {
            if (!File.Exists(_heartbeatPath))
                return -1;

            var content = File.ReadAllText(_heartbeatPath).Trim();
            if (string.IsNullOrEmpty(content) || !long.TryParse(content, out var ticks))
                return -1;

            var lastUpdate = new DateTime(ticks, DateTimeKind.Utc);
            return (DateTime.UtcNow - lastUpdate).TotalSeconds;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Clears the heartbeat file (call on shutdown).
    /// </summary>
    public static void ClearHeartbeat()
    {
        try
        {
            if (File.Exists(_heartbeatPath))
                File.Delete(_heartbeatPath);
        }
        catch { }
    }
}
