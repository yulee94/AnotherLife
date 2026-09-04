# World Atlas Topology and Immutable Query Contract

**Contract ID:** al_world_atlas_topology_query_contract_v001
**Issue:** #181
**Primary Codex mode:** coordination/review
**Baseline:** main@2f5475cbf41ca6ba740f83859ce0007a1bfdde51
**Status:** binding coordination input for ordered narrative and engineering work; not runtime acceptance

## 1. Purpose

This contract records the user's required Another Life launch-world topology without inventing realm placement, terrain geometry, bridge art, wall art, traversal tuning, or runtime authority.

The world has four realm continents arranged as an abstract ring around one center island. Each adjacent realm pair has two distinct physical bridges. Each realm has one distinct physical bridge to the center island. Every realm separates its protected inner safe zone from its outer warzone through an inner wall, a controlled main-gate transition, and an outer wall.

This document is geometry-neutral. Ring slots are structural identities, not compass quadrants. A realm-to-slot mapping remains a user-owned decision and is unresolved in v001.

## 2. Audited source identity

The current narrative source remains authoritative for copy and requested/nonmutating world anchors:

- source packet ID: al_narrative_world_atlas_source_v001
- catalog ID: al_world_atlas_narrative_catalog
- catalog version: 0.1.0
- path: unity/Assets/AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json
- exact Git blob: 76a1af16eccbbb25350d800069541153818e0adc
- exact UTF-8 length: 13,213 bytes
- exact SHA-256: b65900729dffbec14a537db3aba1bc92a58bddfe5ae31158afdc55983302f178
- introducing PR/commit: #333 / 875ce5a

The current source provides 11 zones, 5 requested objectives, and 32 draft-localization entries. It provides four inner-realm zones, four outer-gate warzone zones, the macro zone zone_crossroads_bridges, Accordant Isle, and a requested Sky Castle marker.

It does not yet provide individual topology nodes, physical bridge records, endpoint records, logical wall records, controlled transition zones, boundary records, realm placement, or an adjacency graph. The macro zone zone_crossroads_bridges is preserved, but it cannot substitute for the twelve physical bridge records required below.

The existing center-island ID zone_accordant_isle is stable and must be preserved because current Wishgate source already references it.

## 3. Authority matrix

| Concern | Authority | v001 rule |
| --- | --- | --- |
| Four-continent ring, center island, bridge counts, and inner/outer boundary sequence | User decision | Binding and represented exactly by this contract |
| Realm-to-slot placement and compass orientation | User | Unresolved; no agent or runtime may infer it |
| Zone/objective names, summaries, lore, quest associations, and player-facing bridge/wall copy | Codex narrative/content consuming approved source | Current copy is preserved; additions require a focused narrative source amendment |
| Stable technical identities, topology invariants, validation, typed results, immutable query behavior, and delivery order | Codex coordination/review | Binding in this document |
| Catalog envelope, provenance, strict schema infrastructure, and publication authority | Existing #183/#263 GameData foundation | Reuse; no parallel authority or loader |
| Runtime validator, immutable snapshot, query planner, consumer migration, registration, tests, and optimization | Codex engineering | Implement only in the ordered phases below |
| Terrain, ecosystems, meshes, materials, VFX, camera, sight distance, bridge/wall dimensions, and visual readability | Terrestrial-design/engineering followed by user approval | Not authorized by this contract |
| PvP enforcement, grace periods, respawn, territory, gems, rewards, saves, scene routing, and pathfinding | Their owning contracts/issues | Not authorized by this contract |
| Final integrated placement, visual design, playtest, milestone, and release | User | Required later |

Merge of a document, catalog, validator, or isolated test does not satisfy runtime integration or user approval.

## 4. Terminology

