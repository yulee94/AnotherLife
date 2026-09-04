# AnotherLife Execution Lanes

Status: active delivery map. The working agreement in the repository root is the
only operating authority; older mode, branch-prefix, lock, and phase-governance
documents are historical context.

## Shipping rule

The integration spine is always a runnable game. Infrastructure, art, tools, and
scale experiments may advance in parallel, but none may make the current player
loop unbuildable, depend on generated-world hand edits, duplicate catalog
authority, or break an old save.

The current slice is:

```text
launch -> realm choice/profile -> champion world entry
       -> traverse real terrain with collision and MMO camera
       -> interact/fight -> receive deterministic result
       -> save -> quit -> relaunch -> continue
```

Every milestone must be executable from the normal player entry point. A design
document, isolated benchmark, imported model, passing unit test, or Editor-only
scene is supporting evidence, not a playable milestone by itself.

## Parallel lanes

| Lane | Near-term deliverable | Production direction | Required proof |
| --- | --- | --- | --- |
| Playable game | Stable terrain, champion, physics/collision, MMO camera, interaction and one combat loop | Quest, party roles, kingdom transition, warzone and RvR objectives | Automated gameplay tests plus recorded real-player traversal and graceful save/relaunch |
| World authoring | Three neighboring catalogued chunks, bounds/neighbor overlay, placement tools, validation and play-from-here | Additive streaming, per-chunk nav, LOD/HLOD, probes, minimap and deterministic bake farm | Round-trip authoring test; no edits under generated-world authority; cold/warm traversal without holes |
| Art and Blender | Source validator, export/promotion manifest, one technically valid terrain/architecture/character set | Realm-consistent modular kits, characters/NPCs, LODs, rigs, animation/VAT and impostors | Source + imported asset budgets, visual review, collision/nav/socket checks and deterministic exports |
| Character and combat | Controller/camera contracts, target dummy or enemy, damage/death/recovery receipt | Server-authoritative skills, projectiles, buffs, support contribution and objective combat | Deterministic replay, invalid-command tests, old-save coverage and playable feedback |
| AI and navigation | Ground query plus one NPC patrol/chase/return loop | Per-chunk navigation, crowd steering, encounter ownership and scalable behavior scheduling | No unreachable spawn, bounded path budget, seam traversal and deterministic fallback |
| Designer tooling | Catalog browser, spawn/route/encounter placement and validate/bake report | Unified World Authoring workspace over specialized pipelines | Undo/stable IDs, schema round trip, dirty-scene safety and batch validation |
| Unity scale client | Profile current GameObject slice; define remote-crowd data boundary | Hybrid GameObjects plus Jobs/Burst/Entities after URP/SRP decision; GPU bone textures/VAT/impostors by tier | Minimum-device frame p99, zero steady-state hot-loop allocations, VRAM/upload/draw budgets |
| Rust simulation | Safe protocol primitives, fixed-tick microcell, ownership epoch, ghosts and deterministic handoff | Stable gateway plus NUMA-aware simulation fabric with dynamic fine-cell placement | Golden codec fixtures, fuzzing, replay across thread counts, hotspot benchmarks and handoff fault injection |
| Network edge | Loopback transport, then one regional QUIC gateway | Connection-ID-aware regional gateways; paced streams/datagrams; XDP only if measured | Loss/reorder/rebind/MTU tests, bounded queues and p99 CPU/latency comparisons |
| Persistence | Current local saves and migrations; SQL domain boundary | One SQL authority: CloudNativePG first, TiDB only if measured replacement is justified | Old-save load, idempotency, failover/PITR and no database call in combat ticks |
| Cache/events/objects | Interfaces and failure semantics, no premature deployment | Valkey or Dragonfly disposable cache; outbox then optional Pulsar; S3/SeaweedFS immutable objects | Complete cache loss, duplicate event, backlog and object restore tests cannot corrupt player value |
| Security | Server-authority contract, bounded inputs, signed catalog plan | Layered admission, replay/rate checks, service identity, audit and optional measured Rust client kernels | Hostile parser/property tests, key rotation, build/content verification and false-positive review |
| Delivery/operations | Reproducible Unity tests/build and evidence bundle | CI matrices, signed artifacts, regional observability, capacity/failure drills and release rollback | Required CI green, exact artifacts/logs, restore drill and no fabricated acceptance |

## Dependency order

### M0 — Offline playable foundation (current)

