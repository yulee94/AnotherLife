# Phase 1 NVS-01 Status

**Status date:** 2026-07-15  
**Current integration branch:** `main`  
**Audited current-main head:** `3c695ae289acabcfd8750bd6a2f0811ebdfb24cd`  
**Roadmap state:** the Unity compilation gate is complete; Phase 1 remains paused behind serialized-asset recovery issue #156  
**Immediate owner:** Codex under issue #156

`AGENTS.md` is authoritative. This record distinguishes source presence, issue state, merge state, validation evidence, and actual player-visible completion.

Use this document with:

- `AGENTS.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`
- issue #138 for the approved D1–D16 product decisions
- active recovery and foundation issues referenced below

## Current repository state

### Completed compile recovery

PR #147 was reduced to the required one-file mechanical repair and squash-merged as:

```text
3c695ae289acabcfd8750bd6a2f0811ebdfb24cd
```

The merged diff changes only:

```text
unity/Assets/AL/Scripts/Kingdom/Story/LocalStoryService.cs
```

It replaces obsolete `AL.Data.Definitions.DialogueChoice` references with the imported `AL.Data.Definitions.Narrative.DialogueChoice` type. Dialogue text, IDs, choices, outcomes, saves, metadata, assets, and PlayMode infrastructure are unchanged.

Issue #145 is closed. Its recorded evidence remains:

- pre-fix Unity `CS0234` diagnostics;
- successful post-fix Unity `2022.3.62f3` compilation;
- EditMode result: 6 total, 6 passed, 0 failed.

Safe PlayMode coverage was not claimed by #145 and remains issue #127.

### Pull-request state

There are currently **no open pull requests**.

The speculative visual stack was closed unmerged:

```text
#149 → #154 → #157 → #162 → #164 → #167
→ #170 → #175 → #179 → #182 → #185
```

The useful visual ideas are preserved only as historical references under deferred/not-planned issues #151, #160, and #187. None of those branches holds a shared-file lock, and none should be retargeted or cherry-picked wholesale.

### Shared-file state

Current designated shared-file soft locks: **none**.

Designated shared integration files remain:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

The first approved open implementation PR declaring one of these files holds its soft lock.

## Active Phase 0 asset-health gate

### #156 — QuestDefinition authority and serialized GUID migration

**Status:** Ready and active  
**Owner:** Codex  
**Branch:** `codex/consolidate-quest-definition-assets`  
**Base:** fetched current `main` at or after `3c695ae289acabcfd8750bd6a2f0811ebdfb24cd`

Before merged PR #124, two `QuestDefinition` script identities existed:

```text
AL.Data.Definitions.QuestDefinition
GUID 226022aa7500f3e4abc8ac3757707ad8

AL.Data.Definitions.Narrative.QuestDefinition
GUID c385b2b183b74184ca75eeffbe2256ef
```

PR #124 removed the root type and changed `LocalQuestService` to the narrative type. Compilation success does not prove that tracked ScriptableObjects, prefabs, or scenes still resolve their original script identity.

#156 must:

1. inventory every tracked reference to both GUIDs;
2. inventory all QuestDefinition assets, source references, generators, importers, schemas, and catalogs;
3. choose one final technical authority based on evidence;
4. migrate tracked serialized references deterministically when required;
5. preserve all valid IDs, text, conditions, rewards, and unknown serialized fields;
6. add guards against duplicate type/GUID/quest-asset ID regressions;
7. recompile and reimport in Unity `2022.3.62f3` with exact EditMode/editor evidence.

Do not blindly replace the surviving `.meta` GUID. If no tracked asset references the removed GUID, record that evidence and add authority/GUID regression checks without inventing a migration.

No A1 packet or production Player build may claim a trusted Unity asset base until #156 is complete.

## Independent foundations now ready after #145

No implementation PR is currently open for these lanes. Each must start from fetched current `main`, use one focused branch, and avoid shared-file overlap.

### #127 — profile-safe PlayMode smoke

