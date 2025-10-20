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

    static void ShowHelp()
    {
        Console.WriteLine("RobloxGuard - Parental Control for Roblox");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  RobloxGuard.exe --handle-uri <uri>     Handle roblox-player:// protocol");
        Console.WriteLine("  RobloxGuard.exe --show-block-ui <id>   Show block UI (testing)");
        Console.WriteLine("  RobloxGuard.exe --monitor-logs         Monitor Roblox logs for game joins");
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
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{timestamp}] ✅ ALLOWED: Game {evt.PlaceId}");
            Console.ResetColor();
        }
        Console.Out.Flush();
    }
}
