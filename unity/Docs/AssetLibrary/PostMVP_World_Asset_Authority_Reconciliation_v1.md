# Post-MVP World-Asset Authority Reconciliation v1

**Status:** preparation and recommendation only; not production authorization
**Date:** 2026-08-28
**Task:** `t_747b84d2`
**Runtime-content impact:** none

## 1. Decision boundary

This report reconciles the repository sources that could be mistaken for a post-MVP
world-asset inventory. It does not create an inventory, change a catalog, admit an
asset, replace an MVP asset, alter a scene, or authorize modeling.

Two different approval questions currently exist and must not be conflated:

1. The broad post-MVP-through-live-service roadmap and preservation of the approved
   current MVP were accepted on board task `t_0648ce23`.
2. The repository's Gate 0 package is still fail-closed. The current final Gate 0 card
   `t_a4c586ff` is blocked pending project-owner review of RC-002 in PR `#634`, and
   `unity/Docs/Roadmap/StopShip/SS-20260828-001/record.md` remains open on the current
   branch.

The MVP review and integrated-roadmap prerequisite gates named by this task are thus
complete in their parent-card handoffs. Asset generation and production activation
were prohibited before both completed, and this audit did not perform either action.
Their completion does not convert this report into per-asset production approval or
erase the separate live Gate 0 conflict above.

Therefore this document is safe preparation under the accepted broad roadmap, but no
post-MVP asset implementation may begin from it. The future inventory path, schema,
taxonomy, and naming recommendations below remain proposals until the owner approves
this report and the controlling Gate 0 record reaches an unambiguous approved state.

The current MVP is preserved as-is. In particular,
`Assets/AL/Resources/FirstSessionAuthoredAssetCatalog.asset`, its admitted assets, and
the current first-session journey are not superseded by this report.

## 2. Discovery and coverage

### 2.1 Method

The audit used repository-wide filename and content searches for `manifest`,
`art_ref`, `asset_ref`, `model_asset_ref`, `catalog`, `schema`, `world`, `terrain`,
`building`, `biome`, `LOD`, `impostor`, `pivot`, `grid`, `budget`, `approval`, and
`roadmap`. It then line-read the controlling sources and structurally screened the
asset-specific handoff families. Counts below exclude Unity `.meta` companions unless
noted.

### 2.2 Measured corpus

| Corpus | Measured coverage | Audit treatment |
| --- | --- | --- |
| `Assets/AL/StreamingAssets/GameData/` | 26 JSON payloads plus 26 Unity `.meta` files discovered | All payload names and asset/world relevance screened; every payload containing a direct `asset_ref`, `model_asset_ref`, `portrait_asset_ref`, or `scenePath` line-read |
| Direct-reference GameData payloads | 5 files: `al_building_catalog.json`, `al_world_streaming_catalog.json`, `buildings.json`, `champions.json`, `realms.json` | Full-file line-read |
| `SharedContracts/Schemas/` | 19 JSON Schemas discovered | All schema names screened; building, champion, realm, and first-session-terrain schemas line-read; no world-streaming or world-asset-inventory schema exists |
| World topology | `al_world_streaming_catalog.json`, 1,318 lines; 3 dimensions, 11 worlds, 78 chunks, 4 unresolved realm-slot world bindings | Full-file line-read and pattern-counted |
| Building art bindings | `al_building_catalog.json`, 172 lines; 15 building families and 8 model bindings; schema 141 lines | Full-file line-read and pattern-counted |
| Blender source validation | manifest 902 lines, schema 364 lines, 6 retained `.blend` source records | Full-file line-read; binary `.blend` files treated as referenced artifacts, not textual authority |
| Packaged building manifest | `KingdomBuildingModelCatalog.cs`, 208 lines; serialized asset 79 lines and 8 bindings | Full-file line-read |
| Current first-session admission | `FirstSessionAuthoredAssetCatalog.cs`, builder, serialized asset, terrain catalog/schema, and `FirstSessionAuthoredVisualReplacement.md` (114 lines) | Full-file line-read |
| Architecture production documents | 29 authored Markdown files discovered under `Docs/Architecture/`, plus preview evidence and Unity metadata | All names/status families screened; the five cross-cutting contracts were line-read; per-realm blockout, animation, and final-binding packets are retained as asset-specific evidence |
| Cross-cutting architecture contracts | `FourRealm_Modular_Construction_Envelope.md` (147), `FourRealm_TownHall_Production_Contract.md` (363), `Kingdom_Building_Level_And_Placement_Design.md` (281), `Architecture_Mobile_Compatibility_Handoff.md` (210), `Reusable_Architecture_Construction_State_System.md` (139) | Full-file line-read |
| Visual/modeling policy | root `DESIGN.md` (935 lines), `AnotherLife_Blender_Asset_Production_Contract.md`, `PostMVP_Graphics_And_UI_Quality_Standard.md` | Controlling sections and full linked contracts line-read |
| Performance evidence policy | all 6 entries under `Docs/Benchmarks/` discovered; benchmark spec, source manifest, and sustained Android procedure line-read | Full-file line-read for binding floor and evidence protocol; remaining entries screened as templates/reference artifacts |
| Game-data authority | `Game_Data_Source_Inventory.md`, `Game_Data_Catalog_Authority_Spec.md`, `GameDataCatalog/Unschematized_GameData_Catalog_Inventory.md`, and `GameDataCatalog/Game_Data_Production_Authority_Ledger.md` | Full-file line-read |
| Asset-library evidence | all 5 entries under `Docs/AssetLibrary/` discovered before this report | Full-file line-read for release catalog, coverage audit, schema delta, and migration note; Unity `.meta` excluded |
| Roadmap/Gate 0 | all 5 authored files under `Docs/Roadmap/`, plus `Project_Progression_Roadmap.md` and the live board state of the approval/root cards | Full-file line-read; repository and live-board approval conflict recorded, not normalized |

