using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RobloxGuard.Core;

/// <summary>
/// Represents a blocked game with its placeId and optional name.
/// </summary>
public class BlockedGame
{
    [JsonPropertyName("placeId")]
    public long PlaceId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Configuration model for RobloxGuard.
/// </summary>
public class RobloxGuardConfig
{
    /// <summary>
    /// List of blocked games with names for easy editing.
    /// </summary>
    [JsonPropertyName("blockedGames")]
    public List<BlockedGame> BlockedGames { get; set; } = new();

    /// <summary>
    /// Legacy blocklist for backward compatibility - automatically synced with BlockedGames.
    /// </summary>
    [JsonPropertyName("blocklist")]
    public List<long> Blocklist { get; set; } = new();

    /// <summary>
    /// Parent PIN hash (PBKDF2).
    /// </summary>
    [JsonPropertyName("parentPINHash")]
    public string? ParentPINHash { get; set; }

    /// <summary>
    /// The original upstream handler command before RobloxGuard was installed.
    /// </summary>
    [JsonPropertyName("upstreamHandlerCommand")]
    public string? UpstreamHandlerCommand { get; set; }

    /// <summary>
    /// Whether overlay mode is enabled.
    /// </summary>
    [JsonPropertyName("overlayEnabled")]
    public bool OverlayEnabled { get; set; } = true;

    /// <summary>
    /// Whether to use whitelist mode (only allow listed games) instead of blacklist.
    /// </summary>
    [JsonPropertyName("whitelistMode")]
    public bool WhitelistMode { get; set; } = false;

    // ========== FEATURE A: Playtime Limit ==========

    /// <summary>
    /// Feature A: Enable automatic termination of blocked games after playtime limit exceeded.
    /// </summary>
    [JsonPropertyName("playtimeLimitEnabled")]
    public bool PlaytimeLimitEnabled { get; set; } = false;

    /// <summary>
    /// Feature A: Maximum playtime in minutes before blocked game is terminated. Default: 120 (2 hours).
    /// Kill is scheduled with random 0-60 minute delay.
    /// </summary>
    [JsonPropertyName("playtimeLimitMinutes")]
    public int PlaytimeLimitMinutes { get; set; } = 120;

    /// <summary>
    /// Feature A: Show block UI when blocked game is terminated due to playtime limit.
    /// </summary>
    [JsonPropertyName("showBlockUIOnPlaytimeKill")]
    public bool ShowBlockUIOnPlaytimeKill { get; set; } = true;

    // ========== FEATURE B: After-Hours Enforcement ==========

    /// <summary>
    /// Feature B: Enable automatic termination of blocked games joined after hours.
    /// </summary>
    [JsonPropertyName("afterHoursEnforcementEnabled")]
    public bool AfterHoursEnforcementEnabled { get; set; } = false;

    /// <summary>
    /// Feature B: Hour (0-23) when after-hours enforcement starts. Default: 3 (3 AM).
    /// Games joined at or after this hour will be killed.
    /// </summary>
    [JsonPropertyName("afterHoursStartTime")]
    public int? AfterHoursStartTime { get; set; } = 3;

    /// <summary>
    /// Feature B: Minimum delay in minutes before scheduled kill executes (for randomization).
    /// Default: 0. Combined with Max to create random delay window.
    /// </summary>
    [JsonPropertyName("blockedGameKillDelayMinutesMin")]
    public int BlockedGameKillDelayMinutesMin { get; set; } = 0;

    /// <summary>
    /// Feature B: Maximum delay in minutes before scheduled kill executes (for randomization).
    /// Default: 60. Combined with Min to create random delay window (0-60 minutes).
    /// </summary>
    [JsonPropertyName("blockedGameKillDelayMinutesMax")]
    public int BlockedGameKillDelayMinutesMax { get; set; } = 60;

    /// <summary>
    /// Feature B: Show block UI when blocked game is terminated due to after-hours enforcement.
    /// </summary>
    [JsonPropertyName("showBlockUIOnAfterHoursKill")]
    public bool ShowBlockUIOnAfterHoursKill { get; set; } = true;

    // ========== BLOCKED GAME KILL BEHAVIOR ==========