- Real Unity Terrain and `TerrainCollider`, safe spawn, boundaries and traversal.
- One `CharacterController`, grounded movement, camera-relative input and
  conventional MMO camera controls.
- A visible character, interactable objective/enemy, deterministic gameplay
  receipt, save and relaunch continuity.
- Focused and full PlayMode validation, macOS player build, screen-recorded
  traversal, runtime-log inspection.

Exit only when a player can launch and play without using the Unity Editor.

### M1 — Repeatable authored vertical slice

- Three connected world chunks and the first World Authoring tools.
- Approved technical Blender sources promoted through manifests into prefabs.
- Nav/pathfinding, one NPC loop, one combat encounter and one quest objective.
- Performance budgets captured on representative low/mid/high client tiers.

Exit when another designer can alter approved catalog data/assets, validate,
bake, build, and play the change without editing runtime code.

### M2 — Local authoritative multiplayer seam

- Rust fixed-tick server runs locally and owns movement/combat outcome.
- Unity predicts the local champion, interpolates remote actors and reconciles.
- Versioned Protobuf control messages and a safe measured snapshot codec share
  golden fixtures; loss/reorder/replay tests are automated.
- A single SQL authority persists committed value through an outbox boundary.

Exit with two real clients plus bots completing the vertical slice through
disconnect/reconnect without duplicate ownership, rewards, or saves.

### M3 — Regional scale foundation

- Stable QUIC gateway, fixed fine cells, dynamic worker assignment, border ghosts
  and epoch handoff.
- Hybrid remote-crowd client path and explicit engaged/awareness/mass fidelity.
- Load/failure harness grows through 100, 500 and 1,000 causally interactive
  actors before multi-worker expansion.

Exit only when deterministic replay, failure injection and tick/frame p99 remain
inside recorded budgets with headroom.

### M4 — Multi-worker RvR

- Interaction-graph-aware cell placement and combat-island affinity.
- Handoff failure recovery, shared snapshot compilation and regional admission.
- Optional cache, event backbone, object store, eBPF/XDP, shared-memory middleware,
  or TiDB enter only through a benchmark and operations decision record.

Exit after sustained moving-hotspot and single-hotspot tests at 2,000–4,000 real
or equivalently expensive simulated actors, including worker loss at every
handoff phase.

### M5 — Ten-thousand representation experiment

- Ten thousand connected/represented, individually replicated, and causally
  interactive counts are reported separately.
- Full-fidelity engaged sets, lower-rate awareness, aggregates/impostors for mass,
  and promotion before material interaction are tested on real clients.
- Millions of global connections are validated as a separate fleet/control-plane
  exercise; they are never used as evidence for one battle's causal capacity.

No “10,000 full-fidelity battle” claim ships unless real-human-equivalent input,
network, simulation, rendering, persistence, fault, and one-hour soak gates all
pass without time dilation.

Current synthetic evidence does not advance that exit gate. The Rust
[`multicell battle harness`](../../server/MULTICELL_BATTLE_HARNESS.md) measures
10,000 simple active movers, staggered tier selection, test-payload fanout,
border ghosts, in-process handoffs, fixed deadlines, and bounded shedding. It
reports individually sent and aggregate representation separately and must be
read as a regression/degeneration workload, not a production battle. Likewise,
the native navigation [`Unity integration gate`](../../native/UNITY_INTEGRATION_GATE.md)
remains closed until an end-to-end Burst/Unity comparison shows a material
advantage across supported platforms.

## Parallel integration rules

- One lane owns a file area while working; the integrator resolves shared seams.
- New game authority begins in schemas/catalogs and has strict loaders/tests.
- Generated assets/scenes remain generated; source tools and inputs are edited.
- Performance work starts with a reference implementation and representative
  trace. An optimized Rust/DOTS/GPU/XDP path must remain behaviorally compared to
  that reference until proven.
- Queues, memory, downloads, spawns, visibility, physics, pathfinding, simulation,
  packets, logs, retries, and persistence all have explicit bounds and overload
  behavior.
- Creative appearance, balance, product promises, render-pipeline migration and
  release acceptance remain user decisions. Technical validation may identify
  choices; it does not silently make them.

## Evidence packet per integration

Every merged slice records the exact source revision, catalog/schema versions,
commands, test totals, build artifact, runtime logs, player-recording or profiling
artifact where relevant, known pre-existing failures, performance deltas, save
compatibility result, and any user-owned decision still open.