This is authority coverage, not a claim that every generated mesh, texture, prefab, or
binary source file was visually inspected. Those artifacts are leaves referenced by the
sources above and belong to later inventory ingestion and validation.

## 3. Source-role register

### 3.1 Authoritative within a narrow concern

| Source | Authority it owns | Authority it does not own |
| --- | --- | --- |
| `DESIGN.md` | Project visual direction, production naming, provenance expectations, provisional asset ceilings, LOD/texture/import/accessibility rules | Per-asset admission, exact world placement, or measured device approval |
| `Game_Data_Catalog_Authority_Spec.md` and `GameDataCatalog` runtime models/store/manifest | GameData identity, versioning, envelope, validation, failure, and provenance mechanics | Art quality, DCC source validation, or world-asset readiness |
| `al_realm_catalog.json` + `al-realm.schema.json` | Four canonical lowercase realm IDs, presentation identity, palettes, and realm `assetPrefix` values | Biome taxonomy, asset readiness, world placement, or budgets |
| `al_world_streaming_catalog.json` | Dimension/world/chunk topology, scene paths, neighbors, provisional grids, replacement sockets, and traversal profiles | Its own field declares `topology_only_provisional_coordinates`; it does not bind production art or approve coordinates as final |
| `al_building_catalog.json` + `al-building.schema.json` | Structured art binding for the currently populated Town Hall and Workshop prefab variants, including path/GUID/SHA-256 | Gameplay building rules, source provenance, per-LOD budgets, approval history, or general world assets |
| `KingdomBuildingModelCatalog.asset` + class | Exact packaged runtime binding for eight current realm/building prefabs, motion profiles, board scale, and supported level range | Future inventory, provenance, hashes, device evidence, or non-building environment art |
| `FirstSessionAuthoredAssetCatalog.asset` + class | Runtime admission for the current first-session authored presentation | Final post-MVP quality lock, general world inventory, or production history |
| `al_first_session_terrain_catalog.json` + schema | Current first-session terrain-layout data and constraints | Broad post-MVP environment taxonomy or visual approval |
| `al_blender_source_validation.v1.json` + schema | Validation contract for six retained Blender sources: hashes, required objects/collections, LOD triangle ranges, transforms, exports, promotion checks, and open review | Runtime binding, final creative approval, platform-scene performance, or complete asset coverage |
| `PostMVP_Graphics_Benchmark_Spec_2026-08-25.md` | Binding performance target and evidence method: physical 2022–2023 mid-range Android class, stable 30 FPS, and candidate p95 frame time at or below 33.33 ms | Proof that any current asset or scene passes; numeric install-size or total memory ceilings |

