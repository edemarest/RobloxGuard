using RobloxGuard.Core;
using Xunit;
using System.Text.Json;

namespace RobloxGuard.Core.Tests;

/// <summary>
/// Tests for new configuration properties (Features A, B, Silent Mode, Restart).
/// </summary>
public class ConfigurationNewPropertiesTests
{
    private RobloxGuardConfig CreateTestConfig()
    {
        return new RobloxGuardConfig();
    }

    [Fact]
    public void NewProperties_HaveDefaultValues()
    {
        // Act
        var config = CreateTestConfig();

        // Assert: Feature A properties
        Assert.False(config.PlaytimeLimitEnabled);
        Assert.Equal(120, config.PlaytimeLimitMinutes);
        Assert.True(config.ShowBlockUIOnPlaytimeKill);

        // Assert: Feature B properties
        Assert.False(config.AfterHoursEnforcementEnabled);
        Assert.Equal(3, config.AfterHoursStartTime);
        Assert.Equal(0, config.BlockedGameKillDelayMinutesMin);
        Assert.Equal(60, config.BlockedGameKillDelayMinutesMax);
        Assert.True(config.ShowBlockUIOnAfterHoursKill);

        // Assert: Silent Mode property
        Assert.True(config.SilentMode);

        // Assert: Restart properties
        Assert.True(config.AutoRestartOnKill);
        Assert.Equal(500, config.KillRestartDelayMs);
        Assert.Equal(2000, config.GracefulCloseTimeoutMs);
    }

    [Fact]
    public void PlaytimeLimitEnabled_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.PlaytimeLimitEnabled = true;

