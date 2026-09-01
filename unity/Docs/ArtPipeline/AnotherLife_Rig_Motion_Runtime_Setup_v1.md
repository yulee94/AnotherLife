# AnotherLife Rig and Motion Runtime Setup v1

This runtime implements the contracts in:

- `Assets/AL/StreamingAssets/GameData/al_rig_motion_standard.json`
- `Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json`
- `Docs/ArtPipeline/AnotherLife_Rig_Motion_Catalog_Production_Standard_v1.md`
- `Docs/ArtPipeline/Templates/Rig_Motion_Asset_Binding_Template_v1.json`

Catalog IDs, motion keys, skeleton profiles, retarget profiles, bind poses, sockets, contacts, layers, and event definition IDs remain owned by those sources. Runtime code must not invent replacements.

## Generate the checked-in configuration

Use `Another Life > Motion > Build Default Runtime And Import Profiles` in the Unity Editor, or run:

```text
Unity.exe -batchmode -nographics -projectPath <unity-project> -executeMethod AL.Editor.Motion.MotionConfigurationBuilder.BuildForCli -quit -logFile <log-path>
```

The builder writes deterministic assets to:

- `Assets/AL/Resources/Motion/Profiles/`
- `Assets/AL/Editor/Motion/ImportPresets/`
- `Assets/AL/Editor/Motion/MotionImportPresetRegistry.asset`

Generation fails if the required motion manifest is missing or a generated technical identity is invalid. The generated controller profiles retain a direct `TextAsset` reference to the required manifest so event definition IDs resolve to canonical event names without duplicated data.

The import registry is intentionally empty by default. Add a binding only after an exact remediated source path, clip range, technical identity, and event payload have been approved. A path not present in the registry receives no motion-specific import override.

## Champion setup

1. Use `ChampionMotionControllerProfile.asset`.
2. Import the shared Champion skeleton as Humanoid with:
   - skeleton profile `rmc_skeleton_humanoid_shared_v001`
   - retarget profile `rmc_retarget_humanoid_shared_v001`
   - `HumanoidMotionImportPreset.asset`
3. Preserve the canonical bind pose, material slots, blend shapes, socket bones, contact bones, and hierarchy required by the authoritative standard.
4. Configure the controller's base layer plus upper-body, reaction, and facial/additive layers from the profile. Assign approved `AvatarMask` assets before production use; generated masks are deliberately null rather than guessed.
5. Build a `MotionCatalogSnapshot` from admitted clip bindings. The catalog's safe key is `idle.neutral`.
6. Attach `MotionRuntimeController` beside the `Animator`, call `Configure`, then inject event timelines, the profile-derived `MotionEventNameRegistry`, root-motion authority, sockets, and grounding services.
7. Gameplay owns action sequence, commit, cancellation, interruption, and root displacement acceptance. Presentation does not cancel gameplay or apply combat effects directly.

## NPC setup

1. Use `NpcMotionControllerProfile.asset` and the same shared Humanoid skeleton/retarget/import preset when the asset passes the canonical Humanoid gates.
2. NPCs use the base, upper-body, and reaction layers. Do not silently add the Champion facial layer to lower-cost NPC profiles.
3. Admit exact NPC motion keys and clip bindings into the catalog. Missing optional keys resolve through their explicit fallback and ultimately `idle.neutral`; they do not freeze the Animator or expose a bind/T-pose state.
4. Reuse the Champion event, socket, grounding, and action-sequence contracts. Reduce layer count and clip breadth before weakening technical identity or gameplay readability.

## Non-humanoid fantasy-beast and monster setup

1. Use `BeastMotionControllerProfile.asset`.
2. Import as Generic with `GenericExactMotionImportPreset.asset` only when skeleton signature, bind pose, hierarchy, and retarget profile match exactly.
3. Use `SlagwhistleExactMotionImportPreset.asset` only for the approved Slagwhistle signature. It is not a general fantasy-beast retarget preset.
4. Preserve generic limb chains, grounded contact bones, canonical sockets, and animation-authored facing. Never force a non-humanoid fantasy beast through the Humanoid Avatar path.
5. The generated beast profile uses base and reaction layers. Additive masks and clip families must be asset-specific and approved.
6. Configure generic limb IK through `MotionGroundingDriver`; invalid or missing contacts fail closed and do not move gameplay authority.

## Motion requests, transitions, and recovery

- Resolve motion by exact ordinal key. Optional aliases are not accepted.
- Every admitted clip carries a fallback key; fallback chains are cycle-checked and terminate at `idle.neutral`.
- Submit monotonically increasing gameplay action sequences to `RequestMotion`.
- Priority arbitration rejects lower-priority requests. Pre-commit cancellation and post-commit interruption have distinct outcomes. Gameplay may reject either.
- Call `MarkCommitted` only after gameplay commits the action.
- Call `CompleteCurrent` to enter deterministic recovery and return to the safe locomotion/idle state.
- Playback speed must remain positive. Timeline collection uses runtime time and playback speed, with actor/action/event deduplication.

## Root motion, turning, stride, and grounding

- In-place clips discard authored translation while retaining allowed facing policy.
- Bounded clips clamp horizontal displacement and yaw to controller-configured limits.
- Vertical root motion is accepted only when the gameplay root-motion consumer explicitly allows it.
- Turn and stride adjustments use deterministic bounded helpers; they do not rewrite the catalog.
- Foot and generic-limb contacts resolve by exact canonical bone name. Ground probes and IK weights fail closed when a binding or hit is invalid.

## Sockets and attachments

Build `MotionSocketRig` from canonical socket IDs and exact skeleton bone names. Attachment requests resolve only known socket IDs. Missing sockets return failure; attachments are never silently parented to the actor root.

## Events and hitbox windows

Imported clip events call `AL_MotionEventV1` on `MotionRuntimeController` with the canonical schema-version-1 JSON payload written by `MotionModelImportPostprocessor`. The imported payload stores `actionSequence: 0`; the controller replaces that static placeholder with the authoritative active gameplay sequence before dispatch.

The controller:

1. validates the schema version;
2. resolves `eventDefinitionId` through the canonical required manifest;
3. binds the current gameplay action sequence;
4. deduplicates by actor, action, event definition, and clip-time ordinal;
5. forwards the dispatch to presentation listeners and the configured `MotionWindowTracker`.

Hitbox begin/end events are requests only. `MotionWindowTracker` opens a window only when gameplay authority accepts the action sequence and window ID. Cancellation, interruption, recovery, disable, and destruction close presentation-owned windows.

## Runtime lifecycle

`MotionRuntimeController.Configure` creates a manual-update `PlayableGraph`. `Tick` evaluates it, dispatches ordered events, updates blends, and wraps approved loops. `Release`, `OnDisable`, and `OnDestroy` close windows and destroy the graph idempotently.

Do not keep a graph alive across an Animator replacement or domain lifecycle boundary. Reconfigure it from admitted catalog/profile data.

## Required verification

Before admitting a new rig or motion packet:

1. regenerate configuration and confirm the builder log contains `[AL-MOTION-CONFIG]` with no compiler errors;
2. run `AL.Tests.EditMode.Animation.MotionRuntimeTests`;
3. inspect the imported model and clip settings against the exact registry binding;
4. exercise fallback, transition, cancellation, recovery, additive mask, root-motion, contact/IK, socket, event ordering, playback-speed, and hitbox-window cases;
5. verify the controller releases its `PlayableGraph` when disabled or destroyed;
6. confirm no bind/T-pose, frozen Animator, duplicate event, gameplay-authority bypass, or unbounded root displacement occurs.
