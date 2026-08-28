# Post-MVP World-Asset Catalog Binding and Production Standard v1

**Status:** schema and production-policy definition; production remains `HELD`
**Task:** `t_394f4111`
**Runtime/content impact:** none; no inventory payload, asset, prefab, Addressable group, scene, loader, or C# content binding is created or changed
**Final creative and release authority:** project owner

## 1. Purpose and authority boundary

This standard defines how the logical families in
`PostMVP_World_Asset_Taxonomy_v1.md` can become schema-validated production
records without becoming production authorization. Its machine-readable contract is:

`unity/SharedContracts/Schemas/al-world-asset-inventory.schema.json`

The future single inventory payload remains:

`unity/Assets/AL/StreamingAssets/GameData/al_world_asset_inventory.json`

That payload is intentionally not created here. The downstream assembly task owns it.
The current MVP, the eight Town Hall/Workshop bindings, the Resources catalogs, and
all existing runtime consumers remain unchanged.

The inventory owns production identity, binding metadata, standards, provenance, and
readiness. It links to, but does not copy authority from:

- `al_realm_catalog.json` for realm IDs;
- `al_world_streaming_catalog.json` for dimension/world/chunk/socket IDs;
- `buildings.json` for gameplay building definitions;
- `al_building_catalog.json` for current structured building art bindings;
- `al_blender_source_validation.v1.json` for retained Blender-source validation;
- asset-specific handoffs and benchmark evidence for their narrow decisions.

Runtime C# may generically parse, validate, index, and query the schema. Runtime C#
must not hardcode individual `waf_`, `wak_`, `wa_`, or `wad_` IDs; prefab paths;
Addressable keys; aliases; realm variants; profile IDs; budget classes; or approval
decisions. No C# change may manufacture a fallback record when catalog data is
missing or invalid.

The broad roadmap and current-MVP prerequisites are complete, but the separate live
Gate 0 stop-ship recorded by the authority reconciliation remains controlling. This
schema does not resolve it. Current examples use `preparation_held`, and no record may
be generated or activated merely because it validates.

## 2. Catalog shape and field obligation

The schema is Draft 2020-12 and closed with `additionalProperties: false` at every
owned object boundary. The envelope is the established production GameData form:

```text
gameId              another-life
catalogId            al_world_asset_inventory
family               world_assets
schemaVersion        1
contentVersion       semantic version
sourceRevision       immutable source revision
idFormat             lowercase_ascii_snake_case
authority             mandatory catalog and final-owner authority
gatePolicy            mandatory generation/activation hold and required gates
profiles              mandatory named production-rule profiles
records               canonical asset records
aliases               exact legacy-to-canonical mappings
```

Schema v1 deliberately fixes both `generationState` and `activationState` to
`held` and requires all six gate IDs. Positive catalog-wide transitions are not
representable in this preparation schema. They require a later owner-approved schema
version after the separate Gate 0 conflict is closed; editing a payload cannot unlock
generation or runtime admission.

### 2.1 Required versus optional

A field is optional only where the schema explicitly permits `null` or an empty
array. Omitting it is invalid.

| Record group | Required shape | Explicitly empty or nullable when unknown/not applicable |
| --- | --- | --- |
| Identity | `assetId`, `familyId`, `kitId`, `assetClass`, `displayLabel` | `kitId: null` when no approved kit exists |
| Owner authority | `finalCreativeOwner`, non-empty `accountableOwners`, `ownerDecisionRef` | nothing; owner authority is never optional |
| Placement | `scope`, all four reference arrays, `taxonomyStatus` | unresolved biomes are exactly `taxonomyStatus: unresolved`, `biomeIds: []` |
| Source | status, packet/version, artifact list, derivation revision | `not_started` requires null packet/version and no artifacts |
| Provenance | state, creators, tools, references, rights, AI disclosure, cleanup, similarity review, human decision, issues | unknown values stay explicit; the provenance group and human decision reference remain mandatory |
| Runtime binding | state, nullable prefab and Addressable records, dependencies, evidence, unbound reason | `unbound` requires both bindings null and a reason; candidate or later requires at least one binding |
| Standards | references to all ten production profiles plus exception references | no profile reference is optional; approved exceptions are additive evidence |
| Geometry/material/texture/LOD | measured or explicit unverified/not-applicable states | dimensions, counts, measurements, and hashes may be null only while their gate remains blocked |
| Collision/nav/occlusion/streaming | explicit participation and reason | `none`, `unverified`, or `unassigned` are values, not omission |
| Modularity/anchors | explicit modularity and anchor arrays | non-modular assets use `isModular: false`, `socketIds: []`; anchors may be empty |
| VFX/2.5D | nullable typed object | `vfx_anchor` requires `vfxAnchor`; `derivative_25d` requires `derivative25d` |
| Budget | `budgetClassId` | no unbudgeted production record; temporary assembly placeholders block release |
| Approval | six independent gate objects | reviewer/date/evidence may be null/empty only before a positive decision |
| Lifecycle | state, MVP preservation, replacement/deprecation links | replacement links and reason may be null/empty while not replacing/deprecating |

