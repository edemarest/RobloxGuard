# RobloxGuard Production-Ready Checklist

## ✅ Current Status: LogMonitor WORKING!
- **Fixed:** File locking issue with `FileShare.ReadWrite`
- **Result:** Real-time blocking DURING game launch (not after)
- **Tested:** Successfully blocks placeId 93978595733734 on join

---

## 📦 Installation & Distribution Strategy

### Current Flow (v0.1.0)
1. **Download** → User gets `RobloxGuardInstaller.exe` from GitHub Release
2. **Install** → Inno Setup installs to `%LOCALAPPDATA%\RobloxGuard\`
3. **First Run** → `--install-first-run` registers protocol handler + creates scheduled task
4. **Startup** → Windows Task Scheduler starts `RobloxGuard.exe --monitor-logs` at logon

### Issues to Fix for Production

#### 1. **UI/UX Unification**
- [ ] First-run wizard (Setup UI instead of silent install)
- [ ] Clearer error messages and status feedback
- [ ] Proper logging instead of console spam
- [ ] Start settings UI automatically after install
- [ ] Status indicator (running/stopped/error)

#### 2. **Installation Robustness**
- [ ] Verify admin NOT required (currently works, but edge cases?)
- [ ] Handle registry permission errors gracefully
- [ ] Rollback on failed install
- [ ] Uninstall should clean ALL traces
- [ ] Handle update scenarios (upgrade from v0.1 → v0.2)

#### 3. **Monitoring Reliability**
- [ ] Prevent multiple LogMonitor instances
- [ ] Auto-restart if LogMonitor crashes
- [ ] Heartbeat/health check
- [ ] Error recovery (don't spam console with file lock errors)

#### 4. **Configuration & Safety**
- [ ] UI to manage blocklist (add/remove placeIds)
- [ ] PIN/password to unlock games
- [ ] Logs viewer in UI
- [ ] Settings validation (don't allow malformed config)
- [ ] Backup config on startup

#### 5. **Build & Release Automation**
- [ ] Version bump automation
- [ ] GitHub Actions for build + publish
- [ ] Sign executables (code signing)
- [ ] Create installer properly
- [ ] SHA256 checksums
- [ ] Release notes generation

#### 6. **Testing & Documentation**
- [ ] End-to-end test suite
- [ ] Installation test
- [ ] Protocol handler test
- [ ] LogMonitor reliability test
- [ ] User documentation/README

---

## 🔧 Priority Fixes (Pre-Release)

### CRITICAL (Must Fix)
1. **LogMonitor console spam on file lock errors**
   - Currently logs 100s of error messages → confusing
   - Suppress repetitive errors or log once per 10 seconds
   
2. **Multiple LogMonitor instances**
   - If task runs twice, we need mutex/named pipe to prevent duplicates
   - Could cause unexpected process kills

3. **Uninstall not working**
   - Need to verify restore upstream handler works
   - Need to clean scheduled task properly

### HIGH (Should Fix)
4. **Settings UI needs enhancements**
   - Show current blocklist
   - Add/remove games UI
   - View logs
   - See monitoring status

5. **First-run wizard**
   - Replace silent install with friendly setup UI
   - Explain what RobloxGuard does
   - Set initial PIN
   - Verify installation successful

6. **Installer improvements**
   - Create shortcut to settings (desktop or start menu)
   - Show post-install message with next steps
   - Handle if Roblox not installed (warn user)

### MEDIUM (Nice to Have)
7. **Logging system**
   - Replace `System.Console.WriteLine` with proper logger
   - Different log levels (Debug, Info, Warn, Error)
   - Rotate logs (don't grow infinitely)
   - Accessible from UI

8. **Config validation**
   - Schema validation for config.json
   - Migrate old configs on update
   - Sensible defaults

9. **Code signing**
   - Sign EXE with certificate
   - Sign installer
   - Improves trust/avoids SmartScreen

---

## 📋 Files That Need Changes

### Core Changes Needed

**1. RobloxGuard.Core/LogMonitor.cs**
- ✅ Fixed file locking issue
- [ ] Suppress repetitive file lock errors
- [ ] Add instance count check
- [ ] Better error recovery

**2. RobloxGuard.UI/Program.cs**
- [ ] Add first-run wizard mode
- [ ] Improve `--install-first-run` (call wizard)
- [ ] Add status checking command
- [ ] Reduce console spam

**3. RobloxGuard.UI/SettingsWindow.xaml(.cs)**
- [ ] Show blocklist (add/remove UI)
- [ ] Display monitoring status
- [ ] Show logs
- [ ] PIN management

**4. RobloxGuard.Core/ConfigManager.cs**
- [ ] Validate config on load
- [ ] Provide defaults
- [ ] Handle corrupted configs

**5. build/inno/RobloxGuard.iss**
- [ ] Update version to 0.2.0 or 1.0.0
- [ ] Add pre-install checks
- [ ] Show license/agreement
- [ ] Better finish page

**6. .github/workflows/release.yml** (if exists)
- [ ] Build automation
- [ ] Version bump
- [ ] Checksums
- [ ] Release creation

**7. README.md**
- [ ] User-friendly installation instructions
- [ ] Screenshots
- [ ] FAQ
- [ ] Troubleshooting

---

## 🚀 Proposed Release Plan

### v1.0.0 Minimum Viable Release
```
User downloads RobloxGuardInstaller.exe
    ↓
Runs installer
    ↓
Installation wizard (first-run wizard):
  - Explains RobloxGuard
  - Asks for PIN
  - Shows blocklist (empty)
  - "Install" button
    ↓
Installer registers protocol handler + creates task
    ↓
Settings UI opens automatically
    ↓
User can:
  - Add games to blocklist
  - View current blocked games
  - Close and Roblox is protected
```

### Uninstall Flow
```
User uninstalls via Control Panel
    ↓
Scheduled task removed
    ↓
Protocol handler restored to original
    ↓
App directory deleted
    ↓
Config backed up (optional)
```

---

## 🎯 Next Steps

1. **Immediate**: Suppress LogMonitor file lock error spam
2. **High Priority**: Create first-run wizard (WPF window)
3. **High Priority**: Enhance SettingsWindow UI
4. **High Priority**: Build release workflow (GitHub Actions)
5. **Medium Priority**: Code signing + verified release
6. **Long Term**: Logging system, config migrations, auto-updates

---

## 📝 Success Criteria for Production

- [ ] User can install in 3 clicks
- [ ] No console windows spam
- [ ] Roblox games actually block immediately
- [ ] Pin/unlock works without admin
- [ ] Uninstall completely removes app
- [ ] Can update without re-installing
- [ ] Works on Windows 10 & 11
- [ ] No security warnings/SmartScreen issues
- [ ] Help menu explains everything
- [ ] Error messages are clear and actionable