The current PlayMode test loads `Assets/Test.unity` by editor path but still lacks:

- developer-profile snapshot/isolation/restoration;
- protection for both `save.previous.json` and legacy `save.json.previous`;
- bounded scene-load timeout;
- global log/time-state restoration;
- static `ServiceLocator` cleanup;
- proof that no extra save artifact remains;
- stable structural startup assertions;
- accepted current-main PlayMode XML evidence.

Issue #127 is ready. It must not edit production Build Settings or runtime behavior.

### #136 — reputation, faction, and persona save compatibility

The three missing-field defaults exist, but acceptance still requires:

- real service mutations after normalization;
- preservation of existing values;
- save/reload round trip;
- idempotent normalization;
- Unity compile and focused EditMode evidence.

Issue #136 is ready and must remain separate from broad save hardening.

### #152 — quest-state save compatibility

Current quest services can dereference null entries or blank IDs and can index a missing definition during reward claim. #152 must define and test null, blank, unknown, duplicate, and downgrade-safe quest-state behavior without changing narrative meaning.

Issue #152 is ready. Its semantic policy is required before #137 finalizes save validation.

### #153 — Bootloader service-stack integrity

`Bootloader.InitializeIfMissing()` currently treats one registered `IResourceService` as proof that the complete service graph exists. #153 must establish deterministic full-stack readiness, coherent root dependencies, visible failure, and empty/full/partial/repeated initialization tests.

Issue #153 is ready. Any implementation PR must declare the `Bootloader.cs` shared lock.

### Independent Android and repository lanes

The following may proceed after checking file overlap:

- #148 — restore Android `Quest` positional constructor compatibility;
- #155 — repository/Android CI, staged Unity reporting, and merge controls;
- #159 — remove dynamic Android dependency versions and verify the resolved graph;
- #161 — hide narrative debug routes and arbitrary node triggers outside debug/internal builds.

## Save-hardening dependency

Issue #137 remains blocked until both #136 and #152 are complete.

Its verified gaps include:

- unvalidated primary bytes can overwrite the last-known-good backup;
- ordinary `IOException` failures are treated as unsupported-operation fallback;
- load/recovery and save status are conflated;
- recovery messages can be overwritten by an internal save;
- first successful save does not establish the documented backup generation;
- installed primary and backup are not fully revalidated;
- `DeleteSave()` leaves previous/quarantine artifacts;
- offline progress mutates before durable persistence without rollback;
- current fallback writes `save.json.previous` while the approved model uses `save.previous.json`;
- quarantine failure can lead toward unsafe profile recreation;
- the required fault-injection, semantic-validation, duplicate-safety, and deletion matrices are absent.

## NVS-01 narrative and runtime chain

### #128 — clean Android Studio A1 packet

Issue #128 is blocked by #156.

The merged `NVS_01_Packet.kt` remains the archived pre-approval packet and conflicts with issue #138:

- wrong chapter/realm start context and automatic start;
- missing `DLG_OMEN_1_ARENA_START`;
- affinity at acceptance instead of report completion;
- automatic success dialogue instead of manual report;
- Tear granted at report completion instead of arena success;
- contradictory failure/retry behavior;
- abandonment prohibited;
- incomplete localization inventory;
- incomplete D16 resume model.

The archive is source history, not approved A1 authority.

### #133 — GPT G1 specification

Issue #133 is blocked only by an approved #128 artifact. G1 must not invent or repair narrative. It translates the exact A1 packet into:

- one versioned runtime content authority;
- strict validation/error behavior;
- deterministic state/objective/dialogue behavior;
- typed encounter request/result and session correlation;
- artifact/report semantics;
- persisted D16 resume state;
- atomic and idempotent consequence orchestration;
- explicit files, shared locks, tests, rollback, and C1–C4 order.

### #134 — Codex C1–C4

Issue #134 remains blocked by approved G1 and by the foundations G1 identifies as required.

