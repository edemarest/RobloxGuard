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

            messages.Add("✓ RobloxGuard is ready!");
            messages.Add("");
            messages.Add("ℹ The log monitor will run automatically at startup.");
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
