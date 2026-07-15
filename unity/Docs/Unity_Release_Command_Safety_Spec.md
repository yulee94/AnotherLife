# Unity Production Command Safety Specification

**Status date:** 2026-07-15  
**Specification owner:** GPT  
**Implementation owner:** Codex  
**Tracking issue:** #178  
**Baseline `main`:** `e2632f0f6566fb2904da5c35a5db94de0093e297`  
**Primary source:** `unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs`

## 1. Goal

Contain the current Kingdom command deck before any production Player build can expose direct grants, test mutations, progression previews, false success messages, invalid realm substitution, or one-click profile reset.

The first implementation PR must make the current release surface **honest and non-mutating** without waiting for every downstream domain redesign.

This is a release-containment specification, not a complete production command implementation.

## 2. Immediate product decision

Until a command’s owning domain issue provides a validated, committed result contract, the release command deck must not invoke that mutation.

For the current source baseline:

- the read-only dashboard and **Board View** remain available;
- all direct test/cheat/destructive commands are removed from the normal command deck and from release-compiled invokable code paths;
- production-intent mutations whose result contracts are incomplete are represented as unavailable capabilities, not routed to current void/prototype methods;
- Champion deployment remains unavailable in production until #150 proves the scene route and #173/#180 prove realm and encounter preconditions;
- no terrestrial-design behavior is involved.

The first PR favors an honest temporarily limited release UI over a feature-rich UI that can corrupt, duplicate, or fabricate player state.

## 3. Non-goals

Do not:

- implement #163, #165, #166, #169, #171, #174, #177, or #180 inside this task;
- change resource, credit, reward, cost, territory, Warmaster, quest, or battle balance;
- implement production profile reset before #137;
- create a secret gesture, hidden release cheat menu, or runtime password;
- move prototype grants into another production component;
- alter narrative content, Android source, scenes, Build Settings, save algorithms, catalogs, or terrestrial design;
- turn hard-coded command messages into new narrative/localization authority;
- preserve unsafe prototype commands merely because they are useful for demonstrations.

## 4. Verified current command inventory

### 4.1 Read-only/presentation utility

| Current label | Handler | Classification | First-PR release policy |
| --- | --- | --- | --- |
| Board View | `ToggleDashboard` | production presentation utility | enabled |

### 4.2 Production-intent commands with incomplete integrity contracts

| Current label | Current call | Owning issue(s) | First-PR release policy |
| --- | --- | --- | --- |
| Town Hall | `StartUpgrade("TownHall")` | #165, #183 | unavailable |
| Farm | `StartUpgrade("Farm")` | #165, #183 | unavailable |
| Lumber | `StartUpgrade("LumberMill")` | #165, #183 | unavailable |
| Quarry | `StartUpgrade("Quarry")` | #165, #183 | unavailable |
| Gold Mine | `StartUpgrade("GoldMine")` | #165, #183 | unavailable |
| Mana Shrine | `StartUpgrade("ManaShrine")` | #165, #183 | unavailable; ID not in current fallback catalog |
| Mine | `StartUpgrade("Mine")` | #165, #183 | unavailable; ID conflicts with current catalog naming |
| Infantry | `StartTraining(Infantry, 25)` | #163, #165 | unavailable |
| Ranged | `StartTraining(Ranged, 25)` | #163, #165 | unavailable |
| Claim | loops `GetActiveQuests()` then `ClaimReward(...)` | #152, #133 | unavailable |
| Steel | `StartResearch("Steel Forging")` | #163, #165, #183 | unavailable |
| Armor | `StartResearch("Plate Armor")` | #163, #165, #183 | unavailable |
| Warmaster | hard-coded piece/cost flow | #163, #171, #183 | unavailable |
| Capture | `CaptureTerritory("T5", realm)` | #163, #166, #173 | unavailable |
| Champion | delayed `LoadScene("ChampionArena")` | #150, #173, #180 | unavailable in production until prerequisites are complete |

These commands are not cheats by intent, but current handlers infer success from void/partial APIs, use unapproved IDs, or depend on unsafe domain state. They must not remain active in release merely because their UI exists.

### 4.3 Development simulation or preview

| Current label | Current behavior | Risk | First-PR policy |
| --- | --- | --- | --- |
| War Drill | fixed battle request; simulator currently updates real WinBattle progress | preview mutates authority; invalid Crownlands fallback | remove command and handler from production controller |

The fixed request values may later become a unit/integration fixture, not an end-user command.

### 4.4 Developer grant/cheat

| Current label | Current behavior | Risk | First-PR policy |
| --- | --- | --- | --- |
| Warzone | `AddCredits(250)` | unlimited paid/progression currency | remove command and direct-grant handler |
| Secure Gem | fixed `Stonehold_Gem_1` / `offline_player` pickup | fixed identity and custody mutation | remove command and handler |
| Wishgate | earns entitlement with test reason | entitlement fabrication/overwrite | remove command and handler |
| Claim Wish | chooses fixed reward then adds 300 credits regardless of commit | duplicate/lost/false reward | remove command and handler |