- **Ring slot:** one abstract continent position in the four-position ring. It has no compass meaning.
- **Center slot:** the one abstract center-island position represented narratively by zone_accordant_isle.
- **Physical bridge:** one distinct bidirectional connection with exactly two explicit endpoint records. One bridge is stored once; a reverse duplicate is invalid.
- **Ring adjacency:** the structural pairs 01-02, 02-03, 03-04, and 01-04.
- **Opposite pair:** 01-03 or 02-04. Opposite slots have no direct bridge in v001.
- **Realm placement:** a bijection from the four canonical realm IDs to the four ring slots.
- **Boundary record:** one realm's ordered logical safe-zone-to-warzone transition. It does not imply geometry or traversal behavior.
- **Requested hook:** visible source intent that remains nonmutating and unavailable until its owning runtime contract is accepted.

## 5. Exact abstract topology

### 5.1 Nodes

The abstract topology contains exactly five nodes in this order:

| Ordinal | Node ID | Role | Realm assignment in v001 |
| ---: | --- | --- | --- |
| 1 | ring_slot_01 | realm continent slot | unresolved |
| 2 | ring_slot_02 | realm continent slot | unresolved |
| 3 | ring_slot_03 | realm continent slot | unresolved |
| 4 | ring_slot_04 | realm continent slot | unresolved |
| 5 | center_slot | neutral center island | zone_accordant_isle |

No ring-slot ordinal is a realm order, compass quadrant, narrative priority, power rank, spawn order, or presentation layer.

### 5.2 Ring adjacency

Exactly four unordered adjacent pairs exist:

| Pair ID | Endpoint A | Endpoint B |
| --- | --- | --- |
| adjacency_ring_01_02 | ring_slot_01 | ring_slot_02 |
| adjacency_ring_02_03 | ring_slot_02 | ring_slot_03 |
| adjacency_ring_03_04 | ring_slot_03 | ring_slot_04 |
| adjacency_ring_01_04 | ring_slot_01 | ring_slot_04 |

The pairs ring_slot_01 to ring_slot_03 and ring_slot_02 to ring_slot_04 are opposite and have no direct bridge.

### 5.3 Physical bridge records

The topology contains exactly twelve physical bridges in this order.

| Ordinal | Bridge ID | Node A | Endpoint A ID | Node B | Endpoint B ID |
| ---: | --- | --- | --- | --- | --- |
| 1 | bridge_ring_01_02_01 | ring_slot_01 | endpoint_bridge_ring_01_02_01_ring_01 | ring_slot_02 | endpoint_bridge_ring_01_02_01_ring_02 |
| 2 | bridge_ring_01_02_02 | ring_slot_01 | endpoint_bridge_ring_01_02_02_ring_01 | ring_slot_02 | endpoint_bridge_ring_01_02_02_ring_02 |
| 3 | bridge_ring_02_03_01 | ring_slot_02 | endpoint_bridge_ring_02_03_01_ring_02 | ring_slot_03 | endpoint_bridge_ring_02_03_01_ring_03 |
| 4 | bridge_ring_02_03_02 | ring_slot_02 | endpoint_bridge_ring_02_03_02_ring_02 | ring_slot_03 | endpoint_bridge_ring_02_03_02_ring_03 |
| 5 | bridge_ring_03_04_01 | ring_slot_03 | endpoint_bridge_ring_03_04_01_ring_03 | ring_slot_04 | endpoint_bridge_ring_03_04_01_ring_04 |
| 6 | bridge_ring_03_04_02 | ring_slot_03 | endpoint_bridge_ring_03_04_02_ring_03 | ring_slot_04 | endpoint_bridge_ring_03_04_02_ring_04 |
| 7 | bridge_ring_01_04_01 | ring_slot_01 | endpoint_bridge_ring_01_04_01_ring_01 | ring_slot_04 | endpoint_bridge_ring_01_04_01_ring_04 |
| 8 | bridge_ring_01_04_02 | ring_slot_01 | endpoint_bridge_ring_01_04_02_ring_01 | ring_slot_04 | endpoint_bridge_ring_01_04_02_ring_04 |
| 9 | bridge_center_ring_01_01 | ring_slot_01 | endpoint_bridge_center_ring_01_01_ring_01 | center_slot | endpoint_bridge_center_ring_01_01_center |
| 10 | bridge_center_ring_02_01 | ring_slot_02 | endpoint_bridge_center_ring_02_01_ring_02 | center_slot | endpoint_bridge_center_ring_02_01_center |
| 11 | bridge_center_ring_03_01 | ring_slot_03 | endpoint_bridge_center_ring_03_01_ring_03 | center_slot | endpoint_bridge_center_ring_03_01_center |
| 12 | bridge_center_ring_04_01 | ring_slot_04 | endpoint_bridge_center_ring_04_01_ring_04 | center_slot | endpoint_bridge_center_ring_04_01_center |

