# Release Evidence Package and Rollback Runbook

Status: operational control for approval-ready candidates. A verified package remains
`awaiting_release_owner_approval`; package assembly never promotes a release.

Numerical release criteria belong to `t_4a5b066c`. Capacity and SLO criteria belong to
`t_7f6be100`. This runbook references those authorities and intentionally does not copy,
interpret, or replace their thresholds. The prerequisite approval dependency
`t_0648ce23` is retained in every package.

## 1. Evidence package contract

The machine-readable control is `tools/qa/release_evidence_policy.v1.json`. The portable
package contract is `unity/SharedContracts/release-evidence-package.schema.json`.
`tools/qa/assemble_release_evidence.py` accepts only a clean, passing `full` deterministic
QA run and emits:

```text
<package-root>/
  release-evidence.json
  release-evidence.json.sha256
  controls/
    release_evidence_policy.v1.json
    release-evidence-package.schema.json
    Release_Evidence_And_Rollback_Runbook.md
  evidence/
    qa/report.json
    qa/junit.xml
    qa/logs/*.log
    build/windows64-development.json
    narrative/packaged-evidence.json
```

`release-evidence.json` is canonical UTF-8/LF JSON. `packageSha256` is SHA-256 over the
canonical payload with that member omitted. The sidecar is a detached-signing input, not
a claim that a signer has signed it.

Each source authority is pinned to the QA report's 40-character Git commit, Git blob ID,
and SHA-256. Each copied artifact is pinned by package-relative path and SHA-256. The
build manifest separately pins the source tree, content tree, complete artifact tree,
and normalized reproducible artifact tree. The QA report retains each contract's stable
failure code, normalized evidence, attempt-log path, and attempt-log hash.

Downstream platform, multiplayer, commerce, security, and release consumers must:

1. validate `release-evidence.json` against the shared JSON Schema;
2. recompute `packageSha256` and every listed artifact SHA-256;
3. require `qaRun.profile=full`, `qaRun.status=passed`, and all twelve `qaContracts` passed;
4. require exact source, build-manifest, artifact-tree, scene, content, narrative, save-format,
   save-schema, and fixture identities needed by that consumer;
5. evaluate the external decisions referenced by `t_4a5b066c` and `t_7f6be100` without
   importing their numerical values into this package;
6. require the explicit `RELEASE-CANDIDATE-PROMOTION` owner gate before distribution.

A package with a valid digest is evidence integrity, not release approval.

## 2. Produce and verify a candidate

Run the full deterministic suite from a clean committed checkout with the exact approved
Unity editor:

```powershell
python tools/qa/run_deterministic_qa.py `
  --repo-root . `
  --profile full `
  --unity-exe "C:\Users\MY\AppData\Local\Programs\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" `
  --output-dir unity/Logs/DeterministicQA/release-candidate
```

The full run supplies Unity EditMode/PlayMode, save migration/recovery, one structurally
verified Windows build, scene/content manifests, and packaged narrative evidence. Before
promotion, the release operator must also satisfy `BUILD-PAIR-EQUIVALENCE`: produce two
clean candidate manifests with `tools/reproducible_build.py` and require `compare` to
return `identical` or `normalized_equivalent`. Preserve both manifests, their sidecars,
the comparison document, and the artifact trees they identify. A one-build smoke result
must not be presented as build-pair equivalence.

Assemble and independently verify:

```powershell
python tools/qa/assemble_release_evidence.py assemble `
  --repo-root . `
  --qa-root unity/Logs/DeterministicQA/release-candidate `
  --output-dir unity/Logs/ReleaseEvidence/release-candidate

python tools/qa/assemble_release_evidence.py verify `
  --package-root unity/Logs/ReleaseEvidence/release-candidate
```

Assembly fails closed for a non-full or dirty-source QA report, failed/missing contracts,
manual-result divergence, invalid report/build hashes, a build source mismatch, a failed
artifact smoke, narrative material divergence, missing/tampered logs, unknown source
blobs, or changed authority references.

## 3. Restore the last verified build/data pair

Use this procedure for a bad deployment, startup failure, content mismatch, or migration
incident. Do not select a binary and data set independently.

1. Freeze promotion and client distribution. Preserve the failed release package, logs,
   manifests, telemetry classification, and artifact hashes before changing anything.
2. Identify the last owner-promoted package whose `release-evidence.json`, sidecar, control
   artifacts, and evidence artifacts all pass `verify`. Reject an unsigned or partially
   copied directory even if its build launches.
