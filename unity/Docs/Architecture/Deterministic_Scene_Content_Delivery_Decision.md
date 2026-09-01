# Deterministic Scene Inventory and Content Delivery Decision

Decision ID: `DEC-SCENE-DELIVERY-001`
Status: approved and implemented
Owner approval: `APPROVE DEC-SCENE-DELIVERY-001 HYBRID-LOCAL-ADDRESSABLES`
Approval date: 2026-09-01
Unity authority: `6000.3.22f1`
Addressables authority: `com.unity.addressables` `2.9.1`

## Decision

AnotherLife uses a hybrid local delivery model:

1. The direct Player shell is exactly these five enabled Build Settings scenes, in this order:
   `Boot`, `RealmSelection`, `CharacterCreation`, `ChampionArena`, `Kingdom`.
2. All 78 scenes named by `al_world_streaming_catalog.json` are local Addressable scenes and remain outside Build Settings.
3. Generated scenes are grouped one bundle group per catalog world instance. Each group uses `Local.BuildPath` and `Local.LoadPath`; each address is `scene/<worldId>/<chunkId>`.
4. The remaining 20 known scenes are non-shipping review, prototype, legacy, or representative-test assets. They remain outside Build Settings and Addressables.
5. Remote catalogs, CDN delivery, startup catalog-update checks, and content-only update schemas are disabled. Enabling any of them requires a separate owner-approved decision and a rollback-tested delivery plan.

The committed machine-readable authorities are:

- `Assets/AL/StreamingAssets/GameData/al_enabled_scene_manifest.v1.json`
- `Assets/AL/StreamingAssets/GameData/al_generated_scene_manifest.v1.json`

Together they account for all 103 known `.unity` assets. Every record includes purpose, reachability, ownership, shipping status, GUID, scene/meta hashes, and a deterministic dependency projection. Generated records additionally include dimension, world, catalog access policy, neighbor reachability, Addressables group, and address.

## Options evaluated

### All scenes directly included

Rejected. It would put 83 shipping scenes into Build Settings, couple boot order to streamed content, and provide no owned load/release handle for chunk residency.

### Raw AssetBundles

Rejected. It would require a custom dependency graph, reference counting, release policy, catalog, and update path. Unity warns that direct bundle workflows require explicit dependency handling and can duplicate dependencies.

### All scenes in Addressables

Rejected. Moving the five-scene safe shell into Addressables would make startup depend on content-system initialization and add a failure mode before the recovery UI is available.

### Hybrid local Addressables

Approved. The five-scene shell remains directly packageable while generated chunks gain asynchronous scene handles, symmetric unload/release semantics, deterministic world ownership, and a later reversible path to remote delivery without enabling it now.

## Determinism and validation

Run from the repository root:

```text
python tools/scenes/scene_content_manifest.py --check
```

Generation is intentionally separate and owner-gated:

```text
python tools/scenes/scene_content_manifest.py --generate
```

Normal validation never rewrites a manifest. It fails nonzero for:

- a missing required direct or generated scene;
- an unexpected/disabled/reordered Build Settings scene;
- a duplicate path, GUID, scene ID, chunk ID, or address;
- catalog versus disk scene drift;
- non-canonical JSON or generated-scene ordering;
- scene, meta, dependency, catalog, package-lock, group, address, or hash drift;
- any unaccounted `.unity` addition or removal;
- a wrong Unity or Addressables version;
- remote catalogs, catalog update checks, content-update schemas, or Addressables membership drift.

Canonical manifests use UTF-8, LF, stable key order, ordinal path/ID ordering, no timestamps, and SHA-256 fingerprints. Two runs over identical inputs must be byte-identical.

Unity-side authoring and validation are exposed by:

- `AL.Editor.World.SceneContentDeliveryConfigurator.ConfigureAndValidateForBatch`
- `AL.Editor.World.SceneContentDeliveryConfigurator.BuildApprovedLocalContentForBatch`

The EditMode contract verifies 11 local world groups, 78 exact entries, no unexpected entries, local paths only, and remote catalogs disabled.

## Reopen path

Any newly discovered `.unity` file or catalog chunk returns `SCENE_SET_REVIEW_REQUIRED` and is stop-ship. Do not run `--generate` merely to make validation green.

Reopen this decision by recording:

1. the added/removed scene identity, purpose, owner, reachability, and intended shipping status;
2. whether direct Build Settings order, a world group, or delivery topology changes;
3. dependency and install-size impact;
4. rollback behavior and any remote/content-update implications;
5. a replacement decision ID or explicit owner approval that supersedes `DEC-SCENE-DELIVERY-001`.

After approval, update catalog/configuration and regenerate both manifests in the same reviewed change. Without that approval, the validator must continue to fail closed.

## Known authority discrepancy

The source world catalog currently marks the four dragon-cave worlds `realm_members`, while owner direction says dragon caves are public/unrestricted. Generated records preserve the source value and set `policyDiscrepancy` to `approved_public_unrestricted_intent_differs_from_catalog`; this decision does not silently rewrite gameplay access authority. Correcting that catalog policy requires its own authorized change.

## References

- Unity 6.3 Addressables package: https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.addressables.html
- Addressables scene handles: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/LoadingScenes.html
- Deterministic AssetBundle/Addressables builds: https://docs.unity3d.com/Manual/build-deterministic-assetbundles-addressables.html
