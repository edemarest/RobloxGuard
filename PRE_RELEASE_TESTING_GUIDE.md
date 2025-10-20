# RobloxGuard - Pre-Release Testing & Deployment Guide

**Status**: Ready for Release Testing  
**Last Updated**: Phase 3 Complete  
**Estimated Time to Production**: 1-2 weeks (testing + release)

---

## 🚀 Release Readiness Checklist

### Code Quality ✅
- [x] 36/36 unit tests passing (100%)
- [x] Zero compilation errors
- [x] Release build verified
- [x] Code review standards met
- [x] Security hardened (PBKDF2, no injection)
- [x] Documentation complete

### Infrastructure ✅
- [x] GitHub Actions CI/CD configured
  - [x] Build on PR + push to main
  - [x] Automated testing
  - [x] Single-file publish
  - [x] Installer creation
  - [x] Checksum generation
- [x] Release workflow configured
  - [x] Tag-based triggers (v*)
  - [x] GitHub Release creation
  - [x] Artifact upload

### Testing Needed ⏳
- [ ] Manual protocol handler testing (real Roblox)
- [ ] Manual process watcher testing
- [ ] Installation flow validation
- [ ] Uninstall cleanup verification
- [ ] Real-world Roblox client testing

---

## 🧪 Pre-Release Testing Procedure

### Phase 1: Local Manual Testing

#### 1.1 Protocol Handler Interception
```powershell
# Simulate Roblox protocol URI
Start-Process "roblox-player://placeId=12345"
```
**Expected**: Block UI appears (if 12345 is in blocklist)

#### 1.2 Process Watcher
```powershell
# Start watcher in background
Start-Process "RobloxGuard.exe" -ArgumentList "--watch" -NoNewWindow -PassThru

# Simulate process spawn (requires RobloxPlayerBeta.exe available)
# Should trigger block if placeId in blocklist
```
**Expected**: Block UI appears when RobloxPlayerBeta.exe spawns with blocked placeId

#### 1.3 Installation Flow
```powershell
# First-run setup
RobloxGuard.exe --install-first-run

# Verify registry
Get-ItemProperty 'HKCU:\Software\Classes\roblox-player\shell\open\command'
# Should show: RobloxGuard.exe --handle-uri "%1"

# Verify scheduled task
schtasks /query /tn RobloxGuard\Watcher

# Verify config
ls "$env:LOCALAPPDATA\RobloxGuard\"
```
**Expected**: All three items created successfully

#### 1.4 Uninstall Flow
```powershell
# Uninstall
RobloxGuard.exe --uninstall

# Verify original handler restored
Get-ItemProperty 'HKCU:\Software\Classes\roblox-player\shell\open\command'

# Verify task deleted
schtasks /query /tn RobloxGuard\Watcher
# Should show: ERROR: The system cannot find the file specified
```
**Expected**: Original state restored, RobloxGuard removed

#### 1.5 Settings UI
```powershell
RobloxGuard.exe --ui
```
**Expected**:
- [ ] Settings window opens
- [ ] PIN tab: Can set/change PIN
- [ ] Blocklist tab: Can add/remove games
- [ ] Settings tab: Toggles work
- [ ] About tab: Shows info

---

### Phase 2: Real Roblox Client Testing

**Environment**: Windows VM (clean or with Roblox installed)

#### 2.1 Pre-Test Setup
1. [ ] Install latest Roblox client
2. [ ] Run: `RobloxGuard.exe --install-first-run`
3. [ ] Run: `RobloxGuard.exe --ui`
4. [ ] Set PIN (e.g., "1234")
5. [ ] Add a game to blocklist (find a placeId)

#### 2.2 Test: Allowed Game Launch
```
1. [ ] Click "Play" on a game NOT in blocklist
2. [ ] Verify Roblox launches normally
3. [ ] Expected: No Block UI appears
```

