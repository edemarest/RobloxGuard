# Quick Implementation Guide - Feature 1 & 2

**Status:** Ready to Code  
**Timeline:** 5-7 business days  
**Approach:** Config-first, then parallel development

---

## Phase 0: Config Schema (Start Here)

### File: `Config.cs`
```csharp
// Add these properties to the Config class
public class Config
{
    // FEATURE 1: After-Hours Soft Disconnect
    public bool AfterHoursSoftDisconnectEnabled { get; set; } = true;
    public string AfterHoursSoftDisconnectTime { get; set; } = "03:00";
    public int AfterHoursSoftDisconnectWindowMinutes { get; set; } = 30;
    public int AfterHoursSoftDisconnectProbability { get; set; } = 65;  // 0-100
    public int AfterHoursSoftDisconnectMaxConsecutiveDays { get; set; } = 2;
    public int ConsecutiveDisconnectDays { get; set; } = 0;
    public DateTime? LastDisconnectDate { get; set; } = null;
    public bool SoftDisconnectTestMode { get; set; } = false;
    public int SoftDisconnectTestMinutesOffset { get; set; } = 1;
    
    // FEATURE 2: Inactivity Monitoring
    public bool InactivityDisconnectEnabled { get; set; } = true;
    public uint InactivityDisconnectGamePlaceId { get; set; } = 0;
    public int InactivityDisconnectMinutes { get; set; } = 60;
    public string InactivityQuietHoursStart { get; set; } = "03:30";
    public string InactivityQuietHoursEnd { get; set; } = "09:00";
    public string InactivityDetectionMethod { get; set; } = "input";  // or "focus"
    public bool InactivityTestMode { get; set; } = false;
}
```

### File: `config.json` (Add to template)
```json
{
  "// After-Hours Soft Disconnect Settings": "",
  "afterHoursSoftDisconnectEnabled": true,
  "afterHoursSoftDisconnectTime": "03:00",
  "afterHoursSoftDisconnectWindowMinutes": 30,
  "afterHoursSoftDisconnectProbability": 65,
  "afterHoursSoftDisconnectMaxConsecutiveDays": 2,
  "consecutiveDisconnectDays": 0,
  "lastDisconnectDate": null,
  "softDisconnectTestMode": false,
  "softDisconnectTestMinutesOffset": 1,
  
  "// Inactivity Disconnect Settings": "",
  "inactivityDisconnectEnabled": true,
  "inactivityDisconnectGamePlaceId": 0,
  "inactivityDisconnectMinutes": 60,
  "inactivityQuietHoursStart": "03:30",
  "inactivityQuietHoursEnd": "09:00",
  "inactivityDetectionMethod": "input",
  "inactivityTestMode": false
}
```

---

## Phase 1A: Feature 1 - After-Hours Logic

### File: `PlaytimeTracker.cs` - Add Methods

