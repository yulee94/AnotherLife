# AnotherLife Blender Asset Production Contract

**Status:** Technical production foundation; creative-final approval remains with the project owner

**Version:** 1

**Validated DCC:** Blender 5.2.0 LTS

**Runtime baseline:** Unity 6000.3.22f1, Built-In Render Pipeline

## Purpose and authority

This contract makes Blender work for terrain modules, structures and props, Champions, NPCs,
and creatures interoperable and cheap to iterate. It does not select final terrain biomes,
architecture silhouettes, faces, anatomy, palettes, balance, lore, rendering pipeline, or target
hardware. Those remain owner decisions.

Authority order remains:

1. the owner's latest explicit decision;
2. an approved asset-specific source packet;
3. root `DESIGN.md`;
4. this technical exchange contract;
5. prototypes and generated blockouts.

This document consolidates the measurable parts of `DESIGN.md`,
`Champion_Character_Sheets_Blender_Handoff.md`, the Champion Vanguard sidecars,
`FourRealm_Modular_Construction_Envelope.md`,
`Terrestrials/Ecosystems/Ecosystem_Source_Budgets_And_Asset_Layout.md`, and the Slagwhistle
pilot. More-specific approved packets override the general ceilings here.

## One asset, several derived products

Each asset family has one versioned retained source and separately generated runtime products:

```text
approved packet + .blend
          |
          +-- render meshes and LODs
          +-- simple collision proxies
          +-- walkable/nav source, exclusions, links, and sockets
          +-- rig and animation clips where applicable
          +-- material/texture derivatives
          +-- validation report and source hashes
```

Render topology is never collision, pathfinding, selection, or gameplay authority merely because
it is already present. Those products share coordinates and stable asset IDs but remain separate.
This allows Unity AI Navigation and a future native/Rust bake or query kernel to consume the same
author-authored source without maintaining two world truths.

## Coordinate and transform contract

| Concern | Required rule |
| --- | --- |
| Units | Blender Metric, meters, unit scale `1.0`; `1 Unity unit = 1 meter` |
| New Blender source axes | Blender `+Z` up, character/object forward `-Y` |
| Unity result | Unity `+Y` up, `+Z` forward |
| FBX conversion | `axis_forward="-Z"`, `axis_up="Y"`, unit scale applied |
| Object transforms | Exportable mesh, armature, and socket scale applied to `(1,1,1)` |
| Characters and creatures | `root` at world origin; supporting contact at source `Z=0` |
| Buildings and props | Root at footprint/base center on finished ground unless an approved packet declares another placement pivot |
| Terrain/chunks | Root equals the catalogued chunk origin; exact corner-versus-center policy stays `OPEN` until the world-authoring chunk contract chooses it |
| Precision | Author and validate in chunk-local meters; do not bake continent-scale world coordinates into mesh vertices |

The retained `neutral_covenant_hall_working_v001.blend` is a documented legacy exception: its
mesh coordinates are Unity-style `Y`-up inside Blender. Its reviewed render geometry remains
pinned by a deterministic selected-object GLB. The working source also contains technical-only
helpers derived from that geometry under the source-specific remediation receipt. Do not copy the
axis pattern into new sources; replace it only with a new source version after runtime comparison.

No exporter may silently repair scale, ground, or facing. The source validator fails those
conditions before export so Unity and native consumers receive the same coordinates.

## AnotherLife visual consistency

All categories use the active **mystical medieval naturalism** style lock:

- Start with believable anatomy, load paths, fabrication, weather, repair, and contact.
- Keep a strong primary silhouette and major negative spaces before surface detail.
- Use the `70 / 20 / 10` primary/secondary/tertiary hierarchy as a composition check.
- Make realm identity structural—construction, silhouette, material, motion, and ornament—not a
  palette swap.
- Keep magic sourced and restrained. It cannot outline every edge or replace physical form.
- Validate silhouette, grayscale, neutral lighting, gameplay distance, and the lowest intended
  quality tier before beauty-render approval.
