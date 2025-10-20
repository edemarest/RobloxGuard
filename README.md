# RobloxGuard

Per-user Windows parental control for Roblox that blocks specific experiences (by `placeId`) using:
- A protocol shim for `roblox-player:` (primary, pre-launch block)
- A process watcher fallback for `RobloxPlayerBeta.exe`
- An optional topmost overlay to hide content (non-injection)

## Quick Start
1. Install .NET 8 SDK and Git.
2. Open the repo in VS Code or Visual Studio.
3. Read `.github/copilot-instructions.md` (source of truth for Copilot).
4. Build: `dotnet build` then publish: `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o out/publish`.
5. Package: use Inno Setup script in `build/inno/RobloxGuard.iss`.
6. Install and test: the installer registers the per-user protocol and a logon watcher task.

## Project layout
- `src/RobloxGuard.Core` — logic (URI parsing, watcher, registry/scheduler helpers, logging)
- `src/RobloxGuard.UI` — WPF/WinForms UI for settings + block modal (stub)
- `src/RobloxGuard.Installers` — helpers for first-run/uninstall (stub)
- `docs` — specs Copilot relies on (protocols, registry, UX, parsing fixtures)
- `.github` — Copilot instructions + Actions workflows
- `build/inno` — Inno Setup script for per-user installer

## License
Choose a license (MIT recommended) and add it as `LICENSE`.
