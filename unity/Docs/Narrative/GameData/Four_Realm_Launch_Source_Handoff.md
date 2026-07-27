# Four-Realm Launch Source Handoff

**Packet ID:** `al_narrative_four_realm_launch_source_v001`
**Primary Codex mode:** narrative/content
**Runtime catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json`
**Tracking:** issue #173 and the four-realm launch objective

## Source Intent

This packet completes the A3 narrative/content handoff for the launch realm catalog. It keeps the four playable realm identities stable for the gameplay parser while adding compact continuity guidance and draft localization text for the first realm-selection commitment.

The committed account rule is narrative-facing as well as technical: a player account chooses one realm identity for the current product phase, and every sub-character belongs to that same realm. Realm browsing before commitment is preview-only and must not grant resources, Realm Gems, territory, quests, chapters, or eligibility.

## Stable Realm IDs

| Realm ID | Runtime enum | Account identity | First arc ID |
| --- | --- | --- | --- |
| `crownlands` | `Crownlands` | `sworn_banner_holder` | `quest_arc_crownlands_oathroad` |
| `stonehold` | `Stonehold` | `forge_gate_keeper` | `quest_arc_stonehold_anvil_oath` |
| `eldergrove` | `Eldergrove` | `living_border_guardian` | `quest_arc_eldergrove_rootwatch` |
| `umbral` | `Umbral` | `veiled_claimant` | `quest_arc_umbral_veilclaim` |

The catalog retains exactly two Realm Gem IDs per realm and keeps every ID lowercase snake-case for runtime parsing.

## Localization Drafts

The runtime catalog now carries draft source text for:

- `realm.lock.warning`
- `realm.crownlands.selection.line`
- `realm.stonehold.selection.line`
- `realm.eldergrove.selection.line`
- `realm.umbral.selection.line`

These drafts are source text, not final localization tables. Engineering may display the keys or mapped text only through the approved UI/localization boundary.

## Handoff to Unity Core

Agent 5 / Codex engineering should consume only the stable runtime subset required by the current parser:

- `catalogId`
- `version`
- `selectionPolicy`
- `realmOrder`
- per-realm `id`
- per-realm `legacyRuntimeId`
- per-realm `displayName`
- per-realm `realmGemIds`

The new `narrativeContinuity`, `continuityHooks`, and `localizationDrafts` fields are narrative source for continuity and localization-facing meaning. They must not become hard-coded gameplay authority for rewards, combat stats, account migration, or ECS spawning.

## Non-Goals

- No realm transfer feature is authored.
- No balance values, combat stats, resources, buildings, or save fields are added.
- No runtime, scene, Android, or Unity service code is changed by this source packet.
- No final user creative approval is claimed for release copy.

## Acceptance Status

Source status: ready for Codex coordination/review and Codex Unity Core consumption as a source packet.
User gate: final release copy and irreversible profile UX still require user approval during integrated playtest.
