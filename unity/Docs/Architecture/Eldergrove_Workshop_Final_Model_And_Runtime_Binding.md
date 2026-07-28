# Eldergrove Workshop Final Model and Runtime Binding

**Status:** Owner-approved production contract

**Date:** 2026-07-27

**Building identity:** `Workshop`

**Realm identity:** `Eldergrove`

**Stable slot:** `kingdom.slot.workshop`

**Model identity:** `building.eldergrove.workshop.production.v1`

This contract closes the Eldergrove Workshop dimensions, topology, material,
collider, LOD, loading, capstone, and live-binding gates. It implements the
owner's direction to move from approved construction grammar and level
blockouts into a production-integrated kingdom model.

The model remains presentation-only. Gameplay state, construction completion,
resource spending, save persistence, and quest consequences remain owned by
their existing services and transactions.

## Coordinate and dimension contract

Unity authoring uses one unit per meter. The root pivot is ground-center, with
the protected entrance facing local negative Z.

| Boundary | Final value |
| --- | ---: |
| Stable slot envelope | `10.0 m W × 8.0 m D` |
| Maximum Level 10 art bounds | `9.2 m W × 7.0 m D × 6.8 m H` |
| Level 1 target bounds | `7.4 m W × 6.4 m D × 4.2 m H` |
| Level 6 target bounds | `9.0 m W × 6.8 m D × 5.1 m H` |
| Level 10 target bounds | `9.2 m W × 7.0 m D × 6.8 m H` |
| Protected entrance clearance | `2.4 m W × 2.8 m H × 1.5 m D` |
| Live strategic-board scale | `0.12` |

Every level keeps the same pivot, entrance direction, slot identity, and
maximum envelope. Level modules cannot move the root or silently expand into a
neighboring slot.

## Topology contract

The runtime model is one cumulative modular prefab rather than eleven complete
prefabs. It contains `L01` through `L10` module deltas. Confirmed gameplay level
activates every delta at or below that level.

| Tier | Triangle ceiling | Active renderer ceiling | Purpose |
| --- | ---: | ---: | --- |
| LOD0 | `8,000` | `10` | Close inspection and selected cutaway |
| LOD1 | `65%` of LOD0 | `10` | Normal kingdom play |
| LOD2 | `35%` of LOD0 | `10` | Strategic overview |
| LOD3 | `15%` of LOD0 | `4` | Far static proxy |

Generated meshes combine each level delta into one renderer with no more than
two submeshes. Decorative micro-geometry is removed before protected entrance,
roof, annex, root-lock, or capstone shapes are weakened.

The final mesh hierarchy is deterministic:

```text
Eldergrove_Workshop_Production
├── LOD0
│   └── L01 ... L10 cumulative delta meshes
├── LOD1
│   └── L01 ... L10 reduced delta meshes
├── LOD2
│   └── L01 ... L10 strategic delta meshes
├── LOD3
│   └── retained silhouette delta meshes
└── two root colliders
```

## Materials and atlas

The model uses exactly two shared, instancing-enabled opaque materials:

1. `MAT_Eldergrove_Workshop_Atlas` for aged stone, living root, timber, bronze,
   leaf roof, moss, and grounded utility surfaces.
2. `MAT_Eldergrove_Workshop_Accent` for the localized cultivation and
   seed-lantern core.

`T_Eldergrove_Workshop_Atlas_1024` is one `1024 × 1024` RGB atlas with mipmaps,
no readable CPU copy, no alpha, and high-quality platform compression. The
atlas is divided into stable semantic regions rather than level-specific
textures. Levels reuse the same regions so progression does not multiply
materials or texture memory.

The accent material may emit locally. Structural roots, roofs, masonry,
timber, and bronze cannot emit. No transparent foliage, water, or glow layer is
required for level recognition.

## Collider contract

The production prefab has exactly two box colliders on its root:

- **Selection bounds:** trigger, `9.4 × 6.8 × 7.2 m`, centered at
  `(0, 3.4, 0)`. It supports one stable mobile tap target.
- **Navigation obstruction:** solid, `8.6 × 1.4 × 6.2 m`, centered at
  `(0, 0.7, 0)`. It represents the occupied ground mass without following
  decorative roots.

Construction deltas, leaves, roof trim, props, and the seed lantern do not
receive individual colliders. A future character-scale interior requires a
separate authored navigation/interior contract; it must not inflate the
strategic prefab.

## LOD contract

One `LODGroup` owns four bands:

| Band | Screen-relative transition | Shadow policy |
| --- | ---: | --- |
| LOD0 | `0.60` | Cast and receive |
| LOD1 | `0.30` | Cast and receive |
| LOD2 | `0.12` | Receive only |
| LOD3 | `0.04` | No shadows |

