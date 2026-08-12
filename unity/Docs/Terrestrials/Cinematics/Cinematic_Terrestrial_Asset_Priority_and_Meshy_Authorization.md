# Cinematic Terrestrial Asset Priority and Meshy Authorization

> Status: `DRAFT_COORDINATION_REVIEW_ONLY`
> Verified baseline: `main@6102638a41e1d944e267801df4cb22bbbd0af5eb`
> Primary delivery mode: Codex coordination/review
> Upstream: issues `#460`, `#284`, and `#259`; issue `#456` is referenced only to prevent guardian-dragon source transfer; draft PR `#472` is historical sanitized retention evidence
> Authority: A1 sequencing and per-operation paid-call authorization; authorized co-developer A2 terrestrial source selection/fidelity; user final creative, visual, integrated-cinematic, and release approval
> Operational venue: dedicated Meshy task `019fef94-af14-7671-a739-447391cfb7a5` exclusively
> Scope: this Markdown file only; no Unity/runtime/source-asset change; no shared-file lock

## Decision

Meshy is the project's designated execution tool for generated 3D models. That designation is not blanket permission to generate, a creative or source-selection authority, a production acceptance event, or a requirement to route fundamentally VFX, shader, material, audio, 2D, or compositing work through Meshy.

Every future Meshy creation, refinement, conversion, rigging, animation, remesh, or texture operation requires a new task-specific A1 authorization binding the exact A2-approved source, rights evidence, input hash, endpoint, payload, attempt count, maximum credit ceiling, exact cinematic dependency, and named future gameplay reuse binding. Authorization, execution, polling, downloads, QA, retention, and credit reporting may occur only in dedicated Meshy task `019fef94-af14-7671-a739-447391cfb7a5`. This document and every other task or PR are coordination/evidence consumers and may not invoke Meshy. The co-developer owns terrestrial source selection and fidelity. The user retains final creative and visual selection and integrated-cinematic approval. A successful provider task advances none of those gates automatically.

The spending policy is spend-as-work-becomes-ready: credits are used only for ready, individually authorized work. A displayed or remaining balance is evidence, not a spending target or automatic next-batch authorization. Each paid operation still requires an exact one-shot A1 decision and stop rule. Billing changes remain prohibited; no recharge, purchase, renewal, payment-method, or subscription mutation is authorized.

The priority order in this document is scheduling guidance only. It is not A2 source selection, user approval, Meshy permission, production acceptance, cinematic selection, gameplay authority, runtime integration, purchasing approval, or release approval. GitHub visibility to `@rslee94` is sufficient; a co-developer review is welcome but is not a mandatory readiness gate. No agent may self-approve.

### Historical opening snapshot

The following values describe the original PR-head snapshot at `main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0`, before the later paid batch. They are retained only to explain the document's chronology and are not the current operational ledger.

| Historical control | Opening value |
| --- | --- |
| `activeMeshyAuthorizationCount` | `0` |
| `authorizedCreditCeiling` | `0` |
| `creditsConsumedAtOpeningSnapshot` | `0` |
| `generationState` | `Deferred` |
| `a2SourceDisposition` | `NotRequested` |
| `userCreativeState` | `NotRequested` |
| `cinematicUseState` | `Blocked` |
| `gameplayUseState` | `NotAuthorized` |
| `runtimeIntegrationState` | `Blocked` |
| `releaseState` | `Blocked` |

### Current operational snapshot

| Current control | Verified value |
| --- | --- |
| `verifiedMain` | `6102638a41e1d944e267801df4cb22bbbd0af5eb` |
| `exclusiveMeshyTask` | `019fef94-af14-7671-a739-447391cfb7a5` |
| `executedBatchId` | `priority_spend_20260812_v001` |
| `executedBatchCredits` | `7,675 - 1,014 = 6,661` |
| `executedBatchComposition` | `26 * (9 + 30) = 1,014` credits; equivalently `26 * 9 + 26 * 30 = 1,014` |
| `executedBatchState` | `ExecutedStopped`; no retry; `GeneratedUnreviewed / NON_PRODUCTION` |
| `currentFurtherPaidAuthorizationCount` | `0` |
| `currentFurtherPaidCreditCeiling` | `0` |
| `currentFurtherPaidState` | `NotAuthorized` |
| `wishDragonCurrentState` | `SOURCE_REQUIRED / Meshy NotAuthorized` |
| Approval effects | none: no A2 source, user creative, cinematic, gameplay, runtime, production, or release gate advanced |
| Billing changes | prohibited; no recharge, purchase, renewal, payment-method, or subscription mutation |

## Verified evidence baseline

### Hosted current-main facts

- The authorized base is exactly `main@6102638a41e1d944e267801df4cb22bbbd0af5eb`.
- Issue `#460` is the open coordination source for the proposed 60-second moving-3D launch baseline. Its historical title/body contains earlier no-spend framing; the later user policy permits spend-as-work-becomes-ready only after exact per-operation cost and stop-rule authorization. The issue remains `DRAFT / NOT APPROVED / NOT IN UNITY` and requires genuine moving 3D rather than still-image motion.
- Issue `#284` is an active engineering specification whose media-production lane remains blocked. It may define later playback, fallback, packaging, and device validation, but it does not manufacture or approve cinematic pixels.
- Issue `#259` preserves terrestrial source evidence and scalability direction. Merged source, reviewable sheets, and task success do not grant production, runtime, or user approval.

