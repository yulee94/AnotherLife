# Reusable Architecture Construction-State System

**Status:** Owner-approved technical direction; shared runtime foundation and live confirmed-level production adapter implemented

**Date:** 2026-07-24

**Primary Codex mode:** Engineering

**Upstream design authority:** Root `DESIGN.md`, `Assets/AL/Art/Designs/FourRealmArchitecture.md`, `Kingdom_Building_Level_And_Placement_Design.md`, and the realm animation contracts in this folder

The architecture animation system uses one deterministic construction lifecycle for every realm. A realm-specific profile supplies motion character and timing, while a bounded optional activity component supplies distinctive operational behavior such as forge activity, guided sap, or calibrated energy.

This decision prevents four incompatible state machines without flattening Stonehold, Eldergrove, Crownlands, and Umbral into the same animation.

## Shared lifecycle

| Shared runtime state | Stonehold presentation | Eldergrove presentation | Crownlands presentation | Umbral presentation |
| --- | --- | --- | --- | --- |
| `SitePrepared` | `PlotPrepared` | `PlotPrepared` | `PlotPrepared` | `BoundaryMarked` |
| `BaseStructureEstablished` | `FoundationSeated` | `CraftFrameSet` | `CivicFrameRaised` | `OffsetShellRaised` |
| `SignatureStructureEstablished` | `WallShellLocked` | `GuidedRootGrowth` | `SilverRibsLocked` | `VeilAnchorsBound` |
| `UpperStructureEstablished` | `RoofAndChimneySet` | `RootVaultSettled` | `RoofAndLanternSet` | `SplitRoofsSealed` |
| `FitoutCompleted` | `FittedOut` | `RoofAndLanternSet` | `InstrumentsGrounded` | `ReliquariesGrounded` |
| `Operational` | `Operational` | `CultivationOperational` | `CalibratedOperational` | `VeilConvergenceOperational` |

The shared enum is a presentation boundary resolved from authoritative gameplay
level and active-order progress. It is never persisted as a separate visual
stage. Player-facing labels remain realm-specific.

## Implemented runtime boundary

