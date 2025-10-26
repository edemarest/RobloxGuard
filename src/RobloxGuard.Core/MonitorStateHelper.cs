using System.Diagnostics;
using System.IO;
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

    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    private static void LogToFile(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [MonitorStateHelper] {message}\n");
        }
        catch { }
    }

    /// <summary>
    /// Check if LogMonitor is currently running in the background.
    /// Uses a timeout-based mutex acquisition attempt to verify monitor is actually running.
    /// </summary>
    /// <returns>True if monitor is actively running, false otherwise.</returns>
    public static bool IsMonitorRunning()
    {
        try
        {
            LogToFile("Checking if monitor is running...");
            
            // Try to open the existing mutex
            Mutex? mutex = null;
            try
            {
                mutex = Mutex.OpenExisting(MutexName);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Mutex doesn't exist - monitor definitely not running
                LogToFile("Mutex does not exist - monitor not running");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                // Mutex exists but no access (permission issue or different user)
                // In this case, check if we can find RobloxGuard processes
                // If we find any, trust them. If not, allow new monitor.
                LogToFile($"Mutex access denied ({ex.Message}) - checking process list as fallback");
                
                try
                {
                    var processes = Process.GetProcessesByName("RobloxGuard");
                    if (processes.Length > 0)
                    {
                        LogToFile($"Found {processes.Length} RobloxGuard process(es) despite mutex access issue - treating as running");
                        return true;
                    }
                    else
                    {
                        LogToFile("No RobloxGuard processes found despite mutex access issue - allowing new monitor");
                        return false;
                    }
                }
                catch (Exception procEx)
                {
                    LogToFile($"Could not check processes: {procEx.Message} - allowing new monitor to be safe");
                    return false;
                }
            }

            using (mutex)
            {
                // Mutex exists - try to acquire it with a very short timeout
                // If we can acquire it, nobody is holding it and it's stale
                // If we can't, the monitor is actively holding it
                LogToFile("Mutex exists - testing if actively held...");
                
                if (mutex.WaitOne(100)) // 100ms timeout
                {
                    // We acquired the mutex! This means no process is holding it
                    // This is a stale mutex from a crashed process
                    LogToFile("Mutex acquired in 100ms - stale mutex detected (no process holding it)");
                    
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch { }
                    
                    return false;
                }
                else
                {
                    // Mutex is actively held by another process
                    LogToFile("Mutex could not be acquired (actively held) - monitor is running");
                    
                    // Double-check with process list anyway
                    try
                    {
                        var processes = Process.GetProcessesByName("RobloxGuard");
                        LogToFile($"Confirmed: Found {processes.Length} RobloxGuard process(es)");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Could not check processes: {ex.Message} - assuming monitor is running");
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Unexpected error
            LogToFile($"ERROR in IsMonitorRunning: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MonitorStateHelper] Error: {ex.Message}");
            
            // Safer fallback: assume NOT running to allow restart
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
