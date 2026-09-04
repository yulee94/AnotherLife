# Deterministic multi-cell battle harness

Status: single-process, host-local validation workload. It is not a production
server, a network load test, or evidence of supported player concurrency.

## Question this harness answers

`multicell_battle_harness` asks whether the current safe Rust reference
primitives remain deterministic and bounded when 10,000 simple active entities
are spread over multiple authoritative cells and the workload also performs
interest selection, fidelity degradation, ghost publication, ownership
handoffs, explicit encoding, and queue backpressure.

It measures where this reference implementation spends time and makes overload
visible. A missed deadline is a failed synthetic tick deadline; logical tick
time still advances by exactly one and is never slowed. Passing a host-local
deadline would still not establish production capacity.

## Default deterministic workload

- 10,000 active integer-position entities across a 4 by 4 lattice of
  authoritative microcells;
- 120 fixed ticks at an illustrative 30 Hz / 33,333,333 ns budget;
- one deterministically shuffled velocity intent per resident per tick;
- all 10,000 entities evaluated as observers once every six ticks, staggered
  across tick shards, for 200,000 observer evaluations over four logical
  seconds;
- 96-unit engaged and 384-unit awareness radii plus one deterministic distant
  causal promotion per observer;
- remaining entities represented through per-cell mass aggregates;
- 16-byte engaged, 12-byte awareness, and 12-byte mass records behind a 16-byte
  test page header, split into payloads no larger than 1,200 bytes;
- a 3,072-byte per-observer snapshot budget that preserves engaged records and
  mass coverage before shedding awareness records;
- 64-unit border-ghost margins, explicit 32-byte ghost records, and the existing
  80-byte authenticated inter-cell frame header;
- four balanced handoff waves, each moving 64 entities in opposite directions
  across paired horizontal borders with prepare, ready, and exact-tick commit;
- a 96-frame / 115,200-byte inter-cell queue draining at most 80 frames per tick.

The queue preserves handoff control by rejecting or evicting expendable ghost
work first. The default workload intentionally produces more ghost work during
bursts than the bounded queue accepts, so the artifact reports deterministic
ghost shedding. A unit test separately proves that an already-queued ghost is
evicted before an incoming control frame is rejected.

## Authority and handoff behavior

Every entity has one `OwnershipLease` containing its ID, owner cell, and
monotonic epoch. At a committed handoff tick, the harness:

1. asks the source microcell to `despawn` the entity;
2. remaps the synthetic border position inside the destination geometry;
3. spawns exactly one destination resident;
4. publishes the state machine's next lease with epoch incremented once; and
5. checks that the source has no resident, the destination has one, every entity
   is globally present, and each resident agrees with the sole-writer table.

The reference state machine is driven in process. Its control frames are encoded,
queued, decoded, scoped, and deadline-checked, but receipt of those queued bytes
does not drive the state transition. Therefore this exercises state migration,
codec, and backpressure invariants, not distributed consensus, transport loss,
crash recovery, or a remote handoff protocol.

## Interest and fidelity behavior

Observers are first grouped into deterministic 256-by-256-unit spatial cohorts.
Each cohort reuses a conservative list of authoritative cells whose bounds can
intersect any awareness circle originating inside that cohort. Exact
microcell-radius queries still perform the final circle test, so the hierarchy
removes provably irrelevant cell calls without changing membership. Outcomes
are restored to observer-ID order before metrics and checksums are mixed.

For each observer, the selected microcell radius-query results are merged and
classified:

- `engaged`: exact distance within the engaged radius plus a causal target that
  is promoted even when distant;
- `awareness`: exact distance inside the awareness radius but outside engaged;
- `mass`: counts not sent individually, conserved in per-cell faction aggregates.

The output reports desired and sent awareness counts separately. When the
snapshot byte ceiling is reached, awareness records are shed and those entities
remain counted in mass aggregates. Engaged records are never silently dropped;
an `engaged_over_budget_observers` counter instead marks cases requiring
admission or a product-level rule.

