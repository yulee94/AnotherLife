# Realm-Slice Evidence Capture Harness

Status: deterministic capture foundation for `RSQ-PROTOCOL-v1.0.0`
Authority: [Realm Slice Qualification Protocol](Realm_Slice_Qualification_Protocol_v1.md)

This harness launches one immutable realm/mode/check row against the verified Windows packaged Player, inventories raw output, and writes a hash-bound row manifest. It does not approve a realm, replace independent review, or turn unavailable runtime fixtures into a pass.

## Scope and fail-closed boundary

The implementation provides:

- a versioned 12-row policy for each of `Adventure3D` and `Kingdom2_5D`;
- a 72-run mode cube for each realm after locale, input, and accessibility expansion;
- immutable scenario definitions whose catalog SHA-256 is pinned by the canonical policy;
- exact source/build/QA/catalog/save/platform preflight checks;
- separate `3d` and `2_5d` output namespaces;
- exact packaged-Player command capture;
- artifact byte count and SHA-256 inventory;
- schema and hash verification for completed row manifests;
- a two-phase capture/review finalization flow with a signed complete-manifest projection;
- bounded Player execution (30 minutes by policy) and nonzero status for incomplete capture.

A row cannot pass when the build is dirty, source/build/QA identities differ, full deterministic QA is not green, platform metadata is incomplete, the packaged Player differs from the build manifest, a required metric or artifact is absent, scenario-bound anchors/states/actions/checkpoints differ, save schema metrics disagree with the selected fixture, a performance soak is shorter than the policy floor, the canonical `raw/result.json` disagrees with the manifest, the Player exits nonzero, an artifact role uses a non-canonical path, two inventory rows resolve to one file, an output path collides or contains non-canonical separators, or an artifact changes after collection.

The runtime scenario driver remains responsible for producing the row-specific structured log, raw screenshots, continuous video, and `result.json`. If the current packaged Player does not implement the supplied `--al-realm-slice-evidence` request, the harness records the attempt and emits `FAIL_CLOSED`; it never substitutes Editor evidence or synthetic media.

## Prerequisites

Use a clean, committed source revision and Unity `6000.3.22f1`. First produce the reproducible Windows build and a full deterministic QA report for that exact build. The harness verifies their embedded canonical digests rather than trusting filenames.

Copy `tools/qa/realm_slice_platform.windows.example.json` outside the repository evidence tree and replace every placeholder with observed values. `deviceId` must be a stable pseudonym, not raw PII. The operator and independent reviewer must be different identities.

Create a local OpenSSH `allowed_signers` file and pass it with `--allowed-signers`; do not commit reviewer private keys or local trust files. The harness pins the canonical policy digest, then separately binds the trust-file digest into the signed manifest. A passing result accepts only an `ssh-keygen -Y` signature in namespace `anotherlife-rsq-v1` from the declared independent reviewer; policy substitution, trust substitution, forged signatures, arbitrary signature methods, and operator/reviewer identity aliases fail closed.

Install Python 3 with `jsonschema` for the `verify` command.

## Inventory the first mode

Generate the complete Stonehold `Adventure3D` run cube before capture:

```powershell
python tools/qa/run_realm_slice_evidence.py matrix `
  --realm Stonehold `
  --mode Adventure3D `
  --output artifacts/realm-slice/stonehold-3d-matrix.json
```

The output contains exactly 72 isolated run specifications. Generate `Kingdom2_5D` separately; never combine the files or roots.

## Capture one row

The candidate and packet IDs must use the exact realm, mode namespace, candidate revision, and rerun sequence. Example:

```powershell
python tools/qa/run_realm_slice_evidence.py --allowed-signers C:/evidence-inputs/allowed_signers capture `
  --repo-root . `
  --build-manifest artifacts/build/windows64-development.json `
  --qa-report artifacts/deterministic-qa/report.json `
  --platform-metadata C:/evidence-inputs/windows-platform.json `
  --save-fixture-id pre_schema_v0_kingdom_progress `
  --evidence-root C:/evidence/realm-slice `
  --candidate-id RSQ-Stonehold-3d-r2.4.0-1 `
  --evidence-packet-id RSQ-EV-Stonehold-3d-r2.4.0-1 `
  --realm Stonehold `
  --mode Adventure3D `
  --check-id RSQ-3D-REN-001 `
  --locale en-US `
  --input-class keyboard_mouse `
  --accessibility-preset default `
  --operator operator-id `
  --independent-reviewer reviewer-id