Below `0.04`, the building culls. Cross-fade is disabled so the model does not
require dither or transparency shader variants. Motion vectors, light probes,
and reflection probes remain disabled for the packaged strategic model.

Every band preserves the footprint, entrance notch, main roof direction, Level
6 upper mass when present, and Level 10 capstone when present.

## Final Level 10 capstone

The Level 10 capstone is the approved **six-rib seed lantern**:

- six bronze ribs rise from one restrained cultivation collar;
- one small living core provides the only capstone emission;
- paired crown-roof planes continue the repaired eave-to-ridge direction;
- two final structural root locks visibly connect the crown to the settled
  building;
- the capstone remains static after construction and never spins, breathes,
  floats, or pulses structurally.

This is the final Workshop landmark direction. Replacing it requires a new
owner decision.

## Loading strategy

Core kingdom models use a packaged
`KingdomBuildingModelCatalog` ScriptableObject under `Resources`.

- The catalog stores direct prefab references and stable realm/building/model
  identities.
- `CityLayoutEngine` loads and caches the catalog once for its lifetime.
- No per-frame asset lookup, remote fetch, reflection, or save-backed asset
  path is permitted.
- This avoids a new package dependency while the core kingdom remains a
  packaged scene.
- Optional downloadable districts may adopt a separate bundle manifest later;
  they cannot change this stable core-model identity or silently override it.

The catalog distinguishes an undeclared family from a declared-but-invalid
binding. Undeclared families retain the current explicit legacy presentation.
A declared invalid Eldergrove Workshop binding shows a visible unavailable
marker rather than substituting another model.

## Direct live-kingdom binding

The live resolver remains:

```text
stable slot definition
+ immutable BuildingState snapshot
→ KingdomBuildingPresentation
+ packaged model catalog
→ production prefab + confirmed cumulative level
```

Rules:

- Level `0` renders the reserved plot and does not load a built model.
- Level `1` through `10` instantiate the one production prefab and activate
  only confirmed cumulative deltas.
- An active upgrade keeps the confirmed current level visible. The target
  module is not shown as complete before gameplay commits the new level.
- The existing upgrade work indicator may communicate activity, but no visual
  stage or target level is saved.
- Completion, resource spend, timers, quests, and persistence stay in
  `IBuildingService`, economy, and save services.
- Invalid gameplay data or an invalid declared model binding fails visibly and
  never mutates gameplay state.

## Mobile acceptance

- Static architecture readiness remains above `90 / 100`.
- No runtime Animator, particle system, audio source, per-module collider, or
  prefab light.
- One atlas, two materials, four LOD bands, two colliders, and ten or fewer
  active renderers at any level.
- Level changes remain readable without emission, labels, particles, or color.
- Representative populated-kingdom profiling on Android and iOS remains the
  final performance approval gate; it does not reopen the design decisions in
  this contract unless a protected silhouette cannot meet measured limits.

## Delivered production assets

- Deterministic production builder:
  `Assets/AL/Scripts/Editor/Architecture/EldergroveWorkshopProductionModelBuilder.cs`
- Cumulative live prefab:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/Runtime/Eldergrove_Workshop_Production.prefab`
- Runtime atlas and two materials:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/Runtime`
- Packaged direct-reference catalog:
  `Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`
- Isolated Level 1/6/10 review scene:
  `Assets/AL/Scenes/Prototypes/EldergroveWorkshopProductionModel.unity`
- Focused runtime, asset, topology, and binding tests:
  `Assets/AL/Tests/EditMode/Architecture/EldergroveWorkshopProductionModelTests.cs`

The preview scene remains outside Player build settings. The concept sheet is
not a dependency of the production prefab or catalog.

## Verified production metrics

Representative cumulative LOD0 bounds are:

| Level | Actual bounds | Approved ceiling |
| ---: | ---: | ---: |
| 1 | `6.30 W × 5.60 D × 4.10 H m` | `7.40 W × 6.40 D × 4.20 H m` |
| 6 | `7.77 W × 5.60 D × 4.91 H m` | `9.00 W × 6.80 D × 5.10 H m` |
| 10 | `9.12 W × 5.60 D × 6.68 H m` | `9.20 W × 7.00 D × 6.80 H m` |

| Band | Triangles | LOD0 ratio | Renderers |
| --- | ---: | ---: | ---: |
| LOD0 | `4,984` | `100%` | `10` |
| LOD1 | `2,852` | `57.2%` | `10` |
| LOD2 | `1,200` | `24.1%` | `10` |
| LOD3 | `672` | `13.5%` | `3` |

The production Level 1/6/10 render passed structured comparison against the
approved source at `92 / 100`. The focused EditMode gate passed `18 / 18`,
including direct Level 6 binding, confirmed-level hold during an upgrade,
Level 0 reserved-plot behavior, and visible failure for a declared invalid
model binding or mismatched stable model identity.