```csharp
public class PlaytimeTracker
{
    private Random _random = new Random();
    private DateTime? _randomizedAfterHoursTime = null;

    /// <summary>
    /// Check if after-hours soft disconnect should trigger
    /// </summary>
    public bool ShouldTriggerAfterHoursDisconnect()
    {
        try
        {
            var config = _getConfig();
            
            if (!config.AfterHoursSoftDisconnectEnabled)
            {
                LogToFile("[PlaytimeTracker.ShouldTriggerAfterHoursDisconnect] Feature disabled");
                return false;
            }

            // Initialize randomized time once per day
            if (_randomizedAfterHoursTime == null || _randomizedAfterHoursTime.Value.Date != DateTime.Now.Date)
            {
                InitializeRandomizedAfterHoursTime(config);
            }

            var now = DateTime.Now;
            
            // Test mode: use offset minutes from now
            if (config.SoftDisconnectTestMode)
            {
                var testTime = _randomizedAfterHoursTime.Value.AddMinutes(config.SoftDisconnectTestMinutesOffset);
                LogToFile($"[PlaytimeTracker] Test mode: Check time {testTime:HH:mm:ss}");
                if (now < testTime)
                    return false;
            }
            else
            {
                // Normal mode: check if in randomized window
                var windowStart = _randomizedAfterHoursTime.Value;
                var windowEnd = _randomizedAfterHoursTime.Value.AddMinutes(config.AfterHoursSoftDisconnectWindowMinutes);
                
                if (now < windowStart || now >= windowEnd)
                {
                    return false;
                }
            }

            LogToFile($"[PlaytimeTracker.ShouldTriggerAfterHoursDisconnect] In after-hours window");

            // Check consecutive counter (day 3 is always allowed)
            if (config.ConsecutiveDisconnectDays >= config.AfterHoursSoftDisconnectMaxConsecutiveDays)
            {
                LogToFile($"[PlaytimeTracker.ShouldTriggerAfterHoursDisconnect] ⚠ Day {config.ConsecutiveDisconnectDays + 1}: No disconnect (max consecutive reached)");
                return false;
            }

            // Roll RNG
            var roll = _random.Next(0, 100);
            var threshold = config.AfterHoursSoftDisconnectProbability;
            var triggered = roll < threshold;

            LogToFile($"[PlaytimeTracker.ShouldTriggerAfterHoursDisconnect] RNG: {roll}% vs {threshold}% = {(triggered ? "TRIGGER" : "NO TRIGGER")}");
            LogToFile($"[PlaytimeTracker.ShouldTriggerAfterHoursDisconnect] Consecutive days: {config.ConsecutiveDisconnectDays}/{config.AfterHoursSoftDisconnectMaxConsecutiveDays}");

            return triggered;
        }
        catch (Exception ex)
        {
            LogToFile($"[PlaytimeTracker.ShouldTriggerAfterHoursDisconnect] ERROR: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Initialize randomized after-hours trigger time (once per day)
    /// </summary>
    private void InitializeRandomizedAfterHoursTime(dynamic config)
    {
        // Parse base time
        var timeParts = config.AfterHoursSoftDisconnectTime.ToString().Split(':');
        int hour = int.Parse(timeParts[0]);
        int minute = int.Parse(timeParts[1]);

        // Randomize within window
        var randomMinute = _random.Next(0, config.AfterHoursSoftDisconnectWindowMinutes);
        var now = DateTime.Now;
        _randomizedAfterHoursTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0).AddMinutes(randomMinute);

        LogToFile($"[PlaytimeTracker] Randomized after-hours time: {_randomizedAfterHoursTime:HH:mm:ss} (window: {config.AfterHoursSoftDisconnectWindowMinutes}min)");
    }

    /// <summary>
    /// Update state after soft disconnect attempt
    /// </summary>
    public void UpdateAfterHoursState(bool disconnected)
    {
        try
        {
            var config = _getConfig();

            if (disconnected)
            {
                config.ConsecutiveDisconnectDays++;
                config.LastDisconnectDate = DateTime.Now;
                LogToFile($"[PlaytimeTracker.UpdateAfterHoursState] Disconnected. Counter now: {config.ConsecutiveDisconnectDays}");
            }
            else
            {
                // If a day has passed since last disconnect, reset counter
                var lastDate = config.LastDisconnectDate ?? DateTime.MinValue;
                var daysSinceLastDisconnect = (DateTime.Now - lastDate).Days;

                if (daysSinceLastDisconnect > config.AfterHoursSoftDisconnectMaxConsecutiveDays)
                {
                    config.ConsecutiveDisconnectDays = 0;
                    LogToFile($"[PlaytimeTracker.UpdateAfterHoursState] Counter reset (${daysSinceLastDisconnect} days since last disconnect)");
                }
            }

            SaveConfig(config);
        }
        catch (Exception ex)
        {
            LogToFile($"[PlaytimeTracker.UpdateAfterHoursState] ERROR: {ex.Message}");
        }
    }
}
```

### File: `LogMonitor.cs` - Integrate Feature 1

In the main check loop (probably in `OnLogLine` or similar):

```csharp
private async void CheckForScheduledActions()
{
    try
    {
        // Feature 1: After-Hours Soft Disconnect
        if (_playtimeTracker.ShouldTriggerAfterHoursDisconnect())
        {
            LogToFile("[LogMonitor] After-hours soft disconnect triggered!");
            _playtimeTracker.UpdateAfterHoursState(true);
            await KillAndRestartToHome("After-hours soft disconnect");
            return;
        }
    }
    catch (Exception ex)
    {
        LogToFile($"[LogMonitor.CheckForScheduledActions] ERROR: {ex.Message}");
    }
}
```