    /// <summary>
    /// When true (default): Blocked games are killed IMMEDIATELY on join (instant enforcement).
    /// When false: Blocked games are tracked only by PlaytimeTracker (respects delay settings).
    /// Use false when combining with PlaytimeLimitEnabled=true for "AFK mode" where you want
    /// playtime enforcement (2hr+) instead of instant blocking.
    /// </summary>
    [JsonPropertyName("killBlockedGameImmediately")]
    public bool KillBlockedGameImmediately { get; set; } = true;

    // ========== SILENT MODE ==========

    /// <summary>
    /// Silent mode: When enabled (default: true), no Block UI popup appears when games are killed.
    /// All terminations are silent with comprehensive logging for audit trail.
    /// When disabled, Block UI appears on kill (Feature A, Feature B, and protocol blocking).
    /// Useful for stealth mode or minimizing disruption to user experience.
    /// </summary>
    [JsonPropertyName("silentMode")]
    public bool SilentMode { get; set; } = true;

    // ========== AUTO-RESTART ON KILL ==========

    /// <summary>
    /// When game is killed due to blocking, automatically restart Roblox to home screen
    /// instead of leaving player with closed client. Provides better UX - user can immediately
    /// play another unblocked game without manually restarting Roblox.
    /// Default: true (enabled for better user experience)
    /// </summary>
    [JsonPropertyName("autoRestartOnKill")]
    public bool AutoRestartOnKill { get; set; } = true;

    /// <summary>
    /// Delay in milliseconds before restarting Roblox after kill.
    /// Allows time for process cleanup and system state normalization.
    /// Too fast: Process still using resources
    /// Too slow: User waiting too long
    /// Default: 500ms (recommended: 300-1000ms)
    /// </summary>
    [JsonPropertyName("killRestartDelayMs")]
    public int KillRestartDelayMs { get; set; } = 500;

    /// <summary>
    /// Timeout in milliseconds to wait for graceful close (WM_CLOSE) before force kill.
    /// Graceful close allows Roblox to cleanup state before termination.
    /// Default: 2000ms (2 seconds)
    /// If timeout exceeded, process is force killed immediately.
    /// </summary>
    [JsonPropertyName("gracefulCloseTimeoutMs")]
    public int GracefulCloseTimeoutMs { get; set; } = 2000;

    // ========== DAY COUNTER & SKIP-DAY SYSTEM ==========

    /// <summary>
    /// Tracks consecutive days in the 3-day enforcement cycle (1, 2, or 3).
    /// Day 1-2: Normal enforcement active
    /// Day 3: Skip day - no enforcement (both Playtime and AfterHours)
    /// After enforcement is triggered, resets to Day 1.
    /// Increments at midnight (local time).
    /// Default: 1 (enforcement enabled)
    /// </summary>
    [JsonPropertyName("consecutiveDayCounter")]
    public int ConsecutiveDayCounter { get; set; } = 1;

    /// <summary>
    /// Date of last enforcement action (ISO format: yyyy-MM-dd).
    /// Used to detect midnight boundary and increment day counter.
    /// Empty string means no enforcement has occurred yet.
    /// Default: "" (no enforcement history)
    /// </summary>
    [JsonPropertyName("lastKillDate")]
    public string LastKillDate { get; set; } = "";

    /// <summary>
    /// Day of week (0-6) when last enforcement occurred (for logging/diagnostics).
    /// 0=Sunday, 1=Monday, ..., 6=Saturday
    /// Default: 0 (diagnostic only, not used in logic)
    /// </summary>
    [JsonPropertyName("lastKillDay")]
    public int LastKillDay { get; set; } = 0;

    /// <summary>
    /// Window in minutes for randomizing after-hours start time.
    /// After-hours enforcement activates between 3:00 and 3:00+window AM.
    /// Default: 30 (3:00-3:30 AM range)
    /// Min: 1, Max: 60
    /// </summary>
    [JsonPropertyName("afterHoursRandomWindowMinutes")]
    public int AfterHoursRandomWindowMinutes { get; set; } = 30;

    /// <summary>
    /// Minimum hour for after-hours random window (local time).
    /// Default: 3 (3:00 AM)
    /// </summary>
    [JsonPropertyName("afterHoursStartHourMin")]
    public int AfterHoursStartHourMin { get; set; } = 3;

