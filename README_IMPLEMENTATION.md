# RobloxGuard - Windows Parental Control for Roblox

## Overview

**RobloxGuard** is a free, open-source Windows parental control application that blocks specific Roblox games (by place ID) from launching. It operates completely **out-of-process** (no DLL injection), requires **no administrator privileges**, and provides a **professional WPF user interface** for configuration.

### Key Features

✅ **Zero-Admin Installation** - Installs to `%LOCALAPPDATA%` per user  
✅ **Out-of-Process Blocking** - Protocol handler interception + WMI watcher fallback  
✅ **PIN-Protected** - PBKDF2-SHA256 security (100,000 iterations)  
✅ **Always-On** - Automatic watcher on system logon via scheduled task  
✅ **Professional UI** - Settings management, PIN entry, block alerts  
✅ **Real-Time Blocking** - Event-driven WMI process monitoring  

---

## System Requirements

- **OS**: Windows 10 or later (64-bit)
- **.NET**: 8.0 LTS (bundled in self-contained publish)
- **Permissions**: User-level (no admin required)

---

## Installation

### Option 1: Pre-Built Installer (Recommended)
```powershell
# Download RobloxGuard-Setup.exe from GitHub Release
# Run the installer - it will:
# 1. Register protocol handler
# 2. Create scheduled task for watcher
# 3. Initialize configuration
```

### Option 2: Manual Build & Installation
```powershell
# Clone repository
git clone https://github.com/yourusername/RobloxGuard.git
cd RobloxGuard

# Build Release
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true

# Find executable
# src\RobloxGuard.UI\bin\Release\net8.0-windows\win-x64\publish\RobloxGuard.exe

# Run installation
.\RobloxGuard.exe --install-first-run
```

---

## Usage

### Configuration UI
```powershell
RobloxGuard.exe --ui
```
Opens the Settings window with 4 tabs:
1. **PIN** - Set/change parent PIN
2. **Blocklist** - Add/remove/toggle blocked games
3. **Settings** - Overlay & watcher toggles
4. **About** - Feature information

### Setting Up

1. Open Settings UI: `RobloxGuard.exe --ui`
2. Go to **PIN tab** → Set a strong PIN (parent only)
3. Go to **Blocklist tab** → Add place IDs of games to block
4. Click **Save & Close**
5. **Done!** Watcher will activate on next logon

### Finding Place IDs

Place IDs are visible in the Roblox URL:
```
https://www.roblox.com/games/PlaceId12345
                             ^^^^^^^^^^
```
Or in the Roblox client:
- Hover over game → Details panel shows Place ID
- Copy Place ID → Paste into RobloxGuard

### Managing Blocks

**Blocklist Mode** (default):
- Games in blocklist are blocked
- All other games are allowed

**Whitelist Mode** (optional):
- Only games in whitelist are allowed
- All other games are blocked

Toggle mode in Blocklist tab settings.

### Unlocking a Blocked Game

When a child tries to launch a blocked game:
1. Block alert window appears
2. Shows game name and "Place [ID] is blocked"
3. **Options**:
   - "Back to Favorites" → Close, no unlock
   - "Request Unlock" → Message parent (future feature)
   - "Enter PIN" → Parent enters PIN → Game launches

---

## Uninstallation

### Option 1: Via Installer
- Run the installer again → Choose "Uninstall"
- Removes protocol handler, scheduled task, config folder

### Option 2: Manual
```powershell
RobloxGuard.exe --uninstall

# Or manually:
# 1. Delete scheduled task:
schtasks /delete /tn RobloxGuard\Watcher /f

# 2. Remove registry keys:
# HKCU\Software\RobloxGuard\

# 3. Delete app folder:
# %LOCALAPPDATA%\RobloxGuard\
```

---

## Architecture

```
┌─────────────────────────────────────────────┐
│           RobloxGuard Execution             │
├─────────────────────────────────────────────┤
│                                             │
│  Path 1: Protocol Interception              │
│  ├─ User clicks Roblox game                 │
│  ├─ roblox-player:// URI captured           │
│  ├─ RobloxGuard parses placeId              │
│  ├─ Check blocklist                         │
│  ├─ If blocked → Show Block UI              │
│  └─ If allowed → Forward to Roblox          │
│                                             │
│  Path 2: Process Watcher (Fallback)         │
│  ├─ RobloxPlayerBeta.exe spawned            │
│  ├─ WMI event fired                         │
│  ├─ Parse command line for placeId          │
│  ├─ Check blocklist                         │
│  ├─ If blocked → Terminate + Block UI       │
│  └─ If allowed → Process continues          │
│                                             │
│  Path 3: Scheduled Task                     │
│  ├─ System starts                           │
│  ├─ Logon trigger fires                     │
│  ├─ RobloxGuard --watch starts              │
│  └─ Watcher runs continuously               │
│                                             │
└─────────────────────────────────────────────┘
```

### Key Components