### Executed batch and retained Meshy evidence

- Dedicated task `019fef94-af14-7671-a739-447391cfb7a5` records batch `priority_spend_20260812_v001`: starting balance `7,675`, verified ending balance `6,661`, and exact spend `1,014` credits.
- The arithmetic is exact: `26` paired source-image and Meshy-6 PBR model tasks at `9 + 30` credits each gives `26 * (9 + 30) = 1,014` (equivalently `26 * 9 + 26 * 30 = 1,014`); `7,675 - 1,014 = 6,661`.
- The batch is terminal `ExecutedStopped`: no retry and no further paid operation is currently authorized. Every output remains `GeneratedUnreviewed / NON_PRODUCTION`; provider completion grants no A2 source, user creative, cinematic, gameplay, runtime, production, or release approval.
- Draft PR [#472](https://github.com/yulee94/AnotherLife/pull/472), head `216a1d272c7f6ff3dc35217bc88818524462d877`, is a separate sanitized historical retention cutoff. Its `7,675 -> 7,675` zero-credit snapshot predates the paid batch and must remain historical. It grants no promotion, spend, source, or runtime authority and is not the current balance ledger.

### Retained but non-canonical cinematic evidence

Issue `#460` records the local external-preproduction artifact `FIRST_RUN_CINEMATIC_MOTION_SHOT_PACKET_V001.md`:

- packet ID: `AL_FIRST_RUN_CINEMATIC_MOTION_SHOT_PACKET_V001`
- byte length: `21,698`
- SHA-256: `4d4f2975dd04a86f095d8b8481300ea66e14daa640e89382baa0059195bf967c`
- `DESIGN.md` v1.27 SHA-256: `7e4e908d1af44acc2bd853232d4c22cad76cf2fc9bea6484775cccb1786acee3`
- approved AL identity SHA-256: `db4fb6b262e537b76b57d855e1c46ce104520d3ef279b51f33717e085e5452ed`

### Known hosted motion and sequence records

The hosted record does not provide eight approved canonical shot IDs. Do not synthesize them. Known labels and hashes are evidence only:

- `AL_FR_MOTION_SRC_010_MATERIAL_AWAKEN_V001`: 5.5 seconds, 960x540 H.264 MP4, 24 fps, 132 frames, SHA-256 `23648d2be1021aada4e3ecc371b2cdeb647595ad675ec9eac112d5c523a79b9a`; technical pilot only.
- Known labels `Shot010`, `Shot030`, and `Shot070` are not an approved eight-shot identity set. Hosted `Shot030` has canonical planning range `[0312,0504)` and is a nonconforming camera/party reference; `Shot070` is rigid staging. Neither is approved film or source.
- Planning EDL: 1,440 frames at 24 fps, SHA-256 `370ef3936164de570641bad2f36d54cacafc502ae41e228b0d955b40027f895e`.
- Dependency ledger SHA-256: `d331d06d6f4f651b3e168cc4337e29bfba949c4f80b0f9adce32d565dafefaeb`.
- Approved production moving clips: `0`.
- The later A1 camera contract includes one continuous 288-frame/12-second 360-degree frozen-time orbit at 30 degrees per second plus bounded slow motion. Its exact active shot ID, realm, source, and asset mapping remain `ID_REQUIRED / SOURCE_REQUIRED`.

Unity owns later deterministic staging, render, and encode. The historical optional comparison lane is non-controlling; GitHub visibility to the co-developer remains sufficient and no personal spending or review requirement is inferred.

The local commit `b33d0e65c9d5e54d9330eaa14fd5be9bcdefabbf` and the following four paths are retained unmerged coordination evidence, not files in the verified current-main baseline:

- `unity/Docs/Launch_Cinematic_Production_Spec.md`
- `unity/Docs/Launch_Cinematic_Shot_Manifest.json`
- `unity/Docs/Launch_Cinematic_Media_Manifest.json`
- `unity/Docs/Launch_Cinematic_Asset_Audit.md`

Any issue prose calling those paths authoritative is not represented by `main@426316d...` and must not be treated as a current-main source binding. Their local existence cannot authorize input reuse, Meshy credits, source selection, media production, or runtime packaging.

## Sixty-second cinematic dependency order

The IDs below are document-local coordination IDs, not canonical shot IDs. The exact beat timing and meaning come from issue `#460`.

| Priority | Coordination beat | Time | Locked issue contract | Terrestrial/environment dependency | Current disposition |
| --- | --- | ---: | --- | --- | --- |
| P0 | `CTMA-BEAT-01` | 00.000–05.500 | Real 3D AL material awakening | Brand/material lane; no terrestrial Meshy input is established | `SOURCE_REQUIRED`; no Meshy task |
| P0 | `CTMA-BEAT-02` | 05.500–13.000 | Campaign-scale four-realm environment promise | Shot-critical terrain, trees/burnt trees, rocks/ores, vegetation, clouds/weather, sunlight, and any source-approved lava presentation | Highest environment-source dependency; all exact realm/habitat/shot selections are `SOURCE_REQUIRED` |
| P0 | `CTMA-BEAT-03` | 13.000–21.000 | Exactly five anonymous adults; one central non-semantic caster; combat/impact | Ground/rock/vegetation/weather dressing; character, combat, and VFX ownership remains separate | Environment source `SOURCE_REQUIRED`; no creature or effect inferred |
| P1 | `CTMA-BEAT-04` | 21.000–29.000 | Controlled intact sole main-gate threshold | Rocks, ore-bearing masonry only if sourced, vegetation, weather, and lighting around an architecture-owned gate | `SOURCE_REQUIRED`; architecture is not terrestrial-source authority |
| P1 | `CTMA-BEAT-05` | 29.000–37.000 | Two geographically separated bridges for one adjacent realm pair; solid-body blocking; no victor | Realm geology, trees/grass/flowers where visible, clouds/rain/light, and bridge-adjacent terrain | `SOURCE_REQUIRED`; bridge and conflict meaning remain separate |
| P1 | `CTMA-BEAT-06` | 37.000–45.000 | Stronghold conflict; continuous wall/one gate; no capture outcome | Rocks, burnt trees if habitat-authorized, vegetation, clouds/rain/thunder/light, and lava only if the chosen habitat explicitly contains it | `SOURCE_REQUIRED`; no generic Umbral/volcanic substitution |
| P0 | `CTMA-BEAT-07` | 45.000–52.000 | All eight approved Gem identities/signatures approach source-approved Wish Dragon presence | Hero Wish Dragon source/rig/motion plus source-approved environment and effects | Critical hero-3D blocker; exact visual source, retained input, rights, rig, and motion are `SOURCE_REQUIRED` |
| P2 | `CTMA-BEAT-08` | 52.000–60.000 | Exact approved AL mark plus provisional MAKE THE WISH only | Editorial/brand lane; no terrestrial Meshy asset required | No Meshy task |

Target timing remains exactly `60.000` seconds, `1,440` frames, and `24 fps`. This document does not approve a cut, camera, clip, encode, or shot asset.

## Exact realm and source routing anchors

The current realm catalog is `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json`, Git blob `3de7ea95fb9cd49b30b129b0eb3b46ba156ac9f9`. These IDs constrain identity; they do not select visual source:

| Realm | Inner / warzone / gate | Unresolved realm-dragon reference | Exact Gem IDs |
| --- | --- | --- | --- |
| `crownlands` | `inner_crownlands` / `warzone_crownlands` / `gate_crownlands_meridian` | `dragon_crownlands_dawn_regent` | `gem_crownlands_sun`, `gem_crownlands_oath` |
| `stonehold` | `inner_stonehold` / `warzone_stonehold` / `gate_stonehold_faultline` | `dragon_stonehold_iron_wyrm` | `gem_stonehold_forge`, `gem_stonehold_depth` |
| `eldergrove` | `inner_eldergrove` / `warzone_eldergrove` / `gate_eldergrove_greenveil` | `dragon_eldergrove_moonbough` | `gem_eldergrove_root`, `gem_eldergrove_moon` |
| `umbral` | `inner_umbral` / `warzone_umbral` / `gate_umbral_ashvein` | `dragon_umbral_void_seraph` | `gem_umbral_veil`, `gem_umbral_ember` |

The four realm-dragon strings are unresolved references and are not the Wish Dragon source for `CTMA-BEAT-07`.

The habitat registry is `unity/Docs/Terrestrials/Ecosystems/ecosystem_habitat_profiles_manifest.json`, source version `tdf-eco-2026-07-27-v001`, Git blob `c412416aa6805d5b8bf8fcbc19897a654ad261c86`. All relevant habitat rows remain `RosterProposed`, user `NotRequested`, and runtime `Blocked` unless a narrower packet says otherwise.

| Asset need | Exact routing anchor | Retained source evidence | Gate |
| --- | --- | --- | --- |
| Trees / oldgrowth | `tdf_habitat_eldergrove_hollowbark_oldgrowth` / `tdf_envkit_eldergrove_hollowbark_oldgrowth` | `tdf_packet_hollowbark_oldgrowth_visual_source_v001`; establishing LFS SHA-256 `8ed2abc96a51c56ea7e7725475dbbfbe6c1bb251e9d936bd3930ed2ea95fe303`, `3,153,895` bytes | `PassWithConcern`; no cinematic tree model, wind, material, rights, or shot binding |
| Grass / meadow | `tdf_habitat_crownlands_galegrain_roadbelt` / `tdf_envkit_crownlands_galegrain_roadbelt`; `tdf_habitat_eldergrove_sunmane_edge_meadow` | roster direction only | exact plant families, source assets, rights, and shot binding `SOURCE_REQUIRED` |
| Clouds / storm | `tdf_habitat_crownlands_meridian_storm_shelf` / `tdf_envkit_crownlands_meridian_storm_shelf` | prototype weather direction only | production volumetrics, animation, light, audio, rights, and shot binding `SOURCE_REQUIRED` |
| Rocks | `tdf_habitat_stonehold_faultroad_escarpment` / `tdf_envkit_stonehold_faultroad_escarpment` | `tdf_packet_faultroad_escarpment_visual_source_v001`; establishing LFS SHA-256 `5ed510099512aec63dd58b1ac5853f1da9344c607eefbc4c2000240a43bdaa73`, `2,911,873` bytes | `PassWithConcern`; no approved cinematic 3D rock kit or exact model/material/rights hashes |
| Ores | `tdf_habitat_stonehold_ore_gallery_mouths` / `tdf_envkit_stonehold_ore_gallery_mouths` | roster/unpublished direction only | reviewable production ore models/materials/hashes `SOURCE_REQUIRED` |
| Burnt trees | `tdf_habitat_umbral_ashwood_veil_ravine` / `tdf_envkit_umbral_ashwood_veil_ravine` | roster direction only | exact burnt-tree source/model/material/hashes `SOURCE_REQUIRED` |
| Lava | `tdf_habitat_umbral_cinder_runoff_shelf` / `tdf_envkit_umbral_cinder_runoff_shelf` | direction for cooling lava, local steam, and heat shimmer only | production shader/material/volumetric/audio/composite source and shot binding `SOURCE_REQUIRED`; no Meshy credit merely for lava |
| Slagfall geology | `tdf_habitat_stonehold_slagfall_quarry` / `tdf_envkit_stonehold_slagfall_quarry` | `tdf_packet_slagfall_quarry_visual_source_v002`; master LFS SHA-256 `600a76d983f0cb63abf1169b7a9cdf34477b60ebf2e10ca9f74883efd899d195`, `3,133,264` bytes | separate habitat evidence; explicitly not active lava and no cross-packet approval transfer |

Current architecture production meshes do not establish exact cinematic gate, bridge, or stronghold source. Those source and shot bindings remain `SOURCE_REQUIRED`.

## Ranked asset-family routing

### P0 — bind the shot before building anything

Every family is blocked until the exact beat/shot dependency, camera and coverage, duration, realm and habitat, co-developer-selected source, retained input SHA-256/bytes, rights record, intended cinematic use, and later-gameplay reuse intent are known. A1 must then authorize the exact task, endpoint, payload, attempt count, and maximum credits.

### P1 — shot-critical 3D silhouette and layout

| Family | Correct production routing | Meshy disposition | Required source and acceptance gate |
| --- | --- | --- | --- |
| Wish Dragon or other hero terrestrial subject | A source-approved 3D candidate may be generated, then must receive DCC anatomy, topology, UV, rig, skin, material, deformation, animation, and shot-fidelity work | Current state: `SOURCE_REQUIRED / Meshy NotAuthorized`; the retained rejected result is evidence only and cannot be retried or used as a new input | Exact A2-approved cardinal source views, stable profile/source/concept/input hashes, rights, scale, anatomy, cross-view consistency, motion, exact cinematic coverage, named future gameplay reuse binding, and separate user approval are required. Do not infer this identity from issue `#456` realm guardians or local dragon files |
| Trees | Modular DCC trunk/major-branch silhouettes, leaf/needle cards or bounded clusters, shader wind, instancing; distant matte layers only where parallax permits | Only a unique close hero tree may justify Meshy; bulk forest generation does not | Realm species/form, age, season, density, hero/background role, wind state, shot coverage, source hash, and rights are `SOURCE_REQUIRED` |
| Burnt trees / ashwood | DCC hero silhouette and breakage, authored char/bark material, cards for fine branches, source-approved smoke/ember VFX and compositing | Conditional only for one close unique silhouette; otherwise DCC. Current state: `NotAuthorized` | Habitat/species, burn cause and degree, intact/dead state, smoke/ember permission, source hash, and rights are `SOURCE_REQUIRED`; generic burnt trees do not automatically equal Umbral ashwood |
| Rocks | Procedural/DCC modular kit, sculpt/bake for close hero faces, shared atlas/trim, instancing; 2D only for distant background | Conditional for a unique close hero formation that modular DCC cannot satisfy; never bulk scatter | Realm geology, scale, breakage, wetness/weather, camera distance, collision intent, source hash, and rights are `SOURCE_REQUIRED` |
| Ores | DCC host rock plus bounded ore inserts, decals or masks and material/shader; composited glints only when sourced | Conditional only for a unique close formation; ordinary veins/clusters stay DCC/material | Ore identity, host geology, shape, reflectance/emission, extraction state, source hash, and rights are `SOURCE_REQUIRED`; no automatic glow |
| Sculptural hero plant | Authored DCC refinement after exact source selection; cards/atlas for fine foliage | Conditional only for a shot-critical unique plant, not general flowers/grass | Exact fantasy anatomy/species, realm, scale, motion, source hash, and rights are `SOURCE_REQUIRED` |

The retained Wish Dragon result is A2-rejected exact-source-fidelity evidence from [issue #460 comment 5263093044](https://github.com/yulee94/AnotherLife/issues/460#issuecomment-5263093044): `wish_dragon_review_master.glb`, `53,457,548` bytes, SHA-256 `5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270`, with `9 + 30 = 39` credits consumed. Its disposition is `REJECTED_FOR_EXACT_SOURCE_FIDELITY / GeneratedUnreviewed / NON_PRODUCTION / evidence only`. It is `inputEligible=false`. No retry, regenerate, remesh, rig, animation, retexture, import, promotion, or paid follow-up is authorized. A future request requires four independently A2-approved cardinal views plus exact source hashes, rights, anatomy, and cross-view gates.

### P2 — weather, light, surface motion, sound, and compositing

| Family | Correct production routing | Meshy disposition | Required source and acceptance gate |
| --- | --- | --- | --- |
| Clouds | Volumetric shader/VFX or camera-appropriate 2D/3D cards and compositing | No Meshy | Cloud type, coverage, speed, altitude, lighting continuity, shot duration, source, and reduced-effects behavior are `SOURCE_REQUIRED` |
| Rain / raindrops | Pooled world particles, splashes, wetness shader/decal, optional accessible camera treatment, foley/ambience, and final compositing | No Meshy | Intensity, direction, droplet scale, surfaces, lens treatment, reduced-motion/flash behavior, and audio are `SOURCE_REQUIRED` |
| Thunder / lightning | Audio stems and timing plus source-approved lightning/illumination VFX and compositing | No Meshy | Audible thunder versus visible lightning, strike/source point, delay, intensity, exposure, reduced-flash behavior, and audio are `SOURCE_REQUIRED` |
| Sunlight rays | Lighting and volumetric shader; source-approved dust; composited shafts for locked shots | No Meshy | Sun direction, time, weather, occluders, density, exposure/color, and effects-off readability are `SOURCE_REQUIRED` |
| Lava | Simple DCC flow-bed/channel geometry only where needed; primary identity through material/shader/flow maps, heat haze, steam/volumetrics, lighting, audio, and compositing | No Meshy credit merely for lava | Exact shot/habitat, active or cooling state, speed/depth/crust, light/steam/audio intent, source hash, and rights are `SOURCE_REQUIRED`. Slagfall is not an active-lava source |

### P3 — scalable vegetation and set dressing

| Family | Correct production routing | Meshy disposition | Required source and acceptance gate |
| --- | --- | --- | --- |
| Grass | Instanced low mesh/cards, shared atlas, broad authored wind; cinematic strands only for proved close coverage; distant compositing where appropriate | No Meshy | Realm species family, height/density/season, trampling, wind state, camera coverage, source hash, and rights are `SOURCE_REQUIRED` |
| Flowers | Small low-poly clusters plus alpha cards/atlas and instancing; macro hero flower uses a separate DCC/source record; distant composited patches | No Meshy for ordinary dressing | Exact species/fantasy anatomy, realm, silhouette independent of color, density/season, source hash, and rights are `SOURCE_REQUIRED` |

Dependency order is: P0 source/shot locks -> P1 silhouette and layout blockout -> P2 lighting/weather/lava surface/audio -> P3 scatter vegetation -> final compositing. This order is not a creative selection.

## Known terrestrial evidence that is not selected for this cinematic

The following exact issue `#259` concept sheets remain reviewable historical source evidence. They do not satisfy the active shot, A2-selection, rights, production, cinematic, gameplay, or runtime gates.

| Profile / concept asset | Git pointer blob | LFS SHA-256 / bytes | Current local evidence classification |
| --- | --- | --- | --- |
| `tdf_elite_eldergrove_hollowbark_stalker` / `tdf_asset_elite_eldergrove_hollowbark_stalker_concept_v001` | `1a2615fc4c9a964ea223b66bed37dc5b3d3d6fed` | `8aa0e974fac00a2f6a0ea7d23eb8056c75e8a20f69dba6cb2153ba6501fbe6cb` / `2,658,021` | `ObservedLocal_NON_PRODUCTION / PassWithConcern` |
| `tdf_elite_stonehold_rimehorn_breaker` / `tdf_asset_elite_stonehold_rimehorn_breaker_concept_v001` | `b3181b441ff0cf452268ff8797d14e819b205c08` | `f05cb4102528f430ccf1173f6dee5d0824cfcbcb9e1c858b20b731cc2ecc3bf3` / `2,694,431` | `ObservedLocal_NON_PRODUCTION / PassWithConcern` |
| `tdf_elite_crownlands_reliquary_basilisk` / `tdf_asset_elite_crownlands_reliquary_basilisk_concept_v001` | `90d85ab7ce9369d50bb551d8eb40fb2a91cfbae6` | `a4c4385a7ad444ccb1926163b6ca9ff663cca9563cb5773fd0a245aef5c6a6d1` / `2,640,177` | `ObservedLocal_NON_PRODUCTION / PassWithConcern` |
| `tdf_elite_umbral_cindermaw_salamander` / `tdf_asset_elite_umbral_cindermaw_salamander_concept_v001` | `ce568a64c5bc5f8238df530d9ce65e78df47aa63` | `61a5ea43950826a19dc344c3e8f0413cd78457b33cb85c0aeff52a2e9eb872ee` / `2,804,083` | local Cindermaw DCC `PrototypeOnly`; local Cindermaw Shot030 `RigidPrevisOnly` |
| `tdf_elite_umbral_veilspine_widow` / `tdf_asset_elite_umbral_veilspine_widow_concept_v001` | `f53cfbadd3f1daf0821592ba668b13d7c54c6943` | `8432d1b562689dbe55258b7213ee3ccc1baa01fbe83937c7c7ddd6ff1170590d` / `2,285,521` | local multiview `Rejected` |

These sheet records belong to immutable source version `tdf-rbe-2026-07-24-v001`. A later additive authority overlay may reconcile historical generator lineage with current co-developer authority, but this cinematic document neither performs nor substitutes that source update.

Additional non-transfer boundaries:

- `tdf_habitat_umbral_cinder_runoff_shelf` provides only provisional habitat direction for cooling lava, steam, and heat shimmer. Its `RosterProposed` state is not a shot selection, layout, asset, or approval.
- `tdf-eco-slagfall-2026-07-30-v002` is explicitly not an active-lava biome and forbids lava or glowing-crack transfer. Its source approval cannot authorize any elite, cinematic, lava, rock, ore, or vegetation asset here.
- Issue `#456` realm-guardian and raid-dragon evidence cannot be substituted for the source-approved Wish Dragon required by `CTMA-BEAT-07`.
- Existing local FBX, Blend, rig, lookdev, previs, task, or preview evidence remains `ObservedLocal_NON_PRODUCTION`, `PassWithConcern`, `Rejected`, `PrototypeOnly`, or `RigidPrevisOnly` exactly as already dispositioned.

## Meshy authorization state machine

Exactly one state applies to an item.

| State | Required behavior |
| --- | --- |
| `Deferred` | Method, endpoint, payload, and task ID are unset. Estimated, maximum, authorized, and actual credits are all `0`. Creation, upload, retry, refinement, conversion, and retrieval calls are false |
| `A1AuthorizedOneShot` | Every required field below is complete; exactly one task attempt in dedicated task `019fef94-af14-7671-a739-447391cfb7a5`; expiration and fail-closed stop rules active; any retry or changed endpoint/input/payload requires a new A1 authorization |
| `ExecutedStopped` | The authorized attempt reached a terminal provider result or terminal error; actual credits and evidence are recorded; no continuation, retry, refinement, remesh, rig, animation, retexture, conversion, alternate endpoint, or substitute input is permitted without a new A1 authorization |
| `RetentionGetOnly` | Exact pre-existing task/output identity; GET only in the dedicated Meshy task; zero incremental credits; bounded retrieval of already-produced status/bytes; no creation, continuation, retry, refinement, conversion, upload, mutation, unbounded polling, or approval |

All Meshy authorization, execution, polling, downloads, QA, retention, and credit reporting are exclusive to dedicated task `019fef94-af14-7671-a739-447391cfb7a5`. A separate bounded retention record must bind the exact pre-existing task/output. This document supplies no operational task ID and authorizes or executes no GET. A retention record permits at most one status GET and one GET per already-enumerated ready artifact; ordinary signed-storage redirects are part of that retrieval. A pending/not-ready response stops the session. Any unknown cost or possible mutation fails closed.

## Required future per-item authorization record

No field may be inferred from a file name, task success, balance display, merged review sheet, local path, or similarly named asset.

### Identity and scope

- `authorizationId`
- `priorityItemId`
- `subjectProfileId`
- `shotDependencyIds[]`: one or more exact prioritized cinematic shot/beat dependencies
- `namedGameplayReuseBinding`: exact future gameplay entity or reusable module identity
- `purpose`: `exploratory_nonproduction | source_candidate | production_candidate`
- `reuseIntent`: exactly `cinematic_and_named_gameplay_candidate`
- `maximumAcceptanceCeiling`

### Authority and attempt boundary

- `authorizationState`
- `executionTaskId`: exactly `019fef94-af14-7671-a739-447391cfb7a5`
- `authorizedBy`: exactly `A1`
- `decisionReference`
- `authorizedAtUtc` and `expiresAtUtc`
- `maximumAttempts`: exactly `1`
- `retryRequiresNewAuthorization`: `true`

### Exact source and rights binding

- `sourceOwner`
- `sourcePacketId`, `sourcePacketVersion`, and `sourcePacketSha256`
- `profileId` and `profileSha256`
- `conceptAssetId` and `conceptAssetSha256`
- ordered `inputAssets[]`, each with repository-relative or durable logical locator, byte length, SHA-256, rights-record ID, and rights-evidence SHA-256
- rights evidence recording creator/provider, terms/license URL or repository record, retrieval/effective date, evidence SHA-256, commercial/project-use scope, derivative/AI-use scope, attribution/redistribution limits, and `rightsState`

Every paid candidate, including an exploratory nonproduction candidate, requires an exact A2-approved source, retained source bytes and hashes, and compatible rights evidence before execution. Provisional, composite, inferred, similarly named, provider-generated, or merely visible source cannot be authorized as a paid input. `ObservedLocal_NON_PRODUCTION` and task success cannot cure missing rights or source authority.

### Provider request and credit ceiling

- `provider`: exactly `Meshy`
- `operation`, exact HTTP method, exact endpoint, and `modelOrPipeline`
- canonical `payloadSha256`
- expected output types and exact ordered input count
- `estimatedCredits`, `maximumCredits`, `authorizedCredits`, and later `actualCredits`
- `billingChangeAllowed`: `false`

Unknown or null cost is forbidden. A balance or subscription observation is evidence only and is never authorization.

### Mandatory stop rules

- `stopOnSuccess=true`
- `stopOnAnyError=true`
- `stopOnCreditMismatch=true`
- `stopOnSourceHashMismatch=true`
- `stopOnRightsUncertainty=true`
- `retryPolicy=ForbiddenWithoutNewA1Authorization`
- no automatic retry, fallback, remesh, rig, animation, retexture, conversion, alternate endpoint, or substitute source

### Execution evidence

Only after a valid future run, record:

- exact `taskId`, provider status, request and response UTC timestamps and hashes
- actual credit use and comparison against the authorized ceiling
- `outputFiles[]` with durable locator, byte length, SHA-256, media type, and provider output identity
- rejected-output retention and exact error/stop result
- tool/model/version, DCC derivative operations and hashes, and rights lineage

### Independent approval fields

- `a2SourceDisposition`
- `a1TechnicalDisposition`
- `userCreativeDisposition`
- `cinematicUseState`
- `gameplayUseState`
- `runtimeIntegrationState`
- `releaseState`

A Meshy result, DCC preview, PR merge, green validation, or co-developer visibility changes none of these fields automatically.

## Cinematic and gameplay separation

Meshy produces at most a reusable source candidate. Every future paid candidate must be bound before execution to both an exact prioritized cinematic dependency and a named future gameplay entity or reusable module. That reuse binding is lineage and planning only; it grants no gameplay output or runtime approval. Meshy owns no final topology, rig, blendshapes, equipment fit, animation quality, camera, weather, VFX, audio, edit, encode, gameplay stats, AI, spawning, loot, hitboxes, colliders, or Player packaging.

For every family:

- `cinematicOutput` receives a unique asset ID/version, exact source refs, tool/version, file SHA-256/bytes, rights-record hash, DCC/render-settings hash, `packageClass=cinematic_offline`, and `playerDependency=false`.
- `gameplayOutput`, if later authorized, receives a separate ID/path/file SHA-256/bytes, import settings, quality tier, mesh/texture/VFX/audio budgets, LOD/cards/impostor/pooling/compression evidence, and platform validation. It may name `derivedFromCinematicSha256` for lineage only.
- High-poly meshes, editable DCC, cinematic-only strand systems, volume/render caches, lossless audio masters, and compositing sources stay outside Player dependencies.
- Runtime candidates contain only independently reviewed and profiled derivatives. A mutable cinematic file is never the runtime artifact.
- Cinematic and gameplay versions must preserve required identity while retaining separate approval, budget, import, and package bindings.

## Fail-closed validation

| Check | Failure condition | Result |
| --- | --- | --- |
| `CTMA-BASE-001` | Base or merge-base is not exact `6102638a41e1d944e267801df4cb22bbbd0af5eb` | Block publication or refresh/reconcile through A1 |
| `CTMA-SCOPE-001` | Diff contains anything except this Markdown path, including `.meta`, images, manifests, scripts, or local evidence | Reject diff |
| `CTMA-AUTH-001` | A new paid operation lacks a fresh exact A1 one-shot decision, current further authorization is represented as nonzero without evidence, or any approval state advances automatically | Stop; retain `NotAuthorized`; reject |
| `CTMA-VENUE-001` | Any Meshy authorization, execution, polling, download, QA, retention, or credit reporting occurs outside task `019fef94-af14-7671-a739-447391cfb7a5` | Stop before operation; reject evidence as operational authority |
| `CTMA-METHOD-001` | POST/PUT/PATCH/DELETE/upload/generation is enabled without a complete `A1AuthorizedOneShot` record | Stop before call |
| `CTMA-CREDIT-001` | Cost is null/unknown, estimate exceeds ceiling, actual exceeds ceiling, or billing action appears | Stop before or immediately after evidence capture; no retry |
| `CTMA-SOURCE-001` | Missing/mismatched packet/profile/concept/input hash, wrong profile binding, or provisional source presented as selected | Quarantine and block |
| `CTMA-RIGHTS-001` | Missing, unknown, expired, or incompatible rights for claimed purpose | Block execution/selection |
| `CTMA-RETRY-001` | More than one attempt, auto-retry, or changed endpoint/operation without new A1 decision | Stop; require new authorization |
| `CTMA-GET-001` | GET lacks pre-existing task ID, may mutate/charge, polls, retrieves unenumerated output, or status is not ready | Stop and defer |
| `CTMA-EVIDENCE-001` | Response/output lacks task ID, bytes/hash, actual cost, or rejected-output retention | Evidence incomplete; no approval |
| `CTMA-HISTORY-001` | Batch `priority_spend_20260812_v001`, `7,675 - 1,014 = 6,661`, or `26 * 9 + 26 * 30 = 1,014` is misstated; PR #472 is presented as a current balance ledger | Reject stale or contradictory ledger claim |
| `CTMA-WISH-001` | Rejected Wish Dragon bytes/hash/cost/state changes, its output is reused, or a follow-up is implied without a new A2-approved source | Preserve evidence; remain `SOURCE_REQUIRED / Meshy NotAuthorized`; reject |
| `CTMA-A2-001` | This document selects/redesigns/approves terrestrial identity or elevates local nonproduction evidence | Reject authority leak |
| `CTMA-CIN-001` | Candidate/task success becomes cinematic accepted without source-owner, A1, and user gates | Reject approval leak |
| `CTMA-GAME-001` | Gameplay/combat/AI/spawn/loot/stat/runtime authority is inferred | Reject authority leak |
| `CTMA-PROVENANCE-001` | Local/unmerged `b33d0e6...` evidence is described as current-main canonical | Reject stale source claim |

All new-operation failures leave the item `Deferred / NotAuthorized / NotSelected / NotApproved / NotInUnity`. Historical terminal evidence remains `ExecutedStopped` and cannot be reopened by a failure or later observation. There is no fallback generation, substitute asset, or cross-packet approval transfer.

## Current disposition and next gates

1. Batch `priority_spend_20260812_v001` is complete and terminal `ExecutedStopped`: `1,014` credits consumed, balance `7,675 -> 6,661`, no retry, and all outputs `GeneratedUnreviewed / NON_PRODUCTION`.
2. No further paid Meshy operation is currently authorized. `currentFurtherPaidAuthorizationCount=0`, `currentFurtherPaidCreditCeiling=0`, and `currentFurtherPaidState=NotAuthorized` describe only the post-batch present state; they do not erase the executed batch.
3. Wish Dragon is `SOURCE_REQUIRED / Meshy NotAuthorized`. Its rejected `53,457,548`-byte GLB, SHA-256 `5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270`, and `39`-credit history are retained evidence only with no follow-up.
4. Draft PR #472 remains the sanitized historical retention cutoff at `7,675 -> 7,675`; it is not the current balance ledger and grants no promotion or spend authority.
5. Lava is confirmed as an environment surface/flow family. It routes primarily to VFX, shader, material, volumetric, audio, and compositing work; lava alone authorizes no Meshy credit.
6. The co-developer must select or author each exact terrestrial source and fidelity constraint, and the record must bind rights, retained bytes/hashes, an exact cinematic dependency, and a named future gameplay reuse target before A1 may authorize another paid candidate.
7. Spend-as-work-becomes-ready permits no automatic next batch and establishes no balance-preservation target. A1 must issue a new per-item one-shot authorization in the dedicated Meshy task with exact input, endpoint, payload hash, attempt count, maximum credit ceiling, and stop rule.
8. The user must separately approve the exact visual source/result and later integrated cinematic. DCC, cinematic production, editorial, engineering, runtime packaging, device profiling, and release remain later, separately reviewed lanes.

## Publication and validation boundary

- Authorized repository path: `unity/Docs/Terrestrials/Cinematics/Cinematic_Terrestrial_Asset_Priority_and_Meshy_Authorization.md`
- Authorized branch: `codex/coordination-cinematic-terrestrial-meshy-authority`
- Authorized base: `main@6102638a41e1d944e267801df4cb22bbbd0af5eb`
- Draft PR title: `coordination: define cinematic terrestrial asset priority and Meshy authorization gates`
- Expected diff: exactly one Markdown file; zero binary/LFS/runtime/Player/install bytes; no Unity `.meta` file; no shared lock
- Validation: exact base and merge-base, one-path diff, whitespace check, arithmetic and link reconciliation, dedicated-task exclusivity, historical-versus-current ledger accuracy, Wish Dragon rejection/current state, explicit 3D-vs-VFX/audio routing, A2-approved rights/source hash gates, exact cinematic plus named gameplay reuse binding, cinematic/gameplay package split, and no stale approval claim
- Not run: Meshy/API/plugin operations, because prohibited and unnecessary; Unity/build/device checks, because this is documentation-only with zero runtime impact

Current phase: coordination/review specification. Acceptance state: ready for A1 review after the exact one-file draft is published; not source-approved, not user-approved, not production-approved, not runtime-approved, and not release-approved.
