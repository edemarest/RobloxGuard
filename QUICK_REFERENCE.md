# RobloxGuard - Quick Reference Card

## 📋 Project Summary

| Aspect | Details |
|--------|---------|
| **Project Name** | RobloxGuard |
| **Purpose** | Windows parental control for Roblox games |
| **Target Framework** | .NET 8.0 LTS |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **License** | MIT |
| **Status** | ✅ Core Complete, ⏳ Phase 4 Ready |

---

## 🚀 Quick Start

### Installation (User)
```powershell
RobloxGuard.exe --install-first-run
RobloxGuard.exe --ui
# Add games to blocklist, set PIN
```

### Development Setup
```powershell
git clone <repo>
cd RobloxGuard
dotnet build src\RobloxGuard.sln
dotnet test src\RobloxGuard.sln
```

---

## 📁 Codebase Overview

### Core Logic (12 files, ~700 lines)
```
PlaceIdParser.cs .................. URI/CLI parsing (3 regex patterns)
ConfigManager.cs .................. Config + PIN management
RegistryHelper.cs ................. Protocol handler registration
ProcessWatcher.cs ................. WMI process monitoring
TaskSchedulerHelper.cs ............ Scheduled task management
InstallerHelper.cs ................ Installation orchestration
```

### Tests (3 files, 36 tests)
```
PlaceIdParserTests.cs ............. 24 tests ✅
ConfigManagerTests.cs ............. 9 tests ✅
TaskSchedulerHelperTests.cs ....... 3 tests ✅
```

### User Interface (4 files, ~560 lines)
```
BlockWindow.xaml/cs ............... Block alert UI
PinEntryDialog.xaml/cs ............ PIN entry modal
SettingsWindow.xaml/cs ............ Configuration UI (4 tabs)
Program.cs ........................ Command routing (8 modes)
```

---

## 🧪 Test Coverage

```
✅ PlaceId Extraction
   - Protocol URIs
   - CLI arguments
   - Edge cases (24 tests)

✅ Configuration System
   - JSON persistence
   - PIN hashing
   - Blocklist management (9 tests)

✅ Task Scheduling
   - Task creation
   - Task deletion (3 tests)

📊 TOTAL: 36/36 passing (100%)
```

---

## 🛠️ Command Modes

```
RobloxGuard.exe --ui                    # Settings UI
RobloxGuard.exe --show-block-ui 12345   # Test block alert
RobloxGuard.exe --watch                 # Process watcher
RobloxGuard.exe --install-first-run     # Install
RobloxGuard.exe --uninstall             # Uninstall
RobloxGuard.exe --help                  # Help
```

---

## 🔒 Security Features

| Feature | Implementation |
|---------|-----------------|
| PIN Security | PBKDF2-SHA256 (100k iterations) |
| PIN Storage | Hashed only (never plaintext) |
| PIN Entry | Via modal dialog (PasswordBox) |
| Registry Access | HKCU only (no admin required) |
| Process Monitoring | WMI events (not polling) |
| Code Injection | None (out-of-process only) |

---

## 📊 Performance

| Operation | Time |
|-----------|------|
| Parse placeId | <1ms |
| Config load/save | ~50ms |
| PIN verification | ~150ms (PBKDF2 intentional) |
| Build (incremental) | <1s |
| Test suite (36 tests) | ~1s |

---

## 📝 Configuration

**Location**: `%LOCALAPPDATA%\RobloxGuard\config.json`

```json
{
  "blocklist": [12345, 67890],
  "whitelistMode": false,
  "parentPINHash": "pbkdf2:...",
  "upstreamHandlerCommand": "...",
  "overlayEnabled": false,
  "watcherEnabled": true
}
```

---

## 🔧 Architecture Layers

```
┌─────────────────────────────────────┐
│  UI Layer (WPF)                     │
│  - BlockWindow, Settings, PIN Entry │
└─────────────────────────────────────┘
          ↓
┌─────────────────────────────────────┐
│  Core Logic (No UI Dependencies)    │
│  - Parser, Config, Registry, Watcher│
└─────────────────────────────────────┘
          ↓
┌─────────────────────────────────────┐
│  Windows Runtime                    │
│  - Registry, WMI, Task Scheduler    │
└─────────────────────────────────────┘
```

---

