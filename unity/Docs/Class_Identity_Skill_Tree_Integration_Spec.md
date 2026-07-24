# Class Identity and General Skill-Tree Integration Specification

- Status: Codex coordination/review specification complete; engineering activation blocked
- Date: 2026-07-24
- Primary Codex mode: coordination/review
- Current project phase: Phase 1 — NVS-01
- Integration branch: `main`
- Specification branch: `codex/coordination-class-skill-tree-integration`
- Specification base: narrative source head `2529a170426f1e0cc8c145233e2daf1ca0ac5f6d`
- Upstream user decision: Codex is to decide and create the narrative/content roster-and-class-identity packet, then produce this coordination specification before engineering.

## 1. Controlled source

This specification consumes, but does not replace, the following narrative/content source:

- packet ID: `ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES`;
- packet version: `anotherlife-class-identity-skill-trees-2026-07-24-v001`;
- packet path: `unity/Docs/Narrative/Classes/ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES.packet.json`;
- packet completion report: `unity/Docs/Narrative/Classes/ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES_Completion_Report.md`;
- packet commit: `11bac67f4c4d0be042fd0659692ae5fc9ca16b80`;
- narrative validation-evidence head: `2529a170426f1e0cc8c145233e2daf1ca0ac5f6d`;
- packet SHA-256 at that commit: `065fddf51ab28c4c104f263b7d9f7dc11bd53daaa00aef2b4453cba63d759a75`;
- four hashed family components under `unity/Docs/Narrative/Classes/Families/`.

The packet is a Codex-authored canonical candidate. The user retains final creative and balance approval. Until that approval is recorded, no engineering branch begins; the packet's existing validator is narrative-source validation, not a shadow runtime schema. Class selection, progression, save migration, Warmaster eligibility, and player-facing skill trees remain inactive.

If a later accepted narrative packet changes an ID, owner, class identity, branch identity, role, milestone, mastery trial, Warmaster title, set, relic, or True Warmaster skill, the packet version and hashes must change. Engineering must consume one complete packet version atomically; it must never combine records from different versions.

## 2. Task declaration

### Goal

Define the authoritative boundary, data contracts, state ownership, migration policy, transaction behavior, accessibility requirements, optimization limits, validation, dependency order, and file-lock plan required to turn the accepted 16-class source into a safe general skill-tree system.

### Non-goals

This specification does not:

- change Unity, Android, runtime assets, scenes, services, saves, catalogs, controls, or UI;
- approve the source packet on the user's behalf;
- author the complete production node roster beneath the three identity branches;
- set damage, healing, cooldown, cost, range, duration, proc, point-cadence, Warzone threshold, item-stat, drop-rate, or price values;
- activate the discarded level-100 soul-quest objects;
- activate, reinterpret, or silently migrate `prototype_true_warmaster`;
- make Forge appearance presets into class definitions;
- close, supersede, or implement GitHub issues #137, #163, #165, #171, #180, #183, or #184;
- claim NVS-01, Warmaster, Champion combat, save hardening, or release completion.

### File scope

This coordination branch adds only this specification. It takes no shared-file lock.

### Acceptance criteria for this specification

The specification is accepted as coordination evidence when it:

1. references one immutable source packet and commit;
2. preserves all 16 class identities and their exact family ownership;
3. separates class identity, appearance, skill allocation, combat loadout, mastery trial, and Warmaster state;
4. defines fail-closed catalog, save, migration, transaction, event, rollback, accessibility, and optimization contracts;
5. identifies every known activation dependency and shared-file lock;
6. prevents prototype data from becoming authority through fallback or inference;
7. provides a testable delivery order without implementing engineering.

## 3. Canonical roster contract

The class roster is exactly:

| Family | Classes |
|---|---|
| Warrior | Vanguard, Guardian, Berserker, Paladin |
| Mage | Pyromancer, Cryomancer, Archmage, Necromancer |
| Ranger | Sharpshooter, Stalker, Beastmaster, Druid |
| Assassin | Shadowblade, Infiltrator, Nightstalker, Slayer |

All 16 classes are available in Stonehold, Eldergrove, Crownlands, and Umbral. Realm affects cultural presentation—materials, language, heraldry, animation flavor, and VFX palette—but not class availability, class mechanics, role eligibility, skill-tree topology, or Warmaster requirements.

A character selects one class directly. The family is derived from the packet mapping and is never an independent player choice or separately persisted truth. Class is character-scoped: two characters or future subcharacters in the same realm may select different classes.

Realm allegiance remains profile-wide under #173. A character derives realm from its owning profile and cannot select, persist, or mutate an independent realm. “Same-realm subcharacters” means every character in one profile shares that one durable realm while retaining an independent class.

The authoritative support decision is:

- Druid is the primary healer;
- Paladin is the secondary healer;
- Necromancer is not a healer unless a later accepted packet explicitly changes its authored kit.

Class change is not a skill respec. Any future class-change feature requires a separate user-approved product, migration, inventory, equipment, loadout, quest, and irreversible-profile policy.

Whether the initial choice is permanently locked is itself a user-owned irreversible-profile decision. This specification does not authorize a permanent lock or a class-change path. Before activation, the selection UI and commit contract must reflect one explicitly recorded decision and disclose its consequences before the durable write.

