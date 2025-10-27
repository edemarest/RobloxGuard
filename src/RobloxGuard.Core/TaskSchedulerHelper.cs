using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RobloxGuard.Core;

/// <summary>
/// Helper for managing Windows Task Scheduler tasks for RobloxGuard.
/// Provides per-user scheduled task creation/deletion without requiring admin privileges.
/// 
/// Uses native Windows schtasks.exe command (built-in, no COM dependencies).
/// This approach is compatible with .NET Core and doesn't require COM interop.
/// All operations are logged to launcher.log for debugging.
/// </summary>
public static class TaskSchedulerHelper
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    private const string LOGON_TASK_NAME = "RobloxGuardLogonTask";
    private const string WATCHDOG_TASK_NAME = "RobloxGuardWatchdog";

    /// <summary>
    /// Logs diagnostic information to file for debugging.
    /// </summary>
    private static void LogToFile(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}Z] [TaskSchedulerHelper] {message}\n");
        }
        catch { }
    }

    /// <summary>
    /// Executes schtasks.exe command and captures output/errors.
    /// </summary>
    private static (bool success, string output, string errorMsg) ExecuteSchTasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "", "Failed to start schtasks.exe process");
            }

            string output = process.StandardOutput.ReadToEnd();
            string errorMsg = process.StandardError.ReadToEnd();
            process.WaitForExit();

            bool success = process.ExitCode == 0;
            return (success, output, errorMsg);
        }
        catch (Exception ex)
        {
            return (false, "", $"Exception executing schtasks.exe: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a scheduled task that runs RobloxGuard.exe at user logon.
    /// This ensures the monitor auto-starts after system boot.
    /// 
    /// Task Details:
    ///   - Name: RobloxGuardLogonTask
    ///   - Trigger: User logon
    ///   - Action: Run RobloxGuard.exe with no arguments
    ///   - Delay: 30 seconds after logon
    ///   - Run Level: User (no admin elevation required)
    /// </summary>
    /// <param name="exePath">Full path to RobloxGuard.exe</param>
    /// <returns>Tuple of (success, errorMessage or null if successful)</returns>
    public static (bool success, string? error) CreateLogonTask(string exePath)
    {
        try
        {
            LogToFile($"[CreateLogonTask] Starting... exePath={exePath}");

            // Validate executable exists
            if (!File.Exists(exePath))
            {
                var logonError = $"Executable not found: {exePath}";
                LogToFile($"[CreateLogonTask] ERROR: {logonError}");
                return (false, logonError);
            }

            LogToFile("[CreateLogonTask] ✓ Executable verified");

            // Build schtasks command for logon trigger
            var sb = new StringBuilder();
            sb.Append($"/create /tn \"{LOGON_TASK_NAME}\" ");
            sb.Append($"/tr \"{exePath}\" ");
            sb.Append($"/sc onlogon ");
            sb.Append($"/rl limited ");  // User level (no admin required)
            sb.Append($"/f ");            // Force creation (overwrite if exists)
            sb.Append($"/delay 0000:30 "); // Wait 30 seconds after logon (format: mmmm:ss)
            
            string arguments = sb.ToString();
            LogToFile($"[CreateLogonTask] Executing: schtasks.exe {arguments}");

            var (success, output, errorMsg) = ExecuteSchTasks(arguments);
            
            if (success)
            {
                LogToFile($"[CreateLogonTask] ✓ Task created successfully");
                if (!string.IsNullOrEmpty(output))
                    LogToFile($"[CreateLogonTask] Output: {output.Trim()}");
                return (true, null);
            }
            else
            {
                var fullError = $"schtasks.exe failed with error: {errorMsg}";
                LogToFile($"[CreateLogonTask] ERROR: {fullError}");
                if (!string.IsNullOrEmpty(output))
                    LogToFile($"[CreateLogonTask] Output: {output.Trim()}");
                return (false, fullError);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to create logon task: {ex.GetType().Name}: {ex.Message}";
            LogToFile($"[CreateLogonTask] EXCEPTION: {errorMsg}");
            LogToFile($"[CreateLogonTask] StackTrace: {ex.StackTrace}");
            return (false, errorMsg);
        }
    }

    /// <summary>
    /// Creates a scheduled task that runs periodically to check and restart the monitor if needed.
    /// This provides fast recovery (runs every minute) if the monitor crashes.
    /// 
    /// With 1-minute intervals, if monitor is killed, it will be restarted within ~60 seconds.
    /// 
    /// Task Details:
    ///   - Name: RobloxGuardWatchdog
    ///   - Trigger: Every 1 minute (recurring - fast recovery)
    ///   - Action: Run RobloxGuard.exe --check-monitor
    ///   - Run Level: User (no admin elevation required)
    /// </summary>
    /// <param name="exePath">Full path to RobloxGuard.exe</param>
    /// <param name="intervalMinutes">Check interval in minutes (default: 1 for fast recovery, can be changed to 5+ for production)</param>
    /// <returns>Tuple of (success, errorMessage or null if successful)</returns>
    public static (bool success, string? error) CreateWatchdogTask(string exePath, int intervalMinutes = 1)
    {
        try
        {
            LogToFile($"[CreateWatchdogTask] Starting... exePath={exePath}, interval={intervalMinutes}min");

            // Validate parameters
            if (!File.Exists(exePath))
            {
                var watchdogError = $"Executable not found: {exePath}";
                LogToFile($"[CreateWatchdogTask] ERROR: {watchdogError}");
                return (false, watchdogError);
            }

            if (intervalMinutes < 1 || intervalMinutes > 1440)
            {
                var intervalError = $"Invalid interval: {intervalMinutes} (must be 1-1440 minutes)";
                LogToFile($"[CreateWatchdogTask] ERROR: {intervalError}");
                return (false, intervalError);
            }

            LogToFile("[CreateWatchdogTask] ✓ Validation passed");

            // Build schtasks command for recurring timer
            // Using minute schedule which works for non-admin users
            var sb = new StringBuilder();
            sb.Append($"/create /tn \"{WATCHDOG_TASK_NAME}\" ");
            sb.Append($"/tr \"{exePath} --check-monitor\" ");
            sb.Append($"/sc minute ");          // Schedule: every N minutes (works for non-admin)
            sb.Append($"/mo {intervalMinutes} "); // Frequency
            sb.Append($"/rl limited ");         // User level (no admin required)
            sb.Append($"/f ");                  // Force creation
            
            string arguments = sb.ToString();
            LogToFile($"[CreateWatchdogTask] Creating recurring task: schtasks.exe {arguments}");

            var (success, output, executionError) = ExecuteSchTasks(arguments);
            
            if (success)
            {
                LogToFile($"[CreateWatchdogTask] ✓ Task created successfully (interval: {intervalMinutes}min)");
                if (!string.IsNullOrEmpty(output))
                    LogToFile($"[CreateWatchdogTask] Output: {output.Trim()}");
                return (true, null);
            }
            else
            {
                var fullError = $"schtasks.exe failed with error: {executionError}";
                LogToFile($"[CreateWatchdogTask] ERROR: {fullError}");
                if (!string.IsNullOrEmpty(output))
                    LogToFile($"[CreateWatchdogTask] Output: {output.Trim()}");
                return (false, fullError);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to create watchdog task: {ex.GetType().Name}: {ex.Message}";
            LogToFile($"[CreateWatchdogTask] EXCEPTION: {errorMsg}");
            LogToFile($"[CreateWatchdogTask] StackTrace: {ex.StackTrace}");
            return (false, errorMsg);
        }
    }

    /// <summary>
    /// Deletes the RobloxGuard scheduled tasks.
    /// Called during uninstall to clean up system state.
    /// Tasks deleted: RobloxGuardLogonTask, RobloxGuardWatchdog
    /// </summary>
    /// <returns>Tuple of (success, errorMessage or null if successful)</returns>
    public static (bool success, string? error) DeleteScheduledTasks()
    {
        try
        {
            LogToFile("[DeleteScheduledTasks] Starting...");

            // Delete logon task
            LogToFile($"[DeleteScheduledTasks] Attempting to delete '{LOGON_TASK_NAME}'...");
            var (success1, output1, errorMsg1) = ExecuteSchTasks($"/delete /tn \"{LOGON_TASK_NAME}\" /f");
            
            if (success1)
            {
                LogToFile($"[DeleteScheduledTasks] ✓ Deleted '{LOGON_TASK_NAME}'");
            }
            else
            {
                LogToFile($"[DeleteScheduledTasks] ⚠ Could not delete logon task: {errorMsg1}");
            }

            // Delete watchdog task
            LogToFile($"[DeleteScheduledTasks] Attempting to delete '{WATCHDOG_TASK_NAME}'...");
            var (success2, output2, errorMsg2) = ExecuteSchTasks($"/delete /tn \"{WATCHDOG_TASK_NAME}\" /f");
            
            if (success2)
            {
                LogToFile($"[DeleteScheduledTasks] ✓ Deleted '{WATCHDOG_TASK_NAME}'");
            }
            else
            {
                LogToFile($"[DeleteScheduledTasks] ⚠ Could not delete watchdog task: {errorMsg2}");
            }

            // Consider it a success if we deleted at least one task, or if both don't exist
            bool overallSuccess = success1 || success2;
            
            if (overallSuccess)
            {
                LogToFile("[DeleteScheduledTasks] ✓ Completed successfully");
                return (true, null);
            }
            else
            {
                // Both tasks may not exist (uninstall of uninstalled app), so still return success
                LogToFile("[DeleteScheduledTasks] ✓ Completed (tasks may not have existed)");
                return (true, null);
            }
        }
        catch (Exception ex)
        {
            var deleteError = $"Failed to delete scheduled tasks: {ex.GetType().Name}: {ex.Message}";
            LogToFile($"[DeleteScheduledTasks] EXCEPTION: {deleteError}");
            LogToFile($"[DeleteScheduledTasks] StackTrace: {ex.StackTrace}");
            // Return success anyway - if tasks don't exist, deletion is still the desired state
            return (true, null);
        }
    }

    /// <summary>
    /// Checks if a scheduled task exists.
    /// </summary>
    /// <param name="taskName">Name of the task to check</param>
    /// <returns>True if task exists, false otherwise</returns>
    public static bool DoesTaskExist(string taskName)
    {
        try
        {
            var (success, output, errorMsg) = ExecuteSchTasks($"/query /tn \"{taskName}\"");
            return success && !string.IsNullOrEmpty(output);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the logon task exists.
    /// </summary>
    public static bool DoesLogonTaskExist()
    {
        return DoesTaskExist(LOGON_TASK_NAME);
    }

    /// <summary>
    /// Checks if the watchdog task exists.
    /// </summary>
    public static bool DoesWatchdogTaskExist()
    {
        return DoesTaskExist(WATCHDOG_TASK_NAME);
    }

    /// <summary>
    /// Gets diagnostic information about the scheduled tasks.
    /// Useful for troubleshooting and debugging.
    /// </summary>
    /// <returns>Human-readable status report showing task status</returns>
    public static string GetDiagnostics()
    {
        var report = new StringBuilder();
        report.AppendLine("[TaskScheduler Diagnostics]");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
        report.AppendLine();

        try
        {
            // Query logon task
            report.AppendLine("Checking logon task...");
            var (success1, output1, errorMsg1) = ExecuteSchTasks($"/query /tn \"{LOGON_TASK_NAME}\" /fo list /v");
            if (success1 && !string.IsNullOrEmpty(output1))
            {
                report.AppendLine($"  ✓ Task exists");
                var lines = output1.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("Status") || trimmed.Contains("Enabled") || trimmed.Contains("Next Run") || trimmed.Contains("Task To Run"))
                        report.AppendLine($"    {trimmed}");
                }
            }
            else
            {
                report.AppendLine($"  ✗ Task not found or error: {errorMsg1}");
            }

            report.AppendLine();

            // Query watchdog task
            report.AppendLine("Checking watchdog task...");
            var (success2, output2, errorMsg2) = ExecuteSchTasks($"/query /tn \"{WATCHDOG_TASK_NAME}\" /fo list /v");
            if (success2 && !string.IsNullOrEmpty(output2))
            {
                report.AppendLine($"  ✓ Task exists");
                var lines = output2.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("Status") || trimmed.Contains("Enabled") || trimmed.Contains("Next Run") || trimmed.Contains("Task To Run"))
                        report.AppendLine($"    {trimmed}");
                }
            }
            else
            {
                report.AppendLine($"  ✗ Task not found or error: {errorMsg2}");
            }
        }
        catch (Exception ex)
        {
            report.AppendLine($"✗ Error querying tasks: {ex.Message}");
        }

        return report.ToString();
    }
}