- Reuse coherent rigs, material families, trims, sockets, and construction grammar before making
  unique one-off assets.

Category application:

| Category | Protected read | Production structure |
| --- | --- | --- |
| Terrain/habitat | route, grade, horizon, landmark, biome transition | connected terrain or chunk-local mesh surfaces; separate cliffs, caves, roads, water, foliage, collision, and nav source |
| Structures/props | function, footprint, entrance, roofline, load path, realm construction | modular bays, shared trims/atlases, simple selection/collision, stable entrance/activity sockets |
| Champion | adult anatomy, face/hands, combat silhouette, main/off hand, realm cue | shared Humanoid-compatible body/rig envelope; body, armor, weapon, cape, and 11 modular slots remain separable |
| NPC/soldier | profession, hierarchy, maintenance, readable crowd silhouette | controlled modular variation on shared anatomy/rig families; profession is not communicated by color alone |
| Creature | ecology, center of mass, contact pattern, profile cue, restrained integrated magic | anatomy-compatible rig family only when deformation and gait genuinely match; protected cue survives every LOD |

## Terrain and modular world outputs

The world-authoring lane should use connected Unity Terrain tiles for organic traversable land and
modular meshes for towns, castles, bridges, cliffs, caves, walls, roads, rivers, and encounter
spaces. The Blender lane supplies mesh modules and derived geometry; it does not hand-edit
`Assets/AL/Worlds/Generated`.

Every production terrain/chunk family must provide:

- a catalog asset ID and source version;
- chunk-local bounds and declared chunk-origin policy;
- render surface or referenced `TerrainData` source;
- a simplified physical surface or declared TerrainCollider derivation;
- walkable source surfaces and explicit non-walkable exclusions;
- exact edge samples/sockets for every connected neighbor;
- road, river, wall, cave, and transition sockets where applicable;
- LOD/proxy intent, material layers, vegetation/prop families, and collision ownership;
- a three-neighbor seam check for position, normal, collider, and walkable continuity;
- source-to-runtime hash and a retained bake/validation report.

The existing `128 m x 128 m` ecosystem reference cell is a design/profiling cell, not an approved
runtime terrain-tile size. Tile size, world-origin policy, height resolution, material-layer count,
and final biome treatment remain open until the owner and world-authoring implementation approve
them.

### Reversible first-session landmark review kit

`neutral_covenant_terrain_landmark_kit_working_v001.blend` is the first environment-kit source
made entirely through this contract. It is a **review candidate**, not runtime authority and not a
creative-final selection. It is absent from runtime catalogs and generated worlds.

- Source SHA-256:
  `382765b66936cf744d423a37fe171b4c7c90886f73090e3a968373f9085f089c`.
- Semantic receipt SHA-256:
  `f7c3f2b9ea440ba236b36463661a47f9a79600a7785126de2c831c3bc749cad6`.
- Path beacon LODs: `736 / 220 / 48` triangles.
- Trail post LODs: `412 / 132 / 24` triangles.
- Four-meter boundary-wall LODs: `692 / 176 / 12` triangles.
- All 15 render/collision/navigation mesh objects use a base-center source origin, identity
  transforms, metric units, Z-up, and declared `-Y` forward.
- Every family has a low-complexity box `COL_*` proxy and four-vertex upward-facing `NAVEX_*`
  footprint. The beacon/post have interaction sockets; the wall has stable A/B module-end sockets.
- Its three material parameters are measured copies of the Neutral Covenant Hall material envelope.
  This preserves comparison consistency without choosing final texture, weathering, color,
  heraldry, biome adjacency, or magic treatment.

Author it with `tools/blender/author_neutral_terrain_landmark_kit.py`; the command refuses to
overwrite source or receipt. The semantic receipt is deterministic across Blender saves even though
compressed `.blend` bytes are not claimed byte-deterministic. Review-only GLBs are produced with
the existing selected-object exporter and remain `promotionEligible: false` because the source
approval state is `review-candidate`. `tools/blender/render_al_asset_review_contact_sheet.py`
renders a nine-cell LOD matrix and a collision/navigation/socket sheet outside Unity read paths,
fails closed if review content falls outside either camera, and records framing bounds in its
receipt. Superseded source/render candidates remain under `archive/local-run/blender/superseded/`.