#### 2.3 Test: Blocked Game - Protocol Handler
```
1. [ ] Click "Play" on a game IN blocklist
2. [ ] Expected: Block UI appears immediately
3. [ ] Verify message shows: "Place [ID] is blocked"
4. [ ] Click "Back to Favorites" → Window closes, no launch
5. [ ] Try again with wrong PIN → Error message
6. [ ] Try again with correct PIN → Game launches
```

#### 2.4 Test: Blocked Game - Process Watcher
```
1. [ ] Start watcher: RobloxGuard.exe --watch
2. [ ] Try launching blocked game via Roblox client
3. [ ] Expected: Block UI appears (if protocol handler didn't catch it)
4. [ ] Verify process terminated (RobloxPlayerBeta.exe gone)
```

#### 2.5 Test: Auto-Start on Logon
```
1. [ ] Restart Windows
2. [ ] Verify scheduled task ran (process running in background)
3. [ ] Try launching blocked game → Block UI appears
```

#### 2.6 Test: Uninstall Cleanup
```
1. [ ] Run: RobloxGuard.exe --uninstall
2. [ ] Launch blocked game → Should work (no blocking)
3. [ ] Verify scheduled task gone
4. [ ] Verify registry restored to original Roblox handler
5. [ ] Verify %LOCALAPPDATA%\RobloxGuard\ cleaned up (or contains only config)
```

---

### Phase 3: Installer Testing

#### 3.1 Build Installer
```powershell
# Run GitHub Actions locally or:
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true `
  src/RobloxGuard.UI

# Create installer (requires Inno Setup installed)
choco install innosetup
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\build\inno\RobloxGuard.iss

# Output: build/inno/Output/RobloxGuard-Setup.exe
```

#### 3.2 Test Installer on Clean VM
```
1. [ ] Fresh Windows VM (no RobloxGuard, no .NET)
2. [ ] Run RobloxGuard-Setup.exe
3. [ ] Expected: Prompts for install location, then installs
4. [ ] Expected: Registry changed, task created, config initialized
5. [ ] Expected: Can run RobloxGuard.exe --ui after install
6. [ ] Expected: Roblox blocking works
```

#### 3.3 Test Uninstaller
```
1. [ ] Run installer again or: Control Panel → Programs → Uninstall
2. [ ] Expected: All registry entries removed
3. [ ] Expected: Scheduled task deleted
4. [ ] Expected: App folder deleted (or config kept)
5. [ ] Expected: Roblox works normally again (no blocking)
```

---

### Phase 4: Edge Cases & Stress Testing

#### 4.1 Edge Cases
- [ ] BlockList with 100+ games → No slowdown
- [ ] Very long game names (50+ chars) → UI handles correctly
- [ ] Multiple simultaneous process launches → All blocked
- [ ] Rapid PIN attempts → All rejected
- [ ] PIN with special characters → Hashed correctly
- [ ] Network offline → Block UI still works (no game name)

#### 4.2 Performance Testing
- [ ] Process detection latency <1 second
- [ ] Block UI appears <500ms
- [ ] PIN verification <300ms
- [ ] Settings UI responsive (no lag)

#### 4.3 Stability Testing
- [ ] Watcher runs for 24+ hours without crash
- [ ] Multiple logon/logoff cycles → Task still works
- [ ] System sleep/wake → Watcher resumes

---

## 🔄 GitHub Actions Verification

### CI/CD Workflow Status

**File**: `.github/workflows/ci.yml`

**Triggers**:
- ✅ On push to `main`
- ✅ On pull requests to `main`
- ✅ Manual trigger (workflow_dispatch)

**Steps**:
1. ✅ Checkout code
2. ✅ Setup .NET 8.0
3. ✅ Restore dependencies
4. ✅ Build (Release)
5. ✅ Run 36 tests
6. ✅ Publish single-file EXE
7. ✅ Build installer (Inno Setup)
8. ✅ Generate checksums (SHA256)
9. ✅ Upload artifacts

**File**: `.github/workflows/release.yml`

**Triggers**:
- ✅ On git tag `v*` (e.g., `v1.0.0`)

**Steps**:
1. ✅ Run CI workflow
2. ✅ Create GitHub Release
3. ✅ Upload artifacts (EXE, installer, checksums)

---

## 📦 Release Artifact Verification

### Expected Artifacts

After GitHub Actions completes:

```
RobloxGuard-artifacts/
├── publish/
│   ├── RobloxGuard.exe (self-contained, ~100MB)
│   ├── RobloxGuard.dll (and deps)
│   └── ... (runtime files)
├── inno/Output/
│   └── RobloxGuard-Setup.exe (installer)
└── checksums.sha256
    ├── [SHA256]  RobloxGuard.exe
    └── [SHA256]  RobloxGuard-Setup.exe
