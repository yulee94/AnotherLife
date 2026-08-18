# AnotherLife — Working Agreement

Lean operating rules. No ceremony; the game ships. This replaces the retired Codex-era
governance (branch prefixes, modes, PR templates, shared-file locks, spec-per-domain).
Those older governance documents remain in `unity/Docs/` as frozen historical context
only — they are not operating constraints.

## Canonical workspace

- Single checkout: `C:\Users\MY\Documents\AnotherLife`
- Unity project: `C:\Users\MY\Documents\AnotherLife\unity`
- Remote: `https://github.com/yulee94/AnotherLife` (backup/offsite only)

## Rules (the whole agreement)

1. **One checkout.** Never work from duplicate worktrees, timestamped copies, or
   backup folders. Superseded / scratch material lives in `archive/` (out of the
   read path, gitignored).
2. **Trunk-based.** Commit small and often straight to `main` with clear messages.
   Git history is the paper trail. Branch only if a second concurrent contributor joins.
3. **Never break saves.** Any change to save data requires a backward-compatible
   migration and a test that loads an old save.
4. **Data lives in catalogs, not code.** Single catalog-driven authority:
   `unity/Assets/AL/StreamingAssets/GameData/` with JSON Schemas in
   `unity/SharedContracts/`. No hardcoded game data in C#.
5. **The user owns creative/visual/balance/release.** The agent flags decisions and
   surfaces ambiguities before implementing; the user decides.
6. **Honest validation.** Actually run/build/playtest and report exactly what passed
   and what was blocked. No fabricated evidence.
7. **Optimize continuously.** Keep runtime, assets, and data manageable for the
   broadest feasible device range at the lowest feasible install size.
