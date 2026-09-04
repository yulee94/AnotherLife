# Realm Creature Production Source V001

This tracked packet promotes the owner-approved 2D source identities and the selected 3D review/cleanup sources for four realm dragons, Vaeloryn, four realm bosses, and twelve elites.

- Approved 2D entries: **21 / 21**
- Structural 3D passes: **21 / 21**
- Remaining structural/UV blockers: **0 / 21**
- Owner-tier 8K/4K texture packets: **3 / 21**
- Below owner-tier or texture-rebuild packets: **18 / 21**
- Runtime integration: **Blocked**
- Rigging: **three source-qualification slices** — Fault-Crowned Colossus V001, Cindermaw Salamander V001, and Oreblind Delver V001 under `ProductionSlices/` (runtime still blocked)
- Cindermaw runtime benchmark: source-qualified only. Unity PBR import, device evidence, VFX playback, gameplay/spawn, and `productionReady` remain blocked. Ember/heat/steam stay on sockets, not the clean mesh.
- Runtime VFX: **not delivered and must remain separate**
- Exact Cindermaw runtime blockers: `ProductionSlices/CindermawSalamanderV001/cindermaw_salamander_runtime_blockers_v001.json`
- Exact Oreblind Delver runtime blockers: `ProductionSlices/OreblindDelverV001/oreblind_delver_runtime_blockers_v001.json`

The packet is source/review authority only. It creates no spawn, combat, reward, save, narrative, or runtime catalog authority. Mere-Root and Crownstep use replacement geometry and intentionally do not inherit incompatible atlases. Cindermaw now binds its triangulated, non-overlapping UV atlas to a v005 localized visual-polish source (snout offsets plus material-separated soot hide, obsidian fins, pale scars, and ash-paste underside) while preserving the hash-bound v004 topology and authored 4K tangent normal; v004 remains immutable evidence. Eighteen packets remain below the owner texture tier or need texture rebuilding. Runtime textures under `runtime_2k` are derived convenience packets, not permission to integrate models before texture, LOD, rig, animation, device, and failure-path gates pass.

All packet media is outside `unity/Assets`, so Unity `.meta` files and importer settings are intentionally absent. Runtime import is a separate engineering change.

See `realm_creature_2d_approval_manifest_v002.json`, `realm_creature_3d_source_manifest_v001.json`, and `DCCReports/` for immutable hashes, repair evidence, and status. Editable Blender repairs live under `unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/DCC/`.
