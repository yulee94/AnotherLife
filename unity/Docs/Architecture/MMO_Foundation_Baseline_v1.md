# MMO Foundation Baseline MMO-BL-20260831-001

**Baseline ID:** `MMO-BL-20260831-001`

**Record version:** `1.0.0`

**State:** `CURRENT AT CAPTURED COMMIT — PROCEED WITH CONTROLLED DOWNSTREAM WORK`

**Captured UTC:** `2026-08-30T23:11:49Z`

**Captured repository commit:** `a18de3698f85130c1dcdbf7aac652cdb2adfc6a6`

**Supersedes:** None. A material change creates a new baseline record; this file remains historical evidence.

## Approval evidence and entry-gate decision

The hard entry dependency is satisfied.

- Kanban `APPROVAL_GATE` task `t_0648ce23` contains the explicit game-owner comment dated 2026-08-28: `APPROVE the complete integrated post-MVP roadmap DAG and begin unattended weekend Kanban execution.` The task is complete and is a direct parent of baseline task `t_fce8b06f`.
- The later candidate-specific approval is preserved in [AP-GR-GT-00-RC-20260828-002-003-001](../Roadmap/Approvals/GT-00/RC-20260828-002/AP-GR-GT-00-RC-20260828-002-003-001.md).
- The current immutable roadmap promotion and its completed `t_0648ce23` dependency are preserved in [RB-20260828-002](../Roadmap/Baselines/RB-20260828-002/manifest.md).

Approval applies to the approved dependency-ordered roadmap. It does not waive downstream evidence gates or transfer owner-exclusive vendor, spend, scope, release, creative, visual, balance, or monetization authority.

## Classification vocabulary

- **Observed fact:** directly inspectable at the captured repository commit. This is configuration or source-state evidence, not proof of production behavior or capacity.
- **Approved planning requirement:** an owner-approved target or topology assumption that downstream design and tests must cover. It is not an achieved measurement.
- **Unproven validation target:** a future workload that must pass representative measurement before any capacity claim.

## Observed repository facts

| Area | Observed fact at captured commit | Evidence boundary |
| --- | --- | --- |
| Client runtime | The `Bootloader` constructs and publishes an offline/local service stack. | `unity/Assets/AL/Scripts/Core/Bootloader.cs` declares `OfflineStackVersion`, calls `OfflineServiceStack.Create`, and defaults to `LocalGameDataService`, `LocalSaveGameService`, and the other local services. This does not prove a networked or production-authoritative runtime. |
| Content delivery | Addressables is not installed or configured. | `unity/Packages/manifest.json` has no `com.unity.addressables` dependency, and no Addressables settings/assets exist under `unity/Assets`. Unity's built-in AssetBundle module is present; that is not an Addressables installation. |
| Player build scenes | Exactly 5 scenes are enabled in committed Build Settings: `Boot`, `RealmSelection`, `CharacterCreation`, `ChampionArena`, and `Kingdom`. | `unity/ProjectSettings/EditorBuildSettings.asset`; all five entries have `enabled: 1`. |
| Generated world scenes | Exactly 78 `.unity` scene files exist under `unity/Assets/AL/Worlds/Generated`. | Repository file enumeration at the captured commit. These generated scenes are distinct from the five enabled Build Settings scenes and are not, by this count alone, player-build or runtime-streaming evidence. |
| Android export | `AndroidUnityLibraryExporter.RequiredUnityVersion` is pinned to `2022.3.62f3`. | `unity/Assets/AL/Scripts/Editor/AndroidUnityLibraryExporter.cs`. This exporter-specific pin is not the current project-wide Editor version: `UnityVersionGuard` and `ProductionPlayerBuilder` require `6000.3.22f1`. The discrepancy must be resolved by a later reviewed Android-export change, not silently normalized in this baseline. |
| Narrative | The main-quest packet reports `canonical_narrative_source_complete_runtime_not_wired`. | `unity/Docs/Narrative/MainQuestLine/ANOTHERLIFE_MAIN_QUEST_LINE.packet.json`. Authored source completeness is not runtime integration evidence. |
| MMO backend | No production MMO backend exists. | `server/README.md` identifies `al_server_core` as an engine-free protocol/state-machine prototype not connected to Unity, the Internet, persistence, or production orchestration. Local harness results are explicitly not production capacity evidence. `unity/Docs/Architecture/Authoritative_Multiplayer_Backend_And_Security_Architecture.md` is a proposed boundary, not a deployed system. |

## Approved planning requirements, not measured capacity

| Requirement | Classification | Claim boundary |
| --- | --- | --- |
| 4 realms per regional server | Approved planning requirement | Required topology input; not evidence that a regional server has been implemented or operated. |
| At least 5,000 active accounts per realm | Approved planning requirement | Account-population target; not CCU, zone residency, battle participation, throughput, or latency evidence. |
| At least 20,000 active accounts per regional server | Approved planning requirement | Aggregate account-population target derived from the four-realm requirement; not concurrency or production-capacity evidence. |
| 10,000 steady CCU | Unproven future validation target | Must be demonstrated with representative clients, regional topology, authoritative simulation, persistence, networking, failure injection, observability, and an approved workload definition before it can be called achieved capacity. |
| 20,000 surge CCU | Unproven future validation target | Must be demonstrated as a bounded surge with explicit duration, admission/queue behavior, workload mix, recovery criteria, and no hidden fidelity or correctness waiver before it can be called achieved capacity. |

The 10,000/20,000 CCU targets do not mean 10,000/20,000 mutually interactive full-fidelity combatants in one space. Connected, represented, individually replicated, and causally interactive populations remain separate measures, as required by the proposed multiplayer architecture evidence vocabulary.

## Downstream proceed-or-block status

**PROCEED:** provider-neutral architecture contracts, data classification, threat/failure modeling, equivalent bake-off design, disposable interface skeletons, and reversible GameLift/PlayFab sandbox experiments may proceed through their dependency-ordered cards. Sandbox work must remain isolated, instrumented, non-production, and free of provider-specific gameplay authority.

**BLOCK:** provider selection, provider commitment, production deployment, paid or material spend, production data use, irreversible provider coupling, and any claim that an account or CCU target is achieved remain blocked until their named evidence and owner-decision gates complete. Missing sandbox access, unknown quotas, or incomparable workloads produce an explicit no-selection outcome rather than an inferred winner.

**Reopen this baseline:** create a new versioned record and re-evaluate affected downstream work if the Bootloader authority model, content-delivery system, Build Settings/generated-scene inventory, Android exporter pin, narrative runtime status, backend production status, regional topology, population requirement, CCU target, provider scope, cost/capacity assumption, platform/region/compliance exposure, or owner authority materially changes.

## Review checklist

- [x] Explicit `APPROVAL_GATE` owner evidence identified before sandbox or provider work.
- [x] Repository facts tied to the captured commit and source paths.
- [x] Five enabled scenes and 78 generated scenes recorded as separate inventories.
- [x] Active-account planning requirements separated from CCU validation targets.
- [x] No backend, provider, quota, price, latency, or capacity achievement invented.
- [x] Downstream proceed and block boundaries stated explicitly.