Each bridge is physical, undirected, and bidirectionally queryable. Directional travel rules, one-way states, destruction, repair, capture, siege, collision, and navigation are not defined.

### 5.4 Structural invariants

A valid abstract topology satisfies all of the following:

1. It contains exactly four ring-slot nodes and one center node.
2. It contains exactly twelve physical bridge records and twenty-four endpoint records.
3. Each of the four adjacent ring pairs has exactly two distinct bridges.
4. Each ring slot has exactly two distinct ring neighbors.
5. Each ring slot participates in four inter-ring bridges and exactly one center bridge, for five physical bridge records total.
6. The center slot participates in exactly four bridges, one from each ring slot.
7. No self-edge exists.
8. No direct opposite-pair bridge exists.
9. No reverse duplicate, duplicate endpoint pair, or duplicate physical bridge identity exists.
10. Every bridge references two distinct existing nodes and two distinct, globally unique endpoint IDs.
11. Every endpoint references exactly one bridge and one of that bridge's nodes.
12. Removing zone_crossroads_bridges does not remove or synthesize any physical edge; that zone is narrative grouping only.
13. No realm-specific neighbor or route claim is accepted until a valid realm placement is available.

## 6. Realm placement gate

The canonical realm set is exactly:

1. crownlands
2. stonehold
3. eldergrove
4. umbral

This list is a deterministic catalog order only. It is not spatial authority.

The v001 placement record is:

- placementStatus: unresolved_user_gate
- assignments: empty
- compassOrientation: unresolved
- source: user_decision_required

A resolved placement must be an explicit bijection: every canonical realm appears exactly once, every ring slot receives exactly one realm, and no other realm or slot appears.

Until the user records that mapping:

- abstract slot-topology validation may return Accepted;
- slot-level node and bridge queries may return immutable results;
- realm-specific adjacency, realm-to-realm bridge, realm-to-center bridge, and realm-route queries must return ReferenceUnavailable;
- the required diagnostic code is AL-ATLAS-REALM-PLACEMENT-UNRESOLVED;
- no caller may use realm catalog order, array order, display order, screen position, culture, case conversion, hash order, or random choice as placement;
- production realm topology must not publish;
- no partial assignment may be exposed as accepted authority.

A later source version must persist the user's exact mapping. It must not silently reinterpret v001 slot ordinals.

## 7. Realm boundary contract

### 7.1 Required sequence

Every realm has exactly one ordered boundary record:

protected inner safe zone -> inner wall -> controlled main-gate transition -> outer wall -> outer warzone

The inner wall separates the protected inner safe zone from the controlled transition. The outer wall separates the controlled transition from the outer warzone. The existing realm main-gate ID participates in the controlled transition; it is not merely display copy.

Walls and transitions in this contract are logical identities and references. They grant no mesh, material, collision, destruction, siege, traversal, PvP, grace-period, respawn, camera, or scene authority.

### 7.2 Cross-catalog mapping

The narrative source amendment must preserve the existing realm-catalog and atlas IDs while adding the transition, wall, and boundary identities shown below.

