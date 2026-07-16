# NVS-01 A1 Narrative Packet: OMEN_1 / The First Signal

## Document control

```text
Milestone: NVS-01
Task: A1
Quest: OMEN_1 / The First Signal
Packet version: 1.2 (Clean A1)
Android Studio branch: android-studio/nvs-01-a1-clean
User decision issue: #138
User approval reference: Phase 1 Status Audit 2026-07-14
Narrative owner: Android Studio Narrative Director
GPT review status: Ready
```

## 1. Approval gate

- [x] Issue #138 D1–D16 approval is recorded.
- [x] This packet records those answers exactly.
- [x] The branch started from fetched current `main`.
- [x] The packet contains exactly one bounded `OMEN_1` quest.
- [x] No Unity runtime-owned service, interface, scene, save, or contract file changed.
- [x] No Android runtime model, navigation, Gradle, or unrelated UI file changed.
- [x] All internal IDs and references resolve.
- [x] External semantic dependencies are clearly marked requested.
- [x] Android unit tests and debug assembly pass.

## 2. User-approved decision record

| Decision | Approved answer | Packet sections affected |
| --- | --- | --- |
| D1 — dialogue-to-arena handoff | Semantic request `HOOK_SKY_CASTLE_ARENA` | dialogue, transition, handoff |
| D2 — arena failure recovery | Return to Kingdom, talk to Valerius | state, objective, retry |
| D3 — `FAILED` state meaning | Transient failure, allows retry | state and recovery |
| D4 — Valerius affinity | +5 Affinity at **Report Completion** only | consequence intent |
| D5 — Gold and Celestial Tear | Tear at **Arena Success**, Gold at **Report Completion** | reward and consequence intent |
| D6 — quest completion | Transitions to `COMPLETED` after SUCCESS dialogue | terminal transition |
| D7 — localization policy | Keys for all player-facing text | localization inventory |
| D8 — gameplay-hook status | Requested: `HOOK_SKY_CASTLE_ARENA` | external dependency |
| D9 — cancellation | Allowed; resets quest to `INACTIVE` | cancellation |
| D10 — chapter/realm placement | Chapter 1, Crownlands focus | entry and continuity |
| D11 — Valerius role | Military Advisor for Crownlands | speaker |
| D12 — realm prerequisites | Requires `Crownlands` selection | entry |
| D13 — Celestial Tear | Physical artifact acquired in Arena | objective, artifact |
| D14 — report interaction | **Manual report required** to Valerius after Arena | report behavior |
| D15 — quest-start trigger | Manual interaction with Valerius | unlock |
| D16 — resume intent | Resume at start of current State | persistence |

### Consistency assertions

```text
D2 agrees with D3: pass
D4–D6 agree on consequence and completion order: pass
D10 agrees with D11 and D12: pass
D13 agrees with D5 and report wording: pass
D14 agrees with the final objective and D6: pass
D15 produces one deterministic initial transition: pass
D16 agrees with D2/D3/D5/D6/D14: pass
```

## 3. Purpose and bounded scope

### Player-facing purpose

```text
Establishes the initial threat of celestial vibrations and introduces Captain Valerius as the primary military advisor for the Crownlands.
```

### Included content

```text
- States: INACTIVE, TALK_TO_VALERIUS, INVESTIGATE_SKY_CASTLE, REPORT_TO_VALERIUS, COMPLETED, FAILED.
- Objectives: OBJ_OMEN_1_TALK, OBJ_OMEN_1_ARENA, OBJ_OMEN_1_REPORT.
- Dialogue Nodes: DLG_OMEN_1_START, DLG_OMEN_1_LORE, DLG_OMEN_1_GO, DLG_OMEN_1_SUCCESS, DLG_OMEN_1_FAILURE.
- Consequences: Acquire REW_OMEN_1_TEAR, Gold reward, Advisor affinity, State transitions.
```

## 4. Source-of-truth declaration

### Authoritative packet files

```text
app/src/main/java/com/example/anotherlife/data/simulation/NVS_01_Packet.kt
unity/Docs/A1_Narrative_Packet_OMEN_1.md
```

## 5. Stable ID inventory

| Category | ID | Meaning |
| --- | --- | --- |
| Milestone | `NVS-01` | First Vertical Slice |
| Chapter | `CH1_PROLOGUE` | Realm Rebuilding |
| Quest | `OMEN_1` | The First Signal |
| Advisor | `ADVISOR_VALERIUS` | Captain Valerius |
| Artifact | `REW_OMEN_1_TEAR` | Celestial Tear |
| Hook | `HOOK_SKY_CASTLE_ARENA` | Sky Castle Encounter |

## 6. Entry, placement, and unlock

```text
Realm-selection relationship: Crownlands
Approved chapter/context: CH1_PROLOGUE
Eligible realms: Crownlands
Canonical speaker: ADVISOR_VALERIUS
Authoritative quest-start trigger: MANUAL_INTERACTION:ADVISOR_VALERIUS
Initial state: INACTIVE
First active objective: OBJ_OMEN_1_TALK
```

## 7. State definitions and transition table

| State | Player-facing meaning | Persist/resume meaning | Terminal? |
| --- | --- | --- | --- |
| INACTIVE | Available | Start of quest | No |
| TALK_TO_VALERIUS | Briefing | Start of node | No |
| INVESTIGATE_SKY_CASTLE | Mid-arena | Before handoff | No |
| REPORT_TO_VALERIUS | Manual return | Start of node | No |
| COMPLETED | Finished | Remains completed | Yes |

## 8. Objective progression

| Objective ID | Becomes active when | Completes when |
| --- | --- | --- |
| OBJ_OMEN_1_TALK | Quest start | DLG_OMEN_1_GO choice |
| OBJ_OMEN_1_ARENA | DLG_OMEN_1_GO choice | EVENT_ARENA_ENCOUNTER_SUCCESS |
| OBJ_OMEN_1_REPORT | Arena success | DLG_OMEN_1_SUCCESS end |

## 9. Gameplay handoff and return meaning

```text
Hook ID: HOOK_SKY_CASTLE_ARENA
Status: requested
Expected success event: EVENT_ARENA_ENCOUNTER_SUCCESS:HOOK_SKY_CASTLE_ARENA
Expected failure event: EVENT_ARENA_ENCOUNTER_FAILURE:HOOK_SKY_CASTLE_ARENA
Success destination state: REPORT_TO_VALERIUS
Failure destination state: TALK_TO_VALERIUS
```

## 10. Consequence intent and ordering

| Consequence | Stable target ID | Authoritative trigger | One-time? |
| --- | --- | --- | --- |
| Acquire Tear | REW_OMEN_1_TEAR | Arena Success | Yes |
| Gold Reward | REW_GOLD_500 | Report Completion | Yes |
| Affinity | ADVISOR_VALERIUS | Report Completion | Yes |
| Completion | OMEN_1 | Report Completion | Yes |

## 11. Handoff to GPT

```text
GPT: review this clean A1 packet against issue #138 D1–D16, issue #128, AGENTS.md, the Phase 1 risk register, and ownership boundaries. Do not implement or rewrite narrative. If complete and user-approved, activate #133 and produce G1 from NVS_01_G1_Specification_Template.md.
```
