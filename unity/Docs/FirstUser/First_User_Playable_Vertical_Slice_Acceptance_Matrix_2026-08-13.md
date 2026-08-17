# First-User Playable Vertical-Slice Acceptance Matrix

> Status: `ACCEPTED_COORDINATION_EVIDENCE / P0_NO_GO / IMPLEMENTATION_NOT_BLOCKED`
>
> Evidence baseline: `main@1a215bc46ad5933090ed8bf76f69c325e5adf89b`
>
> Accepted narrative-planning source: merged PR `#479` at source head
> `ac56c77f08a5fe46a76458f2b91b5240bc2ae382`, path
> `unity/Docs/Narrative/First_User_Playable_Spine_Source_Delta.md`, blob
> `c40a37a1c3b95e3cca8aa5f2c7c28df644c0061b`; merge
> `2198edf3e7023403bfac4ab98e93ce149dacf5f7`
>
> Primary delivery mode: Codex coordination/review
>
> Controlling journey: issue `#467`
>
> Scope: acceptance and executable-test planning only. This document changes no runtime,
> save, scene, catalog, package, media, asset, workflow, or shared-file lock.

## 1. Decision and use

The first-user playable vertical slice is **P0 no-go** on the evidence baseline. Current
production source contains useful pieces, but it does not implement or validate the required
uninterrupted journey:

```text
truthful independently looping media/readiness
  -> explicit one-shot Continue
  -> Realm
  -> realm-derived Race
  -> explicit ClassFamily
  -> customization draft
  -> username/public handle
  -> authoritative receipt
  -> verified local projection
  -> WorldIntro movement
  -> common basic-attack command
  -> existing OMEN_1 OFFERED
  -> SELECT_VALERIUS
  -> clickable active quest title/objective
  -> CH01 Lord appointment
  -> kingdom grant and management unlock
  -> shared-menu-only Kingdom entry and return
```

This no-go is an evidence finding, not a stop-work decision. A1 accepted the matrix as
coordination evidence and directed engineering to proceed through focused child changes. A
green fragment test, static screenshot, issue closure, PR merge, or planning approval must not
be represented as end-to-end player acceptance.

The accepted PR #479 semantics used here are source constraints, not final localized copy,
runtime authority, device evidence, or user playtest approval. Its executable v1 assumption is:

- `crownlands` derives Humans;
- `stonehold` derives Dwarves;
- `eldergrove` derives Elves;
- `umbral` derives Dark Elves;
- the player explicitly chooses one current compiled `ClassFamily`: `Warrior`, `Mage`,
  `Ranger`, or `Assassin`;
- no subclass, preset, weapon, realm `starterClassBias`, silhouette, display label, catalog
  order, or enum ordinal may infer that choice.

Subclass taxonomy, abilities, skills, effects, PvP semantics, and balance remain deferred.

### Current-main delta since the original audit

Current main adds real machine-level progress without completing the production journey:

- PR #496 replaces the timer-only launch transition with a typed readiness coordinator,
  bounded Retry, and an explicit one-shot Continue into RealmSelection. The current adapter
  establishes a static fallback rather than playing an approved independently looping film,
  and it does not prove patch-byte truth or physical-device behavior.
- PR #494 adds a pure route-admission planner for ordered Realm, derived Race, explicit
  ClassFamily, customization, handle, receipt, and projection evidence. It intentionally has
  no scene-loading, save, production authority, WorldIntro, tutorial, quest, or Kingdom grant
  behavior.
- PR #497 adds a self-contained Realm/derived-Race/explicit-ClassFamily UGUI draft surface.
  It stops at a customization boundary and is not mounted into the production route.
- PR #495 adds a deterministic Editor-only authority emulator with receipt, projection,
  replay, collision, retained-state, and fail-closed fixtures. It is development evidence,
  not a backend, production save, username service, or player authority.
- PRs #499, #500, and #501 add an isolated Editor Game Test route, a two-step move/basic-
  attack tutorial, an `OMEN_1` offered/follow handoff, and friendlier playtest copy. The route
  is explicitly development-only and cannot certify the Player build or production journey.

Accordingly, the overall disposition remains **P0 no-go**. The delta changes several rows
from Missing to Partially evidenced (M only); it supplies no physical-device evidence and no
human/user acceptance.

## 2. Evidence classes and passage rules

| Class | Meaning | Passage rule |
| --- | --- | --- |
| **M — Machine** | Deterministic static, contract, EditMode, PlayMode, Player, build, and retained machine evidence. | Every named assertion must run against the stated build/source identity. A planned test, source fragment, or green unrelated suite is not a pass. |
| **D — Device** | Measured behavior on a declared Player build and physical PC/mobile/input/device configuration, with logs and captures. | Emulator or Editor evidence cannot silently replace physical-device evidence. Missing CPU/GPU/memory/thermal/input data remains explicitly missing. |
| **H — Human** | Comprehension, comfort, accessibility usability, visual/creative fidelity, integrated playtest, and user approval. | Machine or statistical passage never replaces H. The user retains final product, visual, balance, milestone, integrated-playtest, and release decisions. |

`M`, `D`, and `H` are independent. A requirement that needs more than one class remains open
until each class has its own durable evidence. Numeric latency, performance, memory, and human
sample thresholds in section 10 are **PROPOSED HYPOTHESES** until measured and explicitly
accepted by A1 and the user.

## 3. Exact current-source evidence ledger

Every blob below was read at the stated current-main baseline. The PR #479 row additionally
records its source-head identity for provenance.