```

The harness launches the exact executable listed as `AnotherLifeUnity.exe` in the build manifest with fixed protocol, candidate, packet, realm, mode, check, scenario catalog/definition hashes, seed, clock, locale, input, accessibility, output-root, and `Player.log` arguments.

The Player/runtime capture path must write under the supplied raw output root:

```text
Player.log
<check-specific structured log, such as render.jsonl>
screenshots/<raw stills>
video/<continuous raw video>
result.json
telemetry/<raw telemetry>       # performance rows only
profiler/<raw profiler output>  # performance rows only
```

The harness itself writes `harness.log`. `result.json` must contain:

```json
{
  "executionState": "COMPLETE",
  "technicalResult": "PASS",
  "expectedResult": "specific expected result",
  "observedResult": "specific observed result",
  "reasonCode": "RSQ_OK",
  "defectIds": [],
  "scenarioDefinitionSha256": "<exact --scenario-definition-sha256 value>",
  "metrics": {
    "everyPolicyMetricForTheSelectedCheck": "observed value"
  }
}
```

A completed contradiction uses `technicalResult=FAIL` and durable defect IDs. A successful capture remains `technicalResult=FAIL_CLOSED` with `proposedTechnicalResult=PASS|FAIL` and `review.attestation` missing until a separate reviewer signs the finalized projection. Missing setup, review trust, evidence, authority, or capability remains fail closed.

## Independent review and finalization

Create review metadata after capture completion:

```json
{
  "reviewer": "reviewer-id",
  "reviewedUtc": "2026-09-03T01:30:00Z",
  "reviewerDisposition": "PASS"
}
```

Generate the exact complete-manifest projection, sign it with the reviewer's trusted OpenSSH key, then finalize:

```powershell
python tools/qa/run_realm_slice_evidence.py --allowed-signers C:/evidence-inputs/allowed_signers attestation `
  --evidence-root C:/evidence/realm-slice `
  --manifest <row-root>/manifest.json `
  --review-metadata C:/evidence-inputs/review.json `
  --output C:/evidence-inputs/review-attestation.json

ssh-keygen -Y sign -f C:/keys/reviewer_ed25519 -n anotherlife-rsq-v1 C:/evidence-inputs/review-attestation.json

python tools/qa/run_realm_slice_evidence.py --allowed-signers C:/evidence-inputs/allowed_signers finalize `
  --evidence-root C:/evidence/realm-slice `
  --manifest <row-root>/manifest.json `
  --review-metadata C:/evidence-inputs/review.json `
  --signature C:/evidence-inputs/review-attestation.json.sig
```

Finalization writes `<row-root>/reviewed-manifest.json` only after verifying the policy digest, trust-file digest, pinned scenario catalog and per-scenario definition hashes, full artifact inventory (including paths and collection times), the inventoried Player `result.json` against the manifest disposition and observations, command/timing envelope, metric semantics, reviewer chronology, and detached signature. It never mutates the provisional manifest.

## Verify finalized evidence

Run verification from the same evidence root:

```powershell
python tools/qa/run_realm_slice_evidence.py --allowed-signers C:/evidence-inputs/allowed_signers verify `
  --evidence-root C:/evidence/realm-slice `
  --manifest C:/evidence/realm-slice/RSQ-Stonehold-3d-r2.4.0-1/Stonehold/3d/en-US/RSQ-3D-REN-001/<run-id>/reviewed-manifest.json
```

Verification checks the canonical manifest digest, policy and trust digests, strict JSON Schema, mode namespace, artifact IDs and paths, file sizes and hashes, timing order, metric semantics, reviewer independence, and the detached signature for both completed `PASS` and `FAIL`. Any mutation, missing file, cross-mode path, stale review, or schema drift returns nonzero.

After all 72 rows for one mode are captured and independently reviewed, use the protocol's packet/signature and owner-decision process. A green row manifest is not a signed packet and is not owner approval.

## Developer regression command

```powershell
python tools/qa/test_realm_slice_evidence.py
```

The suite exercises deterministic identity, scenario and run-cube coverage, source evidence binding, mode-isolated success fixtures, path collision, missing-media fail-closed behavior, artifact tamper rejection, CLI inventory, and strict schema validation.
