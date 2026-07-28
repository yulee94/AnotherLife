# Stonehold Town Hall Final Model and Runtime Binding

**Status:** Production contract implemented

**Date:** 2026-07-28

**Realm identity:** `Stonehold`

**Building identity:** `TownHall`

**Stable slot identity:** `kingdom.slot.town-hall`

**Model identity:** `building.stonehold.townhall.production.v1`

**Approved visual source:**
`Assets/AL/Art/Architecture/ConceptSheets/architecture_stonehold_townhall_level_progression_v001.png`

This contract closes the Stonehold Town Hall dimensions, cumulative topology,
atlas, colliders, LODs, loading strategy, Level `10` Oathstone Crown, and
direct live-kingdom model binding. It does not authorize gameplay construction
costs, duration, upgrade commands, economy changes, save migration, workers,
damage states, or final physical-device performance claims.

## Final dimensions

`1 Unity unit = 1 meter`.

The root pivot remains the stable center-slot origin on the finished ground
plane. The public entrance faces local `-Z`.

| Item | Final value |
| --- | ---: |
| Stable slot envelope | `16.0 m W × 16.0 m D × 13.0 m H` |
| Maximum art envelope | `15.2 m W × 14.2 m D × 12.6 m H` |
| Level 1 measured bounds | `9.35 m W × 8.84 m D × 6.33 m H` |
| Level 6 measured bounds | `12.66 m W × 9.09 m D × 6.74 m H` |
| Level 10 measured bounds | `12.66 m W × 10.66 m D × 10.45 m H` |
| Live strategic-board scale | `0.09` |

The root pivot, entrance, camera focus, activity anchor, output anchor, and
slot identity do not move across levels.

## Cumulative topology

The production prefab contains ten ordered gameplay-facing deltas:

