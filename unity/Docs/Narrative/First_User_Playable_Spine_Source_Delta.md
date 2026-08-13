# First-User Playable Spine Source Delta

> Status: `DRAFT_NARRATIVE_SOURCE_FOR_COORDINATION_REVIEW`
> Packet ID: `first-user-playable-spine-source-delta-2026-08-13-v001`
> Verified baseline: `main@238c7e32d2f3d33e4da6e186ae34ed279b09f35e`
> Primary delivery mode: Codex narrative/content
> Controlling journey: issue `#467`
> Related continuity and implementation boundaries: issues `#134`, `#274`, `#284`, `#173`, and `#184`
> Dependency only: issue `#477`
> Scope: this Markdown file only; no runtime, save, schema, catalog, asset, workflow, or shared-file-lock change

## 1. Decision and authority boundary

This packet defines the narrative and localization-facing semantics for the smallest
playable first-user spine:

`looping install/patch cinematic -> truthful Finished Loading interaction -> Realm -> Race & Class -> customization -> username -> verified onboarding commit and local projection -> first 3D world entry -> TUTORIAL_FIRST_WORLD_ENTRY -> OMEN_1 Offered -> MQ_C1_PROOF_OF_WORTH -> existing main-quest chain`

The tutorial is a bounded onboarding sequence, not a new main quest or side quest.
The main-story handoff reuses `OMEN_1` and `MQ_C1_PROOF_OF_WORTH`. The early
Lord appointment, kingdom grant, Kingdom Management unlock, and guided mode
round trip extend the existing `MQ_C1_PROOF_OF_WORTH` source rather than
creating a parallel quest.

This packet establishes semantic referents, stable IDs, objective order, retry and
resume meaning, and localization keys. It does not establish final localized values,
dialogue, NPCs, lore, rewards, class abilities, numeric effects, balance, UI layout,
runtime contracts, persistence fields, provider choices, or release readiness.

The user retains final product, creative, balance, integrated-playtest, and release
approval. A1 retains coordination, implementation sequencing, source-version
coordination, and integration disposition.

## 2. Current-source evidence

The following evidence was read at the verified baseline.

| Source | Git blob | Admitted fact |
| --- | --- | --- |
| `unity/Docs/Product_Direction.md` | `0a380427aa53238d64071a04f7810209cf166537` | First playable mode is direct-control 3D; movement, combat, and interactions are learned there before Kingdom progression. |
| `unity/Docs/Player_Launch_Progression_Contract.md` | `5781d900aee0c0ff5872f5b63f15cb887202431d` | First-user identity, realm, creation, and progression require durable, ordered handling. |
| `unity/Docs/Narrative/MainQuestLine/ANOTHERLIFE_MAIN_QUEST_LINE.packet.json` | `8655d078575e20473d33635aad7a1acd8e6d7370` | The canonical source contains one existing fifteen-chapter main-quest chain. |
| `unity/Docs/Narrative/MainQuestLine/Chapters/ANOTHERLIFE_MAIN_QUEST_LINE.00-ch00_first_signal.json` | `2365f2ef843cfb91c91b9bbe2ac7adbe3cc62166` | Chapter 0 delegates `OMEN_1` authority to the external NVS packet and unlocks `MQ_C1_PROOF_OF_WORTH`. |
| `unity/Docs/Narrative/MainQuestLine/Chapters/ANOTHERLIFE_MAIN_QUEST_LINE.01-ch01_proof_of_worth.json` | `9e503f2d9ac71e118a21191209d9bf1d02299f37` | C1 already culminates in recognition and says the kingdom layer opens after proof of worth. |
| `unity/Docs/Narrative/MainQuestLine/Chapters/ANOTHERLIFE_MAIN_QUEST_LINE.04-ch04_kingdom_under_oath.json` | `1e2057b0f919b1485fdcfc142222799606862400` | C4 is the later full Kingdom-development chapter and already owns its five strategic objectives. |
| `unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json` | `0c7dd514cee0abe3b5e1c823fb4ca596703cceeb` | `OMEN_1` is the authoritative first main quest, with `autoAccept: false` and `SELECT_VALERIUS` offer behavior. |
| `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` | `0c7dd514cee0abe3b5e1c823fb4ca596703cceeb` | Runtime source bytes match the current A1 packet. |
| `unity/Assets/AL/Scripts/Core/Enums/Enums.cs` | `5a532ff67584c055a5151020b392c802585d4898` | Current compiled `ClassFamily` values are `Warrior`, `Mage`, `Ranger`, and `Assassin`. |
| `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json` | `3de7ea95fb9cd49b30b129b0eb3b46ba156ac9f9` | Current realm data supplies the four realm-to-people relationships; `starterClassBias` is not class authority. |
| `unity/Assets/AL/Scripts/ChampionMode/Control/ChampionController.cs` | `61e1f10427554e5beaeefa7151852b1dbf816bee` | Accepted external movement and a common basic-attack request exist. |
| `unity/Assets/AL/Scripts/ChampionMode/ChampionArenaSceneController.cs` | `455e07589168e9d4515a47b334c8c2ee529648b8` | PC/mobile-facing movement and basic attack are present, together with direct Kingdom buttons that conflict with the new gate. |
| `unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs` | `76b3936d78c7e5e2bff306a0404b9c2613c767c7` | The current objective presentation is plain text and does not provide the requested follow action. |
| `unity/Assets/AL/Scripts/RealmWar/World/LocalWorldAtlasService.cs` | `c0bfbd4de979a5f40872895b9c26179ea39fe930` | Existing world-atlas data does not by itself prove active-objective focus or relocation authority. |
| `unity/Assets/AL/Scripts/RealmWar/World/WorldObjectiveMarkerSpawner.cs` | `1d6f622f616909c4db53c6160980d94e0fd3c55b` | Existing markers do not by themselves satisfy the follow/reposition contracts in this packet. |