Do not preserve these handlers in `KingdomSceneController` behind a Boolean. Removal makes the release surface and code path easier to prove safe.

### 4.5 Destructive maintenance

| Current label | Current behavior | Risk | First-PR policy |
| --- | --- | --- | --- |
| Reset Save | `DeleteSave(); Load(); LoadScene("Boot")` | no confirmation/result; incomplete deletion; false success | remove from command deck and controller |

A future production reset belongs to a separate settings/maintenance UI after #137 and #177.

## 5. Command model

Create a small technical command presentation model so UI construction does not embed mutation ownership.

Suggested concepts:

```text
KingdomCommandId
KingdomCommandCategory
KingdomCommandAvailability
KingdomCommandDescriptor
KingdomCommandResolution
```

Equivalent names are acceptable.

### 5.1 Stable command IDs

IDs are technical and localization-independent. Include only current production-intent commands and presentation utilities in the production model, for example:

```text
board.view
building.town_hall.upgrade
building.farm.upgrade
building.lumber_mill.upgrade
building.quarry.upgrade
building.gold_mine.upgrade
training.infantry.start
training.ranged.start
quest.claim_available
research.steel_forging.start
research.plate_armor.start
warmaster.purchase_next
territory.borderlands.capture
champion.deploy
```

Do **not** assign production IDs to current direct grants, fixed gem mutation, test Wishgate grant, fixed Wish reward, War Drill, or one-click reset. Their absence is intentional.

### 5.2 Availability states

Minimum states:

```text
Available
UnavailableDependency
UnavailableInvalidContext
UnavailableBuild
Hidden
```

Optional diagnostic fields:

```text
blockingIssueIds
technicalCode
requiredCapabilityId
```

Player-facing localized copy is supplied later through #177. The first PR may use one safe generic non-story message for an unavailable command, but should prefer disabling/hiding over a new collection of hard-coded explanations.

### 5.3 Production policy source

Add one pure policy/factory that produces command descriptors from:

- build context;
- verified domain capability flags;
- committed realm availability;
- configured scene capability;
- no direct service mutation.

The policy is deterministic and testable outside scene UI.

Do not infer readiness from service registration alone. A service existing does not prove its owning integrity contract is accepted.

For the first PR, capability flags for all mutation commands are false. Later focused PRs may enable each flag only after its owning issue is complete and tested.

## 6. Release command deck behavior

### 6.1 Normal release Player

- render the read-only dashboard and Board View utility;
- no direct grant, test mutation, preview battle, fixed gem/Wishgate, or reset button exists;
- production-intent commands may be omitted or rendered disabled from the pure command model;
- no removed unsafe handler can be invoked through UI, reflection-based UnityEvents, keyboard shortcuts, or retained public methods on the scene controller;
- no Crownlands substitution for missing realm;
- no command reports success until a committed result exists;
- internal IDs, test carrier IDs, test reason strings, and test reward IDs are absent from release UI.

### 6.2 Editor and development Player

The first PR does **not** need to preserve the removed cheats.

Preferred first-PR behavior:

- same honest production-intent capability model;
- optional development diagnostics show command IDs and blocker codes;
- no direct grants or destructive reset are exposed;
- developers use focused tests or dedicated test seams from the owning domain issues.

If a developer console is later justified, it requires a separate issue/PR with:

- development-only compilation;
- isolated disposable persistence root;
- explicit opt-in;
- typed before/after result;
- no low-level bypass of domain validation;
- no inclusion in release Player.

## 7. Controller responsibilities after containment

`KingdomSceneController` may:

- build presentation from command descriptors;
- refresh read-only status panels;
- show the selected command’s availability status;
- invoke an injected/registered command executor only for `Available` commands;
- navigate only after a validated result.

It must not:

- fabricate rewards;
- hard-code test identities or test battle inputs;
- infer committed success from a void call;
- substitute a default realm;
- select a Wish reward and separately grant credits;
- delete/reload profiles directly;
- own domain costs, required pieces, quest reward timing, or territory rules.

## 8. Current source removal boundary

The first containment PR should remove from `KingdomSceneController.cs`:

```text
EarnWarzoneCredits
RunTestBattle
PickTestGem
EarnWishgate
ChooseWishReward
ResetSave
```

Also remove:

- direct button construction for those handlers;
- `WarmasterPieceCost` and `WarmasterPieceIds` from the scene controller if Warmaster execution is disabled and no safe read-only use remains;
- fixed test IDs, reason strings, seeds, armies, and reward IDs;
- production message strings that claim those operations succeeded.