3. Confirm the rollback package's build manifest names the same source revision, content
   tree, enabled/generated scene manifests, and artifact tree as the retained build
   artifacts. Any mismatch is a different pair and is ineligible.
4. Confirm every affected save is within the rollback client's supported read/write
   matrix. Current authority supports pre-schema `0` upgrading to schema `1`, schema `1`
   loading directly, and schemas greater than `1` only as byte-preserved read-only data.
5. Take byte-exact backups of current primary, backup, previous/quarantine generations,
   and recovery ledger/marker files before starting the rollback. Record hashes outside
   player-visible telemetry. Never replace the only retained generation.
6. Remove the failed build/data pair from admission or distribution as one unit. Restore
   the verified build artifacts and their exact content data from the selected package's
   build manifest. Do not regenerate catalogs during rollback.
7. Verify source, build-manifest, executable, artifact-tree, scene/content, and narrative
   hashes before admitting the client. Run structural smoke and the applicable packaged
   startup/narrative smoke against the restored pair.
8. Test a copied save from every observed compatibility disposition. Do not use the live
   save as the probe. If any status differs from the selected package's policy, keep the
   client rejected and escalate to migration recovery.
9. Re-enable distribution only after the package verifies, external release/capacity
   authorities remain satisfied, and the release owner records the promotion decision.
10. Retain both failed and restored evidence packages so the incident can be reproduced.

Never "fix" rollback by editing manifest hashes, regenerating scene manifests, dropping
unknown save fields, or resetting a profile.

## 4. Save-backup retention

Before build/data restoration or migration repair, copy every generation and associated
ledger/marker byte-for-byte. Record format ID, observed schema disposition, generation
role, byte length, and SHA-256. Keep paths and raw save bytes out of telemetry and tickets;
store them only in the approved restricted recovery location.

Retain the byte-exact set until the release owner closes migration observation for the
candidate under the external release authority. This runbook defines no substitute time
or count threshold. If legal, privacy, security, or platform policy requires a different
retention window, record that owner decision as a compatibility exception before deleting
anything.

The runtime may replace a malformed primary only with an exact current-schema writable
backup after both source backup and target are verified. Invalid bytes remain in
hash-linked quarantine. If both primary and backup are invalid, or evidence hashes do not
match, preserve all generations and remain `RecoveryRequired`.

## 5. Reject incompatible clients

Fail closed before any write or mutable session when one of these applies:

- client build/source/content identity is not the admitted build/data pair;
- editor/exporter identity differs from approved Unity `6000.3.22f1`;
- the legacy `2022.3.62f3` exporter is requested for this project;
- save schema is newer than the client's writable schema;
- a migration/recovery marker is missing, malformed, unknown, or hash-mismatched;
- a required scene or catalog identity differs from the package;
- Android or isolated-profile capability is claimed from the current PC-only evidence.

For schema greater than `1`, preserve the save bytes and expose only the existing
forward-schema read-only disposition. Do not select an older backup, create a replacement
profile, or write schema `1` over the future generation. A service or admission layer that
cannot compare required identities must reject the session rather than assume
compatibility.

## 6. Diagnose a failed migration

1. Stop writes and capture the stable load/write authority status and bounded diagnostic
   code. Do not log raw JSON, user content, credentials, or private filesystem paths.
2. Hash and retain primary, backup, prior/quarantine generations, and exact recovery
   ledger/marker. Record which generation was selected without copying its contents into
   telemetry.
3. Classify the failure as unsupported schema, semantic incompatibility, corrupt input,
   marker/checksum mismatch, pre-mutation write failure, post-mutation uncertainty, or
   both generations invalid.
4. Match the status/code to the checked-in fixture and regression authorities. Known
   schema-1 coverage includes pre-schema upgrade, idempotent retry, forward-schema
   read-only rejection, truncated-primary recovery, marker tamper rejection, both-invalid
   preservation, exact ledger resume, retry before mutation, commit uncertainty, and
   verified fallback rollback.
5. If there is no exact existing class, preserve all bytes, create a redacted deterministic
   fixture reproducing the shape, and reopen migration policy. Never retry repeatedly
   against the player's only copy.
6. Run the focused fixture/regression test and then the full deterministic suite. A green
   retry without an explained failure class and retained evidence is not approval.
7. Close only after the owner-approved migration policy, package evidence, and release
   decision all identify the fix.

