# Slagfall Quarry v002 A1 Technical Handoff

## Control

- Tracking issue: `#259`
- Approved source PR: `#418`
- Approved source head: `53dc2096fe4c9ac2bada8f05f88640788b8d938f`
- Approved source version: `tdf-eco-slagfall-2026-07-30-v002`
- User approval evidence:
  `https://github.com/yulee94/AnotherLife/pull/418#issuecomment-5136011144`
- User gate: minimum `90 / 100`
- A2 visual-verdict result: `95 / 100`
- Primary delivery mode: `Codex coordination/review`
- Engineering state: `ReadyForRepresentativeSliceAfterDependencies`
- Runtime integration state: `BlockedUntilEngineeringEvidence`
- Narrative naming state: `WorkingLabelsOnly`

This handoff converts the exact user-approved A2 source into a bounded
production contract. It does not create a production asset, runtime catalog,
spawn rule, AI behavior, combat role, reward, quest, final name, or scene.

## Required Dependency Order

The first representative engineering slice may begin only after:

1. PR `#418` merges the exact approved A2 source without pixel or identity
   changes;
2. this A1 handoff merges without weakening its source-fidelity or safety
   gates;
3. the engineering PR declares the applicable runtime catalog and packaging
   authority;
4. the engineering PR consumes the territory load-safety contract from
   PR `#419` or an equivalent merged successor;
5. no implementation PR edits A2 source assets or silently fills an unresolved
   creative decision.

PR `#419` is a separate engineering dependency. Its 100-user crowd safety
contract must remain separate from the Slagwhistle population authority:
players count toward the protected 100-user representation contract;
Slagwhistles use their own later-authorized pool and presentation budget.

## Immutable Approved Source

| Role | Asset ID | SHA-256 | Bytes |
| --- | --- | --- | ---: |
| Habitat master | `tdf_asset_habitat_stonehold_slagfall_quarry_master_v002` | `600a76d983f0cb63abf1169b7a9cdf34477b60ebf2e10ca9f74883efd899d195` | `3,133,264` |
| Slagwhistle identity | `tdf_asset_fauna_stonehold_slagwhistle_burrower_identity_v002` | `1a08581ef2a49d56f3e3b5a9925a88ee7eebcb6df2895de61691f74b820eaa05` | `2,521,039` |
| Slagwhistle motion/contact | `tdf_asset_fauna_stonehold_slagwhistle_burrower_motion_contact_v002` | `1099937075dba7012545afb7636e100c592c561c30c2fe68ce7a434ca4ff2d92` | `2,617,228` |

The PNGs remain review source under `unity/Docs`. They must never be copied
into a Player asset folder, referenced by a runtime prefab, used as a runtime
texture, or treated as topology.

Any changed pixel, anatomy, silhouette, material meaning, motion meaning, or
habitat grammar requires a new A2 source version and another user decision.

## Protected Fidelity

### Habitat

Production must preserve:

- a broad compressed quarry bowl;
- a small number of unequal interlocked fracture rafts;
- diagonal and radial breakup with missing corners, undercuts, talus, soil,
  and visibly interrupted edges;
- discontinuous braided runoff that crosses slopes, pools, disappears, and
  cannot read as a road;
- broad recessed collapsed Ore Gallery mouths with rubble fans;
- a distinct Faultroad transition through thinning rafts, iron soil, scree,
  and diagonal fault planes;
- no spire, monument, volcano, stable central landmark, road, stair, rail,
  masonry, wall, gate, lava, or mandatory airborne effect.

The user-approved sheet retains one implementation caution: wide and plan
views can drift back toward a tiled plate field. Production graybox review
must therefore compare the habitat in grayscale at gameplay height and reject
continuous cell rhythms, clean rows, curb edges, or stair-step stacking.

### Slagwhistle

Production must preserve:

- one low wedge skull integrated into the neck;
- protected horizontal slit nostrils;
- no visible external ears;
- exactly two shoulder-rooted keratin vent folds forming a closed bracket
  yoke;
- exactly one fused crescent shovel palm plus two short stabilizer claws per
  forefoot;
- compact hindquarters and a short, broad, dorsoventrally flattened brace
  tail;
- soot-brown opaque hide, charcoal mineral dust, dark-iron keratin, and
  restrained pale hinge/claw-root tissue;