| ID | Path | Git blob | Admitted evidence |
| --- | --- | --- | --- |
| `SRC-BOOT` | `unity/Assets/AL/Scripts/UI/BootController.cs` | `14bc3dabad8fb985e27dc27b7632bd78f8d805f3` | Production Boot now consumes typed boot/catalog/media/destination evidence, exposes explicit Continue/Retry, and routes once to RealmSelection. Its current media evidence is a static fallback; it is not an approved film or patch-byte provider. |
| `SRC-CINEMATIC` | `unity/Assets/AL/Scripts/UI/LaunchCinematicRuntime.cs` | `211fd5b90eb89a90baeced9ff60860fdc52fd6a3` | Enforces the exact 1,440-frame/24-FPS master and platform media caps, plus presentation-terminal/Continue lifecycle. No production VideoPlayer/loop/caption/audio adapter is supplied. |
| `SRC-READINESS` | `unity/Assets/AL/Scripts/UI/LaunchReadinessContracts.cs` | `12eb8012a15a8e0250e2c8d6875ba4e5bdd8cdb9` | Typed attempt-bound boot, catalog, media, destination, failure, bounded Retry, and exactly-once transition coordination. It does not discover or download patch bytes. |
| `TEST-CINEMATIC` | `unity/Assets/AL/Tests/EditMode/LaunchCinematicRuntimeTests.cs` | `8c53ec213178b353b91ed6d98d62826a6b846706` | Media record/platform caps/exact-master/skip/exactly-once lifecycle coverage; not film playback or device evidence. |
| `TEST-READINESS` | `unity/Assets/AL/Tests/EditMode/LaunchReadinessCoordinatorTests.cs` | `95b4f7d9b061e8d3accc138c44ad980b0a4b9952` | Independent-predicate, media-only denial, stale-attempt, explicit Continue, bounded Retry, and destination-failure coverage. |
| `SRC-SCENE-DESCRIPTOR` | `unity/Assets/AL/Scripts/Core/Scenes/ProductionSceneDescriptor.cs` | `a239f35920de5bfab8e3893df6b827f59385cb72` | Production route is Boot, RealmSelection, Kingdom. ChampionArena is deferred and excluded; no WorldIntro production scene. |
| `TEST-SCENE-FLOW` | `unity/Assets/AL/Tests/EditMode/ProductionScenes/ProductionSceneFlowTests.cs` | `70cb3e418aec517d7eb71c3ea7eb1f1753363113` | Explicitly asserts the stale Boot/RealmSelection/Kingdom route. Must be replaced deliberately, not counted as vertical-slice passage. |
| `TEST-SCENE-LIFECYCLE` | `unity/Assets/AL/Tests/PlayMode/ProductionSceneLifecycleTests.cs` | `9f7bd2d6e12ca31e470803c70fabb347cff27db6` | Reusable lifecycle ownership plus Boot Continue behavior; later RealmSelection/Kingdom route expectations remain incompatible with the full slice. |
| `SRC-REALM-UI` | `unity/Assets/AL/Scripts/UI/RealmSelection/RealmSelectionController.cs` | `97544680c5d41f34034dba0c14dc5d2bf7dca509` | Realm selection commits immediately and loads Kingdom; failure is not a complete player recovery flow. |
| `TEST-REALM` | `unity/Assets/AL/Tests/EditMode/RealmSelection/RealmCatalogAndSelectionTests.cs` | `5cbba07dd3c2bbf16905a24c7db333481275791d` | Reusable four-realm, catalog, idempotence, and account-realm constraints. |
| `TEST-REALM-MOBILE` | `unity/Assets/AL/Tests/EditMode/RealmSelection/RealmSelectionMobileReadinessTests.cs` | `754a8f1951b98099b99da31cccafe23030646665` | Reusable safe-area and portrait/landscape layout math; not physical-device evidence. |
| `SRC-ROUTE-CONTRACT` | `unity/Assets/AL/Scripts/Core/FirstUserRouteContracts.cs` | `2c449fcac8e0f8b727bd68aa2525066b8aa67636` | Immutable ordered evidence/cursor/intent/destination contract; contains no loading, persistence, or scene authority. |
| `SRC-ROUTE-PLANNER` | `unity/Assets/AL/Scripts/Core/FirstUserRouteAdmissionPlanner.cs` | `e217b137a5dfaaffd49f3464d55adc1fd72a0b7e` | Pure fail-closed ordered-step planner. Production evidence can admit abstract Gameplay; development evidence is capped at an isolated character test; Kingdom is unconditionally denied. |
| `TEST-ROUTE` | `unity/Assets/AL/Tests/EditMode/FirstUserRouteAdmissionPlannerTests.cs` | `b32ea98a2e12145655b9d2efdc60e9b379b711d5` | Ordered-prefix, stale/forward/conflict cursor, development ceiling, Kingdom denial, immutability, determinism, and allocation coverage. |
| `SRC-IDENTITY-DRAFT` | `unity/Assets/AL/Scripts/UI/FirstUserIdentity/FirstUserIdentityDraftFlow.cs` | `fcc0e620960c033c82080a558f6194ce2ef57176` | Self-contained draft logic derives Race from Realm and requires an explicit supported ClassFamily. It stops at customization-ready and is not production-mounted. |
| `TEST-IDENTITY-DRAFT` | `unity/Assets/AL/Tests/EditMode/FirstUserIdentity/FirstUserIdentityDraftFlowTests.cs` | `6671397749bddbea8f4f49f5bbdecd5b67d21eb2` | Four mappings, no default class, supported-family, back/change invalidation, terminal-boundary, and development-copy tests. |
| `TEST-IDENTITY-PLAYMODE` | `unity/Assets/AL/Tests/PlayMode/FirstUserIdentity/FirstUserIdentityDraftPlayModeTests.cs` | `c3eca0971173e723973fdf2b2630113fc4f42c3a` | Standalone UGUI journey ends at customization without navigation; not the production journey or device evidence. |
| `SRC-CUSTOMIZATION-UI` | `unity/Assets/AL/Scripts/ChampionMode/Customization/ChampionCustomizationController.cs` | `82394ff61332421b0532ea982a050015d2265c78` | Production edit paths save per change; no first-user draft/finalize adapter. |
| `TEST-CUSTOMIZATION-DRAFT` | `unity/Assets/AL/Tests/EditMode/Customization/CustomizationDraftPlannerTests.cs` | `e360ee84de61319ce27116a251854759ad4e4f28` | Merged non-destructive draft planner coverage reusable from PR #426. |
| `TEST-CUSTOMIZATION-COMPAT` | `unity/Assets/AL/Tests/EditMode/Customization/CustomizationCompatibilityPlannerTests.cs` | `c38063507758d1f4919d59340d5001a42f346e27` | Reusable raw-state preservation, schema, and compatibility coverage. |
| `TEST-CUSTOMIZATION-APPEARANCE` | `unity/Assets/AL/Tests/EditMode/Customization/CustomizationAppearancePlannerTests.cs` | `ae52b2e3356f34edbca3f39c33c0cf1fe52a4a88` | Reusable atomic preview/apply/rollback planning coverage. |
| `SRC-DEV-AUTHORITY` | `unity/Assets/AL/Scripts/Editor/Development/OnboardingAuthority/DeterministicDevelopmentOnboardingAuthorityEmulator.cs` | `4105ad93d3968e7cad2c9e141036e534607eae4a` | Editor-only deterministic handle/receipt/projection/replay/collision emulator. It is structurally excluded from production authority. |
| `TEST-DEV-AUTHORITY` | `unity/Assets/AL/Tests/EditMode/DevelopmentOnboardingAuthority/DeterministicDevelopmentOnboardingAuthorityEmulatorTests.cs` | `318885c49ec3c0b180396525071e00e462bca264` | Golden vectors, idempotency, tuple collision, verifier, CAS, concurrency, capacity, and no-production-surface coverage. |
| `TEST-DEV-RETAINED` | `unity/Assets/AL/Tests/EditMode/DevelopmentOnboardingAuthority/DevelopmentOnboardingAuthorityRetainedStateTests.cs` | `221851a861f64ad57bacd54deb317a349d31a7a7` | Byte-stable retained-state round trip and malformed/truncated/digest/capacity rejection for the development emulator only. |
| `SRC-SAVE` | `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs` | `de1d77c6eee85305cf136dacdb23746da1acff34` | Has `ProfileId`, selected realm, customization, and NVS state; no username, ClassFamily, onboarding operation, receipt, projection, tutorial, or CH01 milestone authority. |
| `SRC-CHAMPION-CONTROL` | `unity/Assets/AL/Scripts/ChampionMode/Control/ChampionController.cs` | `61e1f10427554e5beaeefa7151852b1dbf816bee` | Reusable movement and common basic-attack request seams; no tutorial state or production route. |
| `SRC-CHAMPION-ARENA` | `unity/Assets/AL/Scripts/ChampionMode/ChampionArenaSceneController.cs` | `455e07589168e9d4515a47b334c8c2ee529648b8` | Contains PC/mobile controls and direct Kingdom HUD controls that conflict with the shared-menu-only gate. |
| `SRC-DEV-TUTORIAL` | `unity/Assets/AL/Scripts/Editor/Development/FirstUserGameTest/FirstUserGameTestTutorialRuntime.cs` | `ed9bcb49254ce74ffb134a6ab4ea4d931bc17902` | Editor-only ordered movement/basic-attack tutorial and typed non-mutating OMEN follow handoff used by isolated Game Test. |
| `TEST-DEV-GAME-ADAPTER` | `unity/Assets/AL/Tests/EditMode/FirstUserGameTest/FirstUserGameTestAdapterTests.cs` | `174a45f9843881243d029fde26630e95312c1919` | Development evidence binding, malformed-handle, forged realm/Race, replay/projection, culture, and editor-only boundary coverage. |
| `TEST-DEV-TUTORIAL` | `unity/Assets/AL/Tests/EditMode/FirstUserGameTest/FirstUserGameTestTutorialHandoffTests.cs` | `a102cb75ddc9a1c4f15826c67ef1022d50bfeced` | Exact move-then-attack, one-shot OMEN offered handoff, typed follow, reload, duplicate, corrupt, cross-generation, and editor-only/nonquest tests. |
| `TEST-DEV-JOURNEY` | `unity/Assets/AL/Tests/PlayMode/FirstUserGameTest/FirstUserGameTestJourneyTests.cs` | `4ea73cf52ebc3e95e65f6047ff5721b0dca11812` | One isolated Editor journey into a controllable arena and tutorial/OMEN handoff; explicitly not a production Player route. |
| `SRC-OMEN` | `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` | `0c7dd514cee0abe3b5e1c823fb4ca596703cceeb` | Existing `OMEN_1`, `OFFERED`, `autoAccept: false`, `SELECT_VALERIUS`, and first objective are reusable; current Kingdom destination conflicts with the later CH01 grant gate. |
| `TEST-OMEN-RUNTIME` | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01QuestRuntimeTests.cs` | `8601a7bb7ce49a5da6658d4aa4a8d3233b892df1` | Reusable offer/deferral, exact choice, failure/retry, duplicate, mismatch, and immutable-state coverage. |
| `TEST-OMEN-PERSISTENCE` | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01PersistenceTests.cs` | `79eba33a0ac61d3a5b1731eb47846595f31227dd` | Reusable NVS disk round-trip and recovery coverage. |
| `TEST-OMEN-PROFILE` | `unity/Assets/AL/Tests/EditMode/Narrative/PersistenceAuthority/Nvs01ProfileAuthorityPersistenceTests.cs` | `dcbe21a1fb7b7b1672292fca663626ae2cee3ef2` | Reusable profile-bound install/replay/recovery patterns. |
| `SRC-KINGDOM-UI` | `unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs` | `76b3936d78c7e5e2bff306a0404b9c2613c767c7` | Initializes OMEN without tutorial proof. Active title/objective are plain text; separate action buttons do not make them clickable/focusable. |
| `TEST-OMEN-WIRING` | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01KingdomSceneWiringTests.cs` | `cd0868d575d835e9661767896e8b21ee0defec38` | Reusable packet verification, offer action, visible failure, and disk-reload wiring coverage. |
| `TEST-PLAYER-SMOKE` | `unity/Assets/AL/Tests/EditMode/ProductionScenes/ProductionPlayerLaunchSmokeTests.cs` | `8c79964c7639f11158277fceab6eaf487ef3a634` | Reusable process/isolation/log evaluator. Its expected route must be revised for the new mandatory boundaries. |
| `TEST-MOBILE-ARCH` | `unity/Assets/AL/Tests/EditMode/Architecture/ArchitectureMobileReadinessTests.cs` | `ac46cb9c51f6ac30d6e43134424d963bd0a7d4be` | Static source/asset checks only; not device performance or usability evidence. |
| `SRC-CH00` | `unity/Docs/Narrative/MainQuestLine/Chapters/ANOTHERLIFE_MAIN_QUEST_LINE.00-ch00_first_signal.json` | `2365f2ef843cfb91c91b9bbe2ac7adbe3cc62166` | Delegates `OMEN_1` authority and unlocks CH01; contains a localization namespace conflict requiring later source synchronization. |
| `SRC-CH01` | `unity/Docs/Narrative/MainQuestLine/Chapters/ANOTHERLIFE_MAIN_QUEST_LINE.01-ch01_proof_of_worth.json` | `9e503f2d9ac71e118a21191209d9bf1d02299f37` | Current CH01 stops at `OBJ_C1_ACCEPT_MARK`; appointment, grant, unlock, introduction, and round trip are not yet versioned runtime source. |
| `SRC-PR479` | `unity/Docs/Narrative/First_User_Playable_Spine_Source_Delta.md` | `c40a37a1c3b95e3cca8aa5f2c7c28df644c0061b` at PR #479 source head `ac56c77f08a5fe46a76458f2b91b5240bc2ae382` | Merged, A1-accepted planning semantics for the tutorial, OMEN handoff, active-objective action, CH01 milestones, and shared-menu round trip. It remains source/planning evidence, not runtime or user acceptance. |

## 4. P0 acceptance matrix

| ID | Stage | Machine acceptance | Device evidence | Human gate | Baseline status / owner |
| --- | --- | --- | --- | --- | --- |
| `P0-01` | Install/patch truth | Real blocking jobs and actual byte totals drive monotonic progress. Unknown totals use an explicit indeterminate state. A no-patch build never pretends to patch. Integrity failure cannot finish. Resume reuses verified chunks. | Offline, interrupted download, corrupt chunk, disk-full, suspend/resume, and low-storage runs on declared builds. | Labels, recovery choices, and progress meaning are comprehensible. | **Missing.** `#284`; patch provider remains a later implementation choice. |
| `P0-02` | Independent cinematic loop | Production-approved media loops independently across its seam. Media complete, skip, reduced motion, decode error, or fallback terminalizes presentation only and cannot assert readiness or route. Master is exactly 1,440 frames at 24 FPS on `[0,1440)`. | Decoder/loop/fallback, audio-caption seam, background, and thermal evidence on PC and Android. | User/co-developer accepts film, loop, audio, captions, comfort, and rights. | **Partially evidenced (M contract only).** Exact master/caps now validate; production media, loop, device, rights, and H evidence remain missing under `#284/#460`. |
| `P0-03` | Truthful Finished Loading | Gate opens only when media is terminal, every mandatory readiness provider is ready, and destination activation is ready. Elapsed time, animated percentage, nominal video duration, or video callback cannot contribute readiness. | Fault/recovery runs for every provider and destination activation. | Player understands ready, pending, failed, and retry states. | **Partially evidenced (M).** Typed production coordinator and Boot adapter are on main; real patch inventory, film adapter, physical-device faults, and H comprehension remain open under `#284`. |
| `P0-04` | Explicit one-shot Continue | Continue is disabled before readiness, then visible, focusable, and tappable. One accepted operation transitions once. Duplicate mouse/key/controller/touch/IME input and late callbacks are idempotent. No auto-route. | Keyboard/mouse, controller, touch, background/recreation, and input-device reconnect. | Action prominence and response are accepted. | **Partially evidenced (M).** Production Boot exposes one-shot Continue/Retry into RealmSelection; cross-input/lifecycle/device/H evidence and the later full-route transition remain missing. |
| `P0-05` | Realm and derived Race | Realm remains in the first-user draft until final authority. Race derives exactly from the selected realm. Cross-realm/stale Race rejects without mutation. | Layout/input/recreation across PC/mobile. | Relationship and irreversible consequences are understandable. | **Partially evidenced (M/development).** Exact derivation and a standalone UGUI draft exist; production RealmSelection still commits/routes independently. `#173/#467`. |
| `P0-06` | Explicit ClassFamily | Exactly one of Warrior/Mage/Ranger/Assassin is explicitly selected. Unknown, absent, aliased, inferred, cross-family, or defaulted state rejects. Choice persists through every later step/restart. | All inputs, safe areas, large text, and recreation. | Choice is legible and informed; final copy/taxonomy remains user-owned. | **Partially evidenced (M/development).** Explicit no-default selection exists in the standalone draft and pure route contract; production persistence, device, copy, and H gates remain open. |
| `P0-07` | Customization draft | Every edit stays in a versioned draft. Back/re-entry/recreation/name rejection restores exact state. Invalid/stale source cannot mutate draft. No edit changes authoritative save bytes. | Full interaction and lifecycle coverage on PC/mobile. | Options, previews, and confirmation are visually usable. | **Pure planners exist; production adapter missing.** `#184`. |
| `P0-08` | Username/public handle | Duplicate, unavailable, expired, service, and uncertain results preserve the full draft. Public handle is not `ProfileId`; internal identity is never derived from or rendered as username. | IME/OSK/controller entry, locale, background, network loss, and retry. | Copy and privacy behavior are accepted. | **Partially evidenced (M/development).** Editor emulator and isolated adapter cover exact handle commitment, collision, replay, and redacted copy; production username authority/identity separation remains missing. `#450/#137/#135`. |
| `P0-09` | Receipt and verified projection | One operation binds realm, derived Race, explicit ClassFamily, customization, and username. Gameplay needs a matching authoritative receipt plus verified local projection. Receipt-only, projection-only, mismatch, stale proof, or uncertain outcome fails closed. Retry reconciles the same operation. | Process death, disk failure, offline/uncertain authority, corrupt primary/backup, and forward-state evidence. | Failure and recovery do not mislead or expose identifiers. | **Partially evidenced (M/development).** Deterministic Editor-only receipt/projection/CAS/replay/retained-state evidence exists; no production authority, durable profile projection, or device recovery exists. |
| `P0-10` | Exact route / WorldIntro | Every incomplete cursor routes to its exact step. Receipt+projection routes to WorldIntro, never Kingdom. No production transition bypasses a mandatory step. | Windows/Android Player cold/warm launch and process-kill evidence. | Time-to-control and continuity are accepted. | **Partially evidenced (M contract/development).** Pure ordered admission and an isolated Game Test route exist; production remains RealmSelection -> Kingdom and has no WorldIntro adapter. `#467`. |
| `P0-11` | Move then basic attack | `TUTORIAL_FIRST_WORLD_ENTRY` is not a quest and has exactly MOVE then BASIC_ATTACK. Valid movement and an accepted common attack command complete their active step; hit/damage/target/kill/reward are not required. Duplicate, out-of-order, or cross-identity evidence fails closed. Completion emits once. | Keyboard/mouse, controller, touch, low-frame-rate, disconnect, reload, and process kill. | Prompt clarity, camera comfort, and feedback are accepted. | **Partially evidenced (M/development).** Editor-only isolated tutorial covers exact order, duplicate/out-of-order/cross-generation/reload behavior; production WorldIntro, device, and H evidence are missing. |
| `P0-12` | Existing OMEN_1 / Valerius | Tutorial completion foregrounds the one existing `OMEN_1` as `OFFERED`; it does not accept or progress it. `autoAccept` remains false and `SELECT_VALERIUS` remains the explicit offer action. Deferral preserves `OFFERED`. | All inputs and restart at offer/choice/runtime states. | Quest visibility, wording, and handoff comprehension are accepted. | **Partially evidenced (M/development).** Isolated tutorial emits one offered handoff without defining a quest; production NVS v004 source/runtime remains in PRs #492/#503. |
| `P0-13` | Clickable active title/objective | Displayed active title and objective each invoke the same `ACTION_FOLLOW_ACTIVE_OBJECTIVE` semantic action. Focused/no-target/unavailable results never mutate quest state, teleport, or relocate, and expose safe detail. | Mouse, keyboard, controller, touch, TalkBack/Switch Access or platform equivalent. | Discoverability and accessible naming are accepted. | **Partially evidenced (M/development).** Editor-only typed follow outcome is non-mutating; current production title/objective remain plain text and device/H evidence is absent. |
| `P0-14` | CH01 appointment/grant/unlock | After `OBJ_C1_ACCEPT_MARK`: appointment -> grant -> review -> enter Kingdom -> return. Appointment precedes grant; unlock needs both. Duplicate events are idempotent; restart restores exact objective/milestones. OMEN completion grants none. | Restart/fault at each milestone and mode transition. | Appointment/grant meaning and guided introduction are accepted. | **Source/runtime stack open, not on main.** PRs #492/#503 remain separately reviewable; no current-main integrated passage is claimed. |
| `P0-15` | Menu-only Kingdom round trip | Kingdom is absent/locked before unlock. Only the shared menu enters `2_5d_inner_kingdom` and returns to `3d_inner_realm`. Profile, session, quest, objective, and navigation context persist. No permanent gameplay-HUD direct switch. | PC/mobile/controller menu, scene interruption, failed switch, and reload. | Round-trip usability and continuity are accepted. | **Missing; direct HUD conflict exists.** `#461`, consuming PR #485 after disposition. |
| `P0-16` | Accessibility/input parity | Every mandatory action has equivalent keyboard/mouse, controller, touch, and accessible activation. Initial focus, focus order, back, and focus restoration are deterministic; no pointer-only action or focus trap. Meaning is not color/audio-only. | Physical device/input/accessibility-service evidence; safe area, orientation, text/UI scale. | Users validate comprehension, comfort, motor, vision, and hearing access. | **Partially evidenced (M/development).** Isolated UGUI/Game Test covers initial focus, semantic activation, friendly copy, and minimum UI-unit targets; production, dp/device, assistive-service, and H evidence remain missing. |
| `P0-17` | Performance/package truth | Publish build hash, compressed download/install/patch delta, media bytes, load/readiness timings, frame distribution, CPU/GPU/GC/memory/thermal/crash/ANR/LMK data and unavailable measurements. No unmeasured support claim. | Declared primary PC and representative physical low/mid Android tiers. | Responsiveness and comfort are accepted. | **Integrated evidence missing.** The pure route planner has a 4,096-call zero-allocation microcheck only; no Player, package, frame, memory, thermal, crash, or device distribution is established. |

