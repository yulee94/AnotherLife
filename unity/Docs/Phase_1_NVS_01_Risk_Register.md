# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-14  
**Active phase:** Phase 1 — NVS-01  
**Completed decision gate:** issue #138 D1–D16  
**Active dependency:** issue #128 clean A1 packet

This register consolidates verified risks. It does not authorize implementation before the approved A1/G1 handoffs.

Use with:

- `AGENTS.md`
- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/NVS_01_A1_Packet_Template.md`
- `unity/Docs/NVS_01_G1_Specification_Template.md`
- `unity/Docs/NVS_01_Review_and_Acceptance_Checklists.md`
- issues #138, #128, #133, and #134
- draft PR #124

## Severity

- **Critical:** can corrupt data, invalidate the milestone, or cause ownership/merge failure.
- **High:** can make the slice incomplete, non-deterministic, or falsely appear implemented.
- **Medium:** can create drift or missing validation with a bounded workaround.
- **Low:** non-blocking quality or tooling issue.

## Status

- **Open:** needs a decision or implementation.
- **Blocked:** waits for an upstream approved artifact.
- **Contained:** isolated from the active path but unresolved.
- **Deferred:** intentionally scheduled later.
- **Mitigated:** controlled by approved intent, evidence, or process but not fully implemented.
- **Closed:** acceptance evidence is complete for the stated risk.

## Active and resolved risks

| ID | Severity | Risk | Verified evidence | Impact | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- | --- |
| R1 | Critical | Mixed archive branch can overwrite runtime fixes and violate ownership | Draft PR #124 changes A1, Chapter 1, Android model, and Unity runtime files | Regression, lost fixes, phase mixing | GPT + Android Studio | **Contained** — archive only; fresh #128 branch required |
| R2 | Critical | Narrative intent was incomplete or contradictory | Archive had missing handoff, conflicting failure, undefined start/report/Tear/resume | G1/Codex would have invented intent | User + project director | **Closed for intent** — authoritative D1–D16 in #138; A1 fidelity still pending |
| R3 | High | No single authoritative runtime narrative catalog exists | Android packet/seeds, Unity fallback dialogue, generic quests, transient chapters | Duplicate IDs/text and disconnected progression | GPT | **Blocked** — #133 after A1 |
| R4 | High | Named encounter request/result contract is absent | Current flow uses direct scene loads; no verified `HOOK_SKY_CASTLE_ARENA` contract | Quest context/results can be lost | GPT + Codex | **Blocked** — D8 classifies as requested; #133/#134 implement |
| R5 | High | `SKY_CASTLE` is absent from current world atlas | Existing atlas does not register Sky Castle | Marker and Deploy action cannot work yet | Android Studio intent / GPT/Codex technical | **Mitigated for intent** — D12 approved requested marker/action; implementation blocked #133/#134 |
| R6 | Medium | Android Unity bridge depends on packaged Unity export | `UnityView.kt` hosts `UnityPlayer` when the exported runtime is present and shows an unavailable state otherwise | Embedded device smoke remains required once `unityLibrary`/AAR is packaged | GPT + Codex | **Mitigated** — #135 runtime bridge and `Android_Unity_Runtime_Bridge.md`; production packaging still requires Unity export validation |
| R7 | High | Chapter, realm, speaker, location, and start semantics conflicted | Save uses `C1`; archive used other IDs; `AdvanceStory()` does not mutate chapter | Wrong eligibility or false progression | Android Studio, then GPT/Codex | **Mitigated for intent** — D10–D12/D15 approved; A1 encoding active, technical design blocked #133 |
| R8 | High | Old saves may contain null narrative fields | Normalization omits `Reputation`, `FactionReputations`, `LordPersona` | Effects can silently fail or throw | Codex | **Open** — #136; required before #134 consequences |
| R9 | High | Multi-service consequences lack an atomic boundary | Affinity saves immediately; resources/quest save separately; artifact path undefined | Partial application or duplication | GPT + Codex | **Blocked** — order approved by D4–D6/D13/D14; atomicity/idempotency required in #133/#134 |
| R10 | High | Celestial Tear meaning was contradictory | Archive said deliver while also granting as reward | Wrong objective and persistence model | User/project director, then Android Studio | **Closed for intent** — retained artifact presented then kept; A1/G1 implementation pending |
| R11 | High | Missing dialogue can fail silently | `GetDialogue` returns null; `TriggerDialogue` emits nothing | Stalled or skipped narrative | GPT + Codex | **Blocked** — strict G1 validation/error behavior |
| R12 | High | Resume behavior can duplicate or lose progress | Existing runtime lacks approved dialogue/arena/report recovery model | Duplicate rewards or lost objectives | GPT + Codex | **Mitigated for intent** — D16 approved; technical persistence blocked #133/#134 |
| R13 | Medium | `end` is convention, not a typed terminal | Dialogue model stores arbitrary string targets | Missing references can masquerade as completion | Android Studio + GPT | **Open** — A1 declaration and G1 validation required |
| R14 | Medium | Objective lifecycle is incomplete in archived content | Objectives lacked deterministic activation/completion mapping | UI/save/runtime cannot be specified | Android Studio | **Open** — D14/D15 approved; #128 must encode tables/tests |
| R15 | Medium | Android QuestScreen is not authoritative or integrated | Shell does not mount it; claim/locate is placeholder | Preview can drift from Unity runtime | GPT + Codex | **Blocked** — #133; no A1 runtime edits |
| R16 | Medium | Transient chapter creation is not a usable catalog | `AddChapter()` creates objects but service exposes no chapter retrieval | Adding OMEN_1 there would not establish authority | GPT + Codex | **Blocked** — #133/#134 |
| R17 | Medium | Localization keys have no verified runtime pipeline | Broad Unity localization support is absent | Authored keys may not be consumed | Android Studio + GPT + Codex | **Mitigated for authoring** — D7 full inventory; runtime design blocked #133, broad tooling #131 |
| R18 | Medium | Existing shared loaders permit silent fallback | Skill catalog loader returns false/null and runtime defaults can take over | Story data could disappear or substitute | GPT + Codex | **Blocked** — G1 must prohibit silent story fallback |
| R19 | Medium | No committed PlayMode smoke existed at Phase 0 validation | Runner discovered zero tests; representative scene used temporary probe | Scene regressions lack repeatable automation | Codex | **Open** — #127 |
| R20 | Medium | Save writes are not crash-safe | Direct write to one `save.json`; no backup/recovery | Partial write can lose profile | GPT + Codex | **Deferred** — #137 |
| R21 | Low | KSP `2.3.5` emits a known post-success command-line exception | Official fix is `2.3.6` | Noise can obscure failures | Codex | **Open** — #126 narrow bump |
| R22 | Low | Deprecated Compose progress overload | Warning in `QuestScreen.kt` | Warning debt | Codex | **Closed** — PR #125 merged; #132 complete |

## Approved D1–D16 risk controls

Issue #138 now supplies the following controls that A1 and G1 must preserve:

- authored deployment node instead of a dangling target,
- one transient failure/retry path,
- no terminal quest failure,
- Tear acquired once at arena success,
- manual report interaction,
- Gold, affinity, completion, and Chapter 1 unlock grouped at report completion,
- full localization-key inventory,
- honest requested-capability classification,
- abandonment only outside encounter,
- realm-selected universal prologue,
- explicit cross-realm Valerius role,
- requested Sky Castle marker and Deploy action,
- retained Tear with corrected report wording,
- deterministic offer/start trigger,
- exact-node dialogue resume and duplicate-safe encounter/report recovery intent.

These controls close ambiguity but do not prove technical implementation.

## Current ownership dependencies

### Android Studio under #128

Must encode without reinterpretation:

- approved wording and stable IDs,
- deterministic offer/start/state/objective/dialogue tables,
- authored deployment node,
- failure/retry/report/abandonment/resume meaning,
- consequence ordering and repeatability intent,
- localization inventory,
- external dependency declarations,
- focused packet tests.

Android Studio may not redesign runtime architecture.

### GPT after approved A1

May decide only technical matters:

- authoritative runtime representation,
- schema/version/fields,
- loader and validator,
- encounter request/result contract,
- chapter/realm/location/start mapping,
- artifact/report representation,
- persistence/defaults/migration/D16 resume,
- consequence atomicity/idempotency,
- diagnostics,
- required/optional files and locks,
- test architecture and C1–C4 order.

### Codex after approved G1

May decide:

- narrow implementation details within the approved contract,
- test implementation,
- diagnostics,
- safe defaults/migrations,
- required refactoring limited to specified scope.

## Shared-file risk

Potential future implementation may affect:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

Current status: no designated shared file is locked.

Rules:

1. A1 may not edit these files.
2. G1 must justify required versus optional impact.
3. The first approved implementation PR declaring a file holds the soft lock.
4. Save fields require backward-compatible defaults and old-save tests.
5. Service-registration conflicts preserve all valid registrations.
6. Generated artifacts must be deterministic and reviewable.
7. Lock release requires merged/closed PR evidence.

## Current mitigation order

1. **Complete:** merge Phase 0/Phase 1 control docs (#123).
2. **Complete:** merge corrected clean-A1 handoff (#139).
3. **Complete:** approve D1–D16 (#138).
4. **Active:** create `android-studio/nvs-01-a1-clean` from current `main` and complete #128.
5. Preserve draft PR #124 until GPT verifies the clean transfer; then close without merge.
6. Activate #133 only after A1 approval.
7. Complete #136 before #134 applies affinity/faction/persona consequences.
8. Activate #134 only after approved G1 and shared-file declarations.
9. Perform G2, A2, and U1 in order.
10. Update this register at every gate transition.

## Risk review cadence

GPT updates this register when:

- A1 opens or changes scope,
- A1 review resolves or exposes a risk,
- a technical implementation PR opens,
- a shared file is declared,
- validation discovers a new root cause,
- a risk is accepted/deferred,
- or a phase gate changes.

A risk is not closed solely because code compiles. Close it only against the appropriate narrative, ownership, persistence, integration, and player-visible evidence.