## Collision and pathfinding geometry

### Collections and names for new sources

Use these collections when the category needs them:

| Collection | Contents |
| --- | --- |
| `AL_RENDER` | Runtime-visible meshes and LODs |
| `AL_COLLISION` | Simple physical and selection proxies |
| `AL_NAVIGATION` | Walkable surfaces, exclusions, and link debug geometry |
| `AL_SOCKETS` | Empty transforms for entrances, links, transitions, interactions, VFX, and attachments |

Use stable names:

- render mesh: existing project convention `M_<RealmOrNeutral>_<Asset>_LOD<n>`;
- physical proxy: `COL_<Asset>_<Purpose>_<nn>`;
- walkable source: `NAV_<Asset>_Walkable_<nn>`;
- excluded volume/surface: `NAVEX_<Asset>_<Reason>_<nn>`;
- entrance or transition: `SOCKET_Entrance_<nn>` / `SOCKET_Transition_<nn>`;
- paired traversal link endpoints: `SOCKET_NavLink_<PairId>_A` and
  `SOCKET_NavLink_<PairId>_B`.

Names support review and import; runtime identity comes from the catalog asset ID plus a stable
local element ID, never Blender collection order, Unity hierarchy order, or save-list order.

### Walkable-source rules

- Walkable source is a simplified, evaluated mesh in meters with applied transforms and explicit
  upward normals. It is not a copy of high-detail render topology.
- Boundaries that meet another chunk share the same endpoint positions in chunk-local space.
- Doors, gates, stairs, ledges, jump gaps, ladders, bridges, and teleport-like transitions expose
  sockets; the art file does not invent traversal capability, agent radius, cost, or gameplay rules.
- Exclusions identify geometry that must not seed traversal. Area types and costs remain catalog or
  bake-profile data, not material names.
- A link is a stable paired transform plus intent metadata. Unity may turn it into a NavMeshLink;
  a native kernel may turn it into a graph edge. Neither derived representation becomes source
  authority.
- Dynamic blockers, doors, siege damage, and ownership state stay runtime data. Blender supplies
  static bounds and sockets only.

### Engine-neutral bake boundary

The bake input must be serializable without Unity objects or Blender data blocks. Its exact wire
format is still open, but it must contain at least:

- schema version, source asset ID/version/hash, and chunk ID;
- coordinate-system declaration (`meters`, Unity `Y` up / `Z` forward after conversion);
- indexed triangle positions and deterministic winding;
- stable surface/exclusion IDs;
- paired link/socket IDs and local transforms;
- neighbor edge IDs and source hashes;
- bake-profile ID rather than hard-coded agent dimensions.

Unity AI Navigation and a future Rust FFI helper can independently consume that same input. Rust is
appropriate for deterministic geometry processing, path-query kernels, procedural generation, and
high-throughput validation; it does not justify hiding game authority in an opaque plug-in or
duplicating C# and native world state.

### Collider rules

- Prefer boxes, capsules, and low-complexity convex proxies. Use a dedicated static collision mesh
  only when primitives cannot preserve gameplay truth.
- Character locomotion capsules, hit volumes, and interaction ranges are prefab/runtime authority;
  they are not inferred from skinned render topology.
- Never assign a stripped/non-readable render mesh to a `MeshCollider` at runtime. Bind an imported
  collision proxy at authoring/import time or create an explicit primitive collider.
- Render meshes keep read/write disabled unless a measured CPU requirement exists.
- Collision and walkable surfaces must be validated at LOD-independent coordinates; visual LOD
  changes cannot change where players stand or whether a route exists.

## Character, Champion, and NPC contract

Champion sources retain the 11 slot collections:

`head`, `hair`, `face`, `torso`, `shoulders`, `arms`, `legs`, `cape`, `main-hand`,
`off-hand`, and `realm-ornament`, plus non-slot `anchors`.

Required technical rules:

- one shared body/rig envelope across compatible realm/body variants;
- Unity Humanoid-compatible naming where the approved character family is Humanoid;
- under 90 Champion deformation bones unless profiling approves an exception;
- no more than four non-zero bone influences per vertex at **every** LOD;
- body, shield, weapon, cape, and equipment remain separate reviewable components;
- ground-center root and source `-Y` facing;
- attachment sockets remain stable across body and LOD variants;
- no full cloth simulation dependency for the baseline cape; use a short skinned mantle or rigid
  segmented solution unless a later device-tested packet approves more;
- crowd/far representation is mandatory for mass-RvR planning and preserves realm, weapon class,
  allegiance, and action silhouette without requiring a full skinned LOD0.

NPCs reuse an approved shared rig/material family when anatomy and clothing seams permit it. They
still require a stable catalog ID, profession/rank silhouette intent, collider profile, sockets,
LOD stack, and animation set. Scenes store spawn instructions and persistent IDs, not hundreds of
always-live authored NPC objects.

## Creature contract

- Define habitat, diet, locomotion, contact points, defensive strategy, and protected silhouette
  before surface production.
- Preserve the approved profile cue through LODs and motion extremes.
- Share a rig only when joint count, contact pattern, deformation, and gait remain compatible.
- Baseline supporting-family motion needs rest/weight shift, locomotion, turn, alert/observe,
  habitat interaction, and recovery.
- Common/ambient creature colliders stay simple and separate; detailed render extremities do not
  silently enlarge gameplay collision.

## Runtime ceilings and LODs

These are provisional maximum planning values from `DESIGN.md`, not fill targets:

| Category | LOD0 triangle ceiling | Typical LOD0 slots | Texture guidance |
| --- | ---: | ---: | --- |
| Champion / major character | `60k` | `3` | up to `2K` primary sets |
| Important NPC / elite | `45k` | `3` | `1K–2K` |
| Ambient creature / common unit | `25k` | `2` | usually shared `1K` |
| Hero building | `40k` | `3` | shared `1K–2K` |
| Common building / large prop | `20k` | `2` | usually shared `1K` |
| Small prop | `5k` | `1` | `512–1K`, atlas repeated props |

Asset-specific packets can be stricter. Champion Vanguard currently targets `8k–18k`, `3k–6k`,
and `800–1,500` for its three mesh LODs. Slagwhistle LOD0 has an `8k–10k` pilot range.

Start general LOD planning near `50–60%`, `20–30%`, and `5–10%`/impostor, then tune by protected
silhouette and measured camera coverage. Lower tiers remove tertiary ornament, secondary motion,
material slots, shadow cost, and particles before player visibility, physical collision, walkable
truth, attack origins, objective cues, or realm silhouette.

Mass-RvR readiness additionally requires:

- static instancing and shared material families rather than per-instance material copies;
- animation/skin update distance tiers and a non-skinned crowd/far representation;
- chunk-addressable dependency ownership and no source/preview assets in Player packaging;
- bounded transparent layers, particles, dynamic lights, and shadows;
- separate low-mobile identity packages so lowering fidelity never changes gameplay truth;
- representative crowd, traversal, streaming, and build-size profiling before a budget becomes
  production authority.

## Materials and textures

- Use physically plausible PBR sources with named color, normal, and packed-mask ownership.
- Keep editable/bake sources separate from runtime derivatives.
- Prefer opaque shared material families, trim sheets, and atlases.
- Pack compatible masks and use mipmaps and platform compression.
- A unique `4K` runtime texture for a small/medium asset requires explicit approval and pixel-
  coverage evidence.
- Final shader features and realm palette tokens wait for the owner-approved render-pipeline and
  representative material decision. Source maps must remain portable enough to derive Built-In or
  a future approved pipeline version.
