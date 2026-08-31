# Post-MVP Realm Character, Creature, Motion, and VFX Catalog Contract v1

**Status:** Active preparation contract; generation and runtime activation remain gated

**Schema:** `unity/SharedContracts/Schemas/al-realm-character-taxonomy.schema.json`

**Semantic validator:** `unity/SharedContracts/Tests/realm_character_taxonomy.py`

**Owner packet:** `unity/Docs/AssetLibrary/Templates/PostMVP_Realm_Owner_Decision_Packet_Template_v1.md`

**Final creative and release authority:** Project owner

**Applies to:** Stonehold, Eldergrove, Crownlands, and Umbral realm character/creature catalogs

## 1. Purpose and authority boundary

This contract is the shared production shape that every realm catalog uses for playable races, NPCs, Champions, fantasy beasts, monsters, modules, rigs, motion, skill presentation, and VFX. It makes every production dependency explicit before content multiplication.

The catalog is descriptive authority only. It does not:

- create lore, culture, morphology, anatomy, clothing, armor, animation personality, or magical grammar;
- authorize model or effect generation;
- replace gameplay timing, targeting, damage, status, or result authority;
- admit a source or runtime asset;
- waive owner, technical, provenance, performance, accessibility, or release gates.

When a required value is unknown, record `owner_decision_required`, reference an owner decision packet, and leave the numeric or creative value null. Never fill a gap with a plausible number or generic fantasy choice.

Source precedence follows `DESIGN.md`: latest explicit owner decision, approved asset-specific source, global design contract, then prototypes/benchmarks. An approved source may replace a provisional value only with a recorded evidence reference and owner decision.

## 2. Recon coverage

The task-critical source-and-contract corpus contained **28 tracked text files / 11,616 lines**. All **28/28 files and 11,616/11,616 lines (100%)** were read end-to-end before authoring this contract.

The corpus covered:

- `DESIGN.md`;
- catalog authority and authoring templates;
- Champion customization, model-sheet, realm-anchor, and visual-direction sources;
- current skill and skill/weather catalogs plus convergence documentation;
- terrestrial/ecosystem, boss/elite, platform, and optimization sources;
- the three post-MVP world-asset taxonomy/budget/binding standards;
- current shared schemas, shared-contract documentation, and validators.

The generated `al_world_asset_inventory.json` body (20,898 lines) was structurally parsed by its canonical validator and inspected through targeted field searches; it was excluded from the line-read denominator because it is generated data, not character/creature design authority. Repository-wide animation/VFX/platform searches were enumerated but are not claimed as end-to-end reads.

## 3. Catalog identity

### 3.1 Stable IDs

Every canonical record uses:

`rct_<scope>_<kind>_<slug>_v<NNN>`

- `scope`: `shared`, `stonehold`, `eldergrove`, `crownlands`, or `umbral`.
- `kind`: `race`, `npc`, `champion`, `beast`, `monster`, `body`, `equipment`, `rig`, `face`, `physics`, `lod`, `collider`, `hitbox`, `platform`, `budget`, `skill`, `motion`, `vfx`, `motion_matrix`, `provenance`, or `decision`.
- `slug`: lowercase ASCII snake case; descriptive, stable, and free of temporary names.
- `NNN`: zero-padded compatibility revision (`v001`, `v002`, and so on).

Catalog IDs use:

`rct_<realm>_catalog_<slug>_v<NNN>`

Rules:

1. A realm catalog may contain its own scope and `shared` records only.
2. IDs are never repurposed. A meaning-breaking change creates the next revision.
3. Display names may change without changing identity.
4. External gameplay IDs stay in `externalSourceId`; they are not normalized or silently replaced.
5. Arrays are bytewise sorted by canonical ID. IDs are unique across the whole catalog, not only within one section.
6. Aliases and migrations belong in a later runtime-binding contract. They do not weaken canonical identity here.

### 3.2 Required catalog sections

