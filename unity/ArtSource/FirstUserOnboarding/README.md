# First-user onboarding MVP asset packet

Status: `MVP_PRODUCTION_CANDIDATE` for the Editor-only isolated first-user trial. This packet is
not a visual-quality sign-off for a shipping Player.

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
- Concept source is retained at
  `unity/ArtSource/Enemies/CovenantSentinel/covenant_sentinel_concept_meshy_v001.png`.
- This is an original neutral humanoid fantasy foe. No Slagwhistle or other realm fauna was used.

### Neutral modular hall

- Authored with Blender 5.2.0 LTS by `build_onboarding_asset_packet.py` and retained as
  `neutral_covenant_hall_working_v001.blend`.
- Ten distinct required modules: floor, wall, inner corner, outer corner, doorway, ceiling beam,
  trim, brazier, banner stand, and crate/barrel prop.
- Exact traversal footprint: 8 m x 12 m. Authoring grid/cell/bay conventions follow the sealed
  runtime budget contract (0.5 m / 2 m / 4 m).
- Uses three distinct built-in Standard PBR materials and one soft-shadow directional light.
- The admission verifier enforces no more than 12,000 visible environment triangles, 35 renderers,
  three shared materials, one shadowed directional light, two unshadowed local lights, and 48
  ambient particles.

### Locked kingdom preview

- Reuses the existing catalogued Eldergrove production Town Hall prefab:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Runtime/`
  `Eldergrove_TownHall_Production.prefab`.
- It is presentation-only and remains `LockedPreviewOnly`; it grants no Kingdom access or save
  authority before lordship.

## Admission boundary

`FirstUserOnboardingFixedAssetManifestGate` accepts only the sealed authored provider. The
AssetDatabase-backed inventory verifier rechecks every role's exact canonical path, GUID, and file
SHA-256, plus all five sentinel PBR dependency maps. Arbitrary factories, caller-selected IDs,
asset drift, added runtime authority, and primitive fallback remain fail-closed.

## Honest BDO-quality gaps

- The retained champion reads as a modular MVP mannequin at close range and has no valid Humanoid
  Avatar or authored locomotion/combat animations.
- Champion body/armor materials are flat retained-source materials, not final authored texture sets.
- The neutral hall is a beveled modular candidate with PBR parameters but no authored texture atlas,
  decals, set dressing pass, or atmospheric VFX.
- The Meshy sentinel is a textured static combat candidate; it is not rigged and has no authored hit,
  defeat, or locomotion animation. Trial reactions are bounded state evidence only.
- The fixed Eldergrove Town Hall satisfies the deterministic test's locked preview slot; dynamic
  realm-matched structure selection is still future production integration.
- The evidence capture is the isolated Editor asset scene, not a claim that the shipping Player has
  reached Black Desert Online presentation quality.