## 4. General tree contract

Every class has:

- one unique class resource or mechanic identity;
- exactly three non-exclusive identity branches;
- a visible general tree from level 1;
- one named milestone skill at levels 10, 20, 30, 40, and 50;
- a level-50 capstone that is an ordinary class skill;
- one optional, recoverable class mastery trial after level 50;
- one class-specific True Warmaster skill outside ordinary level progression.

The packet's three branches and five milestones are sufficient authority for a general identity tree and first implementation spine. They are not permission for engineering to invent the remaining production nodes or numeric balance.

Before a complete production tree is activated, a separately accepted content-and-balance source must define every additional node, rank, prerequisite, node type, point cost, unlock rule, behavior reference, presentation reference, and numeric profile. That source must preserve the packet's class, branch, milestone, capstone, and Warmaster identities.

### 4.1 Tree topology

The production representation must be a directed acyclic graph.

Each node must include:

- stable node ID;
- owning class ID;
- owning branch ID or explicit class-core ownership;
- node type;
- localized name and description keys;
- behavior definition ID;
- presentation definition ID;
- prerequisite node IDs;
- minimum level;
- rank count;
- point cost per rank;
- exclusion group only when explicitly approved;
- tags for role, contribution, targeting, AI, accessibility, and validation;
- source provenance and source version.

Allowed node types must be explicit, not inferred from description text. The initial set should distinguish:

- active skill;
- passive modifier;
- active-skill modifier;
- class-resource modifier;
- utility or traversal;
- milestone;
- capstone.

Milestones at levels 10, 20, 30, 40, and 50 are identity anchors, not evidence that a character automatically owns every earlier branch node. The accepted balance source must state whether a milestone is granted, purchased, or unlocked for purchase.

The graph must reject:

- cycles, including indirect cycles;
- missing prerequisites;
- cross-class prerequisites;
- cross-branch references that are not explicitly allowed;
- duplicate IDs;
- inaccessible nodes;
- rank or cost overflow;
- milestone-level drift;
- a fourth branch;
- a capstone that is gated by a mastery trial or Warmaster state.

### 4.2 Skill points

The authoritative state must distinguish:

- lifetime entitlement earned;
- derived currently spendable points for the pinned catalog revision;
- committed allocations by node and rank;
- refunded points;
- migration adjustments;
- the source operation IDs that made entitlement-changing actions duplicate-safe.

The accounting invariant is:

```text
available points
= entitlement recognized for the pinned allocation revision
+ committed migration/refund adjustments
- cost of valid committed ranks evaluated under that revision
```

Spent and available points are derived values, not independently mutable counters. A cached value must carry its catalog revision and be rejected or recomputed when the revision does not match.

Allocations are pinned to the catalog revision that defined their nodes, ranks, and costs. A new catalog cannot reinterpret an old allocation in place. Cost increases, cost decreases, removed nodes, reduced ranks, changed prerequisites, and branch restructuring require one explicit, versioned migration policy that states whether the prior allocation remains temporarily supported, is normalized, or is refunded. Migration must stage the new allocation and exact adjustment together, persist once, and be duplicate-safe. If no accepted migration exists, the old allocation remains preserved but inactive and the new catalog cannot activate for that character.

Point entitlement must come from explicit, bounded sources such as level milestones or approved rewards. The complete cadence and totals are balance decisions and must not be invented in runtime code.

Spend and refund operations must:

1. validate the character, class, catalog version, node, rank, prerequisites, level, and available entitlement;
2. stage one complete resulting allocation;
3. persist once through the approved save transaction boundary;
4. publish an event only after durable commit;
5. return the committed result on duplicate retry;
6. leave the prior allocation intact on validation or persistence failure.

No API may accept a client-supplied resulting balance as authority.

### 4.3 Respec

Respec changes skill allocation only. It cannot change:

- selected class or derived family;
- character level or point entitlement;
- mastery-trial state;
- Warmaster ownership or eligibility;
- equipment ownership;
- appearance customization.

A respec may target one node, one branch, or the full class tree only if the accepted balance/product policy permits that scope. Its price, cooldown, availability, and refund rule remain user-owned balance decisions.

After a successful respec, any equipped active skill that is no longer unlocked must be removed from its slot in the same durable transaction. A slot must never keep an unusable or foreign-class skill through fallback.

### 4.4 Class-resource runtime contract

The packet authors the identity, gain intent, and spend intent of each class resource. It does not define numeric runtime behavior. Before any class skill becomes playable, the accepted behavior/balance source must define for every resource:

- authoritative cap, initial value, and whether the value is transient or persisted;
- every allowed gain and spend source;
- regeneration, decay, upkeep, conversion, and recovery rules;
- initialization, respawn, death, encounter exit, scene transition, disconnect, and reload behavior;
- overflow, underflow, precision, and rounding policy;
- UI visibility, opponent readability where relevant, and AI query behavior;
- server/host validation and reconciliation for networked play;
- unavailable behavior when the class, resource, skill, or catalog authority is invalid.

