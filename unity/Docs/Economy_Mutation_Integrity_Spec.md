# Economy Mutation Integrity Specification

**Status date:** 2026-07-15  
**Tracking issue:** #163  
**Specification owner:** GPT  
**Implementation owner:** Codex engineering mode  
**Policy dependency:** `unity/Docs/Save_Semantic_Compatibility_Policy.md`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

## 1. Goal

Make resources and Warzone Credits non-exploitable, checked-arithmetic safe, malformed-save aware, deterministic, and composable into later durable transactions without changing balances, costs, production rates, reward amounts, or narrative meaning.

Delivery is intentionally split:

- **Stage A — mutation integrity now:** validation, checked arithmetic, typed results, safe compatibility wrappers, event behavior, and online production integrity.
- **Stage B — durable transaction integration after #137:** committed save result, prepared multi-domain mutation, rollback/recovery, idempotency ledger, and caller migration for rewards/purchases/captures/NVS consequences.

Stage A closes immediate negative/overflow exploits but does not by itself close every #163 acceptance criterion. Issue #163 remains open until Stage B and required caller integration pass.

## 2. Verified current defects

### `LocalResourceService`

- null wallet entries can throw through LINQ predicates;
- `AddResource(type, negative)` subtracts value;
- `ConsumeResource(type, negative)` passes `HasEnough` and adds value;
- addition/subtraction can wrap `long`;
- duplicate types select the first entry;
- a missing entry is silently created by ordinary mutation;
- malformed/negative/unknown wallet state is treated as spendable;
- event subscriber exceptions can interrupt later subscribers/callers;
- `TickProduction` accepts `NaN`/infinity, uses throwing service lookup, silently defaults missing buildings to level 1, can lose or poison fractional remainders, and can partially mutate a tick.

### `LocalWarzoneCreditService`

- negative add/spend can create credits;
- `int` addition/subtraction can wrap;
- negative persisted credit is treated as spendable state;
- each valid call saves independently, preventing later atomic composition;
- the `bool`/`void` API cannot distinguish invalid, insufficient, overflow, save-unavailable, or durable-commit outcomes.

### Save/default/offline interaction

Current generic save normalization adds missing established-profile resources with new-profile amounts. Offline progress also performs unchecked addition and creates missing entries. Both conflict with the merged save semantic policy and must be corrected under #137, not silently expanded inside the Stage A mutation PR.

## 3. Non-goals

Do not:

- change starting balances, production rates, territory bonuses, reward amounts, or prices;
- redesign buildings, territory, training, loot, Warmaster, battle, or NVS-01;
- add cloud/server economy authority;
- repair save candidate selection or file rotation in Stage A;
- edit `SaveGameData.cs` before a separate declared shared lock and #137 migration plan;
- edit `Bootloader.cs` while PR #203 holds its lock;
- add player-facing narrative copy;
- silently clamp, sum duplicates, select first/last duplicate, reseed, or drop unknown data.

## 4. Supported currency identities

Implementation must inventory the current `ResourceType` enum before coding and test every defined value.

The currently consumed identities include:

```text
Food
Wood
Stone
Gold
ManaStone
Ore
DeepOre
WorldSap
RoyalSigil
DarkCrystal
```

Classification:

- **core established-profile entries:** Food, Wood, Stone, Gold, ManaStone, Ore;
- **realm/optional entries:** DeepOre, WorldSap, RoyalSigil, DarkCrystal;
- **unknown future raw enum:** preserve when round-trippable, exclude from current operations;
- **undefined target supplied by current code/caller:** reject as unsupported.

A missing core entry makes the wallet malformed. A missing optional entry does not authorize runtime creation; the targeted mutation returns `MissingEntry` until an approved explicit migration adds neutral `0`.

Warzone Credits are a separate nonnegative `int` currency and follow the same signed-amount/overflow principles.

## 5. Typed technical result model

Add narrowly scoped technical result types in non-UnityEngine interface/core code. Exact names may differ, but semantics must match.

### Mutation status

```text
Applied
NoOp
InsufficientFunds
InvalidAmount
UnsupportedCurrency
SaveUnavailable
WalletMalformed
MissingEntry
Overflow
PersistenceUnverified
PersistenceFailed          # Stage B
DuplicateOperation         # Stage B
```