Issue `#274` remains correctly closed for complete main-quest narrative-source
authorship. This delta does not reopen or replace that source; it requests a later
versioned amendment. Runtime wiring and user playtest remain downstream.

## 3. Realm-derived people and v1 class evidence

### 3.1 Realm determines launch people

The Race & Class step confirms the people identity already determined by the
selected launch realm. It does not offer cross-realm substitution.

| Canonical launch realm ID | Realm | Derived people |
| --- | --- | --- |
| `crownlands` | Crownlands | Humans |
| `stonehold` | Stonehold | Dwarves |
| `eldergrove` | Eldergrove | Elves |
| `umbral` | Umbral | Dark Elves |

These are semantic relationships, not newly approved localized values. Raw realm
IDs and engine enum values are machine-only and must never be rendered as
fallback copy.

### 3.2 v1 selectable class family

The executable v1 slice admits exactly these current compiled `ClassFamily`
values as selectable machine evidence:

- `Warrior`
- `Mage`
- `Ranger`
- `Assassin`

The selected `ClassFamily` must be explicitly committed by the authoritative
first-user operation and carried consistently through customization, username
creation, tutorial context, quest context, reload/reconnect, Lord appointment,
kingdom grant, and Kingdom Management unlock.

The following sources are expressly non-authoritative for v1 class selection:

- `SubclassId`;
- customization-preset names;
- realm `starterClassBias` strings;
- weapons or offhands;
- silhouette labels;
- catalog order;
- legacy class-like display text.

No subclass roster, grouping, support/healer taxonomy, class eligibility,
availability, default, ability, effect, PvP behavior, or balance value is admitted.
Final class and people localization remains copy-blocked. The four raw
`ClassFamily` values are not player-facing text.

## 4. Non-quest first-world tutorial

### 4.1 Identity and scope

The stable tutorial identity is:

`TUTORIAL_FIRST_WORLD_ENTRY`

It is an onboarding tutorial state set, not a `QuestDefinition`, main quest, side
quest, chapter, reward source, or narrative branch. It adds no NPC, dialogue,
location, lore, item, currency, experience, skill, ability, combat coefficient, or
quest consequence.

The tutorial is class-parametric. All four admitted `ClassFamily` selections use
the same two objectives. Completion does not prove any family-specific weapon,
animation, damage, hit, kill, skill, or loadout behavior.

### 4.2 Ordered objectives

| Order | Stable objective ID | Completion event | Exact semantic condition |
| --- | --- | --- | --- |
| 1 | `OBJ_TUTORIAL_FIRST_WORLD_ENTRY_MOVE` | `EVENT_TUTORIAL_FIRST_WORLD_ENTRY_MOVEMENT_CONFIRMED` | Accepted movement input produces valid 3D character movement while this objective is active. |
| 2 | `OBJ_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK` | `EVENT_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK_CONFIRMED` | One common basic-attack command is accepted while this objective is active. A hit, damage, target, kill, or reward is not required. |

After objective 2 commits, the tutorial emits
`EVENT_TUTORIAL_FIRST_WORLD_ENTRY_COMPLETED` exactly once.

### 4.3 Deterministic progression and recovery

- Exactly one tutorial objective is active at a time in the listed order.
- Only the expected event for the active objective can advance progress.
- Duplicate accepted events are idempotent and cannot advance twice.
- Out-of-order, mismatched-profile, mismatched-character, mismatched-realm, or
  mismatched-`ClassFamily` evidence fails closed without changing progress.
