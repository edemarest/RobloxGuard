namespace RobloxGuard.Core;

/// <summary>
/// Handles first-run installation and setup.
/// </summary>
public static class InstallerHelper
{
    /// <summary>
    /// Performs first-run setup: initializes configuration only.
    /// Protocol handler registration is now optional and must be done explicitly.
    /// Returns a list of steps that succeeded/failed.
    /// </summary>
    public static (bool success, List<string> messages) PerformFirstRunSetup(string appExePath)
    {
        var messages = new List<string>();
        
        try
        {
            // Step 1: Create default configuration if it doesn't exist
            try
            {
                var config = ConfigManager.Load();
                ConfigManager.Save(config);
                messages.Add("✓ Configuration initialized");
            }
            catch (Exception ex)
            {
                messages.Add($"⚠ Configuration setup warning: {ex.Message}");
            }

            // Step 2: Create scheduled tasks for auto-restart (v1.6.0+)
            // NOTE: Logon tasks are blocked for non-elevated users, so we use:
            // - TIER 1: Registry Run key for boot startup (guaranteed)
            // - TIER 2: Watchdog with 1-minute intervals (quasi-instant recovery if killed)
            try
            {
                // Create watchdog task - runs every 1 minute to detect and restart monitor if crashed
                // This ensures <60 second recovery time if monitor is killed
                var (watchdogSuccess, watchdogError) = TaskSchedulerHelper.CreateWatchdogTask(appExePath, intervalMinutes: 1);
                if (watchdogSuccess)
                {
                    messages.Add("✓ Watchdog task created (1-minute health checks)");
                }
                else
                {
                    messages.Add($"⚠ Watchdog task creation failed: {watchdogError}");
                }
            }
            catch (Exception ex)
            {
                messages.Add($"⚠ Scheduled task setup warning: {ex.Message}");
            }

            // Step 3: Set Registry Run key as guaranteed boot startup method
            try
            {
                RegistryHelper.SetBootstrapEntry(appExePath);
                messages.Add("✓ Registry startup entry created (guaranteed boot startup)");
            }
            catch (Exception ex)
            {
                messages.Add($"⚠ Registry startup entry failed: {ex.Message}");
            }

            messages.Add("✓ RobloxGuard is ready!");
            messages.Add("");
            messages.Add("ℹ The monitor will run automatically at startup.");
            messages.Add("ℹ A health-check task will verify the monitor is running every 5 minutes.");
            messages.Add("ℹ To enable protocol handler (pre-launch blocking), run:");
            messages.Add($"    {Path.GetFileName(appExePath)} --register-protocol");

            return (true, messages);
        }
        catch (Exception ex)
        {
            messages.Add($"✗ Unexpected error during setup: {ex.Message}");
            return (false, messages);
        }
    }

    /// <summary>
    /// Registers RobloxGuard as the protocol handler for roblox-player://.
    /// This enables pre-launch game blocking.
    /// </summary>
    public static (bool success, List<string> messages) RegisterProtocolHandler(string appExePath)
    {
        var messages = new List<string>();
        
        try
        {
            // Backup current handler first
            RegistryHelper.BackupCurrentProtocolHandler();
            RegistryHelper.InstallProtocolHandler(appExePath);
            messages.Add("✓ Protocol handler registered successfully");
            messages.Add("");
            messages.Add("ℹ Pre-launch game blocking is now enabled.");
            messages.Add("ℹ Games will be blocked BEFORE they launch.");
            return (true, messages);
        }
        catch (Exception ex)
        {
            messages.Add($"✗ Protocol handler registration failed: {ex.Message}");
            return (false, messages);
        }
    }

    /// <summary>
    /// Performs uninstall cleanup: deletes scheduled tasks and restores original protocol handler.
    /// </summary>
    public static void PerformUninstall()
    {
        try
        {
            // Step 1: Delete scheduled tasks (logon + watchdog)
            var (tasksDeleted, tasksError) = TaskSchedulerHelper.DeleteScheduledTasks();
            if (!tasksDeleted)
            {
                // Log but don't fail - tasks may not exist on fresh uninstall
                System.Diagnostics.Debug.WriteLine($"Note: Task deletion reported: {tasksError}");
            }

            // Step 2: Remove registry bootstrap entry (fallback startup)
            try
            {
                RegistryHelper.RemoveBootstrapEntry();
            }
            catch
            {
                // Ignore errors - entry may not exist
            }

            // Step 3: Restore original protocol handler
            RegistryHelper.RestoreProtocolHandler();

            // Step 4: Optional - delete app folder
            // (Installer or user can do this)
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Uninstall failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if RobloxGuard has been installed.
    /// </summary>
    public static bool IsInstalled()
    {
        return RegistryHelper.IsRobloxGuardInstalled();
    }
}