- recognizable non-color identity at every LOD;
- grounded plant, cut, push, scurry, stop, vent, and recovery intent without
  required dust, smoke, debris, glow, or particles.

No implementation may turn the vent folds into ears, wings, fins, or horns;
split the shovel palm into fingers; add a third stabilizer; lengthen the tail
into a reptile or armadillo read; or replace the creature with a familiar
mole/anteater silhouette.

## Representative Slice

The first engineering slice contains only:

- one isolated `128 m × 128 m` Slagfall review cell;
- the eight approved natural prop families;
- one standard-adult Slagwhistle production prototype;
- full, medium, low, and distant/impostor presentation;
- one effects-off and reduced-motion path;
- one profiling scene excluded from production build settings;
- deterministic validators and test fixtures.

The review-cell dimension is a profiling unit, not a terrain-tile, streaming,
zone, navigation, encounter, or spawn boundary.

The slice excludes:

- Ore Gallery entry, Faultroad travel, navmesh, combat, hostility, AI,
  burrowing simulation, population, spawn density, loot, rewards, crafting,
  audio, quests, lore, saves, networking, or live-world placement;
- a boss, elite, settlement, mine architecture, or constructed quarry kit;
- production Addressable labels or a new package dependency before the
  owning catalog and packaging issues authorize them;
- cinematic-only textures, meshes, effects, or source images from Player
  packaging.

## Production Placement

The engineering PR should use these bounded locations:

```text
unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/
  Environment/
    Meshes/
    Materials/
    Textures/
    Prefabs/
  Fauna/Slagwhistle/
    Meshes/
    Materials/
    Textures/
    Animations/
    Prefabs/

unity/Assets/AL/Scenes/Prototype/Terrestrials/
  SlagfallQuarryRepresentativeSlice.unity

unity/Assets/AL/Tests/EditMode/Terrestrials/
unity/Assets/AL/Tests/PlayMode/Terrestrials/
```

Do not create a runtime catalog record until its owning catalog issue maps the
stable source IDs. The prototype may use a direct scene reference while it
remains excluded from Player build settings. Production packaging must later
replace that prototype binding without changing the approved source identity.

## Habitat Production Budget

The eight approved prop families are:

1. irregular fracture raft;
2. broken fracture raft;
3. undercut extraction ledge;
4. talus apron;
5. collapsed gallery mouth;
6. diagonal fault slab;
7. braided runoff pool;
8. iron-soil wedge.

| Measure | `low_mobile` | `balanced` | `high_pc` |
| --- | ---: | ---: | ---: |
| Unique prop families | exactly `8` | same eight, richer variants allowed | same eight, close variants allowed |
| Terrain/surface layers visible | `4` maximum | `6` maximum | `8` maximum |
| Unique Slagfall material families | `8` maximum | `12` maximum | `16` maximum |
| Ground/surface textures | one shared `2K` set | up to two shared `2K` sets | selective `4K` only after pixel-coverage proof |
| Dynamic lights | `0` | `0` by default | `1` shadowless maximum |
| Required particle systems | `0` | `0` | `0` |
| Required active-water families | `0` | `0` | `0` |
| Full-detail review cells | `1` | `2` | `4` |
| Neighbor proxy cells | `4` | `8` | `12` |

Unique compressed habitat content targets `6–8 MiB` and must not exceed
`12 MiB`. Kit variants reuse atlases, packed masks, collision grammar, and
LOD topology. They may not consume new materials merely to hide repetition.

Every large raft family needs at least three non-grid transformations or
mesh variants that change the visible perimeter, missing corners, talus/soil
intrusion, and undercut profile. Rotation alone is not sufficient when the
silhouette remains identical.

## Slagwhistle Production Budget

| Measure | Contract |
| --- | --- |
| LOD0 skinned triangles | target `8,000–10,000`; hard maximum `10,000` |
| Deform bones | target `34–42`; hard maximum `42` |
| Material slots | `1` preferred; `2` hard maximum |
| Texture set | one `1K` color, normal, and packed-mask set |
| Core animation clips | `6` maximum |
| Unique compressed content | target `3–4 MiB`; hard maximum `7 MiB` |
| LOD1 | `55–60%` of LOD0 silhouette cost |
| LOD2 | `20–25%` of LOD0 silhouette cost |
| Distant representation | `6–8%` or one authored opaque impostor |
| Required particles | `0` |
| Required dynamic lights | `0` |