| Realm ID | Realm inner ID | Current inner atlas zone | New inner wall ID | New transition zone ID | Existing main gate ID | New outer wall ID | Realm outer-warzone ID | Current outer-gate atlas zone |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| crownlands | inner_crownlands | zone_inner_crownlands | wall_crownlands_inner | zone_transition_crownlands_gate | gate_crownlands_meridian | wall_crownlands_outer | warzone_crownlands | zone_warzone_crownlands_gate |
| stonehold | inner_stonehold | zone_inner_stonehold | wall_stonehold_inner | zone_transition_stonehold_gate | gate_stonehold_faultline | wall_stonehold_outer | warzone_stonehold | zone_warzone_stonehold_gate |
| eldergrove | inner_eldergrove | zone_inner_eldergrove | wall_eldergrove_inner | zone_transition_eldergrove_gate | gate_eldergrove_greenveil | wall_eldergrove_outer | warzone_eldergrove | zone_warzone_eldergrove_gate |
| umbral | inner_umbral | zone_inner_umbral | wall_umbral_inner | zone_transition_umbral_gate | gate_umbral_ashvein | wall_umbral_outer | warzone_umbral | zone_warzone_umbral_gate |

Boundary record IDs are:

- boundary_crownlands_safe_to_warzone
- boundary_stonehold_safe_to_warzone
- boundary_eldergrove_safe_to_warzone
- boundary_umbral_safe_to_warzone

Each boundary record must reference exactly one canonical realm ID, all eight corresponding identities from its table row, and the exact sequence declared above. Cross-realm references, missing stages, repeated stages, reversed stages, unknown IDs, or additional stages are invalid.

The four boundary records use canonical realm catalog order for deterministic output. That ordering does not imply spatial placement.

### 7.3 Required source amendment minimum

The focused narrative amendment after this contract must:

- version the narrative packet/catalog;
- retain the existing 11 zone IDs, 5 objective IDs, 32 localization entries, source authorities, requested status, and nonmutating rules unless an explicitly reviewed source correction requires otherwise;
- add exactly four controlled main-gate transition zones;
- add the five abstract topology nodes;
- add the twelve physical bridge records and twenty-four explicit endpoints;
- add exactly eight logical walls and four ordered boundary records;
- add the unresolved placement object exactly as defined above;
- preserve zone_crossroads_bridges as a macro narrative zone;
- preserve zone_accordant_isle and its forced-neutral source intent;
- reference existing realm inner, outer-warzone, and main-gate IDs instead of duplicating them;
- add localization only for approved player-facing text; technical identities do not require invented lore;
- keep scene, territory, scoring, gem, PvP enforcement, reward, save, and traversal hooks requested and nonmutating;
- include exact provenance and an engineering handoff for the specialized validator.

It must not assign realms to slots, invent compass orientation, rename established zones, create bridge/wall lore, change quest meaning, or activate runtime behavior.

## 8. Stable identity rules

Topology IDs use ASCII lowercase snake case and are ordinal, case-sensitive identities.

For every topology, boundary, wall, transition, bridge, and endpoint identity:

- encoded length is 1 through 96 UTF-8 bytes;
- the grammar is ^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$: the first byte is a lowercase ASCII letter, followed by lowercase ASCII letters or digits in segments separated by single underscores;
- leading, trailing, or repeated underscores are invalid;
- whitespace, Unicode lookalikes, punctuation, uppercase, mixed case, empty values, and normalization are invalid;
- no trim, case fold, culture normalization, alias, basename inference, display-name inference, or hash-order inference is permitted;
- identity comparison uses exact ordinal bytes;
- all topology identities are globally unique across node, adjacency, bridge, endpoint, wall, boundary, and transition records;
- referenced external realm, zone, gate, inner-realm, and outer-warzone IDs must resolve exactly against their accepted source catalogs;
- endpoint IDs are persisted source data and validated; runtime must not silently manufacture missing endpoints;
- a new source version may add a reviewed migration, but v001 identity cannot be silently rewritten.

Localization keys retain their existing dotted-key contract and are not topology IDs.

## 9. Strict catalog and semantic validation

### 9.1 Bounded input

The technical catalog must be data-only and bounded:

- one UTF-8 artifact, maximum 262,144 bytes;
- UTF-8 without BOM, no invalid sequences, and a retained final LF;
- one closed root object with explicit schema and content versions;
- maximum 256 total localization entries;
- exactly 5 topology nodes;
- exactly 4 adjacency records;
- exactly 12 bridge records;
- exactly 24 endpoint records;
- exactly 8 wall records;
- exactly 4 boundary records;
- exactly 4 transition zones;
- maximum 128 retained canonical diagnostics.

