# Bootloader Service-Stack Integrity Specification

**Status date:** 2026-07-15  
**Specification owner:** GPT  
**Implementation owner:** Codex  
**Tracking issue:** #153  
**Baseline `main`:** `a6232e63c807f055cc43b302ad4e62b846c236ca`  
**Shared-file lock required:** `unity/Assets/AL/Scripts/Core/Bootloader.cs`

## 1. Goal

Replace the current single-`IResourceService` readiness sentinel with one deterministic, coherent, idempotent offline service-stack lifecycle.

The implementation must:

- build every current offline service from one coherent set of root instances;
- publish the stack only after construction succeeds;
- identify completion with a marker registered last;
- reuse a verified complete stack without replacing instances;
- reject partial, stale, or foreign/custom registries visibly rather than silently cross-wiring or overwriting them;
- keep `Awake`, `Update`, pause, and quit safe when initialization fails;
- expose exact phase/type diagnostics;
- remain independent from narrative, save-format, scene, economy, terrestrial-design, and NVS-01 changes.

## 2. Non-goals

Do not:

- add or remove domain services;
- change service behavior, balance, narrative, quest meaning, or save fields;
- make `ServiceLocator.Clear()` public;
- redesign dependency injection across the project;
- support arbitrary hot-swapping of services during gameplay;
- silently adopt an unknown partial custom test stack;
- change Build Settings or scene flow;
- combine #127, #137, #150, or #134 implementation.

## 3. Verified current defect

`Bootloader.InitializeIfMissing()` currently treats this lookup as proof that the full stack exists:

```csharp
ServiceLocator.Get<IResourceService>();
```

When that one service exists, initialization returns even if save, story, quest, notification, world, or reward services are absent or refer to incompatible roots.

`Awake`, `Update`, `OnApplicationPause`, and `OnApplicationQuit` then use throwing `Get<T>()` calls without an initialization result guard.

The current code also registers services while construction is still in progress. A constructor or registration failure can leave a partial graph that the next Bootloader may misclassify as complete.

## 4. Architecture decision

Use four bounded technical additions:

1. **Non-throwing lookup** in `ServiceLocator`.
2. **Offline stack factory/snapshot** that constructs every service before publication.
3. **Completion marker** registered last and containing the exact expected instance map/version.
4. **Typed initialization result** consumed by the Bootloader lifecycle.

A marker is authoritative only when every required registration still matches the marker’s expected object reference.

## 5. Required service inventory

The current offline stack contains these required registrations in this dependency order.

### Root services

| Registration | Implementation | Direct dependencies |
| --- | --- | --- |
| `IGameDataService` | `LocalGameDataService` | none |
| `ISaveGameService` | `LocalSaveGameService` | none |

### Realm and kingdom services

| Registration | Implementation | Direct dependencies |
| --- | --- | --- |
| `IRealmService` | `LocalRealmService` | save, game data |
| `IResourceService` | `LocalResourceService` | save |
| `IResearchService` | `LocalResearchService` | save, resources |
| `IBuildingService` | `LocalBuildingService` | save, resources, game data |
| `ITrainingService` | `LocalTrainingService` | save, resources |

### Battle, economy, narrative support, and world services

| Registration | Implementation | Direct dependencies |
| --- | --- | --- |
| `IBattleSimulator` | `FixedPointBattleSimulator` | none |
| `IWarzoneCreditService` | `LocalWarzoneCreditService` | save |
| `IWarmasterService` | `LocalWarmasterService` | save, Warzone Credits |
| `ITerritoryService` | `WarzoneService` | save |
| `IQuestService` | `LocalQuestService` | save, resources, Warzone Credits |
| `IStoryService` | `LocalStoryService` | save, game data |
| `IReputationService` | `ReputationService` | save |
| `IFactionService` | `FactionService` | save |
| `IPersonaService` | `PersonaService` | save |
| `INotificationService` | `LocalNotificationService` | none |
| `IRealmGemService` | `LocalRealmGemService` | save |
| `IWorldStateService` | `WorldStateService` | save, notifications |
| `IWorldAtlasService` | `LocalWorldAtlasService` | story |
| `IBossLootService` | `LocalBossLootService` | save, Warzone Credits, notifications |

The completion marker is an additional technical registration and is not a gameplay service.

## 6. `ServiceLocator` additions

Add the narrowest APIs needed by Bootloader and tests, for example:

```csharp
public static bool TryGet<T>(out T service)
public static bool IsRegistered<T>()
```

Equivalent APIs are acceptable if they:

- do not throw for absence;
- never return an incompatible cast as success;
- do not mutate the registry;
- preserve the current `Get<T>()` compatibility API.

Add one internal batch-publication operation or equivalent rollback-capable helper.

### Batch publication requirements

- receive the complete required type → instance set plus marker;
- validate no null type or instance;
- snapshot all affected existing entries;
- apply every service registration;
- register marker last;
- if any publication step fails, restore the exact prior affected entries and remove any newly added marker;
- return a typed failure rather than leaving a partially published local stack.

Do not expose general public unregister/clear behavior solely for this task.

## 7. Offline stack snapshot

