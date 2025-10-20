# UX Specs — Block UI & Settings

## Block UI (modal/overlay)
- **Goal:** Inform child the game is blocked, provide friendly options.
- **Title:** "This Roblox game is blocked"
- **Body:** "This experience is on your blocked list. Ask a parent for permission."
- **Details:** Show the `placeId` and (if online) resolve the friendly name.
- **Buttons:**
  - "Back to favorites" (opens allowed games URL or app screen)
  - "Request unlock" (opens mailto: or logs a request)
  - "Parent unlock" (reveals PIN input)
- **Behavior:** Always-on-top; if watcher path, create overlay immediately while closing the game.

## Settings UI
- Set/Change Parent PIN
- Blocklist editor (add/remove placeIds; import from URL or CSV)
- Toggle overlay mode
- View logs (timestamp, placeId, action, source=protocol|watcher)
- Export/Import config.json
