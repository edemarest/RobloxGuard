# Contributing

## Branch & PR
- Create feature branches from `main` using `feature/<area>-<short-desc>` (e.g., `feature/protocol-parser`).
- PRs must include acceptance criteria, manual test steps, and screenshots for UI.

## Commit messages
- Use conventional commits: `feat:`, `fix:`, `docs:`, `chore:`, etc.

## Code style
- .NET 8, C#, nullable enabled. Keep logic in `RobloxGuard.Core`.
- Add unit tests for all parsing changes (see `docs/parsing_tests.md`).

## Security
- No DLL injection, no graphics hooking. Out-of-process only.