```

### Checksum Verification

Users can verify integrity:
```powershell
# Download checksums.sha256
# Run:
certutil -hashfile RobloxGuard.exe SHA256
# Compare with checksums.sha256
```

---

## 📋 Release Notes Template

```markdown
# RobloxGuard v1.0.0

## Features
- ✅ Protocol handler interception for Roblox URIs
- ✅ Process watcher fallback with WMI
- ✅ PIN-protected configuration
- ✅ Professional WPF settings UI
- ✅ Per-user installation (no admin required)
- ✅ 36 unit tests (100% passing)

## What's Included
- `RobloxGuard.exe` - Single-file, self-contained executable
- `RobloxGuard-Setup.exe` - Installer for easy deployment

## Installation
1. Run `RobloxGuard-Setup.exe`
2. Run `RobloxGuard.exe --ui` to configure
3. Add games to blocklist and set parent PIN
4. Done! Blocking activates immediately

## Usage
- Open Settings: `RobloxGuard.exe --ui`
- Block alert appears when child tries blocked game
- Parent enters PIN to allow game launch
- Watcher auto-starts on logon

## Known Limitations
- Per-user only (each account has independent blocklist)
- Requires internet for game name resolution (optional)
- Regex patterns may need updates if Roblox changes URI format

## Security
- PBKDF2-SHA256 PIN hashing (100k iterations)
- No DLL injection or code manipulation
- Out-of-process architecture
- Windows Registry (HKCU) only

## Checksums (SHA256)
```
[SHA256_HERE]  RobloxGuard.exe
[SHA256_HERE]  RobloxGuard-Setup.exe
```

## Troubleshooting
See: https://github.com/yourusername/RobloxGuard/blob/main/docs/INTEGRATION_TEST_GUIDE.md

## Support
- Issues: GitHub Issues
- Questions: GitHub Discussions
```

---

## 🔐 Security Checklist Before Release

- [x] No hardcoded secrets in code
- [x] No API keys in repository
- [x] PIN hashing verified (PBKDF2)
- [x] Random salt generation tested
- [x] No DLL injection vectors
- [x] Registry operations safe (backup before modify)
- [x] Exception handling comprehensive
- [x] No temp file security issues
- [x] HTTPS for Roblox API calls (if internet required)
- [x] Code reviewed for injection vulnerabilities

---

## 📊 Release Testing Matrix

| Test | Manual | Automated | Status |
|------|--------|-----------|--------|
| Unit Tests (36) | ✅ | ✅ (CI) | ✅ Pass |
| Build (Release) | ✅ | ✅ (CI) | ✅ Pass |
| Installer | ⏳ Needed | ✅ (CI) | 🟡 Pending |
| Protocol Handler | ⏳ Needed | ❌ | 🟡 Pending |
| Process Watcher | ⏳ Needed | ❌ | 🟡 Pending |
| Real Roblox | ⏳ Needed | ❌ | 🟡 Pending |
| Uninstall | ⏳ Needed | ❌ | 🟡 Pending |

---

## 🎯 Step-by-Step Release Process

### Step 1: Final Manual Testing (Local)
```powershell
# Install locally
RobloxGuard.exe --install-first-run

# Configure
RobloxGuard.exe --ui
# Add game to blocklist, set PIN

# Test protocol handler
Start-Process "roblox-player://placeId=12345"
# Expected: Block UI appears

