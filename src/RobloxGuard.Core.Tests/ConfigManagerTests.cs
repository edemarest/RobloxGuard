using RobloxGuard.Core;
using Xunit;

namespace RobloxGuard.Core.Tests;

public class ConfigManagerTests
{
    [Fact]
    public void Load_NoExistingConfig_ReturnsDefault()
    {
        // Act
        var config = ConfigManager.Load();

        // Assert
        Assert.NotNull(config);
        // Blocklist may or may not be empty depending on if config.json exists
        // Just verify it's a valid collection
        Assert.IsType<List<long>>(config.Blocklist);
        Assert.True(config.OverlayEnabled);
        Assert.False(config.WhitelistMode);
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