### 3.2 Reference-only

The following sources must be linked as evidence but must not be copied into a new
inventory as competing truth:

- `Docs/Architecture/*_Final_Model_And_Runtime_Binding.md`: per-asset production and
  binding evidence.
- `Docs/Architecture/*_Level_Blockout_Handoff.md`: graybox decisions and open review.
- `Docs/Architecture/*_Architecture_Animation_Contract.md`: realm motion grammar.
- `Docs/Architecture/FourRealm_TownHall_Production_Contract.md`: approved Town Hall
  family contract and provisional mobile ceilings.
- concept sheets, contact sheets, preview videos, and `Art/Designs` documents: visual
  references, not runtime textures or gameplay data.
- `Docs/AssetLibrary/collaborator_asset_release_catalog.v1.json` and
  `collaborator_asset_coverage_audit.v1.json`: release/coverage evidence, not runtime
  loading or global inventory authority.
- benchmark source manifests and evidence templates: provenance for benchmark policy,
  not performance results.
- `Project_Progression_Roadmap.md`: sequencing context. The Gate 0 register/DAG and live
  board state govern actual release of work.

### 3.3 Prototype-only or candidate-only

- Realm construction-animation grayboxes and isolated prototype scenes prove motion and
  lifecycle; they are not final building shapes.
- First-session authored visuals are accepted as a non-greybox MVP candidate, not the
  final premium visual lock.
- Blender manifest states are mixed and must remain exact:
  `mvp-runtime-candidate`, `production-candidate`, `lod0-production-pilot`, and
  `review-candidate`. None may be silently rewritten to `production-approved`.
- The modular construction envelope is explicitly provisional pending graybox and owner
  review. Its grid, footprints, and scene ceilings are recommended starting values.

### 3.4 Duplicate, overlapping, or legacy concerns

| Overlap | Reconciliation |
| --- | --- |
| `buildings.json` vs `al_building_catalog.json` vs `KingdomBuildingModelCatalog.asset` | `buildings.json` owns gameplay building definitions and legacy icon strings; `al_building_catalog.json` is the structured art-binding sidecar; the ScriptableObject is the current runtime load manifest. The same eight production prefab GUIDs appear in the latter two. Keep the concerns separate and cross-reference them; do not declare all three a world inventory. |
| `champions.json` vs `al_champion_catalog.json` | They use different champion identities and scopes; one contains concept-sheet path strings while the other currently contains no asset refs. This unresolved identity mismatch is out of the environment-inventory scope and must not be normalized here. |
| `realms.json` vs `al_realm_catalog.json` | The envelope catalog carries gameplay references and heraldry sprite strings; the `al_` catalog carries richer narrative/presentation identity and schema. Realm IDs agree, but neither owns biome IDs or world-asset readiness. |
| Building `modelId` notation | The packaged ScriptableObject uses dot-separated IDs such as `building.crownlands.workshop.production.v1`; the JSON art-binding catalog uses snake-case IDs such as `building_crownlands_workshop_production_v1`. They point at the same GUIDs but no alias registry declares equivalence. |
| Blender source IDs vs runtime IDs | Source-validation IDs permit hyphen/underscore forms and `working-v001`; runtime catalogs use other conventions. Source-to-runtime linkage is manual and incomplete. |
| Asset status words | `production`, `production-candidate`, `mvp-runtime-candidate`, `review-candidate`, and `lod0-production-pilot` are used in different sources without one state machine. Runtime presence is not equivalent to final creative or measured performance approval. |
| Current Unity baseline | `DESIGN.md` records Unity `6000.3.22f1`; older architecture handoffs record `2022.3.62f3`. Historical evidence remains valid for its recorded build, but new evidence must record the actual current engine/build identity. |
| Gate 0 state | The broad roadmap is owner-approved, while the current Gate 0 final card and open stop-ship record still prohibit implementation. Preparation can proceed; asset implementation cannot. |

No source is deleted or rewritten by this reconciliation. “Duplicate” means overlapping
concern, not permission to remove a currently consumed runtime source.

## 4. Missing production metadata

No current source contains all information needed to answer “what world asset is this,
where may it be used, what source produced it, which runtime derivative is admitted,
what does each platform render, and which gates have passed?” The missing cross-source
record includes:

1. A canonical world-asset ID and explicit aliases for existing dot, snake, and source
   IDs.
2. Asset class, kit membership, realm scope, dimension/world/chunk scope, biome tags,
   and replacement-socket bindings.
3. A canonical biome taxonomy. No reviewed biome-ID authority was found.
4. Source packet ID/version, DCC source path/hash, author/tool/model, prompt or source
   references, license/rights, cleanup record, similarity review, and human decision.
5. Runtime path/GUID/SHA-256 plus the exact source-to-runtime derivation relationship.
6. Platform tier, LOD/impostor policy, protected silhouette cues, triangle/material/
   renderer ceilings and measured values, shadow behavior, and streaming/residency.
7. Texture set, source and runtime resolutions, maps/channels, mip policy, compression
   override, read/write state, atlas/trim ownership, and measured memory/install impact.
8. Meter dimensions, axis conversion, pivot profile, snap-grid profile, sockets,
   collision/navigation ownership, and modular compatibility.
9. Independent technical, creative, performance, accessibility/readability, provenance,
   and release-gate states with evidence references.
10. Deprecation/replacement links and a “preserve current MVP” constraint where an
    asset is in the approved first-session path.

## 5. Recommended future inventory

### 5.1 Format and location

After owner and Gate 0 approval, create exactly one catalog payload and one schema:

- Payload: `unity/Assets/AL/StreamingAssets/GameData/al_world_asset_inventory.json`
- Schema: `unity/SharedContracts/Schemas/al-world-asset-inventory.schema.json`

The payload should use the existing production GameData envelope:

```text
gameId: another-life
catalogId: al_world_asset_inventory
family: world_assets
schemaVersion: 1
contentVersion: <semantic version>
sourceRevision: <immutable revision>
records: [...]
aliases: [...]
```

This is a recommendation only; these files are deliberately not created by this task.
Adding a payload under `StreamingAssets/GameData` is a runtime-content change and
requires the later schema, loader, validator, owner, and platform gates.

The inventory should own **production identity and readiness**, not duplicate other
sources:

- refer to canonical realm IDs from `al_realm_catalog`;
- refer to dimension/world/chunk/socket IDs from `al_world_streaming_catalog`;
- refer to building IDs and runtime prefab bindings rather than copying gameplay data;
- link Blender validation source IDs and evidence packets;
- keep current Resources catalogs as runtime admission until a measured loading
  migration is separately approved.

Runtime C# may generically parse, validate, and query this schema. It must not hardcode
individual world-asset IDs, paths, realm variants, budgets, or approval decisions; those
remain catalog data under `StreamingAssets/GameData`.

### 5.2 Realm/biome hierarchy

Use a normalized logical hierarchy:

```text
scope (realm | shared | neutral | event)
└── realm_id?                 # one of the four canonical IDs when scope=realm
    └── biome_ids[]           # canonical references; cannot be populated until owned
        └── kit_id
            └── asset_id
```

Each record should also carry optional `dimension_ids`, `world_ids`, `chunk_ids`, and
`replacement_socket_ids`. Do not infer a biome from a folder, palette, realm, or
blockout archetype. Until the owner selects a biome-ID authority, `biome_ids` must be
empty with an explicit `taxonomy_status: unresolved`, not guessed.

Recommended source/runtime folder organization after approval:

```text
unity/ArtSource/World/<Realm|Shared>/<Biome>/<Kit>/<asset-id>_source_v###.blend
unity/Assets/AL/Art/World/<Realm|Shared>/<Biome>/<Kit>/Runtime/...
```

Existing files stay in place and enter through path references; this recommendation
does not authorize bulk moves.

### 5.3 Modular-kit and asset naming

The root design guide remains controlling:

- source packet: `<category>_<realm-or-neutral>_<asset>_v###`;
- source model: `<asset-id>_source_v###`;
- runtime prefab: `AL_<Category>_<AssetName>`;
- material: `MAT_<RealmOrNeutral>_<MaterialName>`;
- texture: `T_<AssetName>_<Map>_<Size>`;
- LOD mesh: `<AssetName>_LOD0` through `<AssetName>_LOD#`.

Recommended stable catalog additions:

- `kit_id`: `kit_<realm-or-shared>_<biome>_<role>_v###`;
- `asset_id`: `<realm-or-shared>_<biome>_<class>_<descriptor>_<variant>`;
- collision object: `COL_<AssetName>_<Role>_##`;
- navigation exclusion/source: `NAVEX_<AssetName>_<Role>_##` or the explicitly approved
  navigation prefix;
- socket: `SOCKET_<SemanticRole>_##`;
- render mesh object: `M_<Realm>_<Biome>_<AssetName>_LOD#`.

Stable IDs are lowercase snake case. Runtime/DCC object names retain the existing typed
prefix and readable PascalCase segments. Existing dot-separated building model IDs
must be retained as aliases until an owner-approved migration proves every consumer.
The realm `assetPrefix` values (`crn`, `sth`, `eld`, `umb`) remain available metadata,
but this report does not replace the established full-realm runtime filenames with
abbreviations.

### 5.4 Required record shape

Each inventory record should require these groups:

| Group | Required fields |
| --- | --- |
| Identity | `asset_id`, `asset_class`, `kit_id`, `display_label`, `aliases[]` |
| Placement scope | `scope`, `realm_id?`, `biome_ids[]`, `dimension_ids[]`, `world_ids[]`, `chunk_ids[]`, `replacement_socket_ids[]` |
| Source/provenance | `source_packet_id`, `source_version`, `source_ref {path, sha256}`, `authoring_tool`, `generator?`, `source_references[]`, `license_or_rights`, `cleanup_status`, `similarity_review`, `human_decision_ref` |
| Runtime derivative | `asset_ref {path, guid, sha256}`, `derivation_revision`, `import_profile_id`, `runtime_dependencies[]` |
| Geometry | meter `dimensions`, `axis_profile`, `pivot_profile_id`, `snap_grid_profile_id`, `lods[]`, `impostor_policy`, `materials`, `renderers`, `collision_profile_id`, `navigation_profile_id`, `socket_profile_id` |
| Textures | set IDs, maps/channels, source/runtime resolution, atlas/trim family, mip policy, platform compression, read/write, estimated and measured resident bytes |
| Platform | `platform_tiers[]`, protected cues, quality reductions, shadows, animation/VFX reduction, streaming/residency, measured build-size delta |
| Approval | separate `technical`, `creative`, `provenance`, `performance`, `accessibility`, and `release_gate` states, each with reviewer, decision, UTC date, evidence refs, and open issues |
| Lifecycle | `inventory_state`, `replaces_asset_ids[]`, `replaced_by_asset_id?`, `preserve_mvp_binding`, `deprecation_reason?` |

Use a closed schema (`additionalProperties: false`), canonical ID validation, unique IDs,
path/GUID/hash validation, and fail-closed cross-reference validation. Unknown values
must not be represented by fabricated defaults.

### 5.5 Approval states

Do not use one word such as `production` as a universal approval. Recommended independent
states are:

- technical: `not_tested | candidate | passed | failed | blocked`;
- creative: `not_reviewed | revise | approved | rejected`;
- provenance: `unknown | incomplete | cleared | rejected`;
- performance: `not_measured | provisional_pass | passed | failed | blocked`;
- accessibility: `not_tested | passed | failed | blocked`;
- release gate: `held | eligible | admitted | retired`.

`inventory_state` may describe workflow (`prototype_only`, `mvp_runtime_candidate`,
`production_candidate`, `production_approved`, `deprecated`) but it must be derived from
and consistent with the independent gates. Runtime presence alone cannot set
`production_approved`.

## 6. Platform, LOD, texture, pivot, and grid policy

### 6.1 Binding floor

The first binding tier is a physical 2022–2023 mid-range Android class, provisionally
anchored by a lowest-RAM Galaxy A54-class device. It must sustain stable 30 FPS from the
start and after heat soak. Candidate evidence is p95 frame time at or below 33.33 ms,
p99 reported and investigated, no unexplained gameplay stall at or above 100 ms, and at
least 20 measured minutes after warm-up. Emulator, editor, static score, or chipset name
alone cannot pass the tier.

Every asset record must declare how it participates in:

- `mobile_low`: binding 30 FPS floor and lowest feasible install/resident size;
- `mobile_high`: scalable quality and optional 60 FPS;
- `pc_high`: scalable 60 FPS presentation.

