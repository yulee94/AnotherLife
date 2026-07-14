# Another Life: Narrative Governance

This document defines the strict naming rules and ID registry policies to ensure scalable content expansion without reference drift.

## 1. ID Naming Conventions

All IDs must be **UPPER_SNAKE_CASE** and globally unique.

| Content Type | ID Prefix | Example |
| --- | --- | --- |
| Chapter | `CH[N]_` | `CH1_PROLOGUE`, `CH2_TREASURE_HUNT` |
| Quest | `Q_` or `QUEST_` | `QUEST_OMEN_1`, `Q_CL_REBUILD_1` |
| Dialogue Node | `DLG_` | `DLG_OMEN_1_START`, `DLG_SMITH_GOSSIP` |
| Objective | `OBJ_` | `OBJ_OMEN_1_TALK` |
| NPC / Advisor | `NPC_` or `ADVISOR_` | `ADVISOR_VALERIUS`, `NPC_SMITH_GRUFF` |
| Faction | `FACT_` | `FACT_HUMAN_COUNCIL` |
| Reward | `REW_` | `REW_GOLD_500`, `REW_OMEN_1_TEAR` |
| Gameplay Hook | `HOOK_` | `HOOK_SKY_CASTLE_ARENA` |
| Event | `EVENT_` | `EVENT_ARENA_CLEAR`, `EVENT_REALM_SELECTED` |

## 2. ID Registration Policy

1.  **Stable IDs First**: IDs must be assigned during the narrative authoring phase (Android Studio) BEFORE technical specification (GPT) or implementation (Codex).
2.  **No In-Code Strings**: Runtime logic must refer to these stable IDs via constants or contract mappings.
3.  **Cross-Chapter Continuity**: IDs for recurring NPCs or artifacts must remain constant across all chapters.

## 3. Localization Key Convention

Format: `[content_type].[id].[field]`

- `dialogue.DLG_OMEN_1_START.text`
- `quest.QUEST_OMEN_1.title`
- `npc.ADVISOR_VALERIUS.name`

## 4. Persistence & Recovery Semantics

1.  **Atomic Dialogue Nodes**: If the game is saved mid-dialogue, the resume point must be the **Start** of the current `DialogueNode`.
2.  **Idempotent Consequences**: Consequences (Gold, Artifacts, Affinity) must only trigger once. If a state transition occurs, the corresponding consequence must be flagged as "Applied" in the save data.
3.  **Handoff Recovery**: If the game is closed during a 3D Arena encounter, the player should resume in the **Kingdom State** immediately prior to the handoff, with a "Retry" prompt available.

## 5. Continuity Checks

Before merging any narrative packet, the author must verify:
- [ ] No duplicate IDs exist in current main.
- [ ] All `NEXT_NODE_ID` references resolve to an existing or newly defined node.
- [ ] Consequence triggers (`EVENT_` or `HOOK_`) match the approved registry.
