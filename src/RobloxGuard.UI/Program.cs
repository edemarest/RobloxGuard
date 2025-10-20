using RobloxGuard.Core;
using System.IO;

namespace RobloxGuard.UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Console mode for testing during development
        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "--handle-uri":
                HandleProtocolUri(args.Length > 1 ? args[1] : null);
                break;

            case "--test-parse":
                TestParsing(args.Length > 1 ? args[1] : null);
                break;

            case "--test-config":
                TestConfiguration();
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

            case "--watch":
                StartWatcher();
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

    static void ShowHelp()
    {
        Console.WriteLine("RobloxGuard - Parental Control for Roblox");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  RobloxGuard.exe --handle-uri <uri>     Handle roblox-player:// protocol");
        Console.WriteLine("  RobloxGuard.exe --test-parse <input>   Test placeId parsing");
        Console.WriteLine("  RobloxGuard.exe --test-config          Test configuration system");
        Console.WriteLine("  RobloxGuard.exe --show-block-ui <id>   Show block UI (testing)");
        Console.WriteLine("  RobloxGuard.exe --watch                Start process watcher");
        Console.WriteLine("  RobloxGuard.exe --ui                   Show settings UI");
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

    static void TestParsing(string? input)
    {
        Console.WriteLine("=== PlaceId Parser Test ===");
        Console.WriteLine($"Input: {input}");
        Console.WriteLine();

        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("ERROR: No input provided");
            return;
        }

        var placeId = PlaceIdParser.Extract(input);
        Console.WriteLine($"Result: {placeId?.ToString() ?? "None"}");

        var allPlaceIds = PlaceIdParser.ExtractAll(input);
        if (allPlaceIds.Count > 1)
        {
            Console.WriteLine($"All placeIds found: {string.Join(", ", allPlaceIds)}");
        }
    }

    static void TestConfiguration()
    {
        Console.WriteLine("=== Configuration Test ===");
        Console.WriteLine($"App data path: {ConfigManager.GetAppDataPath()}");
        Console.WriteLine($"Config path: {ConfigManager.GetConfigPath()}");
        Console.WriteLine();

        // Load config
        var config = ConfigManager.Load();
        Console.WriteLine("Current configuration:");
        Console.WriteLine($"  Blocklist count: {config.Blocklist.Count}");
        Console.WriteLine($"  Whitelist mode: {config.WhitelistMode}");
        Console.WriteLine($"  Overlay enabled: {config.OverlayEnabled}");
        Console.WriteLine($"  PIN set: {!string.IsNullOrEmpty(config.ParentPINHash)}");

        if (config.Blocklist.Count > 0)
        {
            Console.WriteLine($"  Blocked games: {string.Join(", ", config.Blocklist)}");
        }

        Console.WriteLine();
        Console.WriteLine("Testing PIN hashing...");
        var testPin = "1234";
        var hash = ConfigManager.HashPIN(testPin);
        Console.WriteLine($"  Hash: {hash.Substring(0, Math.Min(50, hash.Length))}...");
        
        bool verified = ConfigManager.VerifyPIN(testPin, hash);
        Console.WriteLine($"  Verification: {(verified ? "✅ Success" : "❌ Failed")}");

        bool wrongVerified = ConfigManager.VerifyPIN("9999", hash);
        Console.WriteLine($"  Wrong PIN: {(wrongVerified ? "❌ SECURITY ISSUE" : "✅ Correctly rejected")}");
    }

    static void StartWatcher()
    {
        Console.WriteLine("=== Process Watcher Mode ===");
        Console.WriteLine("Watching for RobloxPlayerBeta.exe...");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();

        try
        {
            using var watcher = new ProcessWatcher(OnProcessBlocked);
            watcher.Start();
            Console.WriteLine("✅ Watcher started successfully");
            Console.WriteLine();

            // Keep running until Ctrl+C
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error starting watcher: {ex.Message}");
            Console.ResetColor();
        }
    }

    static void OnProcessBlocked(ProcessBlockEvent e)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚫 BLOCKED PROCESS DETECTED");
        Console.WriteLine($"  Process ID: {e.ProcessId}");
        Console.WriteLine($"  PlaceId: {e.PlaceId}");
        Console.WriteLine($"  Command: {e.CommandLine}");
        Console.WriteLine();

        if (e.Process != null && !e.Process.HasExited)
        {
            Console.WriteLine("  Terminating process...");
            ProcessWatcher.BlockProcess(e.Process);
            Console.WriteLine("  ✅ Process terminated");
        }

        Console.WriteLine("  (Block UI would be shown here)");
        Console.WriteLine();
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
            InstallerHelper.PerformFirstRunSetup(appExePath);
            
            Console.WriteLine("✓ Installation completed successfully!");
            Console.WriteLine($"  • Protocol handler registered");
            Console.WriteLine($"  • Scheduled task created (runs on logon)");
            Console.WriteLine($"  • Configuration initialized");
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
}
