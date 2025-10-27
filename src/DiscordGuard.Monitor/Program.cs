using System.Diagnostics;
using RobloxGuard.Core;

namespace DiscordGuard.Monitor;

/// <summary>
/// DiscordGuard Monitor - Event-driven Roblox game monitor using Discord notifications
/// 
/// This is a NEW implementation (v2.0) that replaces time-based scheduling with event-driven
/// Discord notification triggering.
/// 
/// Key Differences from RobloxGuard v1.x:
/// - No PlaytimeTracker (no time-based killing)
/// - No after-hours enforcement (no schedule-based killing)
/// - Pure event-driven: kills only happen when Discord notification received
/// - Monitors user presence in blocked games
/// - Triggers graceful kill on Discord keyword match
/// 
/// Build Output: build\DiscordGuard\DiscordGuard.exe
/// Config: %LOCALAPPDATA%\DiscordGuard\config.json
/// Logs: %LOCALAPPDATA%\DiscordGuard\launcher.log
/// </summary>
class Program
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiscordGuard",
        "launcher.log"
    );

    static void LogToFile(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"[{DateTime.UtcNow:HH:mm:ss}Z] {message}\n");
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        LogToFile("=== DiscordGuard v2.0 (Discord Notification Monitor) ===");
        LogToFile($"Started with args: {string.Join(" ", args)}");

        try
        {
            if (args.Length == 0)
            {
                HandleAutoStartMode();
                return;
            }

            var command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "--monitor":
                    MonitorDiscordNotifications();
                    break;

                case "--test":
                    TestMode();
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
        catch (Exception ex)
        {
            LogToFile($"[ERROR] Unhandled exception: {ex.Message}");
            LogToFile($"[ERROR] StackTrace: {ex.StackTrace}");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void HandleAutoStartMode()
    {
        LogToFile("[Main] Auto-start mode - starting monitoring");
        MonitorDiscordNotifications();
    }

    static void MonitorDiscordNotifications()
    {
        LogToFile("[Monitor] Starting Discord notification monitoring");
        Console.WriteLine("DiscordGuard Monitor Started");
        Console.WriteLine("Listening for Discord notifications...");
        Console.WriteLine("Press Ctrl+C to stop");

        // TODO: Implement Discord notification listener
        // This will be filled in during Phase 2 of implementation
        
        LogToFile("[Monitor] Discord notification listener initialized");

        try
        {
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
        catch (OperationCanceledException)
        {
            LogToFile("[Monitor] Monitoring stopped");
        }
    }

    static void TestMode()
    {
        LogToFile("[Test] Running in test mode");
        Console.WriteLine("DiscordGuard Test Mode");
        
        // TODO: Add test functions for development
        
        LogToFile("[Test] Test mode complete");
    }

    static void ShowHelp()
    {
        Console.WriteLine(@"
DiscordGuard v2.0 - Discord Notification Roblox Monitor
========================================================

Usage: DiscordGuard.exe [command]

Commands:
  (no args)           Auto-start monitoring (default)
  --monitor          Start Discord notification monitoring
  --test             Run in test mode
  --help, -h         Show this help message

Configuration:
  Location: %LOCALAPPDATA%\DiscordGuard\config.json
  
Required Settings:
  - blockedGames: List of placeIds to monitor/block
  - discordSourceUserId: Discord user ID to listen for
  - discordTriggerKeyword: Keyword that triggers game kill

Example config.json:
{
  ""blockedGames"": [
    {""placeId"": 15532962292, ""name"": ""Blocked Game 1""}
  ],
  ""discordSourceUserId"": ""123456789"",
  ""discordTriggerKeyword"": ""close game"",
  ""discordNotificationEnabled"": true,
  ""notificationMonitorEnabled"": true,
  ""autoRestartOnKill"": true,
  ""overlayEnabled"": true,
  ""silentMode"": true
}

Logs:
  Location: %LOCALAPPDATA%\DiscordGuard\launcher.log

Exit: Ctrl+C
");
    }
}