### Required result data

```text
status
stableDiagnosticCode
currency/resource type
requested amount
previous balance when available
current balance when available
wallet validation disposition
persistence disposition
technical diagnostics (no player copy)
```

Result objects are immutable snapshots and never expose mutable save entries/lists.

Suggested diagnostics:

```text
ECON_RESOURCE_APPLIED
ECON_RESOURCE_NOOP
ECON_INVALID_AMOUNT
ECON_UNSUPPORTED_CURRENCY
ECON_SAVE_UNAVAILABLE
ECON_WALLET_NULL_ENTRY
ECON_WALLET_DUPLICATE_TYPE
ECON_WALLET_NEGATIVE_BALANCE
ECON_WALLET_MISSING_CORE
ECON_MISSING_ENTRY
ECON_INSUFFICIENT_FUNDS
ECON_OVERFLOW
ECON_PRODUCTION_INVALID_DELTA
ECON_PRODUCTION_DEPENDENCY_UNAVAILABLE
ECON_PERSISTENCE_UNVERIFIED
ECON_PERSISTENCE_FAILED
```

Diagnostics are technical and localization-independent. Player-visible delivery belongs to #177.

## 6. Interface compatibility strategy

Add typed methods while retaining current methods during migration so unrelated callers continue to compile.

Equivalent target shape:

```csharp
ResourceBalanceResult TryGetResourceCount(ResourceType type);
ResourceMutationResult TryAddResource(ResourceType type, long amount);
ResourceMutationResult TryConsumeResource(ResourceType type, long amount);

CreditBalanceResult TryGetCredits();
CreditMutationResult TryAddCredits(int amount);
CreditMutationResult TrySpendCredits(int amount);
```

Legacy wrappers:

- `GetResourceCount` / `GetCredits` return the typed balance when valid, otherwise `0` plus stable diagnostics; callers requiring authority must migrate to typed queries.
- `AddResource` / `AddCredits` call the typed method and never bypass validation.
- `ConsumeResource` / `SpendCredits` return `true` only when the in-memory mutation status is `Applied`; the legacy `bool` is not proof of durable composite commit.
- `HasEnough(type, negative)` is always false.
- `HasEnough(type, zero)` is true only when the requested currency and wallet are valid; it performs no mutation.
- `HasEnough(type, positive)` is false for unavailable/malformed/unsupported state.

Do not remove legacy members until all callers are inventoried and migrated through focused PRs.

## 7. Wallet validation contract

Validation is pure and does not normalize, create, remove, reorder, save, or emit events.

### Valid wallet

- save/current wallet exists;
- no null entries;
- every defined current currency appears at most once;
- every stored amount is nonnegative;
- every required core type appears exactly once;
- optional types appear zero or one time;
- unknown future enum entries are unique and round-trippable when preservation is supported.

### Malformed wallet

Any of these disables all wallet mutations:

- null list or null element in an established loaded profile;
- duplicate raw resource type;
- negative stored balance;
- missing required core type;
- unsupported raw enum value that cannot round-trip safely;
- arithmetic state that cannot be represented safely.

Rules:

- do not select the first duplicate;
- do not sum duplicate entries;
- do not convert negative to zero inside the service;
- do not reseed or auto-create a missing entry;
- do not emit normal resource-changed events;
- preserve raw data for #137 candidate selection/repair;
- typed query returns malformed/unavailable, while legacy query returns 0 with diagnostic.

### Preserved unknown

A unique, nonnegative unknown future entry may be preserved without invalidating otherwise valid known entries if raw enum round-trip is proven. Current operations cannot target or mutate it. It does not make an older backup preferable by itself.

## 8. Resource mutation semantics

### Add

- amount `> 0`: checked addition;
- amount `== 0`: `NoOp`, no event, no save;
- amount `< 0`: `InvalidAmount`, no mutation/event/save;
- unsupported target: reject;
- malformed wallet: reject;
- missing target entry: `MissingEntry`, never create;
- overflow: `Overflow`, no mutation/event/save.

### Consume

