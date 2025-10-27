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
            // No backup found, delete our handler completely
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(ProtocolKey, false);
                Registry.CurrentUser.DeleteSubKey(BackupKey, false);
            }
            catch
            {
                // Ignore errors - may not exist
            }
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
    /// <summary>
    /// Checks if RobloxGuard protocol handler is currently registered.
    /// Returns true if HKCU\..\roblox-player\shell\open\command exists and points to RobloxGuard.exe
    /// </summary>
    public static bool IsProtocolHandlerRegistered()
    {
        try
        {
            var handler = GetCurrentProtocolHandler();
            return !string.IsNullOrEmpty(handler) && handler.Contains("RobloxGuard.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if bootstrap entry exists in HKCU\...\Run
    /// </summary>
    public static bool IsBootstrapEntryRegistered()
    {
        try
        {
            const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string runKeyValue = "RobloxGuard";

            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
            var value = key?.GetValue(runKeyValue);
            return value != null && !string.IsNullOrEmpty(value.ToString());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if RobloxGuard is currently installed.
    /// Now checks for config file existence instead of protocol handler,
    /// since protocol handler registration is now optional.
    /// </summary>
    public static bool IsRobloxGuardInstalled()
    {
        try
        {
            // Check if config file exists in AppData
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RobloxGuard",
                "config.json"
            );
            return File.Exists(configPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a registry entry in HKCU\Software\Microsoft\Windows\CurrentVersion\Run
    /// to auto-start RobloxGuard on system boot.
    /// Used as a fallback when scheduled task creation fails.
    /// </summary>
    public static void SetBootstrapEntry(string robloxGuardExePath)
    {
        try
        {
            const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string runKeyValue = "RobloxGuard";

            using var key = Registry.CurrentUser.CreateSubKey(runKeyPath);
            key.SetValue(runKeyValue, robloxGuardExePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create registry bootstrap entry", ex);
        }
    }

    /// <summary>
    /// Removes the registry bootstrap entry (cleanup during uninstall).
    /// </summary>
    public static void RemoveBootstrapEntry()
    {
        try
        {
            const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string runKeyValue = "RobloxGuard";

            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
            if (key != null)
            {
                key.DeleteValue(runKeyValue, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Ignore errors - value may not exist
        }
    }
}