The source amendment may contain the existing zones and objectives in addition to these exact topology collections. No unbounded extension dictionary, recursive structure, arbitrary payload, embedded binary, asset reference expansion, or dynamic script is allowed.

### 9.2 Required validation order

A candidate is evaluated without mutating published authority. Validation order is:

1. transport and size;
2. UTF-8/line contract and JSON parse;
3. closed schema, required fields, types, counts, and versions;
4. source/catalog/provenance identity;
5. ID grammar and global uniqueness;
6. external catalog reference resolution;
7. node, adjacency, bridge, and endpoint invariants;
8. placement state and bijection rules;
9. wall/transition/boundary sequence and cross-catalog mapping;
10. localization and requested-hook references;
11. canonical fingerprint/hash verification.

Diagnostics are collected within the fixed cap, then sorted canonically by severity, diagnostic code, source path, and related ID using ordinal comparison. Truncation occurs only after canonical sorting. Repeated validation of the same bytes must produce byte-identical status, diagnostics, fingerprint, and snapshot.

Any Error rejects the candidate. A rejected reload cannot partially publish and cannot replace a previously accepted authority. If no accepted authority exists, state is Unavailable. Runtime must never construct or publish a fallback atlas.

### 9.3 Minimum typed diagnostics

The engineering contract must preserve at least these stable conditions:

| Code | Meaning |
| --- | --- |
| AL-ATLAS-CATALOG-UNAVAILABLE | No accepted catalog authority is available |
| AL-ATLAS-CATALOG-INVALID | Candidate failed structural or semantic validation |
| AL-ATLAS-REFERENCE-UNKNOWN | A required realm, zone, gate, node, bridge, endpoint, wall, or localization reference is unknown |
| AL-ATLAS-TOPOLOGY-INVALID | The exact five-node/twelve-bridge invariant is violated |
| AL-ATLAS-BOUNDARY-INVALID | A realm boundary is missing, duplicated, cross-wired, reordered, or incomplete |
| AL-ATLAS-REALM-PLACEMENT-UNRESOLVED | Realm-specific topology was requested before user placement exists |
| AL-ATLAS-VIEWER-UNAVAILABLE | Viewer realm context is not committed-valid |
| AL-ATLAS-CONTENT-UNAVAILABLE | Optional story/localization content required by the query is unavailable |
| AL-ATLAS-QUERY-INVALID | Query input is blank, unknown, mismatched, unsupported, or otherwise invalid |

Additional diagnostics require a versioned contract. Diagnostic text is developer evidence, not player-facing localization.

## 10. Immutable snapshot and query contract

### 10.1 Published authority

An accepted snapshot contains immutable value records and immutable bounded collections for:

- source identity and provenance;
- zones and requested objectives;
- topology nodes and adjacency;
- bridges and endpoints;
- placement state and assignments;
- walls, transitions, and boundaries;
- localization/source references;
- canonical fingerprint and diagnostics.

No public mutable field, mutable list, mutable dictionary, source DTO, or backing collection may escape. Returned records cannot mutate accepted authority. Repeated queries cannot mutate internal ordering, caches, viewer state, story state, objectives, scenes, saves, territory, gems, PvP state, or presentation.

A candidate snapshot is built completely off to the side, validated, fingerprinted, and published atomically only after acceptance.

### 10.2 Query families

The pure technical phase supports:

- complete abstract topology snapshot;
- node lookup by exact ID;
- bridge lookup by exact ID;
- bridges for an abstract slot;
- exact abstract neighbors for a ring slot;
- boundary lookup by canonical realm ID;
- zone and requested-objective lookup;
- realm-specific topology only when placement is resolved;
- viewer-filtered narrative queries only with an injected committed-valid realm context;
- explicit requested/unavailable scene and gameplay hook status.

Abstract slot queries remain available while placement is unresolved. Realm-specific topology queries do not.

### 10.3 Deterministic ordering

- nodes follow the table in section 5.1;
- adjacency follows section 5.2;
- bridges and endpoints follow section 5.3;
- boundary records follow crownlands, stonehold, eldergrove, umbral;
- zone and objective order follows the accepted source artifact unless a later version explicitly changes it;
- lookup-result collections retain their parent canonical order;
- diagnostics use the order in section 9.2.