Resource state is owned by the character and selected class. It cannot be inferred from a slot, VFX, animation, or displayed number. A spend validates sufficient authoritative value and commits the spend with the accepted skill action; concurrent or replayed spends cannot both consume the same balance. Underflow fails without mutation. Overflow follows the accepted bounded policy rather than wrapping. Values must remain finite and within schema bounds.

Reordering, unequipping, or re-equipping a skill cannot refill, duplicate, discard, or convert class resource. Class-resource reset behavior is a user-owned balance decision and an activation blocker until explicitly recorded.

### 4.5 Contribution and Warzone-point accounting

Damage is not the only valid contribution. Healing, shielding, cleansing, control, buffs, companion actions, mitigation, and objective presence need auditable attribution so Druid, Paladin, and other support/control identities can earn progression fairly.

An authoritative contribution record needs:

- duplicate-safe event or operation ID;
- contributing character and owning profile;
- selected class and source skill/effect/companion;
- valid target or objective;
- contribution category;
- effective value rather than merely requested value;
- objective/encounter identity and eligibility window;
- timestamp or authoritative sequence;
- catalog and rules revision;
- resulting committed contribution and Warzone-point decision.

Attribution rules must address:

- effective healing rather than overheal;
- damage actually absorbed by shields;
- cleansing a valid hostile effect;
- control that affects an eligible opponent;
- companion and summon contribution credited to the owning Beastmaster, Druid, Necromancer, or other controller;
- party buffs and mitigation without unlimited passive farming;
- self-target loops, allied damage/heal trading, repeated low-value events, idle proximity, and duplicate delivery;
- minimum objective proximity, participation time, and encounter validity;
- disconnect, death, party change, and late-join boundaries.

Contribution and Warzone points require server/host authority in networked RvR. The system must not award or deny progression from client-reported totals, class-name allowlists, raw combat-log volume, or display roles. Rules must be class-neutral but capable of crediting each packet-authored contribution type.

## 5. Combat loadout contract

The initial combat surface has exactly four equipped active-skill slots, indexed `0..3`.

Rules:

- only unlocked active skills owned by the selected class may be equipped;
- a skill may occupy at most one slot;
- passive nodes never consume an active slot;
- changing a slot does not change skill behavior;
- behavior, costs, targeting, cooldown, effects, AI use, and VFX are resolved by stable skill ID;
- cooldown state follows the skill instance or documented skill cooldown key, never the slot number;
- unequip, reorder, respec, death, scene transition, and reload behavior must be explicit and tested;
- a True Warmaster skill uses one of the same four slots unless a later approved control specification creates a separate input surface.

Loadout mutation is prohibited during an active combat/encounter state unless a later accepted combat policy defines a server-authorized swap window and its cost. In all cases, cooldown, charges, ongoing effects, and class-resource state are keyed independently of slot placement. Unequip/re-equip, reorder, death, scene transition, reconnect, and reload cannot reset a cooldown or replenish a resource merely because the binding changed.

True Warmaster cooldown/resource persistence needs an explicit accepted duration and recovery policy. Until it exists, the conservative behavior is to retain remaining authoritative state and reject active-combat swaps. Swap/reset spam, disconnect/reconnect, and save/reload bypass attempts are activation-blocking tests.

The current slot-index behavior in `SkillCaster` is prototype evidence only. An implementation may use a compatibility adapter while migrating, but the adapter must resolve the equipped skill ID before behavior. Missing or invalid definitions must make the affected skill unavailable; they must not fall back to a different hard-coded slot behavior.

The four current prototype skills—

- `realm_strike`;
- `renewing_guard`;
- `warzone_burst`;
- `warmaster_breaker`;

—remain prototype-only. They may be retained as isolated test fixtures or explicitly mapped by a later accepted source. They cannot be silently distributed among classes, treated as universal starter skills, or used to satisfy the packet's milestone or True Warmaster identities.

## 6. Identity, appearance, and equipment separation

Class identity, appearance, and equipment are related but independent authorities.

### 6.1 Forge presets

The nine current Forge presets are freely editable appearance presets. A name or silhouette match does not select, authorize, infer, or migrate a class.

In particular:

- the Forge preset `vanguard` does not select `class_vanguard`;
- `warden` does not select Guardian or Druid;
- `dreadknight` does not select Necromancer, Nightstalker, or Paladin;
- `oracle` does not select Druid or Archmage;
- equipment and color choices do not infer family or class.

UI copy must stop using “identity locked” where it could imply a class decision. Issue #184's appearance-only naming boundary must remain intact.

### 6.2 Equipment

The packet's equipment fields describe class identity and readable silhouette. They are not a numeric equipment catalog.

An accepted equipment-access source must later define:

- allowed armor classes;
- main-hand and off-hand categories;
- dual-wield and two-hand rules;
- shield, focus, banner, relic, companion, and form-specific rules;
- conflict behavior for invalid legacy equipment;
- visual fallback without gameplay fallback;
- inventory and loadout migration.

Invalid equipment after class selection or migration must be preserved in ownership where possible, marked incompatible, and unequipped through an explicit result. It must not be deleted, sold, converted, or silently reclassified.

## 7. Stable IDs, aliases, and localization

Runtime and save references use lower-snake stable IDs from accepted data. Display text is never identity.

### 7.1 Required namespaces

The narrative packet establishes these ownership patterns:

- class: `class_<class-token>`;
- class resource: `class_resource_<class-token>_<resource-token>`;
- branch: `skill_branch_<class-token>_<branch-token>`;
- skill: `skill_<class-token>_<skill-token>`;
- mastery trial: `class_trial_<class-token>_<trial-token>`;
- Warmaster title: `warmaster_title_<class-token>_<title-token>`;
- Warmaster set: `warmaster_set_<class-token>_<set-token>`;
- Warmaster relic: `warmaster_relic_<class-token>_<relic-token>`;
- True Warmaster skill: `skill_<class-token>_true_warmaster_<skill-token>`.

Warmaster piece IDs must be declared in an accepted item source. The default proposed shape is:

```text
warmaster_piece_<class-token>_<piece-slot>
```

That shape is not permission to generate player-owned items before the 160 records, localized names, equipment definitions, acquisition policy, and catalog hashes exist.

### 7.2 Technical aliases

The existing `ClassFamily` and `SubclassId` enum names and values are compatibility mappings only. New data and saves must use stable IDs.

The preserved `SQ_<Subclass>` strings are aliases for the new `class_trial_*` records. They do not reactivate discarded level-100 quest objects, and they do not make the trial a gate.

Aliases must be:

- explicit;
- one-directional toward one canonical ID;
- versioned;
- collision-free;
- validated independently of display names;
- excluded from new writes after migration.

### 7.3 Visual labels

Legacy visual labels such as Barbarian, Marksman, Shadow Assassin, Nightmare, Swordsman, Enchanter, Warlock, Hunter, Healer, and Forest Ranger are search, art, or presentation references only according to the packet's dispositions.

`Cursor` is rejected or superseded. It must not be silently corrected to “Curser.” Curse and debuff motifs are already distributed among Necromancer, Infiltrator, and Nightstalker.

### 7.4 Localization

Localization keys are authoritative references; English text is source copy, not identity. A production catalog must:

- preserve the packet's 244 owned name keys;
- reject duplicate or foreign-owner keys;
- provide fallback text without changing IDs;
- keep class, branch, skill, trial, Warmaster title, set, relic, and skill names separately addressable;
- support screen readers, pluralization, grammatical gender where applicable, and layout expansion;
- report missing translations without substituting another class's text.

## 8. Atomic catalog authority

The runtime must consume one immutable, validated catalog snapshot. The minimum conceptual bundle is:

1. class families;
2. playable classes;
3. role and contribution profiles;
4. class resources/mechanics;
5. equipment-access profiles;
6. skill behavior definitions;
7. skill presentation definitions;
8. skill-tree graphs;
9. point-entitlement and respec policy;
10. default and saved loadouts;
11. class mastery trials;
12. Warmaster titles, sets, pieces, relics, eligibility, and True Warmaster skills;
13. localization references;
14. provenance, schema version, packet version, content version, and hashes.

The bundle must:

- load into an immutable snapshot;
- validate all cross-references before publication;
- have deterministic ordering and hashing;
- expose a catalog revision;
- publish atomically only after complete validation;
- keep the last known-good snapshot if refresh fails;
- expose typed unavailable or invalid state when no known-good snapshot exists;
- never mix current records with hard-coded or partial fallback rows;
- never expose mutable source arrays or dictionaries;
- never let UI, AI, save migration, or combat maintain independent class mappings.

All externally sourced strings and collections need schema-defined length and count bounds. IDs are data keys, never filesystem paths, type names, executable commands, or localization markup. Duplicate-key handling, Unicode normalization policy, non-finite numbers, numeric overflow, and excessive graph/catalog size must be validated before publication.

The packet is source input, not the recommended runtime file format. Engineering may compile it into optimized platform data only when the compiled artifact preserves provenance, IDs, content version, source hash, deterministic output, and validator parity.

### 8.1 Query results

Pure query APIs must distinguish at least:

- `Found`;
- `UnknownId`;
- `CatalogUnavailable`;
- `CatalogInvalid`;
- `UnsupportedVersion`;
- `OptionalAbsent`.

Returning `null`, an empty record, a different class, a first list item, or a hard-coded prototype is not an acceptable substitute for these states.

### 8.2 Command results

Mutation APIs must use typed results that can distinguish:

- committed success;
- duplicate replay of an earlier committed operation;
- invalid character;
- class not selected;
- wrong class;
- unknown or locked node;
- unmet prerequisite;
- insufficient level;
- insufficient points;
- invalid loadout;
- incompatible equipment;
- Warmaster requirement missing;
- unsupported catalog version;
- save unavailable;
- persistence failure;
- conflict or stale revision.

## 9. Character profile and save ownership

The current `SaveGameData` contains realm, customization, generic Warmaster state, equipment, and Warzone credits, but no character roster, character ID, selected class, character level, class skill points, allocation graph, or four-skill loadout.

Adding one top-level class field to the current save would incorrectly prevent same-realm subcharacters from having independent classes. Before class activation, the profile contract must establish a character-scoped container.

### 9.1 Conceptual character state

Each character record needs:

- durable character ID;
- durable owning profile/account reference;
- selected class ID or explicit `class_selection_required`;
- character level and experience authority;
- skill-point entitlement and allocation;
- four-slot active loadout;
- character equipment loadout;
- appearance/customization reference or embedded character-scoped state;
- mastery-trial state;
- character-specific Warmaster state;
- catalog/source version used for the latest successful normalization;
- bounded duplicate-safe operation evidence.

Realm is read from the owning profile's one durable realm authority; it is not duplicated as mutable character truth. If a transport, cache, or legacy record carries a denormalized realm stamp, the stamp is non-authoritative and a mismatch must be rejected or explicitly migrated rather than changing profile allegiance.

Profile realm, character class, appearance, equipment, tree allocation, and loadout must remain separately validated.

### 9.2 Old-save migration

An old save with no class must migrate to `class_selection_required`. It must not infer class from:

- `SubclassId` defaults;
- Forge preset ID;
- armor, weapon, offhand, colors, body, hair, or face marks;
- current skill slot;
- the generic Warmaster set;
- legacy soul-quest state;
- realm.

Migration must preserve existing realm, resources, customization, equipment ownership, quests, and unrelated progression. The class-selection flow must be resumable and cannot mutate unrelated state until the choice is durably committed.

Unknown future IDs should be preserved as inactive/read-only evidence where safe. They must not be dropped, rewritten to a default, or activated under an older catalog.

### 9.3 Save compatibility

New fields require backward-compatible defaults and explicit schema migration under #137. The save layer must test:

- absent, null, duplicate, unknown, unsupported, corrupt, and future-version class records;
- partial write, interrupted replace, quarantined primary, backup recovery, and stale operation replay;
- class/tree/loadout consistency after catalog changes;
- preservation of unrelated save domains;
- bounded file size and migration evidence;
- deterministic serialization where contractually required.

No class or Warmaster activation may bypass the shared-file lock on `SaveGameData.cs`.

## 10. Mastery-trial contract

The 16 legacy celestial quest concepts are preserved as optional level-50 class mastery trials with new stable IDs and explicit aliases.

Every mastery trial must remain:

- optional;
- recoverable;
- nonterminal on failure;
- pausable and abandonable;
- resumable from the latest safe objective;
- duplicate-safe for one-time recognition;
- separate from the level-50 capstone;
- unable to grant or gate class selection, level progress, skill points, Warmaster, True Warmaster, gem custody, final-wish access, the critical path, or the canonical ending.

Allowed rewards are narrative recognition, title presentation, cosmetic remembrance, or optional lore only until a separately accepted reward packet exists.

Runtime implementation must use the approved quest authority when it exists. The current procedural `AddSoulQuest` rows are historical evidence and migration input; they are not sufficient quest definitions.

## 11. Warmaster contract

There are exactly 16 class-specific Warmaster identities. Each has:

- one title;
- one set;
- ten required piece slots;
- one unique class relic occupying the `class_relic` requirement;
- one class-specific True Warmaster RvR skill;
- authored identity and opponent counterplay.

The ten required slots are:

1. weapon;
2. helm;
3. chest;
4. gloves;
5. boots;
6. cape;
7. ring;
8. amulet;
9. mount armor;
10. class relic.

### 11.1 Eligibility

True Warmaster eligibility requires all of:

```text
selected class
+ level 50
+ approved realm contract
+ sufficient committed Warzone points
+ all ten unique valid pieces of that class's Warmaster set
+ a supported, valid catalog version
```

The approved realm contract is required even though its final runtime representation remains to be specified.

“Warzone points” must not be conflated with spendable `WarzoneCredits`. Engineering must establish an authoritative lifetime or committed progression ledger and a distinct spendable currency contract under #163/#171. A caller-provided price or caller-provided point balance is never authoritative.

Piece counting must validate ten different required slots for the selected class and active catalog. Counting ten arbitrary strings, duplicates, foreign-class pieces, unknown pieces, or legacy prototype pieces is invalid.

### 11.2 Acquisition and equipment

Warmaster piece acquisition or purchase must:

1. accept a unique operation ID;
2. resolve the authoritative item and price/acquisition rule from the active catalog;
3. validate class, item prerequisites, currency/ledger, ownership, and capacity;
4. stage currency and item-state changes together;
5. persist once;
6. return the original committed result on retry;
7. publish post-commit events once;
8. leave both balances and ownership unchanged on failure.

The approved realm contract is mandatory for True Warmaster conferral and skill authorization. It is not automatically a prerequisite for ordinary piece acquisition: CH12 may require the character to gather gear and points before that conferral. A piece may require the realm contract only when a separately accepted acquisition rule says so explicitly, preventing a circular gear-before-contract/contract-before-gear gate.

Equipping a set or piece must validate selected class, item compatibility, ownership, active catalog, and character state. It cannot auto-equip a full set merely because the tenth piece arrived unless an accepted product decision explicitly requires that behavior.

### 11.3 True Warmaster skill

A True Warmaster skill:

- is class-specific;
- occupies a normal active slot under the current control decision;
- resolves behavior by skill ID;
- is unavailable outside valid True Warmaster state;
- cannot be granted by a mastery trial;
- requires a visible telegraph, bounded area or duration, an explicit opponent response, non-color readability, scalable VFX, and no instant kill or unconditional invulnerability;
- needs separate large-scale RvR balance, AI, accessibility, network/authority, performance, and exploit review before activation.