- Validate dark surfaces and controlled emission in neutral and gameplay lighting, not only the
  Blender viewport.

## Export, versioning, and provenance

- Never overwrite an approved source. Produce `<asset>_v002` or later and retain the approval trail.
- Export selected runtime objects only; no cameras, lights, preview primitives, hidden helpers, or
  accidental GLB contents.
- Apply units/transforms, use the declared axis conversion, omit FBX leaf bones, and document any
  exception.
- Triangulate deterministically before promotion or prove the exported triangles match reviewed
  topology. N-gons are a review warning.
- Keep render, collision, nav, sockets, rig, animations, materials, textures, and source hashes
  version-linked.
- Record author/tool/version/date, source references, licenses, AI prompts/tasks where applicable,
  cleanup, similarity review, and human approval.
- Generated Unity assets and generated world scenes remain derivatives, not hand-authored source.

## Deterministic validation

The portable source manifest is:

`unity/ArtSource/al_blender_source_validation.v1.json`

Its strict Draft 2020-12 schema is:

`unity/ArtSource/al_blender_source_validation.schema.json`

The headless validator is:

`tools/blender/validate_al_asset_sources.py`

Run from any checkout location:

```bash
blender --background --python-exit-code 1 \
  --python tools/blender/validate_al_asset_sources.py -- \
  --output archive/local-run/blender/al-blender-source-validation.json
```

Use `--fail-on-gaps` only for production promotion. Ordinary MVP iteration fails on contract
errors but reports known missing promotion outputs separately.

The validator also verifies manifest/export-set cross-references, finite and invertible source
transforms, finite mesh coordinates, strictly reducing declared LOD stacks, normalized deform-bone
weights, and every declared export-set object. Promotion helpers must use their required type and
collection, remain render-hidden, carry stable technical metadata, contain non-empty geometry, and
use upward-facing walkable normals. Its receipt metrics include LOD reduction ratios,
UV/shape-key inventory, Blender 5 layered-action slots/keyframes/assignments, resolved export-set
contents, per-skinned-mesh influence evidence, and a top-four influence-prune preview. A prune
preview is evidence of risk, not authorization to rewrite weights.

## Deterministic review export

Every retained source now declares one explicit selected-object export set. Produce a GLB review
artifact with:

```bash
blender --background --python-exit-code 1 \
  --python tools/blender/export_al_asset_candidate.py -- \
  --source neutral-covenant-hall-working-v001 \
  --export-set mvp-render \
  --output archive/local-run/blender/exports/neutral_covenant_hall_mvp_render_v001.glb \
  --allow-promotion-gaps
```

The exporter never overwrites the retained `.blend` or an existing output, and the artifact name
must retain the source `vNNN` token. It blocks hard source errors, blocks promotion gaps unless
they are explicitly acknowledged for a review/MVP artifact, exports only the manifest-resolved
objects, caps the GLB skin stream at four weights, parses the actual GLB node/mesh inventory,
reimports the artifact, and checks triangle count plus world bounds. The adjacent `.receipt.json`
pins source, manifest, Blender, exporter, validator and artifact hashes, selected objects,
validation disposition, coordinate system, and round-trip evidence. `promotionEligible: false`
remains explicit when gaps were allowed; a valid review export is not production approval.

Before applying character transforms, run the non-writing preview:

```bash
blender --background --python-exit-code 1 \
  --python tools/blender/analyze_al_transform_normalization.py -- \
  --source crownlands-champion-male-base-working-v001 \
  --output archive/local-run/blender/crownlands-champion-male-transform-normalization-preview.v001.json \
  --require-safe
```

It applies the proposed normalization in memory only and compares basis vertices, every shape key,
rest bones, attachment sockets, pose matrices, and final evaluated/deformed vertices at five
sampled animation frames. A non-zero `--require-safe` result means the transform must be
deliberately rebaked into rig, sockets and animation rather than repaired by a generic Apply
Transform operation.

The only current in-place source repair plan is the objective hall helper derivation:

```bash
blender --background --factory-startup --python-exit-code 1 \
  --python tools/blender/remediate_al_asset_sources.py -- \
  --source neutral-covenant-hall-working-v001 \
  --output archive/local-run/blender/hall-remediation-v001.json
```

Dry-run is the default. `--apply` is required to save. The remediator refuses source-hash drift,
unsupported sources, ambiguous doorway projection, pre-existing partial/conflicting helpers,
render-object changes, or failed post-validation. It is idempotent after the manifest pins the new
source hash. This is deliberately not a general transform, weight-pruning, LOD-generation, or
animation-generation command.

The validator currently pins and re-measures six retained sources; validation itself never
rewrites them:

| Source | Re-measured result | Current disposition |
| --- | --- | --- |
| Neutral Covenant Hall v001 | `3,200` render tris, `10` render modules, `3` shared materials, exact `8 m x 12 m` floor; `8`-vertex collision box, `4`-vertex walkable source, one entrance socket | Candidate-valid with zero promotion gaps. Legacy Y-up and eight render n-gons remain explicit warnings; runtime import/binding still needs review |
| Neutral Covenant terrain-landmark kit v001 | Three independent strict LOD stacks (`736 / 220 / 48`, `412 / 132 / 24`, `692 / 176 / 12`); three primitive collision proxies, three upward navigation exclusions, four sockets, and 15 pinned base-center pivots | Candidate-valid with zero promotion gaps or warnings, but intentionally `review-candidate`; review exports cannot promote and no runtime placement exists |
| Champion Vanguard v001 | `11,386 / 4,480 / 1,084` tris, `22` bones, `1.781 m`, grounded and correctly facing | LOD budgets pass; `488` lower-LOD vertices exceed four weights. Blind top-four pruning would discard up to `0.372646` weight on one vertex (`0.103056` at p95), so the exporter correctly blocks it pending a new version and deformation review |
| First-session male Champion base v001 | `32,293` tris, `24` bones, `4` slots, `1` keyed walk action (`249` curves / `7,468` keys), grounded and correctly facing | Source reaches **10** influences (`2,412` affected; max discard preview `0.206957`), has `0.01` armature scale/non-applied mesh rotation, missing external image paths, and no cheaper/crowd LODs. Generic transform apply moved sampled pose matrices by up to `1.721534`, evaluated vertices by `1.717510 m`, and socket matrix components by `69.854520`, so a rig/action/socket rebake is required |
| First-session female Champion base v001 | `44,750` tris, `24` bones, `4` slots, `1` keyed walk action (`249` curves / `7,398` keys), grounded and correctly facing | Source reaches **8** influences (`2,224` affected; max discard preview `0.190355`), has `0.01` armature scale/non-applied mesh rotation, missing external image paths, and no cheaper/crowd LODs. Generic transform apply moved sampled pose matrices by up to `1.690308`, evaluated vertices by `1.683435 m`, and socket matrix components by `67.245710`, so a rig/action/socket rebake is required |
| Slagwhistle v001 | `9,200` tris, `38` deform bones, one material, max four influences, grounded | LOD0 pilot passes; LOD1, LOD2, impostor, and six keyed source actions remain explicit promotion gaps |

The bounded hall remediation changed its pinned source SHA-256 from
`3d4b166ff39d8a1eb6739e23a7e40d779dfeadc144df6e35837d0970f894b51e` to
`b807a8ec7d5332a70774405ccf240a16e8555787c9de0778303d0ebe54d85a5c`. It added only
`AL_COLLISION`, `AL_NAVIGATION`, and `AL_SOCKETS` helpers derived from the existing floor and
doorway bounds. The selected ten-object render GLB remained byte-identical before and after at
SHA-256 `3c112f2127def3aafa69e5cde8e544106bf2e9aef6fa68c5bed3a0e07394e8ef` (`3,200`
triangles). The full audit moved from `7` hard errors / `12` promotion gaps / `5` warnings to `7` /
`10` / `5`; the remaining failures are the deliberately unrepaired character and creature work
below.