```text
Stonehold_TownHall_Production
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
| `0` | `0.60` | `1,368` | `10` | `2` maximum |
| `1` | `0.30` | `1,052` | `10` | `2` maximum |
| `2` | `0.12` | `684` | `10` | `2` maximum |
| `3` | `0.04` | `564` | `3` | `1` |

All bands remain beneath the approved `12,000 / 6,000 / 2,500 / 800`
triangle ceilings. Cross-fading is disabled to avoid double-render overlap in
a populated mobile kingdom.

Protected reads at useful distance are:

- fixed clipped public entrance and safe stepped threshold;
- broad Stonehold masonry mass and correctly pitched paired roof plates;
- unequal records and assembly wings;
- paired load-bearing buttress towers and continuous civic lintel;
- grounded upper council course;
- Level `10` Oathstone Crown carried through the established roof load line.

## Materials and atlas

The model uses exactly two opaque, instancing-enabled visible materials:

1. `MAT_Stonehold_TownHall_Atlas` for basalt, masonry, iron roof plates,
   timber service work, restrained wear, and value separation.
2. `MAT_Stonehold_TownHall_Accent` for the narrow entrance and crown amber
   slits only.

`T_Stonehold_TownHall_Atlas_1024` is one `1024 × 1024` RGB atlas with mipmaps,
high-quality compression, no alpha channel, and CPU read/write disabled.
Concept sheets remain source-only and are not prefab dependencies.

The Town Hall does not reuse the Workshop atlas. It reuses only the approved
Stonehold construction-motion profile because that profile represents realm
motion grammar rather than Workshop geometry or activity.

## Colliders and anchors

The production root owns exactly two `BoxCollider` components:

| Role | Trigger | Center | Size |
| --- | --- | --- | --- |
| Selection | Yes | `(0, 6.1, -0.2)` | `(14.2, 12.2, 12.9)` |
| Navigation | No | `(0, 1.0, -0.1)` | `(12.8, 2.0, 10.8)` |

Render meshes and level deltas own no colliders. `Entrance`, `CameraFocus`,
`Activity_00`, and `Output_00` are stable behavior-free anchors.
`Occlusion_Roof`, `Occlusion_Canopies`, and `Occlusion_Crown` are stable
group markers for later camera behavior; this pass does not add a new
occlusion service.

## Final Level 10 capstone

The final capstone is the **Oathstone Crown**:

- one widened grounded load base intersecting the established roof line;
- one stepped second civic course;
- four physically supported belfry piers;
- one fixed iron oath plate and stone boss;
- one correctly pitched paired crown roof and ridge;
- one narrow contained amber civic slit;
- two grounded roof-load brackets.

It remains static. The prefab contains no Animator, particle system, light,
audio source, levitation, continuous bell motion, forge chimney, weapon,
throne, or portal treatment.

Changing the stable center slot, fixed entrance, civic-not-keep role, grounded
Oathstone Crown, direct confirmed-level authority, two-material ceiling,
two-collider strategy, or four-LOD policy is a major design-direction change.

## Loading and live binding

`KingdomBuildingModelCatalog` remains the packaged loading boundary:

- the catalog lives in `Resources` and is cached by `CityLayoutEngine`;
- the entry is keyed by exact `RealmId.Stonehold + BuildingId.TownHall`;
- the entry directly references the production prefab;
- all four Workshop bindings remain unchanged;
- missing undeclared families retain the legacy procedural presentation;
- a declared invalid binding fails visibly as
  `ProductionModelUnavailable`.

The confirmed gameplay level is the only presentation authority:

```text
BuildingState.Level
→ KingdomBuildingPresentationResolver
→ kingdom.slot.town-hall
→ KingdomBuildingModelCatalog
→ Stonehold_TownHall_Production
→ KingdomBuildingLevelModel.ApplyConfirmedLevel
```

- Level `0`: retain the reserved stable plot; instantiate no production model.
- Level `1…10`: enable cumulative confirmed deltas.
- Active upgrade: retain the confirmed model and existing generic worksite
  feedback; do not reveal the target delta.
- Adjacent confirmation during the current session: animate the newly
  confirmed delta once with the compatible Stonehold realm motion profile.
- First load, reconnect, same-level refresh, invalid state, reduced motion, or
  multi-level reconciliation: settle directly at the confirmed level.
- No visual stage, target level, or animation stage is persisted.
- Presentation does not call upgrade, economy, save, quest, or timer services.

The fifth catalog entry does not justify Addressables, AssetBundles, remote
delivery, or a second loading service. Revisit that direction only after
measured package size, residency, or content-delivery evidence.

## Production assets

- Builder:
  `Assets/AL/Scripts/Editor/Architecture/StoneholdTownHallProductionModelBuilder.cs`
- Runtime prefab:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Production/TownHall/Runtime/Stonehold_TownHall_Production.prefab`
- Runtime atlas, materials, and meshes:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Production/TownHall/Runtime`
- Packaged catalog:
  `Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`
- Review scene:
  `Assets/AL/Scenes/Prototypes/StoneholdTownHallProductionModel.unity`
- Focused tests:
  `Assets/AL/Tests/EditMode/Architecture/StoneholdTownHallProductionModelTests.cs`

The review scene remains excluded from Player build settings.

## Validation

| Gate | Result |
| --- | --- |
| Visual verdict | `92 / 100`, pass |
| Static architecture mobile safety | `94 / 100`, pass |
| Focused Stonehold Town Hall production EditMode | `21 / 21` |
| Android Architecture EditMode | `221 / 221` |
| iOS Architecture EditMode | `221 / 221` |
| Unity iOS Player export | pass |
| Xcode unsigned ARM64 native build | pass |
| iOS deployment floor | `15.0` |
| iOS simulator architecture | ARM64 |

The static score is not a physical performance claim. Still required:

- populated-kingdom frame time, memory, draw-call, thermal, and battery
  profiling on representative Android and physical iOS `15` devices;
- current-runtime Simulator visual smoke from a Simulator-SDK export;
- final damaged, destroyed, repairing, selected, disabled, and unavailable art;
- bounded civic activity design and measured effect budgets;
- production models and exact catalog bindings for Eldergrove, Crownlands, and
  Umbral Town Halls;
- progression, economy, save, game-data, and command authority before enabling
  live Town Hall upgrade actions.
