# Battle Caller and Interface Inventory

Issue: #174 — Battle integrity

Phase: B1 — pure computation contracts and simulator

Baseline: `a80515983dec5ef89c47dfa61aa277e99057f675`

## B1 authority boundary

`AL.Battle.Contracts`, `AL.Battle.Validation`, and `AL.Battle.Computation` are a dormant, pure boundary. They define immutable request/result snapshots, strict validation, checked fixed-point arithmetic, SHA-256 entropy, deterministic battle computation, proposed rewards, and retained vectors.

B1 does not register the new computation, replace the legacy interface, mutate game state, grant rewards, update quests, emit events, deliver notifications, write saves, or change scenes/UI. Preview results use the `preview:` result namespace and are permanently application-ineligible.

## Current production path

| Boundary | Current location | Current behavior | B1 disposition |
| --- | --- | --- | --- |
| Interface | `Assets/AL/Scripts/Core/Interfaces/IBattleSimulator.cs` | Accepts mutable `AL.Data.Runtime.BattleRequest` and returns mutable `BattleReport`. | Unchanged. |
| Registration | `Assets/AL/Scripts/Core/Bootloader.cs:913` | Constructs `AL.Battle.Simulator.DeterministicBattleSimulator` and publishes it as `IBattleSimulator`. | Unchanged; new computation is not registered. |
| Production caller | `Assets/AL/Scripts/Utilities/DemoInitializer.cs:378` | Builds a mutable legacy request and resolves `IBattleSimulator` through `ServiceLocator`. | Unchanged. This is the only runtime call site found at the B1 baseline. |
| Legacy implementation | `Assets/AL/Scripts/Battle/Simulator/DeterministicBattleSimulator.cs` | Reads research services, uses floating-point/System.Random behavior, writes logs, and advances a win quest. | Unchanged. Its mixed responsibilities are migration evidence, not behavior copied into the new authority boundary. |
| Legacy data models | `Assets/AL/Scripts/Data/Runtime/BattleModels.cs` | Mutable request/report, lists, prose contributions, and earned-looking reward fields. | Unchanged. No implicit conversion is introduced in B1. |

Repository search at the B1 baseline found no other runtime invocation of `IBattleSimulator.Simulate`, `DeterministicBattleSimulator.Simulate`, or the new `DeterministicBattleComputation`.

## New pure interface

The B1 entry point is:

```text
DeterministicBattleComputation.Compute(BattleComputationRequest)
    -> BattleComputationResult
```

The request binds game/catalog/profile/request/battle/result identities, expected consumer, execution mode, stable battle type ID, determinism version, explicit 32-byte seed, immutable catalog/army/opponent/context snapshots, and versioned terrain/rules/reward profiles.

The result either contains diagnostics with no value or a validated immutable value containing powers, rounds, outcome, troop loss partitions, proposed rewards, technical contributions, all authority hashes, and a canonical computation hash.

## Required adapter before production migration

A later phase must add an explicit adapter; callers must not construct the new graph ad hoc.

The adapter must:

1. Resolve every mutable/live input before entering computation.
2. Build canonical, sorted troop stacks with catalog-bound versions and hashes.
3. Map legacy realm and terrain inputs to explicit stable profiles; substring fallback is prohibited.
4. Bind the expected result consumer, execution mode, request/battle/result IDs, determinism version, and explicit seed.
5. Snapshot research, commander, morale, encounter, rules, and reward provenance without service reads inside computation.
6. Keep preview IDs in the `preview:` namespace and prevent preview application.
7. Return validation failure to the caller without falling back to the legacy simulator.

## Later result-application phase

Result application remains separate from computation. A later planner must compare expected versus actual authoritative state, define idempotency and replay behavior, apply troop/reward/quest/territory effects transactionally, persist before presentation, and delegate boss-item rewards to the existing boss-reward authority. For Boss results, the stable B1 handoff correlation is `BattleId + BattleResultId + OpponentSha256`; B1 generates no boss-item operation or loot roll. No application service may infer missing computation inputs or silently recompute a result.

## Migration gates

The production registration can change only after all of the following exist and pass:

- caller adapter tests for every current caller;
- authoritative result-application and persistence tests;
- replay/idempotency and stale-state rejection tests;
- reward, quest, inventory, territory, and boss-authority integration tests;
- save/load recovery tests;
- explicit removal or quarantine plan for legacy side effects;
- hosted required checks on the migration pull request.

Until those gates are complete, `Bootloader`, `IBattleSimulator`, `DemoInitializer`, and the legacy simulator remain the production path.
