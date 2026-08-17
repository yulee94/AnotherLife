# Champion Encounter Application Contract

Status: C2 application boundary for issue #180
Primary mode: Codex engineering
Authoritative source disposition: blocked_required

## Binding source

The production authority is `GameDataCatalog/PhaseC/Phase_C7A_Champion_Authority_Convergence.md`, merged by PR #413 at `b5426cc9b52cbb06bb3c3987f597867fbe010f42`. That decision contains no production Champion records and retains all six Champion blockers. `GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md` supplies only the narrow skill precursor; it does not supply a Champion loadout.

Consequently, the currently supported production application result is `CatalogUnavailable`. This is a successful implementation of the source contract, not permission to synthesize a Champion. The application boundary contains a structural published-source path so a future reviewed #183 whole-catalog publisher can trigger the encounter without changing application semantics. Tests exercise that path with structural C1/C1b snapshots only; those fixtures are not content authority.

## Deterministic application contract

`ChampionEncounterApplicationGateway.Apply` is the supported engine-free application entry point.

1. Validate the authority identity and exact Git revision before inspecting runtime inputs.
2. Return `CatalogUnavailable` for the binding `blocked_required` disposition. Do not call the application owner and do not create encounter state.
3. A future published source must contain one resolved C1 `ChampionEncounterRequestPlan` and one authoritative C1b `CombatSkillLoadSessionSnapshot` with an atomic published loadout snapshot. Missing, partial, fallback, malformed, or rejected data returns a typed rejection before mutation.
4. C1 remains the owner of encounter eligibility, mode/realm/source correlation, deterministic selection already encoded in the resolved request, and initial state construction. C1b remains the owner of atomic skill-loadout publication. This gateway does not reimplement either planner.
5. After all validation succeeds, the gateway asks the injected application owner to apply exactly one C1-created `Created` state. A rejected application produces no receipt and may be retried by the caller with the same identity.
6. The application identity is the C1 `encounterResultId`. An exact receipt replay returns `DuplicateExact` and does not call the application owner. Reusing the identity with changed source semantics returns `CorrelationConflict`.
7. Retry after a terminal encounter remains a C1 request-planner concern and requires the C1 new-attempt/new-result identity rules. Scene re-entry with the same application receipt is an exact replay, not a reroll or second effect application.

## Ownership boundaries

- #183 owns reviewed catalog-set publication, schema/content/source versions, trusted hashes, complete cross-references, and source availability. This gateway consumes one whole publication; it does not load legacy nullable `GetChampion` or `GetSkill` values.
- #184 owns appearance draft/apply/verify/rollback and persistence compatibility. Appearance is never Champion identity.
- #168 owns boss/reward definitions and duplicate-safe reward receipts. This gateway grants no reward and applies no economy value.
- #174 owns battle simulation and battle application. This gateway does not simulate combat or apply battle outcomes.
- #137 owns profile/write authority, durable candidate application, commit certainty, replay/recovery, and save migration. This gateway emits no save mutation and claims no durable commit.
- C1 owns immutable encounter contracts/planners. C1b owns skill-load publication. Their files are unchanged by this slice.

## Failure behavior

| Condition | Status | Mutation |
| --- | --- | --- |
| Current C7A blocked source | `CatalogUnavailable` | none |
| Null/malformed authority | `InvalidSource` | none |
| Missing/rejected C1 plan | `InvalidSource` | none |
| Missing/non-authoritative/partial C1b load | `InvalidSource` | none |
| Null application or malformed receipt set | `InvalidDependency` | none |
| Exact prior application receipt | `DuplicateExact` | none |
| Same result ID with changed source fingerprint | `CorrelationConflict` | none |
| Application owner rejects initialization | `ApplicationRejected` | owner-defined attempt; no gateway receipt |
| Valid structural publication | `Applied` | one application call and one receipt |

Diagnostics are stable technical codes and contain no player-facing copy. The implementation is bounded, allocation-only at request time, contains no Unity object lookup, polling, coroutine, per-frame work, asset, dependency, save-schema, or package addition.

## Verification contract

Focused EditMode tests prove:

- identical unavailable inputs return identical non-mutating results;
- missing and malformed source fail before application;
- a complete resolved C1 plus authoritative C1b publication initializes and applies once;
- exact re-entry returns the existing receipt without reapplication;
- changed identity reuse is rejected;
- application failure is explicit and does not create a success receipt.

Production encounter content, source publication, persistence/recovery, battle/reward application, presentation, scene activation, Player/device evidence, balance, and user acceptance remain later owned gates.