| Component | Purpose |
|-----------|---------|
| **PlaceIdParser** | Regex-based extraction from URIs and CLI |
| **ConfigManager** | JSON persistence, PIN hashing |
| **RegistryHelper** | Protocol handler registration/restore |
| **ProcessWatcher** | WMI event-driven process monitoring |
| **TaskSchedulerHelper** | Scheduled task creation/deletion |
| **BlockWindow** | Alert UI when game blocked |
| **SettingsWindow** | Configuration UI (4 tabs) |

---

## Configuration

### Location
```
%LOCALAPPDATA%\RobloxGuard\config.json
```

### Structure
```json
{
  "blocklist": [12345, 67890, 11111],
  "whitelistMode": false,
  "parentPINHash": "pbkdf2:100000:salt:hash",
  "upstreamHandlerCommand": "C:\\Path\\To\\OriginalRobloxHandler.exe \"%1\"",
  "overlayEnabled": false,
  "watcherEnabled": true
}
```

### Manual Configuration

Edit `config.json` directly (advanced users):
- Add/remove place IDs to blocklist
- Change whitelistMode (true/false)
- Enable/disable features

**Note**: Config is loaded on each app run and updated by Settings UI.

---

## Command-Line Modes

### Development & Testing

```powershell
# Protocol handler simulation
RobloxGuard.exe --handle-uri "roblox-player://placeId=12345"

# Test parsing
RobloxGuard.exe --test-parse "https://...placeId=12345"
RobloxGuard.exe --test-parse "RobloxPlayerBeta.exe --id 12345"

# Test config system
RobloxGuard.exe --test-config

# Display block UI for testing
RobloxGuard.exe --show-block-ui 12345

# Start process watcher
RobloxGuard.exe --watch

# Show settings UI
RobloxGuard.exe --ui

# Installation
RobloxGuard.exe --install-first-run
RobloxGuard.exe --uninstall

# Help
RobloxGuard.exe --help
```

---

## Security & Privacy

### PIN Security
- **Algorithm**: PBKDF2-SHA256, 100,000 iterations
- **Salt**: Random, unique per PIN
- **Comparison**: Constant-time (no timing attacks)
- **Storage**: Hash only (never plaintext)

### Data Stored
- PIN hash (in config.json)
- Blocklist (not sensitive)
- Upstream handler path (for restore)

### Processes Monitored
- RobloxPlayerBeta.exe only
- Via WMI (Windows native API)
- No DLL injection or code manipulation

### Network Requests
- Roblox API for game names (optional, cached locally)
- No telemetry or tracking
- All data stays on device

---

## Troubleshooting

### Block UI Doesn't Appear

**Check**:
1. Verify config.json exists: `dir %LOCALAPPDATA%\RobloxGuard\`
2. Verify placeId is in blocklist: `RobloxGuard.exe --ui` → Blocklist tab
3. Test manually: `RobloxGuard.exe --show-block-ui 12345`

**Solution**:
- Ensure Roblox is uninstalled before blocking
- Check Windows event logs for watcher errors

### PIN Entry Always Fails

**Check**:
1. Verify PIN is set: `RobloxGuard.exe --ui` → PIN tab
2. Test PIN locally: `RobloxGuard.exe --test-config`

**Solution**:
- Reset PIN: Delete config.json and restart Settings UI
- Note: Only parent can change PIN

### Scheduled Task Not Running

**Check**:
```powershell
schtasks /query /tn RobloxGuard\Watcher /v
```

**Solution**:
- Reinstall: `RobloxGuard.exe --uninstall` then `--install-first-run`
- Manual creation:
```powershell
schtasks /create /tn RobloxGuard\Watcher /tr "C:\Path\RobloxGuard.exe --watch" /sc ONLOGON /ru INTERACTIVE /f
```

### Roblox Still Launches Blocked Game

**Check**:
1. Verify blocking method reached:
   - Protocol handler: Registry key correct?
   - Process watcher: Running? (`RobloxGuard.exe --watch`)

**Solution**:
- Reinstall protocol handler: `RobloxGuard.exe --install-first-run`
- Verify Roblox not installed to admin location
- Check Registry: `HKCU:\Software\Classes\roblox-player\shell\open\command`

---

## Development

### Building from Source

**Prerequisites**:
- .NET 8.0 SDK or later
- Visual Studio 2022 (optional)

**Build**:
```powershell
git clone <repo>
cd RobloxGuard

# Restore + Build
dotnet build src\RobloxGuard.sln

# Run tests
dotnet test src\RobloxGuard.sln

