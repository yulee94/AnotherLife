# Economy Integrity and Transaction-Safe Mutation Specification

**Status date:** 2026-07-15  
**Tracking issue:** #163  
**Specification owner:** GPT  
**Implementation owner:** Codex engineering mode  
**Audited baseline:** `0858d1d2e028a20b44d7c6291ea9ba565e5b4725`  
**Validated Unity target:** `2022.3.62f3`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`  
**Semantic authority:** `unity/Docs/Save_Semantic_Compatibility_Policy.md`

## 1. Goal

Make normal resources and Warzone Credits safe to read and mutate without signed-amount exploits, arithmetic wrap, ambiguous wallet selection, silent profile repair, duplicate normal events, or a persistence boundary that prevents later atomic/idempotent transactions.

This specification establishes the low-level economy contract consumed by #137, #165, #166, #168, #169, #171, #180, and later #133/#134. It does not authorize any reward source, balance change, production-rate change, entitlement, encounter result, or narrative consequence.

## 2. Binding decisions

1. **Low-level balance validation and reward authorization are separate.** A positive amount is not proof that a caller is entitled to grant it.
2. **The merged save semantic policy controls malformed data.** Null rows, duplicate known resource types, negative balances, and missing core resources disable wallet mutation; ordinary service calls do not repair or delete them.
3. **Stable unknown enum rows are preserved.** They are excluded from current known-resource operations and are not reinterpreted as a default enum value.
4. **Core and optional resources are different.** Missing core balances are malformed. A missing supported rare resource may be introduced only as a neutral-zero optional entry through the explicit rule in this document.
5. **All arithmetic is checked and staged before mutation.** Overflow or underflow changes nothing.
6. **Zero behavior is explicit.** Add-zero is a no-op. Consume/spend-zero is invalid.
7. **Authoritative typed primitives do not save.** The caller that owns the business transaction owns persistence.
8. **Legacy Warzone Credit wrappers temporarily retain successful-call persistence for compatibility.** New and migrated callers use the typed no-save primitives. Invalid/no-op calls never save.
9. **Events are post-commit notifications, not part of the commit.** Invalid/no-op operations emit no normal changed event. Subscriber failure cannot roll back or misreport the balance mutation.
10. **Live production is a staged batch.** Invalid delta, dependency data, wallet state, contribution arithmetic, or final balance rejects the whole tick without changing balances or fractional remainders.
11. **Fractional production remainders remain session-only in #163.** No shared save field is added. Each remainder is always finite and in `[0, 1)`; service recreation may lose less than one unit per resource, never create debt or duplicate value.
12. **Direct prototype grants are not made safe merely by validating their amount.** Kingdom grants and Champion proximity credits remain removal/gating responsibilities under #178/#180.

## 3. Verified current-source inventory

### 3.1 `LocalResourceService`

Current risks include:

- `FirstOrDefault(r => r.Type == type)` dereferences null rows;
- duplicate types silently select the first row;
- negative stored balances are returned and spendable;
- `AddResource` accepts negative amounts;
- `ConsumeResource` accepts zero/negative amounts, allowing negative consume to add value;
- unchecked `long` arithmetic can wrap;
- missing entries are created without distinguishing core corruption from optional-resource compatibility;
- read methods and mutations expose no typed validation state;
- production accepts non-finite or arbitrarily large delta values except for a nonpositive check;
- production trusts auto-seeding/mutable building and territory services;
- fractional remainders can become non-finite or negative and are committed before the wallet result is known.

### 3.2 `LocalWarzoneCreditService`

Current risks include:

- `AddCredits` accepts negative amounts;
- `SpendCredits` accepts zero/negative amounts, allowing negative spend to add credits;
- unchecked `int` arithmetic can wrap;
- a negative persisted balance is treated as normal;
- every successful call saves independently, preventing a larger reward/purchase transaction from controlling durability;
- success logging occurs without a typed result.

### 3.3 Current consumer classes

The implementation PR must inventory every current caller. The reviewed baseline includes at least:

| Consumer | Current economy use | Owning follow-up |
| --- | --- | --- |
| `LocalBuildingService` | consumes Stone before upgrade state/save | #165 |
| `LocalResearchService` | consumes Gold before research state/save | #165 |
| `LocalTrainingService` | consumes Food before troop/quest/save | #165 |
| `LocalQuestService` | grants resources and credits before its save | #152 and later #133 |
| `WarzoneService` | grants 100 credits during capture before final save | #166 |
| `LocalWarmasterService` | spends credits before piece/state/save | #171 |
| `LocalBossLootService` | grants credits before equipment and conditional save | #168 |
| Kingdom command UI | direct fixed credit grants on the current main branch | #178 / PR #208 |
| Champion Arena controller | recurring proximity credit grants, including post-clear risk | #178 and #180 |
| `LocalResourceService.TickProduction` | applies building, rare-resource, and territory income | #163 with #165/#166 prerequisites for trusted producers |

The #163 implementation does not absorb the downstream domain issues. It provides the safe typed primitive and records which callers still use the compatibility wrapper.

## 4. Scope and non-goals

### 4.1 In scope

- resource wallet validation and immutable read snapshots;
- supported/core/optional resource classification through `ResourceRules`;
- typed resource and Warzone Credit read/mutation results;
- positive/zero/negative amount behavior;
- checked arithmetic;
- event and diagnostic behavior;
- optional rare-resource neutral-zero insertion rule;
- no-save transaction primitives;
- compatibility wrappers;
- staged live-production arithmetic and session-only remainders;
- focused EditMode tests and current-caller inventory.

### 4.2 Not in scope

- starting-balance changes;
- reward, price, cost, production-rate, or territory-bonus changes;
- reward entitlement or idempotency ledgers;
- territory capture semantics;
- boss loot computation/application;
- Warmaster piece/catalog rules;
- Wishgate rules;
- building/research/training bounds or definitions;
- full save candidate selection, quarantine, repair, deletion, or schema fields;
- `Bootloader.cs` while PR #203 holds its lock;
- direct command/Champion grant authorization;
- narrative, Android, scenes, Build Settings, or terrestrial design.

## 5. Supported resource authority

`ResourceRules.WalletResources` is the current supported wallet set:

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

The first six entries are **core resources**. The four values recognized by `ResourceRules.IsRareResource(...)` are **optional rare resources**.

Implementation requirements:

- validate once that `WalletResources` contains only defined `ResourceType` values and no duplicates;
- add or use explicit helpers equivalent to `IsSupportedWalletResource` and `IsCoreResource` rather than repeating ad hoc lists;
- reject a requested `ResourceType` not in `WalletResources`;
- preserve unknown numeric enum rows in the serialized list when safe to round-trip, but exclude them from known-resource reads/mutations;
- validate `SelectedRealm` before calling `GetRareResourceForRealm`; never allow that method’s fallback to turn `None` or an undefined realm into Royal Sigil production.

No starting amount belongs in `ResourceRules` or the low-level service.

## 6. Typed result contract

Names may vary, but the implementation must expose equivalent immutable typed results through `IResourceService` and `IWarzoneCreditService` or narrowly adjacent contracts.

### 6.1 Mutation statuses

```text
Applied
NoChange
RejectedNoCurrentSave
RejectedProfileNotWritable
RejectedUnsupportedCurrency
RejectedInvalidAmount
RejectedMalformedState
RejectedInsufficientBalance
RejectedOverflow
RejectedDependencyUnavailable
```

Required mutation-result data:

```text
status
currency kind (resource or Warzone Credits)
resource type when applicable
requested amount
previous balance when safely known
current balance when safely known
stable diagnostic code
changed = true only for Applied
```

Balances may use `long` in the common result even though Warzone Credits persist as `int`. A result never contains a local file path or raw profile data.

### 6.2 Read statuses

```text
Available
AvailableReadOnly
CompatibleMissingOptional
UnavailableNoCurrentSave
UnavailableUnsupportedCurrency
UnavailableMalformedState
```

A typed balance read includes status, balance when safely known, and diagnostics. A read is pure: it does not add, remove, reorder, normalize, save, or raise a changed event.

### 6.3 Diagnostics

Use stable codes equivalent to:

```text
AL-ECO-NO-CURRENT-SAVE
AL-ECO-PROFILE-READ-ONLY
AL-ECO-UNSUPPORTED-RESOURCE
AL-ECO-INVALID-AMOUNT
AL-ECO-MALFORMED-WALLET
AL-ECO-MISSING-CORE-RESOURCE
AL-ECO-DUPLICATE-RESOURCE
AL-ECO-NEGATIVE-BALANCE
AL-ECO-OVERFLOW
AL-ECO-INSUFFICIENT-BALANCE
AL-ECO-INVALID-CREDITS
AL-ECO-PRODUCTION-INVALID-DELTA
AL-ECO-PRODUCTION-DEPENDENCY
AL-ECO-EVENT-HANDLER
```

A validation result may contain multiple ordered diagnostics with a record path such as `Resources[3]`; it must not expose full persistence paths or raw record contents in player-facing copy.

Repeated dashboard reads of the same malformed state must not spam the log every frame. Return typed status and either log once per service/state revision or only when a mutation is attempted.

## 7. Resource wallet validation

Build one immutable validation snapshot before every mutation and reuse the same pure validator in tests and later #137 candidate validation.

### 7.1 Validation algorithm

1. If `CurrentSave` is null, return `UnavailableNoCurrentSave`.
2. If the profile is known read-only through a future #137 gate, reads may return `AvailableReadOnly`; every mutation returns `RejectedProfileNotWritable`.
3. If `Resources` is null, classify the wallet as malformed. Do not assign an empty list in a query or mutation service.
4. Enumerate without changing the list.
5. A null row makes the known wallet malformed and disables all known-resource mutation.
6. A row whose type is not in `WalletResources` is preserved as unknown and excluded from known-resource operations. It is not reinterpreted.
7. Two or more rows for one known resource type make the known wallet malformed. Do not sum, select first/last, min/max, or delete a row.
8. A negative balance for any known resource makes the known wallet malformed. Do not clamp to zero.
9. Every core resource must exist exactly once. A missing core resource makes the known wallet malformed.
10. Each optional rare resource may exist zero or one time. Absence alone is compatible and reads as neutral zero with `CompatibleMissingOptional`.
11. A valid snapshot returns an immutable map for the known rows plus preserved-unknown diagnostics.

Malformed wallet state changes nothing and emits no normal changed event.

### 7.2 Optional rare-resource insertion

This document approves one narrowly bounded compatibility rule:

- when the wallet is otherwise fully valid;
- all core resources exist exactly once;
- the requested type is one of the four supported rare resources;
- that rare type is absent;
- the operation is a validated positive add or a validated positive production batch;

then the service may stage exactly one new row with balance `0` and apply the positive delta in the same atomic in-memory commit.

Rules:

- queries never create the row;
- consume never creates the row;
- add-zero never creates the row;
- no starting amount is assigned;
- if any later validation/arithmetic step fails, neither the row nor the balance change is committed;
- the unknown-row order and every existing row/reference remain unchanged; the new optional row is appended deterministically.

A missing core resource is never repaired by this rule.

## 8. Resource read behavior

### 8.1 Typed read

The authoritative read returns the validation status and balance.

- valid existing resource: `Available` with exact nonnegative balance;
- missing optional rare resource: `CompatibleMissingOptional` with `0`;
- read-only valid profile: `AvailableReadOnly` with exact balance;
- unsupported requested type: unavailable;
- malformed known wallet: unavailable; no first-row or partial balance is returned as spendable.

### 8.2 Legacy `GetResourceCount`

Retain for compatibility, implemented as a pure wrapper:

- return exact balance for `Available`, `AvailableReadOnly`, or `CompatibleMissingOptional`;
- return `0` for unavailable states;
- never mutate or save;
- do not treat the returned `0` as proof that the wallet is valid. New integrity-sensitive callers must use the typed read.

### 8.3 `HasEnough`

- amount `<= 0` returns `false`;
- malformed/unsupported/no-save/read-only-for-mutation state returns `false`;
- missing optional entry returns `false` for every positive request;
- valid balance comparison uses the typed snapshot;
- no event, save, or mutation occurs.

A typed affordability result is preferred so callers can distinguish insufficient balance from unavailable/malformed state.

## 9. Resource mutation behavior

### 9.1 Positive add

For `amount > 0`:

1. validate requested type and wallet snapshot;
2. stage optional rare insertion only under section 7.2;
3. compute `checked(previous + amount)`;
4. if overflow occurs, return `RejectedOverflow` and change nothing;
5. commit the insertion/balance only after every step succeeds;
6. return `Applied` with before/after balances;
7. emit exactly one post-commit resource-changed event with the final balance;
8. do not save in the authoritative typed primitive.

### 9.2 Add zero

- return `NoChange`;
- do not create an optional row;
- do not log normal success;
- do not emit an event;
- do not save.

### 9.3 Add negative

- return `RejectedInvalidAmount`;
- do not route to consume;
- do not mutate, event, or save.

### 9.4 Positive consume

For `amount > 0`:

1. validate requested type and wallet snapshot;
2. the requested entry must already exist;
3. if balance is insufficient, return `RejectedInsufficientBalance`;
4. compute subtraction in a checked context;
5. commit exactly once;
6. return `Applied` and emit one post-commit event;
7. do not save in the authoritative typed primitive.

### 9.5 Consume zero or negative

- return `RejectedInvalidAmount`;
- never add value;
- no mutation, event, log claiming success, or save.

### 9.6 Legacy wrappers

Retain current interface methods only as compatibility wrappers:

- `AddResource` delegates to the typed add and emits a stable developer diagnostic on rejection;
- `ConsumeResource` returns `true` only when the typed result is `Applied`;
- neither wrapper saves, preserving current resource-service persistence behavior;
- no new production caller may be introduced on the void/bool wrapper when it needs failure detail or a larger transaction.

Marking wrappers obsolete is allowed if it does not turn current warnings into a build failure and the PR reports every remaining caller.

## 10. Warzone Credit behavior

Warzone Credits use the same signed-amount and checked-arithmetic rules, with `int` persistence and `long` result reporting.

### 10.1 Read

- no current save: unavailable;
- nonnegative stored value: available;
- negative stored value: malformed/unavailable; do not return it as spendable;
- a legacy omitted scalar naturally deserializing to `0` is compatible zero;
- future read-only profile status prevents mutation but may expose the exact nonnegative balance.

### 10.2 Typed add

- positive only;
- zero returns `NoChange`;
- negative rejected;
- checked `int` addition;
- overflow changes nothing;
- no independent save;
- no success log/event unless the result is `Applied`.

### 10.3 Typed spend

- strictly positive only;
- zero/negative rejected;
- negative spend can never add credits;
- insufficient balance returns a distinct status;
- checked subtraction;
- no independent save.

### 10.4 Legacy compatibility wrappers

Existing `AddCredits(int)` and `SpendCredits(int)` may temporarily retain their current successful-call durability so unreviewed callers do not lose committed credits during the migration:

- delegate all validation/arithmetic to the typed no-save primitive;
- if and only if the result is `Applied`, call `ISaveGameService.Save()` exactly once;
- `NoChange` and every rejection call `Save()` zero times;
- `SpendCredits` returns `true` only for `Applied`;
- no new caller may use these wrappers;
- the PR report lists each remaining wrapper caller and its owning migration issue.

This is a compatibility bridge, not the final transaction model. #152/#166/#168/#169/#171/#180 and #133 must use the typed no-save primitive before their transactions are accepted.

## 11. Persistence and transaction boundary

### 11.1 Authoritative rule

```text
validate request and domain state
→ stage checked balance change
→ stage owning domain state/ledger
→ commit in memory
→ caller persists once
→ caller reports committed result
```

The low-level typed economy primitive never decides that a quest, capture, purchase, encounter, Wishgate, or report result is authorized. It never creates an idempotency key.

### 11.2 Save failure

#163 does not implement full rollback across file-system failure. It must:

- expose no-save typed primitives so a caller can clone/stage and persist through #137/#133;
- leave the legacy wrappers’ compatibility persistence explicit;
- use isolated test save services to prove no invalid/no-op path calls save;
- not claim durable success on behalf of a caller.

### 11.3 Caller transition matrix

| Owner | Required transition |
| --- | --- |
| #165 building/research/training | validate destination and checked cost, call typed consume, mutate domain, persist once |
| #152/#133 quest rewards | call typed adds inside the quest/report transaction; no nested economy save |
| #166 territory | call typed credit add inside one ownership/reward transaction |
| #168 boss loot | call typed credit add with equipment and applied-result ledger in one recoverable boundary |
| #169 Wishgate | call typed credit/resource add only after entitlement validation and one-time reward identity |
| #171 Warmaster | call typed spend, mutate piece state, persist once or roll back |
| #180 Champion | no proximity/direct credit grant without an authoritative encounter result |
| #178 command containment | remove/gate all direct production grants; validation is not authorization |
| #137 save/offline progress | validate/stage on a clone, persist/verify, then publish |

## 12. Live production contract

`TickProduction(double)` is a live-frame producer, not an offline-progress API.

### 12.1 Delta contract

- finite only;
- strictly greater than `0`;
- maximum accepted single live tick: `1.0` second;
- values above `1.0`, `NaN`, and infinities are rejected, not clamped;
- invalid delta changes no balance or remainder and emits no normal event;
- offline elapsed time is handled only through the validated #137 path.

The one-second bound is a technical separation between live update and offline progress; it does not change production rates.

### 12.2 Dependency snapshot

Before computing any contribution:

- acquire building and territory services through a non-throwing lookup compatible with current main and future PR #203;
- use `GetAllBuildingStates()` to inspect a read-only snapshot; do not call query methods that auto-create missing state;
- reject a null enumerable, null row, blank ID, duplicate ID, missing required producer building, or negative level;
- required producer IDs are the existing `Farm`, `LumberMill`, `Quarry`, `GoldMine`, `ManaShrine`, `Mine`, and `TownHall` IDs;
- preserve current production rate constants exactly;
- a level’s content maximum remains #165, but all arithmetic/contributions must still be finite, nonnegative, and representable;
- validate `SelectedRealm` is a defined non-`None` realm before rare-resource mapping;
- obtain territory income for each supported resource, catching dependency/overflow failure and rejecting negative results;
- trusted duplicate/definition/bonus semantics remain #166; #163 must not call a negative or failed income result an accepted tick.

If any required dependency/input is invalid, reject the whole tick. Do not apply partial core income while skipping an invalid rare/territory source.

### 12.3 Stage contributions

1. Create a fresh contribution map for supported resources.
2. Compute every building, rare, and territory contribution using finite checked `double` operations.
3. Copy current remainder values into a staged map; absent remainder is `0`.
4. Every existing remainder must be finite and in `[0, 1)`; otherwise reject the tick and reset nothing silently.
5. For each resource, calculate `total = stagedRemainder + contribution`.
6. Require finite, nonnegative total.
7. Calculate `whole = floor(total)` and require `whole <= long.MaxValue`.
8. Calculate `nextRemainder = total - whole` and require finite `[0, 1)`.
9. Stage one atomic wallet batch containing only positive whole amounts.
10. Validate every final wallet balance with checked arithmetic before changing any row.

### 12.4 Commit

- if wallet validation/batch arithmetic fails, commit no balance, optional row, event, or remainder;
- otherwise commit all wallet balances and optional rare insertions, then all remainders;
- emit one post-commit event per changed resource in deterministic `ResourceRules.WalletResources` order;
- a zero-whole contribution may update its remainder but emits no event;
- no save occurs per frame/tick.

### 12.5 Remainder lifetime

Remainders are session-only in #163:

- initialized to zero when the service is constructed;
- never negative or non-finite;
- a successful service reload may lose less than one unit per resource;
- no hidden production debt or gain carries across reload;
- no `SaveGameData.cs` field is added;
- a future persistence change requires a separate shared-file specification and migration.

### 12.6 Save cadence

#163 does not add per-frame or per-five-second saves. Production persists through the owning validated checkpoint/lifecycle save. #137 provides durable writes; #153/PR #203 owns safe pause/quit lifecycle. A hard process crash may lose production since the last successful checkpoint but must not duplicate or fabricate it.

## 13. Event and callback behavior

### 13.1 Resource event

- `OnResourceChanged` fires exactly once per applied resource balance;
- payload is the final committed balance;
- no event for read, no-op, rejected, insufficient, overflow, malformed, or failed batch operations;
- batch events use deterministic wallet-resource order;
- event handlers run after the core mutation is committed;
- one throwing handler is logged with `AL-ECO-EVENT-HANDLER` and cannot undo the mutation, change the returned result, or prevent later handlers from being attempted.

### 13.2 Warzone Credit event

A new credit-changed event is optional. If added, it follows the same post-commit and exception-isolation rules. The typed result is required whether or not an event is added.

### 13.3 Logs and player copy

- invalid operations use stable technical diagnostics;
- insufficient balance may be a normal typed result without warning spam;
- no invalid/no-op path logs “added,” “spent,” “earned,” or another success claim;
- services do not produce final player-facing narrative copy; #177 owns delivery/localization.

## 14. Expected implementation boundary

Expected production files:

```text
unity/Assets/AL/Scripts/Core/Interfaces/IResourceService.cs
unity/Assets/AL/Scripts/Core/Interfaces/IWarzoneCreditService.cs
unity/Assets/AL/Scripts/Core/ResourceRules.cs
unity/Assets/AL/Scripts/Services/Local/LocalResourceService.cs
unity/Assets/AL/Scripts/Services/Local/LocalWarzoneCreditService.cs
new small economy result/validation models under an existing Core/Interfaces or Services path
focused EditMode tests
```

Optional:

- one small internal production-batch helper;
- one internal writable-profile gate seam for future #137 integration;
- source-inventory test/record for legacy wrapper callers.

Do not edit in the first #163 PR:

```text
SaveGameData.cs
Bootloader.cs
LocalGameDataService.cs
ProjectInitializer.cs
LocalSaveGameService.cs
KingdomSceneController.cs
ChampionArenaSceneController.cs
LocalQuestService.cs while PR #212 is open
WarzoneService.cs
LocalBossLootService.cs
LocalWarmasterService.cs
building/research/training services
scenes or Build Settings
Android or narrative source
```

Caller migration occurs in the owning follow-up issues after the typed contract merges. A compile-required mechanical update outside the expected boundary must be declared before editing and must not implement the downstream domain logic.

No designated shared-file lock is expected for #163.

## 15. Required tests

Use isolated fake save services and fake dependencies. Never use the developer profile.

### 15.1 Resource authority and reads

- supported resource catalog contains defined unique entries;
- core versus rare classification is exact;
- valid wallet read;
- pure repeated read preserves list count/order/references;
- null wallet list;
- null row;
- missing each core resource;
- missing optional rare returns compatible zero without insertion;
- duplicate known resource disables wallet;
- negative known balance disables wallet;
- unknown numeric enum row is preserved and excluded;
- unsupported requested type is rejected;
- read-only profile seam exposes balance but prevents mutation.

### 15.2 Resource add

- positive valid add;
- zero no-op;
- negative rejected;
- `long.MaxValue` overflow;
- missing optional rare positive add appends one zero-based row then applies once;
- missing optional rare zero add creates no row;
- missing core add rejected;
- malformed wallet add rejected;
- valid add event exactly once with final balance;
- throwing event subscriber cannot corrupt result or skip later subscribers;
- typed add saves zero times;
- legacy wrapper saves zero times.

### 15.3 Resource consume and affordability

- positive consume with enough balance;
- insufficient balance;
- zero rejected;
- negative exploit rejected;
- exact-to-zero consume;
- missing optional entry cannot be consumed/created;
- malformed wallet cannot spend;
- `HasEnough` false for zero/negative/unavailable;
- applied consume event exactly once;
- invalid/failed consume event/save zero times.

### 15.4 Warzone Credits

- valid nonnegative read;
- negative persisted balance unavailable;
- positive typed add;
- add zero no-op;
- add negative rejected;
- `int.MaxValue` overflow;
- positive typed spend;
- insufficient credits;
- zero spend rejected;
- negative spend exploit rejected;
- exact-to-zero spend;
- typed operations save zero times;
- legacy wrapper valid operation saves exactly once;
- legacy no-op/rejection saves zero times;
- no invalid success log/event.

### 15.5 Production delta and dependencies

- valid finite tick at `1.0` and below;
- zero/negative delta no change;
- `NaN`, positive/negative infinity rejected;
- value above `1.0` rejected;
- missing building service;
- missing territory service;
- null building enumerable/row;
- blank/duplicate/missing required building ID;
- negative building level;
- invalid/`None` realm;
- negative/overflowing/throwing territory income;
- unknown extra building/territory data does not become an implicit known producer;
- no query-created building or territory state.

### 15.6 Production batch and remainders

- fractional accumulation below one;
- exact whole boundary;
- multiple ticks produce exact whole units and `[0,1)` remainder;
- different resources maintain independent remainders;
- zero-whole updates remainder without event;
- optional rare row created only when whole positive;
- malformed existing remainder rejects whole tick;
- non-finite contribution/total rejects whole tick;
- wallet overflow rejects all resources and leaves every remainder unchanged;
- one invalid resource prevents partial batch application;
- deterministic event order;
- service reconstruction resets remainders without negative debt or extra units;
- production tick saves zero times.

### 15.7 Caller and regression evidence

- repository inventory lists every remaining legacy `AddCredits`/`SpendCredits` caller and owning issue;
- no new direct grant is introduced;
- valid building/research/training/quest/territory/Warmaster/boss caller source still compiles against compatibility wrappers;
- invalid low-level calls produce no resource, credit, save, quest, story, or normal success side effect;
- explicit caller-owned save after typed mutation survives save/reload in a test root;
- safe #127 PlayMode suite runs after corrected PR #209 merges; before that, report it as blocked rather than passed.

## 16. Validation requirements

Run from the canonical workspace only:

```powershell
$repo = "D:\260711\MY\AndroidStudioProjects\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\EconomyIntegrityCompile.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform EditMode -assemblyNames AL.EditMode.Tests `
  -testResults "$repo\unity\Logs\EconomyIntegrityEditMode.xml" `
  -logFile "$repo\unity\Logs\EconomyIntegrityEditMode.log"
```

