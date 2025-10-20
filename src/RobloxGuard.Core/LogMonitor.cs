using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace RobloxGuard.Core;

/// <summary>
/// Monitors Roblox player logs to detect when games are being joined.
/// Blocks games by detecting placeId in log entries and terminating the process if needed.
/// Works WITHOUT admin, WITHOUT WMI, and WITHOUT command-line arguments.
/// </summary>
public class LogMonitor : IDisposable
{
    private readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Roblox",
        "logs"
    );

    private FileSystemWatcher? _fileWatcher;
    private StreamReader? _logReader;
    private string? _currentLogFile;
    private bool _isRunning;
    private readonly Action<LogBlockEvent> _onGameDetected;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private long _lastPosition = 0;
    private bool _isFirstStart = true;  // Skip old entries on startup
    
    // Track last error time to suppress repetitive errors
    private DateTime _lastErrorTime = DateTime.MinValue;
    private string? _lastErrorMessage = null;
    
    // Mutex to ensure only one LogMonitor instance runs at a time
    private static Mutex? _singletonMutex;
    private bool _mutexAcquired = false;

    // Pattern: "! Joining game 'GUID' place <PLACEID> at <IP>"
    private static readonly Regex JoiningGamePattern = new(
        @"! Joining game '[^']+' place (\d+) at",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public LogMonitor(Action<LogBlockEvent> onGameDetected)
    {
        _onGameDetected = onGameDetected;
    }

    /// <summary>
    /// Starts monitoring Roblox player logs for game joins.
    /// Only one instance can run at a time (enforced via mutex).
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        // Attempt to acquire singleton mutex
        _singletonMutex ??= new Mutex(false, "Global\\RobloxGuardLogMonitor");
        if (!_singletonMutex.WaitOne(0))
        {
            System.Console.WriteLine("[LogMonitor] Another instance is already running. Exiting.");
            return;
        }
        _mutexAcquired = true;

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start FileSystemWatcher for immediate new file detection
        SetupFileWatcher();
        
        // Start background monitoring
        _monitoringTask = MonitorLogsAsync(_cancellationTokenSource.Token);
        
        System.Console.WriteLine("[LogMonitor] Started monitoring (FileWatcher + Polling)");
    }

    /// <summary>
    /// Sets up FileSystemWatcher to detect new log files immediately.
    /// </summary>
    private void SetupFileWatcher()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
                return;

            _fileWatcher = new FileSystemWatcher(_logDirectory, "*_Player_*_last.log")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            _fileWatcher.Created += (s, e) =>
            {
                System.Console.WriteLine($"[LogMonitor-FSW] NEW FILE CREATED: {Path.GetFileName(e.Name)}");
                SwitchToNewLogFile(e.FullPath);
            };

            _fileWatcher.Changed += (s, e) =>
            {
                // Only switch if it's a new file
                if (e.FullPath != _currentLogFile)
                {
                    System.Console.WriteLine($"[LogMonitor-FSW] FILE CHANGED (potential new): {Path.GetFileName(e.Name)}");
                    SwitchToNewLogFile(e.FullPath);
                }
            };

            _fileWatcher.EnableRaisingEvents = true;
            System.Console.WriteLine("[LogMonitor] FileSystemWatcher enabled for new log detection");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[LogMonitor] Warning: FileSystemWatcher setup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Switches to a new log file and resets read position.
    /// </summary>
    private void SwitchToNewLogFile(string newLogFile)
    {
        try
        {
            if (!File.Exists(newLogFile))
                return;

            if (newLogFile == _currentLogFile)
                return;

            System.Console.WriteLine($"[LogMonitor] === SWITCHING TO NEW LOG FILE: {Path.GetFileName(newLogFile)} ===");
            _logReader?.Dispose();
            _currentLogFile = newLogFile;
            _lastPosition = 0; // Reset to read entire new file from start
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[LogMonitor] Error switching log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        try
        {
            _monitoringTask?.Wait(5000);
        }
        catch { }
        _logReader?.Dispose();
        _fileWatcher?.Dispose();
        
        // Release mutex
        if (_mutexAcquired && _singletonMutex != null)
        {
            try
            {
                _singletonMutex.ReleaseMutex();
                _mutexAcquired = false;
            }
            catch { }
        }
    }

    /// <summary>
    /// Gets the NEWEST Roblox player log file.
    /// Roblox creates a new log file each time you join a game, so we need the most recent one.
    /// </summary>
    private string? GetCurrentLogFile()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                System.Console.WriteLine($"[LogMonitor] Log directory does not exist: {_logDirectory}");
                return null;
            }

            // Look for player logs: 0.694.0.6940982_20251020T043932Z_Player_*.log
            // IMPORTANT: Sort by BOTH creation time (in filename) AND LastWriteTime
            // because Roblox creates new files for each game session
            var allFiles = Directory.GetFiles(_logDirectory, "*_Player_*_last.log");
            System.Console.WriteLine($"[LogMonitor] Found {allFiles.Length} log files");
            
            var logFiles = allFiles
                .OrderByDescending(f => 
                {
                    // Primary: File name timestamp (YYYYMMDTHHMMSSZ format)
                    // Extract and parse the timestamp from filename
                    var match = System.Text.RegularExpressions.Regex.Match(f, @"_(\d{8}T\d{6}Z)_");
                    if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMddTHHmmssZ", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                    {
                        return dt;
                    }
                    // Fallback: Use file LastWriteTime
                    return new FileInfo(f).LastWriteTime;
                })
                .FirstOrDefault();

            if (logFiles != null)
                System.Console.WriteLine($"[LogMonitor] Selected log file: {Path.GetFileName(logFiles)}");
            else
                System.Console.WriteLine($"[LogMonitor] No log files found");

            return logFiles;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[LogMonitor] Error getting current log file: {ex.Message}");
            Debug.WriteLine($"[LogMonitor] Error getting current log file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Background task to monitor log file for new game joins.
    /// </summary>
    private async Task MonitorLogsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Use FileSystemWatcher-detected file, or fall back to polling
                var logFile = _currentLogFile ?? GetCurrentLogFile();
                if (logFile == null)
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                if (!File.Exists(logFile))
                {
                    _currentLogFile = null;
                    _lastPosition = 0;
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                // Read new lines from current file
                ReadNewLinesFromFile(logFile);

                // Poll frequently (100ms) for fast blocking
                await Task.Delay(100, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogMonitor] Error in monitoring loop: {ex.Message}");
                System.Console.WriteLine($"[LogMonitor] Error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Reads new lines from the log file starting from last known position.
    /// On first start, skip all existing entries to only detect new game joins.
    /// </summary>
    private void ReadNewLinesFromFile(string logFile)
    {
        try
        {
            // Open with FileShare.ReadWrite to allow Roblox to write while we read
            using (var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
            using (var reader = new StreamReader(fileStream))
            {
                // On first start, skip all existing entries and position at end of file
                if (_isFirstStart)
                {
                    reader.BaseStream.Seek(0, SeekOrigin.End);
                    _lastPosition = reader.BaseStream.Position;
                    _isFirstStart = false;
                    System.Console.WriteLine("[LogMonitor] Startup: Skipping existing log entries, monitoring for new games only");
                    return;
                }

                reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Check for game join pattern
                    var match = JoiningGamePattern.Match(line);
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var placeId))
                    {
                        System.Console.WriteLine($"[LogMonitor] >>> DETECTED GAME JOIN: placeId={placeId}");
                        Debug.WriteLine($"[LogMonitor] Detected game join: placeId={placeId}");

                        // Load config and check if blocked
                        var config = ConfigManager.Load();
                        if (ConfigManager.IsBlocked(placeId, config))
                        {
                            Debug.WriteLine($"[LogMonitor] Game {placeId} is BLOCKED");

                            _onGameDetected(new LogBlockEvent
                            {
                                PlaceId = placeId,
                                IsBlocked = true,
                                Timestamp = DateTime.UtcNow,
                                LogLine = line
                            });

                            // Try to terminate RobloxPlayerBeta
                            TerminateRobloxProcess();
                        }
                        else
                        {
                            Debug.WriteLine($"[LogMonitor] Game {placeId} is allowed");

                            _onGameDetected(new LogBlockEvent
                            {
                                PlaceId = placeId,
                                IsBlocked = false,
                                Timestamp = DateTime.UtcNow,
                                LogLine = line
                            });
                        }
                    }
                }

                // Save position for next read
                _lastPosition = reader.BaseStream.Position;
            }
        }
        catch (FileNotFoundException)
        {
            // File was deleted, switch to next one
            _currentLogFile = null;
            _lastPosition = 0;
        }
        catch (Exception ex)
        {
            // Suppress repetitive errors (only log every 10 seconds)
            var now = DateTime.UtcNow;
            if (ex.Message != _lastErrorMessage || (now - _lastErrorTime).TotalSeconds > 10)
            {
                System.Console.WriteLine($"[LogMonitor] Error reading file: {ex.Message}");
                Debug.WriteLine($"[LogMonitor] Error reading file: {ex.Message}");
                _lastErrorTime = now;
                _lastErrorMessage = ex.Message;
            }
        }
    }

    /// <summary>
    /// Aggressively terminates all RobloxPlayerBeta process(es) immediately.
    /// Uses force kill (not graceful close) for fastest blocking.
    /// </summary>
    private void TerminateRobloxProcess()
    {
        try
        {
            var processes = Process.GetProcessesByName("RobloxPlayerBeta");
            foreach (var proc in processes)
            {
                try
                {
                    System.Console.WriteLine($"[LogMonitor] TERMINATING RobloxPlayerBeta (PID: {proc.Id})");
                    Debug.WriteLine($"[LogMonitor] FORCE TERMINATING RobloxPlayerBeta (PID: {proc.Id})");
                    // Aggressive: Kill immediately without graceful close
                    // This ensures blocked games cannot launch
                    proc.Kill(true); // Kill with child processes
                    System.Console.WriteLine($"[LogMonitor] Successfully terminated process {proc.Id}");
                    Debug.WriteLine($"[LogMonitor] Process {proc.Id} killed");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[LogMonitor] Error killing process: {ex.Message}");
                    Debug.WriteLine($"[LogMonitor] Error killing process: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[LogMonitor] Error in TerminateRobloxProcess: {ex.Message}");
            Debug.WriteLine($"[LogMonitor] Error in TerminateRobloxProcess: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
        _logReader?.Dispose();
        _fileWatcher?.Dispose();
        _singletonMutex?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for when a game is detected in logs.
/// </summary>
public class LogBlockEvent
{
    public long PlaceId { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime Timestamp { get; set; }
    public string? LogLine { get; set; }
}
