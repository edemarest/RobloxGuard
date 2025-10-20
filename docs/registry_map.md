# Registry Map (per-user install)

## Protocol handler (HKCU)
Key: `HKCU\Software\Classes\roblox-player\`
- `(Default)` = `Roblox Player URL` (optional)
- `URL Protocol` = `` (empty string value)
- `DefaultIcon\(Default)` = `"%LOCALAPPDATA%\RobloxGuard\RobloxGuard.exe",0` (optional)
- `shell\open\command\(Default)` = `"%LOCALAPPDATA%\RobloxGuard\RobloxGuard.exe" --handle-uri "%1"`

## Backup upstream handler
Key: `HKCU\Software\RobloxGuard`
- `Upstream` = previous `shell\open\command` string (if existed)

## Scheduled Task (per-user)
- Name: `RobloxGuard Watcher`
- Trigger: At logon (current user)
- Action: `"%LOCALAPPDATA%\RobloxGuard\RobloxGuard.exe" --watch`
- Settings: restart on failure (3x, 1 min interval)
