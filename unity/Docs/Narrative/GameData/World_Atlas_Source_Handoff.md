# World Atlas Source Handoff

**Packet ID:** `al_narrative_world_atlas_source_v002`
**Catalog version:** `0.2.0`
**Primary delivery mode:** Codex narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json`
**Topology contract:** `al_world_atlas_topology_query_contract_v001`
**Related issue:** #181

## Source intent

This packet exposes the user's approved abstract four-realm world topology as
versioned narrative source without deciding where any realm sits. It gives the
later #181 technical validator stable node, bridge, endpoint, wall, transition,
and boundary references while preserving the original world-atlas narrative
meaning.

The packet remains deliberately nonmutating. Every bridge, transition,
boundary, scene, and objective capability is `requested`; none grants runtime
scene loading, travel, PvP, territory, Realm Gem, reward, save, collision,
pathfinding, geometry, or presentation authority.

## Preserved v001 narrative source

The following v001 collections and their order are unchanged:

- 11 zones;
- 5 requested objectives;
- 32 draft-localization records;
- the four inner-realm and four outer-gate zone identities;
- the `zone_crossroads_bridges` macro narrative zone;
- the forced-neutral `zone_accordant_isle` source intent and Wishgate anchor;
- the requested `zone_sky_castle_marker` NVS-01 anchor;
- all player-facing copy, quest associations, visibility rules, and blocked
  runtime claims that existed in v001.

The pre-v002 `sourceAuthorities` object is unchanged except for one additive
`topologyContract` provenance record. No established zone, objective,
localization, realm, gate, quest, or Wishgate ID was renamed or aliased.

## Approved abstract topology

The source records exactly five nodes in contract order:

1. `ring_slot_01`
2. `ring_slot_02`
3. `ring_slot_03`
4. `ring_slot_04`
5. `center_slot`, referencing `zone_accordant_isle`

Ring slots are structural identities only. Their ordinals are not compass
directions, realm order, power rank, spawn order, or presentation order.

The four exact adjacent ring pairs are:

- `ring_slot_01`–`ring_slot_02`;
- `ring_slot_02`–`ring_slot_03`;
- `ring_slot_03`–`ring_slot_04`;
- `ring_slot_01`–`ring_slot_04`.

Each adjacent pair has exactly two distinct physical bidirectional bridge
records. Each ring slot has exactly one distinct bridge to `center_slot`. The
catalog therefore contains exactly 12 bridges and 24 explicit globally unique
endpoints. There is no self-edge, opposite-slot bridge, reverse duplicate, or
implicit edge synthesized from `zone_crossroads_bridges`.

Bridge records describe stable source identity only. Their traversal and
mutation hooks remain requested and unavailable until Codex engineering
implements and validates the owning contracts.

## Realm boundaries

The packet records four controlled transition zones, eight logical walls, and
four realm boundary records in canonical realm-catalog order. Each boundary
preserves this exact sequence:

```text
protected inner safe zone
-> inner wall
-> controlled main-gate transition
-> outer wall
-> outer warzone
```

The exact cross-catalog references are:

| Realm | Inner realm / atlas zone | Inner wall | Transition / existing gate | Outer wall | Outer warzone / atlas zone |
| --- | --- | --- | --- | --- | --- |
| `crownlands` | `inner_crownlands` / `zone_inner_crownlands` | `wall_crownlands_inner` | `zone_transition_crownlands_gate` / `gate_crownlands_meridian` | `wall_crownlands_outer` | `warzone_crownlands` / `zone_warzone_crownlands_gate` |
| `stonehold` | `inner_stonehold` / `zone_inner_stonehold` | `wall_stonehold_inner` | `zone_transition_stonehold_gate` / `gate_stonehold_faultline` | `wall_stonehold_outer` | `warzone_stonehold` / `zone_warzone_stonehold_gate` |
| `eldergrove` | `inner_eldergrove` / `zone_inner_eldergrove` | `wall_eldergrove_inner` | `zone_transition_eldergrove_gate` / `gate_eldergrove_greenveil` | `wall_eldergrove_outer` | `warzone_eldergrove` / `zone_warzone_eldergrove_gate` |
| `umbral` | `inner_umbral` / `zone_inner_umbral` | `wall_umbral_inner` | `zone_transition_umbral_gate` / `gate_umbral_ashvein` | `wall_umbral_outer` | `warzone_umbral` / `zone_warzone_umbral_gate` |

Walls and transitions are logical source references. This packet does not
define their dimensions, architecture, materials, collision, destruction,
repair, traversal time, grace periods, spawn behavior, or visual treatment.

## Unresolved user placement gate

The placement record remains exactly:

```text
status: unresolved_user_gate
assignments: []
compassOrientation: unresolved
source: user_decision_required
```

No realm is assigned to a ring slot. No contributor or runtime may infer a
mapping from realm-catalog order, array order, UI position, culture, hash order,
or random choice. Abstract slot queries may be implemented later, but every
realm-specific topology query must remain unavailable until the user records a
complete four-realm-to-four-slot bijection.

## Canonical source identity

The canonical identity below is calculated from the staged Git blob: UTF-8,
without BOM, LF-only, with one retained final LF. It intentionally does not use
the Windows working-tree byte representation.

- catalog path: `unity/Assets/AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json`
- catalog ID: `al_world_atlas_narrative_catalog`
- catalog version: `0.2.0`
- source packet ID: `al_narrative_world_atlas_source_v002`
- exact Git blob: `d71e7792603f095c49dccd7f4f6ff762aa043c2a`
- exact canonical UTF-8 length: 29,895 bytes
- exact SHA-256: `d3db74638b55128a46581e31d0c9d0ef9861b743b0b33d1ddf7a5571c9cfd711`

The preserved v001 source introduced by PR #333 remains historical lineage:

- packet: `al_narrative_world_atlas_source_v001`
- Git blob: `76a1af16eccbbb25350d800069541153818e0adc`
- canonical UTF-8 length: 13,213 bytes
- SHA-256: `b65900729dffbec14a537db3aba1bc92a58bddfe5ae31158afdc55983302f178`

## Validation evidence

The focused source validator performed the following checks against exact
current-main v001 source, the current realm catalog, and the v002 candidate:

- JSON parsing and version/packet/contract provenance;
- exact semantic preservation of the 11 zones, 5 objectives, 32 localization
  records, atlas policy, original source authorities, and original handoff
  requirements;
- exact 5-node, 4-adjacency, 12-bridge, and 24-endpoint order and cardinality;
- lowercase ASCII snake-case, 96-byte maximum, and global identity uniqueness;
- exact adjacent-pair multiplicity, center bridges, node degree, endpoint
  ownership, and reference closure;
- exact unresolved placement state with no realm assignment or compass value;
- exact four transition zones, eight walls, and four ordered boundary chains;
- exact realm-catalog `innerRealmId`, `mainGateId`, and `outerWarzoneId`
  references;
- requested/nonmutating status for zones, objectives, bridges, transitions,
  walls, and boundaries;
- 26 representative stale, missing, extra, malformed, duplicate, opposite,
  self-edge, cross-realm, reordered, activated, and inferred-placement negative
  fixtures, all rejected.

Repository diff, classification, hygiene, and hosted checks remain evidence
requirements for publication on the eventual exact post-integration base. Unity
Editor, Player, PlayMode, Android device, visual, and runtime-performance checks
were not performed for this local source handoff and are not claimed.

The canonical catalog grows from 13,213 to 29,895 bytes, an exact raw
StreamingAssets payload increase of 16,682 bytes before platform packaging or
compression. There is no new code, dependency, asset, eager load, cache,
polling, per-frame work, or runtime allocation path. Exact Player build and
installed-size deltas remain unmeasured and platform-dependent; a later
packaging phase must report them rather than treating this source amendment as
zero-impact.

## Windows byte-stability follow-up

PR #429 protects the repository's then-current raw-byte-pinned paths, but this
atlas source is not yet listed in `unity/.gitattributes` or the byte-migration
target set. A Windows checkout with `core.autocrlf=true` can therefore retain a
CRLF working copy even though Git stores the canonical LF blob above.

Before any runtime adapter, schema, validator, or packaged consumer treats this
v002 SHA-256 as raw-file authority, Codex engineering must land a separate
workflow-focused change that pins this exact path to LF and updates the safe
byte-migration/check target to this v002 identity. That follow-up must not be
folded into this narrative PR or treated as runtime activation.

## Handoff and acceptance status

Narrative source status: ready for A1 source-mode review after current-main
refresh and repository gates.

This packet completes only #181 Phase B source authoring. It does not complete
the separate byte-stability follow-up, specialized schema/validator, immutable
query foundation, realm placement, runtime artifact, production registration,
legacy fallback removal, scenes, world geometry, terrestrial design, visual
fidelity, integrated playtest, milestone, or release.

No new user approval is required for this amendment because it records the
already-approved abstract topology and preserves every unresolved creative
decision. The user still owns realm-to-slot placement, compass orientation,
world geometry, presentation, balance, integrated playtest, and release.
