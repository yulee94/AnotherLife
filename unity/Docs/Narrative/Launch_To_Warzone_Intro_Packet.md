# Launch to Warzone Intro Narrative Packet

**Status date:** 2026-07-16
**Primary Codex mode:** narrative/content
**Packet ID:** `launch_warzone_intro_v001`
**Consumes:** `unity/Docs/Player_Launch_Progression_Contract.md`
**User approval:** pending final product/creative/balance/playtest approval

## Purpose

This packet authors the first narrative spine for a new player from realm selection through the first arrival at the neutral center island. It establishes realm allegiance, champion identity, kingdom responsibility, save-pillar meaning, outer-realm danger, bridge-connected warzones, cross-realm language separation, and the wish dragon's limited neutral-zone protection.

This is narrative/source authority only. It does not implement UI, save data, usernames, combat, party/squad systems, leveling math, scenes, AI, economy, cash items, NPC shops, or multiplayer networking.

## Source Constraints

- Four selectable technical realms: `Stonehold`, `Eldergrove`, `Crownlands`, `Umbral`.
- A new account/profile commits to one realm before entering gameplay.
- The first playable mode after character creation is 3D champion mode inside the selected inner realm.
- The questline must introduce 2.5D kingdom management before returning to 3D champion mode.
- The outer-realm gate sequence must introduce exactly one active warzone savepoint controlled only by save-pillar interaction.
- The introductory outer-realm path ends at a central neutral island where the wish dragon enables cross-realm speech and trade.
- Main quest progression should place ordinary players around level 40 by outer-realm warzone entry.
- Level 40 to 50 is not narrated as a solo sprint. It is framed as prolonged party hunting and realm service.

## Realm Narrative Frames

| Technical realm | Source display name | Realm identity | First advisor | First inner-realm tone |
| --- | --- | --- | --- | --- |
| `Stonehold` | Stonehold | fortress clans, oath stone, siege craft, endurance | Marshal Kael Vorr | disciplined, blunt, protective |
| `Eldergrove` | Eldergrove | living boughs, old covenants, wardens, patient renewal | Warden Ilyra Mossvein | watchful, ritual, communal |
| `Crownlands` | Crownlands | banners, courts, roads, command, public duty | Chancellor Mira Valen | formal, strategic, civic |
| `Umbral` | Umbral | twilight citadels, secrets, shadow vows, forbidden paths | Seer Nyx Aravel | restrained, wary, prophetic |

The names above are source-review labels pending user creative approval. Engineering must treat the technical realm IDs as authority and localization keys as player-facing text authority.

## Global Narrative Terms

| ID | Source name | Meaning |
| --- | --- | --- |
| `npc_wish_dragon_aurion` | Aurion, the Wish Dragon | Ancient neutral dragon whose consideration suppresses violence at the center island and briefly translates realm languages. |
| `lang_concord` | Concord | The temporary shared tongue heard only under Aurion's consideration. |
| `zone_center_island` | Concord Isle | Neutral island at the center of the outer realm. |
| `object_save_pillar` | Soul Pillar | Warzone savepoint pillar that binds one active return point. |
| `objective_eight_gems` | Eightfold Wish | Long-term realm objective to gather all eight realm gems before petitioning Aurion. |

These terms are authored for source continuity. Final names may change through user approval before production localization lock.

## Questline Overview

| Quest ID | Title key | Mode | Primary purpose | Completion gate |
| --- | --- | --- | --- | --- |
| `q_launch_realm_oath` | `loc.quest.launch.realm_oath.title` | account/selection | Commit realm allegiance and champion identity. | Realm, character, and unique username accepted. |
| `q_inner_first_watch` | `loc.quest.launch.first_watch.title` | 3D champion | Teach movement, advisor contact, inner-realm threat, and class identity. | Player completes first champion duty. |
| `q_kingdom_first_charge` | `loc.quest.launch.first_charge.title` | 2.5D kingdom | Give the player authority over a small inner-realm holding. | Player completes first kingdom management loop. |
| `q_inner_gate_muster` | `loc.quest.launch.gate_muster.title` | 3D champion | Return to the champion, prepare for outer gate, introduce party dependence. | Gate captain permits outer-realm departure. |
| `q_warzone_soul_pillar` | `loc.quest.launch.soul_pillar.title` | 3D warzone | Bind the first warzone savepoint and explain one-active-pillar rule. | Player interacts with a Soul Pillar. |
| `q_bridge_of_banners` | `loc.quest.launch.bridge_of_banners.title` | 3D warzone | Reveal bridge routes, crossroads danger, realm camps, and party hunting. | Player scouts first bridge/crossroads marker. |
| `q_concord_isle_arrival` | `loc.quest.launch.concord_isle.title` | 3D neutral | Reach Concord Isle, meet Aurion's rule, unlock neutral services as source intent. | Player enters neutral island and receives Aurion's consideration. |