- Shared state controller: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/ArchitectureConstructionAnimationController.cs`
- Shared profile contract: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/ArchitectureConstructionAnimationProfile.cs`
- Live confirmed-level adapter: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/KingdomBuildingConfirmedLevelTransition.cs`
- Packaged production/profile manifest: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/KingdomBuildingModelCatalog.cs`
- Eldergrove operational activity: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/EldergroveAtelierStableActivity.cs`
- Crownlands operational activity: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/CrownlandsStormwrightStableActivity.cs`
- Umbral operational activity: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/UmbralVeilwrightStableActivity.cs`
- Crownlands profile: `Assets/AL/Art/Generated/Architecture/Profiles/Crownlands_Stormwright_ConstructionProfile.asset`
- Stonehold profile: `Assets/AL/Art/Generated/Architecture/Profiles/Stonehold_Workshop_ConstructionProfile.asset`
- Eldergrove profile: `Assets/AL/Art/Generated/Architecture/Profiles/Eldergrove_Atelier_ConstructionProfile.asset`
- Umbral profile: `Assets/AL/Art/Generated/Architecture/Profiles/Umbral_Veilwright_ConstructionProfile.asset`
- Umbral generated graybox: `Assets/AL/Art/Generated/Architecture/Umbral/Umbral_Veilwright_AnimationPrototype.prefab`
- Umbral isolated preview: `Assets/AL/Scenes/Prototypes/UmbralVeilwrightAnimationPrototype.unity`
- Stonehold generated graybox: `Assets/AL/Art/Generated/Architecture/Stonehold/Stonehold_Workshop_AnimationPrototype.prefab`
- Stonehold isolated preview: `Assets/AL/Scenes/Prototypes/StoneholdWorkshopAnimationPrototype.unity`
- Eldergrove generated graybox: `Assets/AL/Art/Generated/Architecture/Eldergrove/Eldergrove_Atelier_AnimationPrototype.prefab`
- Eldergrove isolated preview: `Assets/AL/Scenes/Prototypes/EldergroveAtelierAnimationPrototype.unity`

The Stonehold, Eldergrove, Crownlands, and Umbral profiles, activities,
grayboxes, and isolated scenes establish the approved motion direction and
deterministic ownership proof for all four realms. The four Workshop production
models now bind those profiles through the packaged model catalog. Exact
gameplay seconds and measured device performance remain separate authorities.

## Shared responsibilities

The reusable controller owns:

- deterministic evaluation at any resolved construction state;
- autoplay, explicit playback, looping for isolated review, and sleeping after non-looping completion;
- reduced-motion stage snapping;
- one profile-owned cutaway stage, optional supplemental inspection groups, and an authored review cutaway window;
- stage visibility, rigid entry transforms, and settled-pose restoration;
- dispatch to optional realm activity components;
- no per-module `Animator` requirement.

Realm profiles own:

- stable profile and realm identity;
- review duration and stage rhythm;
- rigid entry offset, rotation, and scale character per stage;
- cutaway-stage ownership;
- the handoff point for operational activity.

Realm activity components may own only bounded distinctive behavior. They cannot create a parallel construction lifecycle or silently change the persistent state.

## Production presentation boundary

- The four prototypes define realm construction-motion grammars, not final
  building models.
- A `0 → 1` construction may use the complete six-state grammar.
- A later `N → N+1` upgrade keeps the confirmed Level N structure settled and
  keeps the target delta hidden until gameplay confirms Level N+1.
- A newly confirmed adjacent level in the current board session applies one
  short realm-profile rigid settle to only the new production-model delta.
- Fresh loads, stream-ins, reconnects, offline reconciliation, same-level
  refreshes, and multi-level jumps appear settled and do not replay motion.
- A presentation adapter maps immutable confirmed gameplay level and upgrade
  status into live production presentation. The controller never reads or
  writes saves, spends resources, completes construction, or advances quests.
- Stable building-slot identity determines placement. Collection order never
  determines grid position, footprint, rotation, or entrance orientation.
- Final model resolution uses realm, building definition, confirmed level, and
  quality tier while keeping realm motion profile identity separate.

## Mobile and compatibility impact

- One controller shape is reused across realm buildings.
- Profiles are small serialized data assets and add negligible runtime memory or install size.
- The controller allocates its stage and activity caches once, precomputes every part's entry transform, and performs no intentional per-frame heap allocation.
- Visibility and cutaway ownership change only when an object's active state changes.
- Generated graybox materials enable GPU instancing, and prototype renderers opt out of unused motion-vector and probe work.
- Static modules do not receive independent continuously updating `Animator` components.
- Completed non-looping buildings disable their controller.
- The live delta adapter adds one component only for a newly confirmed adjacent
  level, caches at most four LOD transforms, and disables itself after at most
  `1.25` seconds.
- Reduced-motion, far-distance, and off-screen policies remain shared.
- No package or dependency was added.
- No save field, gameplay construction timer, economy value, or navigation
  state changed.

The Android/iOS compatibility proof and remaining device-validation boundary are recorded in `Architecture_Mobile_Compatibility_Handoff.md`.

Final device performance remains unmeasured until representative production meshes, materials, VFX, and multiple visible buildings exist.

## Realm-specific extension rules

- **Stonehold:** retain rigid seating, leverage, restrained impact, forge cues, and immovable completed mass.
- **Eldergrove:** use an authored root rig or staged meshes beside the shared lifecycle; unrestricted procedural root generation is not allowed.
- **Crownlands:** retain the fixed conductor route and short calibrated pulse beside the shared lifecycle.
- **Umbral:** retain offset grounded installation, four fixed anchors, one inward convergence, one local eclipse ring, one core-to-chimney confirmation, and a long silent hold. Its Workshop production model is bound; populated-device profiling remains required.

## Critical direction changes

The following require project-owner approval:

- replacing the shared lifecycle with separate incompatible realm state machines;
- using a realm profile to change gameplay construction timing or persistence;
- procedural runtime lightning or unrestricted procedural structural growth;
- procedural Umbral route generation, screen-wide darkness, or repeated chimney flashes;
- continuously animating load-bearing completed structure;
- adding an always-running `Animator` to every static building module;
- allowing presentation motion to mutate production saves, economy,
  navigation, quests, timers, or live kingdom progression;
- persisting the shared visual state separately from authoritative gameplay;
- placing buildings from collection order instead of stable slot identity;
- replaying full-building construction for every later level instead of animating the approved module delta.