The minimum known prerequisites for the approved OMEN_1 consequences include safe quest/save state, resource mutation, relationship mutation, realm identity, encounter lifecycle, and visible failure behavior. Existing focused issues include:

- #127, #136, #137, #152, #153;
- #163 — economy integrity;
- #173 — realm selection integrity;
- #176 — relationship integrity;
- #177 — player notifications;
- #180 — Champion combat and encounter integrity;
- #183 — game-data authority.

G1 must distinguish true NVS-01 blockers from broader release or later-phase quality work; issue existence alone does not automatically place every lane on the critical path.

## Production and integration lanes

### #150 — production scenes and Player build

Issue #150 is blocked by #156. Current `EditorBuildSettings.asset` has `m_Scenes: []` while runtime controllers load scenes by name.

After #156, #150 must inventory every committed scene and string-loaded dependency, define the minimal production flow, keep `Assets/Test.unity` excluded, add deterministic Build Settings validation, build a development Player, and prove the first transition.

### #135 — packaged Android↔Unity bridge

The Android reflection host is not a packaged or mounted end-to-end bridge. There is no Unity export module, shell route, Unity request consumer/result producer, stable session identity, or device/lifecycle evidence.

#135 remains deferred until standalone NVS-01 and #150 are complete.

### Additional integrity and release issues

The source audit also isolated focused issues for economy, progression, territory, loot, Realm Gems/Wishgate, Warmaster, world-state lifecycle, realm selection, deterministic battle results, relationships, notifications, release-only command gating, Champion combat, world atlas, game-data authority, customization, and Android quest preview:

```text
#163 #165 #166 #168 #169 #171 #172 #173 #174
#176 #177 #178 #180 #181 #183 #184 #186
```

These issues preserve verified defects and acceptance criteria. They must be sequenced by actual file overlap and by the G1/release plan rather than implemented as one broad backlog PR.

## Correct dependency order

```text
#145 — Unity namespace compile repair
  COMPLETE at 3c695ae289acabcfd8750bd6a2f0811ebdfb24cd
        ↓
#156 — QuestDefinition authority and serialized-asset migration
  ACTIVE
        ↓
trusted Unity source/asset baseline

Parallel foundations available now:
  #127  profile-safe PlayMode
  #136  relationship-field normalization evidence
  #152  quest-state compatibility
  #153  service-stack integrity
  #148  Android constructor compatibility
  #155  CI/merge controls
  #159  Android dependency reproducibility
  #161  Android debug-route gating

#136 + #152
        ↓
#137 — crash-safe save persistence

After #156:
  #128 — clean D1–D16 A1 packet
  #150 — production scenes and Player build
  catalog/authority work that explicitly depends on #156

#128
  ↓
#133 — GPT G1
  ↓
required technical foundations identified by G1
  ↓
#134 — Codex C1–C4
  ↓
G2 → A2 → U1
  ↓
#135 — packaged Android↔Unity bridge
```

## Evidence rules

A task is complete only when its own acceptance criteria are met. The following are insufficient by themselves:

- an issue being closed;
- a pull request being merged;
- a source or test file existing;
- Android compilation for a Unity change;
- compilation without serialized-asset, persistence, duplicate-safety, packaging, or player-visible evidence;
- a skipped or unavailable job represented as passing;
- a document whose producer/consumer is not implemented;
- a visual prototype based on a rejected ancestor branch.

Every completion report must identify exact base/head SHA, changed files, commands, exit codes, test totals, retained artifacts, unperformed validation, ownership boundaries, shared locks, and remaining risk.

## Current next action

```text
Owner: Codex
Issue: #156
Base: fetched current main at 3c695ae289acabcfd8750bd6a2f0811ebdfb24cd
Branch: codex/consolidate-quest-definition-assets
First deliverable: complete two-GUID, asset, source, generator, schema, and catalog inventory plus proposed authority decision
Prohibited: blind GUID replacement, narrative edits, save changes, Build Settings, Champion work, visual work, or broad catalog migration
Review gate: return inventory and authority decision to GPT before marking the PR ready
```