Schema validation proves shape and local conditions. Cross-record uniqueness,
filesystem/Addressables resolution, hashes, profile references, ordering, aggregate
budgets, and gate consistency require the inventory validator in section 11.

## 3. Stable identity and names

### 3.1 Canonical IDs

The taxonomy grammars are controlling and immutable after publication:

- family: `waf_<domain>_<family>`;
- kit: `wak_<context>_<domain>_<kit>_v###`;
- 3D/runtime asset: `wa_<context>_<domain>_<family>_<descriptor>_v###`;
- 2.5D derivative: `wad_<source_asset_token>_<view_or_state>_v###`.

`context` is one of `shared`, `neutral`, the four realm IDs, the four
`kingdom_<realm>` values, or an owner-approved `event_<token>`. Biome tokens remain
prohibited until a canonical biome authority exists. IDs never encode approval,
platform, LOD, file extension, path, or display text.

Records sort by UTF-8 bytewise `assetId`. Profiles sort by `id`. Aliases sort by exact
`legacyId`. Arrays that model a set use bytewise canonical IDs. LOD levels sort by
ascending index. Serialization is UTF-8, two-space indentation, LF, one terminal LF,
and no insignificant reordering. A formatter run twice must produce identical bytes.

### 3.2 Source and runtime object names

New work uses these names; existing shipped names remain aliases/references until a
separate migration proves every consumer:

| Item | Rule | Example for the canonical Crownlands Town Hall reservation |
| --- | --- | --- |
| source packet | `<category>_<realm-or-neutral>_<asset>_v###` | `architecture_crownlands_town_hall_base_v001` |
| retained source | `<asset-id>_source_v###` | `wa_crownlands_architecture_building_town_hall_base_v001_source_v001.blend` |
| prefab | `AL_<Category>_<Context>_<Descriptor>_v###` | `AL_Architecture_Crownlands_TownHallBase_v001.prefab` |
| render mesh | `M_<Context>_<Asset>_LOD#` | `M_Crownlands_TownHallBase_LOD0` |
| collider | `COL_<Asset>_<Role>_##` | `COL_TownHallBase_Footprint_01` |
| nav source/exclusion | `NAV_<Asset>_Walkable_##` / `NAVEX_<Asset>_<Role>_##` | `NAVEX_TownHallBase_Wall_01` |
| socket | `SOCKET_<SemanticRole>_##` | `SOCKET_Entrance_01` |
| material | `MAT_<Context>_<MaterialName>` | `MAT_Crownlands_CivicStone` |
| texture | `T_<AssetName>_<Map>_<Size>` | `T_CivicStone_N_1024` |
| Addressable key | `al/world/<canonical-asset-id>` | `al/world/wa_crownlands_architecture_building_town_hall_base_v001` |
| Addressable group | stable lower-snake profile ID | `al_world_mobile_common` |

The existing exact prefab tuple remains valid and is used by the test fixture:

```text
assetId  wa_crownlands_architecture_building_town_hall_base_v001
alias    building_crownlands_town_hall_production_v1
alias    building.crownlands.townhall.production.v1
path     Assets/AL/Art/Generated/Architecture/Crownlands/Production/TownHall/Runtime/Crownlands_TownHall_Production.prefab
guid     40d5f7687fed640fd8c0d4b1868ff0ef
sha256   71ea52234ec8aea93b91bf39ae41d111fa1a7d54cf181f54894e895d23463b46
```

This is preservation of an existing binding, not post-MVP creative or release
approval.

## 4. Prefab and Addressable binding

A record has exactly one `binding` object with a state:

- `unbound`: prefab and Addressable are null; non-empty reason required;
- `candidate`: at least one binding exists, but one or more gates remain incomplete;
- `verified`: binding existence, identity, bytes, import, and evidence passed;
- `admitted`: every independent approval gate passed and release gate admitted;
- `retired`: retained for deterministic migration/diagnostics, not new loading.

