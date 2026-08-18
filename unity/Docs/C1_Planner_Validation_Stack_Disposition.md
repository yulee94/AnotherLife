# C1 Planner/Validation Stack — Disposition (DELETE)

**Status:** Decided — delete (this document is the evidence record)
**Date:** 2026-08-18
**Author:** default (kanban task t_e9707858)
**Root task:** t_3512e7c2 — Authoritative persistence & deterministic systems

## 1. What "the C1 stack" is

The dead stack is a Codex-era pure-C# domain architecture (`noEngineReferences: true`,
no `MonoBehaviour`), built against the retired "binding spec for issue #180"
(`unity/Docs/Champion_Combat_Encounter_Integrity_Spec.md`). It was never wired into
the runtime.

Scope (all four directories share a self-contained dependency closure):

| Path | Contents | C# lines |
|---|---|---|
| `unity/Assets/AL/Scripts/ChampionMode/C1/` | 12 files — `CombatPrimitives`, `CombatProfileContracts`, `CombatContractValidators`, `CombatDiagnostics`, `AssemblyInfo`, + `Planning/*` (7 planners/catalogs) | 18,220 |
| `unity/Assets/AL/Scripts/ChampionMode/Integration/` | `ChampionEncounterApplicationGateway` + `AssemblyInfo` | 358 |
| `unity/Assets/AL/Tests/EditMode/ChampionCombat/` | 13 test files (C1 planner/validation tests) | 11,704 |
| `unity/Assets/AL/Tests/EditMode/ChampionEncounterIntegration/` | 1 gateway test file | 213 |
| **Total** | | **30,495** |

## 2. Evidence: the stack is dead

1. **No runtime/production callers.** Exhaustive grep of every top-level type in
   `AL.ChampionMode.C1` (11 types), the `AL.ChampionMode.C1` and
   `AL.ChampionMode.Integration` namespaces, and the gateway class
   `ChampionEncounterApplicationGateway`, across all `.cs` in `unity/` — zero
   references outside the four directories above. The only production consumer of
   C1 is the Integration gateway, and the gateway itself has no production consumer
   (only its own test).
2. **The runtime assembly does not reference it.** `unity/Assets/AL/Scripts/AL.Runtime.asmdef`
   references only `AL.SaveAuthority`, `AL.GameDataCatalog`, `Unity.TextMeshPro`,
   `Unity.ugui`. It does not reference C1, Integration, or DeathPenalty.
3. **Dependency closure is self-contained.** `AL.ChampionCombat.C1` is referenced by
   only `AL.ChampionEncounterIntegration` (dead) and the two test asmdefs.
   `AL.ChampionEncounterIntegration` is referenced by only its test asmdef.
4. **No scene/prefab/reflection binding possible.** C1 and Integration are
   `noEngineReferences: true` pure-C# assemblies (no `MonoBehaviour`, no
   `ScriptableObject`), and a string/reflection sweep (`ChampionMode.C1`,
   `ChampionEncounterPlanner`, `ChampionEncounterApplicationGateway`) across
   `.cs/.json/.asset/.prefab/.unity` returned nothing outside the deletion set.
5. **Last touched 12 days ago.** `git log` shows only two commits ever:
   `#290` (2026-08-06) and `#438` (2026-08-06). No maintenance or integration since.

## 3. Not obsolete-by-duplication, but not needed

C1 is not duplicated by the new deterministic `Battle` namespace
(`unity/Assets/AL/Scripts/Battle/`, ~3.9k lines — the sibling task t_932ec999's
concern). Battle owns *computation/simulation*; C1 owns *encounter request
resolution → skill-load session → target/resource/action planning → application
gateway*. That planning layer has no consumer and no planned consumer: the live
game uses the float-based `ChampionCombat` MonoBehaviour (`ChampionMode/Control/`),
and the current greybox slice (sibling tasks t_59bca09b / t_6ef5205e / t_f93cd02b)
is built on `AL.Slice` + `LocalGameDataService`, not on C1.

## 4. Decision: DELETE (do not wire)

- **Wiring** would mean building the entire ChampionEncounter application pipeline
  into the live runtime against a retired architecture, with no consumer and no
  demand — large speculative cost, no benefit.
- **Deletion** is safe: the dependency closure is fully contained in the four
  directories above, so removing them cannot break any remaining assembly. Git
  history (trunk-based paper trail, AGENTS.md rule 2) preserves the code for
  future reference if the encounter-application concept is ever revisited.

## 5. Related finding (out of scope, not deleted)

`unity/Assets/AL/Scripts/ChampionMode/DeathPenalty/` (~2k source lines + tests) is
the same dead pattern (`noEngineReferences`, referenced only by its own test). It is
a candidate for the same disposition in a follow-up, but it is not part of the C1
stack and was left untouched here to keep this change reviewable and scoped.
