using System.Diagnostics;

namespace RobloxGuard.Core;

/// <summary>
/// Helper for creating/managing Windows scheduled tasks.
/// </summary>
public static class TaskSchedulerHelper
{
    private const string TaskName = "RobloxGuard Watcher";
    private const string TaskDescription = "Monitors for blocked Roblox games and prevents launches";

    /// <summary>
    /// Creates a scheduled task to run RobloxGuard watcher at logon.
    /// </summary>
    public static void CreateWatcherTask(string robloxGuardExePath)
    {
        try
        {
            // Build the schtasks command to create the task
            // /create — create new task
            // /tn — task name
            // /tr — task to run
            // /sc — schedule (ONLOGON)
            // /ru — run as (INTERACTIVE = current user)
            // /f — force (overwrite if exists)

            var taskAction = $"\"{robloxGuardExePath}\" --watch";
            
            var args = new[]
            {
                "/create",
                $"/tn \"{TaskName}\"",
                $"/tr \"{taskAction}\"",
                "/sc ONLOGON",
                "/ru INTERACTIVE",
                "/f"
            };

            var command = $"schtasks {string.Join(" ", args)}";
            
            ExecuteCommand(command);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create scheduled task: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes the RobloxGuard watcher scheduled task.
    /// </summary>
    public static void DeleteWatcherTask()
    {
        try
        {
            var args = new[]
            {
                "/delete",
                $"/tn \"{TaskName}\"",
                "/f"
            };

            var command = $"schtasks {string.Join(" ", args)}";
            ExecuteCommand(command);
        }
        catch
        {
            // Task may not exist, ignore
        }
    }

    /// <summary>
    /// Checks if the watcher task exists.
    /// </summary>
    public static bool TaskExists()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn \"{TaskName}\" /fo csv",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the watcher task immediately.
    /// </summary>
    public static void StartWatcherTaskNow()
    {
        try
        {
            var command = $"schtasks /run /tn \"{TaskName}\" /f";
            ExecuteCommand(command);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start watcher task: {ex.Message}", ex);
        }
    }

    private static void ExecuteCommand(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start process");

        process.WaitForExit(10000);

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Command failed: {error}");
        }
    }
}