No numeric whole-build install-size or memory ceiling is currently authoritative. The
inventory must record measured bytes and deltas, and later governance may set thresholds;
this report does not invent them. Reuse, atlases, compression, pruning unused data, and
removing secondary detail take precedence over expanding install size.

### 6.2 LOD and impostors

Use the `DESIGN.md` category ceilings as starting ceilings, never targets. A production
packet must record LOD count, screen-relative intent, protected identity cues,
material-slot changes, shadows, collider policy, animation/VFX reduction, and measured
scene/camera/device evidence.

Initial reduction guidance is LOD1 near 50–60% of LOD0 triangles, LOD2 near 20–30%, and
a far representation near 5–10%, tuned by silhouette and measured cost. Normal mobile
play should use a reduced LOD; LOD0 is for distances that reveal its value. Cross-fades
must include double-render overlap in profiling.

Every repeated or long-range world kit must explicitly choose one:

- `not_applicable` with reason;
- authored far mesh;
- opaque billboard/impostor with generation and lighting constraints;
- chunk/HLOD proxy owned by the streaming system.

An `impostor` token in a promotion checklist is not proof that the impostor exists.
Transparent distant impostors are disfavored unless measured and visually necessary.

### 6.3 Texture guidance

- Common buildings/large props: usually shared 1K trims or atlases.
- Small props: 512–1K and atlas repeated assets.
- Hero buildings: 1K–2K only when close framing proves value.
- Prefer opaque materials; alpha clip selectively; minimize blended transparency.
- Use mipmaps, platform compression overrides, packed compatible masks, and disabled
  read/write unless runtime CPU access is proven.
- A unique 4K runtime texture for a small/medium asset is prohibited. A retained 4K
  source requires an explicit runtime derivative.
- Record source resolution separately from every platform runtime derivative and record
  actual resident/build-size impact.

### 6.4 Pivots and snap grid

The existing modular-envelope values remain provisional but are the recommended profile
until owner review:

- `1 Unity unit = 1 meter`;
- source authoring sub-grid `0.5 m`;
- placement cell `2 m × 2 m`;
- structural bay and modular bridge span increment `4 m`;
- vertical tier `1 m`;
- building root pivot at footprint center on finished ground;
- wall/road pivot at base center with sockets on exact bay boundaries;
- attachment pivot at connection-face center, forward away from receiver;
- separate semantic entrance/navigation, camera-focus, interaction, VFX, and modular
  end sockets.

Each record must reference named `pivot_profile_id` and `snap_grid_profile_id` values,
not repeat unconstrained floats. Exceptions require an explicit reason, owner/technical
approval, and import test.

## 7. Promotion and implementation gate

No asset implementation may start merely because this report exists. The minimum gate
for creating/populating the future inventory and beginning an asset packet is:

1. current Gate 0 stop-ship resolved and repository/board approval states reconciled;
2. project owner approves or revises this inventory location, schema shape, hierarchy,
   biome authority, naming rules, and platform-tier names;
3. schema and validator land before records;
4. current MVP bindings are fingerprinted and protected by regression tests;
5. each asset packet records provenance and owner direction before production modeling;
6. each candidate passes technical/import/LOD/texture/pivot/grid checks;
7. physical Android-floor evidence passes the stable-30-FPS and sustained procedure;
8. accessibility/readability evidence passes at compact-screen and reduced-quality
   settings;
9. the project owner issues the final creative decision;
10. only then may a release gate mark the runtime derivative admitted.

Any missing source, contradictory identity, unowned biome, unknown license, absent hash,
unmeasured platform claim, or unresolved owner decision is `blocked`, not an invitation
to infer a value.

## 8. Final disposition

- **Preserve:** current MVP first-session catalog and journey; current packaged building
  manifest; all existing GameData gameplay authority.
- **Continue as narrow authorities:** world topology, realm identity, building art
  binding, Blender source validation, benchmark policy, and per-asset handoffs.
- **Reference only:** concept sheets, previews, source manifests, production handoff
  prose, and benchmark source evidence.
- **Do not promote:** grayboxes, prototype scenes, candidate statuses, or unmeasured
  static mobile-readiness scores.
- **Do not delete:** overlapping runtime/catalog sources until their consumers and
  migrations are explicitly proven.