Dictionary enumeration, reflection order, filesystem order, locale, current culture, process hash, and caller-provided order are never authority.

### 10.4 Viewer and optional-content behavior

A viewer-bound query accepts only an injected committed-valid canonical realm identity. None, undefined enum values, blank IDs, uppercase or mixed-case IDs, unknown IDs, unavailable profiles, uncommitted selections, and mismatched ID/enum pairs return a typed failure and mutate nothing.

Missing or failing story/localization services cannot silently remove or rewrite accepted topology. The query returns the accepted structural data plus typed AL-ATLAS-CONTENT-UNAVAILABLE status for unavailable optional content, or fails only if the requested operation explicitly requires that content.

Unknown scene routes and requested gameplay hooks remain visible as requested/unavailable. They cannot launch fallback gameplay or fabricate success.

## 11. Source-to-runtime authority path

Implementation must reuse the existing #183/#263 GameData authority:

1. narrative/content publishes a versioned source amendment;
2. coordination/review verifies source fidelity and this contract;
3. engineering uses the existing bounded catalog envelope/schema/provenance infrastructure plus a specialized world-atlas semantic validator;
4. engineering produces one validated immutable snapshot and query planner without production registration;
5. after placement and source gates, engineering generates/publishes one Player-resident runtime artifact;
6. production services consume that accepted authority;
7. legacy fallback construction is removed only in the integration phase;
8. coordination, narrative fidelity, and user gates remain separate.

Do not create a second world-atlas catalog authority, a parallel JSON loader, a second realm catalog, generated aliases, or a silent fallback.

The accepted realm technical snapshot must retain and expose each realm's existing innerRealmId, outerWarzoneId, and mainGateId. Current runtime DTOs that discard these fields cannot satisfy cross-catalog boundary validation.

## 12. Ordered delivery

### Phase A — this coordination contract

- one new documentation path only;
- no source catalog, runtime, tests, assets, scenes, saves, Android, or shared-file change;
- merge after exact-head source-mode review and repository gates;
- no new user approval is required because this records existing user topology and preserves unresolved decisions.

### Phase B — narrative source v002 amendment

Primary mode: narrative/content.

Add the abstract graph, endpoints, walls, boundaries, transition zones, cross-catalog references, and unresolved placement state. Preserve existing meaning, stable IDs, requested statuses, Wishgate reference, and nonmutating boundaries. No realm placement or new lore.

### Phase C — realm technical convergence

Under #183, ensure the accepted realm schema/snapshot preserves exact innerRealmId, outerWarzoneId, and mainGateId references. Do not combine unrelated boss/equipment source work solely because it shares a parent authority framework.

### Phase D — pure atlas technical foundation

Primary mode: engineering.

Add the specialized schema/semantic validator, persisted negative fixtures, immutable snapshot, pure query planner, typed results, deterministic diagnostics, and focused tests. No production registration, service replacement, geometry, scene, save, or protected shared file.

### Phase E — user placement decision

The user supplies the exact four-realm-to-four-slot bijection and optional compass orientation. This blocks realm-specific topology publication, not abstract validation or pure query work.

### Phase F — resolved narrative source

Narrative/content persists the approved placement and any separately approved player-facing bridge, wall, gate, or location copy. Technical IDs remain stable.

### Phase G — production integration

Engineering generates/publishes one accepted runtime artifact, replaces BuildFallbackAtlas, migrates IWorldAtlasService and WorldObjectiveMarkerSpawner, and keeps unavailable scenes/routes honest.

Re-audit open PRs and declare the Bootloader.cs and/or LocalGameDataService.cs exclusive soft lock only if those files are actually changed. Preserve old saves; this contract itself adds no save field.

### Phase H — dispositions and approval

- coordination/review verifies integration and evidence;
- narrative/content verifies source/runtime fidelity;
- terrestrial-design verifies later visual source fidelity where applicable;
- user approves placement, presentation, readability, playtest, milestone, and release separately.