| Section | Required production content |
| --- | --- |
| `provenance` | Source, creator, tool/version, time, rights, prompt/brief, digest, notes |
| `decisionPackets` | Unresolved owner choices with alternatives and downstream impact |
| `platformProfiles` | Mobile-floor, optional higher-mobile/PC, and offline tiers actually used |
| `budgetProfiles` | Explicit geometry/material/texture/bone/physics/animation/VFX/collider/hitbox fields |
| `motionMatrixTemplates` | One complete template each for Champion, NPC, beast, monster, and boss |
| `playableRaces` | Morphology/culture state and module/rig/profile bindings |
| `npcArchetypes` | Role, role actions, skills, motion, and production bindings |
| `championFamilies` | Race/class/weapon/skill and complete production bindings |
| `beastFamilies` | Habitat, locomotion modes, skills, and production bindings |
| `monsterFamilies` | Rank, habitat, locomotion, skills, boss transitions, and bindings |
| `bodyModules` | Slot, compatibility, rig, mesh source, hidden surfaces, budgets |
| `equipmentModules` | Slot, body/entity compatibility, sockets, mesh source, budgets |
| `rigFamilies` | Skeleton/bind pose/root, root-motion policy, bone count, sockets, retarget group |
| `facialSystems` | Blendshape/bone/hybrid mode, expressions, visemes, gaze, rig, budgets |
| `secondaryPhysicsProfiles` | Cloth/hair/secondary solver, affected parts, fallback, collision, budgets |
| `lodProfiles` | Levels, triangle ratios, thresholds, bone/material reduction, protected cues |
| `colliderProfiles` | Simple LOD-independent movement/ragdoll/environment/interaction proxies |
| `hitboxProfiles` | Gameplay-owned hurt/attack/target/interaction shapes and activation authority |
| `platformVariants` | Per-tier mesh/texture/rig/physics/VFX reduction and protected cues |
| `skills` | Exact external source identity and separate timing/result authority |
| `motions` | Subject, key, skill phase, rig, clip/source, root mode, events, timing ownership |
| `vfxEffects` | Category, subjects/skills, source/direction/timing/area/end, variants, budgets |
| `skillTraceability` | One row per skill covering every motion phase and effect category |

Empty roster arrays are schema-valid during initial preparation, but a realm production gate must define its complete approved/proposed roster and pass the semantic validator before generation. A missing category is not equivalent to an owner decision.

## 4. Fact, proposal, and owner-gate states

Every production record carries `authority`:

| `status` | Required `ownerStatus` | Required evidence |
| --- | --- | --- |
| `approved_fact` | `APPROVE` | Provenance plus explicit approval evidence |
| `proposal` | `PENDING` or `REVISE` | Provenance plus owner decision packet naming the record |
| `owner_decision_required` | `PENDING` or `REVISE` | Provenance plus owner decision packet naming the record |
| `rejected` | `REJECT` | Decision packet and recorded reason/evidence |

Catalog-wide gates are ordered and all required:

1. `owner_creative`
2. `technical`
3. `provenance`
4. `motion_effect_coverage`
5. `performance_mobile_floor`
6. `accessibility`
7. `release`

`gateEvidence` stores a state, reviewer, UTC decision time, evidence references, and open issues for every gate. A positive gate requires a nonblank reviewer, a valid RFC 3339 UTC timestamp (`Z` or `+00:00`), at least one evidence reference, and no open issue. Release admission requires these exact positive states: `ownerCreative=approved`, `technical=passed`, `provenance=cleared`, `motionEffectCoverage=passed`, `performanceMobileFloor=passed`, `accessibility=passed`, and `release=admitted`.

`generationState=owner_approved` is invalid while any record, `PENDING`/`REVISE` decision, or provisional/unresolved budget metric remains pending. `activationState=release_approved` is invalid unless generation is owner-approved, every exact positive gate has evidence, and every applicable budget field is an `approved_limit` rather than `documented_provisional` or `owner_decision_required`.

The eight protected owner-decision dimensions are:

- morphology;
- culture;
- silhouette;
- anatomy;
- clothing;
- armor;
- animation personality;
- magical grammar.

Every playable race, NPC, Champion, beast, and monster carries a
`creativeDecisions` object with all eight dimensions. Each dimension is
`approved` with a source and summary, `owner_decision_required` with a packet,
or `not_applicable` with a reason. Missing a dimension is invalid; an empty or
omitted value is not a neutral decision.

