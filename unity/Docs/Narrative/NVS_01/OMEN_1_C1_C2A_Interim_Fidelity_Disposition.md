# OMEN_1 C1/C2A Interim Narrative Fidelity Disposition

**Status date:** 2026-07-29
**Primary Codex mode:** narrative/content
**Source packet:** `omen1-a1-2026-07-22-v002`
**Coordination specification:** `nvs01-g1-2026-07-22-v002`
**Canonical SHA-256:** `b22c166310617657cf9716f988e697d4c4992b4d1877b6fd4d0a3311af9a9a1f`
**Tracking:** issue #134; merged PRs #261 and #262
**Reviewed baseline:** `main@2673df6`
**C1/C2A disposition:** `PASS FOR BOUNDED A1-TO-C1/C2A FIDELITY`
**Production realm handoff:** `CHANGES REQUIRED`

## Review Boundary

This independent A3 review compares the approved `OMEN_1` A1 packet with the
merged C1 catalog and transport-neutral C2A quest runtime. It covers catalog
identity and loading, deterministic dialogue and quest progression, encounter
request/result meaning, failure and Retry, manual report, consequence intents,
abandonment, and duplicate-delivery behavior.

C2A is deliberately not connected to production scenes, account realm
authority, save/reload, artifact or reward mutation, chapter persistence, or
player-facing presentation. Those areas, the complete G2 review, the final A2
fidelity gate, and user U1 playtest remain later work. Existing issue or PR
checklists that call A2 complete must therefore be read only as bounded C2A
evidence, not final NVS-01 acceptance.

## Fidelity Results

| Area | Result | Current evidence |
| --- | --- | --- |
| Packet and generated catalog identity | Pass | The A1 packet and runtime catalog are byte-identical and share the approved canonical SHA-256. |
| Catalog validation and authority | Pass | C1 validates the approved schema, version, references, localization, external-capability classification, and canonical bytes. C2A consumes the verified catalog rather than duplicating quest data. |
| State and objective order | Pass | `OFFERED`, `TALK_TO_VALERIUS`, `INVESTIGATE_SKY_CASTLE`, transient `FAILED`, `REPORT_TO_VALERIUS`, and `COMPLETED`, plus the talk, arena, and report objectives, remain in approved order. |
| Offer and dialogue paths | Pass | Deferral is non-mutating; acceptance supports both the direct and optional lore paths; dialogue nodes and choices are catalog-backed. |
| Champion deployment meaning | Pass for C2A | The explicit Deploy choice creates the typed Sky Castle request only after required capabilities and a committed eligible realm are supplied. No production scene route is claimed. |
| Encounter outcomes and recovery | Pass for C2A | Success, failure, cancellation, and unavailability remain distinct. Failure reaches the encouraging recovery node, Retry returns without penalty, and cancellation/unavailability do not grant progress. |
| Tear, report, and completion meaning | Pass for consequence intent | Success emits the retained Celestial Tear intent once and activates manual report. Report conclusion orders Gold, affinity, completion, and selected-realm Chapter 1 unlock intents without applying them in C2A. |
| Abandonment | Pass | Abandonment is unavailable during an active encounter, otherwise returns to `OFFERED`, clears active or unearned progress, and retains earned consequence intent. |
| Duplicate and mismatched delivery | Pass | Exact duplicates are idempotent; payload collisions, stale or late results, wrong correlations, hooks, events, states, or realms cannot progress the quest. |
| Player-facing source authority | Pass | The reviewed production C2A files contain none of the packet's 28 localized source literals; dialogue and choice text resolve through catalog localization. |
| Realm identity continuity | Changes required before production | A1/C1/C2A use uppercase IDs while the canonical launch catalog uses lowercase stable IDs. Ordinal comparison yields zero matching IDs, so a production realm adapter cannot pass the current C2A eligibility check unchanged. |
| Visible player presentation and errors | Not yet reviewable | C2A exposes typed dispositions and diagnostics but has no production UI consumer. Player-visible offer, unavailable, failure, Retry, and report presentation remain later integration work. |
| Persistence and D16 resume | Not yet reviewable | C2A has an in-memory commit seam only. Save migration, every D16 reload point, recovery, and atomic consequence application remain C3 scope. |
| Final A2 and U1 | Not reached | Final fidelity requires the integrated current-main implementation, save/reload evidence, complete player paths, G2, and user playtest. |