Do not delete domain services or interfaces. Only remove unsafe UI/controller orchestration.

Production-intent handler methods such as upgrade/research/train/claim/capture/Warmaster/deploy should either:

1. be disconnected from release descriptors and retained only when a focused test requires them; or
2. be replaced by the typed command-execution boundary.

They must not remain reachable release actions that simply call current unsafe APIs.

## 9. Realm behavior

No authoritative command may substitute Crownlands when `CurrentRealmId == None` or the definition is unavailable.

First-PR policy:

- realm-dependent commands resolve to `UnavailableInvalidContext`;
- no service mutation or scene transition;
- no realm-specific message or VFX uses a substituted realm;
- Board View remains realm-neutral where possible;
- read-only panels handle missing realm without throwing.

Later #173 defines committed realm selection and migration.

## 10. Champion deployment

The current coroutine always loads `_arenaSceneName` after a cosmetic delay.

Before production enablement, all must be true:

- #150 confirms the enabled production scene/path and exact name;
- a valid committed realm exists under #173;
- #180 defines free/demo versus quest encounter context and action lifecycle;
- scene load capability is validated before starting the overlay;
- a failed/unavailable load returns to a stable command state with visible feedback;
- repeated input cannot launch duplicate transitions.

The containment PR should mark Champion unavailable in release rather than silently attempting a scene missing from Build Settings.

The overlay/presentation may remain as unused presentation code if retaining it creates no reachable release path; otherwise remove or isolate it in a later focused cleanup.

## 11. Reset flow boundary

Remove current `ResetSave()` from this controller.

A future reset implementation requires:

```text
settings/maintenance surface
→ explicit warning
→ deliberate confirmation
→ typed DeleteSave result from #137
→ verify all artifacts removed
→ only then navigate/create a new profile
```

Required future guarantees:

- cancellation leaves profile unchanged;
- failure leaves current profile/scene stable;
- no immediate `Load()` after an unchecked delete;
- no retained previous/quarantine artifacts;
- accessible keyboard/controller/touch confirmation;
- #177 visible blocking failure/success status.

No placeholder reset button should remain in the command deck.

## 12. Read-only refresh safety

Containment must not leave `Refresh()` throwing because domain data is malformed or a command was removed.

Coordinate, but do not implement unrelated domain migrations. At minimum:

- null-safe enumeration where directly required by the changed UI path;
- no read method is called solely to support a removed command;
- no read method seeds/mutates state while rendering when avoidable;
- missing service/capability produces one visible unavailable controller state rather than an exception loop;
- Bootloader failure under #153 prevents command UI mutation safely.

Do not broaden this PR into the full #165/#166/#169/#171 compatibility work.

## 13. Message behavior

Current handlers call `SetMessage(...)` immediately after void/partial operations.

First-PR rules:

- removed commands produce no message;
- unavailable commands produce a technical unavailable state only when the user can select them;
- no “queued,” “captured,” “secured,” “earned,” “purchased,” “claimed,” or “added” message appears without a validated result;
- no raw exception, local path, internal test ID, or stack trace reaches player copy;
- normal board-selection/read-only messages may remain;
- player-facing localized result messages are migrated later through #177.

## 14. Required tests for the first containment PR

### 14.1 Pure policy tests

- release descriptor set contains no grant/test/reset command;
- development descriptor set also contains no direct grant/test/reset command unless a separately approved internal capability is explicitly enabled;
- Board View is available;
- every mutation command defaults unavailable;
- no realm makes realm-dependent command unavailable without substitution;
- unknown command ID fails closed;
- stable deterministic ordering;
- capability true/false affects only its intended command.

### 14.2 Release-code reachability

Prove in a release-compatible compilation/test path:

- no command descriptor or button label for Warzone grant, Secure Gem, Wishgate earn, Claim Wish grant, War Drill, or Reset Save;
- removed handler methods do not exist on `KingdomSceneController` or are not compiled/reachable, according to the selected implementation;
- no fixed strings remain in production controller:
  - `AddCredits(250)`;
  - `AddCredits(300)`;
  - `Stonehold_Gem_1`;
  - `offline_player`;
  - `Offline realm objective test`;
  - `warmaster_credits`;
  - fixed battle seed `20260708`;
- no release UnityEvent or shortcut references those commands.

### 14.3 Non-mutation tests

With spies/fakes for current services:

- constructing release UI calls no mutation method;
- clicking/selecting an unavailable production-intent command calls no mutation method;
- missing realm calls no capture/battle/scene mutation;
- Board View changes presentation only;
- removed test commands cannot add credits, change gem/Wishgate state, update quests, or delete saves;
- refresh/recomposition does not invoke mutation.

### 14.4 UI hierarchy tests