## Stable State Flow

| State ID | Player-facing meaning | Entry | Exit |
| --- | --- | --- | --- |
| `launch.uncommitted` | The player has not chosen a realm. | Fresh profile/account. | Realm selection accepted. |
| `launch.identity_pending` | Realm chosen; character/username not finalized. | Realm accepted. | Character and unique username accepted. |
| `inner.champion_awakened` | The champion begins inside their realm. | Profile finalized. | First advisor duty accepted. |
| `inner.first_watch_active` | Player learns champion movement/combat through realm-specific duty. | Advisor sends player out. | Duty completed. |
| `kingdom.first_charge_active` | Player manages first holding in 2.5D kingdom mode. | Advisor hands off stewardship. | First kingdom loop completed. |
| `inner.gate_muster_active` | Player returns to champion mode and prepares for outer realm. | Kingdom loop complete. | Gate captain authorizes departure. |
| `warzone.pillar_unbound` | Player has entered warzone but has no bound Soul Pillar. | Main gate exit. | Soul Pillar interaction succeeds. |
| `warzone.pillar_bound` | One active warzone return point is bound. | Soul Pillar interaction. | Bridge/crossroads scout objective complete. |
| `warzone.bridge_scouted` | Player understands bridge routes and realm hunting camps. | Bridge marker scouted. | Concord Isle reached. |
| `neutral.concord_considered` | Aurion suppresses PvP and grants Concord language. | Center island entered. | Intro spine complete. |

## Quest Details

### `q_launch_realm_oath`

Narrative purpose: make realm choice feel like allegiance, not a menu toggle.

Objectives:
- `obj_select_realm`: choose one of the four playable realms.
- `obj_create_champion`: create a realm-specific champion identity.
- `obj_claim_name`: submit a unique username.

Failure/retry:
- Duplicate username returns to character naming without losing editor choices.
- Different-realm change after commitment is rejected by product policy and must be explained as a new-profile/reset matter, not a story loophole.

Localization keys:
- `loc.quest.launch.realm_oath.title`
- `loc.quest.launch.realm_oath.desc`
- `loc.system.username.duplicate`
- `loc.system.realm.locked_same_account`

### `q_inner_first_watch`

Narrative purpose: establish the player's champion as an adult realm defender with immediate duty.

Realm-specific advisor beats:
- Stonehold: Marshal Kael sends the player to inspect a breached watch line.
- Eldergrove: Warden Ilyra sends the player to calm a wounded ward grove.
- Crownlands: Chancellor Mira sends the player to secure a courier road.
- Umbral: Seer Nyx sends the player to silence a forbidden signal.

Objectives:
- `obj_meet_realm_advisor`
- `obj_complete_first_combat_duty`
- `obj_return_to_advisor`

Handoff request:
- Champion combat/tutorial duty must be 3D champion mode.
- Rewards are useful starter gear/accessory intent only, not solo power fantasy proof.

### `q_kingdom_first_charge`

Narrative purpose: show the player that a champion is also a lord responsible for production, training, and realm survival.

Objectives:
- `obj_receive_stewardship`
- `obj_open_kingdom_view`
- `obj_complete_first_build_or_training_order`
- `obj_collect_first_managed_result`

Narrative rule:
- The kingdom is not a childlike toy board. It is a strategic seat of responsibility, with restrained adult UI tone requested for implementation.

Handoff request:
- 2.5D kingdom mode should use dense operational presentation inspired by Lords Mobile/Infinity Kingdom without copying protected designs.

### `q_inner_gate_muster`

Narrative purpose: return to embodied 3D champion mode and make the outer realm feel dangerous, social, and party-oriented.

Objectives:
- `obj_return_to_champion`
- `obj_receive_gate_warning`
- `obj_prepare_for_party_hunting`
- `obj_depart_main_gate`

Advisor warning source intent:
- The main questline can bring the player near level 40, but the final climb requires allies, healers, support roles, and sustained realm service.
- Potions are not framed as a replacement for healers in combat.

### `q_warzone_soul_pillar`

Narrative purpose: teach death-return meaning before the player is exposed to real outer-realm conflict.

Objectives:
- `obj_find_soul_pillar`
- `obj_bind_soul_pillar`
- `obj_confirm_single_return_point`

Source rules:
- A player can have only one active warzone savepoint.
- Savepoint changes require direct Soul Pillar interaction.
- Menus, remote items, passive travel, or UI shortcuts do not change the bound pillar unless a later user-approved feature changes this rule.

### `q_bridge_of_banners`

