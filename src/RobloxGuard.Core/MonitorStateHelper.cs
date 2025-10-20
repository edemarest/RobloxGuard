using System.Threading;

namespace RobloxGuard.Core;

/// <summary>
/// Helpers to detect if LogMonitor is already running in the background.
/// Uses mutex-based detection to work across processes.
/// </summary>
public static class MonitorStateHelper
{
    /// <summary>
    /// Global mutex name used by LogMonitor to enforce single instance.
    /// </summary>
    private static readonly string MutexName = "Global\\RobloxGuardLogMonitor";

    /// <summary>
    /// Check if LogMonitor is currently running in the background.
    /// </summary>
    /// <returns>True if monitor is running, false otherwise.</returns>
    public static bool IsMonitorRunning()
    {
        try
        {
            // Try to open the existing mutex without acquiring it
            // If the mutex exists, it means LogMonitor is currently running
            using var mutex = Mutex.OpenExisting(MutexName);
            
            // Mutex exists - monitor is active
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Mutex doesn't exist - monitor not running
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Mutex exists but no access - treat as running
            return true;
        }
        catch (Exception ex)
        {
            // Unexpected error - log but assume not running (safer fallback)
            System.Diagnostics.Debug.WriteLine($"[MonitorStateHelper] Error checking monitor state: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get human-readable status of monitor.
    /// </summary>
    public static string GetMonitorStatus()
    {
        return IsMonitorRunning() 
            ? "✓ RobloxGuard monitoring is running in the background" 
            : "⚠ RobloxGuard monitoring is not running";
    }
}
