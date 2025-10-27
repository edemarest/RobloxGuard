# Protocol Test Results - October 26, 2025

## Test Execution

**Date/Time:** October 26, 2025 at 15:41-15:42 PST
**Test Subject:** RobloxGuard Soft Disconnect Protocol Testing
**User:** In-game testing with live Roblox session
**Game Location:** Sol's RNG (based on log entries)

---

## Protocols Tested

### 1. `roblox-launcher://go-home`
- **Status:** Protocol accepted by Windows shell
- **Response:** No immediate log changes
- **Disconnect Pattern:** NOT detected
- **Result:** ❌ **Does NOT trigger soft disconnect**

### 2. `roblox-launcher://disconnect`
- **Status:** Protocol accepted by Windows shell
- **Response:** No immediate log changes
- **Disconnect Pattern:** NOT detected
- **Result:** ❌ **Does NOT trigger soft disconnect**

### 3. `roblox-launcher://leave-game`
- **Status:** Protocol accepted by Windows shell
- **Response:** No immediate log changes
- **Disconnect Pattern:** NOT detected
- **Result:** ❌ **Does NOT trigger soft disconnect**

---

## Key Findings

### Protocol Behavior
✗ Windows accepted all three protocol schemes (they were recognized as valid protocol URIs)
✗ None of them triggered any observable response in Roblox logs
✗ No "Client:Disconnect", "Sending disconnect", or "leaveUGCGameInternal" patterns appeared
✗ Game remained in menu state with no changes

### What This Means
- **Hypothesis:** These protocol schemes are NOT implemented in RobloxLauncher
- **Alternative Possibility:** The protocols may only work for specific launcher contexts (not after game is running)
- **Conclusion:** Approach 1 (launcher protocols) appears to be **NOT viable**

---

## Logs Evidence

### Latest Log File Created During Test
```
File: 0.696.0.6960797_20251026T154153Z_Player_BF1EE_last.log
Size: 44.09 KB
Active: Yes (actively being written to)
```

### Last Status Entry
```
2025-10-26T15:42:15.046Z [FLog::Output] [BloxstrapRPC] {"command":"SetRichPresence",
  "data":{"state":"In Main Menu","smallImage":{"hoverText":"Sol's RNG",...}}}
```

**Interpretation:** Game loaded successfully, player is in the game's main menu at timestamp 15:42:15Z

### Protocol Test Timeframe
- Commands sent: 15:41-15:42 PST
- No disconnect patterns appeared in logs before, during, or after test
- Only normal game activity (asset loading, errors) logged

---

## Recommendation

### Approach 1 Status: ❌ NOT VIABLE

The launcher protocol approach does NOT work. The protocols were recognized by Windows but:
1. RobloxLauncher does not support these specific protocol schemes
2. No API documentation exists for these commands (we searched)
3. Roblox does not log any response or recognition of these commands

### Next Steps: Move to Approach 2 (WM_CLOSE)

Since Approach 1 failed, we should implement **Approach 2: Windows Message (WM_CLOSE)**

**Why Approach 2 is Better:**
- Uses standard Windows API (proven reliable)
- Targets game window directly (not launcher)
- Graceful close message (not forceful)
- Can escalate to hard-kill if needed

**Implementation Effort:** 2-3 hours
**Risk Level:** Medium (depends on Roblox window handling)
**Expected Success Rate:** 40-60%

---

## Test Conclusion

**Approach 1 (Launcher Protocols): FAILED** ❌

The RobloxLauncher does not support the tested protocol schemes for soft disconnection. All three variants (`go-home`, `disconnect`, `leave-game`) were accepted by the Windows shell but produced no response from Roblox.

**Recommendation:** Proceed with Approach 2 implementation (WM_CLOSE window message) as backup strategy.