    /// <summary>
    /// Maximum hour for after-hours random window (local time).
    /// Default: 4 (ends at 3:59:59 AM, max 4:xx would cross into 4 AM)
    /// </summary>
    [JsonPropertyName("afterHoursStartHourMax")]
    public int AfterHoursStartHourMax { get; set; } = 4;

    /// <summary>
    /// Probability (0.0-1.0) of triggering after-hours kill on days 1-2.
    /// Default: 0.65 (65% chance)
    /// Day 3: Always 0 (never enforced due to skip-day logic)
    /// </summary>
    [JsonPropertyName("afterHoursKillProbability")]
    public double AfterHoursKillProbability { get; set; } = 0.65;

    /// <summary>
    /// Enable quiet hours - skip all enforcement during specified time range.
    /// Prevents enforcement from interfering with morning activities (e.g., getting ready for school).
    /// Default: true (enabled)
    /// </summary>
    [JsonPropertyName("quietHoursEnabled")]
    public bool QuietHoursEnabled { get; set; } = true;

    /// <summary>
    /// Start of quiet hours (HHmm format, local time).
    /// Example: 330 = 3:30 AM
    /// Default: 330 (3:30 AM - after after-hours enforcement window)
    /// </summary>
    [JsonPropertyName("quietHoursStart")]
    public int QuietHoursStart { get; set; } = 330;

    /// <summary>
    /// End of quiet hours (HHmm format, local time).
    /// Example: 900 = 9:00 AM
    /// Default: 900 (9:00 AM - morning preparation window)
    /// </summary>
    [JsonPropertyName("quietHoursEnd")]
    public int QuietHoursEnd { get; set; } = 900;

    // ========== TEST MODE ==========

    /// <summary>
    /// Enable test mode for time compression.
    /// When enabled, time intervals are compressed for validation.
    /// Allows testing 3-day cycle in minutes instead of days.
    /// Default: false (disabled)
    /// </summary>
    [JsonPropertyName("testModeEnabled")]
    public bool TestModeEnabled { get; set; } = false;

    /// <summary>
    /// Time compression factor for test mode.
    /// Example: factor=5 means 1 real minute = 1 simulated day
    /// This allows 3-day cycle to be validated in 3 real minutes.
    /// Default: 1 (no compression)
    /// Valid range: 1-10000
    /// </summary>
    [JsonPropertyName("testModeTimeCompressionFactor")]
    public int TestModeTimeCompressionFactor { get; set; } = 1;

