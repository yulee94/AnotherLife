# Realm Creature Production Source V001

This tracked packet promotes the owner-approved 2D source identities and the selected 3D review/cleanup sources for four realm dragons, Vaeloryn, four realm bosses, and twelve elites.

- Approved 2D entries: **21 / 21**
- Structural 3D passes: **21 / 21**
- Remaining structural/UV blockers: **0 / 21**
- Owner-tier 8K/4K texture packets: **2 / 21**
- Below owner-tier or texture-rebuild packets: **19 / 21**
- Runtime integration: **Blocked**
- Rigging: **not delivered**
- Runtime VFX: **not delivered and must remain separate**

The packet is source/review authority only. It creates no spawn, combat, reward, save, narrative, or runtime catalog authority. Mere-Root and Crownstep use replacement geometry and intentionally do not inherit incompatible atlases. Cindermaw now has a triangulated, non-overlapping UV atlas plus rebaked 8K base and 4K support maps, but its rejected ray-baked normal was replaced with a neutral 4K tangent fallback; authored normal microdetail remains a texture-rebuild task. Nineteen packets therefore remain below the owner texture tier or need texture rebuilding. Runtime textures under `runtime_2k` are derived convenience packets, not permission to integrate models before texture, LOD, rig, animation, device, and failure-path gates pass.

All packet media is outside `unity/Assets`, so Unity `.meta` files and importer settings are intentionally absent. Runtime import is a separate engineering change.

See `realm_creature_2d_approval_manifest_v002.json`, `realm_creature_3d_source_manifest_v001.json`, and `DCCReports/` for immutable hashes, repair evidence, and status. Editable Blender repairs live under `unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/DCC/`.