Use the packet template for every unresolved choice in these dimensions. A single packet may cover several dimensions only when the alternatives and downstream impact are the same decision.

## 5. Platform and budget contract

### 5.1 Binding mobile-floor context

The current documented physical-floor configuration is the Galaxy A54 5G 6 GB / Exynos 1380 / Mali-G68 candidate, native 2340×1080 landscape at 60 Hz, Vulkan, `mobile_floor`, and a 30 FPS target (`PostMVP_World_Asset_Budgets_And_Readability_v1.md`). Changing that device floor, render pipeline, or material system is a separate owner/technical decision.

A catalog records only the platform tiers it actually supports. `cinematic_offline` never enters a Player build.

### 5.2 Metric representation

Every budget metric records:

- `state`: `documented_provisional`, `approved_limit`, `owner_decision_required`, or `not_applicable`;
- `limitKind`: inclusive maximum, strict less-than, target, inclusive range, owner decision, or not applicable;
- `value` and optional `secondaryValue` for ranges;
- unit;
- source references;
- owner decision packet IDs where unresolved;
- rationale.

`documented_provisional` preserves existing source values but does not promote them to an approved production limit. Only an explicit owner-approved measurement packet may set `approved_limit`.

### 5.3 Required metric groups

| Group | Required fields |
| --- | --- |
| Geometry | LOD0 triangles; LOD1/LOD2/LOD3 reduction percentages |
| Materials | material slots; shader passes |
| Textures | maximum long edge; resident texture memory |
| Bones | deforming bones; influences per vertex |
| Physics | simulated bones; cloth vertices; active rigidbodies |
| Animation | compressed-memory target; compressed-memory maximum; clip count; runtime animator layers |
| VFX | live particles; transparent layers; overdraw coverage; concurrent effects; dynamic lights |
| Colliders | primitive colliders; proxy triangles |
| Hitboxes | active hitboxes |

No group or field may be omitted because a value is unknown.

### 5.4 Existing documented starting points

These values are source constraints, not new approvals:

| Scope | Documented provisional starting point | Source |
| --- | --- | --- |
| Champion / major character | LOD0 <=60k triangles; typical 3 material slots; primary sets <=2K; strictly fewer than 90 deform bones; <=4 influences/vertex | `DESIGN.md` lines 673–705 |
| Major boss (global planning) | LOD0 <=100k; typical 4 material slots; major material sets <=2K; strictly fewer than 120 deform bones; <=4 influences/vertex | `DESIGN.md` lines 673–705 |
| Elite / important NPC | LOD0 <=45k; typical 3 material slots; 1K–2K runtime textures; <=4 influences/vertex; NPC deform-bone ceiling remains an owner decision | `DESIGN.md` lines 673–705 |
| Ambient terrestrial / common unit | LOD0 <=25k; typical 2 material slots; usually 1K shared textures; <=4 influences/vertex; rig/bone ceiling remains an owner decision | `DESIGN.md` lines 673–705 |
| Generic LOD starts | LOD1 50–60%, LOD2 20–30%, far 5–10% of LOD0; then tune by silhouette and measured cost | `DESIGN.md` lines 687–692 |
| Boss low/mobile source target | <=45k skinned triangles; <=96 deform bones; <=3 materials; one 2K packed set; <=180 active particles; 0 dynamic lights; <=24 MB compressed content | `Realm_Boss_Elite_Design_Source.md` lines 328–340 |
| Boss balanced source target | <=80k; <=128 bones; <=4 materials; up to two 2K sets; <=350 particles; <=1 light; <=48 MB | same source |
| Boss high-PC optional source target | <=130k; <=180 bones; <=4 materials; selective 4K plus packed 2K; <=700 particles; <=2 lights; <=96 MB | same source |
| Elite low/mobile source target | <=22k; <=64 bones; <=2 materials; one 1K–2K packed set; <=80 particles; 0 lights; <=10 MB | same source |
| Elite balanced source target | <=45k; <=96 bones; <=3 materials; one 2K set; <=160 particles; <=1 pooled light; <=20 MB | same source |
| Elite high-PC optional source target | <=75k; <=128 bones; <=4 materials; selective 4K hero map, otherwise 2K; <=320 particles; <=1 light; <=40 MB | same source |

