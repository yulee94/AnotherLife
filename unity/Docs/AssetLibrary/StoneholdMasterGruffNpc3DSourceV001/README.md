# Stonehold Master Gruff 3D NPC foundation V001

Status: identity sculpt + AL_MasterRig candidate. Not production-ready. No runtime, terrain, or release authority.

## What landed

- Catalog ID `rct_stonehold_npc_service_v001` bound to parent 2D packet PR #731 (`ce25ef09`).
- Four isolated A-pose views hash-verified and Meshy-7 multi-image generated (`01a06a80-bfb7-7251-9b95-209e7e9e67b0`, 30 credits).
- Remesh (`01a06a89-80f4-7317-981f-7fc07bcc2f2f`, 5 credits) selected as the identity sculpt after visual inspect. Raw 116k GLB is preserved.
- Planted to 1.43 m, A-pose 22-bone `AL_MasterRig` plus beard/apron fallback bones, compound colliders, sockets, blink/viseme shape keys, LOD1/LOD2.
- Review renders are of the actual exported mesh.

## Remainders (fail-closed)

- Clothing/body/hair/apron/tools are still one fused identity mesh. Spatial cuts and cage-shrinkwrap both destroyed the sculpt; they were rejected.
- Gameplay-distance identity reads as Master Gruff. Face close-ups still show Meshy UV islands.
- LOD0 is 46786 triangles, 1786 over the 45k important-NPC planning ceiling.
- No Unity prefab was authored in-Editor this run.
- Auto-weights are first-pass. LeftUpperArm pose-test moves the arm; shoulder volume squash is not production paint.

Do not copy this fused mesh as the Eldergrove pattern. Downstream realm NPCs should reuse the bone names, A-pose, sockets, and validator, not the garment topology.
