using RobloxGuard.Core;
using System.Diagnostics;
using System.IO;

namespace RobloxGuard.UI;

class Program
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    /// <summary>
    /// Logs debug info to a file for troubleshooting when console is not visible.
    /// </summary>
    static void LogToFile(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        // Register global exception handler to prevent silent crashes
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            LogToFile($"[GLOBAL] UNHANDLED EXCEPTION: {e.ExceptionObject.GetType().Name}");
            if (e.ExceptionObject is Exception ex)
            {
                LogToFile($"[GLOBAL] Message: {ex.Message}");
                LogToFile($"[GLOBAL] StackTrace: {ex.StackTrace}");
            }
            LogToFile($"[GLOBAL] IsTerminating: {e.IsTerminating}");
        };

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

            case "--register-protocol":
                RegisterProtocol();
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
    /// 1. If not installed: Auto-install silently
    /// 2. Clean up any stale mutex
    /// 3. If monitor not running: Start it in background
    /// </summary>
    static void HandleAutoStartMode()
    {
        try
        {
            LogToFile("=== AUTO-START MODE ===");
            LogToFile($"ProcessPath: {Environment.ProcessPath}");
            LogToFile($"BaseDirectory: {AppContext.BaseDirectory}");

            // Step 0: Clean up any stale mutex from previous failed instances
            LogToFile("Step 0: Cleaning up stale resources...");
            MonitorStateHelper.ForceCleanup();

            // Step 1: Auto-install if not already installed
            LogToFile("Step 1: Checking if installed...");
            if (!InstallerHelper.IsInstalled())
            {
                LogToFile("First run detected. Installing RobloxGuard...");
                PerformInstall();
                LogToFile("Installation complete!");
                System.Threading.Thread.Sleep(500);
            }
            else
            {
                LogToFile("Already installed.");
            }

            // Step 2: Check if monitor is already running
            LogToFile("Step 2: Checking if monitor is running...");
            bool isRunning = MonitorStateHelper.IsMonitorRunning();
            LogToFile($"IsMonitorRunning() = {isRunning}");
            Console.WriteLine($"[Program] IsMonitorRunning() = {isRunning}");
            
            if (isRunning)
            {
                LogToFile("Monitor already running. Exiting.");
                Console.WriteLine("[Program] Monitor already running, exiting.");
                System.Threading.Thread.Sleep(500);
                return;
            }

            // Step 3: Start monitor in background
            LogToFile("Step 3: Starting monitor in background...");
            Console.WriteLine("[Program] Starting monitor in background");
            StartMonitorInBackground();
            
            // CRITICAL: Exit immediately after starting monitor
            // This releases all resources and allows monitor to acquire the global mutex
            LogToFile("Launcher exiting to allow monitor full control...");
            System.Threading.Thread.Sleep(200);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            LogToFile($"ERROR: {ex.Message}");
            LogToFile($"Stack: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Starts the LogMonitor process in the background with no visible window.
    /// Uses UseShellExecute=false for better process tracking and error detection.
    /// </summary>
    static void StartMonitorInBackground()
    {
        try
        {
            string appExePath = GetApplicationPath();
            LogToFile($"App path resolved to: {appExePath}");

            // Create process info for background monitor
            // UseShellExecute=false allows better tracking and error redirection
            var psi = new ProcessStartInfo
            {
                FileName = appExePath,
                Arguments = "--monitor-logs",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            LogToFile($"Starting process: {psi.FileName} {psi.Arguments}");

            // Start the process
            using var process = Process.Start(psi);
            
            if (process == null)
            {
                LogToFile("ERROR: Failed to start monitor process (returned null)");
                System.Threading.Thread.Sleep(2000);
                return;
            }

            LogToFile($"✓ Monitor started: PID {process.Id}");
            
            // Brief wait to capture any immediate startup errors
            System.Threading.Thread.Sleep(500);
            
            // Check if process exited immediately (indicates startup error)
            if (process.HasExited)
            {
                LogToFile($"⚠ Monitor process exited immediately (exit code: {process.ExitCode})");
                
                // Try to read error output
                try
                {
                    if (process.StandardError.Peek() > -1)
                    {
                        string? stderr = process.StandardError.ReadToEnd();
                        if (!string.IsNullOrEmpty(stderr))
                        {
                            LogToFile($"Monitor stderr: {stderr}");
                        }
                    }
                }
                catch { }
                
                try
                {
                    if (process.StandardOutput.Peek() > -1)
                    {
                        string? stdout = process.StandardOutput.ReadToEnd();
                        if (!string.IsNullOrEmpty(stdout))
                        {
                            LogToFile($"Monitor stdout: {stdout}");
                        }
                    }
                }
                catch { }
                
                return;
            }

            LogToFile($"✓ Monitor process is running (will continue in background)");
            System.Threading.Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            LogToFile($"ERROR starting monitor: {ex.Message}");
            LogToFile($"Stack: {ex.StackTrace}");
            System.Threading.Thread.Sleep(2000);
        }
    }

    /// <summary>
    /// Gets the path to the RobloxGuard executable.
    /// Handles both single-file published apps and normal builds.
    /// </summary>
    static string GetApplicationPath()
    {
        // For single-file published apps, use Environment.ProcessPath (most reliable)
        if (!string.IsNullOrEmpty(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        // Try AppContext.BaseDirectory
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
        Console.WriteLine("  RobloxGuard.exe --monitor-logs         Monitor Roblox logs (foreground)");
        Console.WriteLine("  RobloxGuard.exe --register-protocol    Enable pre-launch game blocking");
        Console.WriteLine("  RobloxGuard.exe --handle-uri <uri>     Handle roblox-player:// protocol");
        Console.WriteLine("  RobloxGuard.exe --install-first-run    Perform first-run setup");
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

    static void PerformInstall()
    {
        try
        {
            LogToFile("PerformInstall: Starting...");
            
            // Get app executable path - use GetApplicationPath() for consistency
            string appExePath = GetApplicationPath();
            LogToFile($"PerformInstall: App path = {appExePath}");

            // Perform installation
            var (setupSuccess, setupMessages) = InstallerHelper.PerformFirstRunSetup(appExePath);
            
            // Log setup results
            LogToFile($"PerformInstall: Success = {setupSuccess}");
            foreach (var message in setupMessages)
            {
                LogToFile($"  {message}");
            }

            if (!setupSuccess)
            {
                LogToFile("PerformInstall: FAILED!");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"PerformInstall ERROR: {ex.Message}");
            LogToFile($"Stack: {ex.StackTrace}");
        }
    }

    static void PerformUninstall()
    {
        try
        {
            LogToFile("=== UNINSTALL MODE ===");
            LogToFile("Starting uninstallation...");
            
            // Step 0: Force cleanup of mutex
            LogToFile("Cleaning up system resources...");
            MonitorStateHelper.ForceCleanup();
            System.Threading.Thread.Sleep(200);
            // Step 1: Kill any running RobloxGuard processes (multiple times to be sure)
            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var processes = Process.GetProcessesByName("RobloxGuard");
                    int killed = 0;
                    foreach (var proc in processes)
                    {
                        // Don't kill ourselves, only background monitors
                        if (proc.Id != Environment.ProcessId)
                        {
                            try
                            {
                                proc.Kill(true); // Kill with children
                                killed++;
                                LogToFile($"✓ Killed process PID {proc.Id} (attempt {attempt + 1})");
                            }
                            catch (Exception e)
                            {
                                LogToFile($"Warning killing PID {proc.Id}: {e.Message}");
                            }
                        }
                    }
                    
                    if (killed == 0)
                    {
                        LogToFile($"No processes to kill on attempt {attempt + 1}");
                        break;
                    }
                    
                    if (attempt < 2)
                    {
                        System.Threading.Thread.Sleep(500); // Wait before retry
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Warning: Process termination issue: {ex.Message}");
            }
            
            // Give processes time to die
            System.Threading.Thread.Sleep(1000);
            
            // Step 2: Perform uninstallation (registry restore)
            InstallerHelper.PerformUninstall();
            LogToFile("✓ Protocol handler restored");
            
            // Step 3: Delete AppData folder
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RobloxGuard"
            );
            
            // Flush logs before deleting folder
            LogToFile("✓ Uninstallation completed successfully!");
            LogToFile("Cleaning up...");
            
            // Give the log system time to flush and processes to exit
            System.Threading.Thread.Sleep(500);
            
            // Final check: kill any remaining RobloxGuard processes one more time
            try
            {
                var finalProcesses = Process.GetProcessesByName("RobloxGuard");
                foreach (var proc in finalProcesses)
                {
                    if (proc.Id != Environment.ProcessId)
                    {
                        try
                        {
                            proc.Kill(true);
                            LogToFile($"✓ Final kill of PID {proc.Id}");
                        }
                        catch { }
                    }
                }
            }
            catch { }
            
            System.Threading.Thread.Sleep(500);
            
            if (Directory.Exists(appDataPath))
            {
                try
                {
                    // Delete the entire folder including logs
                    Directory.Delete(appDataPath, true);
                    LogToFile("✓ AppData folder deleted");
                }
                catch (Exception ex)
                {
                    LogToFile($"Warning: Could not delete AppData: {ex.Message}");
                }
            }
            
            // Show completion message
            Console.WriteLine("\n✓ RobloxGuard has been uninstalled successfully!");
            Console.WriteLine("  - Protocol handler restored");
            Console.WriteLine("  - All files removed");
            Console.WriteLine("  - Monitor process terminated\n");
            System.Threading.Thread.Sleep(1500);
            
            // Force exit to ensure launcher process terminates completely
            // This allows AppData folder to be fully deleted (no file locks)
            LogToFile("Exiting launcher process...");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            LogToFile($"✗ Uninstallation failed: {ex.Message}");
            LogToFile($"Stack: {ex.StackTrace}");
            Console.WriteLine($"\n✗ Uninstall error: {ex.Message}\n");
            Environment.Exit(1);
        }
    }

    static void RegisterProtocol()
    {
        try
        {
            Console.WriteLine("Registering protocol handler...");
            
            // Get app executable path
            string appExePath = Path.Combine(AppContext.BaseDirectory, "RobloxGuard.exe");
            if (!File.Exists(appExePath))
            {
                appExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            }
            if (string.IsNullOrEmpty(appExePath) || !File.Exists(appExePath))
            {
                Console.WriteLine("ERROR: Could not determine application path.");
                Environment.Exit(1);
                return;
            }

            // Register protocol handler
            var (success, messages) = InstallerHelper.RegisterProtocolHandler(appExePath);
            
            // Display results
            Console.WriteLine();
            foreach (var message in messages)
            {
                Console.WriteLine(message);
            }

            if (success)
            {
                Console.WriteLine();
                Console.WriteLine("✓ Protocol handler registered successfully!");
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("✗ Protocol registration failed. Please check the errors above.");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Protocol registration failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void MonitorPlayerLogs()
    {
        LogToFile("=== LOG MONITOR MODE STARTING ===");
        Console.WriteLine("=== Log Monitor Mode ===");
        Console.WriteLine("Monitoring Roblox player logs for game joins...");
        Console.WriteLine($"Log directory: {Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "logs")}");
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();

        using (var monitor = new LogMonitor(OnGameDetected))
        {
            try
            {
                LogToFile("[MonitorPlayerLogs] Starting LogMonitor...");
                monitor.Start();
                LogToFile("[MonitorPlayerLogs] LogMonitor started successfully");
                
                try
                {
                    while (true)
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (OperationCanceledException)
                {
                    LogToFile("[MonitorPlayerLogs] Monitor cancelled");
                    Console.WriteLine("\nLog monitor stopped.");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[MonitorPlayerLogs] CRITICAL ERROR: {ex.GetType().Name}: {ex.Message}");
                LogToFile($"[MonitorPlayerLogs] Stack: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogToFile($"[MonitorPlayerLogs] InnerException: {ex.InnerException.Message}");
                }
                throw;
            }
        }
    }

    static void OnGameDetected(LogBlockEvent evt)
    {
        try
        {
            var timestamp = evt.Timestamp.ToString("HH:mm:ss");
            Console.WriteLine($"\n>>> GAME DETECTED: {evt.PlaceId}");
            LogToFile($"[OnGameDetected] Game {evt.PlaceId} detected, IsBlocked={evt.IsBlocked}");
            
            if (evt.IsBlocked)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{timestamp}] ❌ BLOCKED: Game {evt.PlaceId}");
                Console.WriteLine("╔════════════════════════════════════════╗");
                Console.WriteLine("║  🧠❌ BRAINDEAD CONTENT DETECTED 🧠❌  ║");
                Console.WriteLine("║                                        ║");
                Console.WriteLine("║     This game is blocked by parent.    ║");
                Console.WriteLine("║     The Roblox process was closed.     ║");
                Console.WriteLine("║                                        ║");
                Console.WriteLine("║        Blocking will continue for      ║");
                Console.WriteLine("║        all games on the blocklist.     ║");
                Console.WriteLine("╚════════════════════════════════════════╝");
                Console.ResetColor();
                
                LogToFile($"[OnGameDetected] Game is blocked, showing alert window...");
                
                // Show alert window (Windows Forms - more reliable)
                ShowAlertWindowThreadSafe();
                
                LogToFile($"[OnGameDetected] Alert dispatch complete, monitor continues");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{timestamp}] ✅ ALLOWED: Game {evt.PlaceId}");
                Console.ResetColor();
                LogToFile($"[OnGameDetected] Game is allowed");
            }
            Console.Out.Flush();
        }
        catch (Exception ex)
        {
            LogToFile($"[OnGameDetected] ERROR: {ex.GetType().Name}: {ex.Message}");
            LogToFile($"[OnGameDetected] Stack: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Show alert window using Windows Forms (more reliable than WPF, fewer dependencies).
    /// Runs on a separate thread so the monitor continues in the background.
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
                    LogToFile("[AlertForm] Thread starting...");
                    
                    // Set up exception handler for this thread
                    AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    {
                        LogToFile($"[AlertForm-Thread] UNHANDLED: {e.ExceptionObject.GetType().Name}");
                        if (e.ExceptionObject is Exception ex)
                        {
                            LogToFile($"[AlertForm-Thread] {ex.Message}");
                            LogToFile($"[AlertForm-Thread] {ex.StackTrace}");
                        }
                    };
                    
                    LogToFile("[AlertForm] Enabling visual styles (may only work once per AppDomain)...");
                    try
                    {
                        System.Windows.Forms.Application.EnableVisualStyles();
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[AlertForm] EnableVisualStyles warning (harmless): {ex.Message}");
                    }
                    
                    LogToFile("[AlertForm] Creating AlertForm instance (Windows Forms)...");
                    var alert = new AlertForm();
                    alert.Visible = true;
                    
                    LogToFile("[AlertForm] Starting application message loop...");
                    // Run the form with its own message loop
                    // Set form to visible BEFORE calling Application.Run()
                    System.Windows.Forms.Application.Run(alert);
                    
                    LogToFile($"[AlertForm] Form closed, message loop ended");
                }
                catch (ThreadAbortException ex)
                {
                    LogToFile($"[AlertForm] Thread abort (expected): {ex.Message}");
                }
                catch (Exception ex)
                {
                    LogToFile($"[AlertForm] CRITICAL ERROR: {ex.GetType().Name}: {ex.Message}");
                    LogToFile($"[AlertForm] Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        LogToFile($"[AlertForm] Inner exception: {ex.InnerException.Message}");
                        LogToFile($"[AlertForm] Inner stack: {ex.InnerException.StackTrace}");
                    }
                    
                    // Try to show a fallback message box as last resort
                    try
                    {
                        LogToFile("[AlertForm] Attempting fallback MessageBox...");
                        System.Windows.Forms.MessageBox.Show(
                            "Game blocked by RobloxGuard.\n\nThe Roblox process was terminated.",
                            "🧠❌ BRAINDEAD CONTENT DETECTED",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Stop
                        );
                        LogToFile("[AlertForm] Fallback MessageBox shown successfully");
                    }
                    catch (Exception mbEx)
                    {
                        LogToFile($"[AlertForm] Fallback MessageBox also failed: {mbEx.Message}");
                        LogToFile($"[AlertForm] {mbEx.StackTrace}");
                    }
                }
                finally
                {
                    LogToFile("[AlertForm] Thread cleanup complete");
                }
            })
            {
                IsBackground = false
            };
            
            thread.Name = "AlertFormThread";
            
            LogToFile($"[OnGameDetected] Starting alert form thread...");
            thread.Start();
            
            LogToFile($"[OnGameDetected] Alert thread started (ID: {thread.ManagedThreadId})");
        }
        catch (Exception ex)
        {
            LogToFile($"[OnGameDetected] CRITICAL ERROR starting alert thread: {ex.Message}");
            LogToFile($"[OnGameDetected] Stack trace: {ex.StackTrace}");
        }
    }
}
