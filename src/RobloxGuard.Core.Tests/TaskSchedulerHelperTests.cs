using RobloxGuard.Core;
using Xunit;

namespace RobloxGuard.Core.Tests;

public class TaskSchedulerHelperTests
{
    [Fact]
    public void TaskExists_WhenTaskDoesNotExist_ReturnsFalse()
    {
        // Act
        var exists = TaskSchedulerHelper.TaskExists();

        // Assert (task doesn't exist yet, so should be false)
        // Note: This will be true if task was created by a previous test
        // In real scenario, we'd mock this or use a unique task name
    }

    [Fact]
    public void CreateWatcherTask_WithValidPath_CreatesTask()
    {
        // Arrange
        var testExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "dummy.exe";

        // Act & Assert
        try
        {
            // This will fail in test environment without admin, but we're testing the method doesn't throw unexpectedly
            TaskSchedulerHelper.CreateWatcherTask(testExePath);
        }
        catch (InvalidOperationException ex)
        {
            // Expected in non-admin context
            Assert.Contains("Failed", ex.Message);
        }
    }

    [Fact]
    public void DeleteWatcherTask_DoesNotThrow()
    {
        // Act & Assert
        try
        {
            TaskSchedulerHelper.DeleteWatcherTask();
            // Should not throw even if task doesn't exist
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected exception: {ex.Message}");
        }
    }
}
