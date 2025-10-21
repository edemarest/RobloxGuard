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
