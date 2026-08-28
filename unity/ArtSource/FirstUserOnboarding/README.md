# First-user onboarding MVP asset packet

Status: `MVP_RUNTIME_CANDIDATE` for the production first-session route and the isolated admission
trial. This is an authored MVP promotion, not a final BDO-quality visual sign-off.

## Sources and provenance

### Modular champion

- Canonical retained source: `unity/ArtSource/Champions/champion_vanguard_working_v001.blend`
- Source SHA-256: `10c07b25d4e7316ac664621adaf11156066b57ab9a35471df9d76ab3900bc4cb`
- Exporter: Blender 5.2.0 LTS via `build_onboarding_asset_packet.py`
- Runtime assets: separate body, basic armor, and basic weapon FBX files under
  `Assets/AL/Art/Production/FirstUserOnboarding/Characters/`.
- The body retains its skinned armature and the trial verifies non-empty skinned meshes and bone
  bindings. The current source does not produce a valid Unity Humanoid Avatar; that is an explicit
  animation-quality gap, not hidden by the admission gate.

### Common covenant sentinel

- Original concept generated with Meshy text-to-image task
  `01a0223f-9646-7e4c-a0a8-33ae149dc4b8`, `nano-banana-pro`, 9 credits.
- 3D generated with Meshy image-to-3D task
  `01a02241-4643-75f5-bf6a-318e9b436313`, `meshy-6`, 30 credits.
- Requested budget: triangle topology, 12,000 target polygons, PBR textures, A-pose, FBX.
- Received production candidate: 12,441 triangles; staging camera/light/cube removed; five PBR
  maps capped at 1024 px by the reproducible Blender cleanup step.
- Rigged with Meshy task `01a03125-7885-70bd-9af0-0dec1e975963`, 5 credits. The retained runtime
  file is the skin-bound `Covenant_Sentinel_Meshy6_Walking_v002.fbx`, which supplies both the
  guardian mesh/rig and walking clip without shipping a duplicate skinned FBX. The production binder plays it through an
  `AnimationPlayable` while existing gameplay telegraphs, colliders, health, and attack authority
  remain unchanged.
- Concept source is retained at
  `unity/ArtSource/Enemies/CovenantSentinel/covenant_sentinel_concept_meshy_v001.png`.
- This is an original neutral humanoid fantasy foe. No Slagwhistle or other realm fauna was used.

### Neutral modular hall

- Authored with Blender 5.2.0 LTS by `build_onboarding_asset_packet.py` and retained as
  `neutral_covenant_hall_working_v001.blend`.
- Pinned source SHA-256 after the objective technical-helper remediation:
  `b807a8ec7d5332a70774405ccf240a16e8555787c9de0778303d0ebe54d85a5c`.
- Ten distinct required modules: floor, wall, inner corner, outer corner, doorway, ceiling beam,
  trim, brazier, banner stand, and crate/barrel prop.
- Exact traversal footprint: 8 m x 12 m. Authoring grid/cell/bay conventions follow the sealed
  runtime budget contract (0.5 m / 2 m / 4 m).
- Separate render-hidden `AL_COLLISION`, `AL_NAVIGATION`, and `AL_SOCKETS` collections retain an
  exact floor-bounds box proxy, a four-vertex upward walkable source, and an entrance socket at
  `(-4, 0, 0)` facing inward. They are bake/import inputs, not a replacement for Unity Terrain or
  runtime navigation authority.
- Uses three distinct built-in Standard PBR materials and one soft-shadow directional light.
- The admission verifier enforces no more than 12,000 visible environment triangles, 35 renderers,
  three shared materials, one shadowed directional light, two unshadowed local lights, and 48
  ambient particles.

### Neutral terrain-landmark kit review candidate

- Retained review source:
  `neutral_covenant_terrain_landmark_kit_working_v001.blend`, SHA-256
  `382765b66936cf744d423a37fe171b4c7c90886f73090e3a968373f9085f089c`.
- This source is deliberately `review-candidate`, has `runtimeAuthority: false`, and is not in the
  authored runtime catalog or any generated world. It must not be interpreted as admitted content.
- The path beacon (`736 / 220 / 48` triangles), trail post (`412 / 132 / 24`), and 4 m boundary
  wall (`692 / 176 / 12`) each have strict LOD0/1/2 reductions and base-center, identity-transform
  pivots in metric Z-up source coordinates.
- Each family has a primitive `COL_*` proxy, upward-facing `NAVEX_*` footprint, and stable
  interaction or wall-end `SOCKET_*` helpers in the standard `AL_COLLISION`, `AL_NAVIGATION`, and
  `AL_SOCKETS` collections. These are engine-neutral bake/import inputs, not navigation policy.
- Material parameters are copied exactly from the retained Neutral Covenant Hall so the candidate
  can be judged in the same neutral construction envelope without selecting a final texture,
  palette, wear, decal, biome, placement, or lore direction.
- Headless authoring, validation, review GLBs, and contact sheets are reproducible through
  `tools/blender/author_neutral_terrain_landmark_kit.py`,
  `tools/blender/validate_al_asset_sources.py`, `tools/blender/export_al_asset_candidate.py`, and
  `tools/blender/render_al_asset_review_contact_sheet.py`. The semantic source receipt is pinned at
  `f7c3f2b9ea440ba236b36463661a47f9a79600a7785126de2c831c3bc749cad6`; Blender file bytes are
  not claimed deterministic across saves.

### Locked kingdom preview

- Reuses the existing catalogued Eldergrove production Town Hall prefab:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Runtime/`
  `Eldergrove_TownHall_Production.prefab`.
- It is presentation-only and remains `LockedPreviewOnly`; it grants no Kingdom access or save
  authority before lordship.

## Admission boundary

`FirstUserOnboardingFixedAssetManifestGate` continues to accept only the sealed isolated provider.
The production route uses the generated typed
`Resources/FirstSessionAuthoredAssetCatalog.asset`; its builder binds only the admitted hall,
champion kit, rigged sentinel, five sentinel PBR maps, walking clip, and catalogued four-realm
production architecture. Missing roles fail closed before the first-session scene is built.

## Honest BDO-quality gaps

- The retained champion reads as a modular MVP mannequin at close range and has no valid Humanoid
  Avatar or authored locomotion/combat animations.
- Champion body/armor materials are flat retained-source materials, not final authored texture sets.
- The neutral hall is a beveled modular candidate with PBR parameters but no authored texture atlas
  or decal pass; production architecture and existing bounded atmosphere provide the first runtime
  dressing layer.
- The Meshy sentinel is rigged and has skin-bound locomotion. Dedicated authored attack, hit, and
  defeat clips remain a post-MVP quality gap; existing reviewed combat telegraphs remain authoritative.
- Realm-matched Town Hall and specialist structures are now selected for Stonehold, Eldergrove,
  Crownlands, and Umbral. These are production-runtime LOD assets, but the surrounding city breadth
  remains outside this bounded first-session room.
- Runtime and isolated captures are acceptance evidence, not a claim that the full game has reached
  Black Desert Online presentation quality.