- amount must be strictly positive;
- insufficient balance: `InsufficientFunds`, no mutation/event/save;
- checked subtraction cannot underflow;
- zero/negative: `InvalidAmount`;
- malformed/missing/unsupported state: reject with no side effect.

### Atomic in-memory update

For one mutation:

1. validate save, wallet, target, and amount;
2. calculate checked final balance without modifying state;
3. commit exactly one balance write;
4. create immutable result;
5. deliver technical event safely.

No validation or arithmetic occurs after the balance has already changed.

## 9. Warzone Credit semantics

Apply the same contract with checked `int` arithmetic.

Stage A preserves the existing standalone-save behavior for valid calls only to avoid changing persistence expectations before #137, but reports it honestly:

```text
validate
→ calculate
→ write one in-memory balance
→ invoke existing Save()
→ result persistence = PersistenceUnverified
```

Reason: current `ISaveGameService.Save()` is `void` and swallows persistence exceptions. Stage A cannot claim a durable commit or reliably roll back based on that API.

Rules:

- invalid/no-op/insufficient/overflow calls never invoke `Save()`;
- legacy valid call invokes `Save()` at most once;
- Stage A typed result must not label the operation `Committed`;
- one-time rewards/purchases may not use `PersistenceUnverified` as success after Stage B is available;
- tests use a fake save service to prove invalid calls do not save and valid compatibility calls request one save.

## 10. Stage B durable transaction seam

After #137 exposes a typed durable save/candidate result, extend the typed economy API with prepared mutation semantics equivalent to:

```text
Validate(snapshot, operation)
→ PreparedMutation(previous, next, operationId)
→ caller composes all domain preparations
→ one durable save transaction
→ publish committed state
→ events/notifications
```

Required Stage B behavior:

- current save/candidate must be writable and semantically valid;
- operation/correlation ID required for one-time externally delivered rewards/purchases;
- duplicate operation returns the prior committed result or `DuplicateOperation` without mutation;
- save failure leaves/publishes the prior state or a recoverable pending transaction according to #137;
- resource, credits, quest, affinity, inventory, territory, and unlock operations can share one commit;
- events and player notifications occur only after verified durable commit;
- no nested service-level `Save()` inside a composed transaction.

Stage B caller migrations include #166 territory, #168 boss loot, #171 Warmaster, #174 battle-result application, and #134 NVS consequences.

## 11. Event contract

`OnResourceChanged` remains technical and occurs:

- exactly once after one successful in-memory Stage A resource mutation;
- after durable commit for Stage B composite mutations;
- never for `NoOp`, invalid, malformed, unsupported, insufficient, overflow, duplicate, or failed persistence;
- with the final validated balance.

Subscriber isolation:

- iterate subscribers individually;
- one subscriber exception does not roll back the already committed core mutation or prevent later subscribers;
- each failure emits a stable technical diagnostic;
- subscriber failure does not convert an applied mutation into a second retryable mutation.

A future credit event may be added only through a focused interface change and must follow the same rule.

## 12. Online production contract

`TickProduction` is online frame/service production only. Offline progress remains #137.

### Delta validation

- finite `deltaSeconds` only;
- strictly positive;
- reject `NaN`, positive/negative infinity, zero, and negative values;
- reject values greater than a documented technical maximum; use `60d` unless the implementation PR proves another value from existing runtime constraints;
- never clamp invalid delta into a different economic result.

### Dependency resolution

- use non-throwing `ServiceLocator.TryGet` after PR #203 or an equivalent guarded seam;
- unavailable building/territory service produces stable diagnostics, not broad silent catch;
- do not substitute default building level 1 for missing state;
- invalid/missing/negative building state contributes no fabricated production and is coordinated with #165;
- territory contribution uses only validated nonnegative finite/checked output from #166;
- `RealmId.None` or invalid realm produces no rare resource and no Crownlands substitution.

Because PR #203 holds the `Bootloader.cs` lock, Stage A may not edit Bootloader. Coordinate any `TryGet` API dependency through current `ServiceLocator` or rebase after #203.

### Batch preparation

For one tick:

1. validate wallet once;
2. collect independently valid contributions by resource;
3. reject non-finite/negative contribution values;
4. combine rates with checked/finiteness validation;
5. calculate proposed fractional remainders and whole amounts without mutation;
6. calculate all checked final balances;
7. if the wallet or batch arithmetic fails, commit nothing;
8. commit final balances and remainders once;
9. emit one event per balance that actually increased.

No fractional remainder is subtracted until the associated whole-resource mutation is accepted. A failed tick cannot lose accumulated remainder or leave `NaN`/infinity in `_productionRemainders`.

### Balance preservation

Do not alter current numeric production constants in this issue. Any source that is valid under current behavior keeps the same rate. Invalid/missing dependencies become unavailable rather than fabricated.

## 13. Save/default/offline boundary

Stage A must not edit generic `EnsureSaveDefaults` or offline progress to reseed/repair established profiles.

Under #137:

- explicit new-profile creation assigns current starting balances once;
- compatible versioned migration may add an optional/new currency with neutral `0`;
- missing established core entry is malformed and triggers candidate ranking/domain disablement;
- offline progress validates the writable clone and uses checked batch mutation;
- failed persistence does not publish progressed balances;
- retry cannot reapply the same interval;
- file recovery emits no reward/progress callbacks.

If Stage A tests expose current normalization/offline defects, record them against #137 rather than broadening the mutation PR.

## 14. Required tests — Stage A

### Wallet/query

- null save;
- null wallet;
- null entry;
- each supported type;
- missing core type;
- missing optional target;
- duplicate type;
- negative stored balance;
- unique nonnegative unknown future raw enum preservation where testable;
- unsupported target;
- query does not mutate/reorder/create/save.

### Resource amounts/arithmetic

- add positive, zero, negative;
- consume positive enough/insufficient;
- consume zero/negative exploit;
- `long.MaxValue` overflow;
- subtraction safety;
- target missing;
- malformed wallet;
- exact previous/current result values.

### Credits

- add positive/zero/negative;
- spend positive enough/insufficient;
- spend zero/negative exploit;
- `int.MaxValue` overflow;
- negative persisted credit;
- save unavailable;
- invalid calls request zero saves;
- valid compatibility call requests exactly one save and reports `PersistenceUnverified` rather than committed.

### Events

- successful resource mutation emits exactly once;
- no-op/invalid/insufficient/overflow/malformed emits none;
- subscriber throws and later subscribers still run;
- final balance remains correct and no retry/duplicate event is produced.

### Production

- valid representative tick preserves existing rates;
- fractional accumulation across repeated ticks;
- zero/negative/NaN/+infinity/-infinity/over-maximum delta;
- missing building service;
- missing/invalid building state does not default to level 1;
- missing/throwing territory service has explicit result;
- negative/overflow territory income;
- `RealmId.None` has no rare production;
- checked balance overflow;
- failed batch changes neither balances nor remainders;
- successful batch emits one event per changed resource;
- repeated tick is deterministic for the same valid state/delta sequence.

### Integration regression

- current valid new profile wallet;
- valid save/reload after existing caller-controlled resource save path;
- building/research/training read/spend callers still compile;
- no balance/rate/cost constant changed;
- canonical Unity compile and focused EditMode tests;
- profile-safe PlayMode after #127 implementation when available.

## 15. Required tests — Stage B

- one committed resource/credit operation;
- duplicate operation ID same session and after reload;
- composite transaction with fake quest/affinity/unlock mutations;
- validation failure before preparation;
- save failure before install, after install, and final verification;
- rollback/pending recovery according to #137;
- events/notifications only after durable commit;
- territory/Warmaster/loot/battle/NVS caller integration;
- no nested save;
- exact once after retry/reload.

## 16. Expected Stage A file boundary

Likely:

```text
unity/Assets/AL/Scripts/Core/Interfaces/IResourceService.cs
unity/Assets/AL/Scripts/Core/Interfaces/IWarzoneCreditService.cs
unity/Assets/AL/Scripts/Services/Local/LocalResourceService.cs
unity/Assets/AL/Scripts/Services/Local/LocalWarzoneCreditService.cs
small immutable result/validator types in Core or Services
focused EditMode tests and .meta files
```