        // Assert
        Assert.True(config.PlaytimeLimitEnabled);
    }

    [Fact]
    public void PlaytimeLimitMinutes_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.PlaytimeLimitMinutes = 180;

        // Assert
        Assert.Equal(180, config.PlaytimeLimitMinutes);
    }

    [Fact]
    public void AfterHoursEnforcementEnabled_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.AfterHoursEnforcementEnabled = true;

        // Assert
        Assert.True(config.AfterHoursEnforcementEnabled);
    }

    [Fact]
    public void AfterHoursStartTime_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.AfterHoursStartTime = 9;

        // Assert
        Assert.Equal(9, config.AfterHoursStartTime);
    }

    [Fact]
    public void BlockedGameKillDelayMinutes_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.BlockedGameKillDelayMinutesMin = 5;
        config.BlockedGameKillDelayMinutesMax = 30;

        // Assert
        Assert.Equal(5, config.BlockedGameKillDelayMinutesMin);
        Assert.Equal(30, config.BlockedGameKillDelayMinutesMax);
    }

    [Fact]
    public void SilentMode_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.SilentMode = false;

        // Assert
        Assert.False(config.SilentMode);
    }

    [Fact]
    public void AutoRestartOnKill_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.AutoRestartOnKill = false;

        // Assert
        Assert.False(config.AutoRestartOnKill);
    }

    [Fact]
    public void KillRestartDelayMs_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.KillRestartDelayMs = 1000;

        // Assert
        Assert.Equal(1000, config.KillRestartDelayMs);
    }

    [Fact]
    public void GracefulCloseTimeoutMs_CanBeSet()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        config.GracefulCloseTimeoutMs = 5000;

        // Assert
        Assert.Equal(5000, config.GracefulCloseTimeoutMs);
    }

    [Fact]
    public void SerializeDeserialize_PreservesNewProperties()
    {
        // Arrange
        var originalConfig = CreateTestConfig();
        originalConfig.PlaytimeLimitEnabled = true;
        originalConfig.PlaytimeLimitMinutes = 90;
        originalConfig.AfterHoursEnforcementEnabled = true;
        originalConfig.AfterHoursStartTime = 22;
        originalConfig.SilentMode = false;
        originalConfig.AutoRestartOnKill = false;

        // Act: Serialize to JSON
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(originalConfig, options);
        
        // Deserialize from JSON
        var deserializedConfig = JsonSerializer.Deserialize<RobloxGuardConfig>(json);

        // Assert
        Assert.NotNull(deserializedConfig);
        Assert.True(deserializedConfig.PlaytimeLimitEnabled);
        Assert.Equal(90, deserializedConfig.PlaytimeLimitMinutes);
        Assert.True(deserializedConfig.AfterHoursEnforcementEnabled);
        Assert.Equal(22, deserializedConfig.AfterHoursStartTime);
        Assert.False(deserializedConfig.SilentMode);
        Assert.False(deserializedConfig.AutoRestartOnKill);
    }

    [Fact]
    public void AllNewProperties_HaveJsonPropertyNames()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act: Get property info
        var props = typeof(RobloxGuardConfig).GetProperties();
        var newPropertyNames = new[] 
        { 
            nameof(config.PlaytimeLimitEnabled),
            nameof(config.PlaytimeLimitMinutes),
            nameof(config.ShowBlockUIOnPlaytimeKill),
            nameof(config.AfterHoursEnforcementEnabled),
            nameof(config.AfterHoursStartTime),
            nameof(config.BlockedGameKillDelayMinutesMin),
            nameof(config.BlockedGameKillDelayMinutesMax),
            nameof(config.ShowBlockUIOnAfterHoursKill),
            nameof(config.KillBlockedGameImmediately),
            nameof(config.SilentMode),
            nameof(config.AutoRestartOnKill),
            nameof(config.KillRestartDelayMs),
            nameof(config.GracefulCloseTimeoutMs)
        };

        // Assert: Each property has JsonPropertyName attribute
        foreach (var propName in newPropertyNames)
        {
            var prop = Array.Find(props, p => p.Name == propName);
            Assert.NotNull(prop);
            var jsonAttr = prop!.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), false);
            Assert.NotEmpty(jsonAttr);
        }
    }

    [Fact]
    public void DefaultValues_AreReasonable()
    {
        // Arrange
        var config = CreateTestConfig();

        // Assert: All defaults make sense
        Assert.False(config.PlaytimeLimitEnabled);
        Assert.InRange(config.PlaytimeLimitMinutes, 60, 240);
        Assert.False(config.AfterHoursEnforcementEnabled);
        Assert.InRange(config.AfterHoursStartTime!.Value, 0, 23);
        Assert.InRange(config.BlockedGameKillDelayMinutesMin, 0, 60);
        Assert.InRange(config.BlockedGameKillDelayMinutesMax, 0, 120);
        Assert.True(config.BlockedGameKillDelayMinutesMax >= config.BlockedGameKillDelayMinutesMin);
        Assert.True(config.KillBlockedGameImmediately);  // Default: instant kill enabled
        Assert.True(config.SilentMode);
        Assert.True(config.AutoRestartOnKill);
        Assert.InRange(config.KillRestartDelayMs, 100, 2000);
        Assert.InRange(config.GracefulCloseTimeoutMs, 500, 5000);
    }

    [Fact]
    public void MultipleInstances_AreIndependent()
    {
        // Arrange
        var config1 = CreateTestConfig();
        var config2 = CreateTestConfig();

        // Act
        config1.PlaytimeLimitEnabled = true;
        config1.PlaytimeLimitMinutes = 200;

        // Assert
        Assert.False(config2.PlaytimeLimitEnabled);
        Assert.Equal(120, config2.PlaytimeLimitMinutes);
    }

    [Fact]
    public void KillBlockedGameImmediately_CanBeDisabledForAFKMode()
    {
        // Arrange: For AFK mode (playtime-based), we want killBlockedGameImmediately=false
        var config = CreateTestConfig();
        config.PlaytimeLimitEnabled = true;
        config.PlaytimeLimitMinutes = 120;
        config.KillBlockedGameImmediately = false;  // Don't kill immediately; let PlaytimeTracker handle it

        // Act & Assert
        Assert.True(config.PlaytimeLimitEnabled);
        Assert.False(config.KillBlockedGameImmediately);
        Assert.Equal(120, config.PlaytimeLimitMinutes);
    }

    [Fact]
    public void KillBlockedGameImmediately_DefaultIsTrue()
    {
        // Arrange: For normal (non-AFK) mode, default should be instant kill
        var config = CreateTestConfig();

        // Assert: Default is true (backward compatible)
        Assert.True(config.KillBlockedGameImmediately);
    }

    [Fact]
    public void ConfigCanBeSerialized_ToValidJson()
    {
        // Arrange
        var config = CreateTestConfig();
        config.PlaytimeLimitEnabled = true;
        config.AfterHoursEnforcementEnabled = true;
        config.KillBlockedGameImmediately = false;

        // Act
        var json = JsonSerializer.Serialize(config);

        // Assert
        Assert.NotEmpty(json);
        Assert.Contains("playtimeLimitEnabled", json);
        Assert.Contains("afterHoursEnforcementEnabled", json);
        Assert.Contains("killBlockedGameImmediately", json);
        Assert.Contains("silentMode", json);
        Assert.Contains("autoRestartOnKill", json);
    }
}