---

## Phase 1B: Feature 2 - InputMonitor

### File: `InputMonitor.cs` (NEW FILE)

```csharp
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace RobloxGuard.Core;

/// <summary>
/// Monitors keyboard and mouse input using Windows low-level hooks.
/// Used to detect user inactivity for automatic disconnect.
/// </summary>
public class InputMonitor : IDisposable
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxGuard",
        "input.log"
    );

    private DateTime _lastActivityTime = DateTime.UtcNow;
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private IntPtr _moduleHandle = IntPtr.Zero;
    private bool _disposed = false;

    // Windows hook constants
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_KEYDOWN = 0x0100;

    // Hook delegates (must keep alive)
    private LowLevelMouseProc _mouseDelegate;
    private LowLevelKeyboardProc _keyboardDelegate;

    // P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public bool IsRunning { get; private set; } = false;
    public DateTime LastActivityTime => _lastActivityTime;

    public void Start()
    {
        try
        {
            if (IsRunning) return;

            LogToFile("[InputMonitor.Start] Starting input monitoring");
            _lastActivityTime = DateTime.UtcNow;

            // Get current process module
            var currentProcess = Process.GetCurrentProcess();
            _moduleHandle = GetModuleHandle(currentProcess.MainModule?.ModuleName ?? "RobloxGuard.Core");

            // Create and register hooks
            _mouseDelegate = MouseHookCallback;
            _keyboardDelegate = KeyboardHookCallback;

            _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseDelegate, _moduleHandle, 0);
            if (_mouseHookHandle == IntPtr.Zero)
            {
                LogToFile("[InputMonitor.Start] ⚠ Failed to register mouse hook");
            }

            _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardDelegate, _moduleHandle, 0);
            if (_keyboardHookHandle == IntPtr.Zero)
            {
                LogToFile("[InputMonitor.Start] ⚠ Failed to register keyboard hook");
            }

            IsRunning = true;
            LogToFile("[InputMonitor.Start] ✓ Input monitoring started");
        }
        catch (Exception ex)
        {
            LogToFile($"[InputMonitor.Start] ERROR: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            if (!IsRunning) return;

            LogToFile("[InputMonitor.Stop] Stopping input monitoring");

            if (_mouseHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }

            if (_keyboardHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }

            IsRunning = false;
            LogToFile("[InputMonitor.Stop] ✓ Input monitoring stopped");
        }
        catch (Exception ex)
        {
            LogToFile($"[InputMonitor.Stop] ERROR: {ex.Message}");
        }
    }

    public TimeSpan GetInactivityDuration()
    {
        return DateTime.UtcNow - _lastActivityTime;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                int message = (int)wParam;
                
                // Only register actual mouse movements/clicks, not hover
                if (message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN || message == WM_MOUSEWHEEL)
                {
                    _lastActivityTime = DateTime.UtcNow;
                }
            }
        }
        catch { /* Silently ignore */ }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                _lastActivityTime = DateTime.UtcNow;
            }
        }
        catch { /* Silently ignore */ }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static void LogToFile(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch { /* Ignore logging errors */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
```

### File: `PlaytimeTracker.cs` - Add Inactivity Method