Any failed P0 row blocks an integrated vertical-slice acceptance claim, but does not prohibit
focused engineering toward that row.

## 5. P1 and P2 acceptance

### P1 — required hardening after the P0 contract exists

1. **Skip/replay:** an accessible skip never bypasses readiness; replay never mutates profile,
   tutorial, quest, or milestone state.
2. **Account/profile recovery:** local backup recovery, authority unavailable, reservation
   expiry, and commit-uncertain outcomes have bounded, privacy-safe UI and same-operation
   reconciliation.
3. **Performance scaling:** low-memory media fallback, bounded VFX/weather, caption parity,
   and scalable quality behavior are measured without adding a competing global quality owner.
4. **Evidence observability:** privacy-minimal operation/stage/duration/error-category evidence
   contains no username, free text, raw account/profile/character/device ID, or provider secret.
5. **Responsive presentation:** supported PC/mobile aspect, safe-area, text/UI scale, input,
   focus, caption, reduced-motion, and photosensitivity cases have both machine and device
   evidence.

### P2 — polish and later evidence

1. Film-to-creation-to-WorldIntro camera, audio, weather, and VFX continuity receives H review.
2. Tutorial and cinematic replay/reset preserve durable progression.
3. Accessibility preferences persist across restart and platform lifecycle.
4. Privacy-reviewed cohort telemetry begins only after backend, retention, and user authority;
   this matrix authorizes no upload.

