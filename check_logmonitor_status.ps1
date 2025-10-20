# PowerShell Script: Start LogMonitor and Test

Write-Host "════════════════════════════════════════════════════════"
Write-Host "RobloxGuard LogMonitor - Quick Test"
Write-Host "════════════════════════════════════════════════════════"
Write-Host ""

# Check processes
$robloxGuardRunning = Get-Process RobloxGuard -ErrorAction SilentlyContinue
$robloxGameRunning = Get-Process RobloxPlayerBeta -ErrorAction SilentlyContinue

Write-Host "Current Status:"
Write-Host "  LogMonitor running: $(if ($robloxGuardRunning) { '✅ YES' } else { '❌ NO' })"
Write-Host "  Roblox game running: $(if ($robloxGameRunning) { '✅ YES' } else { '❌ NO' })"
Write-Host ""

# Check logs
$logPath = "$env:LOCALAPPDATA\Roblox\logs"
if (Test-Path $logPath) {
    $latestLog = ls "$logPath\*_Player_*_last.log" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($latestLog) {
        Write-Host "Latest Roblox Log File:"
        Write-Host "  Path: $($latestLog.FullName)"
        Write-Host "  Size: $([math]::Round($latestLog.Length / 1MB, 2)) MB"
        Write-Host "  Modified: $($latestLog.LastWriteTime)"
        
        # Search for recent joins
        $joins = Get-Content $latestLog.FullName | Select-String "! Joining game" | Select-Object -Last 3
        Write-Host ""
        Write-Host "Recent Game Joins (last 3):"
        if ($joins) {
            $joins | ForEach-Object { Write-Host "  $_" }
        } else {
            Write-Host "  (None found in this log session)"
        }
    } else {
        Write-Host "❌ No player log file found"
    }
} else {
    Write-Host "❌ Roblox logs directory not found"
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════════"
Write-Host "NEXT STEPS:"
Write-Host "════════════════════════════════════════════════════════"
Write-Host ""
Write-Host "1. CLOSE Roblox game if running:"
Write-Host "   Get-Process Roblox* | Stop-Process -Force"
Write-Host ""
Write-Host "2. START LogMonitor in NEW terminal:"
Write-Host "   & `"$env:LOCALAPPDATA\RobloxGuard\RobloxGuard.exe`" --monitor-logs"
Write-Host ""
Write-Host "3. OPEN Roblox app (NOT a game yet)"
Write-Host ""
Write-Host "4. CLICK Play on a blocked game (1818 or 93978595733734)"
Write-Host ""
Write-Host "5. WATCH Terminal 1 for:"
Write-Host "   ❌ BLOCKED: Game 1818"
Write-Host ""
Write-Host "6. VERIFY game didn't launch"
Write-Host ""
Write-Host "════════════════════════════════════════════════════════"