Construct all service instances in local memory before touching `ServiceLocator`.

A technical snapshot should expose:

```text
stackVersion
registrationId
ownerKind = LocalOffline
saveRoot
 gameDataRoot
required type → exact object instance map
```

The implementation may use a class, immutable dictionary, or strongly typed properties. The following invariants are mandatory:

- exactly one save root;
- exactly one game-data root;
- every dependent service was constructed from the snapshot’s intended roots;
- no required registration is null;
- no duplicate service type;
- the required type set exactly matches the reviewed inventory;
- marker data is immutable after construction.

A factory seam may accept internal constructor delegates for fault-injection tests. Production uses the current local implementations and values.

## 8. Completion marker

Use an internal interface/class such as:

```text
IOfflineServiceStackMarker
LocalOfflineServiceStackMarker
```

The marker contains:

- supported stack version;
- unique registration ID;
- owner kind;
- exact required type → instance map;
- root references needed for diagnostics;
- creation timestamp only for diagnostics, never for readiness ordering.

### Complete-stack validation

A stack is complete only when:

1. the marker exists;
2. marker version is supported;
3. every required type is registered;
4. every current registration is reference-equal to the marker’s expected instance;
5. no expected instance is null;
6. marker root references match its expected root registrations.

Do not use implementation type alone as coherence proof. Reference identity against the marker prevents a single registration from being replaced after initialization while the old marker remains.

## 9. Initialization state machine

`InitializeIfMissing()` must return a typed result rather than relying on exceptions and logs alone.

Suggested states:

```text
NotStarted
ReusedCompleteStack
CreatedCompleteStack
FailedPartialRegistry
FailedInconsistentMarker
FailedConstruction
FailedPublication
```

Equivalent names are acceptable.

### 9.1 Complete marker present

- validate the marker and all expected references;
- if valid, reuse every existing instance;
- return `ReusedCompleteStack`;
- do not reconstruct, re-register, load, or save merely because another Bootloader awakened.

### 9.2 Registry contains none of the required types and no marker

- construct the full snapshot;
- validate it;
- publish the batch with marker last;
- re-read and validate the installed marker/registrations;
- return `CreatedCompleteStack`.

### 9.3 Any required service exists but no marker exists

Treat this as a partial, legacy, foreign, or custom registry.

Required behavior:

- enumerate present and missing required types;
- make no registry changes;
- do not overwrite or complete it by guessing roots;
- return `FailedPartialRegistry`;
- stop Bootloader-owned gameplay startup visibly.

This rule preserves intentional test/custom registrations and prevents cross-wired services. Tests needing Bootloader startup must register an approved complete stack/marker or begin from an empty registry.

### 9.4 Marker exists but validation fails

Examples:

- required service missing;
- service replaced after marker creation;
- unsupported marker version;
- root mismatch;
- null expected instance.

Required behavior:

- return `FailedInconsistentMarker`;
- report exact mismatched/missing type;
- do not silently rebuild or overwrite the registry;
- stop Bootloader-owned startup.

A future explicit recovery API may rebuild a locally owned stack, but this issue does not authorize hot replacement after an inconsistent marker.

### 9.5 Construction failure

- registry remains byte-for-byte/type-for-type unchanged;
- no marker exists;
- result identifies the construction phase/service type;
- no `Load()` call occurs.

### 9.6 Publication failure

- restore the exact prior affected registry snapshot;
- no completion marker remains from the failed attempt;
- result identifies publication phase/type;
- no `Load()` call occurs.

## 10. Bootloader lifecycle behavior

Store the initialization result on the component or equivalent state.

### `Awake`

```text
initialize
→ if failed: report once, disable Bootloader-owned runtime work, do not load
→ if created/reused complete and auto-load: TryGet save → Load
```

- no throwing `Get<T>()` after a failed result;
- load failure is reported separately from initialization failure;
- multiple Bootloaders may reuse one complete stack, but load should not be unintentionally duplicated by every Bootloader instance.

Define one load-once policy for the current scene/session, for example marker/session load state or an owning Bootloader guard. At minimum, tests must prove two Bootloaders do not apply offline progress twice.

### `Update`

- run only after successful initialization;
- use non-throwing resource lookup;
- if a previously complete required service disappears/replaces, transition to a visible runtime-stack failure once and stop ticking;
- do not repeatedly log every frame.

### `OnApplicationPause(true)` and `OnApplicationQuit`

- use non-throwing save lookup;
- save only when initialization succeeded and a save service/current profile is available according to its contract;
- catch/report save failure at the save boundary;
- never throw an unhandled “service not registered” exception;
- do not create/reinitialize the stack from pause/quit.

### Component failure state

The Bootloader may set `enabled = false` after a blocking initialization failure if lifecycle save handlers remain explicitly safe. Do not destroy unrelated scene objects or load another scene as an implicit recovery.

## 11. Diagnostics

Use stable codes and structured data where practical.

Minimum fields:

```text
code
initializationState
stackVersion
registrationId when available
phase
serviceType
presentRequiredTypes
missingRequiredTypes
mismatchedTypes
message
```

Suggested codes:

