# AnotherLife Blender Rig Cleanup Pipeline v1

Status: bounded engineering pipeline; not a production-admission or creative-approval artifact.

## Scope

This pipeline deterministically cleans, preflights, and exports one representative from each required family:

| Family | Pipeline asset | Source | Rights gate |
|---|---|---|---|
| Champion | `rmc_cleanup_champion_vanguard_v002` | `unity/ArtSource/Champions/champion_vanguard_working_v001.blend` | Project-authored source retained; creative-final approval still required. |
| NPC | `rmc_cleanup_npc_covenant_sentinel_v003` | `unity/Assets/AL/Art/Production/FirstUserOnboarding/Enemies/Covenant_Sentinel_Meshy6_Walking_v002.fbx` | Source-specific Meshy task/account/license record is missing; production promotion is blocked. |
| Fantasy beast | `rmc_cleanup_beast_slagwhistle_v002` | `unity/ArtSource/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend` | Authorized bounded source candidate with visual lineage; production-rights and owner-approval gates remain. |

The authority order is:

1. `AnotherLife_Blender_Asset_Production_Contract.md`
2. `AnotherLife_Rig_Motion_Catalog_Production_Standard_v1.md`
3. `al_rig_motion_standard.json`
4. `al_required_motion_manifest.json`
5. `al_rig_cleanup_manifest.v1.json`

The pipeline never promotes a representative into an approved runtime catalog. Every generated sidecar sets `productionEligible` to `false` and retains explicit `productionGaps`.

## Entry point

Run from the repository root:

```text
python tools/blender/run_al_rig_pipeline.py validate
python tools/blender/run_al_rig_pipeline.py build
python tools/blender/run_al_rig_pipeline.py preflight
python tools/blender/run_al_rig_pipeline.py export
python tools/blender/run_al_rig_pipeline.py repeatability
```

Use `--asset <pipeline-asset-id>` to process one representative. Set `AL_BLENDER_EXECUTABLE` when Blender 5.2 is not at the default Windows location.

The launcher always invokes Blender with `--python-exit-code 2`. Any missing input, hash mismatch, schema error, unsupported animation shape, cleanup regression, preflight error, export failure, or round-trip mismatch returns nonzero.

## Manifest and provenance

- Pipeline manifest: `unity/ArtSource/RigPipeline/al_rig_cleanup_manifest.v1.json`
- JSON Schema: `unity/SharedContracts/Schemas/al-rig-cleanup-pipeline.schema.json`
- Source ledger: `unity/ArtSource/RigPipeline/al_rig_source_provenance.v1.json`
- Pure-Python contract validator: `tools/blender/al_rig_pipeline_contract.py`

Source paths and SHA-256 values are pinned before Blender opens a file. The contract rejects path escapes, output/source aliasing, unknown catalog bindings, duplicate bone targets, missing rights evidence, invalid production claims, and representative-family duplication.

## Cleanup sequence

For each asset, Blender 5.2 performs these fail-closed steps:

1. Verify the pinned source hash and provenance binding.
2. Load the `.blend` or import the `.fbx`.
3. Resolve the declared armature and mesh set; reject missing or ambiguous objects.
4. Remove undeclared scene objects without deleting selected children.
5. Remove only constant object-transform animation curves, scale pose translations when applying uniform source scale, apply world transforms, and ground the rest mesh at Z=0.
6. Rename every declared bone and matching vertex group through collision-safe temporary names.
7. Insert non-deforming `root` and `motion_root`; apply hierarchy overrides; add non-deforming sockets.
8. Normalize object/data names and isolate the representative in one collection.
9. For animated sources, sample every integer frame and rebuild one canonical quaternion action in stable bone/frame order.
10. Compare source and normalized evaluated geometry at every sampled frame; reject drift over the manifest tolerance.
11. Clean, limit to four, and normalize deform weights; resample every validation frame; reject unweighted or non-normalized vertices and cleanup-induced deformation above the asset's declared bounded tolerance.
12. Triangulate and remove degenerate or exact duplicate triangles.
13. Run the contract preflight and save the cleaned `.blend` plus `.rig.json` sidecar.
14. Export with the pinned Unity FBX preset, reimport into a factory-empty Blender scene, and compare skeleton names/hierarchy, mesh/triangle counts, bounds, and action presence.

## Determinism policy

The manifest states the determinism level explicitly:

- Cleaned Blender files use `semantic_content_signature_v1`. Blender can serialize session-internal IDs differently when an FBX source is reconstructed, so raw `.blend` byte hashes are not treated as semantic identity.
- Static FBX exports use `byte_exact_sha256_v1`. Repeatability checks require identical FBX bytes.
- Animated FBX exports use `semantic_roundtrip_receipt_v1`. Blender's binary FBX exporter can vary internal IDs and sub-tolerance sampled floats between processes. The pipeline instead requires an identical canonical action signature, blend content signature, preflight report, export preset, and successful round-trip receipt while recording the actual FBX SHA-256 for each run.
- Animated float tolerance is `0.00001`.

The exporter also fixes FBX creation time and attempts stable SHA-256-derived IDs. Those measures make static exports byte-exact but are not overstated as a guarantee for animated Blender FBX output.

## Generated evidence

Clean representatives and sidecars:

- `unity/ArtSource/RigPipeline/Representatives/champion_vanguard_rig_clean_v002.blend`
- `unity/ArtSource/RigPipeline/Representatives/champion_vanguard_rig_clean_v002.rig.json`
- `unity/ArtSource/RigPipeline/Representatives/covenant_sentinel_rig_clean_v003.blend`
- `unity/ArtSource/RigPipeline/Representatives/covenant_sentinel_rig_clean_v003.rig.json`
- `unity/ArtSource/RigPipeline/Representatives/slagwhistle_burrower_rig_clean_v002.blend`
- `unity/ArtSource/RigPipeline/Representatives/slagwhistle_burrower_rig_clean_v002.rig.json`

FBX exports and round-trip receipts are in `unity/ArtSource/RigPipeline/Exports/`.

The latest verified skeleton signatures are:

- Champion: `9941de0420ff53f714be79db199f6b924d608c6991ed66f6443282a6a5585757`
- NPC: `cbe1c6ad6f0395cdf93522e18a66680214c5f9427965f14e3dad9eb9b804d973`
- Fantasy beast: `6538a26bfca72eb8a8c8acd485e2f6474abf7f44f3137baf3184ca1c541b8338`

The latest repeatability run reported stable content/receipt signatures for all three representatives, byte-exact static FBX hashes, and a stable semantic animated-FBX signature for the NPC.

## Intentional non-production gaps

This work does not claim:

- complete required motion-set coverage,
- NPC walk contact/loop qualification,
- owner review of the NPC's measured 32.274 mm maximum four-influence weight-reduction deformation delta (bounded to 35 mm for this technical candidate),
- champion or NPC facial deformation completion,
- fantasy-beast LOD1/LOD2/impostor completion,
- fantasy-beast six-slot motion completion,
- owner creative approval,
- production-rights clearance where the provenance ledger says it is unresolved.

Closing any of those gates requires a separately reviewed production task and updated authoritative catalog records; rerunning cleanup alone cannot clear them.