The six-clip ceiling may group the seven approved motion moments:

1. rest, vent, and closed-yoke recovery;
2. primary low scurry;
3. plant and forward-loaded stop;
4. cut under a fractured edge;
5. backward spoil push with tail brace;
6. turn and neutral recovery.

These are presentation clips, not AI or gameplay states. Root motion,
burrowing displacement, damage, attack, death, or interaction behavior is not
authorized.

LOD reduction removes micro-scales, scar microdetail, distal secondary
controls, small wrinkle deformation, and close-only keratin damage first. It
must preserve the wedge skull, shoulder yoke, fused shovel palms, low body,
two stabilizers per forefoot, and flattened brace tail.

## Rendering And Load-Degradation Contract

The representative slice must integrate with the approved territory safety
boundary without altering global `QualitySettings` from a territory object.

- `low_mobile` targets `30 fps` (`33.33 ms`).
- `mobile_standard` targets `45 fps` (`22.22 ms`).
- desktop tiers target `60 fps` (`16.67 ms`).
- The worst of quality tier, registered player population, and sustained
  frame pressure determines presentation.
- A known 100-user congregation enters at least heavy degradation before it
  renders a normal-detail frame.
- All registered users remain represented through full, medium, low/static,
  or impostor tiers up to the 100-user contract.
- Slagwhistles do not increase the user count. Their later-authorized visible
  population must independently degrade animation and LOD under the same
  territory pressure.
- At critical load, Slagwhistle presentation may retain only low/static or
  distant representations; it may not preserve close animation at the cost
  of player representation.
- No quality tier may remove a required targetability, collision, route, or
  threat cue once later gameplay authority defines one.

The first engineering slice remains non-gameplay, so it validates rendering
and representation only.

## Memory, Packaging, And Streaming

| Measure | `low_mobile` | `balanced` | `high_pc` |
| --- | ---: | ---: | ---: |
| Combined Slagfall unique compressed target | `12 MiB` | `24 MiB` | `48 MiB` |
| Combined hard compressed ceiling | `19 MiB` | `48 MiB` | `96 MiB` |
| Incremental runtime-resident art target | `24 MiB` | `48 MiB` | `96 MiB` |
| Texture mip policy | retain at least one lower mip; global limit may reduce one level | full `1K/2K` authored set | selective higher mip only |

The low combined target is the approved habitat `8 MiB` target plus fauna
`4 MiB` target. The hard combined ceiling is the approved `12 MiB + 7 MiB`
source ceiling; exceeding it returns the asset to A1 review.

Streaming and recovery must:

- load the low identity before optional balanced/high additions;
- allow optional-tier cancellation without losing the low representation;
- release optional tiers after territory exit and prove a stable memory
  plateau over repeated enter/exit cycles;
- fail visibly and diagnostically when required low content is missing;
- fall only toward a cheaper authored representation;
- never promote a missing low or impostor asset back to an expensive tier;
- never pull concept PNGs, editable sources, or cinematic-only assets into
  the Player dependency graph.

## Failure Behavior

| Failure | Required result |
| --- | --- |
| Missing required habitat family | Block the representative slice and name the missing stable family ID |
| Missing Slagwhistle full/medium/low/impostor representation | Reject registration; do not silently cull an approved visible subject |
| Missing optional balanced/high content | Continue with low identity and emit one bounded diagnostic |
| Hash or source-version mismatch | Block fidelity acceptance and return to A2/A1 review |
| Streaming cancellation | Retain or restore the low representation; release incomplete optional content |
| Invalid LOD ordering | Fail validation before scene acceptance |
| Memory ceiling exceeded | Remove optional detail or density before protected silhouette |
| Frame-pressure threshold exceeded | Degrade presentation and effects before user representation |
| Repeated plate/tile read in graybox review | Reject habitat fidelity even when numeric budgets pass |

## Accessibility And Readability

- The quarry bowl, gallery mouths, fault direction, runoff, and traversable
  ground-value separation must remain readable in grayscale and effects-off.
- Slagwhistle identity must survive color removal through skull, yoke, shovel
  palms, body height, and tail.
- Reduced motion removes yoke flutter, idle micro-motion, particles, debris,
  and surface pulsing while preserving contact, direction, stop, and recovery.
- No required cue may depend on glow, wet specular response, particles,
  dynamic light, or a high-resolution texture.