### 4.1 Prefab tuple

A prefab tuple is `{path, guid, sha256}`. The validator must prove:

1. normalized project-relative `Assets/.../*.prefab` path with forward slashes;
2. file exists in the clean checkout;
3. adjacent `.meta` exists and its GUID exactly matches lower-case 32-hex `guid`;
4. lower-case SHA-256 matches raw prefab bytes;
5. GUID/path is not claimed by a different canonical asset unless an explicitly
   shared derivative relationship permits it;
6. the imported root and required helper objects satisfy the selected profiles.

A path string without GUID/hash is not a binding. Folder inference is prohibited.

### 4.2 Addressable tuple

An Addressable tuple is `{key, groupId, labels[], assetKind}`. The key is always
`al/world/<canonical-asset-id>`. The validator must read the actual Addressables
settings and prove that the key resolves exactly once to the intended GUID, group,
kind, and labels. If both prefab and Addressable tuples are present, they must resolve
to the same GUID.

Addressables are optional metadata, not a mandate to install, enable, or migrate to
the Addressables package. Current building bindings remain prefab/Resources-backed.
An Addressable binding is introduced only when measured package size, residency,
streaming, or delivery evidence justifies it and the loader migration is separately
approved. A plausible key that is absent from Addressables settings is broken, not a
future fallback.

Bindings may target prefab, scene, TerrainData, mesh, material, texture/sprite/atlas,
VFX prefab, animation, audio, or technical prefab derivatives. Gameplay behavior,
spawn policy, balance, and traversal authority remain in their owning catalogs.

## 5. Source and provenance

Every record has mandatory source and provenance groups even when production has not
started. Provenance cannot be inferred from a filename, Git author, generator task,
or current runtime presence.

Required provenance facts are:

- exact source packet/version and retained artifacts when they exist;
- creator identities and authoring tools when known;
- source/reference/evidence links;
- rights/license state and declaration;
- AI use: `none`, `assisted`, `generated`, or `unknown`;
- cleanup status and similarity review;
- owner decision reference and open issues;
- immutable source-to-runtime derivation revision.

`cleared` requires identified creators, source references, cleared rights, and a
passed similarity review. Unknown rights, missing source hash, or missing owner
review blocks provenance and release. It must not be repaired with invented values.
Source-model hashes are mandatory before source promotion. A null hash is allowed for
an early handoff reference that is still explicitly incomplete; it can never support
`cleared` or `admitted`.

## 6. Coordinates, scale, pivots, and modular grids

The default coordinate profile is:

- Blender Metric, meters, unit scale `1.0`;
- source `+Z` up and `-Y` semantic forward;
- runtime Unity `+Y` up and `+Z` forward;
- `1 Unity unit = 1 meter`;
- exportable mesh, armature, and socket scales `(1,1,1)`;
- chunk-local meters; no continent-scale coordinates baked into vertices.

Legacy Y-up Blender sources retain an explicit legacy coordinate profile. Exporters
must not silently repair scale, ground, axes, or forward direction.

Named pivot profiles are mandatory:

- structures and props: base/footprint center on finished ground;
- walls and roads: base center, modular ends on exact bay boundaries;
- attachments: connection-face center, forward away from receiver;
- characters/fantasy beasts: root at world origin with supporting contact on source
  `Z=0`;
- terrain/chunks: catalogued chunk origin; corner-versus-center stays explicit until
  the world-authoring contract approves it;
- 2.5D: inherited source pivot plus deterministic camera/framing profile.

Default modular profile values remain the reviewed provisional envelope:

- authoring sub-grid `0.5 m`;
- placement cell `2 m × 2 m`;
- structural bay/bridge increment `4 m`;
- vertical tier `1 m`;
- right-angle module rotation increment `90°`.

Non-right-angle natural modules use a separately named profile. Exceptions require a
catalog `exceptionRef` with rationale, accountable owner, test evidence, and owner/
technical approval. Arbitrary per-record floats cannot replace a profile.

## 7. Materials, shaders, textures, and texel density

The current runtime profile targets Unity 6000.3.22f1 Built-In Render Pipeline while
retained PBR sources remain portable:

- physically plausible base color, normal, and packed-mask ownership;
- shared material families, trim sheets, and atlases before unique materials;
- opaque by default, alpha clip selectively, blended transparency only by measured
  exception;
