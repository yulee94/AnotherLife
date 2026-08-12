# Meshy Retention and Traceability Ledger — 2026-08-12

**Status:** COMPLETE WITH CONCERNS / EVIDENCE ONLY / NO ASSET PROMOTION
**Primary delivery mode:** Codex coordination/review
**Related coordination PR:** #463
**Evidence cutoff:** 2026-08-12 13:10:29 KST

## Executive disposition

The retained Meshy campaign contains 22 API tasks. The accepted GET-only reconciliation found 22/22 HTTP 200 responses, 22/22 provider states at SUCCEEDED and 100 percent progress, and 425 historical credits consumed. The authorized retention rescue changed the balance by zero credits: 7,675 before and 7,675 after.

The provider advertised 124 output roles:

- 87 roles were already valid locally before rescue.
- Batch v001 rescued 7 roles totaling 184,410,999 bytes.
- Batch v002 attempted exactly the 30 remaining roles once each. It rescued 26 valid roles totaling 548,487,383 bytes and retained four interrupted download fragments totaling 82,911,232 bytes.
- Final valid local coverage is 120/124 roles, or 96.7742 percent.
- The 33 valid rescued roles total 732,898,382 bytes.

All valid-rescue byte counts and SHA-256 values are in the adjacent JSON manifest. The four fragments are identified separately as incomplete and are not valid artifacts.

Provider success and local retention do not establish source fidelity, cinematic suitability, gameplay readiness, production acceptance, Unity integration, or user approval. Every prior PrototypeOnly, Reject, ReviewCandidate, PassWithConcern, and NON_PRODUCTION disposition remains unchanged.

## Authorized scope and hard boundaries

The rescue was limited to authenticated GET-by-ID calls and downloads of exact provider-advertised roles that were absent locally. It made no POST, upload, generation, retry, remesh creation, rigging, animation, conversion, retexture, delete, recharge, purchase, renewal, or billing change.

The continuation used:

- one process;
- one attempt per residual role;
- no retry of the seven v001 saves;
- no overwrite of an existing artifact;
- file-backed progress and output logs;
- balance gates fixed at 7,675 before and after;
- nonzero-byte, extension, magic, parse, and SHA-256 checks before a file could be classified valid.

The four interrupted transfers stopped after their single attempts. Regeneration or retry was not authorized and did not occur.

This publication contains metadata only. It excludes private source images, generated images, models, textures, thumbnails, DCC files, prompts, signed result URLs, credentials, account/workspace identity, and machine-local paths.

## API task traceability