- release command deck contains only approved descriptors;
- no direct cheat/destructive label or button exists;
- disabled/unavailable command is non-interactable;
- status is communicated by text/icon, not color alone;
- keyboard/controller/touch focus skips hidden commands and does not activate disabled commands;
- small screen/long generic unavailable status does not overlap critical controls.

### 14.5 Regression

- read-only resource/building/research/quest/territory panels still render with valid current data;
- Board View toggles correctly;
- scene startup does not throw when no realm is committed;
- `KingdomSceneController` does not duplicate command UI on re-enable/reload;
- Unity compile passes;
- focused EditMode tests pass;
- safe #127 PlayMode runs when available;
- #150 release Player smoke proves the final deck when #150 is complete.

## 15. Follow-up reconnection matrix

Commands return through separate focused PRs only after these contracts are merged and validated.

| Command family | Required upstream before `Available` |
| --- | --- |
| resource/credit mutations | #137 + #163 |
| building/research/training | #137 + #163 + #165 + #183 definitions |
| quest claim | #137 + #152 and owning quest contract; OMEN_1 follows #133 timing |
| Warmaster | #137 + #163 + #171 + authoritative catalog |
| territory capture | #137 + #163 + #166 + #173 |
| Realm Gem/Wishgate | #137 + #169 + approved reward catalog |
| battle | #152 + #163 + #165 + #174 |
| Champion deployment | #150 + #173 + #180; #133 for quest entry |
| profile reset | #137 + #177 + separate settings/confirmation UX |

Each reconnection PR must:

- enable only its command IDs;
- consume typed committed results;
- remove no safety gate for unrelated commands;
- add failure/duplicate/reload tests;
- add localized/accessible result presentation through #177 when available.

## 16. Expected first-PR file boundary

Likely:

```text
unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs
new small command policy/model files under unity/Assets/AL/Scripts/UI/Kingdom/
focused EditMode tests
focused PlayMode/UI tests after #127
```

Optional:

- a small build-context abstraction for testability;
- a generic disabled-command presenter.

Do not edit:

- `SaveGameData.cs`;
- `Bootloader.cs`;
- `LocalGameDataService.cs`;
- domain services/interfaces;
- scenes or Build Settings;
- Android source;
- narrative source;
- Gemini terrestrial design files;
- balance/catalog content.

No designated shared-file lock is expected for the containment PR.

## 17. Validation commands and evidence

Run from fetched current `main` in the canonical workspace with Unity `2022.3.62f3`.

Required:

- batch import/C# compile;
- focused EditMode tests and complete suite totals;
- safe PlayMode when #127 exists; otherwise state unavailable;
- `git diff --check`;
- repository search proving the listed test/grant strings are absent from production controller paths;
- changed-file list and final status;
- release Player/UI hierarchy evidence after #150, not falsely claimed before it exists.

A licensing IPC failure or missing Player build remains blocked validation, not a pass.

## 18. Implementation order

1. Fetch current `main` and inspect open PR overlap.
2. Add pure command IDs/descriptors/availability policy.
3. Replace hard-coded command-deck creation with descriptor-driven presentation.
4. Remove grant/test/reset buttons and handler methods.
5. Mark all production-intent mutations unavailable by default.
6. Remove Crownlands fallback from controller command paths.
7. Ensure unavailable selection is non-mutating and honest.
8. Add policy, reachability, mutation-spy, and UI hierarchy tests.
9. Compile and run available Unity suites.
10. Rebase and return for GPT review.

## 19. Acceptance criteria

- [ ] Release command deck contains no direct currency grant, test gem, Wishgate earn/claim, War Drill, or Reset Save action.
- [ ] Unsafe handlers and fixed test IDs/amounts are absent from production controller reachability.
- [ ] Production-intent commands are unavailable until their owning committed-result contracts exist.
- [ ] Board View and read-only dashboard remain usable.
- [ ] No command silently substitutes Crownlands.
- [ ] No unavailable or failed operation reports success.
- [ ] Command presentation is data/policy-driven and deterministic.
- [ ] UI construction/refresh invokes no mutation.
- [ ] Release/development behavior is explicit and tested.
- [ ] Future command re-enablement is mapped to owning issues.
- [ ] Unity compile and focused tests pass with exact evidence.
- [ ] Safe PlayMode and Player evidence are reported honestly according to #127/#150 availability.
- [ ] No balance, narrative, save, Android, terrestrial-design, scene, or unrelated change is included.

# Codex handoff

```text
Codex: implement the first containment PR for #178 from current main using Unity_Release_Command_Safety_Spec.md. Convert the Kingdom command deck to a pure availability policy, keep Board View/read-only status, remove the direct grant/test/Wishgate/War Drill/reset commands and handlers, mark all remaining mutation commands unavailable until their owning result contracts merge, remove Crownlands fallback, and prove release reachability plus non-mutation with focused tests. Do not implement domain fixes, save reset, scenes, narrative, Android, or terrestrial design.
```