- Unavailable input or action is nonterminal. Retry addresses the same active
  objective and does not discard completed objectives.
- Exit, reload, or reconnect restores the exact active objective together with the
  already committed realm-derived people and `ClassFamily` context.
- There is no v1 skip or abandon path. Leaving the scene does not mark the
  tutorial complete.
- Completion is recorded once and does not grant a reward, alter a quest
  consequence, or repeat first-user authority.
- A failure after tutorial completion cannot recreate the tutorial or emit a second
  completion event.

### 4.4 Tutorial localization keys

Every key below is player-facing and `UNAPPROVED_COPY_BLOCKED`. This packet
defines the semantic referent only and supplies no localized value.

| Key | Referent |
| --- | --- |
| `tutorial.first_world_entry.title` | Minimal title for the bounded first-world tutorial. |
| `objective.tutorial.first_world_entry.move` | Current movement objective. |
| `objective.tutorial.first_world_entry.basic_attack` | Current common basic-attack objective. |
| `tutorial.first_world_entry.retry` | Retry the unchanged active tutorial objective. |
| `tutorial.first_world_entry.unavailable` | Current tutorial action cannot be completed now; no progress changed. |
| `tutorial.first_world_entry.resume` | The exact prior tutorial objective was restored. |

No raw profile, account, character, realm, class, operation, receipt, save,
provider, scene, objective, or diagnostic ID may be interpolated or rendered.

## 5. Direct handoff to the existing main quest

### 5.1 Handoff invariant

`EVENT_TUTORIAL_FIRST_WORLD_ENTRY_COMPLETED` exposes and foregrounds the
existing `OMEN_1` offer in its authoritative `OFFERED` state.

The handoff must preserve all of the following:

- `OMEN_1` remains the existing quest; no duplicate quest is created.
- `autoAccept` remains `false`.
- `SELECT_VALERIUS` remains the offer interaction.
- `OBJ_OMEN_1_TALK` remains the first authoritative objective.
- `quest.omen1.title` and `objective.omen1.talk` remain the authoritative keys.
- The tutorial completion event does not accept, progress, complete, reward, or
  otherwise mutate `OMEN_1`.
- Deferring the offer preserves `OFFERED` and does not reopen the tutorial.
- Completing `OMEN_1` continues to unlock existing `MQ_C1_PROOF_OF_WORTH`.

Chapter 0's `quest.omen_1.title` and `quest.omen_1.summary` use a divergent
underscore namespace while declaring the external NVS packet authoritative.
They must not become a second localization authority. A later versioned source
amendment must alias, retire, or otherwise resolve them in favor of the
authoritative `quest.omen1.*` namespace.

### 5.2 Active-objective follow action

The stable machine action is:

`ACTION_FOLLOW_ACTIVE_OBJECTIVE`

Both the displayed active main-quest title and the displayed active objective must
invoke this same semantic action on PC and mobile. The action selects the
authoritative current quest/objective and presents its best available focus or
navigation guidance.

Allowed results are:

- `RESULT_ACTIVE_OBJECTIVE_FOCUSED`
- `RESULT_ACTIVE_OBJECTIVE_NO_TARGET`
- `RESULT_ACTIVE_OBJECTIVE_UNAVAILABLE`

The action never accepts, advances, completes, abandons, retries, teleports, or
relocates the player and never fabricates a target. `NO_TARGET` and
`UNAVAILABLE` preserve all progression and open or focus safe quest detail.
Keyboard, controller, touch, and accessibility activation must reach the same
semantic action. This packet specifies no widget, layout, icon, route algorithm,
camera behavior, or navigation implementation.

Player-facing keys are `UNAPPROVED_COPY_BLOCKED`:

| Key | Referent |
| --- | --- |
| `quest.active.follow` | Follow the currently authoritative active objective. |
| `quest.active.follow.unavailable` | Guidance cannot be produced now; state is unchanged. |
| `quest.active.follow.no_target` | The objective has no valid navigation target. |
| `quest.active.follow.accessibility_label` | Accessible name for the same follow action. |

## 6. Lord appointment, kingdom grant, and C1 extension

### 6.1 Exact placement

The early milestone belongs inside existing `MQ_C1_PROOF_OF_WORTH` immediately
after existing `OBJ_C1_ACCEPT_MARK`.

This placement preserves the authored proof-of-worth meaning, makes the formal
appointment and grant earned through the main quest, and matches C1's existing
completion outcome that the kingdom layer opens as a responsibility. Realm
selection, onboarding commit, first world entry, tutorial completion, and
`OMEN_1` completion do not appoint the player Lord, grant a kingdom, unlock
Kingdom Management, or enter Kingdom mode.