Also run when available:

```text
corrected profile-safe AL.PlayMode.Tests after #127/PR #209
```

Report:

- final base/head SHA;
- exact changed files;
- Unity version and compile exit/final markers;
- focused and complete EditMode totals;
- PlayMode availability/result;
- every amount/wallet/credit/production test matrix row;
- remaining legacy wrapper callers;
- save-call counts for typed and compatibility methods;
- event counts/order;
- final `git diff --check origin/main...HEAD`;
- final repository status;
- every blocked/unperformed check.

Duplicate-workspace evidence is blocked validation.

## 17. Implementation order

1. Fetch current `main` and inspect every open issue/PR and shared-file declaration.
2. Reconfirm no open PR owns the economy interface/service files.
3. Add immutable read/mutation/diagnostic models.
4. Make `ResourceRules` expose one supported/core/optional authority.
5. Add the pure wallet validator and typed reads.
6. Add typed resource add/consume and compatibility wrappers.
7. Add typed Warzone Credit add/spend and compatibility wrappers.
8. Refactor live production into validate → stage contribution/remainder/batch → commit.
9. Add the complete focused test matrix.
10. Inventory remaining legacy callers and link each to its owning issue.
11. Run canonical Unity validation and return a draft PR for GPT review.

## 18. Acceptance criteria