## 6. Restart and interruption oracle

The same table drives EditMode contract fixtures, PlayMode fault injection, Player smoke
markers, and device process-kill runs.

| ID | Interrupted boundary | Required resume |
| --- | --- | --- |
| `R00` | Readiness providers pending | Same truthful loading state; media may continue looping. |
| `R01` | Media terminal, readiness pending | Loading remains pending; Continue disabled. |
| `R02` | All ready, Continue not submitted | Finished Loading, Continue enabled, no route. |
| `R03` | Continue accepted, route pending | Reuse transition identity; never accept a second Continue. |
| `R04` | Realm draft | Realm step; authoritative save unchanged. |
| `R05` | Realm selected / Race derived | ClassFamily step with the exact derived Race. |
| `R06` | ClassFamily selected | Customization with the same realm/Race/ClassFamily. |
| `R07` | Customization draft | Exact customization draft and source revision. |
| `R08` | Username validation pending | Same draft and pending/retry state. |
| `R09` | Username rejected | Username step with all prior choices intact. |
| `R10` | Authoritative commit outcome unknown | Reconcile the same operation; do not resubmit. |
| `R11` | Receipt committed, projection absent | Install/retry projection; no gameplay. |
| `R12` | Projection installed, WorldIntro not active | Activate WorldIntro; never Kingdom. |
| `R13` | WorldIntro move active | Exact move objective and identity context. |
| `R14` | Move committed, attack pending | Common basic-attack objective. |
| `R15` | Attack accepted, tutorial commit uncertain | Reconcile the same completion; do not emit twice. |
| `R16` | Tutorial complete, OMEN not foregrounded | Existing `OMEN_1` in `OFFERED`. |
| `R17` | OMEN offer deferred | `OFFERED`; tutorial remains complete. |
| `R18` | OMEN accepted/active | Exact existing NVS state. |
| `R19` | OMEN complete / CH01 active | CH01; no appointment or grant implied. |
| `R20` | `OBJ_C1_ACCEPT_MARK` complete | Lord appointment objective. |
| `R21` | Appointment committed, grant pending | Grant objective with appointment retained. |
| `R22` | Grant/unlock committed | Exact review or mode-round-trip objective and milestones. |
| `R23` | Kingdom entered | Kingdom mode with return objective and all context. |
| `R24` | Returned to Character mode | 3D mode with CH01 continuation/completion context. |