## 13. Required evidence

### 13.1 Source and schema

- exact packet/catalog/version/byte/hash/provenance match;
- positive source fixture accepted;
- persisted representative negative fixtures rejected;
- closed objects and bounded arrays;
- blank, malformed, duplicate, missing, extra, uppercase, mixed-case, reordered, unknown, stale-version, and hash-mismatch cases;
- exact external realm/zone/gate/localization references.

### 13.2 Topology

- exactly 5 nodes, 4 adjacency pairs, 12 bridges, and 24 endpoints;
- exactly 2 bridges for every adjacent ring pair;
- exactly 1 center bridge for every ring slot;
- no self, reverse duplicate, duplicate endpoint, or opposite-pair bridge;
- every ring slot has two neighbors and five physical bridge records;
- center has four physical bridge records;
- all endpoints and references resolve;
- unresolved placement rejects every realm-specific topology query nonmutatingly;
- resolved-placement fixtures accept only a complete bijection.

### 13.3 Boundaries

- exactly 4 transitions, 8 walls, and 4 boundary records;
- exact cross-catalog mapping for all four realms;
- exact safe-zone, inner-wall, gate-transition, outer-wall, outer-warzone order;
- missing, duplicate, reordered, cross-realm, or unknown stages rejected;
- wall/gate records grant no implicit scene, PvP, save, territory, or geometry authority.

### 13.4 Queries and integration

- returned collections and records cannot mutate accepted authority;
- repeated queries are pure and deterministically ordered;
- invalid viewer and unknown lookup cases are typed;
- failed reload cannot publish partial authority;
- missing optional content is visible and cannot change topology;
- requested hooks remain visible and nonmutating;
- legacy fallback and unapproved passive objective weights are absent from the accepted production path;
- Unity compile, focused and full EditMode, safe PlayMode, applicable Player/device, classify, hygiene, and diff checks are reported in their applicable phases;
- every unavailable or unperformed check is explicit.

## 14. Optimization and compatibility

- Keep the topology data-only and within the fixed bounds in section 9.
- Maintain one accepted Player-resident runtime artifact; do not retain duplicate parsed catalogs or unbounded history.
- Parse/validate once per candidate load. Build bounded indexes once. Queries must not scan full catalogs repeatedly when an immutable index suffices.
- No polling, background refresh loop, eager geometry, eager asset load, world-wide GameObject instantiation, recursive graph expansion, or per-frame allocation is authorized.
- Use stable asset/scene/presentation references for later lazy streaming; an atlas query never loads them.
- Keep bridge, wall, and boundary records independent from mesh and VFX weight.
- Preserve later configurable sight-distance and scalable quality-tier integration; this contract adds no render-distance value.
- Report snapshot bytes, retained indexes, load allocations/time, query allocations/time, Player/build/install delta, and low-end/mobile compatibility in engineering phases.
- A missing catalog, unresolved placement, unsupported version, or failed reload is a typed unavailable/rejected state, never permission to construct fallback data.
- If a future durable profile stores topology identity, add an explicit backward-compatible migration under #137; do not infer one here.

## 15. Current runtime defects this contract must not normalize

At the audited baseline:

- LocalWorldAtlasService always constructs BuildFallbackAtlas instead of consuming the source catalog;
- the fallback's 9 zones and 13 objectives differ from the source's 11 zones and 5 objectives;
- neutral_borderlands is treated as a contested central warzone, conflicting with source-authoritative forced-neutral Accordant Isle;
- IWorldAtlasService exposes mutable records and backing lists;
- duplicate IDs overwrite silently;
- query ordering is implicit;
- story-service absence/errors silently change narration;
- WorldObjectiveMarkerSpawner consumes unapproved PassiveCreditWeight values and constructs presentation from fallback data;
- current realm runtime DTOs discard innerRealmId, outerWarzoneId, and mainGateId;
- production build settings contain no world/warzone destination;
- no focused atlas validator/query suite exists.

These are defects for later phases. They are not authorization to edit runtime in the coordination or narrative source PR.

## 16. Explicit non-goals