- **Future authority, pending approval:** one schema-validated
  `al_world_asset_inventory.json` owning world-asset production identity/readiness and
  linking—not copying—the narrow authorities above.
- **Current implementation verdict:** `HELD`; report complete, asset production not
  authorized.

## Appendix A. Exact file account

### A.1 Direct catalog and runtime-manifest sources

- `unity/Assets/AL/StreamingAssets/GameData/al_world_streaming_catalog.json`
- `unity/Assets/AL/StreamingAssets/GameData/al_building_catalog.json`
- `unity/Assets/AL/StreamingAssets/GameData/al_champion_catalog.json`
- `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json`
- `unity/Assets/AL/StreamingAssets/GameData/al_first_session_terrain_catalog.json`
- `unity/Assets/AL/StreamingAssets/GameData/buildings.json`
- `unity/Assets/AL/StreamingAssets/GameData/champions.json`
- `unity/Assets/AL/StreamingAssets/GameData/realms.json`
- `unity/Assets/AL/Scripts/Data/Catalogs/GameDataCatalogModels.cs`
- `unity/Assets/AL/Scripts/Kingdom/Visuals/Architecture/KingdomBuildingModelCatalog.cs`
- `unity/Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`
- `unity/Assets/AL/Scripts/World/FirstSessionAuthoredAssetCatalog.cs`
- `unity/Assets/AL/Scripts/Editor/FirstSessionAuthoredAssetCatalogBuilder.cs`
- `unity/Assets/AL/Resources/FirstSessionAuthoredAssetCatalog.asset`

The remaining GameData payloads were name/content-screened and excluded from world-asset
ownership because they contain gameplay, UI, narrative, relationship, notification,
quest, character-customization, combat, or weather content rather than production
world-asset records:

- `skills.json`, `skill_weather.v1.json`, `realm_specialized.v1.json`,
  `character_customization.v1.json`, `champion_runtime.json`;
- `al_world_event_content_catalog.json`, `al_world_atlas_narrative_catalog.json`,
  `al_warmaster_content_catalog.json`, `al_skill_weather_catalog.json`,
  `al_relationship_authority_content_catalog.json`;
- `al_realm_gem_wishgate_content_catalog.json`,
  `al_quest_preview_content_catalog.json`, `al_notification_production_catalog.json`,
  `al_notification_content_catalog.json`, `al_main_quest_map_marker_catalog.json`;
- `al_kingdom_teaching_catalog.json`,
  `al_character_customization_content_catalog.json`,
  `al_character_customization_catalog.json`.

Their Unity `.meta` files were accounted as import metadata, not content authorities.

### A.2 Schema account

Asset-adjacent schemas line-read:

- `unity/SharedContracts/Schemas/al-building.schema.json`
- `unity/SharedContracts/Schemas/al-champion.schema.json`
- `unity/SharedContracts/Schemas/al-realm.schema.json`
- `unity/SharedContracts/Schemas/al-first-session-terrain.schema.json`

The other 15 schemas were name/scope-screened and excluded from world-asset ownership:

- `al-world-event-content.schema.json`, `al-world-atlas-narrative.schema.json`,
  `al-warmaster-content.schema.json`, `al-skill-weather.schema.json`,
  `al-six-family.schema.json`;
- `al-relationship-authority-content.schema.json`,
  `al-realm-gem-wishgate-content.schema.json`,
  `al-quest-preview-content.schema.json`, `al-notification-production.schema.json`,
  `al-main-quest-map-marker.schema.json`;
- `al-notification-content.schema.json`, `al-kingdom-teaching.schema.json`,
  `al-character-customization.schema.json`,
  `al-character-customization-content.schema.json`,
  `al-canonical-contracts.schema.json`.

No `al-world-streaming` or `al-world-asset-inventory` schema was found.

### A.3 Source, standards, and authority documents

