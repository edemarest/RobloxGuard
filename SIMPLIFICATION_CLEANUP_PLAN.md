# Simplification Checklist: Remove Process Watcher

## Decision: REMOVE Process Watcher & HandlerLock (Keep Both Optional)

Since **LogMonitor now works perfectly**, the other two mechanisms are either:
- **Process Watcher**: Redundant (LogMonitor is faster and more reliable)
- **HandlerLock**: Nice-to-have paranoia (optional surveillance)

This removes ~400 lines of code and WMI complexity.

---

## Phase 1: Code Cleanup (30 minutes)

### Files to Delete:
- [ ] `src/RobloxGuard.Core/ProcessWatcher.cs` (165 lines)
  ```
  rm "C:\Users\ellaj\Desktop\RobloxGuard\src\RobloxGuard.Core\ProcessWatcher.cs"
  ```

- [ ] `src/RobloxGuard.Core/HandlerLock.cs` (225 lines)
  ```
  rm "C:\Users\ellaj\Desktop\RobloxGuard\src\RobloxGuard.Core\HandlerLock.cs"
  ```

- [ ] Test files (if any)
  - [ ] `src/RobloxGuard.Core.Tests/ProcessWatcherTests.cs` (if exists)
  - [ ] `src/RobloxGuard.Core.Tests/HandlerLockTests.cs` (if exists)

### Files to Modify:

#### 1. `src/RobloxGuard.UI/Program.cs`
**Remove**: `--watch` and `--lock-handler` command handlers
- [ ] Delete `case "--watch":` block
- [ ] Delete `case "--lock-handler":` block
- [ ] Delete `StartWatcher()` method
- [ ] Delete `LockProtocolHandler()` method
- [ ] Update `ShowHelp()` to remove these options
- [ ] Remove any `using` statements only used by deleted code

**Before** (lines to delete):
```csharp
case "--watch":
    StartWatcher();
    break;

case "--lock-handler":
    LockProtocolHandler();
    break;

// ... methods
static void StartWatcher()
{
    // ... ~40 lines
}

static void LockProtocolHandler()
{
    // ... ~30 lines
}
```

#### 2. `src/RobloxGuard.Core/RobloxGuard.Core.csproj`
**Remove**: ProjectReference to test projects if they depend on deleted files
- [ ] Search for `ProcessWatcher` references
- [ ] Search for `HandlerLock` references
- [ ] Remove ItemGroup entries if necessary

#### 3. Analysis Documents (Optional Cleanup)
These are just notes, not affecting code:
- [ ] Delete old architecture docs that explain Process Watcher
- [ ] Delete test documents specific to WMI
- [ ] Update README if it mentions these features

---

## Phase 2: Update & Verify (30 minutes)

### 1. Remove Dependencies
- [ ] Remove `using` for ProcessWatcher from any files that import it
- [ ] Remove `using` for HandlerLock from any files that import it
- [ ] Remove System.Management if only used by ProcessWatcher

### 2. Update csproj
```bash
cd "C:\Users\ellaj\Desktop\RobloxGuard\src"
# Check for any compilation errors
dotnet build RobloxGuard.sln -c Release
```

### 3. Verify No Broken References
- [ ] Search entire `src/` for "ProcessWatcher"
- [ ] Search entire `src/` for "HandlerLock"
- [ ] If found: Remove or update references

### 4. Run Tests
```bash
cd "C:\Users\ellaj\Desktop\RobloxGuard\src"
dotnet test # Should still pass (just fewer tests)
```

---

## Phase 3: Document Changes (15 minutes)

### Update `README.md`
- [ ] Remove "Process Watcher" section from architecture
- [ ] Remove "HandlerLock" section
- [ ] Update "How It Works" diagram
- [ ] Remove installation section about scheduled task (KEEP scheduled task, but for LogMonitor only)

### Update `docs/` files
- [ ] `docs/architecture.md` - Remove these components
- [ ] `docs/protocol_behavior.md` - Still valid, no changes needed
- [ ] `docs/ux_specs.md` - Check if mentions Process Watcher
- [ ] `docs/registry_map.md` - Update to remove HandlerLock registry keys

### Create Migration Note
- [ ] New file: `docs/v1.0-simplification.md`
  - Explains why these were removed
  - Confirms LogMonitor provides same coverage
  - What if user wants Process Watcher back?

---

## Phase 4: Build & Test (20 minutes)

