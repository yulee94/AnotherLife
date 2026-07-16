# AnotherLife CI Gates

This directory contains deterministic local validators used by issue #155 Phase A.

Run from the repository root:

```powershell
./tools/ci/Invoke-AnotherLifeQualityGate.ps1 -Mode Classify
./tools/ci/Invoke-AnotherLifeQualityGate.ps1 -Mode Hygiene
./tools/ci/Test-AnotherLifeQualityGateFixtures.ps1
```

The GitHub workflow exposes the stable check names from `unity/Docs/Repository_Quality_Gate_Policy.md`:

- `policy / classify`
- `repository / hygiene`
- `android / unit-debug`

Unity compile, EditMode, PlayMode, and Player-build checks remain manual evidence until the self-hosted runner and #127/#150 gates are proven.

`Test-AnotherLifeQualityGateFixtures.ps1` creates disposable temporary Git repositories and proves representative failure cases without modifying the working tree.

See `QUALITY_GATE_PROOF_AND_MERGE_CONTROLS.md` for current proof evidence, temporary failing-PR proof branches, and required `main` branch-protection settings.
