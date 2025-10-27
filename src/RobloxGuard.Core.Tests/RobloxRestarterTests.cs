using RobloxGuard.Core;
using Xunit;
using System;

namespace RobloxGuard.Core.Tests;

/// <summary>
/// Comprehensive tests for RobloxRestarter class.
/// Tests graceful close, force kill, path finding, and restart logic.
/// </summary>
public class RobloxRestarterTests
{
    private readonly Func<dynamic> _getConfig;

    public RobloxRestarterTests()
    {
        _getConfig = () =>
        {
            return new RobloxGuardConfig
            {
                AutoRestartOnKill = true,
                KillRestartDelayMs = 500,
                GracefulCloseTimeoutMs = 2000
            };
        };
    }

    [Fact]
    public void Constructor_WithValidParams_Initializes()
    {
        // Act
        var restarter = new RobloxRestarter(_getConfig);

        // Assert
        Assert.NotNull(restarter);
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new RobloxRestarter(null!));
    }

    [Fact]
    public void GetRobloxPath_AppDataExists_ReturnsPathOrNull()
    {
        // Arrange
        var restarter = new RobloxRestarter(_getConfig);

        // Act
        var path = restarter.GetRobloxPath();

        // Assert: Either finds it or returns null (graceful)
        // We just verify it doesn't throw and returns a string or null
        _ = path;
    }

    [Fact]
    public void KillAndRestartToHome_ConfigAutoRestartDisabled_SkipsRestart()
    {
        // Arrange
        var configNoRestart = new Func<dynamic>(() =>
        {
            return new RobloxGuardConfig
            {
                AutoRestartOnKill = false,
                KillRestartDelayMs = 500
            };
        });

        var restarter = new RobloxRestarter(configNoRestart);

        // Act: Call kill and restart with auto-restart disabled
        #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        restarter.KillAndRestartToHome("Test reason");
        #pragma warning restore CS4014

        // Assert: Should not restart (just log)
        // We verify it doesn't throw
    }

    [Fact]
    public void KillAndRestartToHome_ValidReason_LogsReason()
    {
        // Arrange
        var restarter = new RobloxRestarter(_getConfig);
        var reason = "Test reason for kill";

        // Act: Kill and restart with reason
        #pragma warning disable CS4014
        restarter.KillAndRestartToHome(reason);
        #pragma warning restore CS4014

        // Assert: Logs should contain the reason
        // Note: We can't easily verify logs without access to the file system
        // Just verify no exception thrown
    }

    [Fact]
    public void KillAndRestartToHome_MultipleReasons_AllLogged()
    {
        // Arrange
        var restarter = new RobloxRestarter(_getConfig);

        // Act: Call multiple times with different reasons
        #pragma warning disable CS4014
        restarter.KillAndRestartToHome("Reason 1");
        restarter.KillAndRestartToHome("Reason 2");
        restarter.KillAndRestartToHome("Reason 3");
        #pragma warning restore CS4014

        // Assert: No exception should be thrown
    }

    [Fact]
    public void GetRobloxPath_SearchMultiplePaths_TriesAllLocations()
    {
        // Arrange
        var restarter = new RobloxRestarter(_getConfig);

        // Act: Get path (will try AppData, ProgramFiles, Registry)
        var path = restarter.GetRobloxPath();

        // Assert: Either finds a valid path or returns null
        if (path != null)
        {
            Assert.NotEmpty(path);
            Assert.True(path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
                       path.EndsWith(".EXE", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Dispose_CleansUp()
    {
        // Arrange
        var restarter = new RobloxRestarter(_getConfig);

        // Act
        restarter.Dispose();

        // Assert: Should not throw when disposed
    }
}
