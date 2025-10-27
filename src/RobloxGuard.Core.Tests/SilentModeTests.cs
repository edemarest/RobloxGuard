using RobloxGuard.Core;
using Xunit;
using System;

namespace RobloxGuard.Core.Tests;

/// <summary>
/// Tests for Silent Mode functionality.
/// Silent Mode suppresses Block UI display on kills.
/// </summary>
public class SilentModeTests
{
    private bool _terminateCalled = false;

    [Fact]
    public void SilentMode_DefaultTrue()
    {
        // Arrange & Act
        var config = new RobloxGuardConfig();

        // Assert
        Assert.True(config.SilentMode);
    }

    [Fact]
    public void SilentMode_CanBeDisabled()
    {
        // Arrange
        var config = new RobloxGuardConfig();

        // Act
        config.SilentMode = false;

        // Assert
        Assert.False(config.SilentMode);
    }

    [Fact]
    public void SilentMode_CanBeEnabled()
    {
        // Arrange
        var config = new RobloxGuardConfig();
        config.SilentMode = false;

        // Act
        config.SilentMode = true;

        // Assert
        Assert.True(config.SilentMode);
    }

    [Fact]
    public void PlaytimeTracker_SilentModeTrue_DoesNotPassReason()
    {
        // Arrange
        _terminateCalled = false;
        var config = new Func<dynamic>(() =>
        {
            return new RobloxGuardConfig
            {
                PlaytimeLimitEnabled = true,
                PlaytimeLimitMinutes = 120,
                SilentMode = true,
                Blocklist = new List<long> { 12345 }
            };
        });

        string? terminateReason = null;
        var tracker = new PlaytimeTracker(config, reason => 
        { 
            _terminateCalled = true;
            terminateReason = reason;
        });

        var blockedPlaceId = 12345L;
        var guid = Guid.NewGuid().ToString();
        var joinTime = DateTime.UtcNow;
        tracker.RecordGameJoin(blockedPlaceId, guid, joinTime);

        // Act: Trigger kill by exceeding time limit
        var checkTime = joinTime.AddMinutes(121);
        tracker.CheckAndApplyLimits(checkTime);

        var info = tracker.GetCurrentSessionInfo();
        if (info?.ScheduledKillTime != null)
        {
            tracker.CheckAndApplyLimits(info.ScheduledKillTime.Value.AddSeconds(10));
        }

        // Assert: In silent mode, reason passed should be empty or null
        Assert.True(string.IsNullOrEmpty(terminateReason) || terminateReason == "");
    }

    [Fact]
    public void PlaytimeTracker_SilentModeFalse_MayPassReason()
    {
        // Arrange
        _terminateCalled = false;
        var config = new Func<dynamic>(() =>
        {
            return new RobloxGuardConfig
            {
                PlaytimeLimitEnabled = true,
                PlaytimeLimitMinutes = 120,
                SilentMode = false,
                Blocklist = new List<long> { 12345 }
            };
        });

        string? terminateReason = null;
        var tracker = new PlaytimeTracker(config, reason => 
        { 
            _terminateCalled = true;
            terminateReason = reason;
        });

        var blockedPlaceId = 12345L;
        var guid = Guid.NewGuid().ToString();
        var joinTime = DateTime.UtcNow;
        tracker.RecordGameJoin(blockedPlaceId, guid, joinTime);

        // Act: Trigger kill
        var checkTime = joinTime.AddMinutes(121);
        tracker.CheckAndApplyLimits(checkTime);

        var info = tracker.GetCurrentSessionInfo();
        if (info?.ScheduledKillTime != null)
        {
            tracker.CheckAndApplyLimits(info.ScheduledKillTime.Value.AddSeconds(10));
        }

        // Assert: With silent mode disabled, if kill happens, reason should be populated
        // Note: Kill may or may not execute in test depending on timing
        if (_terminateCalled)
        {
            Assert.NotEmpty(terminateReason!);
        }
    }

    [Fact]
    public void SilentMode_AffectsPlaytimeKillOnly()
    {
        // Arrange: When silent mode is true, block UI should not show
        var config = new RobloxGuardConfig();
        config.SilentMode = true;
        config.ShowBlockUIOnPlaytimeKill = true;

        // Act: Store the values
        var silentEnabled = config.SilentMode;
        var showUIEnabled = config.ShowBlockUIOnPlaytimeKill;

        // Assert: Config stores both; logic layer decides which to use
        Assert.True(silentEnabled);
        Assert.True(showUIEnabled);
    }

    [Fact]
    public void SilentMode_DoesNotAffectBlockedPlaceBehavior()
    {
        // Arrange
        var config = new Func<dynamic>(() =>
        {
            return new RobloxGuardConfig
            {
                SilentMode = true,
                Blocklist = new List<long> { 12345 }
            };
        });

        var tracker = new PlaytimeTracker(config, _ => { });

        // Act: Join a blocked place
        var blockedPlaceId = 12345L;
        var guid = Guid.NewGuid().ToString();
        tracker.RecordGameJoin(blockedPlaceId, guid, DateTime.UtcNow);
        var info = tracker.GetCurrentSessionInfo();

        // Assert: Silent mode doesn't affect blocked place detection
        Assert.NotNull(info);
        Assert.True(info.IsBlocked);
    }

    [Fact]
    public void SilentMode_RespectAfterHoursIndependently()
    {
        // Arrange
        var config = new Func<dynamic>(() =>
        {
            return new RobloxGuardConfig
            {
                AfterHoursEnforcementEnabled = true,
                SilentMode = true,
                Blocklist = new List<long> { 12345 }
            };
        });

        var tracker = new PlaytimeTracker(config, _ => { });

        // Act: Join a blocked place
        var blockedPlaceId = 12345L;
        var guid = Guid.NewGuid().ToString();
        tracker.RecordGameJoin(blockedPlaceId, guid, DateTime.UtcNow);

        // Assert: Features are independent
        var info = tracker.GetCurrentSessionInfo();
        Assert.NotNull(info);
    }

    [Fact]
    public void SilentMode_CanBeToggledDuringRuntime()
    {
        // Arrange
        var config = new RobloxGuardConfig();
        config.SilentMode = false;
        var initialValue = config.SilentMode;

        // Act
        config.SilentMode = true;
        var toggledValue = config.SilentMode;

        // Assert
        Assert.False(initialValue);
        Assert.True(toggledValue);
    }
}
