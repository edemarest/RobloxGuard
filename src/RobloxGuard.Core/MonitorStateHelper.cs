using System.Diagnostics;
using System.IO;
using System.Threading;

namespace RobloxGuard.Core;

/// <summary>
/// Helpers to detect if LogMonitor is already running in the background.
/// Uses PID lockfile for reliable single-instance enforcement.
/// Lockfile-based approach is much more reliable than Windows global mutexes.
/// </summary>
public static class MonitorStateHelper
{
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
    /// Uses PID lockfile for reliable detection.
    /// </summary>
    /// <returns>True if monitor is actively running, false otherwise.</returns>
    public static bool IsMonitorRunning()
    {
        try
        {
            LogToFile("Checking if monitor is running using PID lockfile...");
            return PidLockHelper.IsMonitorRunning();
        }
        catch (Exception ex)
        {
            LogToFile($"ERROR in IsMonitorRunning: {ex.GetType().Name}: {ex.Message}");
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
    /// Force cleanup: delete lockfile and abandon all references.
    /// Used during uninstall to ensure clean state.
    /// </summary>
    public static void ForceCleanup()
    {
        try
        {
            LogToFile("ForceCleanup: Attempting to remove lockfile...");
            PidLockHelper.ForceCleanup();
            LogToFile("ForceCleanup: Complete");
        }
        catch (Exception ex)
        {
            LogToFile($"ForceCleanup: Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get diagnostic information about monitor state.
    /// </summary>
    public static string GetDiagnostics()
    {
        return PidLockHelper.GetDiagnostics();
    }
}