Where two provisional sources differ, keep separate budget profiles with their source and platform scope. Do not silently choose the larger number. Asset-specific owner approval resolves which profile controls production.

The following remain explicit owner decisions unless a more specific approved source is cited: Champion/NPC physics counts, character animation-memory limits, clip counts, animator layers, character VFX overdraw, skill-effect concurrency, collider counts, hitbox counts, and any device-tier value not listed above.

### 5.5 Protected degradation rules

Mobile scaling may reduce mesh detail, material slots, texture resolution, secondary bones, cloth/hair simulation, animation layers, particle density, transparency, lights, and secondary effects. It may not hide or change:

- timing and committed result;
- target, danger, ownership, objective, or interaction state;
- face/focal region, weapon/attack origin, primary silhouette, realm cue, or threat cue;
- required hitbox/collider authority;
- reduced-motion, non-color, and off-state accessibility cues.

## 6. Canonical motion matrices

Each realm catalog includes exactly one canonical template for each subject kind. The semantic validator rejects missing or extra baseline keys. A realm may add motions but may not remove these floors.

### 6.1 Champion

- `idle.neutral`, `idle.variant`
- `locomotion.walk`, `locomotion.run`, `locomotion.sprint`
- `locomotion.start`, `locomotion.stop`, `locomotion.turn`
- `locomotion.jump`, `locomotion.fall`, `locomotion.land`
- `combat.dodge`, `combat.block`, `combat.parry`
- `weapon.draw`, `weapon.stow`
- `attack.basic`, `attack.chain`, `attack.heavy`
- `reaction.hit`, `reaction.knockdown`, `reaction.get_up`
- `defeat`, `traversal`, `interaction`, `emote`

### 6.2 NPC

NPCs include the complete Champion floor plus:

- `social.talk`, `social.gesture`
- `daily.sit`, `daily.sleep`, `daily.work`, `daily.carry`
- `daily.gather`, `daily.trade`, `daily.craft`
- `reaction.react`, `reaction.flee`
- `combat.defend`
- every cataloged `role.<action>` key for that archetype

A noncombat NPC may mark a skill or effect requirement `not_applicable` with a rationale; it still needs the baseline motion record or a gated owner revision to the shared template.

### 6.3 Fantasy beast

- `locomotion.turn`
- `idle.neutral`, `idle.variant`
- `attack.basic`, `attack.special`
- `reaction.hit`, `reaction.stagger`
- `defeat`
- `locomotion.<mode>` for every declared `walk`, `run`, `fly`, `swim`, or `crawl` mode

### 6.4 Monster

Monsters include the complete beast floor plus `combat.alert` and every declared locomotion mode.

### 6.5 Boss

Bosses include the complete monster floor plus:

- `boss.enter`
- `boss.phase`
- `boss.transition`
- every declared `boss.transition.<name>` key

### 6.6 Skill motion phases

Every skill trace row includes all five phases:

1. `anticipation`
2. `cast`
3. `channel`
4. `release`
5. `recovery`

A phase is either `required` with one or more motion IDs, or `not_applicable` with no IDs and a non-empty rationale. Gameplay timing remains authoritative; animation events may synchronize presentation but do not create results.

## 7. VFX taxonomy and protected effect grammar

Every motion template and every skill trace row includes all categories:

| Category | Production meaning |
| --- | --- |
| `telegraph` | Pre-commit danger/target/area read |
| `cast` | Cast-state source and intent |
| `channel` | Sustained state and interruption/readability |
| `release` | Committed release cue |
| `trail` | Motion path without obscuring target/read |
| `projectile` | Projectile, beam, or summon travel/body |
| `impact` | Contact location and moment |
| `area` | Persistent or bounded field footprint |
| `buff` | Beneficial state identity |
| `debuff` | Harmful modifier identity |
| `status` | Ongoing status identity |
| `environmental` | Environment-coupled response |
| `result` | Confirmed outcome presentation |
| `cleanup` | Pool return, fade, detachment, and terminal state |

Each VFX record defines source, direction, timing, area, end state, gameplay-authority reference, off/low/balanced/high variants, reduced-motion behavior, an off-state physical/non-color cue, and budget profiles.