## 🎯 Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Out-of-process | Safe, simple, no injection |
| Per-user only | Each user independent blocklist |
| PBKDF2 PIN | Strong, slow by design |
| Regex parsing | Fast, maintainable |
| WMI watcher | Event-driven, efficient |
| Single EXE | Simple deployment |

---

## 📚 Documentation Files

| File | Purpose |
|------|---------|
| ARCHITECTURE.md | System design |
| INTEGRATION_TEST_GUIDE.md | Testing procedures |
| STATUS_REPORT.md | Progress tracking |
| README_IMPLEMENTATION.md | User guide |
| IMPLEMENTATION_COMPLETE.md | Dev summary |

---

## ✅ Completed Phases

| Phase | Status | Deliverables |
|-------|--------|--------------|
| 1 | ✅ Complete | Core logic, 24 tests |
| 2 | ✅ Complete | Config, 9 tests |
| 3 | ✅ Complete | Infrastructure, UI, 36 tests |
| 4 | ⏳ Ready | Integration testing procedures |
| 5 | ⏳ Next | Single-file publishing |
| 6 | ⏳ Next | Installer packaging |
| 7 | ⏳ Next | CI/CD automation |

---

## 📋 Build Status

```
Configuration: Release
Status: ✅ Success
Errors: 0
Warnings: 40 (CA1416 platform compatibility - expected)
Tests Passing: 36/36 (100%)
```

---

## 🚦 What's Next

**Immediate (This Week)**:
- [ ] Protocol handler integration tests
- [ ] Process watcher validation
- [ ] Registry persistence verification

**Short-term (Next Week)**:
- [ ] Real Roblox client testing
- [ ] Single-file publish
- [ ] Installer testing

**Medium-term**:
- [ ] CI/CD GitHub Actions
- [ ] GitHub Release creation
- [ ] Production deployment

---

## 🔍 File Locations

```
Core Logic:     src/RobloxGuard.Core/*.cs
Tests:          src/RobloxGuard.Core.Tests/*Tests.cs
UI:             src/RobloxGuard.UI/*.xaml/cs
Config:         %LOCALAPPDATA%\RobloxGuard\config.json
Registry:       HKCU\Software\Classes\roblox-player\...
Logs:           %LOCALAPPDATA%\RobloxGuard\logs\
```

---

## 💾 Installation Registry Keys

```
HKCU\Software\Classes\roblox-player\shell\open\command
└─ (Default) = "C:\Path\RobloxGuard.exe" --handle-uri "%1"

HKCU\Software\RobloxGuard\Upstream
└─ Handler = <original handler path>
```

---

## 🧵 Threading Model

**UI Thread (WPF Dispatcher)**:
- BlockWindow rendering
- Settings UI updates
- Pin entry dialog

**Thread Pool (Async/Await)**:
- Roblox API calls (game name fetch)
- Config JSON I/O
- Registry operations

**Scheduled Task (Background)**:
- Process watcher (--watch mode)

---

## 🎓 Learning Resources

- **PBKDF2**: https://en.wikipedia.org/wiki/PBKDF2
- **.NET 8**: https://dotnet.microsoft.com/download/dotnet/8.0
- **WPF**: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/
- **Windows Registry**: https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry
- **WMI**: https://learn.microsoft.com/en-us/windows/win32/wmisdk/

---

## 🤝 Contributing

- Bug reports: GitHub Issues
- Feature requests: GitHub Discussions
- Code submissions: Pull Requests
- Security issues: security@example.com

---

## 📞 Support

- **Issues**: GitHub Issues
- **Discussions**: GitHub Discussions
- **Security**: Email security contact
- **Documentation**: /docs folder

---

## ⚡ One-Liners

```powershell
# Build
dotnet build src\RobloxGuard.sln

# Test
dotnet test src\RobloxGuard.sln

# Publish
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true src\RobloxGuard.UI

# Install
RobloxGuard.exe --install-first-run

# Configure
RobloxGuard.exe --ui

# Uninstall
RobloxGuard.exe --uninstall
```

---

## 📈 Growth Roadmap

**v1.0**: Core blocking (Current)  
**v1.1**: Whitelist mode, request unlock notifications  
**v1.2**: Activity logging, time-based restrictions  
**v2.0**: Multi-platform, cross-device sync  

---

**RobloxGuard** - Making Roblox parental controls simple, secure, and free.

For more information: See docs/ folder or ARCHITECTURE.md