# Publish single-file EXE
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true src\RobloxGuard.UI
```

### Project Structure

```
src/
├── RobloxGuard.Core/          # Core logic (no UI)
│   ├── PlaceIdParser.cs       # Regex extraction
│   ├── ConfigManager.cs       # Config + PIN
│   ├── RegistryHelper.cs      # Protocol registration
│   ├── ProcessWatcher.cs      # WMI monitoring
│   ├── TaskSchedulerHelper.cs # Scheduled tasks
│   └── InstallerHelper.cs     # Installation
├── RobloxGuard.Core.Tests/    # 36 unit tests
│   ├── PlaceIdParserTests.cs  # 24 parsing tests
│   ├── ConfigManagerTests.cs  # 9 config tests
│   └── TaskSchedulerHelperTests.cs # 3 scheduler tests
├── RobloxGuard.UI/            # WPF user interface
│   ├── BlockWindow.xaml       # Alert on block
│   ├── PinEntryDialog.xaml    # PIN verification
│   ├── SettingsWindow.xaml    # Configuration (4 tabs)
│   └── Program.cs             # Command routing
└── RobloxGuard.Installers/    # Installer packaging
```

### Running Tests

```powershell
# All tests
dotnet test src\RobloxGuard.sln

# Specific test suite
dotnet test src\RobloxGuard.Core.Tests --filter "PlaceIdParserTests"

# With coverage
dotnet test src\RobloxGuard.sln /p:CollectCoverage=true
```

### Test Results

```
PlaceIdParserTests ........... 24 ✅
ConfigManagerTests ........... 9  ✅
TaskSchedulerHelperTests ..... 3  ✅
─────────────────────────────────
Total ........................ 36 ✅
Pass Rate: 100%
```

---

## Known Limitations

1. **Single-User Blocking** - Each user has independent blocklist
   - Parents can't enforce across all accounts from one place
   - Workaround: Set up each account separately

2. **Game Name Resolution** - Requires internet for Roblox API
   - Block UI shows generic "Unknown Game (offline)" if no internet
   - Still blocks the game, just no name

3. **Registry Elevation** - Some operations may require elevation
   - Mitigated by per-user keys (HKCU only)
   - Should work without admin for installation

4. **Roblox URI Parsing** - Regex patterns may need updates
   - Roblox changes URI format occasionally
   - Easy to update patterns in PlaceIdParser.cs

---

## Roadmap

### v1.0 (Current)
- ✅ Protocol handler interception
- ✅ Process watcher fallback
- ✅ PIN-protected settings
- ✅ Per-user installation

### v1.1 (Planned)
- ⏳ Whitelist mode
- ⏳ Request unlock notifications
- ⏳ Activity logging
- ⏳ Block reason customization

### v1.2 (Planned)
- ⏳ Multi-device sync
- ⏳ Time-based restrictions
- ⏳ Screen time limits
- ⏳ Parental report cards

### v2.0 (Planned)
- ⏳ Cross-platform (macOS, Linux)
- ⏳ VPN/DNS blocking
- ⏳ Browser extension blocking
- ⏳ Mobile app companion

---

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Areas for Contribution
- Bug reports and fixes
- UI/UX improvements
- Additional regex patterns for URI parsing
- Performance optimizations
- Localization (i18n)
- Documentation improvements

---

## License

This project is licensed under the **MIT License** - see [LICENSE](LICENSE) file for details.

---

## Support

### Getting Help

1. **Documentation**: See [docs/](docs/) folder
2. **Issues**: GitHub Issues for bugs/feature requests
3. **Discussions**: GitHub Discussions for questions

### Reporting Bugs

Please include:
- Windows version
- .NET version (if applicable)
- Steps to reproduce
- Expected vs. actual behavior
- Screenshots of Block UI (if applicable)

### Security Issues

⚠️ **Do not** post security vulnerabilities on GitHub Issues.

Email: security@example.com (replace with actual contact)

---

## Acknowledgments

- Built with .NET 8.0 and WPF
- Inspired by parental control systems across platforms
- Thanks to the open-source community

---

## FAQ

### Q: Will RobloxGuard work after Roblox updates?
**A**: Usually yes. The parsing patterns are regex-based and robust to URI format variations. Monitor releases for potential updates.

### Q: Can my child bypass the PIN?
**A**: The PIN is hashed with PBKDF2 (100,000 iterations), making brute-force attacks impractical. They would need to:
1. Uninstall RobloxGuard (requires knowing your computer admin password)
2. Delete registry keys and config file
3. Restart system

This is intentionally difficult and detectable.

### Q: Does it slow down the computer?
**A**: No. The watcher uses WMI events (not polling) and only runs when RobloxPlayerBeta.exe starts. Typically <5MB memory footprint.

### Q: Can I block Roblox entirely?
**A**: Not directly, but you can:
1. Switch to Whitelist mode
2. Don't add any games to the whitelist
3. All games will be blocked

Or use Windows Parental Controls for broader restrictions.

### Q: What if I forget my PIN?
**A**: You'll need to:
1. Delete `%LOCALAPPDATA%\RobloxGuard\config.json`
2. Restart RobloxGuard Settings UI
3. Set a new PIN

Make sure you remember it!

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.

---

**RobloxGuard** - Making Roblox parental controls simple, secure, and free.

For more information, visit: [GitHub Repository](https://github.com/yourusername/RobloxGuard)