### Build Release
```bash
cd "C:\Users\ellaj\Desktop\RobloxGuard\src"
dotnet build RobloxGuard.sln -c Release
# Expected: Success, 0 errors

# Check for new warnings
dotnet build RobloxGuard.sln -c Release 2>&1 | grep -i error
```

### Rebuild & Publish
```bash
cd "C:\Users\ellaj\Desktop\RobloxGuard\src\RobloxGuard.UI"
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true `
  --output "$env:LOCALAPPDATA\RobloxGuard"
```

### Test Installation
- [ ] Run `--install-first-run`
- [ ] Verify NO errors about ProcessWatcher
- [ ] Verify NO errors about HandlerLock
- [ ] Verify LogMonitor starts correctly at logon
- [ ] Test blocking game: Works ✓

### Test Each Mode
```bash
# Should still work:
RobloxGuard.exe --handle-uri "roblox://placeId=1818"  # ✓ Works
RobloxGuard.exe --monitor-logs                        # ✓ Works
RobloxGuard.exe --ui                                  # ✓ Works

# Should show error or be gone:
RobloxGuard.exe --watch                               # Should give "Unknown command"
RobloxGuard.exe --lock-handler                        # Should give "Unknown command"
```

---

## Phase 5: Commit & Document (15 minutes)

### Git Commit
```bash
cd "C:\Users\ellaj\Desktop\RobloxGuard"

# Review changes
git status

# Stage deletions
git add -A

# Commit with descriptive message
git commit -m "Simplify: Remove Process Watcher & HandlerLock

- Removed ProcessWatcher.cs (WMI-based monitoring)
- Removed HandlerLock.cs (registry surveillance)
- Removed --watch and --lock-handler CLI options
- LogMonitor now provides complete coverage
- Result: ~400 lines deleted, simpler codebase, no functional change
- Closes: N/A (internal cleanup)

Rationale:
LogMonitor (log file monitoring) is faster, more reliable, and catches
all game launches (protocol + CLI + launchers). Process Watcher was a
fallback when LogMonitor didn't work. Now that it works, Process Watcher
is redundant. HandlerLock is nice-to-have paranoia but not essential.

Testing:
- Protocol Handler: Still blocks web clicks ✓
- LogMonitor: Still blocks all launches ✓
- Build: 0 errors ✓
- Tests: All pass ✓
"
```

### Create Cleanup Verification Checklist
- [ ] Create file: `.github/CHANGELOG_v1.0.md`
  ```
  ## v1.0.0 - Release Simplification
  
  ### Removed
  - Process Watcher (--watch mode) - Replaced by LogMonitor
  - HandlerLock (--lock-handler mode) - Not needed with LogMonitor
  - ~400 lines of code
  
  ### Why?
  LogMonitor is more reliable and catches all game launches.
  
  ### What's Left?
  - Protocol Handler (catches web clicks)
  - LogMonitor (catches all launches)
  - Complete blocking coverage
  - Simpler codebase
  ```

---

## Files Modified Summary

### Deleted (3 files, ~400 lines):
```
ProcessWatcher.cs           -165 lines
HandlerLock.cs              -225 lines
Related test files          -~30 lines
────────────────────────────────────
Total                       ~420 lines saved
```

### Modified (2 files, ~50 lines):
```
Program.cs
├─ Remove --watch case: -40 lines
├─ Remove --lock-handler case: -10 lines
├─ Update ShowHelp(): -2 lines
└─ Remove methods: -15 lines
──────────────────────────────
Total: ~50 lines removed
```

### Documentation Updates (5+ files):
```
README.md
docs/architecture.md
docs/ux_specs.md
docs/registry_map.md
docs/v1.0-simplification.md (new)
```

---

## Final Verification Checklist

- [ ] All deleted files removed from disk
- [ ] All references removed from Program.cs
- [ ] Build succeeds: `dotnet build -c Release`
- [ ] Tests pass: `dotnet test`
- [ ] Publish succeeds
- [ ] LogMonitor still starts and works
- [ ] Protocol Handler still works
- [ ] Settings UI still works
- [ ] Help text updated
- [ ] Git commit created
- [ ] README updated

---

## Estimated Time: 2 hours total

1. Code cleanup: 30 min
2. Update & verify: 30 min
3. Documentation: 15 min
4. Build & test: 20 min
5. Commit & document: 15 min
**Total: ~1.5-2 hours**

---

## Rollback Plan (if needed)

If we need to keep Process Watcher/HandlerLock:
1. `git revert <commit-hash>`
2. Re-add the files
3. Re-run tests

But honestly? Not needed. LogMonitor is the future.
