using Microsoft.Win32;

namespace RobloxGuard.Core;

/// <summary>
/// Helper for managing Windows Registry entries for protocol handlers.
/// </summary>
public static class RegistryHelper
{
    private const string ProtocolKey = @"Software\Classes\roblox-player";
    private const string BackupKey = @"Software\RobloxGuard";
    
    /// <summary>
    /// Gets the current protocol handler command from registry.
    /// </summary>
    public static string? GetCurrentProtocolHandler()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{ProtocolKey}\shell\open\command");
            return key?.GetValue(null) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Backs up the current protocol handler before replacing it.
    /// </summary>
    public static void BackupCurrentProtocolHandler()
    {
        var currentHandler = GetCurrentProtocolHandler();
        if (string.IsNullOrEmpty(currentHandler))
            return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(BackupKey);
            key.SetValue("Upstream", currentHandler);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to backup protocol handler", ex);
        }
    }

    /// <summary>
    /// Gets the backed-up upstream protocol handler.
    /// </summary>
    public static string? GetBackedUpProtocolHandler()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(BackupKey);
            return key?.GetValue("Upstream") as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sets RobloxGuard as the protocol handler for roblox-player://.
    /// </summary>
    public static void InstallProtocolHandler(string robloxGuardExePath)
    {
        try
        {
            // Backup current handler first
            BackupCurrentProtocolHandler();

            // Create protocol handler keys
            using var protocolKey = Registry.CurrentUser.CreateSubKey(ProtocolKey);
            protocolKey.SetValue(null, "Roblox Player URL");
            protocolKey.SetValue("URL Protocol", "");

            // Set default icon (optional)
            using var iconKey = Registry.CurrentUser.CreateSubKey($@"{ProtocolKey}\DefaultIcon");
            iconKey.SetValue(null, $"\"{robloxGuardExePath}\",0");

            // Set command
            using var commandKey = Registry.CurrentUser.CreateSubKey($@"{ProtocolKey}\shell\open\command");
            commandKey.SetValue(null, $"\"{robloxGuardExePath}\" --handle-uri \"%1\"");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to install protocol handler", ex);
        }
    }

    /// <summary>
    /// Restores the original protocol handler from backup.
    /// </summary>
    public static void RestoreProtocolHandler()
    {
        var backedUpHandler = GetBackedUpProtocolHandler();
        if (string.IsNullOrEmpty(backedUpHandler))
        {
            // No backup found, remove our handler
            UninstallProtocolHandler();
            return;
        }

        try
        {
            using var commandKey = Registry.CurrentUser.CreateSubKey($@"{ProtocolKey}\shell\open\command");
            commandKey.SetValue(null, backedUpHandler);

            // Clean up backup
            Registry.CurrentUser.DeleteSubKey(BackupKey, false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to restore protocol handler", ex);
        }
    }

    /// <summary>
    /// Removes the protocol handler entirely.
    /// </summary>
    public static void UninstallProtocolHandler()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ProtocolKey, false);
            Registry.CurrentUser.DeleteSubKey(BackupKey, false);
        }
        catch
        {
            // Ignore errors - may not exist
        }
    }

    /// <summary>
    /// Checks if RobloxGuard is currently installed as the protocol handler.
    /// </summary>
    public static bool IsRobloxGuardInstalled()
    {
        var currentHandler = GetCurrentProtocolHandler();
        return currentHandler?.Contains("RobloxGuard", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
