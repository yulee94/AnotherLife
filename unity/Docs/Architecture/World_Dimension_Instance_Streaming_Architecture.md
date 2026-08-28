# World Dimension, Instance, and Streaming Architecture

Status: implementation contract for the full-world blockout and later production substitution.

## Purpose

AnotherLife must support a large 3D adventure world, a private 2.5D kingdom, and later event or arena spaces without loading every spatial asset at once. The architecture separates gameplay ownership, transitions, and streaming so the world can grow without becoming one monolithic Unity scene.

The 2D atlas is topology authority only. Coordinates in generated blockout scenes are provisional layout coordinates, not canon geography.

## Runtime hierarchy

```text
Bootstrap / non-spatial services
└── one active Dimension
    └── one active World Instance
        └── bounded set of active Streaming Chunks
```

### Bootstrap

The bootstrap contains only non-spatial services, loading presentation, save/profile authority, catalogs, audio policy, and transition orchestration. Terrain, buildings, cameras owned by a gameplay world, NavMesh data, world lighting, and encounter objects do not persist here.

### Dimension

A dimension is a mutually exclusive gameplay and presentation mode.

Initial dimensions:

- `dimension_adventure_3d`
- `dimension_kingdom_25d`
- `dimension_special_event_3d`

Only one spatial dimension is active. Switching dimensions unloads the prior world's chunks before activating the destination world. Shared Menu remains the only route between adventure 3D and kingdom 2.5D.

### World instance

A world instance is a purpose-specific traversable or viewable space inside a dimension. Examples are an inner-realm continent slot, the shared warzone, a realm dragon cave, the private kingdom, or Accordant Isle. World-instance transitions may use a short masked load at gates, cave entrances, or event portals.

Only chunks owned by the active world instance may be loaded additively. Geometry from two gameplay worlds must never overlap merely to hide a transition.

### Streaming chunk

A chunk is the smallest independently loaded spatial unit. Boundaries follow traversal, visibility, ownership, and authoring seams—not individual props. A chunk owns its static geometry, local lights/probes, collision, navigation surface/data, spawn sockets, and replacement sockets.

Chunks use local coordinates inside their world instance. Generated full-world coordinates are provisional blockout values and can be re-authored without changing stable world/chunk IDs.

## Loading policy

- Use asynchronous loading for all world and chunk scenes.
- Use single-world transitions between dimensions or world instances: unload the prior spatial world, reclaim unreferenced content, then activate the destination.
- Use additive loading only for neighboring chunks in the same active world instance.
- Maintain a small hysteresis ring so crossing a boundary does not unload and reload the same chunk repeatedly.
- Never block the async operation queue by holding several scene loads at 90% with activation disabled.
- Every successful load has a recorded owner/handle and a symmetric unload/release path.
- Cancellation or failure restores the exact prior residency set when compensation succeeds. If compensation cannot restore that set, the coordinator clears the active-world claim and uses the loader visibility seam to keep partial spatial residency hidden behind the safe loading shell.

### Current implementation boundary

`WorldStreamingCoordinator` serializes focus requests and enforces world exclusivity through an injected `IWorldChunkLoader`. Initial no-claim focus and cross-world transitions—including recovery from a null-claim safe shell that still owns foreign-world chunks—enter the hidden loading shell before the first destination load; cross-world recovery also unloads the foreign set first. The destination is revealed only after every required load and stale unload completes. Same-world transitions with a valid active-world claim load required neighbors before releasing stale chunks. Any load, unload, cancellation, or visibility-activation failure hides spatial content before exact-set compensation begins; a failed compensation remains non-visible with no active-world claim until a later complete focus request succeeds. The active-world claim is assigned only after visibility restoration succeeds. Individual `LoadAsync` implementations must return only after a chunk is initialized and must report their actual residency even when an operation throws so compensation can remove partial mutations. `SetSpatialVisibilityAsync(false)` is fail-closed by contract: even a faulted hide request leaves spatial content hidden. `WorldResidencyPlanner` remains a pure catalog-driven policy layer.