| API task | Intended mapping | Local role coverage | Existing disposition |
| --- | --- | ---: | --- |
| 019feff9-2da1-7714-a808-1448178a25b3 | No canonical entity; superseded Crownlands character prototype | 7/7 | PrototypeOnly; rejected multi-subject |
| 019ff053-052e-771b-8c52-4f4a794dc310 | No canonical entity; superseded Crownlands character prototype | 7/7 | PrototypeOnly |
| 019ff06a-e3b7-7380-a45e-882c3fbf1401 | Same superseded character prototype | 7/7 | Reject / PrototypeOnly; topology and pivot defects |
| 019ff16f-4bb7-79a3-9b6e-4b3c7d2b8c3c | dragon_crownlands_dawn_regent / aurelius | 8/8 | NON_PRODUCTION DCC intake and visual QA only |
| 019ff181-255e-7e7e-8c44-f81007e29023 | dragon_stonehold_iron_wyrm / ferrum | 2/2 | Source multiview only |
| 019ff18a-1893-70a7-812b-15d54f8e27d4 | dragon_stonehold_iron_wyrm / ferrum | 8/8 | PrototypeOnly; fidelity failed |
| 019ff18e-34d8-73d0-a35c-0b192b63b296 | dragon_eldergrove_moonbough / virens | 3/3 | Source multiview only |
| 019ff192-8dfe-72c0-b1b2-3ba2a839ebc9 | dragon_eldergrove_moonbough / virens | 6/8 | PassWithConcern; NON_PRODUCTION silhouette only |
| 019ff193-6c34-74df-9835-a90bdeaf833e | dragon_umbral_void_seraph / nox | 3/3 | Reject; two-legged anatomy; superseded |
| 019ff195-5f5f-765d-a465-519f6b61cb79 | NPC_VAELORYN / npc_vaeloryn | 2/2 | Source multiview only |
| 019ff198-c142-75f5-a2b3-b7ee4dbf2ddc | NPC_VAELORYN / npc_vaeloryn | 7/8 | NON_PRODUCTION intake silhouette |
| 019ff19c-e54d-76d9-9e7d-fb503dd1b670 | dragon_umbral_void_seraph / nox | 2/2 | Corrected source multiview only |
| 019ff19f-43b1-7792-b33a-9d75512c628c | dragon_umbral_void_seraph / nox | 7/8 | NON_PRODUCTION intake silhouette |
| 019ff1a0-df34-78fd-b787-c60bf421f10c | tdf_elite_eldergrove_hollowbark_stalker | 3/3 | Source PassWithConcern; missing front view |
| 019ff1a4-97dd-77d1-87b9-cb23ac26c618 | tdf_elite_eldergrove_hollowbark_stalker | 9/9 | ReviewCandidate; NON_PRODUCTION |
| 019ff1a9-a06e-773b-bf69-b34aa94871c3 | tdf_elite_stonehold_rimehorn_breaker | 3/3 | Source PassWithConcern; missing rear view |
| 019ff1ab-e7fd-77ef-9962-1b5d39b15be4 | tdf_elite_stonehold_rimehorn_breaker | 9/9 | ReviewCandidate; NON_PRODUCTION |
| 019ff1b2-1908-7b3e-962f-a30db47953b0 | tdf_elite_crownlands_reliquary_basilisk | 3/3 | Source PassWithConcern; missing rear view |
| 019ff1b4-4ff4-767f-96bf-32f7dafaaead | tdf_elite_crownlands_reliquary_basilisk | 9/9 | ReviewCandidate; NON_PRODUCTION |
| 019ff1b9-9a34-7a55-8310-4448f7c0739d | tdf_elite_umbral_veilspine_widow | 3/3 | Reject; inconsistent spine count; no 3D task |
| 019ff1bc-6c72-787f-9517-70c2cf2f0a62 | tdf_elite_umbral_cindermaw_salamander | 3/3 | Source multiview only |
| 019ff1be-a0ac-7b63-b1b7-7e6353522a3e | tdf_elite_umbral_cindermaw_salamander | 9/9 | Intake visual QA only; not final art |

These are intended traceability mappings, not runtime bindings or promotions. Crownlands character prototypes remain deliberately unbound and cannot substitute for the reusable modular character-creator foundation.

## Final residual gaps

Exactly four provider roles remain without a valid complete local artifact. Each stopped after one ChunkedEncodingError and was preserved only as a hashed incomplete fragment:

| Task | Provider role | Fragment bytes | SHA-256 |
| --- | --- | ---: | --- |
| 019ff192-8dfe-72c0-b1b2-3ba2a839ebc9 | model_url | 13,901,824 | 2c51ba2a855d36fc20034bd0039f9bf8dd936cab0b9dc7d8e4383015c2e5ac32 |
| 019ff192-8dfe-72c0-b1b2-3ba2a839ebc9 | model_urls.pre_remeshed_glb | 13,877,248 | f7be6375e80b775de1c7e22599eee9d1ed52d6ba065011d601bdf1866514ac60 |
| 019ff198-c142-75f5-a2b3-b7ee4dbf2ddc | model_url | 25,919,488 | 41482264f75bcbdc3ea6164eba93831c34c5c5fd7561325be6d120d0ef71663e |
| 019ff19f-43b1-7792-b33a-9d75512c628c | model_urls.pre_remeshed_glb | 29,212,672 | 713e24ca1f68116eb0020a7c157aa309272c6dc2fd056e163581f97b645513b6 |

These fragments are not parse-validated outputs, cannot be imported or promoted, and do not authorize a retry. The other 120 advertised roles are valid locally.

## API-hosted versus local-only evidence

The durable local evidence consists of task JSON, private sources or generated views where retained, FBX/GLB/PBR/thumbnail provider outputs, and selected DCC/QA derivatives. None of those binaries is published by this PR. The JSON manifest publishes only sanitized identities, counts, sizes, hashes, role states, and acceptance boundaries.