The current local save and `LocalWarmasterService` cannot be the final authority for networked RvR. A multiplayer implementation must commit points, acquisition, eligibility, and skill authorization through the approved server/host authority and duplicate-safe operation receipts. An offline prototype cannot be presented as release-complete Warmaster authority.

### 11.4 Prototype migration

Current values such as `prototype_true_warmaster`, arbitrary purchased piece strings, generic piece counts, `IsTrueWarmaster`, and the generic `warmaster_breaker` skill are not proof of a valid class-specific state.

Migration policy:

- preserve the legacy values as auditable migration evidence;
- exclude them from new eligibility;
- do not auto-map them to one of the 16 class sets;
- do not auto-unlock a class, set, relic, or skill;
- do not delete or compensate them until the user approves an explicit migration/compensation decision;
- show a recoverable pending-migration state when relevant;
- make retry and reload duplicate-safe.

## 12. Events and observation

Post-commit events should include:

- operation ID;
- character ID;
- class ID;
- catalog revision;
- save revision;
- prior and resulting state summaries where safe;
- event type and timestamp/sequence.

At minimum, consumers may need:

- class selected;
- skill point allocated;
- skill point refunded;
- loadout changed;
- mastery trial state changed;
- Warmaster piece acquired;
- Warmaster eligibility changed;
- True Warmaster eligibility activated or suspended because authority changed.

Events publish only after durable state commit. Subscriber failure must not roll back committed state or cause the transaction to repeat. Re-observation must not duplicate rewards, points, pieces, or unlocks.

## 13. Failure, rollback, and recovery

The system fails closed by domain.

| Failure | Required behavior |
|---|---|
| Source packet invalid | Do not compile or publish a new catalog |
| Compiled catalog invalid | Keep last known-good snapshot or expose unavailable state |
| Unknown class or node | Preserve evidence, disable affected action, report exact ID |
| Missing optional localization | Use owned fallback text; do not change identity |
| Missing behavior definition | Skill unavailable; no slot-index fallback |
| Invalid saved allocation | Preserve raw evidence, normalize only through explicit migration result |
| Invalid equipped skill | Unequip through a durable normalization transaction |
| Save write failure | Retain prior state; publish no success event |
| Duplicate operation | Return original committed result |
| Future catalog/save version | Preserve inactive/read-only state; do not reinterpret |
| Warmaster authority unavailable | Disable purchase/equip/unlock; retain ownership evidence |
| Prototype state found | Mark pending migration; do not grant class-specific progress |

Rollback of a catalog release must restore the previous complete snapshot. It must not rewrite player state to fit the older version unless a separately tested downgrade migration exists.

## 14. Accessibility and presentation

Class selection and tree UI must support:

- touch, controller, keyboard, and assistive navigation;
- deterministic focus order and focus restoration;
- screen-reader names, roles, state, requirements, and error reasons;
- text scaling and localization expansion without clipped node names;
- non-color cues for family, branch, state, prerequisite, damage type, support type, and Warmaster telegraphs;
- visible locked reasons, not only disabled controls;
- reduced motion, reduced flash, and scalable VFX;
- readable comparison between current and resulting allocation;
- confirmation for destructive respec without trapping the user;
- cancellation and recovery from interrupted class selection;
- equivalent information in compact/list view when a graph layout is difficult to perceive.

The UI must distinguish:

- class name from Forge preset name;
- unlocked from allocated;
- allocated from equipped;
- locked by level from locked by prerequisite;
- ordinary capstone from mastery trial;
- Warmaster set ownership from True Warmaster eligibility;
- unavailable content from empty content.

## 15. Optimization and device compatibility

Expected direction:

- compile source into compact immutable records;
- intern or index stable IDs;
- precompute class, branch, prerequisite, reverse-prerequisite, loadout, and Warmaster indices;
- validate DAGs and references outside per-frame gameplay;
- virtualize or cull large tree UI;
- avoid per-frame JSON parsing, LINQ allocation, reflection, and string-built lookups;
- lazy-load heavy icons, VFX, and presentation assets;
- pool bounded summons, projectiles, telegraphs, and repeated VFX;
- provide quality tiers for True Warmaster and mass-RvR effects;
- deduplicate shared icons, materials, animations, and localized text;
- keep transaction journals bounded and compacted without losing required duplicate-safety evidence.

Before activation, engineering must measure and report:

- catalog parse/compile time;
- startup and scene-transition impact;
- resident and peak memory;
- tree-screen CPU, GPU, draw-call, and allocation cost;
- combat-frame cost with multiple class and True Warmaster effects;
- save size and migration time;
- build/install-size delta;
- lowest supported device behavior and quality-tier fallback.

This specification introduces no runtime, memory, performance, build-size, install-size, or device-compatibility change.

## 16. Validation matrix

### 16.1 Source and catalog

- exact packet ID/version/hash accepted;
- wrong hash or mixed component version rejected;
- all four families and all 16 classes present once;
- exact family ownership and all-realm availability preserved;
- Druid primary-healer and Paladin secondary-healer policy preserved;
- three branches and levels 10/20/30/40/50 preserved per class;
- stable ID, owner prefix, localization owner, and alias collisions rejected;
- unexpected narrative gameplay numbers rejected;
- missing behavior/presentation/equipment references reject publication;
- DAG cycle, missing node, inaccessible node, and cross-class edge rejected;
- deterministic output and hash reproduced on all supported build hosts;
- partial catalog never replaces the active snapshot.