This is one combined five-Hz reference snapshot per observer. It does not yet
implement distinct production update frequencies, occlusion, stealth,
authorization, squads, abilities, targets selected by real gameplay, shared
snapshot-page reuse, delta baselines, acknowledgements, or client prediction.
The compiler reuses caller-owned entity, faction-count, mass-record, and
1,200-byte page buffers. The compact format is a test codec, not a published
client protocol. Its 32-bit handles assume a separately authenticated session
mapping to authoritative 64-bit entity IDs.

## Wire and byte accounting

Every compact page and inter-cell payload is encoded and decoded field by field
in explicit little-endian order. The example crate forbids unsafe Rust. It does
not cast structures, depend on Rust layout, or accept truncation/trailing bytes.

`client_sent_bytes` and its projected rate count only encoded test-page bytes.
They exclude QUIC/UDP/IP framing, AEAD tags, acknowledgements, retransmission,
congestion, pacing, FEC, voice/chat, asset traffic, login/control messages, and
private overlays. `delivered_intercell_bytes` includes the current 80-byte core
frame header and synthetic payload but still represents an in-memory queue, not
packets delivered by a kernel or NIC. Projected rates are arithmetic over the
four logical seconds, not measured network throughput.

## Run and archive

From the repository root:

```sh
cargo run --quiet --manifest-path server/Cargo.toml --release \
  -p al_server_core --example multicell_battle_harness \
  | tee archive/local-run/server/multicell-battle-harness-cohort-v2.jsonl \
  | jq -ce .
```

Standard output is two JSON Lines records: deterministic configuration followed
by metrics. Candidate/record/byte/drop/handoff counts and
`workload_checksum` must repeat for identical source and inputs. Wall-clock
timings may change with compiler, host, power, thermal state, and contention.

The scenario record includes:

- total/mean/p50/p95/p99/max tick time and deadline misses;
- simulation, interest, ghost-wire, and queue phase timings;
- cohort cell-query and broadphase-candidate counts plus
  engaged/awareness/mass representation counts;
- desired/sent bytes, datagrams, shedding, per-observer byte percentiles, and
  illustrative projected payload rates;
- ghost records/frames, generated and delivered bytes, queue high-water marks,
  drops, evictions, expiry, and rejected controls;
- started/committed/aborted handoffs plus ownership and ghost oracle counts; and
- a timing-independent checksum over deterministic observations, decoded
  payloads, final positions, owners, epochs, and queue outcomes.

## Latest local evidence (2026-08-25)

Host: Apple M5 Max, 18 physical cores, 128 GiB memory, macOS 26.5.2 / Darwin
25.5.0, Rust 1.98.0, arm64 release profile with thin LTO, one codegen unit, and
overflow checks enabled. These are short foreground laptop launches, not an
isolated or sustained production-server benchmark.

The recorded before/after artifacts are:

| Workload | Artifact | SHA-256 |
| --- | --- | --- |
| Flat all-cell baseline | `archive/local-run/server/multicell-battle-harness-final.jsonl` | `fc26d35cad105da0676a6111c093e03f68aff496e3232b36f10ead3c4b4ef4a9` |
| Cohort v2 primary | `archive/local-run/server/multicell-battle-harness-cohort-v2.jsonl` | `05bdbee8c2fd166831cad97224309a0fa6e37043dea4b17d8ae1e7bc73e3d36b` |
| Cohort v2 immediate repeat | `archive/local-run/server/multicell-battle-harness-cohort-v2-repeat.jsonl` | `1d6a14c2d226282b671cec0c0d1c54b114f2a2e5935ab6c7ebc6b45621473be8` |
| Cohort v2 preliminary launch | `archive/local-run/server/multicell-battle-harness-cohort-v2-preliminary.jsonl` | `87fac0c3b93e72ef7c2e477a381e5aad36ebb472e2460b52ad945f612121a4a0` |
| Cohort v2 final validation | `archive/local-run/server/multicell-battle-harness-cohort-v2-validation.jsonl` | `155b62ed4d31e1a91ca5aeb9e23978159b52378d022d813bc599f5f0181e9cf6` |