### 6.2 Appended C1 objectives

Append these objectives in this exact order:

| Order after `OBJ_C1_ACCEPT_MARK` | Stable objective ID | Semantic result |
| --- | --- | --- |
| 1 | `OBJ_C1_RECEIVE_LORD_APPOINTMENT` | Formally commit the player's appointment as Lord. |
| 2 | `OBJ_C1_RECEIVE_KINGDOM_GRANT` | Formally commit the kingdom grant and its dependency on the appointment. |
| 3 | `OBJ_C1_REVIEW_KINGDOM_MANAGEMENT` | Present the concise guided Kingdom Management introduction. |
| 4 | `OBJ_C1_ENTER_KINGDOM_MANAGEMENT` | Use the unlocked shared-menu entry to enter Kingdom mode. |
| 5 | `OBJ_C1_RETURN_TO_CHARACTER_MODE` | Use the shared menu to return to 3D Character mode with context preserved. |

`MQ_C1_PROOF_OF_WORTH` completes and unlocks existing
`MQ_C2_BORDER_OATHS` only after the five appended objectives complete.
The existing C1 choice, realm variants, side quests, failure policy,
abandonment policy, and resume policy otherwise remain unchanged.

Existing `MQ_C4_KINGDOM_UNDER_OATH` remains the later full strategic
Kingdom-development chapter. Its five existing objectives are not moved,
renamed, duplicated, or completed by the C1 introduction.

### 6.3 Milestones and events

Stable machine-only milestones:

- `MILESTONE_LORD_APPOINTED`
- `MILESTONE_KINGDOM_GRANTED`
- `MILESTONE_KINGDOM_MANAGEMENT_UNLOCKED`

Stable machine-only events:

- `EVENT_LORD_APPOINTMENT_COMMITTED`
- `EVENT_KINGDOM_GRANT_COMMITTED`
- `EVENT_KINGDOM_MANAGEMENT_UNLOCK_COMMITTED`

The appointment commits before the grant. The Kingdom Management unlock
requires both the committed appointment and committed grant. The unlock commits
with the valid grant and is idempotent. A later guided-introduction or mode-switch
failure cannot revoke or duplicate the appointment, grant, or unlock.

### 6.4 Guided introduction boundary

The guided introduction communicates only these approved meanings:

1. Kingdom Management is the player's top-down 2.5D kingdom/city-management
   mode.
2. Players manage and develop the kingdom through its authorized systems.
3. Kingdom development can affect the player's 3D character stats and skills.

The introduction does not name or promise any exact bonus, coefficient, skill,
recipe, resource rate, economy outcome, PvP effect, timer, cost, unlock level, or
balance value. Any unavailable effect must not be presented as currently active.

### 6.5 Failure, retry, reload, and resume

- Appointment, grant, and unlock events are each duplicate-safe and commit once.
- Failure before appointment resumes the current C1 objective without authority.
- Failure after appointment but before grant retains appointment and resumes the
  grant objective.
- Failure after grant retains appointment, grant, and unlock; it resumes the exact
  introduction or mode-round-trip objective.
- An unavailable mode transition is nonterminal and retries the same objective.
- Reload/reconnect restores the exact C1 objective, milestone set, current mode,
  committed realm-derived people, and `ClassFamily` context.
- Returning to Character mode preserves profile, session, quest, and navigation
  context. It does not restart C1 or reissue the grant.
- A duplicate grant, unlock, enter, or return event cannot advance another
  objective or create another kingdom.

### 6.6 C1 localization keys

Every key is `UNAPPROVED_COPY_BLOCKED`.

| Key | Referent |
| --- | --- |
| `objective.obj_c1_receive_lord_appointment` | Receive the formal Lord appointment. |
| `objective.obj_c1_receive_kingdom_grant` | Receive the formal kingdom grant. |
| `objective.obj_c1_review_kingdom_management` | Review the bounded Kingdom Management introduction. |
| `objective.obj_c1_enter_kingdom_management` | Enter Kingdom mode through the shared menu. |
| `objective.obj_c1_return_to_character_mode` | Return to 3D Character mode through the shared menu. |
| `milestone.lord_appointment.title` | Lord appointment milestone surface. |
| `milestone.kingdom_grant.title` | Kingdom grant milestone surface. |
| `tutorial.kingdom_management.title` | Guided-introduction title. |
| `tutorial.kingdom_management.purpose` | What Kingdom Management is. |
| `tutorial.kingdom_management.development` | How kingdom management and development relate. |
| `tutorial.kingdom_management.character_impact` | Non-numeric statement that development can affect the 3D character's stats and skills. |
| `tutorial.kingdom_management.retry` | Retry the current unchanged introduction step. |
| `tutorial.kingdom_management.unavailable` | Current introduction or transition step is unavailable without progression loss. |

