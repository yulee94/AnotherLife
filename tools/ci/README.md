# AnotherLife CI Gates

This directory contains deterministic local validators used by issue #155 Phase A.

Run from the repository root:

```powershell
./tools/ci/Invoke-AnotherLifeQualityGate.ps1 -Mode Classify
./tools/ci/Invoke-AnotherLifeQualityGate.ps1 -Mode Hygiene
```

The GitHub workflow exposes the stable check names from `unity/Docs/Repository_Quality_Gate_Policy.md`:

- `policy / classify`
- `repository / hygiene`
- `android / unit-debug`

Unity compile, EditMode, PlayMode, and Player-build checks remain manual evidence until the self-hosted runner and #127/#150 gates are proven.