- Validation captures are required at `96 px`, `64 px`, and `32 px` fauna
  silhouette height plus low/mobile, balanced, and high habitat distances.

## Automated Evidence Required

### EditMode

- exact source-version and approved-hash mapping;
- eight and only eight habitat family IDs;
- Slagwhistle topology, bone, material, texture, clip, and LOD ceilings;
- monotonic LOD and quality budgets;
- missing required representations fail closed;
- source PNGs have no runtime dependency path;
- prototype scene is excluded from enabled Player build settings;
- no required particles, dynamic lights, or active-water family;
- lower tiers preserve protected silhouette identifiers.

### PlayMode

- 100 synthetic registered users remain represented through twelve
  heavy/critical cycles;
- the Slagfall review cell coexists with the 100-user crowd without exceeding
  the active player representation caps;
- Slagwhistle presentation degrades without object-replacement storms;
- repeated review-cell enter/exit and optional-tier cancellation return to a
  stable object and memory count;
- critical load disables decorative work before required representation;
- authored low state recovers after pressure subsides;
- no unexpected logs, leaked test roots, or concept-sheet runtime references.

Machine-dependent FPS is not the primary CI assertion. Automated tests prove
deterministic caps and lifecycle behavior; device profiling proves timing.

## Representative-Device Evidence

Production acceptance requires retained captures from:

- one constrained Android GLES3 or Vulkan device that enters the existing
  `mobile_low` profile;
- one normal Android or iOS Metal device using the standard mobile profile;
- one desktop low profile;
- one desktop standard profile;
- an iOS build with deployment target `15.0`, plus physical iOS runtime
  evidence when compatible hardware is available.

Each device lane records:

- P50, P95, and worst CPU and GPU frame time;
- total and incremental resident memory;
- texture, mesh, animation, and instance-buffer residency;
- renderer, batch, SetPass, material, triangle, shadow, particle, and overdraw
  counts;
- cold load, warm load, cancellation, and territory-exit release time;
- first-five-minute versus final-five-minute performance in a `30` minute
  congregation run;
- thermal state, battery behavior where exposed, crash/ANR state, and build
  size delta.

The run must include the representative review cell, the maximum
later-authorized Slagwhistle presentation pool, and 100 registered synthetic
users. No networking capacity claim may be made until a multiplayer transport
and server budget exist.

## 90/100 Safety Gate

The user requires at least `90 / 100`. Production scoring is evidence-based:

| Dimension | Points |
| --- | ---: |
| Approved-source fidelity and non-color identity | `20` |
| Asset separation, packaging, and dependency hygiene | `15` |
| LOD, rendering, and 100-user degradation behavior | `20` |
| Streaming, cancellation, fallback, and recovery | `15` |
| Representative-device frame, memory, thermal, and stability evidence | `20` |
| Effects-off, reduced-motion, and distance readability | `10` |
| **Total** | **`100`** |

Automatic failure applies regardless of score when:

- the exact approved source version or hash is lost;
- a concept sheet enters the Player dependency graph;
- any registered user within the 100-user contract becomes unrepresented;
- a missing cheap tier promotes to a more expensive representation;
- required navigation, targetability, collision, threat, or interaction
  information differs by quality tier;
- memory grows across repeated enter/exit cycles without returning to a
  bounded plateau;
- the representative device run crashes, hangs, produces an ANR, or shows
  unrecovered thermal degradation;
- production habitat recreates the prohibited paving, stair, masonry, road,
  or volcanic-landmark read;
- Slagwhistle anatomy violates the locked yoke, shovel, stabilizer, or tail
  structure.

The existing `95 / 100` A2 visual verdict satisfies the user's creative-source
gate. It is not a production safety score. Production remains unscored until
the representative slice supplies all six evidence dimensions.

## Review And Stop Conditions

Engineering stops and returns to:

- A2 when silhouette, anatomy, material meaning, motion meaning, habitat
  grammar, or approved pixels must change;
- A1 when budgets, packaging, lifecycle, failure behavior, catalog mapping,
  or device criteria must change;
- narrative/content when a working label becomes player-facing;
- the user when protected identity would need weakening or the safety score
  cannot reach `90 / 100` without a creative tradeoff.

Passing this handoff authorizes one representative engineering slice only.
Scaling Slagfall into the live world, adding fauna population authority, or
replicating the approach across the remaining ecosystem roster requires
separate measured acceptance.
