# First-session authored visual replacement

Status: production-candidate runtime replacement; not final visual lock.

## Runtime scope

The first-session `ChampionArena` branch now builds `FirstSessionAuthoredInnerRealm`
instead of `TEMPORARY_InnerRealmGreybox`. It keeps the reviewed spawn, movement,
proof-of-worth, guardian, quest, reward, save, and scene-flow systems unchanged while
replacing the player-facing presentation with admitted imported assets:

- modular Covenant threshold kit with a generated PBR flagstone surface;
- distinct rigged Meshy male/female champion bases with four material regions,
  four body-build blendshapes, equipment sockets, and embedded walk clips;
- rigged and textured Covenant sentinel guardian with a runtime walking clip;
- one generated premium capital-hall landmark per realm plus a typed 2:1 panoramic
  realm sky for distant mountain/forest/city/spire depth;
- existing live combat collider, boss AI, telegraphs, camera, weather, and HUD.

The same builder is data-driven by the canonical world-atlas layout and covers
Stonehold, Eldergrove, Crownlands, and Umbral. Realm identity is structural: each
branch loads its own Town Hall silhouette and realm/neutral materials rather than
only recolouring a shared primitive.

## Mobile envelope

The representative LOD0 view was measured in Unity `6000.3.22f1` after rendering two
angles per realm. Every branch is below the first-user 12,000 visible-triangle and
six-renderer envelope:

| Realm | Visible renderers | Visible triangles | Shared materials |
|---|---:|---:|---:|
| Stonehold | 4 | 10,401 | 2 |
| Eldergrove | 4 | 9,990 | 2 |
| Crownlands | 4 | 10,005 | 2 |
| Umbral | 4 | 10,039 | 2 |

The visible composition is the compact three-tile Covenant threshold plus one premium
realm landmark; disabled modular blockout descendants are excluded. Each Town Hall
retains four LOD levels and its single 1024x1024 atlas. Champion, guardian, hall, and
floor packets import normal maps as normal data, metallic/roughness as linear data,
and feed derived metallic-RGB/smoothness-alpha maps to Unity's Standard shader.

Raw metrics and eight 1280x720 captures are generated under
`Logs/FirstSessionAuthoredEvidence/` by
`AL.Editor.FirstSessionAuthoredEvidenceCapture.CaptureForCli`. The captures are
execution evidence and remain untracked.

## Provenance

The original generation records remain in `ArtSource/FirstUserOnboarding/README.md`.
The runtime guardian uses Meshy source task
`01a02241-4643-75f5-bf6a-318e9b436313` plus rigging task
`01a03125-7885-70bd-9af0-0dec1e975963` (Meshy 6, five credits). Runtime assets are
admitted by the generated `Resources/FirstSessionAuthoredAssetCatalog.asset`; no
asset path lookup is performed during play.

## Verification

- focused authored-world and first-session EditMode contracts;
- existing champion-presentation and canonical inner-realm spawn regressions;
- exact isolated writable first-user PlayMode journey through realm selection,
  character creation, username, authored 3D landing, movement, guardian combat,
  reward, save, and kingdom lockout;
- two rendered angles for each realm, inspected for orientation, grounding,
  silhouette, material assignment, and obvious primitive/deformation regressions.

Final authoritative results:

| Evidence | Result | SHA-256 |
|---|---:|---|
| World EditMode | 92/92 | `ffec439dd4d7679125c8828f96c8f3223b766ed1bd4cf685a2825c1370a5db9f` |
| CharacterCreation EditMode | 25/25 | `e830769cb8470622ac82f08e8511c5a80dfed60335cc46347b37965ac859f743` |
| Strict save semantic validation | 82/82 | `ce4c8cab1d909543cbec14f086a25f9daf58888c71676be77b98b23ed88e80cb` |
| Integrated writable first-user PlayMode journey | 1/1 | `a5c101c9c9088ab5df5984953a1d8453ac5b165e09934e8c9fccb0003e8d40e3` |
| Final four-realm contact sheet | accepted as visual3D MVP candidate | `d0dee3d5d97c80592ff6df7703316dcd1cd6b12dc7fd5546a22897b98d17fa9e` |
| Clean D3D11 evidence capture log | completed without prior warning/crash signatures | `ee9af555ace92725234613f6f4ba7938b363126bff5931964c03ae0794399da3` |

The final PlayMode and D3D11 capture logs contain none of the previously observed
particle-curve, PlayableGraph leak, compiler, null/missing-component, unhandled-log,
or native-crash signatures.

The first attempted PlayMode command named the wrong assembly and executed zero
tests; it was rejected as a false green. The accepted evidence uses
`AL.Development.FirstUserGameTest.PlayModeTests` and a non-zero test count in XML.
The first `-nographics` evidence render also crashed in `Camera.Render`; captures were
rerun successfully with the graphics device enabled.

## Honest remaining gaps

This change removes the player-facing mannequin/cylinder/temporary-plaque path and
establishes a coherent authored MVP presentation, but it is not the final premium
BDO-quality visual target:

- the Covenant hall is still a compact modular encounter room, not a fully dressed
  capital district;
- premium halls and panoramic skies provide one strong landmark and distant realm
  identity per branch; immediate streets, vegetation, crowds, props, and traversal
  dressing remain sparse;
- the champion has distinct male/female bases, saved creator selection, per-region
  color controls, build blendshapes, sockets, and stable locomotion, but not final
  class-specific armour/hair variants, facial morph depth, cloth simulation, or a
  bespoke animation set;
- the guardian has a real rig, full texture set, locomotion, and live telegraphs, but
  still needs final attack/reaction/death animation clips and animation blending;
- lighting now includes realm-authored panoramic skies and fog while remaining
  mobile-safe; reflection probes, baked GI, weather/VFX layering, and final
  post-processing remain;
- capture framing now includes one wide and one medium combat-readability view per
  realm, but remains review evidence rather than final cinematic composition.

The visual3D slice is accepted as a non-greybox MVP candidate with these explicit
revision items; it is not final whole-game visual approval. Final approval must evaluate
the device build and a human playtest rather than the evidence captures alone.