At every row also exercise application quit/relaunch, forced process death, scene reload,
focus/background, duplicate/reordered/late callbacks, repeated input, unavailable dependency,
and corrupt/stale/forward evidence where applicable.

## 7. Hard-fail oracle

Any of the following is an immediate P0 failure:

- a realm-only or otherwise incomplete profile reaches Kingdom;
- elapsed time, an animated percentage, video frame/time/end/skip/error/fallback, or nominal
  media duration declares Finished Loading;
- media end, readiness completion, or a late callback routes without explicit Continue;
- duplicate click, tap, key, controller, IME, retry, or callback causes more than one route,
  profile, character, receipt, projection, tutorial completion, quest mutation, grant, or kingdom;
- Race is independently selectable, stale, or mismatched with realm;
- ClassFamily is absent, defaulted, aliased, or inferred from subclass, preset, weapon,
  `starterClassBias`, label, silhouette, catalog order, or enum ordinal;
- any realm/class/customization/username draft edit changes authoritative save bytes before a
  matching receipt is installed as a verified projection (**save pollution**);
- `ProfileId` equals, derives from, is renamed with, is displayed as, or becomes uniqueness
  authority for username;
- raw username or internal account/profile/character/operation/receipt/provider/diagnostic IDs
  appear in logs or player copy;
- gameplay starts with receipt-only, projection-only, mismatched, stale, or uncertain proof;
- an unknown commit outcome creates a new operation instead of reconciling the original;
- WorldIntro or either tutorial objective is skipped, the tutorial becomes a quest, or attack
  completion requires hit, damage, target, kill, or reward;
- tutorial completion accepts/progresses/completes `OMEN_1` or creates a parallel quest;
- OMEN completion grants Kingdom authority or routes to Kingdom before CH01 milestones;
- active-title/objective activation mutates quest progress, teleports/relocates, or is
  pointer-only;
- appointment, grant, and unlock order is violated or a duplicate creates another kingdom;
- any permanent gameplay-HUD control bypasses the shared-menu-only mode transition;
- restart advances, rolls back, fabricates completion, drops a valid draft, or repeats mutation;
- a machine ID, enum token, raw localization key, exception, provider response, or stack trace is
  rendered to the player;
- a green fragment test, static screenshot, Editor preview, or planning approval is reported as
  integrated M, D, or H passage.

## 8. Reusable current tests

These are retained seams, not end-to-end acceptance.

