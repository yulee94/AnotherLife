# Crownlands Workshop Final Model and Runtime Binding

**Status:** Production contract implemented

**Date:** 2026-07-27

**Realm identity:** `Crownlands`

**Building identity:** `Workshop`

**Model identity:** `building.crownlands.workshop.production.v1`

**Production source:**
`Assets/AL/Art/Designs/CrownlandsWorkshopLevelProgression.md`

**Approved visual reference:**
`Assets/AL/Art/Architecture/ConceptSheets/architecture_crownlands_modular_stormwright_detail_v001.png`

**Approved motion reference:**
`Assets/AL/Art/Architecture/ConceptSheets/architecture_crownlands_animation_reference_v001.png`

This contract closes the Crownlands Workshop dimensions, topology, material,
collider, LOD, loading, Level `10` capstone, and live-kingdom model binding.
Gameplay construction time, economy, workers, final damage/repair states, and
device performance remain separate authorities.

## Final dimensions

`1 Unity unit = 1 meter`.

The root pivot is the footprint center on the finished ground plane. The
entrance faces local `-Z`.

| Item | Final value |
| --- | ---: |
| Stable slot envelope | `10.0 m W × 8.0 m D × 7.8 m H` |
| Maximum art envelope | `9.65 m W × 7.20 m D × 7.55 m H` |
| Level 1 measured bounds | `6.55 m W × 5.00 m D × 5.17 m H` |
| Level 6 measured bounds | `8.03 m W × 6.57 m D × 6.34 m H` |
| Level 10 measured bounds | `9.62 m W × 6.57 m D × 7.50 m H` |
| Live strategic-board scale | `0.12` |

The entrance, pivot, and slot identity do not change across levels.

## Cumulative topology

The prefab contains ten ordered deltas:

```text
Crownlands_Stormwright_Production
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
| `0` | `0.60` | `2,400` | `10` | Close inspection; shadows on |
| `1` | `0.30` | `1,420` | `10` | Normal district; shadows on |
| `2` | `0.12` | `828` | `10` | Strategic view; no shadow casting |
| `3` | `0.04` | `316` | `3` | Far milestone silhouettes; no shadows |

Cross-fading is disabled. The controlled kingdom camera and dense mobile scene
favor avoiding double-render overlap; the protected Crownlands silhouette
carries the direct transitions.

Protected geometry at every useful distance:

- fixed foundation, axial entrance, and paired civic piers;
- dominant segmented silver arch;
- stepped dark-blue roof and raised lantern;
- central grounded calibration device;
- Level `10` Meridian Crown Lantern and paired conductor pylons.

## Materials and atlas

The model uses exactly two opaque, instancing-enabled materials:

1. `MAT_Crownlands_Stormwright_Atlas` for civic stone, silver trim,
   dark-blue roof planes, brass conductors, and contained blue details.
2. `MAT_Crownlands_Stormwright_Indigo` for the localized calibration focus
   and Level `10` crown aperture.

`T_Crownlands_Stormwright_Atlas_1024` is one `1024 × 1024` RGB atlas with
mipmaps, high-quality compression, no alpha channel, and CPU read/write
disabled.

Concept sheets remain source-only and are not prefab dependencies.

## Colliders

The root owns exactly two `BoxCollider` components:

| Role | Trigger | Center | Size |
| --- | --- | --- | --- |
| Selection | Yes | `(0, 3.78, 0)` | `(9.8, 7.56, 7.4)` |
| Navigation | No | `(0, 0.75, 0)` | `(9.4, 1.5, 6.8)` |

Render meshes do not own colliders. The selection collider covers the complete
landmark envelope; the navigation collider protects the grounded civic mass
while remaining independent from upper lantern geometry.

## Final Level 10 capstone

The final capstone is the **Meridian Crown Lantern**:

- one grounded circular lantern carried by the established roof;
- four fixed silver meridian ribs and one restrained face ring;
- one contained indigo calibration aperture;
- paired grounded conductor pylons and silver finials.

It remains static. No particle system, light, Animator, audio source,
levitation, procedural lightning, continuous rotation, or full-building
emission is included in the production prefab.

## Loading strategy

`KingdomBuildingModelCatalog` remains the packaged loading boundary:

- the catalog lives in `Resources` and is loaded once by
  `CityLayoutEngine`;
- entries hold direct production-prefab references;
- the engine caches the resolved catalog for the board lifetime;
- the Crownlands entry is keyed by stable
  `RealmId.Crownlands + BuildingId.Workshop`;
- missing undeclared families retain the legacy procedural presentation;
- a declared invalid binding fails visibly as
  `ProductionModelUnavailable`.

This third production family does not justify Addressables, AssetBundles, a
new package, remote delivery, or a parallel loading service. Revisit that
decision only when measured build size, memory residency, or content delivery
requirements make the existing packaged catalog insufficient.

## Direct live-kingdom binding

The confirmed gameplay level is the only presentation authority:

```text
KingdomBuildingPresentation.CurrentLevel
→ CityLayoutEngine
→ KingdomBuildingModelCatalog
→ Crownlands_Stormwright_Production
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
  `Assets/AL/Scripts/Editor/Architecture/CrownlandsStormwrightProductionModelBuilder.cs`
- Runtime prefab:
  `Assets/AL/Art/Generated/Architecture/Crownlands/Production/Runtime/Crownlands_Stormwright_Production.prefab`
- Runtime atlas/materials/meshes:
  `Assets/AL/Art/Generated/Architecture/Crownlands/Production/Runtime`
- Packaged catalog:
  `Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`
- Review scene:
  `Assets/AL/Scenes/Prototypes/CrownlandsStormwrightProductionModel.unity`
- Focused tests:
  `Assets/AL/Tests/EditMode/Architecture/CrownlandsStormwrightProductionModelTests.cs`

The review scene remains excluded from Player build settings.

## Validation and direction gates

The final production review scored `91 / 100` and passed the protected-identity
gate. Static mobile readiness remains above the project `90 / 100` floor.
Still required:

- populated-kingdom frame time, memory, draw-call, thermal, and battery
  profiling on representative Android and physical iOS `15` devices;
- final damaged, destroyed, repairing, selected, and unavailable art
  treatments;
- exact operational activity scheduling and effect budgets;
- production models for the remaining realm/building families.

Changing the stable footprint, entrance, paired-pier and silver-arch identity,
Level `10` Meridian Crown Lantern, direct gameplay-level authority,
two-material ceiling, two-collider strategy, or four-LOD policy is a major
direction change.
