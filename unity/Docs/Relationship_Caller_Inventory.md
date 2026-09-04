# Relationship Caller and Interface Inventory

**Status date:** 2026-09-04
**Tracking issue:** #176
**Audited baseline:** `756173a2` (`origin/main` after PR #728)
**Primary mode:** engineering
**Phase:** save-backed typed service and standalone clone → apply → persist/verify → publish adapter over the merged unregistered planner

## Current production surface

| Surface | Current location | Verified use on the audited baseline |
| --- | --- | --- |
| `IReputationService` | `unity/Assets/AL/Scripts/Core/Interfaces/IReputationService.cs` | Implemented by `ReputationService`. Bootloader constructs one instance. |
| `IReputationService.GetAffinity` / `ChangeAffinity` / `GetAffinityRank` | same | Prototype save-backed first-row behavior; rank strings remain hard-coded English. Contained by `ProfileMutationContainment`. |
| `IFactionService` | `unity/Assets/AL/Scripts/Core/Interfaces/IFactionService.cs` | Implemented by `FactionService`. Bootloader constructs one instance. |
| `IFactionService.GetReputation` / `AdjustReputation` / `GetFactionAffiliation` | same | Prototype save-backed first-row behavior; affiliation strings remain hard-coded English. Contained. |
| `IPersonaService` | `unity/Assets/AL/Scripts/Core/Interfaces/IPersonaService.cs` | Implemented by `PersonaService`. Bootloader constructs one instance. |
| `IPersonaService.GetTraitValue` / `AdjustTrait` / `GetDominantTrait` | same | Unchecked integer mutation; missing object returns `Sage`; all-zero/tie returns `Warlord`. Contained. |
| `ReputationService` construction | `unity/Assets/AL/Scripts/Core/Bootloader.cs` | Registered as `IReputationService`. |
| `FactionService` construction | same | Registered as `IFactionService`. |
| `PersonaService` construction | same | Registered as `IPersonaService`. |
| Save persistence regression | `unity/Assets/AL/Tests/EditMode/SavePersistenceRegressionTests.cs` | Invokes the three legacy mutators by reflection. |
| NVS-01 runtime scan | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01QuestRuntimeTests.cs` | String inventory only; `ChangeAffinity(` is listed as a forbidden production coupling, not a live caller. |

Repository-wide exact-symbol search found no other production caller of `ChangeAffinity`, `AdjustReputation`, `AdjustTrait`, `GetAffinityRank`, `GetFactionAffiliation`, or `GetDominantTrait`.

## Phase D additive surface

The unregistered planner remains under:

```text
unity/Assets/AL/Scripts/Core/Interfaces/Relationships/
unity/Assets/AL/Scripts/Services/Relationships/
```

This slice adds:

- typed snapshot/query/plan APIs over cloned candidates (`RelationshipDurableService`);
- standalone adapter: clone → apply → persist/verify once → publish;
- commit events after verified persistence, with isolated subscriber failure;
- optional notification outbox enqueue after commit (no presenter, no player copy);
- `RelationshipLegacyCompatibilityAdapter` mapping the three void wrappers onto the adapter for explicit construction only.

`#347` IDs, aliases, and legacy thresholds remain injected fixture records. They are not production `#183` catalog authority.

Nothing in this phase is registered in `Bootloader`, referenced by production `ReputationService` / `FactionService` / `PersonaService`, connected to NVS report composition, or connected to a notification presenter.

## #183 fail-closed boundary

Issue `#183` remains OPEN. Production family inputs and global generation approval are pending. This slice therefore:

- does not add a relationship family to the game-data catalog foundation;
- does not load `al_relationship_authority_content_catalog.json` as runtime authority;
- returns `CatalogPending` / `CatalogUnavailable` when the injected resolver is constructed in those modes, even if fixture records are supplied;
- rejects mutation planning and durable commit with `RejectedPolicyUnavailable` / `RejectedValidation`.

## #137 / NVS-01 boundary

`#137` clone/persist/publish exists for the two typed legacy candidate adapters (realm bootstrap/selection and NVS-01). Consumer-visible `Writable` is not activated. Schema-one ordinary writers, including the Bootloader-registered relationship services, remain contained.

This slice does **not**:

- edit `SaveGameData.cs`, `LocalSaveGameService.cs`, or `Bootloader.cs`;
- add a third production candidate adapter;
- implement the NVS-01 report transaction or the approved `+5` Valerius composition;
- invent faction/persona NVS consequences.

Standalone persist/verify uses an injected `IRelationshipCandidatePersistence` port. Tests use `InMemoryRelationshipCandidatePersistence`.

## Preserved migration boundary

Inventory allows no Bootloader wrapper replacement in this PR:

- no production caller of `ChangeAffinity` / `AdjustReputation` / `AdjustTrait` except the contained services themselves and persistence-regression reflection;
- `#183` production identity/policy catalogs are unavailable;
- `#137` consumer-visible `Writable` is not activated.

The following prototype behavior therefore remains intentionally unchanged in Bootloader-registered services:

- nullable/first-row `IReputationService` / `IFactionService` / `IPersonaService` compatibility surface;
- hard-coded English rank/affiliation labels;
- missing-persona `Sage` and all-zero `Warlord` dominant-trait wrappers;
- independent `_saveGameService.Save()` after each legacy mutation, still gated by containment;
- existing `Bootloader` construction and service registration.

`RelationshipLegacyCompatibilityAdapter` is the explicit, unregistered mapping for later replacement. No new caller should use the Bootloader-registered legacy API.

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
RelationshipDurableService
RelationshipLegacyCompatibilityAdapter
```

## Impact statement

The Phase D implementation is platform-neutral managed code with bounded collection copies, one persist attempt per applied standalone mutation, and no per-frame work, asset, dependency, scene, Player resource, package, or Android change. It is dormant unless a later caller explicitly constructs it. Runtime memory, frame time, build/install size, and device behavior are therefore unchanged in production for this phase.