### 16.2 Character and save

- old save becomes `class_selection_required` without inference;
- two same-realm characters can choose different classes;
- every character derives the profile-wide realm and a mismatched character realm stamp is rejected without changing allegiance;
- selection retry returns the original committed result;
- interrupted selection preserves prior state;
- null, absent, corrupt, duplicate, unknown, and future fields handled explicitly;
- unrelated save domains survive migration byte-for-byte where contract permits;
- invalid allocation/loadout normalization is durable and duplicate-safe;
- forward IDs remain preserved but inactive;
- primary/backup/quarantine recovery retains class state or reports exact loss boundary;
- save growth remains bounded.

### 16.3 Tree and loadout

- point spend validates entitlement, level, prerequisites, class, and revision;
- simultaneous or repeated spend cannot overspend;
- cost increase, cost decrease, node removal, rank reduction, and unsupported catalog revision cannot create or destroy points without one committed migration adjustment;
- refund/respec cannot duplicate points;
- respec removes newly invalid equipped skills atomically;
- four slots accept only unique unlocked active skills;
- reordering a skill does not change behavior;
- cooldown follows the skill contract, not its slot;
- unequip/re-equip, reorder, death, scene transition, disconnect/reconnect, and reload cannot bypass cooldown, charge, or resource policy;
- active-combat swaps fail unless an accepted server-authorized swap policy exists;
- class-resource initialization, gain, spend, overflow, underflow, reset, death, transition, reload, concurrency, and unavailable state follow the accepted resource definition;
- missing skill definition becomes unavailable instead of another skill;
- passive nodes cannot be equipped;
- True Warmaster skill occupies a normal slot;
- AI and player resolve the same behavior authority.

### 16.4 Contribution and Warzone points

- effective healing counts while overheal and self-farming do not;
- shielding counts actual eligible absorption rather than applied shield size;
- cleanse and control require a valid hostile effect or affected opponent;
- companion/summon actions attribute to the owning character exactly once;
- support buffs, mitigation, and objective presence use bounded eligibility windows;
- idle proximity, allied trading, raw event volume, late duplicates, and replay after reload cannot farm contribution;
- death, disconnect, party change, and late join have explicit boundaries;
- Druid, Paladin, damage, control, tank, and companion identities can satisfy class-neutral contribution rules;
- client-reported totals cannot grant committed Warzone points.

### 16.5 Mastery trials

- aliases resolve to the correct new trial;
- trial absence does not block capstone or Warmaster;
- failure, abandonment, reload, and retry remain recoverable;
- one-time recognition cannot duplicate;
- discarded level-100 objects do not reactivate.

### 16.6 Warmaster

- exact selected-class set and ten unique slots required;
- foreign, duplicate, unknown, or prototype pieces do not count;
- level 50, approved realm contract, and committed points all required;
- ordinary gear acquisition can precede realm-contract conferral unless an accepted item rule explicitly says otherwise;
- lifetime points and spendable credits cannot substitute for each other;
- caller-supplied price rejected;
- currency and item ownership commit atomically;
- retry returns original acquisition result;
- generic `IsTrueWarmaster` cannot authorize a class-specific skill;
- prototype state remains pending migration;
- loss of valid catalog authority fails closed;
- True Warmaster cooldown/resource state resists swap, death, transition, reconnect, and reload reset exploits;
- every skill has visible, bounded, non-color counterplay;
- summon/VFX budgets hold in mass-RvR stress tests.

### 16.7 Accessibility and performance

- full class and tree flows complete with touch, controller, and keyboard;
- screen reader exposes names, roles, locks, prerequisites, ranks, and results;
- 200% text scaling remains usable;
- high-contrast and non-color state cues remain distinguishable;
- reduced motion and reduced flash affect every class/Warmaster presentation;
- compact/list alternative preserves graph information;
- no steady-state per-frame catalog allocations;
- virtualized UI and pooled effects meet accepted low-end budgets;
- build/install-size and memory deltas are measured, not inferred.

## 17. Delivery and dependency order

Engineering work must remain split into focused branches and PRs. Recommended order:

1. **User source acceptance** — record acceptance or requested narrative changes for packet `v001`.
2. **Narrative publication** — publish the focused narrative branch and merge it only after its source/fidelity review and user acceptance.
3. **Coordination acceptance** — update this branch onto the resulting current `main`, review this specification, and open one focused tracking issue if no existing issue owns the integrated class/tree contract.
4. **Pure schema and validator slice** — add immutable class/tree/loadout/Warmaster schema and offline validators without runtime activation or shared-file edits.
5. **Complete content and balance source** — author and obtain user approval for all non-milestone nodes, behavior definitions, numeric profiles, point cadence, respec, equipment access, Warmaster points, piece acquisition, item stats, and RvR tuning.
6. **Character/profile contract and save migration** — depend on #137 and hold the `SaveGameData.cs` lock before adding character-scoped fields.
7. **Atomic catalog/query integration** — coordinate with #183 and hold the `LocalGameDataService.cs` lock only if that file is still the approved integration point.
8. **Skill-point, tree-allocation, respec, and loadout services** — implement pure state transitions and duplicate-safe persistence before UI.
9. **Combat adapter** — under #180, make `SkillCaster` or its replacement resolve behavior by skill ID, preserving encounter authority and four-slot controls.
10. **Warmaster implementation** — under #171 only after #137, #163, the approved realm contract, item catalog, and point ledger exist.
11. **UI, accessibility, AI, presentation, and scalable VFX** — consume the same immutable authority; do not create parallel mappings.
12. **Coordination integration disposition** — validate current main, dependencies, locks, migrations, tests, evidence, optimization measurements, and rollback.
13. **Narrative fidelity disposition** — verify all names, roles, branches, capstones, trials, Warmaster identities, and presentation remain faithful.
14. **User playtest and approval** — required before milestone or release acceptance.

Parallel implementation is permitted only where slices do not compete for shared files or define overlapping authority. Runtime activation must wait for the dependency chain even if isolated schemas or validators are ready.

## 18. Issue and lock map

Relevant issue dependencies:

| Issue | Relationship |
|---|---|
| #137 | save transaction, migration, backup, quarantine, and recovery dependency |
| #152 | quest-state compatibility dependency if class mastery trials enter runtime |
| #155 | repository quality-gate integration for new deterministic validators |
| #160 | skill presentation-tier alignment |
| #163 | authoritative economy/currency transaction dependency |
| #165 | adjacent progression invariants; it does not by itself define character-level or skill-point authority |
| #168 | owned-equipment and duplicate-safe acquisition adjacency for Warmaster pieces |
| #171 | Warmaster gear, points, purchase, set, and True Warmaster implementation owner |
| #173 | durable realm-selection dependency for character realm and approved realm-contract validation |
| #177 | typed post-commit notification integration when player-facing events are added |
| #180 | Champion combat behavior and four-skill loadout integration owner |
| #183 | active game-data authority and catalog integration owner |
| #184 | appearance naming and Forge/class separation owner |

No open PR held a shared-file lock when this specification was prepared. Lock ownership must be checked again immediately before each engineering PR.

Shared-file plan:

| Shared file | This specification | Later use |
|---|---|---|
| `unity/Assets/AL/Scripts/Core/Bootloader.cs` | no lock, no edit | separate service-registration slice only if required |
| `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs` | no lock, no edit | character/profile and migration slice after #137 |
| `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` | no lock, no edit | catalog integration only after #183 authority decision |
| `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs` | no lock, no edit | avoid generated authority; use only if an approved asset workflow requires it |

## 19. Current baseline and required replacement

Current source evidence shows:

- `ClassFamily` has the correct four family enum names;
- `SubclassId` has the 16 compatible enum names and values;
- `ClassDefinition` has only family, enum, display name, and icon fields;
- no committed production class definitions establish the packet's class identities;
- `SkillDefinition` is too small to represent ownership, resources, branches, prerequisites, ranks, loadouts, behavior separation, or provenance;
- `SkillCaster` owns four hard-coded skill behaviors and still switches behavior by slot;
- the partial StreamingAssets skill catalog can mix file data with hard-coded values;
- `SaveGameData` has no character-scoped class, level, allocation, or loadout state;
- `WarmasterState` is generic and piece strings are not catalog-validated;
- `LocalWarmasterService` uses caller price, count-based eligibility, and `prototype_true_warmaster`;
- procedural legacy soul-quest rows preserve useful names but do not meet the mastery-trial contract;
- Forge preset labels are appearance data, not class authority.

These are regression and migration inputs. They are not justification to weaken the contracts in this specification.

## 20. Acceptance and activation disposition

### Coordination status

This specification is complete as a pre-engineering contract.

### Narrative status

The 16-class packet is a validated canonical candidate. It awaits the user's final creative acceptance or requested revisions.

### Engineering status

Blocked. Engineering activation requires:

- recorded user acceptance of the narrative packet;
- a complete node and numeric balance source;
- an explicit user decision on initial class-selection permanence and any future class-change boundary;
- authoritative profile-wide realm, character/profile, character-resource, contribution, cooldown/reset, and progression decisions;
- issue/dependency readiness for #137, #152, #155, #163, #165, #168, #171, #173, #177, #180, #183, and #184 as applicable;
- current PR/lock reinspection;
- focused engineering branch declarations and acceptance tests.

### PR and issue status at preparation

- local narrative source commit exists on `codex/narrative-general-class-identity`;
- this dependent coordination branch contains only this specification;
- no PR has been opened or pushed by this task;
- no new issue has been created;
- no shared-file lock is held by this task.

If published, the narrative PR targets `main` first. The coordination branch then updates onto the post-narrative current `main` and opens as its own coordination/review PR; the current local ancestry records the dependency but is not permission to publish a stale stacked PR.

### Next Codex mode

After user acceptance, the next mode is coordination/review to open or align the focused tracking issue and convert the dependency order into engineering slices. Engineering begins only with the first approved pure schema/validator slice or another explicitly accepted slice; it does not begin from this document automatically.
