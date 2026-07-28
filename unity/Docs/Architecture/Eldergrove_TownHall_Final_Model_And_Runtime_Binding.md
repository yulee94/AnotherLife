# Eldergrove Town Hall Final Model and Runtime Binding

**Status:** Production contract implemented

**Date:** 2026-07-28

**Realm identity:** `Eldergrove`

**Building identity:** `TownHall`

**Stable slot identity:** `kingdom.slot.town-hall`

**Model identity:** `building.eldergrove.townhall.production.v1`

**Approved visual source:**
`Assets/AL/Art/Architecture/ConceptSheets/architecture_eldergrove_townhall_level_progression_v001.png`

This contract closes the Eldergrove Town Hall dimensions, cumulative topology,
atlas, colliders, LODs, loading strategy, Level `10` Open Crown Arbor, and
direct live-kingdom model binding. It does not authorize construction costs,
durations, upgrade commands, economy changes, save migration, workers, damage
states, or final physical-device performance claims.

## Final dimensions

`1 Unity unit = 1 meter`. The root pivot stays at the center-slot origin on
finished ground, and the public entrance faces local `-Z`.

| Item | Final value |
| --- | ---: |
| Stable slot envelope | `16.0 m W × 16.0 m D × 13.0 m H` |
| Maximum art envelope | `15.2 m W × 14.2 m D × 12.6 m H` |
| Level 1 measured bounds | `9.30 m W × 8.78 m D × 5.62 m H` |
| Level 6 measured bounds | `12.42 m W × 8.95 m D × 5.70 m H` |
| Level 10 measured bounds | `12.42 m W × 10.51 m D × 7.96 m H` |
| Live strategic-board scale | `0.09` |

The pivot, entrance, camera focus, activity anchor, output anchor, and stable
slot identity do not move across levels.

## Cumulative topology

The production prefab contains ten ordered gameplay-facing deltas:

```text
Eldergrove_TownHall_Production
├── Entrance
├── CameraFocus
├── Activity_00
├── Output_00
├── Occlusion_Roof
├── Occlusion_Canopies
├── Occlusion_Crown
├── LOD0
│   ├── L01_Delta … L10_Delta
├── LOD1
│   ├── L01_Delta … L10_Delta
├── LOD2
│   ├── L01_Delta … L10_Delta
└── LOD3
    ├── L01_Delta
    ├── L06_Delta
    └── L10_Delta
```

Level `N` enables deltas `1…N`. LOD0–2 preserve every confirmed gameplay
threshold. LOD3 collapses the same cumulative state into Level `1`, `6`, and
`10` silhouette milestones.

| LOD | Screen-relative transition | Triangles | Renderers | Materials |
| --- | ---: | ---: | ---: | ---: |
| `0` | `0.60` | `3,276` | `10` | `2` maximum |
| `1` | `0.30` | `2,432` | `10` | `2` maximum |
| `2` | `0.12` | `1,452` | `10` | `2` maximum |
| `3` | `0.04` | `800` | `3` | `1` |

All bands remain at or beneath the approved `12,000 / 6,000 / 2,500 / 800`
triangle ceilings. Cross-fading is disabled to avoid double-render overlap.

Protected reads at useful distance are:

- one fixed centered public entrance and breathable open civic court;
- pale-stone public floors with unequal dark-timber civic wings;
- exactly three authored primary living-root arches;
- deterministic root feet, drainage, threshold, and council ridge;
- cumulative steward, records, upper-council, rear-service, and forecourt
  additions;
- the Level `10` Open Crown Arbor carried by one continuation from each
  established arch.

## Materials and atlas

The model uses exactly two opaque, instancing-enabled visible materials:

1. `MAT_Eldergrove_TownHall_Atlas` for pale stone, dark timber, bark, bronze,
   and restrained surface variation.
2. `MAT_Eldergrove_TownHall_Accent` for one localized non-emissive public leaf
   waymark.

`T_Eldergrove_TownHall_Atlas_1024` is one `1024 × 1024` RGB atlas with
mipmaps, high-quality compression, no alpha channel, and CPU read/write
disabled. Both materials remain non-emissive. The concept sheet remains
source-only and is not a prefab dependency.

The Town Hall does not reuse the Atelier/Workshop atlas. It reuses only the
approved `eldergrove.atelier` construction-motion profile because that profile
represents Eldergrove motion grammar, not Workshop geometry or activity.

## Colliders and anchors

The production root owns exactly two `BoxCollider` components:

| Role | Trigger | Center | Size |
| --- | --- | --- | --- |
| Selection | Yes | `(0, 5.65, -0.05)` | `(14.0, 11.3, 12.8)` |
| Navigation | No | `(0, 0.8, -0.05)` | `(12.7, 1.6, 10.7)` |

Render meshes and level deltas own no colliders. `Entrance`, `CameraFocus`,
`Activity_00`, and `Output_00` remain behavior-free. `Occlusion_Roof`,
`Occlusion_Canopies`, and `Occlusion_Crown` are stable markers for later
camera behavior; this pass does not add another occlusion service.

## Final Level 10 capstone

The final capstone is the **Open Crown Arbor**:

- exactly three bent mature-root continuations rise from the established
  Level `1` arches;
- each continuation physically meets the crown;
- one fixed horizontal bronze ring frames the sky;
- the oculus contains no mesh, light, particle, orb, seed lantern, shrine
  focus, portal, or permanent green emission.

The prefab contains no Animator, particle system, light, or audio source.
Changing the stable slot, fixed entrance, exactly-three-arch identity, open
court, empty supported crown, direct confirmed-level authority, two-material
ceiling, two-collider strategy, or four-LOD policy is a major
design-direction change.

## Loading and live binding

`KingdomBuildingModelCatalog` remains the packaged loading boundary:

- the entry is keyed by exact `RealmId.Eldergrove + BuildingId.TownHall`;
- it directly references the production prefab;
- all four Workshop bindings and the Stonehold Town Hall binding remain
  unchanged;
- a declared invalid binding fails visibly as
  `ProductionModelUnavailable`.

The confirmed gameplay level is the only presentation authority:

```text
BuildingState.Level
→ KingdomBuildingPresentationResolver
→ kingdom.slot.town-hall
→ KingdomBuildingModelCatalog
→ Eldergrove_TownHall_Production
→ KingdomBuildingLevelModel.ApplyConfirmedLevel
```

- Level `0`: keep the reserved plot; instantiate no production model.
- Level `1…10`: enable cumulative confirmed deltas.
- Active upgrade: keep the confirmed model and generic worksite feedback; do
  not reveal the target delta.
- Adjacent confirmation in the current session: animate only the newly
  confirmed delta once with the compatible Eldergrove realm motion profile.
- First load, reconnect, same-level refresh, invalid state, reduced motion, or
  multi-level reconciliation: settle directly at the confirmed level.
- No visual stage, target level, or animation stage is persisted.
- Presentation does not call upgrade, economy, save, quest, or timer services.

The sixth catalog entry does not justify Addressables, AssetBundles, remote
delivery, or a second loading service. Revisit that direction only after
measured package-size or residency evidence.

## Production assets

- Builder:
  `Assets/AL/Scripts/Editor/Architecture/EldergroveTownHallProductionModelBuilder.cs`
- Runtime prefab:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Runtime/Eldergrove_TownHall_Production.prefab`
- Runtime atlas, materials, and meshes:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Runtime`
- Packaged catalog:
  `Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`
- Review scene:
  `Assets/AL/Scenes/Prototypes/EldergroveTownHallProductionModel.unity`
- Focused tests:
  `Assets/AL/Tests/EditMode/Architecture/EldergroveTownHallProductionModelTests.cs`

The review scene remains excluded from Player build settings.

## Validation

| Gate | Result |
| --- | --- |
| Visual verdict | `92 / 100`, pass |
| Static architecture mobile safety | `94 / 100`, pass |
| Focused Eldergrove Town Hall production EditMode | `21 / 21` |
| Android Architecture EditMode | `242 / 242` |
| iOS Architecture EditMode | `242 / 242` |
| Unity iOS Player export | pass |
| Xcode 26.6 unsigned ARM64 native build | pass |
| iOS deployment floor | `15.0` |
| iOS simulator architecture | ARM64 |

Xcode compiled and linked with `arm64-apple-ios15.0`. Its generated app icon
catalog still reports the existing requirement for a `1024 × 1024` App Store
icon; that packaging warning is outside this model slice.

The static score is not a physical performance claim. Still required:

- populated-kingdom frame time, memory, draw-call, thermal, battery, and
  texture-residency profiling on representative Android and iOS devices;
- an actual iOS `15` runtime launch when a compatible runtime or device is
  available;
- final damaged, destroyed, repairing, selected, disabled, and unavailable
  art;
- bounded civic activity design and measured effect budgets;
- Crownlands and Umbral Town Hall production models and exact bindings;
- progression, economy, save, game-data, and command authority before enabling
  live Town Hall upgrade actions.
