# Stonehold Workshop Final Model and Runtime Binding

**Status:** Production contract implemented

**Date:** 2026-07-27

**Realm identity:** `Stonehold`

**Building identity:** `Workshop`

**Model identity:** `building.stonehold.workshop.production.v1`

**Production source:**
`Assets/AL/Art/Designs/StoneholdWorkshopLevelProgression.md`

**Approved visual reference:**
`Assets/AL/Art/Architecture/ConceptSheets/architecture_stonehold_modular_workshop_detail_v001.png`

**Approved motion reference:**
`Assets/AL/Art/Architecture/ConceptSheets/architecture_stonehold_animation_reference_v001.png`

This contract closes the Stonehold Workshop dimensions, topology, material,
collider, LOD, loading, Level `10` capstone, and live-kingdom model binding.
Gameplay construction time, economy, workers, final damage/repair states, and
device performance remain separate authorities.

## Final dimensions

`1 Unity unit = 1 meter`.

The root pivot is the footprint center on the finished ground plane. The
entrance faces local `-Z`.

| Item | Final value |
| --- | ---: |
| Stable slot envelope | `10.0 m W × 8.0 m D` |
| Maximum art envelope | `9.2 m W × 6.8 m D × 6.6 m H` |
| Level 1 measured bounds | `6.40 m W × 5.40 m D × 5.20 m H` |
| Level 6 measured bounds | `8.03 m W × 6.64 m D × 6.13 m H` |
| Level 10 measured bounds | `9.18 m W × 6.64 m D × 6.48 m H` |
| Live strategic-board scale | `0.12` |

The entrance, pivot, and slot identity do not change across levels.

## Cumulative topology

The prefab contains ten ordered deltas:

```text
Stonehold_Workshop_Production
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

Level `N` enables deltas `1…N`. LOD0–2 preserve every level threshold; LOD3
uses three cumulative silhouette milestones so extreme distance does not pay
for ten renderers.

| LOD | Screen-relative transition | Triangles | Renderers | Policy |
| --- | ---: | ---: | ---: | --- |
| `0` | `0.60` | `1,872` | `10` | Close inspection; shadows on |
| `1` | `0.30` | `912` | `10` | Normal district; shadows on |
| `2` | `0.12` | `504` | `10` | Strategic view; no shadow casting |
| `3` | `0.04` | `276` | `3` | Far milestone silhouettes; no shadows |

Cross-fading is disabled. The controlled kingdom camera and dense mobile scene
favor avoiding double-render overlap; the strong Stonehold silhouette carries
the direct transitions.

Protected geometry at every useful distance:

- fixed foundation and entrance orientation;
- paired roof plates and ridge;
- off-center chimney;
- level-band annex growth;
- Level `10` anvil crown and landmark pressure locks.

## Materials and atlas

The model uses exactly two opaque, instancing-enabled materials:

1. `MAT_Stonehold_Workshop_Atlas` for basalt, masonry, dark iron, timber,
   leather, ash trim, soot, and restrained copper wear.
2. `MAT_Stonehold_Workshop_Accent` for the localized forge core and Level `10`
   crown ember slit.

`T_Stonehold_Workshop_Atlas_1024` is one `1024 × 1024` RGB atlas with mipmaps,
high-quality compression, no alpha channel, and CPU read/write disabled.

Concept sheets remain source-only and are not prefab dependencies.

## Colliders

The root owns exactly two `BoxCollider` components:

| Role | Trigger | Center | Size |
| --- | --- | --- | --- |
| Selection | Yes | `(0, 3.3, 0)` | `(9.4, 6.6, 7.2)` |
| Navigation | No | `(0, 0.75, 0)` | `(9.0, 1.5, 6.4)` |

Render meshes do not own colliders. The selection collider covers the complete
landmark envelope; the navigation collider protects the grounded mass while
remaining independent from decorative upper geometry.

## Final Level 10 capstone

The final capstone is the **anvil-crown forge chimney**:

- three grounded masonry/iron crown tiers;
- paired cap plates forming an anvil-like top silhouette;
- one narrow, opaque emissive ember slit;
- paired rear landmark pressure locks.

It remains static. No particle system, light, Animator, audio source, levitation,
or full-building emission is included in the production prefab.

## Loading strategy

`KingdomBuildingModelCatalog` remains the packaged loading boundary:

- the catalog lives in `Resources` and is loaded once by
  `CityLayoutEngine`;
- entries hold direct production-prefab references;
- the engine caches the resolved catalog for the board lifetime;
- the Stonehold entry is keyed by stable
  `RealmId.Stonehold + BuildingId.Workshop`;
- missing undeclared families retain the legacy procedural presentation;
- a declared invalid binding fails visibly as
  `ProductionModelUnavailable`.

This second production family does not justify Addressables, AssetBundles, a
new package, remote delivery, or a parallel loading service. Revisit that
decision only when measured build size, memory residency, or content delivery
requirements make the existing packaged catalog insufficient.

## Direct live-kingdom binding

The confirmed gameplay level is the only presentation authority:

```text
KingdomBuildingPresentation.CurrentLevel
→ CityLayoutEngine
→ KingdomBuildingModelCatalog
→ Stonehold_Workshop_Production
→ KingdomBuildingLevelModel.ApplyConfirmedLevel(CurrentLevel)
```

- Level `0`: reserved plot; no production model is instantiated.
- Level `1…10`: enable cumulative confirmed deltas.
- Upgrading: retain the current confirmed level and existing upgrade feedback.
- Completed upgrade: the next layout rebuild applies the newly confirmed level.
- No visual stage, target level, or animation stage is added to saves.
- No economy rule, timer, or construction duration is changed.

## Production assets

- Builder:
  `Assets/AL/Scripts/Editor/Architecture/StoneholdWorkshopProductionModelBuilder.cs`
- Runtime prefab:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Production/Runtime/Stonehold_Workshop_Production.prefab`
- Runtime atlas/materials/meshes:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Production/Runtime`
- Packaged catalog:
  `Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`
- Review scene:
  `Assets/AL/Scenes/Prototypes/StoneholdWorkshopProductionModel.unity`
- Focused tests:
  `Assets/AL/Tests/EditMode/Architecture/StoneholdWorkshopProductionModelTests.cs`

The review scene remains excluded from Player build settings.

## Remaining validation and direction gates

Static mobile readiness remains above the project `90 / 100` floor. Still
required:

- populated-kingdom frame time, memory, draw-call, thermal, and battery
  profiling on representative Android and physical iOS `15` devices;
- final damaged, destroyed, repairing, selected, and unavailable art
  treatments;
- exact operational activity scheduling and effect budgets;
- production models for the remaining building families.

Changing the stable footprint, entrance, Level `10` anvil crown, direct
gameplay-level authority, two-material ceiling, two-collider strategy, or
four-LOD policy is a major direction change.