| Existing test path | Reuse | Required correction or limit |
| --- | --- | --- |
| `unity/Assets/AL/Tests/EditMode/LaunchReadinessCoordinatorTests.cs` | Independent readiness predicates, stale attempts, explicit one-shot Continue, bounded Retry, and destination failure. | Real patch/media providers, a production film loop, Player/device/lifecycle evidence, and the full post-Continue route remain outside this suite. |
| `unity/Assets/AL/Tests/EditMode/LaunchCinematicRuntimeTests.cs` | Media record/platform caps, exact 1,440-frame/24-FPS master, skip, and exactly-once lifecycle. | Integrate with a production media loop/caption/audio adapter and physical-device evidence. |
| `unity/Assets/AL/Tests/EditMode/FirstUserRouteAdmissionPlannerTests.cs` | Exact ordered prefix, cursor conflict/stale/forward rejection, host/writable gates, production/development ceilings, Kingdom denial, determinism, and warm allocation. | Pure planner only; it neither persists nor loads a production destination. |
| `unity/Assets/AL/Tests/EditMode/FirstUserIdentity/FirstUserIdentityDraftFlowTests.cs` | Four Realm/Race mappings, no ClassFamily default, explicit supported family, invalidation, and terminal customization boundary. | Development draft only; it does not prove customization, username, commit, projection, or production navigation. |
| `unity/Assets/AL/Tests/PlayMode/FirstUserIdentity/FirstUserIdentityDraftPlayModeTests.cs` | Standalone UGUI interaction through the customization boundary. | Editor PlayMode fragment, not Player/device evidence; intentionally performs no production navigation. |
| `unity/Assets/AL/Tests/EditMode/DevelopmentOnboardingAuthority/DeterministicDevelopmentOnboardingAuthorityEmulatorTests.cs` | Deterministic handle, full-tuple commit, receipt, projection, replay, collision, tamper, concurrency, and capacity fixtures. | Editor-only emulator; cannot satisfy production authority, backend, save, privacy, or device gates. |
| `unity/Assets/AL/Tests/EditMode/DevelopmentOnboardingAuthority/DevelopmentOnboardingAuthorityRetainedStateTests.cs` | Byte-stable development retained-state and fail-closed corruption/capacity recovery. | Does not install or migrate a production profile/save. |
| `unity/Assets/AL/Tests/EditMode/FirstUserGameTest/FirstUserGameTestAdapterTests.cs` | Exact development evidence binding, forged-pair rejection, receipt/projection replay, and no-save/no-production-route guard. | Isolated Game Test only. |
| `unity/Assets/AL/Tests/EditMode/FirstUserGameTest/FirstUserGameTestTutorialHandoffTests.cs` | MOVE -> BASIC_ATTACK -> one-shot `OMEN_1` offered/follow, duplicate/out-of-order/cross-generation/reload rejection. | Editor-only and explicitly nonquest; it does not mount or mutate the production quest runtime. |
| `unity/Assets/AL/Tests/PlayMode/FirstUserGameTest/FirstUserGameTestJourneyTests.cs` | One integrated development journey into a controllable isolated arena and tutorial handoff. | No production Player route, durable save, physical device, CH01, or Kingdom round trip. |
| `unity/Assets/AL/Tests/EditMode/GameTestModePlannerTests.cs` | Exact temporary-root ownership, recovery record, scene restoration, cleanup, and session isolation for Game Test. | Test-environment safety only; it is not first-user production recovery. |
| `unity/Assets/AL/Tests/EditMode/RealmSelection/RealmCatalogAndSelectionTests.cs` | Four canonical realms, loader bounds, realm commit/idempotence. | Realm selection must become part of a first-user draft/finalization transaction. |
| `unity/Assets/AL/Tests/EditMode/RealmSelection/RealmSelectionMobileReadinessTests.cs` | Safe-area and responsive layout math. | Static math is not device/input/accessibility evidence. |
| `unity/Assets/AL/Tests/EditMode/Customization/CustomizationDraftPlannerTests.cs` | Non-destructive draft transitions, stale-source rejection, deterministic randomization. | Bind ClassFamily and onboarding operation without making production defaults. |
| `unity/Assets/AL/Tests/EditMode/Customization/CustomizationCompatibilityPlannerTests.cs` | Exact raw-state/schema preservation. | Does not prove onboarding persistence or UI. |
| `unity/Assets/AL/Tests/EditMode/Customization/CustomizationAppearancePlannerTests.cs` | Atomic preview/apply/rollback planning. | Does not authorize per-edit save or final profile commit. |
| `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01QuestRuntimeTests.cs` | Offer deferral, exact choice, retry, duplicate/mismatch, immutable state. | Add tutorial-to-existing-offer integration; retain one quest authority. |
| `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01PersistenceTests.cs` | NVS round trip, corrupt-primary recovery, version checks. | Does not represent first-user cursor or CH01 milestones. |
| `unity/Assets/AL/Tests/EditMode/Narrative/PersistenceAuthority/Nvs01ProfileAuthorityPersistenceTests.cs` | Same-operation install/replay/recovery patterns. | Reuse patterns; do not merge NVS and onboarding authorities. |
| `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01KingdomSceneWiringTests.cs` | Verified packet, explicit actions, visible failure, reload. | Current Kingdom mounting is not the required WorldIntro handoff. |
| `unity/Assets/AL/Tests/EditMode/ProductionScenes/ProductionPlayerLaunchSmokeTests.cs` | Process isolation, marker order, timeout, early-exit, severe-error inventory. | Replace expected marker sequence with every mandatory vertical-slice boundary. |
| `unity/Assets/AL/Tests/EditMode/Architecture/ArchitectureMobileReadinessTests.cs` | Static source/asset hygiene. | Never claim physical device, memory, performance, accessibility, or package passage. |

The following current tests encode the old route and must be deliberately replaced or inverted:

- `ProductionSceneFlowTests.BootTransitionsResolveToCommittedRealmSelectionAndKingdom`;
- `ProductionSceneFlowTests.RealmSelectionTransitionResolvesToKingdom`.

Current lifecycle tests correctly require explicit Continue before RealmSelection:

- `ProductionSceneLifecycleTests.BootWaitsForExplicitContinueThenReachesRealmSelectionWithFourControls`;
- `ProductionSceneLifecycleTests.BootWithCommittedRealmStillRequiresContinueAndCannotBypassOnboarding`.

## 9. Planned executable test inventory and owners

The exact production/integration target files below do not yet exist. Current-main tests in
section 8 cover portions under different paths and development-only assemblies. The target
names remain planned and must not be reported as run or passed until an engineering PR creates
and executes them against the production route.

### 9.1 Shared test fixtures

Path: `unity/Assets/AL/Tests/EditMode/FirstUser/FirstUserScenarioFixtures.cs`

Owner: A1 contract sequencing plus engineering test infrastructure.

Fixtures: deterministic media terminal, readiness inventory, destination activator,
onboarding authority, fault-injecting projection store, operation/receipt builders, four
realm/Race/ClassFamily cases, and the `R00`–`R24` restart source. Fixtures contain no production
defaults and no provider/network dependency.

### 9.2 Readiness and Continue

Path: `unity/Assets/AL/Tests/EditMode/FirstUser/LaunchReadinessCoordinatorTests.cs`

Owner: issue `#284` engineering.

- `MediaCompletionAloneCannotDeclareFinishedLoading`
- `ElapsedTimerCannotAdvanceReadinessOrEnableContinue`
- `FinishedLoadingRequiresMediaTerminalAllMandatoryReadinessAndDestination`
- `LoopingMediaRemainsIndependentAcrossReadinessChanges`
- `ContinueBeforeReadyFailsWithoutTransition`
- `RapidDuplicateContinueTransitionsExactlyOnce`
- `LateCallbacksAfterContinueCannotReenter`
- `RetryPublishesNoPriorAttemptEvidenceUntilProvidersRepublish`
- `MasterFilmRequiresExactly1440FramesAt24Fps`
- `ReducedMotionAndDecodeFallbackTerminalizeMediaWithoutClaimingReadiness`

### 9.3 Journey router

Path: `unity/Assets/AL/Tests/EditMode/FirstUser/FirstUserJourneyRouterTests.cs`

Owner: issue `#467` route engineering.

- `FreshProfileStartsAtTruthfulLoadingGate`
- `EveryIncompleteCursorRoutesToItsExactRequiredStep`
- `RealmOnlyProfileCannotRouteToKingdomOrWorldIntro`
- `ReceiptWithoutProjectionCannotEnterWorldIntro`
- `ProjectionWithoutMatchingReceiptCannotEnterWorldIntro`
- `CompletedOnboardingRoutesToWorldIntroUntilTutorialCompletes`
- `TutorialCompletionRoutesToOmenOfferedNotKingdom`
- `KingdomRequiresC1AppointmentGrantUnlockAndMenuAction`
- `MalformedStaleAndForwardCursorFailClosed`
- `NoProductionTransitionSkipsAMandatoryStep`

### 9.4 Draft, commit, identity, and recovery

Path: `unity/Assets/AL/Tests/EditMode/FirstUser/OnboardingDraftCommitTests.cs`

Owners: issues `#173`, `#184`, `#450`, and `#137` engineering.

