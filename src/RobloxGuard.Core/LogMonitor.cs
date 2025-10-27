using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.Versioning;

namespace RobloxGuard.Core;

/// <summary>
/// Monitors Roblox player logs to detect when games are being joined.
/// Blocks games by detecting placeId in log entries and terminating the process if needed.
/// Works WITHOUT admin, WITHOUT WMI, and WITHOUT command-line arguments.
/// </summary>
[SupportedOSPlatform("windows")]
public class LogMonitor : IDisposable
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "launcher.log"
    );

    private readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Roblox",
        "logs"
    );

    private FileSystemWatcher? _fileWatcher;
    private StreamReader? _logReader;
    private string? _currentLogFile;
    private string? _lastLoggedFile;  // Track last logged file to avoid spam
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

    // Pattern: Extract GUID from join line for session tracking
    // "! Joining game 'GUID' place <PLACEID> at <IP>"
    private static readonly Regex JoiningGameGuidPattern = new(
        @"! Joining game '([^']+)' place",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // Pattern: Game exit detection
    // Matches "Client:Disconnect" or "Disconnection Notification"
    private static readonly Regex GameExitPattern = new(
        @"(?:Client:Disconnect|Disconnection Notification)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // Pattern: Timestamp extraction from log line (ISO 8601 UTC format)
    // Example: "[2025-10-26T03:29:23.303Z] ..."
    private static readonly Regex TimestampPattern = new(
        @"\[(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // PlaytimeTracker for Feature A (playtime limit) and Feature B (after-hours)
    private PlaytimeTracker? _playtimeTracker;

    // RobloxRestarter for graceful kill + auto-restart to home screen
    private RobloxRestarter? _restarter;

    public LogMonitor(Action<LogBlockEvent> onGameDetected)
    {
        _onGameDetected = onGameDetected;
    }

    /// <summary>
    /// Logs to file for debugging in WinExe mode where console is not available.
    /// </summary>
    private static void LogToFile(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}Z] {message}\n");
        }
        catch { }
    }

    /// <summary>
    /// Tries to recover a session by scanning recent Roblox logs.
    /// Looks for a recent game join with no corresponding exit.
    /// Handles case where monitor crashed before SaveSession was called.
    /// </summary>
    private SessionStateManager.SessionState? TryBackfillSessionFromRecentLogs()
    {
        try
        {
            // Safety: Only backfill if RobloxPlayerBeta is actively running NOW
            var robloxProcess = System.Diagnostics.Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
            if (robloxProcess == null)
            {
                LogToFile("[LogMonitor.TryBackfillSessionFromRecentLogs] RobloxPlayerBeta not running - no backfill needed");
                return null;
            }

            // Look back max 5 minutes for recent game joins
            var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
            if (!Directory.Exists(logsDir))
            {
                LogToFile("[LogMonitor.TryBackfillSessionFromRecentLogs] Roblox logs dir not found");
                return null;
            }

            // Get the most recent log file
            var latestLog = new DirectoryInfo(logsDir)
                .GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestLog == null)
            {
                LogToFile("[LogMonitor.TryBackfillSessionFromRecentLogs] No Roblox logs found");
                return null;
            }

            // Read file with shared access (RobloxPlayerBeta is still writing to it)
            string[] lines;
            try
            {
                using (var fs = new FileStream(latestLog.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    var content = reader.ReadToEnd();
                    lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                }
            }
            catch (IOException ex)
            {
                LogToFile($"[LogMonitor.TryBackfillSessionFromRecentLogs] Cannot read log file (still locked, retrying next time): {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogToFile($"[LogMonitor.TryBackfillSessionFromRecentLogs] Error reading log file: {ex.GetType().Name}: {ex.Message}");
                return null;
            }

            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

            // Scan for recent game joins and exits (reverse order to find most recent)
            long? lastPlaceId = null;
            DateTime? lastJoinTime = null;

            // Parse log lines in reverse to find most recent game join
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                
                // Look for game join: placeid:XXXXX in FLog::GameJoinLoadTime
                if (line.Contains("placeid:") && line.Contains("GameJoinLoadTime"))
                {
                    var match = Regex.Match(line, @"placeid:(\d+)");
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var placeId))
                    {
                        // Extract timestamp from start of line (ISO format: YYYY-MM-DDTHH:MM:SS.FFFZ)
                        // Roblox logs use format: 2025-10-26T15:12:30.923Z,653.923950,... (24 characters for ISO part)
                        // The Z suffix indicates UTC timezone, so we parse as UTC and verify Kind
                        if (line.Length >= 24)
                        {
                            var timeStr = line.Substring(0, 24);
                            if (DateTime.TryParseExact(
                                timeStr,
                                "yyyy-MM-ddTHH:mm:ss.fffZ",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.RoundtripKind,  // Preserves Z to mean UTC
                                out var ts))
                            {
                                // Ensure parsed DateTime has Utc Kind (RoundtripKind should handle this, but be explicit)
                                if (ts.Kind != DateTimeKind.Utc)
                                {
                                    ts = DateTime.SpecifyKind(ts, DateTimeKind.Utc);
                                }
                                lastPlaceId = placeId;
                                lastJoinTime = ts;
                                var now = DateTime.UtcNow;
                                var elapsed = now - ts;
                                LogToFile($"[LogMonitor.TryBackfillSessionFromRecentLogs] Found game join: placeId={placeId}");
                                LogToFile($"  Raw timestamp string: '{timeStr}'");
                                LogToFile($"  Parsed ts: {ts:yyyy-MM-dd HH:mm:ss.fff}Z (Kind={ts.Kind})");
                                LogToFile($"  Current UtcNow: {now:yyyy-MM-dd HH:mm:ss.fff}Z (Kind={now.Kind})");
                                LogToFile($"  Elapsed: {elapsed.TotalSeconds:F1}sec = {elapsed.TotalMinutes:F1}min");
                                break;  // Found most recent join
                            }
                            else
                            {
                                LogToFile($"[LogMonitor.TryBackfillSessionFromRecentLogs] Failed to parse timestamp: '{timeStr}'");
                            }
                        }
                    }
                }
            }

            // If no recent join found, nothing to backfill
            if (lastPlaceId == null || lastJoinTime == null)
            {
                LogToFile("[LogMonitor.TryBackfillSessionFromRecentLogs] No recent game join found in logs");
                return null;
            }

            // If too old (>5 min), don't backfill (user may have left already)
            if (lastJoinTime < fiveMinutesAgo)
            {
                LogToFile($"[LogMonitor.TryBackfillSessionFromRecentLogs] Game join too old ({(DateTime.UtcNow - lastJoinTime.Value).TotalMinutes:F1}min) - skipping");
                return null;
            }

            // Passed all checks - create synthetic session
            LogToFile($"[LogMonitor.TryBackfillSessionFromRecentLogs] ✓ Creating synthetic session: placeId={lastPlaceId}, joinTime={lastJoinTime:HH:mm:ss}Z");

            return new SessionStateManager.SessionState
            {
                PlaceId = lastPlaceId.Value,
                SessionGuid = Guid.NewGuid().ToString(),
                JoinTimeUtc = lastJoinTime.Value,
                LastHeartbeatUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.TryBackfillSessionFromRecentLogs] ERROR: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Starts monitoring Roblox player logs for game joins.
    /// Only one instance can run at a time (enforced via mutex).
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        // Create PID lockfile to signal we're running
        // This replaces the old mutex-based approach with a more reliable lockfile
        try
        {
            LogToFile("[LogMonitor.Start] Creating PID lockfile...");
            PidLockHelper.CreateLock();
            LogToFile("[LogMonitor.Start] ✓ PID lockfile created successfully");
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.Start] ⚠ Failed to create PID lockfile: {ex.Message}");
            System.Console.WriteLine($"[LogMonitor] ERROR: Could not create lockfile: {ex.Message}");
            Environment.Exit(1);
            return;
        }

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        LogToFile("[LogMonitor.Start] Initializing log monitor");

        // Initialize RobloxRestarter for graceful kill + auto-restart functionality
        try
        {
            _restarter = new RobloxRestarter(
                getConfig: () => ConfigManager.Load()
            );
            LogToFile("[LogMonitor.Start] RobloxRestarter initialized successfully");
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.Start] WARNING: Failed to initialize RobloxRestarter: {ex.Message}");
            _restarter = null;
        }

        // Initialize PlaytimeTracker for Feature A (playtime limit) and Feature B (after-hours)
        try
        {
            _playtimeTracker = new PlaytimeTracker(
                getConfig: () => ConfigManager.Load(),
                onTerminate: (reason) =>
                {
                    LogToFile($"[LogMonitor.PlaytimeTracker] KILL SCHEDULED: {reason}");
                    System.Console.WriteLine($"[LogMonitor] PlaytimeTracker initiating process termination: {reason}");
                    // Use graceful kill + auto-restart if possible
                    KillAndRestartToHome(reason);
                },
                onDiagnostic: (message) =>
                {
                    LogToFile($"[LogMonitor.PlaytimeTracker] {message}");
                }
            );
            LogToFile("[LogMonitor.Start] PlaytimeTracker initialized successfully");

            // Try to resume from a persisted session (crash recovery)
            var persistedSession = SessionStateManager.LoadActiveSession();
            if (persistedSession != null)
            {
                LogToFile($"[LogMonitor.Start] Resuming persisted session: placeId={persistedSession.PlaceId}, elapsed={persistedSession.ElapsedTime.TotalMinutes:F1}min");
                _playtimeTracker.ResumeSession(persistedSession);
                _isFirstStart = false;  // Don't skip logs, we're resuming a session
            }
            else
            {
                // No persisted session. Try backfilling from recent Roblox logs
                // (handles case where monitor crashed before SaveSession was called)
                var backsession = TryBackfillSessionFromRecentLogs();
                if (backsession != null)
                {
                    LogToFile($"[LogMonitor.Start] Backfilled session from recent logs: placeId={backsession.PlaceId}, elapsed={backsession.ElapsedTime.TotalMinutes:F1}min");
                    _playtimeTracker.ResumeSession(backsession);
                    _isFirstStart = false;  // Don't skip logs, we're resuming a session
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.Start] WARNING: Failed to initialize PlaytimeTracker: {ex.Message}");
            _playtimeTracker = null;
        }
        
        // Start FileSystemWatcher for immediate new file detection
        SetupFileWatcher();
        
        // Start background monitoring
        _monitoringTask = MonitorLogsAsync(_cancellationTokenSource.Token);
        
        LogToFile("[LogMonitor.Start] Monitor task started");
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
    /// On first start, preserves position to skip old entries.
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
            
            // On first start, don't reset position - let ReadNewLinesFromFile handle it
            if (!_isFirstStart)
            {
                _lastPosition = 0; // Reset to read entire new file from start
            }
            // If _isFirstStart is true, keep _lastPosition = 0 but ReadNewLinesFromFile will
            // skip to EOF, so old entries won't be processed
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
            
            var logFiles = allFiles
                .OrderByDescending(f => 
                {
                    // Primary: File name timestamp (YYYYMMDTHHMMSSZ format)
                    // Extract and parse the timestamp from filename
                    var match = System.Text.RegularExpressions.Regex.Match(f, @"_(\d{8}T\d{6}Z)_");
                    if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMddTHHmmssZ", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        System.Globalization.DateTimeStyles.RoundtripKind, out var dt))  // RoundtripKind ensures Kind=Utc
                    {
                        // Ensure Kind is UTC (defensive)
                        if (dt.Kind != DateTimeKind.Utc)
                            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        return dt;
                    }
                    // Fallback: Use file LastWriteTime (convert to UTC if local)
                    var fileTime = new FileInfo(f).LastWriteTime;
                    if (fileTime.Kind == DateTimeKind.Local)
                        fileTime = fileTime.ToUniversalTime();
                    return fileTime;
                })
                .FirstOrDefault();

            // Only log if file changed (avoid spam)
            if (logFiles != null && logFiles != _lastLoggedFile)
            {
                System.Console.WriteLine($"[LogMonitor] Monitoring: {Path.GetFileName(logFiles)}");
                _lastLoggedFile = logFiles;
            }

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
    /// Also polls for new log files to detect game launches.
    /// Updates heartbeat periodically to signal the monitor is alive.
    /// </summary>
    private async Task MonitorLogsAsync(CancellationToken cancellationToken)
    {
        int pollCounter = 0;
        int heartbeatCounter = 0;
        
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Update heartbeat every 10 iterations (~1 second)
                heartbeatCounter++;
                if (heartbeatCounter >= 10)
                {
                    heartbeatCounter = 0;
                    HeartbeatHelper.UpdateHeartbeat();
                    
                    // Update session heartbeat while game is active (prove session is still alive)
                    if (_playtimeTracker?.HasActiveSession() == true)
                    {
                        SessionStateManager.UpdateHeartbeat();
                    }
                }

                // Periodically check for NEW log files (every 500ms poll = every 5 iterations = 500ms)
                pollCounter++;
                if (pollCounter >= 5)
                {
                    pollCounter = 0;
                    var newestLogFile = GetCurrentLogFile();
                    if (newestLogFile != null && newestLogFile != _currentLogFile)
                    {
                        System.Console.WriteLine($"[LogMonitor] [POLLING] Detected newer log file!");
                        SwitchToNewLogFile(newestLogFile);
                    }
                }
                
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

                // Regular PlaytimeTracker checks (even between log updates)
                if (_playtimeTracker != null)
                {
                    _playtimeTracker.CheckAndApplyLimits(DateTime.UtcNow);
                }

                // Poll frequently (100ms) for fast blocking
                await Task.Delay(100, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogToFile($"[LogMonitor.MonitorLogsAsync] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                LogToFile($"[LogMonitor.MonitorLogsAsync] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogToFile($"[LogMonitor.MonitorLogsAsync] InnerException: {ex.InnerException.Message}");
                }
                Debug.WriteLine($"[LogMonitor] Error in monitoring loop: {ex.Message}");
                System.Console.WriteLine($"[LogMonitor] Error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }

        // Clear heartbeat on shutdown so watchdog knows we're gone
        HeartbeatHelper.ClearHeartbeat();
    }

    /// <summary>
    /// Reads new lines from the log file starting from last known position.
    /// On startup with no active session, skip all existing entries to only detect NEW game joins.
    /// If resuming a session, don't skip - we need to track playtime from the join time.
    /// </summary>
    private void ReadNewLinesFromFile(string logFile)
    {
        try
        {
            // Open with FileShare.ReadWrite to allow Roblox to write while we read
            using (var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
            using (var reader = new StreamReader(fileStream))
            {
                // On first start with NO active session, skip all existing entries and position at end of file
                // (This is the normal startup case with no crash recovery)
                if (_isFirstStart && !_playtimeTracker?.HasActiveSession() == true)
                {
                    reader.BaseStream.Seek(0, SeekOrigin.End);
                    _lastPosition = reader.BaseStream.Position;
                    _isFirstStart = false;
                    System.Console.WriteLine("[LogMonitor] Startup (no active session): Skipping existing log entries, monitoring for new games only");
                    return;
                }

                // First time reading after startup with active session - don't skip
                // (Session was resumed from crash recovery)
                if (_isFirstStart)
                {
                    _isFirstStart = false;
                    System.Console.WriteLine("[LogMonitor] Startup (active session resumed): Reading logs from current position");
                    return;
                }

                // If _lastPosition is 0 and we haven't just started, this is a NEW file
                // Skip to EOF to avoid processing old entries from the new file
                if (_lastPosition == 0)
                {
                    reader.BaseStream.Seek(0, SeekOrigin.End);
                    _lastPosition = reader.BaseStream.Position;
                    System.Console.WriteLine("[LogMonitor] New log file detected, skipping existing entries");
                    return;
                }

                reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Extract timestamp from log line for PlaytimeTracker
                    var timestampUtc = ExtractTimestampFromLogLine(line);

                    // ========== FEATURE B: After-hours enforcement + Feature A: Playtime limit ==========
                    // Check for game join to record in PlaytimeTracker
                    var joinMatch = JoiningGamePattern.Match(line);
                    if (joinMatch.Success && long.TryParse(joinMatch.Groups[1].Value, out var joinPlaceId))
                    {
                        // Extract session GUID for session tracking
                        var guidMatch = JoiningGameGuidPattern.Match(line);
                        var sessionGuid = guidMatch.Success ? guidMatch.Groups[1].Value : Guid.NewGuid().ToString();

                        LogToFile($"[LogMonitor.PlaytimeTracker] Game join detected: placeId={joinPlaceId}, guid={sessionGuid.Substring(0, 8)}..., time={timestampUtc:HH:mm:ss}Z");
                        _playtimeTracker?.RecordGameJoin(joinPlaceId, sessionGuid, timestampUtc);
                    }

                    // Check for game exit to clear PlaytimeTracker session
                    if (GameExitPattern.IsMatch(line))
                    {
                        LogToFile($"[LogMonitor.PlaytimeTracker] Game exit detected: time={timestampUtc:HH:mm:ss}Z");
                        _playtimeTracker?.RecordGameExit(timestampUtc);
                    }

                    // Check if any scheduled kills need to be executed
                    _playtimeTracker?.CheckAndApplyLimits(timestampUtc);

                    // ========== ORIGINAL PROTOCOL BLOCKING (backward compatibility) ==========
                    // Check for game join pattern
                    var match = JoiningGamePattern.Match(line);
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var placeId))
                    {
                        LogToFile($"[LogMonitor] DETECTED GAME JOIN: placeId={placeId}");
                        System.Console.WriteLine($"[LogMonitor] >>> DETECTED GAME JOIN: placeId={placeId}");
                        Debug.WriteLine($"[LogMonitor] Detected game join: placeId={placeId}");

                        // Load config and check if blocked
                        var config = ConfigManager.Load();
                        if (ConfigManager.IsBlocked(placeId, config))
                        {
                            LogToFile($"[LogMonitor] Game {placeId} IS BLOCKED");
                            LogToFile($"[LogMonitor] Silent Mode: {config.SilentMode}");
                            LogToFile($"[LogMonitor] KillBlockedGameImmediately: {config.KillBlockedGameImmediately}");
                            Debug.WriteLine($"[LogMonitor] Game {placeId} is BLOCKED");

                            _onGameDetected(new LogBlockEvent
                            {
                                PlaceId = placeId,
                                IsBlocked = true,
                                Timestamp = DateTime.UtcNow,
                                LogLine = line
                            });

                            // If immediate kill is enabled, terminate now
                            if (config.KillBlockedGameImmediately)
                            {
                                LogToFile($"[LogMonitor] Calling TerminateRobloxProcess (immediate kill enabled)");
                                TerminateRobloxProcess();
                            }
                            else
                            {
                                LogToFile($"[LogMonitor] NOT terminating immediately (KillBlockedGameImmediately=false); PlaytimeTracker will handle");
                            }
                        }
                        else
                        {
                            LogToFile($"[LogMonitor] Game {placeId} is allowed");
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
    /// Gracefully kill Roblox process and automatically restart to home screen.
    /// This is the preferred method for PlaytimeTracker kills as it:
    ///   1. Sends WM_CLOSE for graceful close (allows cleanup)
    ///   2. Force kills if timeout
    ///   3. Waits for system cleanup
    ///   4. Automatically restarts Roblox to home page
    /// Fallback to TerminateRobloxProcess if RobloxRestarter unavailable.
    /// </summary>
    private async void KillAndRestartToHome(string reason)
    {
        try
        {
            if (_restarter == null)
            {
                LogToFile("[LogMonitor.KillAndRestartToHome] RobloxRestarter not initialized, falling back to basic kill");
                TerminateRobloxProcess();
                return;
            }

            LogToFile("[LogMonitor.KillAndRestartToHome] Starting graceful kill + restart sequence");
            LogToFile($"[LogMonitor.KillAndRestartToHome] Reason: {reason}");
            
            // This will:
            // - Send WM_CLOSE to main window (graceful signal)
            // - Wait up to 2 seconds for process to exit
            // - Force kill if still running
            // - Wait for cleanup (500ms)
            // - Automatically restart Roblox to home screen
            await _restarter.KillAndRestartToHome(reason);
            
            LogToFile("[LogMonitor.KillAndRestartToHome] Kill + restart sequence complete");
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.KillAndRestartToHome] ERROR: {ex.GetType().Name}: {ex.Message}");
            LogToFile("[LogMonitor.KillAndRestartToHome] Falling back to basic kill");
            TerminateRobloxProcess();
        }
    }

    /// <summary>
    /// Soft-disconnect from current game (graceful close without restart).
    /// 
    /// Use case: After-hours enforcement or inactivity limits where we want
    /// to close the game gracefully but allow user to manually restart to a different game.
    /// 
    /// Strategy:
    ///   1. Graceful close (respects timeout, allows cleanup)
    ///   2. Force kill if timeout (escalation)
    ///   3. No automatic restart
    /// 
    /// Fallback to TerminateRobloxProcess if RobloxRestarter unavailable.
    /// </summary>
    private async void SoftDisconnectGame(string reason)
    {
        try
        {
            if (_restarter == null)
            {
                LogToFile("[LogMonitor.SoftDisconnectGame] RobloxRestarter not initialized, falling back to basic kill");
                TerminateRobloxProcess();
                return;
            }

            LogToFile("[LogMonitor.SoftDisconnectGame] Starting soft disconnect sequence (graceful close, no restart)");
            LogToFile($"[LogMonitor.SoftDisconnectGame] Reason: {reason}");
            
            // This will:
            // - Send WM_CLOSE to main window (graceful signal)
            // - Wait up to 5 seconds for process to exit (generous timeout)
            // - Force kill if still running
            // - NO automatic restart
            await _restarter.SoftDisconnectGame(reason);
            
            LogToFile("[LogMonitor.SoftDisconnectGame] Soft disconnect sequence complete");
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.SoftDisconnectGame] ERROR: {ex.GetType().Name}: {ex.Message}");
            LogToFile("[LogMonitor.SoftDisconnectGame] Falling back to basic kill");
            TerminateRobloxProcess();
        }
    }

    /// <summary>
    /// Aggressively terminates all RobloxPlayerBeta process(es) immediately.
    /// Uses force kill (not graceful close) for fastest blocking.
    /// NOTE: This does NOT auto-restart. Use KillAndRestartToHome() instead for auto-restart.
    /// </summary>
    private void TerminateRobloxProcess()
    {
        try
        {
            LogToFile("[LogMonitor.TerminateRobloxProcess] Looking for RobloxPlayerBeta processes...");
            var processes = Process.GetProcessesByName("RobloxPlayerBeta");
            LogToFile($"[LogMonitor.TerminateRobloxProcess] Found {processes.Length} process(es)");
            
            foreach (var proc in processes)
            {
                try
                {
                    LogToFile($"[LogMonitor.TerminateRobloxProcess] Killing process PID={proc.Id}");
                    System.Console.WriteLine($"[LogMonitor] TERMINATING RobloxPlayerBeta (PID: {proc.Id})");
                    Debug.WriteLine($"[LogMonitor] FORCE TERMINATING RobloxPlayerBeta (PID: {proc.Id})");
                    // Aggressive: Kill immediately without graceful close
                    // This ensures blocked games cannot launch
                    proc.Kill(true); // Kill with child processes
                    LogToFile($"[LogMonitor.TerminateRobloxProcess] Process {proc.Id} successfully terminated");
                    System.Console.WriteLine($"[LogMonitor] Successfully terminated process {proc.Id}");
                    Debug.WriteLine($"[LogMonitor] Process {proc.Id} killed");
                }
                catch (Exception ex)
                {
                    LogToFile($"[LogMonitor.TerminateRobloxProcess] Error killing process {proc.Id}: {ex.Message}");
                    System.Console.WriteLine($"[LogMonitor] Error killing process: {ex.Message}");
                    Debug.WriteLine($"[LogMonitor] Error killing process: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.TerminateRobloxProcess] CRITICAL ERROR: {ex.GetType().Name}: {ex.Message}");
            LogToFile($"[LogMonitor.TerminateRobloxProcess] StackTrace: {ex.StackTrace}");
            System.Console.WriteLine($"[LogMonitor] Error in TerminateRobloxProcess: {ex.Message}");
            Debug.WriteLine($"[LogMonitor] Error in TerminateRobloxProcess: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts ISO 8601 UTC timestamp from log line.
    /// Format: [2025-10-26T03:29:23.303Z]
    /// Returns DateTime.UtcNow as fallback.
    /// CRITICAL: Ensures parsed DateTime has Kind=Utc to avoid timezone mismatch bugs.
    /// </summary>
    private DateTime ExtractTimestampFromLogLine(string logLine)
    {
        try
        {
            var match = TimestampPattern.Match(logLine);
            if (match.Success && DateTime.TryParseExact(
                match.Groups[1].Value,
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,  // Z suffix means UTC
                out var timestamp))
            {
                // Ensure parsed DateTime has Utc Kind (defensive)
                if (timestamp.Kind != DateTimeKind.Utc)
                {
                    timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
                }
                return timestamp;
            }
        }
        catch { }

        return DateTime.UtcNow;
    }

    public void Dispose()
    {
        Stop();
        
        // Dispose PlaytimeTracker
        try
        {
            _playtimeTracker?.Dispose();
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.Dispose] WARNING: Error disposing PlaytimeTracker: {ex.Message}");
        }
        
        // Clean up PID lockfile
        try
        {
            LogToFile("[LogMonitor.Dispose] Removing PID lockfile...");
            PidLockHelper.RemoveLock();
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.Dispose] WARNING: Could not remove lockfile: {ex.Message}");
        }
        
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
