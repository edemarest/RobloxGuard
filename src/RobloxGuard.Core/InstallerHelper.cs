namespace RobloxGuard.Core;

/// <summary>
/// Handles first-run installation and setup.
/// </summary>
public static class InstallerHelper
{
    /// <summary>
    /// Performs first-run setup: registers protocol handler and initializes configuration.
    /// Returns a list of steps that succeeded/failed.
    /// </summary>
    public static (bool success, List<string> messages) PerformFirstRunSetup(string appExePath)
    {
        var messages = new List<string>();
        
        try
        {
            // Step 1: Backup and register protocol handler (CRITICAL - must succeed)
            try
            {
                RegistryHelper.BackupCurrentProtocolHandler();
                RegistryHelper.InstallProtocolHandler(appExePath);
                messages.Add("✓ Protocol handler registered successfully");
            }
            catch (Exception ex)
            {
                messages.Add($"✗ Protocol handler registration failed: {ex.Message}");
                return (false, messages);  // This is critical, fail the setup
            }

            // Step 2: Create default configuration if it doesn't exist
            try
            {
                var config = ConfigManager.Load();
                if (string.IsNullOrEmpty(config.ParentPINHash))
                {
                    // No PIN set yet - will prompt user via UI
                    ConfigManager.Save(config);
                }
                messages.Add("✓ Configuration initialized");
            }
            catch (Exception ex)
            {
                messages.Add($"⚠ Configuration setup warning: {ex.Message}");
            }

            return (true, messages);
        }
        catch (Exception ex)
        {
            messages.Add($"✗ Unexpected error during setup: {ex.Message}");
            return (false, messages);
        }
    }

    /// <summary>
    /// Performs uninstall cleanup: restores original protocol handler.
    /// </summary>
    public static void PerformUninstall()
    {
        try
        {
            // Step 1: Restore original protocol handler
            RegistryHelper.RestoreProtocolHandler();

            // Step 2: Optional - delete app folder
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