## 7. Shared game menu and cross-mode transition

### 7.1 Module inventory

Stable machine-only module IDs:

| Module ID | Player-facing key |
| --- | --- |
| `MENU_MODULE_INVENTORY` | `menu.inventory.title` |
| `MENU_MODULE_CHARACTER_STATS_EQUIPMENT` | `menu.character_stats_equipment.title` |
| `MENU_MODULE_SKILL_SETS` | `menu.skill_sets.title` |
| `MENU_MODULE_QUESTS` | `menu.quests.title` |
| `MENU_MODULE_KINGDOM_MANAGEMENT` | `menu.kingdom_management.title` |
| `MENU_MODULE_SETTINGS` | `menu.settings.title` |

The menu is extensible. This inventory does not approve visual order, grouping,
navigation pattern, layout, iconography, animation, or final copy.

### 7.2 Sole cross-mode authority

The shared game menu owns the only transition between 3D Character mode and
Kingdom Management.

Stable machine actions and result:

- `ACTION_ENTER_KINGDOM_MANAGEMENT`
- `ACTION_RETURN_TO_CHARACTER_MODE`
- `RESULT_MODE_SWITCH_UNAVAILABLE`

Before `MILESTONE_KINGDOM_MANAGEMENT_UNLOCKED`, the Kingdom Management
entry is absent or visibly locked and cannot invoke Kingdom mode. The exact
absent-versus-locked presentation is a later UX decision; neither variant may
bypass the main quest.

After unlock:

- `ACTION_ENTER_KINGDOM_MANAGEMENT` enters existing semantic mode
  `2_5d_inner_kingdom`;
- the shared menu inside Kingdom mode exposes
  `ACTION_RETURN_TO_CHARACTER_MODE`;
- return restores existing semantic mode `3d_inner_realm`;
- both transitions preserve profile, session, active quest, active objective, and
  navigation context;
- no permanent gameplay-HUD control may switch between these two major modes.

Player-facing keys are `UNAPPROVED_COPY_BLOCKED`:

| Key | Referent |
| --- | --- |
| `menu.inventory.title` | Inventory module. |
| `menu.character_stats_equipment.title` | Character Stats/Equipment module. |
| `menu.skill_sets.title` | Skill Sets module. |
| `menu.quests.title` | Quests module. |
| `menu.kingdom_management.title` | Kingdom Management module. |
| `menu.settings.title` | Settings module. |
| `menu.kingdom_management.locked` | Kingdom Management is not yet admitted. |
| `menu.kingdom_management.newly_unlocked` | The quest-earned entry has just become available. |
| `menu.kingdom_management.enter` | Enter Kingdom mode from the shared menu. |
| `menu.kingdom_management.return_to_character` | Return to 3D Character mode from the shared menu. |
| `menu.kingdom_management.unavailable` | The requested mode transition cannot complete now; context is preserved. |

## 8. Kingdom-local Kingdom View and World Map

### 8.1 View hierarchy

Once Kingdom Management is unlocked and entered, a non-intrusive Kingdom HUD
control may switch locally between:

- `KINGDOM_SURFACE_KINGDOM_VIEW`
- `KINGDOM_SURFACE_WORLD_MAP`

Stable machine actions:

- `ACTION_SHOW_KINGDOM_VIEW`
- `ACTION_SHOW_WORLD_MAP`

This Kingdom-local view switch does not enter or leave Kingdom Management.
The shared menu remains the sole 3D Character mode to Kingdom Management
transition.

Player-facing keys are `UNAPPROVED_COPY_BLOCKED`:

| Key | Referent |
| --- | --- |
| `kingdom.view.kingdom.title` | Kingdom View surface. |
| `kingdom.view.world_map.title` | World Map surface. |
| `kingdom.view.switch_to_kingdom` | Switch locally from World Map to Kingdom View. |
| `kingdom.view.switch_to_world_map` | Switch locally from Kingdom View to World Map. |

### 8.2 World Map and future relocation vocabulary

World Map supports regional and territory navigation plus player
kingdom/castle presence. Future castle or kingdom repositioning must use an
authoritative, explicit operation. Selecting or tapping a destination never
relocates immediately.

Stable machine vocabulary:

- `ACTION_PREVIEW_KINGDOM_RELOCATION`
- `ACTION_CONFIRM_KINGDOM_RELOCATION`
- `ACTION_CANCEL_KINGDOM_RELOCATION`
- `ACTION_RECONCILE_KINGDOM_RELOCATION`
- `RESULT_KINGDOM_RELOCATION_PREVIEW_READY`
- `RESULT_KINGDOM_RELOCATION_BLOCKED`
- `RESULT_KINGDOM_RELOCATION_COMMITTED`
- `RESULT_KINGDOM_RELOCATION_CANCELLED`
- `RESULT_KINGDOM_RELOCATION_RECOVERY_REQUIRED`

Semantic sequence:

1. Select a destination without mutation.
2. Request preview.
3. Present source-authorized eligibility, destination risk, cost, cooldown, and
   protection information, or an approved blocked reason.
4. Confirm or cancel explicitly.
5. If commit outcome is unknown, reconcile the same operation identity.
6. Report the committed, cancelled, blocked, or recovery-required result without
   issuing a second relocation.

Player-facing keys are `UNAPPROVED_COPY_BLOCKED`:

| Key | Referent |
| --- | --- |
| `world_map.relocation.preview.title` | Relocation preview surface. |
| `world_map.relocation.preview.destination` | Human-readable selected destination. |
| `world_map.relocation.preview.eligibility` | Source-authorized eligibility summary. |
| `world_map.relocation.preview.cost` | Source-authorized cost summary, if any. |
| `world_map.relocation.preview.cooldown` | Source-authorized cooldown summary, if any. |
| `world_map.relocation.preview.protection` | Source-authorized protection summary, if any. |
| `world_map.relocation.preview.destination_risk` | Source-authorized destination-risk summary. |
| `world_map.relocation.confirm` | Explicit confirmation action. |
| `world_map.relocation.cancel` | Explicit cancellation action. |
| `world_map.relocation.blocked_reason` | Localized approved reason that relocation cannot proceed. |
| `world_map.relocation.committing` | Commit outcome is pending; do not repeat the operation. |
| `world_map.relocation.committed` | The authoritative operation committed. |
| `world_map.relocation.recovery_required` | Outcome remains unknown and requires reconciliation. |

No exact item, currency, amount, price, cooldown duration, eligibility rule,
safe-region rule, protection rule, siege rule, alliance rule, occupancy rule,
collision rule, anti-abuse rule, server/provider contract, save field, or recovery
implementation is approved here. Interpolation may use only separately
source-authorized display values. Raw destination, account, profile, character,
operation, receipt, commit, provider, database, or diagnostic IDs must never be
rendered.

## 9. Complete machine-ID inventory

All IDs in this section are machine-only and never localized or rendered.

### Tutorial and quest-follow IDs

- `TUTORIAL_FIRST_WORLD_ENTRY`
- `OBJ_TUTORIAL_FIRST_WORLD_ENTRY_MOVE`
- `OBJ_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK`
- `EVENT_TUTORIAL_FIRST_WORLD_ENTRY_MOVEMENT_CONFIRMED`
- `EVENT_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK_CONFIRMED`
- `EVENT_TUTORIAL_FIRST_WORLD_ENTRY_COMPLETED`
- `ACTION_FOLLOW_ACTIVE_OBJECTIVE`
- `RESULT_ACTIVE_OBJECTIVE_FOCUSED`
- `RESULT_ACTIVE_OBJECTIVE_NO_TARGET`
- `RESULT_ACTIVE_OBJECTIVE_UNAVAILABLE`

### C1 appointment, grant, and mode IDs

- `OBJ_C1_RECEIVE_LORD_APPOINTMENT`
- `OBJ_C1_RECEIVE_KINGDOM_GRANT`
- `OBJ_C1_REVIEW_KINGDOM_MANAGEMENT`
- `OBJ_C1_ENTER_KINGDOM_MANAGEMENT`
- `OBJ_C1_RETURN_TO_CHARACTER_MODE`
- `MILESTONE_LORD_APPOINTED`
- `MILESTONE_KINGDOM_GRANTED`
- `MILESTONE_KINGDOM_MANAGEMENT_UNLOCKED`
- `EVENT_LORD_APPOINTMENT_COMMITTED`
- `EVENT_KINGDOM_GRANT_COMMITTED`
- `EVENT_KINGDOM_MANAGEMENT_UNLOCK_COMMITTED`
- `ACTION_ENTER_KINGDOM_MANAGEMENT`
- `ACTION_RETURN_TO_CHARACTER_MODE`
- `RESULT_MODE_SWITCH_UNAVAILABLE`

### Shared-menu IDs