```text
BOOT_STACK_REUSED
BOOT_STACK_CREATED
BOOT_STACK_PARTIAL_REGISTRY
BOOT_STACK_MARKER_INCONSISTENT
BOOT_STACK_CONSTRUCTION_FAILED
BOOT_STACK_PUBLICATION_FAILED
BOOT_STACK_LOAD_FAILED
BOOT_STACK_RUNTIME_DRIFT
```

Do not include raw save contents or sensitive local paths in player-facing messages. Final visible delivery integrates with #177 later; stable logs/results are required now.

## 12. Required tests

### Empty and complete registry

- empty registry creates all 21 required services plus marker;
- every registration matches the marker expected instance;
- marker roots match save/game-data registrations;
- initialization result is `CreatedCompleteStack`;
- second initialization returns `ReusedCompleteStack` and preserves every reference;
- no duplicate load/offline progress from repeated Bootloader startup.

### Partial registry

- only `IResourceService` registered;
- only `ISaveGameService` registered;
- story without quest;
- quest without story;
- several local-looking types with no marker;
- one foreign/mock implementation.

For each:

- result is partial-registry failure;
- exact present/missing types reported;
- registry unchanged;
- no marker added;
- no save load, resource tick, or lifecycle save invoked.

### Marker inconsistency

- marker present with one service missing;
- marker present after one service is replaced;
- unsupported marker version;
- mismatched root registration;
- null expected instance where injectable.

For each:

- visible deterministic failure;
- no silent rebuild/overwrite;
- no load/tick/save.

### Construction and publication faults

Inject failure at:

- game-data construction;
- save construction;
- representative dependent service construction;
- notification/world service construction;
- final boss-loot construction;
- registration before marker;
- marker registration;
- post-install verification.

Assert exact registry rollback and no false complete state.

### Lifecycle safety

- `Awake` failure does not throw;
- `Update` before/after failure does nothing safely;
- pause/quit with no save service does not throw;
- pause/quit after complete stack calls save through the expected instance;
- service replacement after marker causes one runtime-drift failure and stops ticking;
- scene reload/repeated Bootloader does not duplicate stack or offline progress.

### Regression

- all current required types remain registered;
- current valid local stack behavior remains available;
- current save load occurs once in the intended boot path;
- current representative scene and safe #127 PlayMode suite pass when available;
- no production public clear/reset method is added.

## 13. Expected file boundary

Likely:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs
unity/Assets/AL/Scripts/Core/ServiceLocator.cs
new narrow offline-stack snapshot/marker/result types under Core or Services/Local
focused EditMode tests
focused PlayMode tests after #127
```

`Bootloader.cs` holds the shared-file soft lock for the implementation PR.

Do not edit:

- `SaveGameData.cs`;
- `LocalGameDataService.cs` content authority;
- `ProjectInitializer.cs`;
- Android source;
- narrative files;
- terrestrial design files;
- scenes or Build Settings;
- service domain behavior beyond construction/registration lifecycle.

## 14. Merge and implementation order

1. Start from fetched current `main` and inspect all open PRs.
2. Declare the `Bootloader.cs` lock in the draft PR before editing.
3. Add non-throwing lookup and the snapshot/marker/result types.
4. Add factory and fault seams without changing service behavior.
5. Implement complete/empty/partial/inconsistent state handling.
6. Make Bootloader lifecycle consume the typed result safely.
7. Add focused EditMode tests first.
8. Run Unity compile and exact EditMode totals.
9. Run safe PlayMode after #127 is available; until then report it as unavailable, not passing.
10. Rebase and return for Codex coordination/review before merge.

## 15. Acceptance criteria

- [ ] One arbitrary service is no longer a readiness sentinel.
- [ ] Exactly the reviewed required type set defines the offline stack.
- [ ] Every stack is constructed before publication.
- [ ] Completion marker is registered last and validated by exact instance identity.
- [ ] Empty registry creates one coherent stack.
- [ ] Repeated initialization preserves exact references and does not duplicate load/offline progress.
- [ ] Partial or foreign registry is left unchanged and fails visibly.
- [ ] Inconsistent marker is left unchanged and fails visibly.
- [ ] Construction/publication failure leaves no false marker or partial local stack.
- [ ] `Awake`, `Update`, pause, and quit cannot throw because a service is missing.
- [ ] Runtime registry drift is detected once and stops Bootloader-owned work.
- [ ] Full focused tests and Unity compilation pass with exact evidence.
- [ ] Shared-file lock is declared and released.
- [ ] No narrative, save-format, gameplay, balance, Android, terrestrial-design, or unrelated change is included.

# Codex handoff

```text
Codex: implement issue #153 from current main using Bootloader_Service_Stack_Integrity_Spec.md. Declare the Bootloader.cs soft lock. Construct all current local services before publication, register an immutable completion marker last, validate exact instance identity, reuse only a verified complete marker stack, and leave any partial/foreign registry unchanged with a typed visible failure. Make Awake/Update/pause/quit safe, prevent duplicate load/offline progress, add construction/publication fault tests, and do not modify domain behavior, save fields, scenes, narrative, Android, or terrestrial design.
```
