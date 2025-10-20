namespace RobloxGuard.Core;

/// <summary>
/// Handles first-run installation and setup.
/// </summary>
public static class InstallerHelper
{
    /// <summary>
    /// Performs first-run setup: registers protocol, creates scheduled task, etc.
    /// </summary>
    public static void PerformFirstRunSetup(string appExePath)
    {
        try
        {
            // Step 1: Backup and register protocol handler
            RegistryHelper.BackupCurrentProtocolHandler();
            RegistryHelper.InstallProtocolHandler(appExePath);

            // Step 2: Create scheduled task for watcher
            TaskSchedulerHelper.CreateWatcherTask(appExePath);

            // Step 3: Create default configuration if it doesn't exist
            var config = ConfigManager.Load();
            if (string.IsNullOrEmpty(config.ParentPINHash))
            {
                // No PIN set yet - will prompt user via UI
                ConfigManager.Save(config);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"First-run setup failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Performs uninstall cleanup.
    /// </summary>
    public static void PerformUninstall()
    {
        try
        {
            // Step 1: Delete scheduled task
            TaskSchedulerHelper.DeleteWatcherTask();

            // Step 2: Restore original protocol handler
            RegistryHelper.RestoreProtocolHandler();

            // Step 3: Optional - delete app folder
            // (Installer or user can do this)
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Uninstall failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if first-run setup has been completed.
    /// </summary>
    public static bool IsInstalled()
    {
        return RegistryHelper.IsRobloxGuardInstalled() && TaskSchedulerHelper.TaskExists();
    }
}