This contract does not define or authorize:

- realm-to-quadrant assignment or compass rotation;
- continent outline, terrain height, biome, ecosystem, prop, mesh, material, VFX, lighting, audio, or camera;
- exact bridge landing points, bridge dimensions, wall dimensions, gate dimensions, traversal distance, or architecture;
- destruction, repair, siege, collision, navigation, pathfinding, spawn, or encounter behavior;
- main-gate PvP, grace, respawn, save-pillar, territory, gem, reward, score, economy, or wish behavior;
- scene creation/loading or Android navigation;
- new dialogue, lore, quests, objective balance, localization copy, or player-facing names;
- persistence or save migration;
- final visual, balance, playtest, milestone, or release approval.

## 17. Unresolved user-owned decisions

The following remain explicit later gates:

1. Which realm occupies ring_slot_01, ring_slot_02, ring_slot_03, and ring_slot_04.
2. Whether the abstract ring receives a compass orientation and, if so, its rotation.
3. Exact continent shapes and physical bridge landing locations.
4. Bridge, wall, gate, and transition architecture, names, dimensions, and traversal distance.
5. Main-gate and center-island landing PvP/grace/respawn behavior.
6. Final terrain/ecosystem presentation, objective tuning, sight-distance behavior, world readability, and integrated playtest.

None blocks this contract, the unresolved narrative source amendment, or pure validator/query work. The placement mapping blocks production realm-specific topology publication.

## 18. Acceptance status

- [x] Existing user topology is recorded without quadrant invention.
- [x] The abstract five-node/twelve-bridge graph is exact.
- [x] Each adjacent pair has two bridges and each ring slot has one center bridge.
- [x] Inner wall, controlled main-gate transition, outer wall, and outer warzone ordering is exact for all realms.
- [x] Existing realm, zone, gate, and Accordant Isle identities are preserved.
- [x] Unresolved placement has a typed fail-closed contract.
- [x] Validation, immutable queries, optimization, delivery order, locks, and user gates are specified.
- [ ] Narrative source v002 exposes the accepted topology and boundary references.
- [ ] Realm technical authority retains inner/outer/gate references.
- [ ] Pure specialized schema, validator, immutable snapshot, query planner, and negative fixtures pass.
- [ ] User realm placement is recorded.
- [ ] Resolved source and runtime artifact match.
- [ ] Legacy fallback authority and unapproved marker weights are removed from production use.
- [ ] Production compile/test/Player/device/performance/package evidence passes.
- [ ] Narrative fidelity and final user visual/playtest/release approval are recorded.

This document completes only Phase A coordination. It creates no production or creative acceptance claim.

## 19. Protected-zone query addendum

**Addendum contract ID:** `al_world_atlas_protected_zone_query_contract_v001`

Catalog source v003 extends, but does not rename or reorder, the v002 topology,
boundary, zone, objective, or localization identities. It adds three immutable
contract-only policy IDs:

1. `zone_policy_city_safe_v001`
2. `zone_policy_beginner_safe_v001`
3. `zone_policy_town_safe_v001`

Each policy is `forced_non_pvp`, applies to all player harmful effects, requires
revalidation at effect application, blocks war override, has no mutation
authority, and remains `contract_only`. This metadata does not enforce PvP,
move an actor, infer presence, or define a physical boundary.

Each canonical realm exposes one technical subzone ID per policy kind, ordered
by `crownlands`, `stonehold`, `eldergrove`, `umbral`, then `city`, `beginner`,
`town`: `zone_protected_<realm>_<kind>`. Every record references its existing
`zone_inner_<realm>` parent and the corresponding policy ID. Names, city/town
placement, geometry, scenes, triggers, traversal, presence authority, and
player-facing copy remain unavailable.

The specialized validator rejects missing, duplicate, reordered, cross-realm,
unknown-parent, unknown-policy, activated-enforcement, war-override, or mutation
claims. Immutable typed queries expose exact policy/subzone lookup and canonical
per-realm subzone lists. Unknown and malformed IDs return typed failures; no
query mutates movement, combat, PvP, scenes, saves, territory, or catalog state.
