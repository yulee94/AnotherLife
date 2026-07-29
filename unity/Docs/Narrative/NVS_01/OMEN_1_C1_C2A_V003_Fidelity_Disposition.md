# OMEN_1 v003 C1/C2A Narrative Fidelity Re-Review

**Status date:** 2026-07-29
**Primary Codex mode:** narrative/content
**Source packet:** `omen1-a1-2026-07-29-v003`
**Coordination specification:** `nvs01-g1-2026-07-29-v004`
**Canonical SHA-256:** `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732`
**Tracking:** issue #365 Step 4; merged PRs #367, #373, #379, #377, and #378
**Reviewed baseline:** `main@4d07d1e7984855ada78a10a812bb163d0cd77831`
**C1/C2A v003 disposition:** `PASS FOR BOUNDED FOUR-REALM FIDELITY`
**Final production G2/A2/U1 disposition:** `NOT REACHED`

## Review Boundary

This A3 re-review closes the realm-identity fidelity loop identified by the
historical v002 C1/C2A disposition. It compares the approved A1 v003 packet
with the exact generated C1 runtime catalog and the transport-neutral C2A
runtime after the committed-realm adapter was synchronized through issue #365.

The historical
`OMEN_1_C1_C2A_Interim_Fidelity_Disposition.md` remains immutable evidence of
the v002 uppercase-ID defect. This document does not rewrite that result or
claim that v002 was compatible with the canonical launch identity.

The reviewed C2A runtime is still in-memory and transport-neutral. This
disposition does not claim production scene routing, player-visible UI,
durable save/reload, artifact or reward mutation, chapter persistence,
complete C3/C4 integration, final G2, final A2, or user U1 acceptance.

## Canonical Identity

The sole narrative source and generated runtime artifact are byte-identical:

| Contract item | Accepted value |
| --- | --- |
| Source | `unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json` |
| Generated runtime artifact | `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` |
| Packet version | `omen1-a1-2026-07-29-v003` |
| Canonical byte length | `8,317` |
| Canonical SHA-256 | `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732` |
| Line contract | UTF-8, LF-only, no BOM, final LF retained |
| Eligible realm order | `crownlands`, `stonehold`, `eldergrove`, `umbral` |

The current MainQuestLine authority and Chapter 00 external source reference
also resolve to A1 v003. No current canonical MainQuestLine reference remains
on the v002 identity.

## Four-Realm Fidelity Matrix

The adapter consumes the typed committed realm snapshot from the canonical
launch identity contract. It maps only the four known enum values to their
approved lowercase stable IDs; it does not lowercase, case-fold, or normalize
arbitrary strings.

| Committed realm snapshot | Adapter output | C2A offer/encounter realm | Result |
| --- | --- | --- | --- |
| `RealmId.Crownlands` + `CommittedValid` | `crownlands` | `crownlands` | Match |
| `RealmId.Stonehold` + `CommittedValid` | `stonehold` | `stonehold` | Match |
| `RealmId.Eldergrove` + `CommittedValid` | `eldergrove` | `eldergrove` | Match |
| `RealmId.Umbral` + `CommittedValid` | `umbral` | `umbral` | Match |

For every row, the focused runtime matrix verifies that the exact realm ID is
retained in the typed Sky Castle encounter request together with the approved
quest, state, objective, hook, location, success/failure/cancel/unavailable
events, correlation identity, and `Kingdom` return scene. Exact duplicate
requests reuse the original request and correlation without another commit.

## Narrative Fidelity Results

| Area | Result | Current evidence |
| --- | --- | --- |
| Packet and generated catalog identity | Pass | A1 and the runtime artifact are byte-identical at the accepted version, length, and SHA-256. |
| Realm eligibility | Pass | All four approved committed realms enter through their exact lowercase launch IDs in the approved order. No realm was added, removed, merged, or given different quest eligibility. |
| State order | Pass | `OFFERED`, `TALK_TO_VALERIUS`, `INVESTIGATE_SKY_CASTLE`, transient `FAILED`, `REPORT_TO_VALERIUS`, and `COMPLETED` remain unchanged. |
| Objective order | Pass | `OBJ_OMEN_1_TALK`, `OBJ_OMEN_1_ARENA`, and `OBJ_OMEN_1_REPORT` remain unchanged and ordered. |
| Offer and dialogue paths | Pass | The direct accept path and optional lore path remain catalog-backed; deferral remains non-mutating. All eight dialogue node IDs are unchanged. |
| Encounter meaning | Pass for C2A | Explicit Champion deployment creates the typed Sky Castle request only after the approved capability and committed-realm checks. No production scene route is claimed. |
| Failure and Retry | Pass for C2A | Failure remains encouraging, transient, and penalty-free. Retry is explicit and creates a new request only after the retry choice commits. Cancelled and unavailable remain distinct technical outcomes. |
| Celestial Tear and report | Pass for consequence intent | Success emits the retained Tear intent once and activates the manual report. Report conclusion orders Gold, affinity, completion, and selected-realm Chapter 1 unlock intents without claiming durable application. |
| Abandonment and duplicates | Pass | Active-encounter abandonment remains blocked; other abandonment, duplicate delivery, stale results, collisions, and mismatched realm/correlation data remain non-mutating or idempotent as approved. |
| Player-facing source text | Pass | The 28 localization entries, title, description, objective text, dialogue lines, choices, artifact meaning, reward meaning, and report meaning come from the byte-identical A1 source. The realm-ID correction changed none of them. |
| MainQuestLine continuity | Pass | The current prologue authority and Chapter 00 source authority now reference A1 v003, and all 15 MainQuestLine component hashes match their exact bytes. |