- [ ] Negative consume/spend cannot add value.
- [ ] Negative add is rejected.
- [ ] Add-zero is a no-op; consume/spend-zero is rejected.
- [ ] `long`/`int` overflow or underflow changes nothing.
- [ ] Null, duplicate, negative, and missing-core wallet state cannot mutate or emit a normal event.
- [ ] Unknown stable enum rows are preserved and excluded.
- [ ] Missing optional rare resources read as zero without mutation and are inserted only by a validated positive add/batch.
- [ ] Read methods are pure and expose typed validity.
- [ ] Typed resource and credit primitives do not save.
- [ ] Legacy credit wrappers save exactly once only after an applied mutation and have a documented migration list.
- [ ] Valid resource events fire exactly once after commit; invalid/no-op events fire zero times.
- [ ] Live production rejects invalid delta/dependencies, commits atomically, and never corrupts remainders.
- [ ] Session-only remainder behavior is explicit and tested.
- [ ] No direct grant, reward entitlement, balance/rate, narrative, Android, scene, save-schema, or shared-file change is included.
- [ ] Canonical Unity compile and focused/complete tests pass with exact evidence.

## Codex handoff

```text
Codex engineering: implement issue #163 from current main using unity/Docs/Economy_Integrity_Spec.md and Save_Semantic_Compatibility_Policy.md. Add typed no-save resource and Warzone Credit read/mutation results, preserve compatibility wrappers, validate the complete known wallet without repairing malformed rows, distinguish missing core from optional rare resources, use checked staged arithmetic, and make live production an atomic bounded batch with session-only finite remainders. Do not authorize rewards, change balances/rates, edit shared save/Bootloader files, or absorb #165/#166/#168/#169/#171/#178/#180. Run the full matrix from D:\260711\MY\AndroidStudioProjects\AnotherLife\unity and return one focused draft PR for GPT review.
```