Narrative purpose: reveal the outer realm as a contested RvR geography.

Objectives:
- `obj_scout_realm_bridge`
- `obj_identify_crossroads`
- `obj_identify_party_hunting_camp`
- `obj_receive_squad_warning`

Source rules:
- Bridge routes connect realm continents.
- Crossroads are intentionally dangerous and campable.
- Party hunting camps are condensed and fast-spawning, but narratively require coordinated groups.
- Squads are for RvR and party PvP; warzone points require contribution, not passive presence.

### `q_concord_isle_arrival`

Narrative purpose: introduce the only neutral outer-realm zone and the wish dragon rule without ending realm conflict.

Objectives:
- `obj_enter_concord_isle`
- `obj_receive_aurion_consideration`
- `obj_identify_neutral_services`
- `obj_hear_eightfold_wish`

Source rules:
- PvP is forcibly suppressed on Concord Isle.
- Realm languages remain separated outside the island.
- Aurion's consideration grants temporary Concord language only inside the neutral zone.
- Cross-realm trade and chat are allowed only under this protection.
- NPC vendors can exist for potions, consumables, event items, cash items, and related services, but exact economy/cash/item design is separate.
- The Eightfold Wish is introduced as a long-term realm objective, not completed in this intro.

## Dialogue Source Samples

These lines are source samples for tone and localization planning, not final voiceover lock.

| Key | Speaker | Source text |
| --- | --- | --- |
| `loc.dialogue.launch.oath.warning` | Realm oath officiant | "A realm is not a banner you wear for a day. Choose with both eyes open." |
| `loc.dialogue.launch.party_warning` | Gate captain | "Past this gate, lone pride dies quickly. A healer, a shield, a scout, someone who knows when to run. Bring people." |
| `loc.dialogue.launch.pillar_rule` | Soul Pillar keeper | "The pillar remembers one return. Bind another and this one releases you." |
| `loc.dialogue.launch.concord_rule` | Aurion | "Under my consideration, blades sleep and tongues meet. Beyond my island, your realms remember their old divisions." |
| `loc.dialogue.launch.eightfold_wish` | Aurion | "Eight gems. One wish. No realm asks the world to bend without first proving what it is willing to lose." |

## Localization Inventory

Required key groups:
- `loc.realm.stonehold.*`
- `loc.realm.eldergrove.*`
- `loc.realm.crownlands.*`
- `loc.realm.umbral.*`
- `loc.quest.launch.*`
- `loc.objective.launch.*`
- `loc.dialogue.launch.*`
- `loc.system.username.*`
- `loc.system.realm.*`
- `loc.system.save_pillar.*`
- `loc.zone.concord_isle.*`
- `loc.npc.aurion.*`
- `loc.language.concord.*`

## External Capability Requests

| Capability | Narrative need | Downstream mode |
| --- | --- | --- |
| Realm commit | One realm per account/profile before gameplay. | coordination/review then engineering |
| Character creator | Realm-specific customization and username flow. | narrative/content labels, engineering UI/runtime |
| Username uniqueness | Duplicate rejection without editor-state loss. | coordination/review then engineering |
| 3D champion mode | Inner realm and warzone embodied play. | engineering |
| 2.5D kingdom mode | Stewardship loop and strategic responsibility. | engineering |
| Quest state persistence | Save/reload across every state above. | coordination/review then engineering |
| Soul Pillar savepoint | Single active warzone return point. | coordination/review then engineering |
| Party/squad systems | Party sharing, squad contribution warzone points. | coordination/review then engineering |
| Neutral zone suppression | Concord Isle PvP disable and cross-realm language. | coordination/review then engineering |
| Realm gems and wish | Eightfold Wish long-term objective. | narrative/content, coordination/review, engineering |

## Narrative Acceptance

- [ ] Four realm choices have stable technical IDs and source display/culture framing.
- [ ] Character creation and username creation are represented before gameplay.
- [ ] Duplicate username failure preserves character creation progress.
- [ ] The first playable state is 3D champion mode inside the committed realm.
- [ ] 2.5D kingdom management is introduced by quest, then hands back to 3D champion mode.
- [ ] Outer gate, Soul Pillar, bridge/crossroads, party hunting, squad contribution, and Concord Isle are all represented.
- [ ] Level ~40 warzone arrival and month-scale 40-50 party hunting are framed without final numeric balance.
- [ ] Healers/support roles and non-combat potion regeneration are represented as player-facing expectations.
- [ ] Aurion, Concord language, neutral PvP suppression, and cross-realm trade/chat rules are internally consistent.
- [ ] No runtime implementation, save schema, UI layout, scene, combat, economy, item, cash shop, or balance authority is claimed by this packet.

