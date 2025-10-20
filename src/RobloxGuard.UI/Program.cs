using RobloxGuard.Core;
using System.Diagnostics;
using System.IO;

namespace RobloxGuard.UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Auto-start mode: when EXE clicked with no arguments
        if (args.Length == 0)
        {
            HandleAutoStartMode();
            return;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "--handle-uri":
                HandleProtocolUri(args.Length > 1 ? args[1] : null);
                break;

            case "--show-block-ui":
                if (long.TryParse(args.Length > 1 ? args[1] : "", out var placeId))
                    ShowBlockUI(placeId);
                else
                    Console.WriteLine("Usage: --show-block-ui <placeId>");
                break;

            case "--install-first-run":
                PerformInstall();
                break;

            case "--uninstall":
                PerformUninstall();
                break;

            case "--monitor-logs":
                MonitorPlayerLogs();
                break;

            case "--ui":
                ShowSettingsUI();
                break;

            case "--help":
            case "-h":
                ShowHelp();
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                ShowHelp();
                break;
        }
    }

    /// <summary>
    /// Handles auto-start mode when EXE is clicked with no arguments.
    /// Logic:
    /// 1. If not installed: Show help and exit
    /// 2. If monitor already running: Show settings UI
    /// 3. If monitor not running: Start it in background
    /// </summary>
    static void HandleAutoStartMode()
    {
        try
        {
            // Step 1: Check if RobloxGuard is installed
            if (!InstallerHelper.IsInstalled())
            {
                Console.WriteLine("RobloxGuard is not installed yet.");
                Console.WriteLine("Please run: RobloxGuard.exe --install-first-run");
                Console.WriteLine();
                Console.WriteLine("Or use the installer to set up RobloxGuard.");
                System.Threading.Thread.Sleep(3000);
                return;
            }

            // Step 2: Check if monitor is already running
            if (MonitorStateHelper.IsMonitorRunning())
            {
                Console.WriteLine(MonitorStateHelper.GetMonitorStatus());
                Console.WriteLine("Opening settings UI...");
                System.Threading.Thread.Sleep(1000);
                ShowSettingsUI();
                return;
            }

            // Step 3: Start monitor in background
            Console.WriteLine("Starting RobloxGuard monitoring...");
            StartMonitorInBackground();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in auto-start mode: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            System.Threading.Thread.Sleep(2000);
        }
    }

    /// <summary>
    /// Starts the LogMonitor process in the background with no visible window.
    /// </summary>
    static void StartMonitorInBackground()
    {
        try
        {
            string appExePath = GetApplicationPath();

            // Create process info for background monitor
            var psi = new ProcessStartInfo
            {
                FileName = appExePath,
                Arguments = "--monitor-logs",
                UseShellExecute = true,
                CreateNoWindow = true,           // Hide console window
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = false,   // Don't redirect when using UseShellExecute=true
                RedirectStandardError = false,
            };

            // Start the process
            using var process = Process.Start(psi);
            
            if (process == null)
            {
                Console.WriteLine("✗ Failed to start monitor process");
                System.Threading.Thread.Sleep(2000);
                return;
            }

            Console.WriteLine("✓ RobloxGuard monitoring started in background");
            Console.WriteLine($"  Process ID: {process.Id}");
            Console.WriteLine();
            Console.WriteLine("Monitoring is now active. You can close this window.");
            System.Threading.Thread.Sleep(2000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to start monitor: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Error details: {ex}");
            System.Threading.Thread.Sleep(2000);
        }
    }

    /// <summary>
    /// Gets the path to the RobloxGuard executable.
    /// Handles both single-file published apps and normal builds.
    /// </summary>
    static string GetApplicationPath()
    {
        // Try AppContext.BaseDirectory first (works for single-file published apps)
        string appExePath = Path.Combine(AppContext.BaseDirectory, "RobloxGuard.exe");
        if (File.Exists(appExePath))
        {
            return appExePath;
        }

        // Fallback to Assembly.Location
        appExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (File.Exists(appExePath))
        {
            return appExePath;
        }

        // Last resort: Try to find in current directory
        appExePath = "RobloxGuard.exe";
        if (File.Exists(appExePath))
        {
            return appExePath;
        }

        throw new InvalidOperationException(
            "Could not locate RobloxGuard.exe. " +
            "Tried: AppContext.BaseDirectory, Assembly.Location, and current directory."
        );
    }

    static void ShowHelp()
    {
        Console.WriteLine("RobloxGuard - Parental Control for Roblox");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  RobloxGuard.exe                        Auto-start monitor in background");
        Console.WriteLine("  RobloxGuard.exe --handle-uri <uri>     Handle roblox-player:// protocol");
        Console.WriteLine("  RobloxGuard.exe --show-block-ui <id>   Show block UI (testing)");
        Console.WriteLine("  RobloxGuard.exe --monitor-logs         Monitor Roblox logs (foreground)");
        Console.WriteLine("  RobloxGuard.exe --ui                   Show settings UI");
        Console.WriteLine("  RobloxGuard.exe --install-first-run    Install RobloxGuard");
        Console.WriteLine("  RobloxGuard.exe --uninstall            Uninstall RobloxGuard");
        Console.WriteLine("  RobloxGuard.exe --help                 Show this help");
    }

    static void HandleProtocolUri(string? uri)
    {
        Console.WriteLine("=== Protocol Handler Mode ===");
        Console.WriteLine($"URI: {uri}");
        Console.WriteLine();

        if (string.IsNullOrEmpty(uri))
        {
            Console.WriteLine("ERROR: No URI provided");
            return;
        }

        // Parse placeId
        var placeId = PlaceIdParser.Extract(uri);
        Console.WriteLine($"Extracted placeId: {placeId?.ToString() ?? "None"}");

        if (!placeId.HasValue)
        {
            Console.WriteLine("WARNING: Could not extract placeId from URI");
            Console.WriteLine("Forwarding to upstream handler...");
            // TODO: Forward to upstream
            return;
        }

        // Load config
        var config = ConfigManager.Load();
        Console.WriteLine($"Config loaded from: {ConfigManager.GetConfigPath()}");
        Console.WriteLine($"Blocklist mode: {(config.WhitelistMode ? "Whitelist" : "Blacklist")}");
        Console.WriteLine($"Blocked games: {config.Blocklist.Count}");

        // Check if blocked
        bool isBlocked = ConfigManager.IsBlocked(placeId.Value, config);
        Console.WriteLine();

        if (isBlocked)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"🚫 BLOCKED: PlaceId {placeId} is not allowed");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("This game would be blocked. Block UI would be shown here.");
            // TODO: Show block UI
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ ALLOWED: PlaceId {placeId} is permitted");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Forwarding to upstream handler...");
            // TODO: Forward to upstream handler
        }
    }

    static void ShowSettingsUI()
    {
        var app = new System.Windows.Application();
        var window = new SettingsWindow();
        app.Run(window);
    }

    static void ShowBlockUI(long placeId)
    {
        var app = new System.Windows.Application();
        var window = new BlockWindow(placeId);
        
        if (window.ShowDialog() == true && window.IsUnlocked)
        {
            Console.WriteLine($"✅ User entered correct PIN - game unlocked");
        }
        else
        {
            Console.WriteLine($"❌ Game blocked - access denied");
        }
    }

    static void PerformInstall()
    {
        try
        {
            Console.WriteLine("Starting RobloxGuard installation...");
            
            // Get app executable path
            // Use AppContext.BaseDirectory for single-file published apps
            string appExePath = Path.Combine(AppContext.BaseDirectory, "RobloxGuard.exe");
            if (!File.Exists(appExePath))
            {
                // Fallback to Assembly.Location if BaseDirectory fails
                appExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            }
            if (string.IsNullOrEmpty(appExePath) || !File.Exists(appExePath))
            {
                Console.WriteLine("ERROR: Could not determine application path.");
                Environment.Exit(1);
                return;
            }

            // Perform installation
            var (setupSuccess, setupMessages) = InstallerHelper.PerformFirstRunSetup(appExePath);
            
            // Display setup results
            Console.WriteLine();
            foreach (var message in setupMessages)
            {
                Console.WriteLine(message);
            }

            if (setupSuccess)
            {
                Console.WriteLine();
                Console.WriteLine("✓ Installation completed successfully!");
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("✗ Installation failed. Please check the errors above.");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Installation failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void PerformUninstall()
    {
        try
        {
            Console.WriteLine("Uninstalling RobloxGuard...");
            
            // Perform uninstallation
            InstallerHelper.PerformUninstall();
            
            Console.WriteLine("✓ Uninstallation completed successfully!");
            Console.WriteLine($"  • Protocol handler restored");
            Console.WriteLine($"  • Scheduled task removed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Uninstallation failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void MonitorPlayerLogs()
    {
        Console.WriteLine("=== Log Monitor Mode ===");
        Console.WriteLine("Monitoring Roblox player logs for game joins...");
        Console.WriteLine($"Log directory: {Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "logs")}");
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();

        using (var monitor = new LogMonitor(OnGameDetected))
        {
            monitor.Start();
            try
            {
                while (true)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nLog monitor stopped.");
            }
        }
    }

    static void OnGameDetected(LogBlockEvent evt)
    {
        var timestamp = evt.Timestamp.ToString("HH:mm:ss");
        Console.WriteLine($"\n>>> GAME DETECTED: {evt.PlaceId}");
        if (evt.IsBlocked)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{timestamp}] ❌ BLOCKED: Game {evt.PlaceId}");
            Console.ResetColor();
            
            // Show alert window on the UI thread
            ShowAlertWindowThreadSafe();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{timestamp}] ✅ ALLOWED: Game {evt.PlaceId}");
            Console.ResetColor();
        }
        Console.Out.Flush();
    }

    /// <summary>
    /// Thread-safe method to show alert window from background monitor thread.
    /// </summary>
    static void ShowAlertWindowThreadSafe()
    {
        try
        {
            // Create and show alert window on its own thread to avoid blocking the monitor
            var thread = new Thread(() =>
            {
                try
                {
                    var alert = new AlertWindow();
                    alert.ShowDialog();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlertWindow] Error showing alert: {ex.Message}");
                }
            })
            {
                IsBackground = false
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGameDetected] Error showing alert: {ex.Message}");
        }
    }
}
