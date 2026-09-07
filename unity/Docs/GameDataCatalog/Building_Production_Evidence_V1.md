# Building production evidence v1

Refs #183. Audit base: `a958885b1c99c3bfaa1df191099b49761f2ff425`.

## Disposition

This is a source-only evidence packet, not a building production catalog. Both
`buildings.production_profiles` and `buildings.asset_refs` remain open. The live
`six-family-production-authority.v1.json` and its validator remain unchanged:
25 family blockers plus the separate global approval gate, no production output,
and no new runtime activation.

The owner-authorized recommendation is to resume this lane, reconcile newer
merged evidence, preserve accepted IDs and any explicit non-producing profiles,
and never invent rates, assets, or eligibility. No per-building non-producing
profile was found to preserve. An unavailable profile is therefore `null`, not an
empty output list or an invented zero-rate profile. Explicit non-producing
civic/defensive profiles remain the recommended form when their source exists.

## What changed since the six-family rebaseline

PR #750 (`69d41782199f5d5e6fcd8339a191f92593494894`) implemented the catalog-backed
production provider. PR #757 (`f805ddb24d9b838a0c87a5c2e38f9d24b39a6b8c`) registered
its fail-closed consumer. The audit pins the current loader, provider, consumer,
and provider tests, including later elapsed-time/profile-authority changes.
The historical C4A statement that no implementation exists is no longer current.
It is not edited or treated as approval for missing profile records.

The current consumer binds the ineligible ledger and has no contribution records.
The provider rejects ineligible or empty contributions. Its test-only eligible
catalog names `test-source-v1`; its numeric fixture values do not establish
production balance. Searching JSON under `unity/` for
`kingdom_production_profile_v1` and `ratePerLevelPerSecond` found no source records
before this audit packet was authored. The ongoing validator narrowly monitors
canonical GameData JSON for those profile markers; a new match requires a fresh
review, never automatic approval. This is not a claim that arbitrary external
source packets cannot exist.

GitHub #183 was observed CLOSED, with a closure event dated 2026-09-04, despite
its latest comment describing it as OPEN. PR #417 is CLOSED and unmerged, not
accepted source. Neither issue closure nor the older draft's proposal resolves
any authority blocker. This PR uses only `Refs #183` and does not alter issue state.

## Exact per-building contract

`building-production-evidence.v1.json` contains all 15 accepted IDs in v003 order.
Every row preserves the exact alias, name reference, Level 0–10 bounds, and cost,
duration, prerequisite, and realm-eligibility profile references. The pinned v003
source retains all 150 cost vectors, rounding rules, and duration values.
`ManaShrine` and `Mine` stay unavailable anchors.

For every building, production outputs, rates, caps, resource bindings, and failure
policy remain explicitly unavailable. `resource_output` and each WIRE placeholder
PNG path are recorded as rejected evidence, not promoted authority.

Town Hall and Workshop each retain their four realm-specific model tuples in
catalog order. All eight paths, GUIDs, and raw Git-blob SHA-256 values are checked
against prefab and metadata sources at the audit revision and current checkout.
The other 13 IDs retain no model bindings. These tuples establish partial existing
mapping evidence, not new final visual approval, dependency-closure qualification,
or a common asset shared by every realm/building.

## Validator and failure boundary

Run:

```text
python tools/game-data/test_validate_building_production_evidence.py
python tools/game-data/validate_building_production_evidence.py
python tools/game-data/validate_building_production_evidence.py --require-production-eligible
```

The ordinary audit exits 0 only for the exact valid blocked packet. The strict gate
exits 2 with `AL-BUILDING-EVIDENCE-BLOCKED`, zero output paths and zero activation
targets. Invalid source/packet evidence exits 1; it is not a valid blocked result.
The required repository hygiene job runs the tests and requires strict exit 2.

The packet is canonically serialized and checked against immutable source-derived
observations, so editing its claimed hash, status, flags, fields, order, or source
revision cannot self-approve it. Git sources are read only at the fixed full commit
ID, never at a caller-selected ref. Raw hashes are over committed bytes; current
text comparison permits CRLF-to-LF checkout conversion only. No output directory,
generator switch, runtime writer, or activation API exists in this validator.

Tests cover exact acceptance, identity/progression drift, omitted/reordered/extra
rows, source-path/revision/hash tampering, model SHA/GUID drift, realm-dimension
collapse, fake zero-rate and fixture-rate promotion, output/activation injection,
malformed/duplicate/nonfinite JSON, checkout portability, source absence/drift,
new profile detection, deterministic CLI output, and invalid-vs-blocked exit codes.

## Compatibility, rollback, and next source packet

No runtime, save, migration, scene, generated 3D asset, packaged catalog, dependency,
or gameplay value changes. Reverting this source-only PR removes its added audit
without changing the original fail-closed ledger or any runtime authority.
Unity/Player/device and new visual acceptance are not claimed by Python checks.

Next work must obtain an exact versioned source packet for all 15 production
profiles (including explicit non-producing decisions where justified), plus complete
reviewed per-building or explicitly realm-dimensional model mappings. Each profile
must specify output resource IDs, level domain/scaling, bounded rate/cap semantics,
realm applicability, online/offline elapsed-time policy, and failure behavior.
Do not copy fixture values or infer roles from building names. Preserve the eight
existing model tuples unless a reviewed superseding asset packet provides exact
provenance. Version the evidence and gate deliberately when source changes;
never mutate the live eligibility Boolean to bypass the missing source set.
