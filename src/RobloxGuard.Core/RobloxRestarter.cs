using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace RobloxGuard.Core;

/// <summary>
/// Handles graceful Roblox process termination and automatic restart to home screen.
/// 
/// Strategy: Kill + Restart is more reliable than graceful disconnect (Roblox doesn't expose disconnect API).
/// 
/// Flow:
///   1. Attempt graceful close with timeout (2 seconds by default)
///   2. If timeout, force kill process
///   3. Wait for process cleanup (500ms by default)
///   4. Automatically restart Roblox to home screen
///   5. Log all steps for audit trail
/// 
/// Benefits:
///   - Better UX: User can immediately play another game
///   - Process cleanup: Graceful close window for state preservation
///   - Configurable: All timeouts and delays customizable
///   - Logged: Comprehensive audit trail for parent visibility
/// 
/// Platform: Windows only (uses Windows registry, process APIs)
/// </summary>
[SupportedOSPlatform("windows")]
public class RobloxRestarter : IDisposable
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    private readonly Func<dynamic> _getConfig;
    private readonly string? _cachedRobloxPath;
    private bool _disposed = false;

    /// <summary>
    /// Initialize restarter with config loader and optional custom Roblox path.
    /// </summary>
    public RobloxRestarter(Func<dynamic> getConfig, string? customRobloxPath = null)
    {
        _getConfig = getConfig ?? throw new ArgumentNullException(nameof(getConfig));
        _cachedRobloxPath = customRobloxPath;

        if (!string.IsNullOrEmpty(_cachedRobloxPath))
        {
            LogToFile($"[RobloxRestarter] Initialized with custom path: {_cachedRobloxPath}");
        }
        else
        {
            LogToFile("[RobloxRestarter] Initialized (will search for Roblox at runtime)");
        }
    }

    /// <summary>
    /// Kill Roblox process and restart to home screen.
    /// Strategy:
    ///   1. Graceful close (respects timeout, allows cleanup)
    ///   2. Force kill if timeout
    ///   3. Wait for system cleanup
    ///   4. Restart to home page
    /// </summary>
    public async Task KillAndRestartToHome(string reason)
    {
        try
        {
            var config = _getConfig();
            var autoRestart = config.AutoRestartOnKill ?? true;
            var gracefulTimeoutMs = (config.GracefulCloseTimeoutMs ?? 2000);
            var restartDelayMs = (config.KillRestartDelayMs ?? 500);

            LogToFile($"[RobloxRestarter.KillAndRestartToHome] ===== Kill + Restart Sequence =====");
            LogToFile($"[RobloxRestarter.KillAndRestartToHome] Reason: {reason}");
            LogToFile($"[RobloxRestarter.KillAndRestartToHome] AutoRestart: {autoRestart}");
            LogToFile($"[RobloxRestarter.KillAndRestartToHome] GracefulTimeout: {gracefulTimeoutMs}ms");
            LogToFile($"[RobloxRestarter.KillAndRestartToHome] RestartDelay: {restartDelayMs}ms");

            // Step 1: Kill process
            await KillRobloxProcess(gracefulTimeoutMs);

            // Step 2: Check if restart is disabled
            if (!autoRestart)
            {
                LogToFile("[RobloxRestarter.KillAndRestartToHome] AutoRestart disabled, stopping here");
                LogToFile("[RobloxRestarter.KillAndRestartToHome] User can manually restart Roblox");
                return;
            }

            // Step 3: Wait for system cleanup
            LogToFile($"[RobloxRestarter.KillAndRestartToHome] Waiting {restartDelayMs}ms for cleanup...");
            await Task.Delay(restartDelayMs);

            // Step 4: Restart to home
            await RestartToHome();

            LogToFile("[RobloxRestarter.KillAndRestartToHome] ===== Kill + Restart Complete =====");
        }
        catch (Exception ex)
        {
            LogToFile($"[RobloxRestarter.KillAndRestartToHome] ERROR: {ex.GetType().Name}: {ex.Message}");
            LogToFile($"[RobloxRestarter.KillAndRestartToHome] StackTrace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Soft-disconnect from current game (graceful close without restart).
    /// 
    /// Use case: After-hours enforcement or inactivity limits where we want
    /// to close the game gracefully but allow user to manually restart to a different game.
    /// 
    /// Strategy:
    ///   1. Graceful close (WM_CLOSE signal)
    ///   2. No automatic restart
    /// </summary>
    public async Task SoftDisconnectGame(string reason)
    {
        try
        {
            var config = _getConfig();
            var gracefulTimeoutMs = (config.GracefulCloseTimeoutMs ?? 5000);  // Longer timeout for soft disconnect

            LogToFile($"[RobloxRestarter.SoftDisconnectGame] ===== Soft Disconnect Sequence =====");
            LogToFile($"[RobloxRestarter.SoftDisconnectGame] Reason: {reason}");
            LogToFile($"[RobloxRestarter.SoftDisconnectGame] GracefulTimeout: {gracefulTimeoutMs}ms");

            // Perform graceful close (reuses standard KillRobloxProcess which now only does WM_CLOSE)
            await KillRobloxProcess(gracefulTimeoutMs);

            LogToFile("[RobloxRestarter.SoftDisconnectGame] ===== Soft Disconnect Complete =====");
        }
        catch (Exception ex)
        {
            LogToFile($"[RobloxRestarter.SoftDisconnectGame] ERROR: {ex.GetType().Name}: {ex.Message}");
            LogToFile($"[RobloxRestarter.SoftDisconnectGame] StackTrace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Gracefully kill Roblox process with force kill escalation and cleanup.
    /// 
    /// Note: Roblox does NOT respond to Windows messages (WM_CLOSE, WM_DESTROY, etc).
    /// Investigation shows Roblox uses custom internal event systems, not standard
    /// Windows message passing. Therefore, graceful close will always timeout and
    /// we must escalate to force kill.
    /// 
    /// Steps:
    ///   1. Send WM_CLOSE to main window (graceful close signal - will be ignored)
    ///   2. Wait up to gracefulTimeoutMs for process to exit
    ///   3. Force kill if timeout (this is the ONLY reliable method)
    ///   4. Verify force kill
    ///   5. Clean up crash handler and other artifacts
    /// </summary>
    private async Task KillRobloxProcess(int gracefulTimeoutMs)
    {
        try
        {
            var processes = Process.GetProcessesByName("RobloxPlayerBeta");
            if (processes.Length == 0)
            {
                LogToFile("[RobloxRestarter.KillRobloxProcess] No RobloxPlayerBeta processes found (already exited?)");
                return;
            }

            LogToFile($"[RobloxRestarter.KillRobloxProcess] Found {processes.Length} process(es)");

            foreach (var proc in processes)
            {
                try
                {
                    LogToFile($"[RobloxRestarter.KillRobloxProcess] PID {proc.Id}: Starting kill sequence");

                    // Step 1: Try graceful close (will likely be ignored by Roblox)
                    LogToFile($"[RobloxRestarter.KillRobloxProcess] PID {proc.Id}: Sending graceful close signal (WM_CLOSE)");
                    proc.CloseMainWindow();

                    // Step 2: Wait for graceful exit
                    bool exited = proc.WaitForExit(gracefulTimeoutMs);

                    if (exited)
                    {
                        LogToFile($"[RobloxRestarter.KillRobloxProcess] PID {proc.Id}: ✓ Graceful close successful");
                        await Task.Delay(300); // Brief delay before cleanup
                        CleanupRobloxArtifacts();
                        return;
                    }

                    // Step 3: Force kill (Roblox doesn't respond to messages, so this is mandatory)
                    LogToFile($"[RobloxRestarter.KillRobloxProcess] PID {proc.Id}: Graceful timeout ({gracefulTimeoutMs}ms), force killing");
                    LogToFile($"[RobloxRestarter.KillRobloxProcess] NOTE: Roblox does not respond to WM_CLOSE. Force kill is required.");
                    proc.Kill(true);  // Kill with child processes

                    // Step 4: Verify force kill
                    exited = proc.WaitForExit(1000);
                    if (exited)
                    {
                        LogToFile($"[RobloxRestarter.KillRobloxProcess] PID {proc.Id}: ✓ Force kill successful");
                    }
                    else
                    {
                        LogToFile($"[RobloxRestarter.KillRobloxProcess] PID {proc.Id}: ⚠ Force kill timeout - process may still be alive");
                    }

                    // Step 5: Wait a bit then clean up artifacts
                    LogToFile($"[RobloxRestarter.KillRobloxProcess] PID {proc.Id}: Waiting for system cleanup (500ms)");
                    await Task.Delay(500);
                    CleanupRobloxArtifacts();
                }
                catch (InvalidOperationException ex)
                {
                    // Process already exited
                    LogToFile($"[RobloxRestarter.KillRobloxProcess] PID (unknown): Process already exited ({ex.Message})");
                    await Task.Delay(300);
                    CleanupRobloxArtifacts();
                }
                catch (Exception ex)
                {
                    LogToFile($"[RobloxRestarter.KillRobloxProcess] PID (unknown): ERROR: {ex.GetType().Name}: {ex.Message}");
                    await Task.Delay(300);
                    CleanupRobloxArtifacts();
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[RobloxRestarter.KillRobloxProcess] CRITICAL ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Clean up Roblox-related processes and artifacts after kill.
    /// Terminates RobloxCrashHandler and other leftover processes.
    /// </summary>
    private void CleanupRobloxArtifacts()
    {
        try
        {
            LogToFile("[RobloxRestarter.CleanupRobloxArtifacts] Starting cleanup");

            // Kill RobloxCrashHandler
            var crashHandlers = Process.GetProcessesByName("RobloxCrashHandler");
            if (crashHandlers.Length > 0)
            {
                foreach (var handler in crashHandlers)
                {
                    try
                    {
                        LogToFile($"[RobloxRestarter.CleanupRobloxArtifacts] Killing RobloxCrashHandler PID {handler.Id}");
                        handler.Kill(true);
                        handler.WaitForExit(500);
                        LogToFile($"[RobloxRestarter.CleanupRobloxArtifacts] ✓ RobloxCrashHandler terminated");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[RobloxRestarter.CleanupRobloxArtifacts] Error killing RobloxCrashHandler: {ex.Message}");
                    }
                }
            }

            // Clean up any other Roblox processes (RobloxApp, etc)
            var otherProcesses = new[] { "RobloxApp", "RobloxBrowserTools" };
            foreach (var procName in otherProcesses)
            {
                try
                {
                    var procs = Process.GetProcessesByName(procName);
                    if (procs.Length > 0)
                    {
                        LogToFile($"[RobloxRestarter.CleanupRobloxArtifacts] Found {procName}: {procs.Length} process(es)");
                        foreach (var p in procs)
                        {
                            try
                            {
                                p.Kill(true);
                                p.WaitForExit(500);
                                LogToFile($"[RobloxRestarter.CleanupRobloxArtifacts] ✓ Terminated {procName} PID {p.Id}");
                            }
                            catch { /* Already dead */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"[RobloxRestarter.CleanupRobloxArtifacts] Error with {procName}: {ex.Message}");
                }
            }

            LogToFile("[RobloxRestarter.CleanupRobloxArtifacts] Cleanup complete");
        }
        catch (Exception ex)
        {
            LogToFile($"[RobloxRestarter.CleanupRobloxArtifacts] CRITICAL ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Restart Roblox to home screen.
    /// 
    /// Strategy:
    ///   1. Use custom path if provided
    ///   2. Search common Roblox installation paths
    ///   3. Start with UseShellExecute=true (uses system association)
    ///   4. Log result for diagnostics
    /// </summary>
    private async Task RestartToHome()
    {
        try
        {
            // Find Roblox executable
            string? robloxPath = _cachedRobloxPath ?? FindRobloxExecutable();

            if (string.IsNullOrEmpty(robloxPath))
            {
                LogToFile("[RobloxRestarter.RestartToHome] ERROR: Cannot restart - Roblox executable not found");
                LogToFile("[RobloxRestarter.RestartToHome] Searched common paths and registry");
                LogToFile("[RobloxRestarter.RestartToHome] User must manually restart Roblox");
                return;
            }

            LogToFile($"[RobloxRestarter.RestartToHome] Found Roblox: {robloxPath}");

            // Start process
            var startInfo = new ProcessStartInfo
            {
                FileName = robloxPath,
                UseShellExecute = true,  // Use system association (cleaner)
                CreateNoWindow = false
            };

            LogToFile("[RobloxRestarter.RestartToHome] Starting Roblox process...");
            var process = Process.Start(startInfo);

            if (process != null)
            {
                LogToFile($"[RobloxRestarter.RestartToHome] ✓ Roblox restarted successfully");
                LogToFile($"[RobloxRestarter.RestartToHome] New PID: {process.Id}");
                LogToFile("[RobloxRestarter.RestartToHome] Roblox launcher will open to home page");
                LogToFile("[RobloxRestarter.RestartToHome] User can now select an unblocked game");
            }
            else
            {
                LogToFile("[RobloxRestarter.RestartToHome] ERROR: Process.Start returned null - check permissions and path");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[RobloxRestarter.RestartToHome] ERROR: {ex.GetType().Name}: {ex.Message}");
            LogToFile($"[RobloxRestarter.RestartToHome] StackTrace: {ex.StackTrace}");
        }

        await Task.CompletedTask;  // Make this async-friendly
    }

    /// <summary>
    /// Locate RobloxPlayerBeta.exe on the system.
    /// 
    /// Strategy:
    ///   1. Search for latest version-XXXX subfolder in AppData\Local\Roblox\Versions
    ///      (Roblox uses versioned folders, not a single Versions folder)
    ///   2. Search other common paths
    ///   3. Registry lookup as fallback
    /// </summary>
    private string? FindRobloxExecutable()
    {
        try
        {
            LogToFile("[RobloxRestarter.FindRobloxExecutable] Searching for Roblox executable...");

            // Strategy 1: Search for latest version-XXXX subfolder in AppData Local
            // (Most modern Roblox installations use this structure)
            var appDataVersionsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Roblox", "Versions");

            if (Directory.Exists(appDataVersionsPath))
            {
                LogToFile($"[RobloxRestarter.FindRobloxExecutable] Searching version folders in: {appDataVersionsPath}");

                try
                {
                    // Find all version-XXXX folders and sort by latest (most recent modification)
                    var versionFolders = Directory.GetDirectories(appDataVersionsPath)
                        .Where(d => Path.GetFileName(d).StartsWith("version-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(d => Directory.GetLastWriteTime(d))
                        .ToArray();

                    LogToFile($"[RobloxRestarter.FindRobloxExecutable] Found {versionFolders.Length} version folder(s)");

                    // Check each version folder, starting with the latest
                    foreach (var versionFolder in versionFolders)
                    {
                        var exePath = Path.Combine(versionFolder, "RobloxPlayerBeta.exe");
                        LogToFile($"[RobloxRestarter.FindRobloxExecutable] Checking: {exePath}");

                        if (File.Exists(exePath))
                        {
                            LogToFile($"[RobloxRestarter.FindRobloxExecutable] ✓ Found: {exePath}");
                            return exePath;
                        }
                    }

                    LogToFile("[RobloxRestarter.FindRobloxExecutable] ⚠ No valid version folders found");
                }
                catch (Exception ex)
                {
                    LogToFile($"[RobloxRestarter.FindRobloxExecutable] Error scanning version folders: {ex.Message}");
                }
            }
            else
            {
                LogToFile($"[RobloxRestarter.FindRobloxExecutable] ⚠ Versions folder not found: {appDataVersionsPath}");
            }

            // Strategy 2: Search other common paths (older installations or alternative locations)
            var otherPaths = new[]
            {
                // Program Files (older installations)
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Roblox", "Versions", "RobloxPlayerBeta.exe"),
                
                // Program Files (x86)
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Roblox", "Versions", "RobloxPlayerBeta.exe"),
            };

            LogToFile("[RobloxRestarter.FindRobloxExecutable] Checking other common paths...");

            foreach (var path in otherPaths)
            {
                LogToFile($"[RobloxRestarter.FindRobloxExecutable] Checking: {path}");

                if (File.Exists(path))
                {
                    LogToFile($"[RobloxRestarter.FindRobloxExecutable] ✓ Found: {path}");
                    return path;
                }
            }

            LogToFile("[RobloxRestarter.FindRobloxExecutable] ⚠ Not found in common paths");

            // Strategy 3: Try registry lookup as fallback
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Classes\roblox-player\shell\open\command"))
                {
                    var command = key?.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(command))
                    {
                        LogToFile($"[RobloxRestarter.FindRobloxExecutable] Found via HKCU registry: {command}");
                        // Registry value is typically: "C:\...\RobloxPlayerBeta.exe" "%1"
                        // Extract just the executable path
                        var extracted = command.Split('"')[1];
                        if (File.Exists(extracted))
                        {
                            LogToFile($"[RobloxRestarter.FindRobloxExecutable] ✓ Extracted and verified: {extracted}");
                            return extracted;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[RobloxRestarter.FindRobloxExecutable] HKCU registry lookup failed: {ex.Message}");
            }

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"Software\Classes\roblox-player\shell\open\command"))
                {
                    var command = key?.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(command))
                    {
                        LogToFile($"[RobloxRestarter.FindRobloxExecutable] Found via HKLM registry: {command}");
                        // Extract just the executable path
                        var extracted = command.Split('"')[1];
                        if (File.Exists(extracted))
                        {
                            LogToFile($"[RobloxRestarter.FindRobloxExecutable] ✓ Extracted and verified: {extracted}");
                            return extracted;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[RobloxRestarter.FindRobloxExecutable] HKLM registry lookup failed: {ex.Message}");
            }

            LogToFile("[RobloxRestarter.FindRobloxExecutable] ERROR: Roblox executable not found");
            return null;
        }
        catch (Exception ex)
        {
            LogToFile($"[RobloxRestarter.FindRobloxExecutable] CRITICAL ERROR: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get Roblox executable path (for diagnostics/testing).
    /// </summary>
    public string? GetRobloxPath()
    {
        return _cachedRobloxPath ?? FindRobloxExecutable();
    }

    /// <summary>
    /// Robust logging to launcher.log with automatic directory creation.
    /// </summary>
    private static void LogToFile(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(_logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}Z] {message}\n");
        }
        catch
        {
            // Last resort: try console
            try
            {
                System.Diagnostics.Debug.WriteLine(message);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            LogToFile("[RobloxRestarter] Disposed");
        }
        catch { }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
