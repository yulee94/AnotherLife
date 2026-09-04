# Shot070 Vaeloryn motion-source packet V002

This packet qualifies the approved Wish Dragon / Vaeloryn production source for the
`CTMA-BEAT-07` `Shot070` interval `[1080,1248)` at 24 fps. It is a reusable motion-review
candidate, not runtime/gameplay authority and not final cinematic approval.

## Authority and provenance

- Approved 2D identity sources are the two Vaeloryn multiview sheets bound by exact SHA-256
  values in `shot070_vaeloryn_source_manifest_v002.json`.
- Candidate input is the source-controlled Meshy result
  `wish_dragon_vaeloryn_source_v001.fbx`, task
  `01a05b2c-92c6-7329-939f-a538fdaa859b`, SHA-256
  `80bcc74a2cf95cb2626437bba3d3ba805d6087f1498e64b1603cb256f43e68cb`.
- `wish_dragon_review_master.glb` is explicitly rejected and input-ineligible. Its locked
  digest is `5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270`;
  the validator rejects any attempted reuse.
- Construction used only repository-local source plus Blender 5.2 LTS and FFmpeg. Incremental
  spend and paid-provider calls are both zero.

## Candidate

- `unity/ArtSource/Cinematics/Shot070VaelorynSourceV002/shot070_vaeloryn_motion_source_v002.blend`
  is the editable DCC and topology authority. Textures are packed.
- `unity/ArtSource/Cinematics/Shot070VaelorynSourceV002/shot070_vaeloryn_motion_source_v002.glb`
  is the self-contained interchange/review copy with its skin and animation.
- One source mesh was cleaned from two connected components to one. The 15-vertex debris
  component was removed. The authoring mesh has 31,857 vertices, 63,708 triangles, zero
  boundary edges, and two bounded over-connected seam edges (down from 29 non-manifold source
  edges). The fail-closed contract permits at most four over-connected edges and never permits
  an open boundary.
- Ten semantic anatomy regions and four independent material regions make the head, jaw,
  eyes, wing arms, wing membranes, body, and tail auditable without claiming separate mesh
  objects.
- The armature contains 25 deform bones plus a non-deforming root. Skinning has at most two
  deform influences per vertex and zero unweighted vertices.
- `Shot070_Vaeloryn_Articulation_v002` spans 168 frames / 7 seconds. It animates both wing
  roots, neck, jaw, tail, body translation, and the review camera.

The GLB importer may expand vertices at UV/material/normal seams and emits a 42-vertex
`Icosphere` exporter helper. Those importer artifacts are not topology authority; the Blender
source and `rig_articulation_report_v002.json` are.

## Evidence

- `shot070_vaeloryn_frame_16x9_v002.png`: native 1920x1080 textured framing proof.
- `shot070_vaeloryn_frame_9x16_v002.png`: native 1080x1920 textured framing proof.
- `shot070_vaeloryn_motion_review_v002.mp4`: 960x540 H.264, 24 fps, exactly 168 frames.
- `shot070_vaeloryn_motion_contact_v002.png`: five sampled motion poses.
- `source_audit_v002.json`: immutable-source geometry/material/rig audit.
- `rig_articulation_report_v002.json`: candidate geometry, semantic, skin, rig, clip, and
  authority audit.

Visual inspection confirms a single crowned head, four legs, one wing pair, long tail,
visible material hierarchy, complete portrait framing, and usable landscape/motion framing.
The motion sheet shows distinct opposing wing/body poses rather than repeated stills.
Runtime VFX (eight Gems, portal, wish-space, atmospheric magic, attacks) remain separate from
the clean beast source.

## Reproduce and validate

Run from the repository root:

```text
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python-exit-code 1 --python tools/cinematics/build_shot070_vaeloryn_candidate.py -- audit
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python-exit-code 1 --python tools/cinematics/build_shot070_vaeloryn_candidate.py -- build
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python-exit-code 1 --python tools/cinematics/build_shot070_vaeloryn_candidate.py -- verify
python tools/cinematics/validate_shot070_vaeloryn_source.py
python tools/cinematics/test_validate_shot070_vaeloryn_source.py
```

Any artifact hash mismatch, rejected-source reuse, missing native framing, fake/static motion,
wrong anatomy count, insufficient semantic/material separation, unweighted skin, unsafe path,
paid call, or authority leak fails validation.
