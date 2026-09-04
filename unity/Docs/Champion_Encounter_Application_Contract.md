# Champion Encounter Application Contract

Status: C2 application boundary for issue #180
Primary mode: engineering
Authoritative source: six-family production authority ledger accepted by #183 / PR #723

## Binding source

The production Champion/skill source set is `unity/Docs/GameDataCatalog/six-family-production-authority.v1.json` (`al_six_family_production_authority_v1`, `2026-09-04-v1`). Both `champions` and `skills` remain `blocked_required`, `productionEligible` is false, and generation activation targets are empty. C1/C1b planners were deleted as an unwired stack; this slice does not restore them.

Consequently, the currently supported production load result is `CatalogUnavailable`. That is a successful implementation of the source contract, not permission to synthesize a Champion, loadout, boss, or skill row. Structural `PublishedForTests` snapshots exist only so a future reviewed #183 publication can load without changing application semantics. Those fixtures are not content authority.

## Deterministic load contract

`ChampionEncounterLoadGateway.Start` is the engine-free load/application entry. `ChampionEncounterProductionLoadPath.StartFromCommittedRealm` is the production start/load path.

1. Require a committed valid realm (`stonehold` / `eldergrove` / `crownlands` / `umbral`). Uncommitted or unknown realm returns `InvalidSource` with zero mutation.
2. Require the request to name the exact #183 source-set version and SHA-256. Stale or mismatched snapshot/hash returns `CatalogUnavailable`.
3. Return `CatalogUnavailable` while the ledger marks Champion or skill families blocked, or production ineligible. Do not call the application owner and do not create encounter state.
4. A published source must carry exact actor, caster, boss, and loadout identities plus the authored four-slot WIRE order (`realm_strike`, `renewing_guard`, `warzone_burst`, `warmaster_breaker`). Mixed Champion-catalog / WIRE hybrid slots return `InvalidSource`. Reordered or incomplete slots return `InvalidSource`.
5. After validation succeeds, the gateway asks the injected application owner to apply exactly one in-memory load snapshot. A rejected application produces no receipt. The owner must not persist results or grant rewards.
6. The application identity is the encounter id. Exact receipt replay returns `DuplicateExact` and does not call the owner. Reusing the identity with a changed source fingerprint returns `CorrelationConflict`.

## Ownership boundaries

- #183 owns reviewed catalog-set publication, schema/content/source versions, trusted hashes, complete cross-references, and source availability. This gateway consumes that whole publication identity; it does not load legacy nullable `GetChampion` or `GetSkill` values as new authority.
- #184 owns appearance draft/apply/verify/rollback. Appearance is never Champion identity.
- #168 owns boss/reward definitions and duplicate-safe reward receipts. This gateway grants no reward and applies no economy value.
- #174 owns battle simulation and battle application. This gateway does not simulate combat.
- #137 owns profile/write authority, durable candidate application, commit certainty, replay/recovery, and save migration. This gateway emits no save mutation.
- First-session `FirstFightCatalog` / `SkillCaster` hybrid overlay remains a labeled first-session path until C3 runtime migration. C2 does not restore C1 planners.

## Failure behavior

| Condition | Status | Mutation |
| --- | --- | --- |
| Current #183 blocked Champion/skill source | `CatalogUnavailable` | none |
| Stale source-set version or hash | `CatalogUnavailable` | none |
| Null/malformed authority | `InvalidSource` | none |
| Uncommitted or invalid realm | `InvalidSource` | none |
| Mixed/hybrid skill identities | `InvalidSource` | none |
| Invalid authored slot order | `InvalidSource` | none |
| Missing/mismatched actor/caster/boss/loadout | `InvalidSource` | none |
| Null application or malformed receipt set | `InvalidDependency` | none |
| Exact prior load receipt | `DuplicateExact` | none |
| Same encounter id with changed source fingerprint | `CorrelationConflict` | none |
| Application owner rejects initialization | `ApplicationRejected` | owner-defined attempt; no gateway receipt |
| Valid structural publication | `Loaded` | one in-memory application call and one receipt |

Diagnostics are stable technical codes and contain no player-facing copy. The implementation is bounded, allocation-only at request time, and adds no save-schema, package, coroutine, or per-frame work.

## Verification contract

Focused EditMode tests prove:

- the production factory pins the live #183 Champion/skill source-set identity;
- identical unavailable inputs return identical non-mutating results;
- missing, stale, mixed, hybrid, and invalid source fail before application;
- a complete published structural catalog loads and applies once;
- exact re-entry returns the existing receipt without reapplication;
- changed identity reuse is rejected;
- application failure is explicit and does not create a success receipt;
- the production scene start path calls `ChampionEncounterProductionLoadPath` without save or reward APIs.

C3 combat-result persistence, reward application, and presentation remain later owned gates.
