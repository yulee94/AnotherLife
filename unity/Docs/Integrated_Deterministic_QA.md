# Integrated Deterministic QA

## Entry point and scope

`tools/qa/run_deterministic_qa.py` is the single local/CI entry point for the QA
foundation. `tools/qa/deterministic_qa_policy.json` fixes the suite order, command
bindings, failure codes, seed, logical clock, fixture version, and artifact root.

The canonical `full` profile contains exactly these contracts:

| Contract | Executed authority |
| --- | --- |
| `unit` | Python reproducible-build and Android package verifier tests |
| `integration` | Python scene/content integration tests |
| `play-mode` | `AL.PlayMode.Tests` main-quest PlayMode journey |
| `build-smoke` | reproducible Windows build plus structural artifact inspection |
| `scene-manifest` | approved five direct, 78 generated, and 21 non-shipping scene accounting |
| `content-manifest` | canonical local Addressables membership and generated-manifest identity |
| `save-round-trip` | schema-1 load/save/reload |
| `save-migration` | pre-schema upgrade and idempotent retry |
| `save-downgrade-rejection` | future schema and future nested data remain byte-preserved/read-only |
| `save-corruption-recovery` | truncated-primary recovery, marker tamper rejection, and both-invalid preservation |
| `save-crash-recovery` | interrupted stage retry, exact ledger resume, commit uncertainty, and verified rollback |
| `packaged-narrative` | Windows Player CH00/OMEN_1 acceptance, persistence, and resumed state evidence |

## Clean-checkout commands

Run from the repository root. The full profile requires a clean committed checkout
because the reproducible build refuses dirty build inputs.

```powershell
python tools/qa/run_deterministic_qa.py `
  --repo-root . `
  --profile full `
  --unity-exe "C:\Users\MY\AppData\Local\Programs\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" `
  --output-dir unity/Logs/DeterministicQA/full
```

The command omits Unity `-quit` for test runs, names the non-auto-referenced test
assemblies explicitly, serializes every Unity invocation, parses NUnit XML, and
requires nonzero test totals. The build uses the pinned
`AL.EditorTools.ProductionPlayerBuilder.BuildWindows64Development` path. The packaged
narrative contract consumes that build's manifest and executable.

Hosted CI has no Unity license/editor runner. It therefore runs three honest gates:

```powershell
python tools/qa/test_run_deterministic_qa.py
python tools/qa/run_deterministic_qa.py --repo-root . --profile contract `
  --output-dir artifacts/deterministic-qa
python tools/qa/run_deterministic_qa.py --repo-root . --profile ci `
  --output-dir artifacts/deterministic-qa-ci
```

`contract` exercises all twelve orchestration, evidence, comparison, and failure
contracts with versioned deterministic fixtures. It is not runtime Unity evidence.
The `ci` profile runs the actual Python unit, integration, scene-manifest, and
content-manifest subset. Release evidence is valid only from `full`; a hosted
fixture or Python-subset pass must never be relabeled as a Player, save-runtime,
or Unity test pass. The original reproducible-build unittest and inventory commands
remain in `repository / hygiene` and are not replaced by this suite.

## Deterministic controls

Every process receives:

- `AL_QA_SEED=1618033988`
- `AL_QA_CLOCK_UTC=2026-01-01T00:00:00Z`
- `AL_QA_FIXTURE_VERSION=qa-fixtures-v1`
- `PYTHONHASHSEED=1618033988`
- `TZ=UTC`

The report run ID is derived from fixture version, seed, logical clock, and Git source
revision. Artifact paths are contract/attempt-scoped. Reports record source dirty
state and SHA-256 identities for the runner, policy, manual baseline, evidence schema,
scene manifests, world and narrative catalogs, and save fixture manifest. Runtime
build evidence adds the build-manifest and normalized artifact-tree hashes.

Manifest contracts run twice and compare normalized evidence. Unity evidence is
normalized to sorted test full names/results and an SHA-256 fingerprint; durations,
host paths, and process IDs do not decide equivalence. Raw stdout/stderr remains in
human-readable attempt logs.

The checked-in save fixture manifest is the fixture-data authority. Tests may create
isolated temporary file locations internally; those ephemeral paths are not evidence
identifiers and are not exported to downstream consumers.

## Evidence API and artifact layout

Each run writes:

```text
<output-dir>/
  report.json
  junit.xml
  logs/<contract>-attempt-<n>.log
  xml/<unity-contract>-<n>.xml        # full profile
  unity/<unity-contract>-<n>.log      # full profile
  build/windows64-development.json    # full profile
  narrative/packaged-evidence.json    # full profile
```

`report.json` is canonical UTF-8/LF JSON with `reportSha256` computed over the same
payload without that member. Consumers must validate it against
`unity/SharedContracts/integrated-qa-evidence.schema.json` and verify the digest before
using any status. The stable `failureCode`, `reasonCode`, normalized `evidence`,
provenance block, JUnit output, and relative artifact links are the supported API for
platform, multiplayer, commerce, security, and release automation.

The suite exits `0` only when every selected contract and automated/manual comparison
passes. Configuration/prerequisite failures and stop-ship results exit `2`. An
intentional proof can be generated with `--inject-failure <contract-id>`; it exits
nonzero and names the exact contract and failure code.

## Automated versus documented manual results

`tools/qa/manual_results.v1.json` records the immutable parent-task evidence references,
expected contract statuses, and material build/scene/content/save identities. Every run
compares selected automated statuses and all material provenance. A missing row, status
mismatch, or identity mismatch is stop-ship. Updating the baseline is evidence work,
not a way to make a failure green: retain the superseded record and require the
applicable owner decision before changing material scene, content, save, or release
truth.

## Stop-ship handling

The runner returns nonzero and blocks release for:

- any command/test/build/Player failure;
- zero or missing NUnit totals, missing result files, or missing normalized evidence;
- differing repeated manifest evidence or mixed repeated outcomes (flake/nondeterminism);
- scene/content manifest drift;
- automated/manual status or material identity divergence;
- missing build, scene, content, save-schema, fixture, or provenance evidence;
- unknown contract/profile, malformed policy/baseline/report, or failed report digest.

Do not retry a material divergence into green. Preserve the failed artifact directory,
diagnose the stable failure code, and either correct the implementation or reopen the
applicable owner authority. Numerical release and capacity thresholds remain referenced
to `t_4a5b066c` and `t_7f6be100`; this suite does not redefine them.

## Representative proof

`tools/qa/evidence/representative-green/` is a passing all-contract fixture run.
`tools/qa/evidence/representative-intentional-failure/` injects
`QA_SCENE_MANIFEST`, records `intentional_failure_fixture`, and exits nonzero. These
prove orchestration and failure propagation only. Full local evidence is retained under
`unity/Logs/DeterministicQA/full/` and is intentionally gitignored.

## Recon coverage

The bounded QA corpus contained 18 existing build, scene, narrative, save, test-assembly,
CI, and policy files totaling 10,671 lines. 4,872 lines were read (45.65%): every file
was read in full except the 6,449-line save persistence regression suite, where 650
lines covering the exact corruption/crash-recovery methods used by this runner were
read. Generated scene-manifest bodies and unrelated gameplay systems were excluded.