- `EveryRealmDerivesItsExactRace`
- `CrossRealmRaceInjectionRejectsWithoutDraftMutation`
- `ClassFamilyMustBeExplicitAndOneOfFour`
- `SubclassPresetWeaponStarterBiasAndOrderCannotInferClass`
- `BackReentryAndRecreationPreserveFullDraft`
- `CustomizationEditsDoNotWriteAuthoritativeSave`
- `UsernameFailuresPreserveRealmRaceClassAndCustomization`
- `UsernameIsPublicHandleAndNeverProfileId`
- `DraftFieldsDoNotPolluteSaveBeforeAuthoritativeReceipt`
- `CommitRequestBindsRealmRaceClassCustomizationAndUsername`
- `DuplicateSubmitReusesOneOperation`
- `ReceiptMismatchRejectsLocalProjection`
- `MatchingReceiptAndProjectionFinalizeExactlyOnce`
- `UncertainCommitReconcilesTheSameOperation`
- `RejectedFinalizationLeavesPriorSaveByteStable`
- `DiagnosticsAndCopyNeverExposeUsernameAsInternalIdentity`

Path: `unity/Assets/AL/Tests/EditMode/FirstUser/FirstUserRecoveryTests.cs`

Owners: issues `#450` and `#137` plus each journey-step owner.

- `RestartAtEveryJourneyBoundaryRestoresTheExactExpectedState`
- `DuplicateReorderedAndLateCallbacksCannotAdvanceTwice`
- `ProcessDeathDuringAuthorityPhasesNeverMintsANewOperation`
- `LowStorageProjectionFailurePreservesReceiptForRetry`
- `CorruptPrimaryUsesLastValidBackupWithoutFabricatingCompletion`
- `ForwardStateIsReadOnlyAndCannotEnterGameplay`
- `BackgroundAndFocusLossDoNotAutoContinueOrLoseDraft`

### 9.5 Integrated PlayMode and WorldIntro

Path: `unity/Assets/AL/Tests/PlayMode/FirstUser/FirstUserPlayableVerticalSliceTests.cs`

Owner: issue `#467` integration engineering under A1 sequencing.

- `FreshProfileCompletesTheExactVerticalSliceInOrder`
- `DirectKingdomAndRealmOnlyEntrypointsFailClosed`
- `NoSceneActivatesBeforeExplicitContinue`
- `ReceiptAndProjectionAreRequiredBeforeWorldIntroControl`
- `TutorialMoveThenBasicAttackForegroundsOmenOffered`
- `SelectValeriusAdvancesTheExistingOmenWithoutDuplicateQuest`
- `Chapter01AppointmentGrantAndUnlockRemainOrdered`
- `MenuOnlyKingdomRoundTripPreservesAllContext`

Path: `unity/Assets/AL/Tests/PlayMode/FirstUser/WorldIntroTutorialFlowTests.cs`

Owner: issue `#467` engineering.

- `MoveThenBasicAttackCompletesExactlyOnce`
- `DuplicateOutOfOrderAndMismatchedEvidenceFailClosed`
- `KeyboardControllerAndTouchCompleteTheSameObjectives`
- `ReloadRestoresExactActiveObjectiveAndIdentityContext`
- `PostCompletionReplayCannotReopenOrReemit`

### 9.6 Quest follow, CH01, and menu round trip

Path: `unity/Assets/AL/Tests/PlayMode/FirstUser/ActiveObjectiveFollowTests.cs`

Owner: issue `#467` UI engineering.

- `TitleAndObjectiveInvokeTheSameFollowActionAcrossInputs`
- `FollowNeverMutatesQuestOrPlayerPosition`
- `NoTargetAndUnavailablePreserveStateAndOpenSafeDetail`
- `FocusAndAccessibilityActivationUseTheSameSemanticAction`

Path: `unity/Assets/AL/Tests/EditMode/FirstUser/Chapter01KingdomUnlockTests.cs`

Owners: issue `#274/#467` source synchronization and engineering.

- `AppointmentPrecedesGrantAndGrantPrecedesUnlock`
- `DuplicateMilestoneEventsCannotAdvanceOrCreateAnotherKingdom`
- `RestartAtEachMilestoneRestoresExactObjectiveAndContext`
- `OmenCompletionCannotGrantKingdomAuthority`

Path: `unity/Assets/AL/Tests/PlayMode/FirstUser/SharedGameMenuRoundTripTests.cs`

Owner: issue `#461` engineering after the coordination contract is accepted.

- `KingdomIsUnavailableBeforeQuestUnlock`
- `UnlockedMenuRoundTripPreservesProfileSessionQuestObjectiveAndNavigation`
- `PermanentGameplayHudContainsNoDirectCrossModeRoute`
- `FailedModeSwitchRetriesTheSameObjectiveWithoutAuthorityLoss`

### 9.7 Input, accessibility, Player, and Android host

Path: `unity/Assets/AL/Tests/PlayMode/FirstUser/FirstUserInputNavigationTests.cs`

Owners: each feature UI owner plus issue `#135`.

- `KeyboardMouseControllerAndTouchExposeEveryMandatoryAction`
- `InitialFocusOrderBackAndFocusRestoreAreDeterministic`
- `DoubleSubmitAcrossInputSourcesTransitionsOnce`
- `UsernameImeSubmitDoesNotDoubleFinalize`
- `PointerOnlyInteractionIsRejectedBySemanticCoverage`

Path: `unity/Assets/AL/Tests/PlayMode/FirstUser/FirstUserResponsiveAccessibilityTests.cs`

Owners: feature UI, issue `#135`, and A7 evidence review.

- `MandatoryControlsRemainVisibleAndNonOverlapping`
- `MobileInteractiveTargetsMeet48Dp`
- `LargeTextSafeAreaAndOrientationPreserveFocusAndContent`
- `CaptionsAndAudioOffPreserveCriticalMeaning`
- `ReducedMotionPreservesMeaningWithoutNonessentialMotion`
- `LoopSeamSatisfiesPhotosensitivityGate`
- `StatusNeverDependsOnlyOnColorAudioOrHaptics`

Path: `unity/Assets/AL/Tests/EditMode/ProductionScenes/FirstUserPlayerLaunchSmokeTests.cs`

Owner: production build engineering. Reuse the current process/isolation evaluator, but require
exact boundary markers and hard-fail on Kingdom-before-unlock, timer/video readiness,
receipt/projection absence, or missing WorldIntro.

Path: `app/src/androidTest/java/com/example/anotherlife/ui/firstuser/FirstUserHostLifecycleJourneyTest.kt`

Owner: issue `#135` Android host engineering.

- `ConfigurationChangeRestoresExactCursorWithoutAutoContinue`
- `ProcessDeathRestoresDraftReceiptAndProjectionBoundary`
- `BackgroundDuringCommitDoesNotDuplicateAuthority`
- `SystemBackPreservesOrExplicitlyDiscardsDraft`
- `ImeSubmitReusesTheSameOperation`

Path: `app/src/androidTest/java/com/example/anotherlife/ui/firstuser/FirstUserAccessibilityDeviceTest.kt`

Owner: issue `#135` UI/device engineering.

- `TalkBackAndSwitchAccessReachEveryMandatoryAction`
- `TouchTargetsAndLargeTextRemainUsableAcrossSafeAreas`
- `ReducedMotionAndCaptionsPersistAcrossRecreation`

## 10. Device, accessibility, performance, and package evidence

### 10.1 Required configurations

