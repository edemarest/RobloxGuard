using System.Diagnostics;
using System.Management;

namespace RobloxGuard.Core;

/// <summary>
/// Watches for RobloxPlayerBeta.exe process starts and checks for blocked placeIds.
/// </summary>
public class ProcessWatcher : IDisposable
{
    private ManagementEventWatcher? _watcher;
    private readonly Action<ProcessBlockEvent> _onProcessBlocked;
    private bool _isRunning;

    public ProcessWatcher(Action<ProcessBlockEvent> onProcessBlocked)
    {
        _onProcessBlocked = onProcessBlocked;
    }

    /// <summary>
    /// Starts watching for Roblox process starts.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = 'RobloxPlayerBeta.exe'");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnProcessStarted;
            _watcher.Start();
            _isRunning = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to start process watcher. Make sure you have WMI permissions.", ex);
        }
    }

    /// <summary>
    /// Stops watching for processes.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _watcher?.Stop();
        _isRunning = false;
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            
            // Small delay to let process initialize
            Thread.Sleep(100);

            // Get process info
            var process = Process.GetProcessById(processId);
            var commandLine = GetProcessCommandLine(processId);

            if (string.IsNullOrEmpty(commandLine))
                return;

            // Parse placeId from command line
            var placeId = PlaceIdParser.Extract(commandLine);
            if (!placeId.HasValue)
                return;

            // Load config and check if blocked
            var config = ConfigManager.Load();
            if (ConfigManager.IsBlocked(placeId.Value, config))
            {
                // Notify about block
                _onProcessBlocked(new ProcessBlockEvent
                {
                    ProcessId = processId,
                    Process = process,
                    PlaceId = placeId.Value,
                    CommandLine = commandLine
                });
            }
        }
        catch
        {
            // Process may have exited already, ignore
        }
    }

    /// <summary>
    /// Gets the command line of a process using WMI.
    /// </summary>
    private static string? GetProcessCommandLine(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            
            using var results = searcher.Get();
            foreach (var obj in results)
            {
                return obj["CommandLine"]?.ToString();
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    /// <summary>
    /// Attempts to gracefully close a process, then force kills if needed.
    /// </summary>
    public static void BlockProcess(Process process)
    {
        if (process.HasExited)
            return;

        try
        {
            // Try graceful close first
            process.CloseMainWindow();
            
            // Wait up to 700ms for graceful close
            if (!process.WaitForExit(700))
            {
                // Force kill if still running
                process.Kill();
            }
        }
        catch
        {
            // Process may have exited, ignore
        }
    }

    public void Dispose()
    {
        Stop();
        _watcher?.Dispose();
    }
}

/// <summary>
/// Event data when a blocked process is detected.
/// </summary>
public class ProcessBlockEvent
{
    public int ProcessId { get; set; }
    public Process? Process { get; set; }
    public long PlaceId { get; set; }
    public string CommandLine { get; set; } = string.Empty;
}