- no always-on edge emission as realm or interaction authority;
- mipmaps and platform compression required;
- read/write disabled unless measured CPU access requires it;
- no per-instance material copies where property blocks/instancing can carry data;
- source/preview textures excluded from Player packaging.

Named texel-density profiles use object-space pixels per meter measured on the
runtime derivative, not source-image dimensions alone:

| Profile intent | Initial target | Tolerance | Typical use |
| --- | ---: | ---: | --- |
| terrain/horizon macro | `128 px/m` | `±25%` | terrain, cliffs, distant massing; layers/trims may supersede direct UV density |
| shared world trim | `256 px/m` | `±20%` | common structures, large props, modular interiors |
| close/hero surface | `512 px/m` | `±20%` | owner-approved close framing only |
| atlas small prop | `512 px/m` | `±25%` | small repeated props packed into shared atlases |
| non-spatial/2.5D | not applicable | — | validate output pixel dimensions, zoom coverage, and legibility instead |

These are initial production-consistency profiles, not permission to maximize texture
resolution. Normal runtime guidance remains shared 1K trims/atlases for common
buildings, 512–1K for small props, and 1K–2K only when hero framing proves value. A
unique 4K runtime texture for a small/medium asset is prohibited. Source and each
platform derivative record resolution, compression, resident bytes, and build-size
bytes separately.

## 8. LODs, far representation, and rendering pressure

Every renderable record selects a named LOD profile and records levels, exact triangle
counts, transition heights, material slots, shadow behavior, and protected cues.
`not_applicable` requires a reason.

Initial static-world planning is:

- LOD1 at no more than `60%` of LOD0 triangles;
- LOD2 at no more than `30%`;
- far mesh/impostor/HLOD near `10%` where a mesh remains;
- normal mobile play uses a reduced LOD when the camera distance permits;
- silhouette, gameplay state, interaction/harvest cue, route, entrance, allegiance,
  and realm-construction cues survive every tier;
- cross-fade overlap is included in profiling;
- lower tiers reduce tertiary detail, material/shadow pressure, secondary motion, and
  particles before gameplay truth.

Every repeated or long-range kit chooses one explicit impostor mode:
`not_applicable`, `authored_far_mesh`, `opaque_impostor`, or `chunk_hlod`.
`unresolved` blocks production. Transparent impostors require measured exception
evidence. Animated fantasy beasts/monsters additionally require bounded skin-update
tiers and a non-skinned crowd/far representation before mass-RvR readiness.

`PostMVP_World_Asset_Budgets_And_Readability_v1.md` owns the closed family-to-class
assignment, per-class ceilings, aggregate envelopes, mobile-floor thresholds,
readability gates, and exception process. This schema supplies exact measured fields and
mandatory `budgetClassId`; it does not invent or duplicate those budget values.

## 9. Collider, navigation, occlusion, and streaming policy

### 9.1 Collision

Render geometry is never collision authority. Prefer primitive and compound primitive
colliders, then convex or low-complexity static proxies only when primitives cannot
preserve gameplay truth. Terrain uses its approved collider derivation. Triggers are
separate. A stripped/non-readable render mesh must never be cooked as a runtime
MeshCollider. Collision is LOD-independent and cannot move a standing surface or
route as visual LOD changes.

Every record explicitly chooses `none`, a concrete collider mode, or `unverified` with
a reason. Character locomotion, hit volumes, interaction ranges, and selection
footprints remain prefab/gameplay authority and cannot be inferred from skinned or
render topology.

### 9.2 Navigation

Every record explicitly declares `none`, source, exclusion, link, combined, or
unverified participation. Walkable source is simplified upward-facing geometry;
exclusions use `NAVEX_`; links use paired semantic sockets. Material names do not
select nav areas or costs. Doors, gates, ladders, jumps, teleports, and siege states
need separate gameplay authority. A visible opening never authorizes traversal.

### 9.3 Occlusion

Every record selects a named mode: none, static occluder, separate obstruction groups,
cell/portal, or HLOD-owned. Roof, canopy, upper-wall, tower-crown, and tall-foliage
obstruction groups are separate where approved cameras can encounter them. Cutaway
views require complete backing walls/floors/interior faces; hiding an occluder must not
reveal voids. Occlusion metadata cannot change collision, nav, or gameplay visibility.

### 9.4 Streaming