Failed migration telemetry is itself a reopen trigger even when a later retry succeeds.

## 7. Schema rollback safety

No schema down-migration is currently approved. Existing tests prove schema `0` to `1`
upgrade, schema `1` round trip, failure atomicity/recovery, and rejection of future schema;
they do not prove rewriting schema `1` to `0` or any future schema to `1`.

A build rollback is permitted only when the restored client uses a save-compatible schema
and the exact verified build/data pair. If it cannot safely consume the current generation,
reject the client and keep the save byte-preserved. Schema rollback may be introduced only
by a new owner-approved migration policy with bidirectional golden fixtures, byte/account
invariants, atomic failure injection, idempotent retry, crash recovery, forward and
backward client tests, and a superseding package contract.

## 8. Stop-ship mapping

| Condition | Automated evidence | Explicit manual owner gate |
| --- | --- | --- |
| Unreproducible build | `QA_BUILD_SMOKE`; manifest/source/artifact hash checks | `BUILD-PAIR-EQUIVALENCE` |
| Editor/exporter incompatibility | exact-editor build preflight and `QA_BUILD_SMOKE` | `COMPATIBILITY-EXCEPTION-APPROVAL` |
| Save loss or silent downgrade | five `QA_SAVE_*` contracts | `MIGRATION-POLICY-APPROVAL` for any changed path |
| Missing required scene | `QA_SCENE_MANIFEST` | `SCENE-CONTENT-TRUTH-APPROVAL` before regeneration |
| Nondeterministic content manifest | repeated `QA_SCENE_MANIFEST` and `QA_CONTENT_MANIFEST` evidence | `SCENE-CONTENT-TRUTH-APPROVAL` for material changes |
| Narrative runtime disconnected | `QA_PLAY_MODE` and `QA_PACKAGED_NARRATIVE` | none; evidence must pass |
| Automated/manual material divergence | report `manualComparison` and all twelve contracts | `RELEASE-CANDIDATE-PROMOTION`; baseline changes require applicable content/save owner decision |
| Missing or malformed evidence | report/package digest, schema, provenance, artifact and source-blob checks | none; evidence must be replaced |

Any mapped automated failure remains stop-ship; a manual gate cannot waive corrupted or
missing evidence. New compatibility exceptions require their own owner approval and cannot
be smuggled through the final promotion gate.

## 9. Recorded decisions and exceptions

The package preserves these decisions instead of silently broadening them:

- approved hybrid local Addressables scene/content truth under
  `DEC-SCENE-DELIVERY-001` and `DEC-SCENE-DELIVERY-002`;
- owner-approved schema-1-only migration policy;
- PC-first current-authenticated-user launch scope; isolated-profile, Android packaged
  narrative, physical-device, and mobile readiness are not claimed;
- Unity `2022.3.62f3` cross-version export is rejected, not a supported fallback;
- future save schemas are preserved read-only and never downgraded;
- four dragon-cave source access values remain an explicit unresolved authority
  discrepancy; package assembly does not rewrite them;
- hosted contract fixtures are orchestration proof, not Unity, Player, save-runtime, or
  scene/content-hash evidence.

## 10. Reopen triggers

- Editor or exporter change: rerun exact-editor preflight, two clean builds, comparison,
  full QA, and compatibility owner review.
- Save schema or supported-path change: add old-save fixtures and prove upgrade,
  round-trip, atomic failure, idempotence, corruption/crash recovery, and downgrade rules.
- Scene or catalog addition/removal: keep `SCENE_SET_REVIEW_REQUIRED` stop-ship until the
  owner supersedes scene/content truth, then regenerate both manifests in one review.
- Narrative runtime/catalog/scene/persistence change: rerun editor, PlayMode, packaged
  Player, persist/resume, and editor/package material identity evidence.
- Failed migration telemetry: freeze promotion, retain generations/evidence, classify the
  failure, add a deterministic fixture, and reopen migration policy.

## 11. Recon coverage

The bounded evidence corpus comprised 15 existing build, QA, scene, save, narrative,
contract, policy, and test files totaling 3,825 lines. 2,981 lines were read: fourteen
files in full and 400 targeted lines of the 1,244-line reproducible-build runner covering
manifest verification, launch binding, artifact hashing, signed-ready manifests, and
comparison behavior. Generated scene-manifest bodies and unrelated runtime systems were
excluded; their identities are verified by SHA-256 and Git blob instead.
