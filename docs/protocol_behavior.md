# Protocol & Launch Behavior (reference)

## Protocol URIs
Examples that the OS may pass as `%1`:
- `roblox://experiences/start?placeId=1818&launchData=...`
- `roblox://placeId=1537690962/`
- `roblox-player:1+launchmode:app+...` (may embed or resolve to an assetgame URL)

**Rule:** extract `placeId` via `/[?&]placeId=(\d+)/`. If absent, search for an embedded `PlaceLauncher.ashx?...placeId=...` URL.

## Client command-line forms
When launched by the bootstrapper/shortcut, you may see:
- `RobloxPlayerBeta.exe --id 519015469`
- `RobloxPlayerBeta.exe --play -j https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&placeId=1416690850&userId=-1 ...`

**Rule:** parse via `/--id\s+(\d+)/` and `/PlaceLauncher\.ashx.*?[?&]placeId=(\d+)/`.

## Teleports
In-game teleports won’t trigger a new protocol launch. This project does **not** intercept in-game teleports (no injection). Policy recommendation: default‑deny (whitelist known placeIds) or accept that teleports may move the user within the allowed experience.
