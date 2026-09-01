# AnotherLife Rig, Motion, and Catalog Production Standard v1

**Status:** Active technical production standard; individual asset admission, creative approval, gameplay results, balance, and release remain separately gated

**Standard catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_rig_motion_standard.json`

**Required-motion manifest:** `unity/Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json`

**Schemas:**

- `unity/SharedContracts/Schemas/al-rig-motion-standard.schema.json`
- `unity/SharedContracts/Schemas/al-required-motion-manifest.schema.json`

**Binding template:** `unity/Docs/ArtPipeline/Templates/Rig_Motion_Asset_Binding_Template_v1.json`

**Semantic validator:** `unity/SharedContracts/Tests/rig_motion_standard.py`

**Version:** 1.0.0

**Validated production baseline:** Blender 5.2.0 LTS; Unity 6000.3.22f1

## 1. Authority and scope

This standard controls technical interchange for every approved or explicitly admitted Champion, NPC, fantasy-beast, and monster source. It defines skeletons, transforms, bind poses, skinning, sockets, hitbox attachment, retargeting, clips, events, layers, interruption, budgets, and deterministic catalog bindings.

It does not:

- approve a face, anatomy, silhouette, outfit, creature behavior, animation personality, or release;
- turn a proposal, concept sheet, Meshy result, filename, or embedded take into an admitted production asset;
- let animation create damage, healing, control, movement permission, costs, cooldowns, loot, or state results;
- authorize a motion absent from the latest owner or approved asset-specific source;
- allow runtime lookup by file order, FBX take order, Unity local file ID, hierarchy index, or import timing.

Authority order is the latest explicit owner decision, approved asset-specific packet, root design authority, this technical standard, then prototypes. A source-specific owner restriction overrides a generic motion floor. The catalog records the restriction instead of filling the gap with invented behavior.

Normative language: **MUST** and **REQUIRED** fail admission; **SHOULD** requires a recorded exception when not followed; **MAY** is optional.

## 2. Recon and current baseline

The initial Kanban recon enumerated 227 tracked text files / 86,483 lines plus 112 relevant binary assets and directly reviewed 6,228 lines across 24 primary files/sections (7.20% focused coverage). After updating to the current merged `origin/main`, this task also re-read the current schemas, validators, catalogs, rig reports, import metadata, and runtime consumers used by this standard. The measured 7.20% is focused coverage, not a claim that every repository file was read end to end; the later refresh is deliberately not added to that percentage because its paged reads were not captured as a second disjoint corpus.

Current facts that this standard fails closed around:

- There are no production Animator Controllers or Override Controllers and only two standalone cinematic `.anim` assets.
- The current Champion motor publishes movement receipts and basic-attack acceptance; `SkillCaster` owns cast timing and gameplay resolution, but there is no general rig/motion phase consumer.
- `FirstSessionAuthoredVisualBinder` loops one `AnimationClipPlayable`, disables IK, and adds procedural bob/yaw. This is MVP presentation, not the production motion runtime.
- The male and female first-session Champion sources contain one generic take, have no valid Humanoid Avatar, and need transform/weight rebaking.
- Covenant Sentinel has one Generic 1.033-second, 30 Hz, non-looping embedded walking take with no Avatar, explicit clip definition, or motion root.
- Slagwhistle v001 has 9,200 triangles, 38 deform bones, no non-deform root, no motion root, and zero actions.

## 3. Coordinate, unit, and transform contract

All new or remediated sources MUST satisfy:

| Concern | Required value |
| --- | --- |
| World unit | 1 meter |
| Unity scale | 1 Unity unit per meter |
| Blender units | Metric, meters, unit scale 1.0 |
| Blender source axes | +Z up, -Y forward |
| Unity result axes | +Y up, +Z forward |
| FBX export axes | forward -Z, up +Y |
| Exportable object location | `(0, 0, 0)` |
| Exportable object rotation | `(0, 0, 0)` degrees |
| Exportable object scale | `(1, 1, 1)` |
| Ground tolerance | 2 mm |
| Scalar identity tolerance | 0.0001 |

Characters and grounded non-humanoids use a ground-center root. Airborne, aquatic, serpentine, or recumbent subjects declare an anatomy-specific support datum, but root and motion root still use canonical metric transforms.

An exporter MUST NOT silently repair source scale, facing, ground, shear, or hierarchy. Cleanup creates a new source version, rebakes bind matrices, sockets, constraints, shape keys, and clips, then compares the new version against the retained source.

## 4. Skeleton root contract

Every exported skeleton has exactly this control root:

`root` → `motion_root` → `pelvis` or `body_root`

- `root` is the only parentless exported bone, is non-deforming, remains at origin, and carries no authored trajectory.
- `motion_root` is non-deforming. It carries the retained source trajectory and yaw used for analysis, root-motion clips, and in-place derivation.
- `pelvis` is the Humanoid body root. `body_root` is the non-humanoid center-of-mass root.
- Deform bones never parent to sockets, controls, constraint targets, or preview helpers.
- Negative scale, shear, duplicate names, zero-length deform bones, disconnected undeclared roots, and cycles are invalid.

The final skeleton signature is SHA-256 over each canonical full parent path, parent path, quantized local bind matrix, and deform flag, serialized in bytewise path order under algorithm `sha256_canonical_parent_path_bind_matrix_deform_flag_v1`. Until that signature exists, the binding is not production-admissible.

## 5. Bone names and hierarchy

Names are lowercase ASCII snake case. Paired sides end `_l` and `_r`. Ordered segments use `_01`, `_02`, and so on. Identity is the full parent path, not the short name or array index.

### 5.1 Humanoid minimum

The required body is:

- `root/motion_root/pelvis`
- `spine_01/spine_02/chest/neck_01/head`
- `clavicle_<side>/upper_arm_<side>/lower_arm_<side>/hand_<side>`
- `upper_leg_<side>/lower_leg_<side>/foot_<side>/toe_<side>`

Optional upper chest, eyes, jaw, fingers, twist bones, face bones, cloth, and hair chains follow the same naming and must stay within the selected budget. Control bones remain in Blender and are baked out unless a declared runtime solver requires an exposed non-deform transform.

### 5.2 Non-humanoid minimum

Every anatomy declares:

- root, motion root, and center of mass;
- an ordered axial/facing chain;
- every load-bearing limb and terminal contact;
- every wing, tail, fin, tentacle, membrane, or extra-limb chain by semantic name and segment;
- each approved attack, interaction, VFX, camera, and contact origin by socket.

A non-humanoid may retarget only when center of mass, contact count, chain order, gait class, bend planes, facing, bind-pose disposition, and the semantic map are compatible. Similar silhouettes are not evidence.

## 6. Bind poses

### 6.1 Humanoid A-pose

`rmc_bind_humanoid_a_pose_v001` is upright and neutral:

- feet parallel and grounded;
- knees and elbows visibly biased about five degrees toward their intended bend plane;
- arms forty-five degrees below horizontal;
- palms toward thighs, fingers relaxed;
- pelvis and shoulders level;
- head and eyes forward;
- paired landmarks symmetric within 2 mm unless an approved anatomy exception exists.

Unity Humanoid Configure Avatar MUST validate the mapping and T-pose conversion without changing stored source bind matrices.

### 6.2 Non-humanoid neutral contact

`rmc_bind_nonhumanoid_neutral_contact_v001` is the lowest-strain anatomy-neutral rest with supported center of mass, visible bend planes, inspectable appendages, and every declared support contact on its datum within 2 mm. It does not invent a biped or quadruped stance.

### 6.3 Slagwhistle exception

`rmc_bind_slagwhistle_recumbent_v001` preserves the approved folded/recumbent source. A standing rest would invent silhouette. A new cleanup version adds non-deforming `root` and `motion_root`, canonical aliases, sockets, and contact semantics while preserving measured source deformation. Unity Humanoid and generic standing-quadruped retargeting are forbidden.

## 7. Topology, weights, and deformation

Runtime meshes MUST be deterministically triangulated with no n-gons, no degenerate triangle at or below `1e-10 m²`, finite coordinates, valid normals/tangents, and no non-manifold topology except documented module seams.

Every LOD MUST satisfy:

- no more than four non-zero influences per vertex after the 0.001 storage threshold;
- weights normalized to 1.0 within 0.0001;
- zero unweighted vertices;
- no blind pruning without before/after deformation evidence;
- no inverted triangles in the required pose suite;
- modular seam separation no greater than 2 mm;
- protected body/equipment penetration no deeper than 5 mm and no larger than `0.0001 m²` at a tested contact patch;
- at least 12 required deformation poses, or 16 for Champions and mobile bosses.

The pose suite includes bind, compression, extension, maximum stride, each planted contact, left/right turns, overhead reach or equivalent anatomy extreme, guarded/attack extreme, torso/axial twist, and the most demanding approved special or interaction.

Cloth and hair may use skinned, rigid segmented, or profiled secondary motion. Mobile fallback disables simulation before removing protected silhouette or clipping protection. A baseline Champion cape is a short skinned mantle or rigid segmented solution; full cloth simulation is not a dependency.

## 8. Facial rigs

- Champions use `rmc_face_champion_hybrid_v001`: eye aim, independent blink, jaw, ten visemes, and eight readable expressions.
- Humanoid NPCs use `rmc_face_npc_bones_v001`: eye aim, blink, jaw, eight visemes, and neutral/focus/pain/anger.
- Non-humanoids default to `rmc_face_creature_none_v001`. Approved jaw, lid, membrane, ear, or equivalent controls are anatomy data; a humanoid face rig is never inferred.

The mobile fallback preserves gaze, blink, jaw, speech readability, and required emotion before secondary correctives. Face curves and controls bind by deterministic IDs and paths, not blendshape index.

## 9. Sockets and attachments

Canonical sockets are non-deforming, identity-scale, stable across every mesh LOD, and parented to a canonical semantic bone path. Required categories include:

- hands and `socket_weapon_main` / `socket_weapon_off`;
- `socket_back`, `socket_chest`, `socket_pelvis`, and `socket_head`;
- `socket_cape`, `socket_cloth_waist`, and `socket_hair`;
- `socket_vfx_chest`, `socket_vfx_hand_l`, and `socket_vfx_hand_r`;
- `socket_camera_focus`;
- anatomy-specific contacts and `socket_attack_origin`.

Equipment bind offsets live in the equipment binding, not the skeleton. VFX sockets are presentation origins only. Camera focus never becomes targeting authority.

Legacy names such as `PetAnchor`, `MountAnchor`, `VFX_ChestAnchor`, `VFX_Hand_L`, and `VFX_Hand_R` require an explicit alias table during migration; the importer must not guess from case or hierarchy order.

## 10. Hitbox and collider attachment

Render topology is never collision or hitbox authority.

- Hurtboxes attach by canonical bone path or `socket_hit_<region>` and use cataloged box, capsule, sphere, or low-poly convex proxy shapes.
- Attack volumes attach to a canonical weapon or anatomy attack origin.
- Shape, offset, layer, team filter, window, damage identity, and result remain gameplay catalog data.
- Visual LOD changes never move a hitbox or collider.
- Negative transform scale is forbidden.
- An animation event can request a window. Gameplay accepts it only when actor, action sequence, phase, and catalog binding all match.
- Missing, duplicate, reordered, stale, or late events fail closed and cannot create a hit.

## 11. Root motion policy

| Category | Runtime rule | Authority and fallback |
| --- | --- | --- |
| Locomotion | In-place | Retain source trajectory for stride evidence; character motor/server moves the actor. Fall back by declared speed set, then idle. |
| Combat | Bounded root motion | Gameplay owns displacement/collision/cancel. Sample and clamp the normalized trajectory; otherwise use in-place plus motor-owned displacement. |
| Skill | Bounded root motion | Gameplay skill receipt owns movement and result. A trajectory cannot create a hit, teleport, or status. |
| Traversal | Authored root motion | Traversal controller validates start, target, sweep, cancellation, and final anchor before consumption. Invalid entry is rejected. |
| Interaction | In-place | Gameplay alignment owns the anchor; optional motion warping is bounded to the declared target. |
| NPC life | In-place | Schedule/navigation owns position; props align through sockets. |
| Reaction | In-place | Gameplay knockback, control, ragdoll, defeat, and result own displacement. |
| Cinematic | Authored root motion | Timeline owns an isolated production shot; gameplay scenes reconcile to a legal anchor. Missing final motion blocks production, while labeled previs may use a proxy. |

Vertical root translation is forbidden outside validated traversal/cinematic use. Yaw is allowed only by the selected policy. Raw `Animator.applyRootMotion` is never enabled as an undeclared default.

## 12. Retarget profiles

### 12.1 Unity Humanoid

`rmc_retarget_humanoid_shared_v001` requires the canonical A-pose, valid Avatar, complete bone/socket map, metric scale, and passed pose/contact/deformation suite. Root and motion root retain trajectory; pelvis retains necessary vertical body motion; other translation curves are rejected unless the Avatar mapping explicitly requires them.

### 12.2 Generic exact signature

`rmc_retarget_generic_exact_v001` requires exact skeleton signatures. Meshes, materials, and declared non-skeletal sockets may vary. Deform translations remain source-specific.

The approved Slagwhistle uses `rmc_retarget_slagwhistle_exact_v001`. Its retained source is recumbent and does not match the generic grounded neutral-contact bind. The representative therefore records an explicit bind-pose override, requires the Slagwhistle recumbent bind as the retarget source, and still uses the shared grounded semantic skeleton family for catalog classification. This exception does not authorize cross-retargeting to another grounded fantasy beast.

### 12.3 Generic semantic chain

`rmc_retarget_generic_semantic_v001` requires a versioned one-to-one semantic map and compatible contact count, chain order, gait, center of mass, bind pose, and facing. It retargets rotations and normalized chain extension, preserves target bind translations, then must pass 2% stride and contact tolerances.

Retarget success is proven by bind, compression, extension, contacts, turns, maximum stride, and the most extreme approved action. A T-pose-free preview alone is insufficient.

## 13. Contacts, loops, and clipping

At normal speed and the declared sample rate:

- planted horizontal drift is at most 2 cm;
- planted vertical error is at most 1 cm;
- loop position mismatch is at most 1 cm;
- loop rotation mismatch is at most 1 degree;
- retained source trajectory versus expected stride differs by at most 2%;
- start, loop, stop, turn, and transition poses preserve velocity direction and do not pop;
- interaction anchors, weapon grips, and hand/prop contacts stay within the applicable socket tolerance;
- protected body/equipment clipping satisfies section 7.

Foot or limb IK is correction, not a substitute for a valid source contact. IK weight comes from contact intervals and fades before lift/after plant. Grounding uses gameplay physical authority, not render mesh sampling.

## 14. Events and payloads

Unity clips call one function: `AL_MotionEventV1(string utf8Json)`. The payload's `eventId` resolves an event definition in the manifest. The function name is stable; event semantics are versioned catalog IDs.

The runtime handler payload includes:

- `schemaVersion` = 1;
- canonical `eventId`;
- monotonic actor-owned `actionSequence`;
- zero-based `eventOrdinal` within the clip;
- `normalizedTime` as evidence only;
- the event-specific `phase`, `contactId`, `windowId`, or `cueId`.

Canonical names are:

- `al.motion.phase.enter` / `al.motion.phase.exit`
- `al.motion.contact.begin` / `al.motion.contact.end`
- `al.motion.hitbox.request_begin` / `al.motion.hitbox.request_end`
- `al.motion.interruptible.begin` / `al.motion.interruptible.end`
- `al.motion.vfx.request`
- `al.motion.audio.request`

Events are authored on integer source frames as event-definition ID, source frame, clip-local ordinal, and event-specific static payload. The runtime injects `schemaVersion`, canonical `eventId`, actor-owned `actionSequence`, and normalized time; those runtime identity fields are never baked into a reusable clip. Runtime ordering is action sequence, clip-local time, then ordinal. Deduplication key is actor instance + action sequence + event ID + ordinal. Playback-speed changes affect presentation time, not event order or gameplay timing.

## 15. Skill phases

Every skill declares all ten canonical phases:

1. `anticipation` — required
2. `cast` — required
3. `channel_start` — conditional
4. `channel_loop` — conditional
5. `commit` — required
6. `release` — conditional
7. `impact` — conditional
8. `recovery` — required
9. `interruption` — conditional
10. `cancellation` — conditional

A conditional phase is either required with a clip binding or explicitly not applicable with gameplay-backed rationale. `channel_loop` loops only while gameplay channel state remains active. `commit` mirrors a gameplay receipt. `impact` follows authoritative contact/result evidence. Clip length never changes configured cast, channel, cooldown, cost, or result timing.

The previous five-phase taxonomy maps as follows: anticipation → anticipation; cast → cast; channel → channel start/loop; release → commit/release; recovery → recovery. Impact, interruption, and cancellation are explicit rather than hidden in clip transitions.

## 16. Required motion coverage

The machine-readable manifest is authoritative for exact keys and applicability.

### 16.1 Champion

Required categories include neutral/variant idle; walk/run/sprint; starts/stops/turns/strafes; jump/fall/land; dodge/block/parry; draw/stow; basic/chain/charged/heavy attacks; hit/knockdown/get-up/defeat; interaction/emote/traversal; and all applicable skill phases.

### 16.2 NPC

NPCs include the Champion technical floor plus talk, gesture, sit, sleep, work, carry, gather, trade, craft, react, flee, defend, role actions, and applicable skill phases. A genuinely noncombat role records exact not-applicable combat keys and an approved role binding; absence is not permission.

### 16.3 Fantasy beasts and monsters

General beast coverage includes idle variants, turn, every declared locomotion mode, basic/special attack, hit/stagger/defeat, interactions/traversal when capable, and all declared skill phases. Monsters additionally require alert. Boss is a rank within the monster subject kind, not a fifth subject kind; a boss-ranked monster binds the boss required-motion set and additionally requires enter, phase, transition, and every named transition.

### 16.4 Slagwhistle source-bounded set

The approved bounded Slagwhistle source permits no more than six presentation clips:

1. rest/vent → `idle.neutral`
2. scurry → `locomotion.walk`
3. plant-stop → `locomotion.stop`
4. cut → `interaction.cut`
5. spoil-push → `reaction.spoil_push`
6. turn → `locomotion.turn`

Attack, special attack, defeat, burrow, standing rest, and any seventh clip are `blocked_owner_authorization`. The preparation-held four-realm taxonomy's generic beast rows do not override this source-specific restriction.

## 17. Layers and avatar masks

Masks list canonical bone paths and are versioned data. Missing paths fail import; the runtime never rebuilds a mask by child order.

- Champion mobile: full-body base, upper-body action override, additive aim/look, additive reaction; at most four simultaneous layers.
- NPC mobile: full-body base, upper-body action override, additive look/reaction; at most three.
- Beast mobile: full-body base and additive reaction; at most two.
- Monster mobile: full-body base, upper-body action override, additive reaction; at most three. Boss-ranked monsters remain subject kind `monster` and use the boss budget/set rather than a separate layer subject kind.

Additive clips use a declared reference pose and zero root/motion-root deltas. Lower-body locomotion remains motor aligned. Face/secondary animation is profile-driven and may be culled by distance without changing gameplay cues.

## 18. Interruption, cancellation, recovery, and fallback

Transition priority is:

`defeat > hard_control > interruption > reaction > skill > attack > interaction > traversal > locomotion > idle`

Before commit, cancellation may suppress the gameplay result only when gameplay accepts cancellation. It closes request-only windows, emits cancellation once, and blends through the declared cancel/recovery motion.

After commit, the result remains authoritative. Interruption closes presentation windows, emits interruption once, and blends to the highest-priority legal reaction or recovery without replaying commit.

- Maximum blend-out: 0.15 seconds.
- Maximum bounded recovery: 0.75 seconds.
- Missing optional clip: reject the optional action or use its declared safe fallback.
- Missing required base clip, skeleton, mask, or binding: block admission.
- No path may expose bind pose or freeze indefinitely.
- Idle is a runtime safe state, not a substitute for a required committed action.

## 19. Deterministic identifiers and catalog binding

Canonical IDs use `rmc_<kind>_<slug>_v<NNN>`. IDs are immutable and never repurposed. Meaning, hierarchy, bind pose, event payload, or compatibility changes create a new revision.

Bindings resolve exact IDs for:

- character/entity asset;
- retained source and provenance;
- skeleton and skeleton signature;
- bind pose and retarget profile;
- facial, layer, socket, hitbox, and budget profiles;
- required motion set;
- each clip, motion key, root policy, event definition, and clip signature.

Clip signature algorithm is SHA-256 over skeleton signature, sample rate, frame count, canonical curve paths, and frame/ordinal/payload events. A null signature means source candidate only. Arrays may be sorted for stable serialization, but runtime meaning never depends on that order.

A clean import or build must produce the same exact ID graph and signatures. Unity GUIDs/local IDs may cache a resolved asset but cannot replace canonical IDs in a catalog or save.

## 20. Mobile budgets

These are technical admission ceilings, not fill targets and not creative approval.

| Scope | LOD0 tris | Materials | Deform bones | Animated transforms | Resident clips | Compressed clip memory | Active layers |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Champion | 60,000 | 3 | 89 | 110 | 48 | 12 MiB | 4 |
| Important NPC | 45,000 | 3 | 72 | 96 | 32 | 8 MiB | 3 |
| Ambient beast | 25,000 | 2 | 42 | 56 | 16 | 4 MiB | 2 |
| Monster / elite | 45,000 | 3 | 72 | 96 | 32 | 8 MiB | 3 |
| Mobile boss | 45,000 | 3 | 96 | 128 | 48 | 16 MiB | 4 |

All use at most four influences per vertex and 30–60 Hz source sampling. A more specific approved packet may be stricter. Increasing a ceiling requires measured physical-device evidence and a new version; a source that exceeds a ceiling is not silently downsampled on import.

Slagwhistle has a stricter six-clip source ceiling regardless of the general beast resident limit.

## 21. Representative dispositions

### 21.1 Humanoid Champion — shared Vanguard

Target profile: shared Humanoid skeleton, canonical A-pose, Champion face/layers, Champion mobile budget, complete Champion set.

Current disposition: blocked. The source is useful bounded engineering scaffolding, but lower LODs exceed four influences, there is no valid Humanoid Avatar, and the required motion set is missing. Cleanup creates a new version and preserves v001.

### 21.2 Humanoid NPC — Covenant Sentinel

Target profile: shared Humanoid skeleton, canonical A-pose, NPC face/layers, NPC mobile budget, complete NPC set.

Current disposition: blocked for production motion. One Generic walking take is a source candidate, not qualified coverage. Rebind and cleanup must retain provenance, create explicit roots/sockets/clips, validate contacts/loops, and compare to the admitted MVP visual before replacement.

### 21.3 Non-humanoid fantasy beast — Slagwhistle

Target profile: grounded non-humanoid skeleton with exact-signature retargeting, source-specific recumbent bind, creature face-none profile, beast layers/budget, and six-key source-bounded set.

Current disposition: blocked pending root/hierarchy cleanup and six authored/validated clips. Its anatomy and authorization exceptions are mandatory, not optional waivers.

## 22. Promotion gate

A production binding passes only when all applicable evidence exists:

1. exact source, hash, rights/provenance, asset ID, and approval state;
2. metric transforms, axes, ground/support datum, roots, names, hierarchy, and skeleton signature;
3. bind pose, weights, topology, deformation, seams, clipping, and LOD budgets;
4. sockets, equipment offsets, colliders, hitboxes, and LOD-independent attachment paths;
5. retarget pose suite, loops, contacts, stride, turns, starts/stops, and root trajectory;
6. every required motion and applicable skill phase bound by exact clip ID/signature;
7. event payload/schema/order/deduplication and authoritative gameplay synchronization;
8. avatar masks, additive references, transitions, interruption, cancellation, recovery, and missing-clip fallback;
9. fresh Unity import and clean-build deterministic lookup with no T-pose or import-order dependency;
10. physical mobile-floor memory/performance evidence and owner creative/release approval.

A weighted score cannot hide a missing required field. Any hard failure leaves admission blocked.

## 23. Validation commands

Focused semantic and fail-closed tests:

`uv run --with jsonschema python -m unittest discover -s unity/SharedContracts/Tests -p "test_rig_motion_standard.py" -v`

Direct validation:

`uv run --with jsonschema python unity/SharedContracts/Tests/rig_motion_standard.py`

Full shared-contract validation:

`uv run --with jsonschema python unity/SharedContracts/Tests/validate.py`

The validator rejects malformed IDs, duplicate IDs, missing references, cyclic skeletons, wrong roots, missing socket/profile bindings, event-name drift, incomplete skill phases, undefined required motion keys, unclassified representative requirements, unauthorized Slagwhistle motions, absent clip signatures marked qualified, and acceptance counters that do not match computed evidence.
