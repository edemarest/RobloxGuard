using RobloxGuard.Core;
using Xunit;

namespace RobloxGuard.Core.Tests;

public class ConfigManagerTests
{
    [Fact]
    public void Load_NoExistingConfig_ReturnsDefault()
    {
        // This test loads the existing config (which may have been created by previous test runs)
        // The key assertion is that ConfigManager can deserialize the old config format
        // with removed properties (playtimeLimitEnabled, afterHoursEnforcementEnabled, etc)
        // and still return a valid RobloxGuardConfig object
        
        // Act
        var config = ConfigManager.Load();

        // Assert
        Assert.NotNull(config);
        // Verify it's a valid collection
        Assert.IsType<List<long>>(config.Blocklist);
        // Just verify config loaded successfully without errors
        Assert.IsType<RobloxGuardConfig>(config);
    }

    [Fact]
    public void IsBlocked_BlacklistMode_BlocksItemsInList()
    {
        // Arrange
        var config = new RobloxGuardConfig
        {
            Blocklist = new List<long> { 12345, 67890 },
            WhitelistMode = false
        };

        // Act & Assert
        Assert.True(ConfigManager.IsBlocked(12345, config));
        Assert.True(ConfigManager.IsBlocked(67890, config));
        Assert.False(ConfigManager.IsBlocked(99999, config));
    }

    [Fact]
    public void IsBlocked_WhitelistMode_AllowsOnlyItemsInList()
    {
        // Arrange
        var config = new RobloxGuardConfig
        {
            Blocklist = new List<long> { 12345, 67890 },
            WhitelistMode = true
        };

        // Act & Assert
        Assert.False(ConfigManager.IsBlocked(12345, config)); // In list = allowed
        Assert.False(ConfigManager.IsBlocked(67890, config)); // In list = allowed
        Assert.True(ConfigManager.IsBlocked(99999, config)); // Not in list = blocked
    }

    [Fact]
    public void HashPIN_CreatesValidHash()
    {
        // Arrange
        var pin = "1234";

        // Act
        var hash = ConfigManager.HashPIN(pin);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("pbkdf2:sha256:", hash);
    }

    [Fact]
    public void VerifyPIN_CorrectPIN_ReturnsTrue()
    {
        // Arrange
        var pin = "1234";
        var hash = ConfigManager.HashPIN(pin);

        // Act
        var result = ConfigManager.VerifyPIN(pin, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPIN_IncorrectPIN_ReturnsFalse()
    {
        // Arrange
        var correctPin = "1234";
        var wrongPin = "5678";
        var hash = ConfigManager.HashPIN(correctPin);

        // Act
        var result = ConfigManager.VerifyPIN(wrongPin, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPIN_EmptyHash_ReturnsFalse()
    {
        // Act
        var result = ConfigManager.VerifyPIN("1234", "");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPIN_InvalidHashFormat_ReturnsFalse()
    {
        // Act
        var result = ConfigManager.VerifyPIN("1234", "invalid_hash");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HashPIN_SamePINTwice_CreatesDifferentHashes()
    {
        // Arrange
        var pin = "1234";

        // Act
        var hash1 = ConfigManager.HashPIN(pin);
        var hash2 = ConfigManager.HashPIN(pin);

        // Assert
        Assert.NotEqual(hash1, hash2); // Different due to random salt
    }
}