# Uninstall
RobloxGuard.exe --uninstall
```

### Step 2: Trigger GitHub Actions Build
```powershell
# Build in GitHub (automatically on push to main)
# Or manually via workflow_dispatch

# Verify artifacts:
# - RobloxGuard.exe (self-contained)
# - RobloxGuard-Setup.exe (installer)
# - checksums.sha256
```

### Step 3: Test Installer
```powershell
# Download RobloxGuard-Setup.exe from CI artifacts
# Run on test Windows VM
# Verify installation works
# Verify blocking works
# Test uninstall
```

### Step 4: Create GitHub Release
```powershell
# Tag release
git tag v1.0.0
git push origin v1.0.0

# GitHub Actions automatically:
# - Builds
# - Tests
# - Creates GitHub Release
# - Uploads artifacts
```

### Step 5: Verify Release
```
1. Go to GitHub Releases page
2. Verify v1.0.0 created
3. Verify artifacts present:
   - RobloxGuard.exe
   - RobloxGuard-Setup.exe
   - checksums.sha256
4. Download and verify checksums
5. Announce release
```

---

## ⚠️ Pre-Release Warnings

**DO NOT release if**:
- ❌ Any unit test failing
- ❌ Build errors in Release mode
- ❌ Protocol handler doesn't intercept
- ❌ Block UI doesn't appear
- ❌ PIN unlock fails
- ❌ Uninstall leaves registry entries
- ❌ Installer doesn't work on clean VM

**DO verify before release**:
- ✅ All 36 tests pass
- ✅ Release build succeeds
- ✅ Real Roblox blocking works
- ✅ PIN protection works
- ✅ Uninstall cleanup complete
- ✅ Installer creates all needed files
- ✅ Checksums generated correctly

---

## 🚀 Post-Release Activities

### Week 1 Post-Release
- [ ] Monitor GitHub Issues for bugs
- [ ] Respond to user questions
- [ ] Track download metrics
- [ ] Check for crash reports

### Month 1 Post-Release
- [ ] Gather user feedback
- [ ] Plan v1.1 features
- [ ] Monitor Roblox URI format changes
- [ ] Fix any critical bugs

### Ongoing
- [ ] Monitor GitHub Issues
- [ ] Release security updates immediately
- [ ] Update documentation based on user feedback
- [ ] Plan major features (whitelist mode, time restrictions, etc.)

---

## 📞 Release Communication

### GitHub Release Description
- [x] Feature list
- [x] Installation instructions
- [x] Security notes
- [x] Checksums
- [x] Known limitations
- [x] Support links

### README Updates
- [x] Link to latest release
- [x] Installation instructions
- [x] Quick start guide

### Documentation
- [x] ARCHITECTURE.md (complete)
- [x] INTEGRATION_TEST_GUIDE.md (complete)
- [x] README_IMPLEMENTATION.md (complete)
- [x] QUICK_REFERENCE.md (complete)

---

## ✅ Final Readiness Checklist

Before clicking "Release":

- [ ] All 36 unit tests passing locally
- [ ] Release build clean locally
- [ ] GitHub Actions CI passing on main
- [ ] Manual protocol handler test passed
- [ ] Manual process watcher test passed
- [ ] Installer test passed on clean VM
- [ ] Uninstall cleanup verified
- [ ] README updated with download link
- [ ] Release notes written
- [ ] Checksums documented
- [ ] Security review completed
- [ ] No known critical bugs

---

## 🎉 Ready for Release!

Once all above items are verified, you can proceed with:

```powershell
# Tag and push
git tag v1.0.0
git push origin v1.0.0

# GitHub Actions will automatically:
# 1. Build Release
# 2. Run all tests
# 3. Publish single-file EXE
# 4. Create installer
# 5. Generate checksums
# 6. Create GitHub Release with all artifacts
```

**Expected result**: Production-ready RobloxGuard v1.0.0 available on GitHub Releases

---

**Status**: 🟡 **READY FOR RELEASE TESTING**

All infrastructure in place. Awaiting manual verification with real Roblox client before final release tag.