## Realm Identity Correction

The approved A1 packet and its generated runtime catalog currently declare:

```text
CROWNLANDS
STONEHOLD
ELDERGROVE
UMBRAL
```

The canonical four-realm launch catalog declares stable IDs:

```text
crownlands
stonehold
eldergrove
umbral
```

`Nvs01CatalogValidator` pins the uppercase sequence and `Nvs01QuestRuntime`
uses ordinal membership and equality. The current C2A implementation is
faithful to A1, but the two approved source surfaces do not share one stable
realm identity. This must be corrected before C2B, C3, or any account-realm
adapter is treated as production-ready.

The correction sequence is:

1. Narrative/content mode versions the A1 packet and adopts the canonical
   lowercase realm IDs without changing quest meaning, dialogue, choices,
   reward timing, or realm eligibility.
2. Coordination/review mode synchronizes G1 to the new packet version and hash,
   and records the compatibility rule for any previously emitted uppercase
   development contract or snapshot.
3. Engineering regenerates the runtime catalog, updates strict validation and
   tests, and connects the committed realm through one explicit identity
   boundary.
4. A3 re-reviews all four realm paths after integration.

Runtime code must not silently lowercase arbitrary input or maintain a second
parallel realm-ID authority. Unsupported or mismatched values must continue to
fail closed. No production save migration is required by current C2A because it
is not wired or persisted, but any later compatibility claim must be explicit
and tested.

## Acceptance for Re-Review

The production realm handoff can pass when:

- A1, G1, the generated catalog, validator, contracts, and tests share one
  canonical realm-ID representation;
- every canonical committed realm can enter the offer and create an exact
  encounter request without fallback or case coercion;
- mismatched, unknown, uncommitted, and cross-realm contexts fail visibly and
  without mutation;
- production UI resolves all player-facing content from approved catalog or
  localization authority;
- persistence proves every D16 resume point and preserves the Tear, pending
  report, completed effects, and selected-realm Chapter 1 identity;
- Gold, affinity, completion, and unlock commit atomically and exactly once;
- G2 reviews the integrated implementation and A3 performs final A2 against
  the frozen source;
- the user completes U1 across both dialogue paths, all encounter outcomes,
  Retry, success, manual report, save/reload, and duplicate-effect checks.

## Validation

- Source/artifact byte comparison: pass; both SHA-256 values are
  `b22c166310617657cf9716f988e697d4c4992b4d1877b6fd4d0a3311af9a9a1f`.
- Canonical realm comparison: four A1 IDs versus four launch-catalog IDs;
  ordinal intersection count `0`.
- Production source-literal scan: 28 localization values checked; `0` matches
  in the reviewed C2A runtime, contract, and Champion adapter files.
- Unity EditMode `AL.Tests.EditMode.Narrative.Nvs01QuestRuntimeTests`: `24/24`
  passed, `0` failed, skipped, or inconclusive.
- Unity EditMode `AL.Tests.EditMode.Narrative`: `43/43` passed, comprising
  `19/19` C1 catalog tests and `24/24` C2A runtime tests, with `0` failed,
  skipped, or inconclusive.
- Unity project import and script compilation completed without compiler
  errors before the focused test runs.

Not run: production PlayMode, Player build, scene round trip, persistence,
fault injection, device accessibility, reward mutation, or U1. These checks
are unavailable or outside the bounded C1/C2A implementation and remain
required before final A2.

## Impact

This disposition adds documentation only. It changes no narrative packet,
runtime catalog, contract, save, scene, asset, dependency, workflow,
performance, memory, build size, install size, or device compatibility. No
shared file is touched or locked.