Primary before/after timing comparison:

| Measure | Flat baseline | Cohort v2 | Change |
| --- | ---: | ---: | ---: |
| Interest cell queries | 3,200,000 implicit calls | 609,194 calls | -80.963% |
| Interest mean | 34.954 ms | 30.424 ms | -12.958% |
| Interest p99 | 36.256 ms | 30.882 ms | -14.822% |
| Full tick mean | 35.538 ms | 31.008 ms | -12.748% |
| Full tick p99 | 36.841 ms | 31.463 ms | -14.599% |
| Full tick max | 37.598 ms | 31.507 ms | n/a |
| 33.333 ms deadline misses | 120 / 120 | 0 / 120 | n/a |

The repeat also recorded zero misses, a 31.606 ms tick p99, and a 31.613 ms
maximum. However, the preliminary v2 launch recorded two misses, and the fresh
final validation recorded seven misses, a 34.965 ms p99, and a 35.048 ms
maximum. Even the two clean launches retained only about 1.7--1.8 ms of
maximum-tick headroom. Consequently, this does **not** establish a reliable
30 Hz gate, much less 10,000-player battle capacity.

The optimization did not alter the synthetic workload's meaning. The optimized
run examined the same 74,994,119 broadphase candidates, produced the same
engaged/awareness/mass records and bytes, committed the same 256 handoffs,
preserved the same bounded ghost/control outcomes, and retained checksum
`ab8c9edcc9782870`. A machine comparison deleted only wall-clock/deadline fields
and the new cell-query counter; every remaining scenario field compared equal
to the baseline. The two optimized launches also compared equal after deleting
only wall-clock/deadline fields.

The result validates the hierarchy as a useful local optimization and confirms
that interest/page construction remains the dominant reference cost. It does
not validate network fanout, combat cost, horizontal scaling, client rendering,
or a production concurrency target.

## Correctness coverage

The harness and core tests verify:

- fixed-tick entity conservation and exactly one applied intent per resident;
- safe despawn, sorted SoA storage, and lazy spatial-index rebuild;
- global unique residency, owner-cell agreement, and epoch-incremented cutover;
- direct neighbor ghost routing against an independent all-cell border oracle;
- conservative cohort-cell selection against a global brute-force radius oracle,
  plus every small-scenario observer compared with the retained flat-cell
  baseline for equal deterministic metrics, page checksum, and byte samples;
- compact, ghost, and handoff payload round trips plus truncation, trailing-byte,
  invalid-version/opcode, scope, and deadline rejection through the core codec;
- exact tier conservation: engaged + sent awareness + mass equals every
  non-observer entity;
- byte-budget arithmetic equals bytes actually encoded;
- bounded queue control priority and zero partial payload acceptance; and
- repeated small multi-cell runs produce equal deterministic metrics/checksum.

## What this does not prove

The 10,000 entities are simple movers, not 10,000 mutually interacting players.
There is no combat, abilities, projectiles, buffs, AI, pathfinding, collision,
terrain, line of sight, objectives, inventory, persistence, scripting, or
adversarial input. The process has no gateway, socket, QUIC implementation,
encryption, loss/jitter/reordering, slow clients, multi-process workers, shared
memory, NUMA scheduling, orchestrator, database, cache, event system, or failure
injection. There is no Unity client, rendering, animation, asset streaming, or
end-user frame measurement.

The reference still performs exact selection and bespoke compact-page encoding
for every observer, synchronously decodes its own outbound test pages as an
oracle, and has no delta or shared-page reuse between nearby observers. Further
optimization must retain the oracle and checksum behavior, then be tested under
real gameplay traces. Required next gates remain real headless combat bots,
shared snapshot compilation, real transport/fanout, multi-process handoff fault
injection, loss/reorder/reconnect tests, cache/memory/CPU telemetry, sustained
soak on named server hardware, and paired Unity-client measurements.