- `MENU_MODULE_INVENTORY`
- `MENU_MODULE_CHARACTER_STATS_EQUIPMENT`
- `MENU_MODULE_SKILL_SETS`
- `MENU_MODULE_QUESTS`
- `MENU_MODULE_KINGDOM_MANAGEMENT`
- `MENU_MODULE_SETTINGS`

### Kingdom-local view and relocation IDs

- `KINGDOM_SURFACE_KINGDOM_VIEW`
- `KINGDOM_SURFACE_WORLD_MAP`
- `ACTION_SHOW_KINGDOM_VIEW`
- `ACTION_SHOW_WORLD_MAP`
- `ACTION_PREVIEW_KINGDOM_RELOCATION`
- `ACTION_CONFIRM_KINGDOM_RELOCATION`
- `ACTION_CANCEL_KINGDOM_RELOCATION`
- `ACTION_RECONCILE_KINGDOM_RELOCATION`
- `RESULT_KINGDOM_RELOCATION_PREVIEW_READY`
- `RESULT_KINGDOM_RELOCATION_BLOCKED`
- `RESULT_KINGDOM_RELOCATION_COMMITTED`
- `RESULT_KINGDOM_RELOCATION_CANCELLED`
- `RESULT_KINGDOM_RELOCATION_RECOVERY_REQUIRED`

## 10. Localization and privacy rules

This packet defines 51 player-facing semantic keys and no localized values.

- Machine IDs, enum names, state names, diagnostic codes, raw catalog IDs, and
  operation identifiers are never localized or rendered.
- Player-facing surfaces resolve through the declared key namespace only.
- Missing or unresolved values fail visibly through an approved safe fallback;
  they never expose the key, machine ID, enum token, exception, provider
  response, or internal reason.
- No key may interpolate raw `ProfileId`, `AccountId`, `CharacterId`,
  onboarding operation identity, semantic fingerprint, receipt ID, commit ID,
  database transaction ID, save revision, scene name, or stack trace.
- Destination, cooldown, risk, protection, blocked-reason, and cost display values
  remain source-blocked until their owning contracts authorize bounded,
  localized display inputs.
- Final wording, translation, pluralization, typography, layout, accessibility
  phrasing, voice, and recording remain unapproved.
- Minimal on-screen prose remains the governing presentation constraint.

## 11. Current conflicts and required source amendments

### 11.1 P0 before an end-to-end playable claim

1. The A1 and runtime `OMEN_1` bytes both set
   `completionDestination: KINGDOM_COMMAND_VIEW` and request that capability.
   This bypasses the new C1 appointment/grant gate. A later versioned
   A1 -> coordination -> runtime synchronization must remove or supersede that
   destination while preserving selected-realm `CH1_REALM_INTRO`.
2. `dialogue.omen1.offer` currently addresses the player as "My lord" before the
   formal C1 appointment. The key remains stable, but its final replacement value
   requires narrative copy authority.
3. `ChampionArenaSceneController.cs` currently creates direct Kingdom HUD
   buttons at source lines 998, 2704, and 2751. They bypass the shared-menu-only
   rule and the quest unlock.
4. `KingdomSceneController.cs` currently presents `OBJECTIVES`, quest title, and
   objective as plain text around source lines 280 and 684-708. It does not
   satisfy `ACTION_FOLLOW_ACTIVE_OBJECTIVE`.
5. No current tutorial source defines `TUTORIAL_FIRST_WORLD_ENTRY` or its two
   objectives. Existing movement and basic attack are mechanics evidence only,
   not tutorial completion authority.

### 11.2 P1 source-version chain

1. Version the C1 component to append the five objectives, milestones, events,
   recovery meaning, and later approved copy values.
2. Update the root main-quest packet's component version/hash through its normal
   packet mechanics.
3. Resolve Chapter 0's `quest.omen_1.*` namespace against authoritative
   `quest.omen1.*` without duplicating localization authority.
4. Revise C1's "beyond ceremonial title" wording so it cannot imply a formal Lord
   appointment before the appended milestone. This packet does not author the
   replacement.
5. Treat Product Direction's older combined champion/lord/character wording as
   pre-decision shorthand, not appointment or unlock authority.
6. Keep C4's existing strategic objectives intact and cross-reference the
   quest-earned unlock rather than moving or duplicating C4.

## 12. Issue #477 dependency boundary

Issue `#477` exclusively owns the next economy and localization reconciliation
lane. It does not broaden this packet or block publication of this source delta.

The dependency includes:

- player-facing singular/plural `Oathmark` and `Oathmarks` terminology;
- 3D-character-mode-only earning authority for the gold-like currency;
- non-Oathmark resource earning in Kingdom and World Map modes;
- Marketplace buyer debit, seller net, and destroyed-tax preview;
- repair and consumable sink copy;
- direct player-to-player trading unavailable by default;
- compatibility treatment for machine `ResourceType.Gold` and stable ID `gold`;
- explicit reconciliation of `GoldMine`, Kingdom Gold rewards/yields/costs,
  starting balances, territory yields, and NVS-01's "500 Gold" consequence and
  copy.

Current reconciliation targets include:

| Source | Current conflict |
| --- | --- |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C3B_Resource_Reference_Authority.md` | Stable `gold` maps to `ResourceType.Gold`. |
| `unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs` | Player-facing `GOLD` and `Gold Mine` text is hard-coded. |
| `unity/Assets/AL/Scripts/UI/Kingdom/KingdomCommandPolicy.cs` | `Gold Mine` command copy remains. |
| `unity/Assets/AL/StreamingAssets/GameData/al_quest_preview_content_catalog.json` | `reward.omen1.gold` currently renders "500 Gold". |
| `unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json` and runtime mirror | The authoritative consequence/copy still says "500 Gold". |
| Kingdom, territory, research, construction, fallback, and starting-balance code | Existing Gold grants, yields, and costs conflict with the new source split until explicitly migrated. |

This packet defines no economy localization key, currency migration, replacement
building function, Marketplace transaction contract, repair value, consumable
effect, tax rounding, price bound, direct-trade behavior, or save migration.
No Gold/GoldMine effect may be silently renamed or reinterpreted as Oathmarks.

## 13. Dependency order

1. Accept this narrative/content source delta.
2. A1 publishes the focused coordination contracts and implementation order.
3. Narrative/content versions the affected `OMEN_1` and main-quest sources with
   separately approved final copy.
4. Engineering implements the tutorial, active-objective action, quest gate,
   shared menu, mode transition, persistence/recovery, and tests through focused
   slices.
5. Narrative/content performs fidelity review against the accepted source.
6. The user performs the integrated first-user playtest and milestone decision.
7. Issue `#477` proceeds independently through its own source, coordination,
   migration, engineering, and balance gates.

No dependent implementation may treat this draft PR, green hosted checks, issue
closure, or merge state as user playtest, final copy, runtime, balance, or release
approval.

## 14. Acceptance criteria

The source delta is internally acceptable only when all of these remain true:

- the tutorial has exactly two ordered objectives;
- the tutorial is not counted as a quest and grants no reward;
- the four realm-derived people relationships remain exact;
- v1 class selection admits exactly the four current `ClassFamily` values and no
  subclass or preset inference;
- tutorial completion exposes existing `OMEN_1` in `OFFERED` without accepting it;
- `OMEN_1` continues into existing `MQ_C1_PROOF_OF_WORTH`;
- Lord appointment and kingdom grant occur only inside C1 after proof of worth;
- Kingdom Management remains locked before both milestones;
- the guided C1 round trip uses the shared menu in both directions;
- the shared menu is the only 3D Character/Kingdom transition;
- Kingdom HUD switching remains local to Kingdom View/World Map;
- map destination selection cannot commit relocation;
- unknown relocation outcome reconciles the same operation rather than repeating it;
- active-objective follow cannot mutate quest progress or teleport;
- failure, retry, reload, reconnect, and duplicate delivery preserve exact progress;
- machine IDs are never rendered;
- all 51 player-facing keys remain value-free and copy-blocked;
- C4, the wider main-quest chain, NVS consequences, and economy effects are not
  silently rewritten;
- issue `#477` remains the sole economy-source dependency;
- no runtime, save, schema, catalog, asset, workflow, shared lock, or A2 source is
  changed by this packet.

## 15. Explicit nonclaims

This packet does not approve or claim:

- final localized strings, dialogue, narration, voice, NPC, lore, reward, or visual;
- subclasses, class grouping, support taxonomy, abilities, skills, effects, PvP,
  class balance, or release taxonomy;
- a runtime state machine, event payload, scene route, UI layout, navigation
  algorithm, map implementation, server/provider, database, or save schema;
- a kingdom bonus, stat coefficient, skill value, resource rate, economy value,
  relocation cost/cooldown/eligibility, or anti-abuse rule;
- Oathmark migration, Marketplace implementation, repair/consumable balance, or
  direct trading;
- Unity, Android, Player, device, performance, accessibility, localization, or
  integrated-playtest evidence;
- production, milestone, user acceptance, or release readiness.

The next valid state after this draft is A1 source disposition and focused
coordination sequencing. It is not direct implementation or merge by the
narrative author.
