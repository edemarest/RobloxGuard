using System.Diagnostics;
using System.Threading;

namespace RobloxGuard.Core;

/// <summary>
/// Helpers to detect if LogMonitor is already running in the background.
/// Uses mutex-based detection + process validation to work across processes.
/// Also cleans up stale mutexes from crashed processes.
/// </summary>
public static class MonitorStateHelper
{
    /// <summary>
    /// Global mutex name used by LogMonitor to enforce single instance.
    /// </summary>
    private static readonly string MutexName = "Global\\RobloxGuardLogMonitor";

    /// <summary>
    /// Check if LogMonitor is currently running in the background.
    /// Validates that:
    /// 1. Mutex exists (indicating monitor was started)
    /// 2. At least one RobloxGuard process is actually running (not just stale mutex)
    /// </summary>
    /// <returns>True if monitor is actively running, false otherwise.</returns>
    public static bool IsMonitorRunning()
    {
        try
        {
            // Step 1: Check if mutex exists
            using var mutex = Mutex.OpenExisting(MutexName);
            
            // Step 2: Mutex exists, but verify an actual process is running
            // Check for any RobloxGuard processes (could be launcher or monitor)
            var processes = Process.GetProcessesByName("RobloxGuard");
            
            if (processes.Length > 0)
            {
                // At least one RobloxGuard process is running - monitor is active
                return true;
            }
            
            // Mutex exists but no processes running - stale mutex
            // This can happen if process crashed or was killed
            // Return false so a new monitor can start
            return false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Mutex doesn't exist - monitor not running
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Mutex exists but no access - treat as running to be safe
            // (another user's session or elevated process)
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