The only A1 v002-to-v003 semantic change was the four eligible realm
identifiers and the packet version required to publish that correction.
Approved dialogue, choices, ordering, objectives, encounter meaning, Retry,
artifact retention, rewards, manual report, chapter unlock, and localization
meaning remain unchanged.

## Fail-Closed Identity Matrix

The synchronized implementation rejects without aliasing or mutation:

- the exact v002 packet version and hash;
- v002 snapshots, requests, and results;
- uppercase, mixed-case, culture-sensitive, unknown, or blank launch IDs;
- duplicate, missing, extra, or reordered catalog realm IDs;
- `None`, undefined enum values, invalid persisted identity, stale catalog
  versions, unavailable profiles/catalogs, and uncommitted profiles;
- wrong-realm requests and results;
- missing capabilities and invalid, late, mismatched, or colliding events.

No runtime path silently lowercases v002 input, accepts an uppercase alias, or
grants v003 progress or consequences to v002 state.

## Validation

Fresh evidence on `main@4d07d1e7984855ada78a10a812bb163d0cd77831`:

- source/runtime `cmp`: exact byte match;
- source/runtime byte length: `8,317` each;
- source/runtime SHA-256:
  `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732`;
- Unity 2022.3.62f3 focused EditMode:
  `AL.Tests.EditMode.Narrative.Nvs01CatalogTests` and
  `AL.Tests.EditMode.Narrative.Nvs01QuestRuntimeTests`;
- result: `56/56` passed, `0` failed, skipped, or inconclusive;
- catalog fixture: `21/21` passed;
- runtime fixture: `35/35` passed;
- four-realm request matrix:
  `EncounterRequestIsExactForEveryRealmAndDuplicatesReuseItsCorrelation`;
- four-realm adapter matrix:
  `CommittedRealmAdapterEmitsOnlyCanonicalLaunchIds`;
- stale/invalid adapter matrix:
  `RealmAdapterFailsClosedForUnavailableUncommittedInvalidUndefinedAndStaleIdentity`;
- case/unknown rejection:
  `UppercaseMixedCaseAndUnknownLaunchRealmIdsFailClosed`;
- complete v002 rejection:
  `V002PacketHashAndRealmIdentitySnapshotsRequestsAndResultsFailClosed`.

Hosted evidence:

- PR #377 run `30485655151`: policy/classify, repository/hygiene, and Android
  unit-debug passed for the synchronized runtime;
- PR #378 run `30486222545`: policy/classify, repository/hygiene, and Android
  unit-debug passed for the current MainQuestLine v003 references.

Not run for this documentation-only re-review: production PlayMode, Player
build, device profiling, scene round trip, durable save/reload, reward
mutation, fault-injected consequence recovery, accessibility presentation, or
U1. These are not available in the bounded C1/C2A slice and remain required by
the G1 C3/C4, final G2/A2, and U1 gates.

## Decision and Handoff

Issue #365 Step 4 passes for the corrected v003 realm-identity scope:

- A1, G1, the generated runtime artifact, validators, contracts, adapter, and
  focused tests share the same canonical lowercase realm identity;
- all four committed canonical realms reach the exact C2A request boundary;
- stale, invalid, unavailable, uncommitted, case-drifted, wrong-realm, and
  v002 identities fail closed without aliasing or mutation;
- no narrative or player-facing meaning drift was found.

This is not final NVS-01 A2 approval. Issue #134 still owns the production
implementation sequence, including C3 persistence and transaction behavior,
C4 integration/evidence, and the complete G2 package. Final A2 must review the
integrated production implementation, and the user still owns U1 playtest and
acceptance. Issue #186 retains every broader Android preview consumer,
presentation, and source-fidelity change outside the already-merged narrow
identity exception.

## Impact

This disposition adds documentation only. It changes no narrative packet,
runtime catalog, contract, save, scene, asset, dependency, workflow,
performance, memory, package size, install size, device compatibility, or
shared file.