    /// <summary>
    /// Check interval in milliseconds when test mode is enabled.
    /// Reduces from normal 100ms to allow faster observation of results.
    /// Example: 1000 means check every 1 second instead of every 100ms.
    /// Default: 1000 (1 second interval)
    /// </summary>
    [JsonPropertyName("testModeCheckIntervalMs")]
    public int TestModeCheckIntervalMs { get; set; } = 1000;
}

/// <summary>
/// Manages loading and saving RobloxGuard configuration.
/// </summary>
public static class ConfigManager
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard"
    );

    private static readonly string ConfigPath = Path.Combine(AppDataPath, "config.json");

    /// <summary>
    /// Gets the application data directory path.
    /// </summary>
    public static string GetAppDataPath() => AppDataPath;

    /// <summary>
    /// Gets the configuration file path.
    /// </summary>
    public static string GetConfigPath() => ConfigPath;

    /// <summary>
    /// Loads configuration from disk, or creates default if not found.
    /// </summary>
    public static RobloxGuardConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return CreateDefault();
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<RobloxGuardConfig>(json);
            if (config == null)
                return CreateDefault();

            // Sync Blocklist from BlockedGames for backward compatibility
            if (config.BlockedGames?.Count > 0)
            {
                config.Blocklist = config.BlockedGames.Select(g => g.PlaceId).ToList();
            }

            return config;
        }
        catch (Exception)
        {
            // If config is corrupted, return default
            return CreateDefault();
        }
    }

    /// <summary>
    /// Saves configuration to disk.
    /// </summary>
    public static void Save(RobloxGuardConfig config)
    {
        // Ensure directory exists
        Directory.CreateDirectory(AppDataPath);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// Creates a default configuration with sample blocked games.
    /// </summary>
    private static RobloxGuardConfig CreateDefault()
    {
        var config = new RobloxGuardConfig
        {
            OverlayEnabled = true,
            WhitelistMode = false,
            
            // Feature A: Playtime Limit
            PlaytimeLimitEnabled = false,
            PlaytimeLimitMinutes = 120,  // 2 hours
            ShowBlockUIOnPlaytimeKill = true,
            
            // Feature B: After-Hours Enforcement
            AfterHoursEnforcementEnabled = false,
            AfterHoursStartTime = 3,  // 3 AM
            BlockedGameKillDelayMinutesMin = 0,
            BlockedGameKillDelayMinutesMax = 60,
            ShowBlockUIOnAfterHoursKill = true,
            
            // Blocked Game Kill Behavior
            KillBlockedGameImmediately = true,  // Default: instant kill on blocked game join
            
            // Silent Mode
            SilentMode = true,  // No UI popup by default
            
            // Auto-Restart on Kill
            AutoRestartOnKill = true,  // Restart Roblox to home page
            KillRestartDelayMs = 500,  // Wait 500ms for cleanup
            GracefulCloseTimeoutMs = 2000,  // 2 second graceful close window
            
            // Day Counter & Skip-Day System
            ConsecutiveDayCounter = 1,
            LastKillDate = "",
            LastKillDay = 0,
            AfterHoursRandomWindowMinutes = 30,
            AfterHoursStartHourMin = 3,
            AfterHoursStartHourMax = 4,
            AfterHoursKillProbability = 0.65,
            QuietHoursEnabled = true,
            QuietHoursStart = 330,   // 3:30 AM
            QuietHoursEnd = 900,     // 9:00 AM
            
            // Test Mode
            TestModeEnabled = false,
            TestModeTimeCompressionFactor = 1,
            TestModeCheckIntervalMs = 1000,
            
            BlockedGames = new List<BlockedGame>
            {
                new BlockedGame { PlaceId = 15532962292, Name = "BRAINDEAD CONTENT DETECTED" },
                new BlockedGame { PlaceId = 1818, Name = "Adult Game" },
                new BlockedGame { PlaceId = 1, Name = "TestPlace" }
            }
        };

        // Sync blocklist from BlockedGames for IsBlocked checks
        config.Blocklist = config.BlockedGames.Select(g => g.PlaceId).ToList();

        return config;
    }

    /// <summary>
    /// Checks if a placeId is blocked based on current configuration.
    /// </summary>
    public static bool IsBlocked(long placeId, RobloxGuardConfig config)
    {
        if (config.WhitelistMode)
        {
            // Whitelist mode: block if NOT in list
            return !config.Blocklist.Contains(placeId);
        }
        else
        {
            // Blacklist mode: block if IN list
            return config.Blocklist.Contains(placeId);
        }
    }

    /// <summary>
    /// Hashes a PIN using PBKDF2.
    /// </summary>
    public static string HashPIN(string pin)
    {
        // Generate a random salt
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        
        // Hash the PIN
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin),
            salt,
            iterations: 100000,
            HashAlgorithmName.SHA256,
            outputLength: 32
        );

        // Combine salt and hash
        byte[] combined = new byte[salt.Length + hash.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(hash, 0, combined, salt.Length, hash.Length);

        // Return as base64 with prefix
        return $"pbkdf2:sha256:{Convert.ToBase64String(combined)}";
    }

    /// <summary>
    /// Verifies a PIN against a stored hash.
    /// </summary>
    public static bool VerifyPIN(string pin, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash) || !storedHash.StartsWith("pbkdf2:sha256:"))
            return false;

        try
        {
            // Extract the base64 part
            var base64 = storedHash.Substring("pbkdf2:sha256:".Length);
            byte[] combined = Convert.FromBase64String(base64);

            // Extract salt and hash
            byte[] salt = new byte[16];
            byte[] storedHashBytes = new byte[32];
            Buffer.BlockCopy(combined, 0, salt, 0, 16);
            Buffer.BlockCopy(combined, 16, storedHashBytes, 0, 32);

            // Hash the input PIN with the same salt
            byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(pin),
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256,
                outputLength: 32
            );

            // Compare
            return CryptographicOperations.FixedTimeEquals(inputHash, storedHashBytes);
        }
        catch
        {
            return false;
        }
    }
}