The existing Champion reports measured the four-influence ceiling on LOD0 only. This validator
checks every declared skinned LOD, which exposed the lower-LOD defect. Do not repair or overwrite
the approved `.blend` in place; derive a new version, clamp/normalize weights deliberately, render
deformation comparisons, import to Unity, and obtain owner approval.

The first-session male/female FBX importers declare `maxBonesPerVertex: 4`, so Unity prunes the
working-source weights for the current MVP. That protects the present Player import but does not
make the retained sources portable or ready for a native skinning/crowd pipeline. A v002 cleanup
must apply transforms, limit and normalize weights before export, relink or pack retained texture
sources, then compare the walk and all body-build shape keys in Blender and Unity.

## Current visual audit

The retained preview/render evidence separates technical readiness from aesthetic readiness:

- `neutral_covenant_hall_working_v001.blend` is a readable, correctly scaled modular traversal
  graybox with controlled shared materials. It is useful for the playable MVP but lacks the authored
  atlas, wear, heraldic craft, and district dressing needed for mystical medieval naturalism.
- The Neutral Covenant terrain-landmark kit is technically coherent with that hall and keeps the
  path-beacon fork, trail-post profile, and wall module readable through LOD2. Those silhouettes are
  reversible hypotheses for owner review, not an approved lore or terrain-placement decision.
- `champion_vanguard_working_v001.blend` is an intentionally separated modular mannequin. Its
  shield/cape/slot structure is useful technical scaffolding, but its visible anatomy, body gaps,
  flat materials, and primitive equipment do **not** meet final AnotherLife character quality.
- The first-session male/female bases provide plausible adult bodies, one walk clip, four material
  regions, four build blendshapes, and equipment sockets. They remain generic under-dressed bases;
  the approved realm/class armor, hair, material craft, and controlled magical focal read must come
  from separately approved modules rather than treating these bases as finished Champions.
- Slagwhistle is closest to the tactile naturalistic surface target, with believable mass and
  restrained dark material variation. Its recumbent bind pose, incomplete vent-yoke read, and
  lingering mole/armadillo silhouette are still owner-review questions, not production acceptance.

The correct parallel art plan is therefore not to multiply final skins from these sources. Keep the
hall and Champion bases as explicit MVP runtime candidates while a new-version terrain/navigation
kit, Champion deformation/LOD cleanup, approved armor modules, and creature LOD/motion work proceed
behind measurable gates.

## Promotion evidence

An asset is not production-ready until its applicable evidence exists:

1. complete brief and owner-approved silhouette/source packet;
2. source hash, units, axes, pivot, scale, dimensions, and modular inventory;
3. exact per-LOD triangles, material slots, UVs, textures, bones, and influences;
4. collision/nav/socket outputs or an explicit category-specific reason they live in the prefab;
5. neutral, silhouette, grayscale, gameplay-distance, and LOD-transition captures;
6. rig/contact/deformation and animation validation where applicable;
7. Unity import/prefab validation with source linkage and no runtime mesh-cooking warnings;
8. representative mobile-low and PC profiling, memory/build-size evidence, and streaming behavior;
9. owner creative-final approval.

## Decisions still owned by the user

- final terrain biomes, landforms, surface/material language, foliage, settlements, and atmosphere;
- terrain tile size, origin/pivot policy, height resolution, and final neighbor-bake workflow;
- render-pipeline migration and final material/palette tokens;
- final Champion body/face range, armor silhouettes, hair, cloth, and deformation quality;
- whether Slagwhistle's recumbent bind pose and current silhouette remain approved;
- lowest target hardware, frame-time, memory, install-size, and crowd-density targets;
- which NPC professions and creature families enter the first playable/RvR content slice.

Until those decisions land, the Blender lane can safely build validators, neutral technical
proxies, sockets, LOD/collider/nav derivations from already approved silhouettes, and review
captures. It must not multiply final visual variants around an unresolved choice.
