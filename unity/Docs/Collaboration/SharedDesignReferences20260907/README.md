# Shared design references — 2026-09-07

This handoff publishes previously local 2D assets and design references for co-development. It does not integrate or qualify a playable world, change saves, or promote runtime assets.

## Get the actual images

From a checkout containing this PR (or main after merge):

```sh
git lfs install
git lfs pull --include="unity/ArtSource/Environment/**,unity/Docs/World/Map/**,unity/Docs/Architecture/**"
python unity/Docs/Collaboration/SharedDesignReferences20260907/verify.py
```

Open the HTML review files locally after fetching LFS. GitHub displays HTML source rather than the interactive review. The PNG/JPG originals and authoring images are retained alongside manifests.

## Start here

- [Owner-approved V013 world map](../../World/Map/AnotherLife_Full_World_TopDown_Map_V013.html) and [approval](../../World/Map/AnotherLife_Full_World_TopDown_Map_V013_APPROVAL.md).
- [Owner source sketches and custody](../../World/Map/References/Owner_World_Map_Sources.json).
- [World 2D-to-3D review](../../Environment/World/World_2D_to_3D_Production_Review_V001.html).
- [Stonehold modular architecture](../../Architecture/Stonehold_Modular_Architecture_Contact_Sheet_V002.html) and [owner approval](../../Architecture/Stonehold_Modular_Architecture_Contact_Sheet_V002_APPROVAL.md).
- [Shared P0 concepts](../../Environment/World/World_Concept_Gap_P0_Packet_V001.html).
- [Stonehold P1](../../Environment/World/Stonehold_Concept_P1_Packet_V001.html), [Stonehold P2](../../Environment/World/Stonehold_Concept_Gap_P2_Packet_V001.html).
- [Crownlands P1](../../Environment/World/Crownlands_Concept_P1_Packet_V001.html), [Crownlands P2](../../Environment/World/Crownlands_Concept_Gap_P2_Packet_V001.html).
- [Eldergrove P1](../../Environment/World/Eldergrove_Concept_P1_Packet_V001.html), [Eldergrove P2](../../Environment/World/Eldergrove_Concept_Gap_P2_Packet_V001.html).
- [Umbral P1](../../Environment/World/Umbral_Concept_P1_Packet_V001.html).
- [Event-only Accordant P1](../../Environment/World/Accordant_Concept_Gap_P1_Packet_V001.html).

## Authority and limitations

- V013 is the approved map. V001–V012 are retained historical iterations, not competing authorities.
- These are historical source packets. Their original directing-model and image-renderer provenance is preserved. Old generator scripts and fallback policies are not authorization to use retired providers or spend credits. Current owner instructions govern any new generation.
- Source approval and packet-validator success are not 3D, rig, traversal, Unity, performance, or release acceptance. Existing conditional statuses remain conditional.
- Any `rejected/` image or retry record is retained only for custody and comparison; it is not approved source authority.
- Authoring upscales are not native 8K detail. Use the recorded native-resolution originals and provenance.
- This PR adds no runtime assets, gameplay scripts, scenes, catalog mutations, or save changes. Rollback is deletion/reversion of this additive reference collection.
- Changing Stonehold DCC outputs, rejected rig/proxy experiments, cinematic frame dumps, machine-local caches, credentials, and obsolete branch implementations are intentionally excluded.
- Existing creature production sources, approved asset-library packets, and Shot070 sources already published on main remain available in their existing locations; this handoff does not overwrite them with older local copies.

`inventory.json` pins every shared source file. `validation.json` records the source checks executed for this handoff; it does not claim a fresh independent visual review. The verifier rejects missing files, altered bytes, unresolved LFS pointers, duplicate paths, and paths escaping this repository.
