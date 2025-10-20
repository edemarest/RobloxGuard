# Parsing Tests (fixtures)

Use these examples to drive unit tests in `RobloxGuard.Core.Tests`.

## Protocol URI samples
- `roblox://experiences/start?placeId=1818&launchData=x`
  - Expect: 1818
- `roblox://placeId=1537690962/`
  - Expect: 1537690962
- `roblox-player:1+launchmode:app+...PlaceLauncher.ashx?request=RequestGame&placeId=1416690850&userId=-1`
  - Expect: 1416690850

## Client CLI samples
- `RobloxPlayerBeta.exe --id 519015469`
  - Expect: 519015469
- `RobloxPlayerBeta.exe --play -j https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&placeId=1416690850&userId=-1 -a https://... -t <token>`
  - Expect: 1416690850

## Edge cases
- No `placeId` present → Expect: None
- `placeId` appears multiple times → Expect: first occurrence
- Mixed casing (`PlaceLauncher.Ashx`) → Expect: case-insensitive match
