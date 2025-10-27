using System;
using System.IO;

namespace RobloxGuard.Core;

/// <summary>
/// Manages %LOCALAPPDATA%\RobloxGuard directory with defensive creation and validation.
/// Ensures the directory exists and is writable before any operations.
/// This handles edge case where AppData directory is deleted or inaccessible.
/// </summary>
public static class AppDataHelper
{
    /// <summary>
    /// Gets the RobloxGuard application data directory path.
    /// </summary>
    public static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard"
    );

    private static readonly string _logPath = Path.Combine(AppDataPath, "launcher.log");

    private static void LogToFile(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}Z] [AppDataHelper] {message}\n");
        }
        catch { }
    }

    /// <summary>
    /// Ensures the RobloxGuard AppData directory exists and is writable.
    /// Creates the directory if it doesn't exist.
    /// Returns true if directory is ready to use, false if there's a critical error.
    /// </summary>
    public static bool EnsureAppDataDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(AppDataPath))
            {
                LogToFile("AppData directory doesn't exist - creating...");
                Directory.CreateDirectory(AppDataPath);
                LogToFile("✓ AppData directory created successfully");
            }
            else
            {
                LogToFile("✓ AppData directory already exists");
            }

            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"✗ CRITICAL: Failed to create/access AppData directory: {ex.Message}");
            Console.WriteLine($"[AppDataHelper] ERROR: Cannot create app directory: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if AppData directory is accessible and writable.
    /// </summary>
    public static bool IsAppDataAccessible()
    {
        try
        {
            if (!Directory.Exists(AppDataPath))
                return false;

            // Try to write a test file
            var testFile = Path.Combine(AppDataPath, ".write_test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the subdirectory path within AppData (creates if needed).
    /// </summary>
    public static string GetSubdirectory(string subdirName)
    {
        try
        {
            var subdir = Path.Combine(AppDataPath, subdirName);
            if (!Directory.Exists(subdir))
            {
                Directory.CreateDirectory(subdir);
                LogToFile($"Created subdirectory: {subdirName}");
            }
            return subdir;
        }
        catch (Exception ex)
        {
            LogToFile($"Failed to create subdirectory {subdirName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets diagnostic information about AppData directory.
    /// </summary>
    public static string GetDiagnostics()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"AppData Path: {AppDataPath}");
        info.AppendLine($"Exists: {Directory.Exists(AppDataPath)}");
        
        try
        {
            if (Directory.Exists(AppDataPath))
            {
                var dirInfo = new DirectoryInfo(AppDataPath);
                info.AppendLine($"Writable: {IsAppDataAccessible()}");
                info.AppendLine($"Subdirectories: {dirInfo.GetDirectories().Length}");
                info.AppendLine($"Files: {dirInfo.GetFiles().Length}");
                
                // List main files
                foreach (var file in dirInfo.GetFiles())
                {
                    info.AppendLine($"  - {file.Name} ({file.Length} bytes)");
                }
            }
        }
        catch (Exception ex)
        {
            info.AppendLine($"Error reading directory: {ex.Message}");
        }

        return info.ToString();
    }
}
