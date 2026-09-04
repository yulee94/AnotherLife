# Realm-Slice Evidence Registry and Rollback Runbook

Status: operational control for `RSQ-PROTOCOL-v1.0.0`.

The canonical initial registry is
`unity/Docs/RealmSliceEvidenceRegistry/registry.v1.json`. The machine policy is
`tools/qa/realm_evidence_registry_policy.v1.json`; the portable contracts are
`unity/SharedContracts/realm-slice-evidence-pack.schema.json` and
`unity/SharedContracts/realm-slice-evidence-registry.schema.json`.

## 1. Authority and sequence

Qualification opens only in this order:

1. `Stonehold`
2. `Eldergrove`
3. `Crownlands`
4. `Umbral`

Each realm maintains independent `Adventure3D` and `Kingdom2_5D` qualification and
owner-approval states, plus separate creative/visual approval and owner authorization.
Only one realm has `entryGate=OPEN`. The next realm does not open until the current realm
has current signed packs for both modes and explicit owner records for 3D, 2.5D,
creative/visual approval, and the exact advancement action.

The registry never treats CI, a digest, a PR merge, task completion, elapsed time, or owner
silence as approval.

## 2. Signed pack ingestion

A pack is one immutable realm/mode cube. It contains the exact 72 harness row manifests
produced by `expand_run_specs` for that realm and mode, each independently verified against
`unity/SharedContracts/realm-slice-evidence-manifest.schema.json`. The registry calls the
harness `verify_manifest` / `verify_review_signature` path so a present
`reviewerSignature` is never trusted without `ssh-keygen -Y` verification. Shared
candidate, packet, build, catalog, save-fixture, and full-QA identities must match across
every row. The thin envelope HMAC only binds that verified cube; it is not a substitute
for the detached row signatures.

`hmac-sha256-v1` is an operator-managed authentication method. The shared secret stays in a
restricted environment variable and is never stored in the pack, registry, repository,
logs, command line, or board record. A digest or `.sha256` sidecar without the authenticated
signature is rejected. The HMAC proves possession of the registered secret; it is not an
owner decision or release approval.

Copy `tools/qa/realm_evidence_keyring.example.json` outside the repository. Its two
policy-pinned key IDs resolve separate `AL_RSQ_EVIDENCE_SIGNING_SECRET` and
`AL_RSQ_OWNER_SIGNING_SECRET` environment variables. Do not collapse those trust scopes.

Verify the empty registry:

```powershell
python tools/qa/realm_evidence_registry.py verify `
  --registry unity/Docs/RealmSliceEvidenceRegistry/registry.v1.json
```

Ingest a pre-signed mode pack:

```powershell
python tools/qa/realm_evidence_registry.py ingest `
  --registry unity/Docs/RealmSliceEvidenceRegistry/registry.v1.json `
  --pack <immutable-pack.json> `
  --artifact-root <immutable-pack-artifact-root> `
  --keyring <restricted-evidence-keyring.json> `
  --allowed-signers <restricted-allowed-signers> `
  --now-utc <actual-utc>
```

The command fails closed for a closed realm, missing or extra fields, dirty source,
non-full/failed QA, fewer or more than the exact mode's twelve checks, incomplete run
coverage, failed/unreviewed rows, missing/tampered artifacts, owner/reviewer collision,
wrong realm ordinal, candidate/packet mode mismatch, cross-mode artifact identity, unknown
signer, bad signature, future-dated signature, or expired `validUntilUtc`. A replacement
pack is accepted only after a signed scoped reopen, must use a new ID, name the current
packet in `supersedes`, and carry a distinct full-QA run completed strictly after the reopen. Prior
packets remain in history and `rerunRequired` remains true until owner re-approval.

The external authority that owns an evidence window sets `validUntilUtc`. This registry does
not invent or copy a numerical window.

## 3. Owner decisions and advancement

Owner decisions are immutable signed JSON records. Each record binds the authoritative
Kanban task/event, exact packet ID/hash/candidate references, decision, limitations,
baseline ID, UTC time, and supersession. Record them separately with `kind=MODE` for each
mode, then `kind=CREATIVE_VISUAL`, then `kind=AUTHORIZATION`:

```powershell
python tools/qa/realm_evidence_registry.py approve `
  --registry unity/Docs/RealmSliceEvidenceRegistry/registry.v1.json `
  --decision <signed-owner-decision.json> `
  --keyring <restricted-owner-keyring.json> `
  --allowed-signers <restricted-allowed-signers> `
  --now-utc <actual-utc>
```

Allowed advancement actions are exactly `ADVANCE_TO_ELDERGROVE`,
`ADVANCE_TO_CROWNLANDS`, `ADVANCE_TO_UMBRAL`, and `COMPLETE_REALM_SEQUENCE` for their
corresponding realms. Any out-of-order or premature action is rejected without mutating the
registry.

