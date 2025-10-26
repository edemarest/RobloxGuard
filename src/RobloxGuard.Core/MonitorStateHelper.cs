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
    /// PRIMARY: Look for OTHER RobloxGuard processes (excluding the current process).
    /// SECONDARY: Validate mutex is actively held (if process check inconclusive).
    /// </summary>
    /// <returns>True if monitor is actively running, false otherwise.</returns>
    public static bool IsMonitorRunning()
    {
        try
        {
            LogToFile("Checking if monitor is running...");
            int currentPID = Environment.ProcessId;
            LogToFile($"Current process ID: {currentPID}");
            
            // PRIMARY CHECK: Look for OTHER RobloxGuard processes (exclude ourselves)
            // This is the most reliable indicator - if no OTHER process exists, monitor is not running
            try
            {
                var processes = Process.GetProcessesByName("RobloxGuard");
                var otherProcesses = processes.Where(p => p.Id != currentPID).ToList();
                
                LogToFile($"Process check: Found {processes.Length} RobloxGuard process(es), {otherProcesses.Count} OTHER than current");
                
                if (otherProcesses.Count > 0)
                {
                    LogToFile($"✓ Monitor is running (found {otherProcesses.Count} other RobloxGuard process(es))");
                    return true;
                }
                else
                {
                    LogToFile("No OTHER RobloxGuard processes found - monitor not running");
                    return false;
                }
            }
            catch (Exception procEx)
            {
                LogToFile($"ERROR checking processes: {procEx.GetType().Name}: {procEx.Message}");
                
                // FALLBACK: Check mutex as secondary validation
                // If we can't check processes, use mutex as backup
                LogToFile("Falling back to mutex-based check...");
                try
                {
                    Mutex? mutex = null;
                    try
                    {
                        mutex = Mutex.OpenExisting(MutexName);
                    }
                    catch (WaitHandleCannotBeOpenedException)
                    {
                        LogToFile("Mutex does not exist - monitor not running");
                        return false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        LogToFile("Mutex access denied - treating as unknown state, allowing new monitor");
                        return false;
                    }

                    using (mutex)
                    {
                        if (mutex.WaitOne(100))
                        {
                            LogToFile("Mutex acquired - stale");
                            try { mutex.ReleaseMutex(); } catch { }
                            return false;
                        }
                        else
                        {
                            LogToFile("Mutex held - assuming monitor running");
                            return true;
                        }
                    }
                }
                catch (Exception mutexEx)
                {
                    LogToFile($"Mutex fallback also failed: {mutexEx.GetType().Name} - allowing new monitor");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"CRITICAL ERROR in IsMonitorRunning: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MonitorStateHelper] Critical error: {ex.Message}");
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

    /// <summary>
    /// Force cleanup: Release mutex and abandon all references.
    /// Used during uninstall to ensure clean state.
    /// </summary>
    public static void ForceCleanup()
    {
        try
        {
            LogToFile("ForceCleanup: Attempting to release mutex...");
            
            // Try to open and release the mutex
            try
            {
                var mutex = Mutex.OpenExisting(MutexName);
                try
                {
                    // Try to acquire and release to flush it
                    if (mutex.WaitOne(100))
                    {
                        mutex.ReleaseMutex();
                        LogToFile("ForceCleanup: Mutex released");
                    }
                }
                catch { }
                finally
                {
                    mutex?.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogToFile($"ForceCleanup: Mutex operation failed: {ex.GetType().Name}");
            }

            LogToFile("ForceCleanup: Complete");
        }
        catch (Exception ex)
        {
            LogToFile($"ForceCleanup: Error: {ex.Message}");
        }
    }
}