Streaming metadata explicitly records residency, authoritative chunk IDs, optional
bundle ID, prefetch ring, and estimated/measured resident/build-size bytes. Allowed
rings are interaction, prefetch, and horizon. Source, concept, preview, and review
artifacts are excluded from Player packaging. Chunk ownership follows
`al_world_streaming_catalog.json`; the inventory references it and never invents
coordinates or replacement sockets.

A record marked chunk-owned must resolve every chunk reference and bundle dependency.
A record with no approved placement uses `unassigned`, empty chunk IDs, and held gates;
it must not be made always-resident as a convenience fallback.

## 10. 2.5D derivatives and VFX anchors

### 10.1 2.5D

A 2.5D derivative has a distinct `wad_` ID and `assetClass: derivative_25d`. It must
reference one canonical 3D source asset, exactly `dimension_kingdom_25d` and
`world_kingdom_private`, one view/state, and named deterministic camera/framing
profiles. It has independent creative, technical, accessibility, performance, and
release decisions. Approval of the 3D source does not approve the derivative.

Rendered sprites, thumbnails, icons, strategic building presentations, and state
layers may use prefab, texture/sprite/atlas, or Addressable bindings, but the key still
uses the derivative ID. Derivatives cannot replace the source record, copy its owner
approval, or hide gameplay state through color-only treatment.

### 10.2 VFX anchors

`assetClass: vfx_anchor` requires typed anchor IDs, effect role, quality states,
physical/off-state cue, and optional gameplay authority reference. Anchor objects are
behavior-free transforms named `SOCKET_<SemanticRole>_##`. They do not contain damage,
spawn, teleport, objective, weather, or interaction authority.

Every VFX family provides bounded `off`, `low`, `balanced`, `high`, and/or
`reduced_motion` states as applicable. Realm/objective identity and interaction state
must remain readable with VFX and emission disabled. Runtime pooling, particles,
transparent overdraw, lights, shadows, and update costs belong to the selected budget
class and physical-device evidence.

## 11. Validation and failure behavior

The downstream validator must fail closed and emit deterministic diagnostics for:

1. malformed schema/envelope/version or unknown fields;
2. duplicate/malformed family, kit, asset, profile, local-anchor, or alias IDs;
3. unsorted records/profiles/aliases/LOD levels and non-canonical serialization;
4. alias chains, cycles, ambiguous targets, canonical-ID shadowing, or aliases that
   do not resolve exactly one record;
5. family IDs absent from the taxonomy, unexplained category/realm gaps, or wrong
   realm applicability;
6. missing owner authority, final owner other than `project_owner`, missing decision
   reference, or unowned exception;
7. missing/contradictory source packet, provenance, rights, AI disclosure, cleanup,
   similarity review, owner decision, or derivation record;
8. unresolved profile references or a record without a budget class;
9. missing prefab, `.meta`, GUID mismatch, byte-hash mismatch, duplicate GUID/path,
   import failure, or helper/profile mismatch;
10. Addressable key absent, duplicated, wrong group/kind/labels/GUID, or disagreement
    with the prefab tuple;
11. broken realm/dimension/world/chunk/replacement-socket/source/runtime dependency or
    source-asset reference;
12. invalid scale/axes/pivot/grid, missing approved exception, or non-finite values;
13. LODs that do not reduce, duplicate indices, absent far decision, excessive material
    slots, or protected-cue evidence gaps;
14. render-mesh collision, LOD-dependent collision/nav, invalid nav link pairs, or
    occlusion groups with missing backing;
15. streaming records with unresolved chunks/bundles, source/preview packaging, missing
    measured bytes for a claimed pass, or aggregate budget overrun;
16. 2.5D derivative with missing source or copied approval; VFX anchor with missing
    off/reduced fallback or embedded gameplay authority;
17. any positive gate decision without reviewer, UTC decision, and evidence;
18. `production_approved` or `admitted` when any technical, creative, provenance,
    performance, accessibility, owner, release, exception, or external Gate 0 state is
    incomplete, failed, blocked, or held;
19. current-MVP fingerprint drift or replacement without an approved migration;
20. hardcoded C# content discovered for individual inventory records.

Failure result categories should distinguish `MalformedCatalog`, `DuplicateId`,
`MissingReference`, `BrokenPrefabBinding`, `BrokenAddressableBinding`, `HashMismatch`,
`ProvenanceBlocked`, `OwnerAuthorityMissing`, `ProfileMissing`, `BudgetUnassigned`,
`GateConflict`, and `CanonicalOrderViolation`. Production runtime publishes no partial
snapshot and creates no fallback content. Editor diagnostics may continue to report
all errors after the first, but the inventory remains unavailable.