- `DESIGN.md`
- `unity/ArtSource/al_blender_source_validation.v1.json`
- `unity/ArtSource/al_blender_source_validation.schema.json`
- `unity/ArtSource/FirstUserOnboarding/README.md`
- `unity/Docs/ArtPipeline/AnotherLife_Blender_Asset_Production_Contract.md`
- `unity/Docs/FirstSessionAuthoredVisualReplacement.md`
- `unity/Docs/PostMVP_Graphics_And_UI_Quality_Standard.md`
- `unity/Docs/Game_Data_Source_Inventory.md`
- `unity/Docs/Game_Data_Catalog_Authority_Spec.md`
- `unity/Docs/GameDataCatalog/Unschematized_GameData_Catalog_Inventory.md`
- `unity/Docs/GameDataCatalog/Game_Data_Production_Authority_Ledger.md`
- `unity/Docs/AssetLibrary/building_champion_schema_delta_proposal.md`
- `unity/Docs/AssetLibrary/building_champion_migration_note.md`
- `unity/Docs/AssetLibrary/collaborator_asset_release_catalog.v1.json`
- `unity/Docs/AssetLibrary/collaborator_asset_coverage_audit.v1.json`

### A.4 Architecture family

Cross-cutting architecture sources:

- `Architecture_Mobile_Compatibility_Handoff.md`
- `FourRealm_Modular_Construction_Envelope.md`
- `FourRealm_TownHall_Production_Contract.md`
- `Kingdom_Building_Level_And_Placement_Design.md`
- `Reusable_Architecture_Construction_State_System.md`
- `Live_Kingdom_Construction_UX_Design.md`
- `World_Dimension_Instance_Streaming_Architecture.md`
- `Authoritative_Multiplayer_Backend_And_Security_Architecture.md`

Realm asset-specific evidence under `unity/Docs/Architecture/`:

- `Crownlands_Animation_Prototype_Handoff.md`,
  `Crownlands_Architecture_Animation_Contract.md`,
  `Crownlands_TownHall_Level_Blockout_Handoff.md`,
  `Crownlands_TownHall_Final_Model_And_Runtime_Binding.md`,
  `Crownlands_Workshop_Final_Model_And_Runtime_Binding.md`;
- `Stonehold_Animation_Prototype_Handoff.md`,
  `Stonehold_Architecture_Animation_Contract.md`,
  `Stonehold_TownHall_Level_Blockout_Handoff.md`,
  `Stonehold_TownHall_Final_Model_And_Runtime_Binding.md`,
  `Stonehold_Workshop_Final_Model_And_Runtime_Binding.md`;
- `Eldergrove_Animation_Prototype_Handoff.md`,
  `Eldergrove_Architecture_Animation_Contract.md`,
  `Eldergrove_TownHall_Level_Blockout_Handoff.md`,
  `Eldergrove_TownHall_Final_Model_And_Runtime_Binding.md`,
  `Eldergrove_Workshop_Level_Blockout_Handoff.md`,
  `Eldergrove_Workshop_Final_Model_And_Runtime_Binding.md`;
- `Umbral_Animation_Prototype_Handoff.md`,
  `Umbral_Architecture_Animation_Contract.md`,
  `Umbral_TownHall_Level_Blockout_Handoff.md`,
  `Umbral_TownHall_Final_Model_And_Runtime_Binding.md`,
  `Umbral_Workshop_Final_Model_And_Runtime_Binding.md`.

Preview PNG/MP4 files and Unity `.meta` companions were accounted as evidence/import
metadata and do not own catalog values.

### A.5 Performance, roadmap, and gates

- `unity/Docs/Benchmarks/PostMVP_Graphics_Benchmark_Spec_2026-08-25.md`
- `unity/Docs/Benchmarks/PostMVP_Graphics_Benchmark_Source_Manifest_2026-08-25.json`
- `unity/Docs/Benchmarks/PostMVP_Sustained_Physical_Android_Benchmark_Procedure.md`
- `unity/Docs/Benchmarks/Templates/PostMVP_Golden_Scene_Scorecard.md`
- `unity/Docs/Benchmarks/Cross_Genre_Benchmark_Source_Manifest_2026-08-12.json`
- `unity/Docs/Benchmarks/Combat_Balance_Methods_2026-08-12.md` (screened and excluded
  from world-asset budget authority)
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md`
- `unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md`
- `unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v1.md`
- `unity/Docs/Roadmap/Gate0_Integrated_Delivery_DAG_v1.md`
- `unity/Docs/Roadmap/StopShip/SS-20260828-001/record.md`

Live board records checked for decision state: `t_a7840060` (MVP approval),
`t_0648ce23` (integrated roadmap approval), and `t_a4c586ff` (current separate Gate 0
finalization blocker).