Meshy documents a maximum three-day retention period for non-Enterprise API-generated models. Signed result links are time-limited, and the documentation does not promise durable task-result metadata after asset expiry. See [Asset Retention](https://docs.meshy.ai/en/api/asset-retention).

The safe recovery path is the exact task-type endpoint plus task ID, or that endpoint's list operation. See [API Quick Start](https://docs.meshy.ai/en/api/quick-start), [Image to 3D](https://docs.meshy.ai/en/api/image-to-3d), and [Remesh](https://docs.meshy.ai/en/api/remesh).

Meshy's [Rigging API](https://docs.meshy.ai/en/api/rigging) and [Animation API](https://docs.meshy.ai/en/api/animation) explicitly say API-created tasks are managed through the API and do not appear in the web app's My Assets. Therefore absence from My Assets alone is not proof of loss or account mismatch. A balance discrepancy or failure to find known IDs under the correct authenticated task endpoint is a stronger mismatch indicator. The [Balance API](https://docs.meshy.ai/en/api/balance) remains the account-level check.

## Validation evidence

The final reconciliation independently verified:

- exactly 30 v002 starts, finishes, receipts, and unique task/role keys;
- exactly 11 unique task GET summaries, all HTTP 200;
- 11 task manifests summing to 30 planned = 26 valid + 4 residual;
- all 26 v002 valid files present, nonzero, and byte/hash-matching;
- 14/14 v002 GLBs with valid glTF v2 magic, declared length, and parseable JSON chunk;
- 12/12 v002 PNGs with valid signature, IHDR dimensions, and IEND;
- all four incomplete fragments present and byte/hash-matching their receipts;
- no overlapping task/role key between the 7 v001 saves and 26 v002 saves;
- zero live rescue processes;
- zero `.partial`, `.download`, or `.invalid` temporary residue files; the four separately classified, hashed `.incomplete` fragments remain retained as failure evidence;
- zero stderr bytes;
- balance exactly 7,675 before and after.

No Unity import, rig validation, animation validation, LOD review, equipment-fit test, target-device performance measurement, cinematic render, gameplay integration, A2 terrestrial fidelity review, or user playtest was performed. Those checks are outside this retention-only scope and remain blocking where applicable.

## Cinematic and gameplay acceptance boundary

Cinematic remains the prioritized player-experience path, but this retention evidence alone unlocks no shot. Meshy supplies reusable model/reference sources only. Unity remains responsible for deterministic staging, animation, camera, weather, lighting, VFX, audio, and rendering.

Future paid Meshy work requires one exact A1-authorized task with:

- approved source identity and hash;
- intended 1,440-frame cinematic shot dependency;
- intended later gameplay entity or reusable module binding;
- endpoint and payload;
- maximum credit ceiling;
- one-shot stop and no-retry rule;
- local intake, fidelity, topology, material, rig/animation, LOD, performance, and player-experience checks appropriate to that asset.

No broad campaign, habitat expansion, fort/castle set, terrestrial, dragon, boss, elite, modular character, mount, gear, or other model generation is authorized by this ledger. Lava remains an environment VFX/shader/material/volumetric/audio/compositing dependency unless A1 separately justifies exact 3D geometry.

## File and approval impact

- Narrative/content source changed: no.
- Terrestrial design source changed: no.
- Android or Unity runtime/gameplay changed: no.
- Assets, scenes, importers, or generated artifacts committed: no.
- Shared contracts/catalogs changed: no.
- Save/migration/recovery changed: no.
- Workflow/dependency/repository settings changed: no.
- Runtime performance, memory, package size, install size, or device compatibility changed: no.
- Shared-file locks: none.

The active roadmap gate remains blocked; this cross-lane retention evidence does not advance Phase 1 or first-user cinematic acceptance. The draft PR may establish durable traceability only. Asset promotion, future Meshy spending, Unity integration, cinematic acceptance, gameplay reuse, merge readiness, milestone acceptance, and release approval all remain separately gated.

## Next authority

A1 must select or reject the next Meshy task. The Meshy execution lane has requested at most one exact, source-approved, cinematic-critical and later-gameplay-reusable paid task with a disclosed credit ceiling. Until A1 provides that authorization, the recorded balance remains 7,675 and all paid work stays on hold.
