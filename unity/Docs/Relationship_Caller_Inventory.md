# Relationship Caller and Interface Inventory

**Status date:** 2026-09-04
**Tracking issue:** #176
**Audited baseline:** `ce9f4fd981c5e2ae01c439efcf358e525024a5b4`
**Primary mode:** engineering
**Phase:** unregistered immutable identity resolver and pure snapshot/mutation planner only

## Current production surface

| Surface | Current location | Verified use on the audited baseline |
| --- | --- | --- |
| `IReputationService` | `unity/Assets/AL/Scripts/Core/Interfaces/IReputationService.cs` | Implemented by `ReputationService`. Bootloader constructs one instance. |
| `IReputationService.GetAffinity` / `ChangeAffinity` / `GetAffinityRank` | same | Prototype save-backed first-row behavior; rank strings remain hard-coded English. |
| `IFactionService` | `unity/Assets/AL/Scripts/Core/Interfaces/IFactionService.cs` | Implemented by `FactionService`. Bootloader constructs one instance. |
| `IFactionService.GetReputation` / `AdjustReputation` / `GetFactionAffiliation` | same | Prototype save-backed first-row behavior; affiliation strings remain hard-coded English. |
| `IPersonaService` | `unity/Assets/AL/Scripts/Core/Interfaces/IPersonaService.cs` | Implemented by `PersonaService`. Bootloader constructs one instance. |
| `IPersonaService.GetTraitValue` / `AdjustTrait` / `GetDominantTrait` | same | Unchecked integer mutation; missing object returns `Sage`; all-zero/tie returns `Warlord`. |
| `ReputationService` construction | `unity/Assets/AL/Scripts/Core/Bootloader.cs` | Registered as `IReputationService`. |
| `FactionService` construction | same | Registered as `IFactionService`. |
| `PersonaService` construction | same | Registered as `IPersonaService`. |
| Save persistence regression | `unity/Assets/AL/Tests/EditMode/SavePersistenceRegressionTests.cs` | Invokes the three legacy mutators by reflection. |
| NVS-01 runtime scan | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01QuestRuntimeTests.cs` | String inventory only; `ChangeAffinity(` is listed as a forbidden production coupling, not a live caller. |

Repository-wide exact-symbol search found no other production caller of `ChangeAffinity`, `AdjustReputation`, `AdjustTrait`, `GetAffinityRank`, `GetFactionAffiliation`, or `GetDominantTrait`.

## Phase B additive surface

The unregistered planner lives under:

```text
unity/Assets/AL/Scripts/Core/Interfaces/Relationships/
unity/Assets/AL/Scripts/Services/Relationships/
```

It adds immutable identity/policy ports, typed snapshots/queries/classification/request/plan/apply-result/event descriptions, an injected identity/policy resolver, pure snapshot builders, a pure mutation planner, and an in-memory fake mutation-target/ledger seam.

`#347` IDs, aliases, and legacy thresholds are used only as injected fixture records. They are not production `#183` catalog authority.

Nothing in this phase is registered in `Bootloader`, referenced by `ReputationService` / `FactionService` / `PersonaService`, connected to a save, connected to NVS, or connected to notifications.

## #183 fail-closed boundary

Issue `#183` remains OPEN. Production family inputs and global generation approval are pending. This slice therefore:

- does not add a relationship family to the game-data catalog foundation;
- does not load `al_relationship_authority_content_catalog.json` as runtime authority;
- returns `CatalogPending` / `CatalogUnavailable` when the injected resolver is constructed in those modes, even if fixture records are supplied;
- rejects mutation planning with `RejectedPolicyUnavailable`.

## Preserved migration boundary

The following prototype behavior remains intentionally unchanged:

- nullable/first-row `IReputationService` / `IFactionService` / `IPersonaService` compatibility surface;
- hard-coded English rank/affiliation labels;
- missing-persona `Sage` and all-zero `Warlord` dominant-trait wrappers;
- independent `_saveGameService.Save()` after each legacy mutation;
- existing `Bootloader` construction and service registration.

Those surfaces may change only in a later durable-integration successor after `#137`, `#183` production identity/policy catalogs, and the owning transaction ledger are accepted. No new caller should use the legacy API.

## Verification queries

```text
IReputationService
IFactionService
IPersonaService
ChangeAffinity
AdjustReputation
AdjustTrait
GetAffinityRank
GetFactionAffiliation
GetDominantTrait
IRelationshipIdentityResolver
RelationshipMutationPlanner
```

The inventory must be refreshed from current `main` before the later service-integration phase.

## Impact statement

The Phase B implementation is platform-neutral managed code with bounded collection copies and no per-frame work, asset, dependency, scene, Player resource, package, or Android change. It is dormant unless a later caller explicitly constructs it. Runtime memory, frame time, build/install size, and device behavior are therefore unchanged in production for this phase.