```csharp
public class PlaytimeTracker
{
    private InputMonitor _inputMonitor;

    public PlaytimeTracker(Func<dynamic> getConfig, InputMonitor inputMonitor = null)
    {
        _getConfig = getConfig;
        _inputMonitor = inputMonitor;
    }

    /// <summary>
    /// Check if inactivity disconnect should trigger
    /// </summary>
    public bool ShouldTriggerInactivityDisconnect(uint currentPlaceId)
    {
        try
        {
            var config = _getConfig();

            if (!config.InactivityDisconnectEnabled)
                return false;

            if (_inputMonitor == null || !_inputMonitor.IsRunning)
                return false;

            // Check if current game matches configured placeId
            if (config.InactivityDisconnectGamePlaceId > 0 && currentPlaceId != config.InactivityDisconnectGamePlaceId)
            {
                return false;
            }

            // Check quiet hours
            var now = DateTime.Now;
            var (quietStart, quietEnd) = ParseTimeRange(config.InactivityQuietHoursStart, config.InactivityQuietHoursEnd);

            if (IsTimeInRange(now.TimeOfDay, quietStart, quietEnd))
            {
                LogToFile($"[PlaytimeTracker] Inactivity suppressed (quiet hours)");
                return false;
            }

            // Check inactivity duration
            var inactivityDuration = _inputMonitor.GetInactivityDuration();
            var threshold = TimeSpan.FromMinutes(config.InactivityDisconnectMinutes);

            if (config.InactivityTestMode)
                threshold = TimeSpan.FromMinutes(1);  // 1 minute in test mode

            LogToFile($"[PlaytimeTracker] Inactivity: {inactivityDuration.TotalMinutes:F1}min vs {threshold.TotalMinutes:F1}min threshold");

            return inactivityDuration >= threshold;
        }
        catch (Exception ex)
        {
            LogToFile($"[PlaytimeTracker.ShouldTriggerInactivityDisconnect] ERROR: {ex.Message}");
            return false;
        }
    }

    private (TimeSpan start, TimeSpan end) ParseTimeRange(string startStr, string endStr)
    {
        var startParts = startStr.Split(':');
        var endParts = endStr.Split(':');

        var start = new TimeSpan(int.Parse(startParts[0]), int.Parse(startParts[1]), 0);
        var end = new TimeSpan(int.Parse(endParts[0]), int.Parse(endParts[1]), 0);

        return (start, end);
    }

    private bool IsTimeInRange(TimeSpan current, TimeSpan start, TimeSpan end)
    {
        if (start < end)
            return current >= start && current < end;
        else
            return current >= start || current < end;  // Handles midnight wrap
    }
}
```

### File: `LogMonitor.cs` - Integrate Feature 2

```csharp
public class LogMonitor
{
    private InputMonitor _inputMonitor;

    public LogMonitor()
    {
        _inputMonitor = new InputMonitor();
        _inputMonitor.Start();
    }

    private async void CheckForScheduledActions()
    {
        try
        {
            // Feature 2: Inactivity Disconnect
            var currentPlaceId = GetCurrentPlaceId();  // You implement this
            if (_playtimeTracker.ShouldTriggerInactivityDisconnect(currentPlaceId))
            {
                LogToFile("[LogMonitor] Inactivity disconnect triggered!");
                await KillAndRestartToHome("Inactivity disconnect");
                return;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[LogMonitor.CheckForScheduledActions] ERROR: {ex.Message}");
        }
    }

    public override void Dispose()
    {
        _inputMonitor?.Dispose();
        base.Dispose();
    }
}
```

---

## Testing Quick Commands

### Test Feature 1 (After-Hours)
```powershell
# Set config to test mode (check every 1 minute)
$config.softDisconnectTestMode = $true
$config.afterHoursSoftDisconnectProbability = 100  # Always trigger in test

# Wait and observe logs
Get-Content "C:\Users\ellaj\AppData\Local\RobloxGuard\launcher.log" -Wait
```

### Test Feature 2 (Inactivity)
```powershell
# Set config for 1-minute inactivity detection
$config.inactivityTestMode = $true
$config.inactivityDisconnectMinutes = 1

# Launch game and don't move mouse/keyboard
# Should disconnect after 1 minute of no input
```

---

## Files to Edit
1. ✅ `Config.cs` - Add config fields
2. ✅ `config.json` - Add template values
3. ✅ `InputMonitor.cs` - NEW FILE for Feature 2
4. ✅ `PlaytimeTracker.cs` - Add both feature methods
5. ✅ `LogMonitor.cs` - Add scheduling calls

---

## Success Criteria

- [ ] Config loads with all new fields
- [ ] Feature 1 time window randomizes daily
- [ ] Feature 1 RNG works at configured probability
- [ ] Feature 1 consecutive counter tracks correctly
- [ ] Feature 2 InputMonitor tracks inactivity
- [ ] Feature 2 quiet hours suppress disconnect
- [ ] Both features log all decisions
- [ ] Test mode compresses time correctly
- [ ] All code compiles without errors

---

**Ready to implement?** Start with Phase 0 (Config), then parallelize Phases 1A and 1B!