`WorldStreamingCatalogLoader` is fail-closed for this foundation: unknown properties, non-canonical dimensions/world placement, altered world ownership metadata, extra or missing chunk partitions, wrong generator archetypes, and altered traversal authorities are rejected before a snapshot can reach the planner or generator. A deterministic semantic signature also locks each canonical chunk's scene path, provisional grid coordinate, neighbor set, and complete replacement-socket set without making JSON formatting or property order authoritative. Intentional topology revisions update the catalog and reviewed signature together. The accepted authority is exactly three dimensions, eleven purpose-specific worlds, seventy-eight chunks, and the three locked adventure traversal profiles.

`SceneManagerWorldChunkLoader` is the package-free transport adapter for the current foundation. It resolves only definitions from an accepted catalog snapshot, loads their exact scene paths additively, deduplicates concurrent ownership, releases the physical scene after the final symmetric unload, reconciles late completion after cancellation, and keeps an unload failure in `LoadedChunkIds` so coordinator compensation sees actual residency. `WorldChunkNavigationData` is the explicit per-chunk owner of serialized baked `NavMeshData`; both runtime readiness and the World Authoring preflight fail closed when that data is absent or cannot register.

Physical traversal readiness is a separate gate from navigation readiness. Each playable chunk must contain one active `WorldChunkPhysicalGroundAuthority` that explicitly binds enabled, non-trigger, static colliders owned by the catalog chunk hierarchy and declares all four seam policies against catalog neighbors. A renderer or NavMesh never proves physical safety. Terrain requires a bound `TerrainCollider`; dedicated mesh ground cannot reuse a render mesh and must be a closed convex collision volume. Non-convex caves or modular structures remain possible only through the explicit reviewed-collision source and per-portal review receipts. Missing, disabled, unbound, renderer-reused, or edge-incomplete collision fails both runtime activation and World Authoring preflight without rewriting the generated scene.

The physical-ground component is derived scene binding and review evidence, not a second gameplay-data authority. It cannot add or redirect catalog topology: every continuous seam requires a reciprocal cardinal catalog neighbor, local full-edge collider coverage, and a cross-scene sampled height/contact continuity-review receipt; optional reviewed portals are also checked against the accepted snapshot. Local bounds alone are explicitly reported as `AL-WORLD-CHUNK-SEAM-CONTINUITY-UNPROVEN`, because a neighbor scene may still contain a height mismatch or crack. The current catalog does not yet carry collision-source and review-receipt declarations; production authoring must add those fields to the catalog/schema and export the scene binding from them so serialized scene metadata cannot silently override reviewed intent.

This adapter does not make the current generated blockout scenes player-loadable by itself. Those scenes remain outside production Build Settings and currently contain no baked navigation data, so `Application.CanStreamedLevelBeLoaded` and readiness validation intentionally reject them. A packaged SceneManager route requires an explicitly reviewed Build Settings inclusion policy and baked per-chunk navigation assets. Addressables remains the preferred later transport for dependency-aware remote/local content and release handles; adding that package and migrating the transport must preserve the loader ownership, cancellation, visibility, and actual-residency contracts.

## Content and memory policy

- Shared meshes, materials, textures, VFX, and audio are referenced through stable asset keys and grouped by actual co-residency to avoid duplicated dependencies and asset churn.
- A chunk scene contains placement and per-chunk data, not private copies of shared art.
- Player-facing MVP routes use representative production assets. Full-world non-MVP regions may use substitution-ready modular blockout.
- Every blockout object declares a stable replacement socket, footprint, pivot, bounds class, traversal contract, and collision role.
- Large structures and terrain use LODs; impostors or simplified distant silhouettes are allowed where art direction approves.
- Occlusion culling is applied where solid walls, buildings, caves, and corridors create meaningful occlusion. Open terrain relies primarily on frustum culling, LOD, distance, and chunk residency because baked occlusion can cost memory and CPU without helping.
- Runtime pools are for frequently reused dynamic objects. Static world geometry is scene-owned and unloaded with its chunk.

## World partition

### Authoring chunk spans

- Adventure 3D uses 1,200 m authoring chunks so the warzone can sustain the locked multi-kilometer traversal routes.
- Private kingdom 2.5D uses 128 m chunks for dense isometric management content.
- Special-event 3D uses 800 m chunks because Accordant Isle is a bounded event instance, not part of the adventure traversal budget.

Chunk span is purpose-specific; "3D" does not imply one global size. The 10/15/20–25 minute traversal targets below apply to `dimension_adventure_3d`, not to isolated event instances.

### Traversal scale authority

The reference champion run speed is 6 m/s. World scale is validated by traversable route length rather than straight-line map distance:

- Main gate to nearest warzone fortress: approximately 600 seconds / 3.6 km.
- Main gate to nearest adjacent-realm bridge crossing: approximately 900 seconds / 5.4 km.
- Main gate to nearest opposing-realm warzone fortress: approximately 1,200–1,500 seconds / 7.2–9.0 km.

Terrain roads, switchbacks, elevation, wall approaches, and bridge decks contribute to route length. Final production terrain must be checked with measured navigation paths at the reference speed. Mounts reduce traversal time later; they do not redefine world distance.

### Adventure 3D

- Four unresolved atlas ring-slot inner worlds (`ring_slot_01` through `ring_slot_04`). Realm-to-slot binding remains unresolved until the user approves the compass assignment.
- One shared outer-warzone world containing four sector chunks, the eight required adjacent-pair bridge corridors, four gate approaches, and a central crossroads chunk.
- Each realm boundary preserves inner safe zone → inner wall/main gate → controlled transition → outer wall → warzone entry. Gate-approach chunks own the controlled-transition, outer-wall, and warzone-entry sockets.
- Four realm-specific dragon-cave instances. Their entrances remain routing hooks until realm-to-ring placement is resolved.
- The regular adventure world never additively loads Accordant Isle.

### Private kingdom 2.5D

- One owner-only private-kingdom world template with a selected-realm visual variant.
- Castle core and twelve placeholder Area chunks; placeholder IDs remain stable until the labeled city map is supplied.
- No public-world geometry, warzone, dual bridges, Accordant Isle, visitors, or enemies.
- The strategic camera may keep several low-cost Area chunks resident when zoomed out, but each remains independently authorable and replaceable.
- The legacy `KingdomVisualizer` territory/outpost overlay and fixed-slot `KingdomBuildingLayout` are not runtime authorities for this world. The eventual adapter must use kingdom-only presentation and bounded unlocked cells.

### Special-event 3D

- Accordant Isle is an event-only world instance in its own dimension.
- Surface, castle, four entrances, descent, Wish Dragon cavern, and four distinct ring-to-center bridge approaches are separate chunks.
- The four sealed regular-play bridge approaches preserve the physical atlas topology as routing/visual hooks. They do not become additive connections to the adventure world and cannot be crossed during regular play.

## Scene authoring rules

Each generated scene contains one `WorldChunkRoot` and no duplicate bootstrap singleton, persistent UI, main camera, or global directional-light owner. The active world shell owns its gameplay camera and global lighting. Chunk scenes may own local probes and bounded local lights.

Scene paths and IDs are catalog data. Runtime code must not contain a hardcoded list of world scene names.

## Validation gates

A catalog or generated world is rejected unless:

1. IDs are unique lowercase snake case.
2. Every chunk belongs to exactly one world and one dimension.
3. Every scene path is unique.
4. Additive neighbors belong to the same world instance.
5. Every mandatory topology connection has a chunk or explicit transition hook.
6. The 3D atlas has four ring slots, eight ring bridges, four ring-to-center sealed bridge hooks, four complete gate-boundary approaches, and the shared warzone.
7. The 2.5D world is owner-only and contains no outer-world zone.
8. Accordant Isle is event-only and outside the adventure dimension.
9. Realm-to-ring binding remains unresolved until explicitly approved.
10. Generated scenes contain stable substitution sockets and no forbidden singleton components.

## Profiling and smoothness gates

Scene partitioning enables smooth loading but does not guarantee it. Each target device tier must be profiled for frame time, peak memory, GC allocation, asset churn, draw calls, texture residency, and transition latency. Chunk radii, LOD thresholds, and content groups are tuned from profiler captures rather than assumed constants.

## Unity references

- Unity 6.3 multi-scene editing: https://docs.unity3d.com/6000.3/Documentation/Manual/MultiSceneEditing.html
- `SceneManager.LoadSceneAsync`: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html
- `LoadSceneMode`: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.LoadSceneMode.html
- Unity 6.3 Addressables package (2.9.1 released): https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.addressables.html
- Addressables scene loading: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/LoadingScenes.html
- Addressables memory management: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/MemoryManagement.html
- Level of detail: https://docs.unity3d.com/Manual/LevelOfDetail.html
- Occlusion culling: https://docs.unity3d.com/6/Documentation/Manual/OcclusionCulling.html