Realm grammar is structural and motion-led, never palette-only:

- Stonehold: pressure, sparks, heat distortion, impact, short forceful motion.
- Eldergrove: germination, spirals, drifting seed, elastic recovery, flowing arcs.
- Crownlands: ordered arcs, radiant lines, banners, measured precision.
- Umbral: absorption, delayed trails, smoke, folding space, quiet directional motion.

These are approved global directions from `DESIGN.md`; the exact effect shape, intensity, culture, and skill-specific grammar remains an owner decision unless an approved asset packet supplies it.

## 8. Skill traceability

Every `skills` record has exactly one `skillTraceability` row, and every trace row resolves to one skill. The row contains:

- all five motion phases;
- all fourteen effect categories;
- audio synchronization references;
- camera synchronization references;
- accessibility evidence references.

For a `required` cell, every referenced record must exist and match the same skill and phase/category. For `not_applicable`, the ID list is empty and the rationale explains why. Missing rows, orphan rows, duplicate rows, wrong-category effects, wrong-phase motions, and unknown IDs fail validation.

Skill records preserve exact external catalog identity in `externalSourceId` and identify source catalog, timing authority, and result authority. Presentation never hardcodes or overrides gameplay timing/results.

## 9. Production entry and review gates

Before model/VFX production begins for a record:

1. Stable ID, scope, kind, and provenance resolve.
2. Approved fact or owner-gated proposal state is explicit.
3. Every creative unknown has a decision packet.
4. Rig, module, LOD, collider, hitbox, platform, and budget references resolve.
5. Motion templates are assigned and complete.
6. Every skill has complete motion/effect traceability.
7. Mobile-floor and accessibility degradation preserve protected cues.
8. Generation is explicitly owner-approved.

Before runtime admission:

1. Source rights/provenance are cleared.
2. Bind pose, weights, root motion, contacts, events, transitions, sockets, hitboxes, LODs, pooling, and cleanup pass.
3. Skill-phase synchronization and committed outcomes pass.
4. Physical mobile-floor performance, memory, thermal, and build evidence pass.
5. Owner creative, technical, provenance, motion/effect, performance, accessibility, and release gates pass.
6. The runtime binding is covered by its own catalog/migration contract.

No weighted score may hide a missing motion, effect, reference, or gate.

## 10. Automated validation

Schema and shared-contract suite:

`uv run --with jsonschema python unity/SharedContracts/Tests/validate.py`

Semantic and fail-closed tests:

`uv run --with jsonschema python -m unittest discover -s unity/SharedContracts/Tests -p "test_realm_character_taxonomy.py" -v`

Validate a realm catalog:

`uv run --with jsonschema python unity/SharedContracts/Tests/realm_character_taxonomy.py <repo-relative-catalog.json>`

The semantic validator rejects:

- malformed, duplicate, wrong-kind, cross-realm, or unsorted IDs;
- orphan references and wrong-type references;
- unbacked proposals and owner-decision budgets;
- missing or drifted canonical motion templates;
- missing entity motions, locomotion modes, NPC role motions, or boss transitions;
- subjectless skills, motions, effects, and platform variants;
- missing, duplicate, orphan, wrong-phase, or wrong-category skill trace rows;
- skill-bound motions or effects omitted from their skill's trace row;
- release approval that bypasses owner-approved generation, exact gate states, or evidence;
- an approved catalog with pending records, decisions, or budget metrics.

## 11. Realm-task consumption checklist

Each realm task must:

1. Copy this schema version without weakening required fields or gates.
2. Create one realm-scoped catalog ID and keep shared records byte-identical where reused.
3. Enumerate the full roster; unresolved identity remains a gated proposal, not an omission.
4. Preserve exact external skill IDs and authoritative source references.
5. Populate all platform/budget groups; use source values only as `documented_provisional` until approved.
6. Assign the complete motion template(s) and author every required motion record.
7. Create one complete skill trace row per skill.
8. Create owner packets before proposing morphology, culture, silhouette, anatomy, clothing, armor, animation personality, or magical grammar.
9. Run schema, semantic, orphan, missing-motion, and traceability validation.
10. Hold generation and activation until their explicit owner and release decisions.