No designated shared-file lock is expected. Do not edit `Bootloader.cs`, `SaveGameData.cs`, `LocalSaveGameService.cs`, scenes, Build Settings, Android, narrative/content, terrestrial-design source, or balance catalogs in Stage A.

If interface edits overlap another open PR, coordinate/rebase rather than creating duplicate result types.

## 17. Branch and PR order

### Stage A

```text
codex/resource-integrity-foundation
```

Primary mode: Codex engineering.

PR links:

```text
Refs #163
Refs #137
Refs #165
Refs #166
Refs #168
Refs #171
Refs #174
```

Do not use `Fixes #163` unless Stage B and caller acceptance are also complete.

### Stage B

After #137 typed persistence/transaction seam:

```text
codex/economy-transaction-integration
```

Split caller migrations when file overlap or review size requires it, with explicit dependency order and idempotency identity.

## 18. Validation commands

Run from:

```text
D:\260711\MY\AndroidStudioProjects\AnotherLife\unity
```

```powershell
$repo = "D:\260711\MY\AndroidStudioProjects\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\EconomyIntegrityCompile.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform EditMode `
  -testResults "$repo\unity\Logs\EconomyIntegrityEditMode.xml" `
  -logFile "$repo\unity\Logs\EconomyIntegrityEditMode.log"
```

After #127 implementation, run the profile-safe PlayMode suite when Stage A changes online runtime behavior. Never use duplicate-checkout evidence as acceptance.

Report exact base/head SHA, commands, exit codes, totals, compiler-error scan, files changed, interface callers checked, no-balance-change proof, shared locks, blocked validation, and final diff/status.

## 19. Acceptance criteria

### Stage A

- [ ] Negative consume/spend cannot add resources or credits.
- [ ] Negative add is rejected.
- [ ] Zero semantics are explicit.
- [ ] Overflow/underflow cannot wrap.
- [ ] Null/duplicate/negative/missing-core wallet data cannot crash or mutate.
- [ ] Unknown future data is preserved/excluded according to policy.
- [ ] Ordinary mutation never auto-creates a missing entry.
- [ ] Typed results distinguish invalid, unavailable, malformed, insufficient, overflow, and applied state.
- [ ] Legacy wrappers route through safe typed methods.
- [ ] Invalid operations emit no event and request no save.
- [ ] Event subscriber failure cannot corrupt/repeat the mutation.
- [ ] Credits do not falsely claim durable commit before #137.
- [ ] Online production rejects non-finite/invalid time and commits balances/remainders safely.
- [ ] Current valid rates/balances/costs are unchanged.
- [ ] Focused compile/EditMode evidence passes from the canonical workspace.
- [ ] No save algorithm, shared file, narrative, terrestrial design, Android, scene, or unrelated change is included.

### Full #163 completion

- [ ] Stage B durable prepared/commit semantics are integrated with #137.
- [ ] Duplicate operation IDs are idempotent across reload.
- [ ] Territory, Warmaster, loot, battle-result, and NVS callers no longer depend on nested unverified saves.
- [ ] Events/notifications occur only after verified composite commit.
- [ ] Save/reload and fault matrices pass.
- [ ] Issue acceptance criteria and all named integration regressions pass.

# GPT handoff to Codex

```text
Codex engineering: implement Stage A of issue #163 from current main using unity/Docs/Economy_Mutation_Integrity_Spec.md. Add typed resource/credit query and mutation results while retaining safe legacy wrappers. Validate the whole wallet without mutation; reject negative/zero-per-policy, duplicate, negative stored, missing core, unsupported, and overflow cases; never auto-create missing entries. Make resource events subscriber-safe. Harden online production with finite bounded delta, guarded dependencies, checked batch balance/remainder preparation, and no fabricated level-1 production. For valid Warzone Credit calls, preserve the existing standalone save request but report persistence as unverified because ISaveGameService is still void; invalid calls must not save. Do not edit Bootloader.cs, SaveGameData.cs, LocalSaveGameService.cs, balance values, Android, narrative, terrestrial design, scenes, or Build Settings. Open codex/resource-integrity-foundation as a focused draft, report exact canonical Unity compile/EditMode evidence, and leave #163 open for Stage B after #137.
```