Schema-only verification is available now:

```bash
uv run --with jsonschema python unity/SharedContracts/Tests/validate.py
```

The valid fixture preserves the current Crownlands Town Hall tuple while held. The
invalid fixture proves that catalog owner authority cannot be omitted.

## 12. Migration from existing manifest and `art_ref`/catalog records

Migration is additive and staged; no existing source is overwritten or deleted.

### Phase A — inventory without runtime switching

1. Fingerprint current `al_building_catalog.json`,
   `KingdomBuildingModelCatalog.asset`, `FirstSessionAuthoredAssetCatalog.asset`, and
   first-session terrain bindings.
2. Create inventory records in canonical `assetId` order. Preserve exact current
   prefab path/GUID/SHA tuples; do not rename or move files.
3. Retain existing snake and dotted building model IDs as exact aliases. Do not
   normalize punctuation/case and do not create alias chains.
4. Use `current_bound_partial` and `preserveMvpBinding: true` for current protected
   bindings. Their runtime presence does not imply complete provenance, performance,
   accessibility, or creative approval.
5. For the thirteen unbound building families and every unbound taxonomy family, use
   `bindingState: unbound`, null bindings, and a specific reason. Do not fabricate a
   prefab, Addressable key, source, biome, or approval.
6. Keep the first-session procedural terrain in its narrow existing catalog and refer
   to its replacement socket. Do not copy its generation data into the world inventory
   or classify it as final biome art.

### Phase B — classify legacy references

For each legacy `art_ref`, `asset_ref`, `model_asset_ref`, portrait, scene path, source
manifest entry, and handoff reference, classify it as exactly one of:

- runtime binding: promote only to a hash/GUID-verified `binding` tuple;
- retained source/provenance artifact: place under `source`/`provenance`;
- external narrow-authority reference: retain in `sourceReferences` or evidence;
- exact alias: add one `legacyId -> canonicalAssetId` mapping;
- unresolved/invalid reference: record an issue and keep gates held;
- irrelevant family reference: leave in its owning catalog, not the world inventory.

A concept-sheet or Blender source path is not a runtime binding. A runtime prefab is
not proof of source ownership. A display string is not an alias. A scene path is not an
Addressable key unless the actual Addressables settings prove it.

### Phase C — validate parity

Before runtime integration, produce deterministic reports for:

- old-to-new record and alias coverage;
- exact eight-building tuple parity and Resources-catalog fingerprint parity;
- missing/duplicate/broken bindings;
- source/provenance/owner coverage;
- profile and budget assignment;
- category, realm, world/chunk, 2.5D, and VFX coverage;
- canonical-byte stability across two independent formatting/validation runs.

Any unexplained old reference or changed tuple blocks migration.

### Phase D — separate runtime consumer migration

Only a separately approved task may add the inventory to a packaged manifest, implement
a generic immutable loader/query surface, add Addressables, or switch a runtime
consumer. That task must preserve saves and current MVP behavior, prove clean-checkout
packaging, support typed unavailable/invalid states, and pass physical mobile-floor and
owner gates. It must not add per-asset switches, dictionaries, paths, or fallback
content in C#.

The legacy catalogs and Resources manifests remain until every consumer and rollback
path is inventoried and a measured migration is approved. Deletion or bulk path moves
are not part of this standard.

## 13. Class coverage

The schema's `assetClass` union represents every taxonomy domain:

- terrain surfaces, decals, and water;
- vegetation and organic dressing;
- geology, caves, ores, mineables, and crystals;
- roads, bridges, walls, gates, and traversal;
- castles, fortresses, cities, service/general architecture;
- interior modules and all prop groups;
- interactables, harvestables, signage, and banners;
- VFX anchors and technical collision/nav/occlusion/streaming helpers;
- independent 2.5D derivatives;
- deferred fantasy beasts, monsters, and dragons.

Class coverage is representational only. The schema creates no asset and approves no
creative identity, gameplay behavior, biome, budget, or release.

## 14. Disposition

- Schema: defined and validated.
- Required versus nullable/empty fields: explicit.
- Naming and binding examples: internally consistent with taxonomy and preserved
  existing tuples.
- Provenance and owner authority: mandatory and fail-closed.
- Production standards: named, data-driven profiles with approved-exception references.
- Runtime content: unchanged.
- Asset generation and activation: not initiated; held behind independent gates and
  project-owner authority.