| Platform/input | Minimum evidence shape |
| --- | --- |
| Windows keyboard/mouse | Development and candidate Player builds; 1280×720, 1920×1080, and representative ultrawide; windowed/fullscreen; cold/warm launch. |
| Windows controller | Declared controller at 1280×720 and 1920×1080; focus order, disconnect/reconnect, simultaneous input, back/cancel, and haptics-off parity. |
| Android touch | Representative physical low and primary tiers; portrait/landscape policy declared; 360×800-equivalent and 412×915-equivalent safe-area/cutout cases; hardware keyboard/IME where supported. |
| Android accessibility | TalkBack and Switch Access (or declared supported equivalents), large text/UI scale, reduced motion, captions/audio-off, color-independent status. |
| Lifecycle/fault | Cold restart and process kill at `R00`–`R24`; background/foreground; low memory/storage; decoder/catalog/network/authority failure; controller disconnect; locale/IME change. |

Each D record identifies build SHA, source SHA, device-model hash, OS, CPU/GPU/RAM, viewport,
safe area, input, quality tier, cold/warm state, exact scenario, logs, profiler/capture paths, and
every unavailable measurement. Raw personal/device identifiers do not enter Git.

### 10.2 Binding limits already in source/planning authority

- Master film: exactly `1,440` frames at `24 FPS` over half-open `[0,1440)`; container probe
  tolerance never adds or drops a master frame.
- Desktop cinematic encode: at most `95,000,000` bytes at the declared 1920×1080 profile.
- Android cinematic encode: at most `42,000,000` bytes at the declared 1280×720 profile.
- Android touch targets use the platform baseline of at least 48×48 dp; this is an
  accessibility/platform requirement, not approval of a final layout.
- No total game install, patch, working-set, or supported-device cap is approved on this
  baseline. Every such value remains measured evidence plus a later A1/user decision.

### 10.3 Proposed AnotherLife hypotheses

All values in this table are planning targets only. They are not current performance,
accessibility, support-device, or release claims.

| ID | Proposed hypothesis | Required evidence |
| --- | --- | --- |
| `HYP-LATENCY-ACK` | Visible input acknowledgement p95 ≤100 ms. | Input timestamp to first visible state, separated by keyboard/mouse, controller, and touch. |
| `HYP-TIME-CONTROL` | Explicit Continue to first controllable WorldIntro frame p95 ≤5 s PC and ≤8 s Android after required local media/data are ready. | Cold/warm Player distributions with loading stages and device identity. |
| `HYP-MEDIA-FIRST-FRAME` | Media first frame p95 ≤2 s PC and ≤4 s Android; deterministic fallback by the existing 8 s prepare bound. | Decoder/codec/device matrix including failure and reduced-motion paths. |
| `HYP-FRAME-PC` | PC gameplay p95 frame ≤16.7 ms at a declared 60 FPS tier. | CPU/GPU frame-time distribution, 1% lows, scene/build/quality identity. |
| `HYP-FRAME-ANDROID` | Supported low-tier Android p95 frame ≤33.3 ms at a declared 30 FPS tier. | Physical-device frame-time and thermal degradation distribution. |
| `HYP-GC` | Avoidable steady-state GC allocation is 0 B/frame; transition allocations are measured and bounded. | Profiler evidence for cinematic, menus, WorldIntro, quest handoff, and mode switch. |
| `HYP-MEMORY-PC` | Initial measurement ceiling: PC working set ≤1.5 GiB. | Peak/steady working set with build and content identity. |
| `HYP-MEMORY-ANDROID` | Initial measurement ceiling: Android peak PSS ≤512 MiB, with no LMK or ANR. | Physical-device PSS, native/managed split when available, LMK/ANR/crash evidence. |
| `HYP-HUMAN-SAMPLE` | Planning minimum: at least 5 first-time participants per primary input/device cohort and at least 80% unmoderated completion/comprehension for every mandatory step. | Privacy-safe protocol/results plus qualitative findings; user H disposition remains mandatory. |

Record exact compressed download, installed size, first-patch delta, media size, CPU/GPU/GC,
managed/native memory, PSS/working set, thermal/power where available, crash/ANR/LMK, and
startup/readiness/time-to-control distributions. Do not invent a pass when a profiler, metric,
device, or sample is unavailable.

## 11. Evidence output contract

Machine-local and CI evidence belongs in retained artifacts, not committed binaries:

```text
unity/Logs/FirstUser/<run-id>/editmode.xml
unity/Logs/FirstUser/<run-id>/playmode.xml
unity/Logs/FirstUser/<run-id>/player-smoke.json
unity/Logs/FirstUser/<run-id>/metrics.json
unity/Logs/FirstUser/<run-id>/device-matrix.json
unity/Logs/FirstUser/<run-id>/captures/*
```

Every record carries source/build SHA, run identity, tool/Unity version, platform, test/filter,
start/end timestamps, exit code, total/pass/fail/skip, and error inventory. Captures and media are
attached as CI/PR evidence only after privacy and rights review; this coordination artifact
commits none.

## 12. Smallest implementation sequence

1. `#284/#460`: preserve the merged typed readiness/Continue contract, then add truthful patch
   inventory and the approved looping media/caption/audio adapter without weakening Retry or
   exactly-once transition semantics.
2. `#173/#467`: mount the merged derived-Race/explicit-ClassFamily draft and pure route planner
   behind a production first-user coordinator; do not reuse the development evidence origin.
3. `#184`: production customization adapter that consumes the merged non-destructive planners.
4. `#450/#137`: username authority, receipt/reconcile/projection, backward-compatible migration,
   and shared-file locks in a separately authorized engineering PR.
5. `#467`: production WorldIntro route and two-step tutorial.
6. NVS/CH01 source synchronization, then quest-follow and milestone integration tests.
7. `#461`: shared-menu-only round trip after its coordination contract is accepted.
8. Cross-input, host, Player, physical-device, accessibility, performance, and package evidence.
9. A1 technical disposition, applicable narrative fidelity, then user H/integrated playtest.

Each implementation PR stays focused and updates tests owned by that slice. No child may turn
the hypotheses in section 10 into production defaults or balance authority merely because they
appear in this matrix.

## 13. Publication and approval boundary

This coordination publication consists only of:

- `First_User_Playable_Vertical_Slice_Acceptance_Matrix_2026-08-13.md`;
- `First_User_Playable_Vertical_Slice_Test_Manifest.v1.json`.

It acquires no shared-file lock and changes no runtime, scene, prefab, save, schema, catalog,
package, build setting, workflow, media, art, cinematic, narrative source, localization value,
or device state. It authorizes no provider, upload, paid operation, asset ingestion, or release.

## 14. Explicit nonclaims

This artifact does **not** claim or approve:

- a working first-user route, production readiness gate, patcher, cinematic player, onboarding
  authority, WorldIntro, tutorial, CH01 milestone, shared menu, or Kingdom round trip;
- final localized copy, dialogue, narration, voice, race/class release taxonomy, subclass,
  eligibility, ability, skill, effect, PvP, combat, or balance authority;
- cinematic pixels, audio, rights, creative fidelity, accessibility certification, visual
  quality, camera continuity, or user approval;
- Windows/Android Player passage, physical-device behavior, performance, memory, thermal,
  package/install/patch compliance, supported-device coverage, or human comprehension;
- save migration, backend/provider, username reservation, receipt, projection, telemetry,
  privacy compliance, production, milestone, release candidate, or release readiness;
- closure of `#467`, `#284`, `#173`, `#184`, `#450`, `#137`, `#135`, `#274`, or `#461`;
- replacement of user/co-developer source authority or approval of any unrelated open PR.

Current phase: coordination evidence and executable test planning. Acceptance status: P0
no-go, engineering unblocked through the ordered children above. Next valid steps are A1 review
of this focused draft and focused engineering—not a broad redesign or PvP/balance work.
