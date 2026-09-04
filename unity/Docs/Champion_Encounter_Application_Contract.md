# Champion Encounter Application Contract

Status: C4 durable consequences for issue #180
Primary mode: engineering
Authoritative source: six-family production authority ledger accepted by #183 / PR #723

## Binding source

The production Champion/skill source set is `unity/Docs/GameDataCatalog/six-family-production-authority.v1.json` (`al_six_family_production_authority_v1`, `2026-09-04-v1`). Both `champions` and `skills` remain `blocked_required`, `productionEligible` is false, and generation activation targets are empty. C1/C1b planners were deleted as an unwired stack; this slice does not restore them.

Consequently, the currently supported production load result is `CatalogUnavailable`. That is a successful implementation of the source contract, not permission to synthesize a Champion, loadout, boss, or skill row. Structural `PublishedForTests` snapshots exist only so a future reviewed #183 publication can load without changing application semantics. Those fixtures are not content authority.

## Deterministic load contract

`ChampionEncounterLoadGateway.Start` is the engine-free load/application entry. `ChampionEncounterProductionLoadPath.StartFromCommittedRealm` is the production start/load path. `ChampionEncounterRuntimeGateway.Apply` is the C3 runtime consumer of that C2 snapshot/receipt. `ChampionEncounterConsequenceGateway.Apply` is the C4 durable-consequence orchestrator.

1. Require a committed valid realm (`stonehold` / `eldergrove` / `crownlands` / `umbral`). Uncommitted or unknown realm returns `InvalidSource` with zero mutation.
2. Require the request to name the exact #183 source-set version and SHA-256. Stale or mismatched snapshot/hash returns `CatalogUnavailable`.
3. Return `CatalogUnavailable` while the ledger marks Champion or skill families blocked, or production ineligible. Do not call the application owner and do not create encounter state.
4. A published source must carry exact actor, caster, boss, and loadout identities plus the authored four-slot WIRE order (`realm_strike`, `renewing_guard`, `warzone_burst`, `warmaster_breaker`). Mixed Champion-catalog / WIRE hybrid slots return `InvalidSource`. Reordered or incomplete slots return `InvalidSource`.
5. After validation succeeds, the gateway asks the injected application owner to apply exactly one in-memory load snapshot. A rejected application produces no receipt. The owner must not persist results or grant rewards.
6. The application identity is the encounter id. Exact receipt replay returns `DuplicateExact` and does not call the owner. Reusing the identity with a changed source fingerprint returns `CorrelationConflict`.
7. C3 runtime apply consumes only a `Loaded` C2 receipt. Hybrid, unavailable, invalid, or non-finite identity/reference/value input is typed and non-mutating. Loader/caster/combat/boss start do not overlay hard-coded slots, `FindFirstValid`, or uncommitted-realm fallbacks. Boss death does not roll loot or mutate rewards.
8. C4 applies one duplicate-safe encounter result only for `AuthoritativeQuest`. It orchestrates typed #168 boss/reward receipts and #137 profile/write authority and never writes saves or computes loot itself. Practice, first-session labeled practice, DevelopmentDemo, AuthoritativeBoss, uncommitted realm, and missing NVS correlation/realm identity fail closed with zero mutation. Exact result replay returns `DuplicateExact`. Changed reuse returns `CorrelationConflict`. Presentation/route evidence remains C5.

## Ownership boundaries

- #183 owns reviewed catalog-set publication, schema/content/source versions, trusted hashes, complete cross-references, and source availability. This gateway consumes that whole publication identity; it does not load legacy nullable `GetChampion` or `GetSkill` values as new authority.
- #184 owns appearance draft/apply/verify/rollback. Appearance is never Champion identity.
- #168 owns boss/reward definitions and duplicate-safe reward receipts. C4 asks that typed surface for one victory receipt and does not compute credits, items, or seeds. Practice/fallback paths never call #168.
- #174 owns battle simulation and battle application. This gateway does not simulate combat.
- #137 owns profile/write authority, durable candidate application, commit certainty, replay/recovery, and save migration. C4 requires a writable #137 snapshot and commits only through the injected profile-commit owner.
- First-session `FirstFightCatalog` / `SkillCaster` practice remains labeled practice and cannot silently become `AuthoritativeQuest`.
- NVS correlation and committed realm identity are preserved on the C4 receipt. C4 does not bypass the owning NVS adapter.

## Failure behavior

| Condition | Status | Mutation |
| --- | --- | --- |
| Current #183 blocked Champion/skill source | `CatalogUnavailable` | none |
| Stale source-set version or hash | `CatalogUnavailable` | none |
| Null/malformed authority | `InvalidSource` | none |
| Uncommitted or invalid realm | `InvalidSource` / C4 `InvalidInput` | none |
| Mixed/hybrid skill identities | `InvalidSource` / runtime `HybridRejected` | none |
| Invalid authored slot order | `InvalidSource` | none |
| Missing/mismatched actor/caster/boss/loadout | `InvalidSource` | none |
| Null application or malformed receipt set | `InvalidDependency` | none |
| Exact prior load receipt | `DuplicateExact` | none |
| Same encounter id with changed source fingerprint | `CorrelationConflict` | none |
| Application owner rejects initialization | `ApplicationRejected` | owner-defined attempt; no gateway receipt |
| Invalid/non-finite runtime identity or numeric input | runtime `InvalidInput` | none |
| Practice / first-session labeled practice | C4 `PracticeSuppressed` | none |
| DevelopmentDemo / AuthoritativeBoss | C4 `ModeRejected` | none |
| Missing NVS correlation or quest identity | C4 `InvalidInput` | none |
| Missing #168/#137 owner | C4 `InvalidDependency` | none |
| #137 not writable or profile mismatch | C4 `ProfileWriteUnavailable` | none |
| #168 unavailable/invalid/conflict | C4 `RewardAuthorityUnavailable` | none |
| Exact prior C4 result | C4 `DuplicateExact` | none |
| Same C4 result id with changed identity | C4 `CorrelationConflict` | none |
| Valid structural publication | `Loaded` then runtime `Applied` | one in-memory application call, one receipt, one runtime bind |
| AuthoritativeQuest victory/defeat with writable #137 | C4 `Applied` | one #168 plan on victory, one #137 commit, one result receipt |

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
- the production scene start path calls `ChampionEncounterProductionLoadPath` without save or reward APIs;
- C3 runtime apply consumes a published C2 snapshot as the sole production load authority;
- hybrid, uncommitted-realm, and non-finite input reject with zero mutation;
- first-session practice cannot become `AuthoritativeQuest`;
- boss death does not call loot/reward services;
- C4 AuthoritativeQuest victory applies once through #168 and #137;
- C4 exact replay is duplicate-safe and changed reuse is `CorrelationConflict`;
- C4 practice, first-session labeled practice, fallback, uncommitted, and missing/unavailable authority paths fail closed with zero save mutation;
- C4 defeat commits the encounter result without issuing a #168 reward.

C5 presentation/route evidence remains a later owned gate.