## 4. Reopen impact procedure

Create a new game-owner-signed reopen record with a unique `reopenId`, approved trigger ID, affected realm,
smallest proven `affectedModes`, explicitly dependent later realms, impact rationale,
authority task/event IDs, and UTC time. Then run:

```powershell
python tools/qa/realm_evidence_registry.py reopen `
  --registry unity/Docs/RealmSliceEvidenceRegistry/registry.v1.json `
  --record <signed-reopen-record.json> `
  --keyring <restricted-owner-keyring.json> `
  --allowed-signers <restricted-allowed-signers> `
  --now-utc <actual-utc>
```

The affected mode becomes `REOPENED`, its owner approval becomes historical rather than
current, its content path becomes `DISABLED_PENDING_RERUN`, and `rerunRequired=true`.
Creative approval reopens and advancement is suspended because both depend on the changed
mode. An unaffected mode remains qualified, approved, enabled, and bound to its retained
baseline. The next unexercised realm gate becomes `SUSPENDED`. Approved downstream evidence
is invalidated only when named by the impact analysis; uncertainty expands the dependency
list and fails closed.

Every correction requires a complete replacement pack. A partial rerun, edited artifact,
resigned old manifest, or spot check cannot clear `rerunRequired`.

## 5. Scoped rollback and containment

Rollback is containment, not approval. Before action:

1. Freeze qualification and preserve the triggering pack, all prior packs, signed decisions,
   event history, defects, logs, captures, and artifact hashes.
2. Hash and byte-preserve every affected save generation and recovery ledger in the approved
   restricted recovery location. Never test against or reset the live save.
3. Identify the affected realm/mode's `lastOwnerApprovedBaseline`; if it is missing, unsafe,
   incompatible, or unverifiable, keep the content path disabled and remain fail closed.
4. Disable only the unapproved realm/mode path. Do not disable or revert the other mode or an
   unrelated realm without a recorded dependency impact.
5. Restore only the exact approved baseline assets/configuration through a scoped revert PR
   or reversible content/config selector. Never force-reset `main`, rewrite Git/registry
   history, regenerate hashes to conceal drift, or down-migrate saves.
6. Verify the retained baseline packet, decision, content identity, and save compatibility.
7. Record the rollback and keep `rerunRequired=true` until the complete impacted pack is run,
   signed, independently reviewed, re-ingested under a new packet ID, and owner-approved.

The signed rollback record must set `preserveEvidence=true`, `preserveSaves=true`, and
`disableOnlyAffectedPaths=true`. Its `baselineRefs` and `saveSnapshots` must exactly match
the retained signed packet, owner decision, and save-fixture hashes:

```powershell
python tools/qa/realm_evidence_registry.py rollback `
  --registry unity/Docs/RealmSliceEvidenceRegistry/registry.v1.json `
  --record <signed-rollback-record.json> `
  --artifact-root <retained-baseline-artifact-root> `
  --keyring <restricted-owner-keyring.json> `
  --allowed-signers <restricted-allowed-signers> `
  --now-utc <actual-utc>
```

The command refuses a mode that is not reopened, has no retained owner-approved baseline,
or requests broader/destructive handling. It records the exact baseline target and changes
only the selected mode to `ROLLED_BACK_TO_APPROVED_BASELINE`; evidence, decisions, and saves
remain preserved.

## 6. Audit and recovery

Every accepted mutation appends a monotonic event with its predecessor hash, resulting-state
hash, policy-pinned signer ID, HMAC signature, and own SHA-256. The registry pins the exact
policy digest and is also hash-sealed. `verify` checks the current seal, state invariants,
sequence, authenticated event chain, and signed record binding. For a non-empty registry it
also requires `--keyring`. `verify-append-only --base <trusted-base> --registry <candidate>`
rejects removed or rewritten events and records. Never hand-edit a registry. Commit every
accepted mutation as its own scoped PR so Git history and the internal chain agree.

If verification fails, preserve the file, disable the affected unapproved content path
outside the registry through existing reversible authority, recover the last verified
registry commit, and record a new incident/reopen event. Do not recompute hashes over altered
history or delete the corrupt copy.

## 7. Validation

Run:

```powershell
python tools/qa/test_realm_evidence_registry.py -v
python tools/qa/realm_evidence_registry.py verify `
  --registry unity/Docs/RealmSliceEvidenceRegistry/registry.v1.json
```

The hosted repository hygiene job runs the contract suite. Runtime evidence packs and real
owner decisions remain downstream work; the empty Stonehold-open registry is configuration,
not qualification or approval.

## 8. Recon coverage

The bounded implementation corpus comprised nine protocol, governance, release-evidence,
policy, schema, test, and workflow files totaling 3,005 lines. All 3,005 lines were read in
full (100%). Generated evidence bodies, binary captures, unrelated runtime systems, and
external numerical authority cards were excluded.
