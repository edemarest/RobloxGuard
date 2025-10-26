using System.Diagnostics;
using System.IO;
using System.Text;

namespace RobloxGuard.Core;

/// <summary>
/// PID-based lockfile for single-instance enforcement.
/// Much more reliable than Windows global mutexes.
/// 
/// Lockfile location: %LOCALAPPDATA%\RobloxGuard\.monitor.lock
/// Contains: Single line with the process ID of the running monitor
/// 
/// Advantages over Mutex:
/// - No OS-level persistence issues
/// - Can be easily inspected and cleaned
/// - Survives process crashes better
/// - No permission issues
/// - File-based, so very portable
/// </summary>
public static class PidLockHelper
{
    private static readonly string LockFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        ".monitor.lock"
    );

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    private static void LogToFile(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [PidLockHelper] {message}\n");
        }
        catch { }
    }

    /// <summary>
    /// Check if monitor is running by verifying the lockfile and process.
    /// Returns true only if the lockfile exists AND the process ID in it is still running.
    /// </summary>
    public static bool IsMonitorRunning()
    {
        try
        {
            LogToFile("Checking if monitor is running...");

            // Lockfile doesn't exist = no monitor
            if (!File.Exists(LockFilePath))
            {
                LogToFile("✓ Lockfile does not exist - monitor not running");
                return false;
            }

            // Try to read the lockfile
            string lockContent = File.ReadAllText(LockFilePath).Trim();
            LogToFile($"Lockfile found, content: '{lockContent}'");

            if (!int.TryParse(lockContent, out int lockPid))
            {
                LogToFile($"⚠ Lockfile contains invalid PID: '{lockContent}', treating as stale");
                CleanupStaleFile();
                return false;
            }

            LogToFile($"Lockfile PID: {lockPid}");

            // Check if the process with that PID still exists
            try
            {
                var process = Process.GetProcessById(lockPid);
                
                // Verify it's actually RobloxGuard, not some other process that reused the PID
                if (process.ProcessName.Equals("RobloxGuard", StringComparison.OrdinalIgnoreCase))
                {
                    LogToFile($"✓ Monitor is running (PID {lockPid} confirmed)");
                    return true;
                }
                else
                {
                    LogToFile($"⚠ Process {lockPid} exists but name is '{process.ProcessName}', not RobloxGuard - treating as stale");
                    CleanupStaleFile();
                    return false;
                }
            }
            catch (ArgumentException)
            {
                // Process doesn't exist
                LogToFile($"⚠ Process {lockPid} does not exist - lockfile is stale");
                CleanupStaleFile();
                return false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"ERROR in IsMonitorRunning: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Create the lockfile with the current process ID.
    /// Called by the monitor when it starts.
    /// </summary>
    public static void CreateLock()
    {
        try
        {
            int pid = Environment.ProcessId;
            LogToFile($"Creating lockfile for PID {pid}...");
            
            Directory.CreateDirectory(Path.GetDirectoryName(LockFilePath)!);
            File.WriteAllText(LockFilePath, pid.ToString(), Encoding.UTF8);
            
            LogToFile($"✓ Lockfile created successfully: {LockFilePath}");
        }
        catch (Exception ex)
        {
            LogToFile($"ERROR creating lockfile: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Remove the lockfile. Called by the monitor when it shuts down cleanly.
    /// </summary>
    public static void RemoveLock()
    {
        try
        {
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
                LogToFile($"✓ Lockfile removed");
            }
            else
            {
                LogToFile("Lockfile doesn't exist, nothing to remove");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"WARNING: Could not remove lockfile: {ex.Message}");
        }
    }

    /// <summary>
    /// Clean up a stale lockfile.
    /// </summary>
    private static void CleanupStaleFile()
    {
        try
        {
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
                LogToFile("✓ Stale lockfile cleaned up");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"WARNING: Could not clean stale lockfile: {ex.Message}");
        }
    }

    /// <summary>
    /// Force cleanup - delete lockfile regardless of state.
    /// Used during uninstall or emergency recovery.
    /// </summary>
    public static void ForceCleanup()
    {
        try
        {
            LogToFile("Force cleanup: attempting to remove lockfile...");
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
                LogToFile("✓ Lockfile force-deleted");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"WARNING: Force cleanup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get diagnostic information about the monitor state.
    /// </summary>
    public static string GetDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PID Lock Diagnostics ===");
        sb.AppendLine($"Lock file path: {LockFilePath}");
        sb.AppendLine($"Lock file exists: {File.Exists(LockFilePath)}");
        
        if (File.Exists(LockFilePath))
        {
            try
            {
                string content = File.ReadAllText(LockFilePath).Trim();
                sb.AppendLine($"Lock file content: {content}");
                
                if (int.TryParse(content, out int pid))
                {
                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        sb.AppendLine($"Process PID {pid} status: RUNNING ({proc.ProcessName})");
                    }
                    catch
                    {
                        sb.AppendLine($"Process PID {pid} status: NOT RUNNING (stale lockfile)");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error reading lockfile: {ex.Message}");
            }
        }

        sb.AppendLine($"Current process ID: {Environment.ProcessId}");
        sb.AppendLine($"RobloxGuard processes running: {Process.GetProcessesByName("RobloxGuard").Length}");

        return sb.ToString();
    }
}
