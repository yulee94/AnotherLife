# AnotherLife CI Gates

This directory contains deterministic local validators for repository hygiene
and Android release applicability.
`.github/anotherlife-policy.yml` is the maintained policy source for forbidden
tracked patterns, the production test-scene path, and Android release-sensitive
paths.

Run from the repository root:

```powershell
./tools/ci/Invoke-AnotherLifeQualityGate.ps1 -Mode Hygiene
./tools/ci/Invoke-AnotherLifeQualityGate.ps1 -Mode AndroidReleaseApplicability
./tools/ci/Test-AnotherLifeQualityGateFixtures.ps1
```

The GitHub workflow exposes these check names:

- `repository / hygiene`
- `android / unit-debug`
- `android / release`

Unity compile, EditMode, PlayMode, and Player-build checks remain manual evidence until the self-hosted runner and #127/#150 gates are proven.

`Test-AnotherLifeQualityGateFixtures.ps1` creates disposable temporary Git repositories and proves representative failure cases without modifying the working tree.

See `QUALITY_GATE_PROOF_AND_MERGE_CONTROLS.md` for historical proof evidence and `main` branch-protection settings.
