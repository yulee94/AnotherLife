# Authoritative microcell capacity harness

Status: deterministic local diagnostic for the engine-free reference
microcell. It is deliberately not a production load test or a capacity claim.

## Purpose

`authoritative_capacity_harness` makes one important failure mode measurable:
a uniform spatial grid is efficient when occupancy is spread out, but its
broadphase still degenerates toward a full scan when every relevant entity
occupies one bucket. The harness also exercises the reference microcell's fixed
tick, immutable-intent reduction, atomic SoA state commit, index rebuild, exact
radius query, canonical result ordering, and bounded query-output contract.

The default deterministic workload uses:

- 10,000 resident entities and exactly one velocity intent per entity per tick;
- 30 untimed warmup ticks and 120 measured ticks;
- eight radius queries per measured tick, for 960 timed queries per scenario;
- a 256 by 256 uniform grid with 64-unit cells and a 128-unit query radius;
- a caller-owned result capacity of 256 entity IDs;
- seed `0x414c5f4341505f31`;
- three distributions run in a stable order:
  - `spread`: deterministic positions across the interior of the grid;
  - `boundary_dense`: entities distributed over a 16 by 16 group of internal
    grid boundaries, oscillating one unit across those boundaries every tick;
  - `single_hotspot`: all entities remain in one bucket and oscillate one unit.

Forward and reverse intent arrays are created and deterministically shuffled
before measurement, so the reduction does not receive the already-canonical
entity-ID order. Every entity returns to its starting position after two ticks,
preventing long-run drift. The state-update timer covers intent
copy/sort/reduction, integer integration, next-index construction, and atomic
state/index commit. Query construction and result inspection are timed
separately.

## Run and validate

Use an optimized build. Debug results primarily measure debug checks and are
not comparable with release results.

```sh
cargo run --manifest-path server/Cargo.toml --release \
  -p al_server_core --example authoritative_capacity_harness
```

Optional parameters are `--entities`, `--warmup-ticks`, `--measured-ticks`,
`--queries-per-tick`, `--query-radius`, `--result-capacity`, and `--seed`.
Seeds accept decimal or `0x` hexadecimal notation. Entity count, tick count,
query count, and result capacity have explicit safety ceilings. Run `--help`
for the compact invocation summary.

The program writes one metadata object followed by one object per scenario as
JSON Lines. Validate or archive stdout separately from Cargo's stderr, for
example:

```sh
cargo run --quiet --manifest-path server/Cargo.toml --release \
  -p al_server_core --example authoritative_capacity_harness \
  | python3 -c 'import json,sys; [json.loads(line) for line in sys.stdin]'
```

For useful comparisons, record the source revision, Rust version, release
profile, operating system, exact CPU and memory topology, power mode, thermal
state, and competing workload. Repeat multiple process launches on an otherwise
idle target host; do not compare a laptop burst result directly with a sustained
server result.

## Output contract

The metadata record captures the deterministic input configuration and whether
debug assertions are enabled. Each scenario record contains:

- total, mean, p50, p95, p99, and maximum nanoseconds for state updates;
- the same timing fields for individual radius queries;
- total, mean, and maximum broadphase candidates and exact matches;
- query count, configured result capacity, overflow count, and maximum exact
  capacity required by an overflowing query;
- `overflow_output_cleared=true`, which means every observed overflow returned
  its exact required capacity and exposed no partial entity-ID result;
- two untimed brute-force oracle comparisons, one before and one after the
  workload; and
- a timing-independent workload checksum over ticks, query observations,
  successful bounded results, and final entity state.

Candidate counts, match counts, overflow counts, and workload checksums must be
identical for the same executable inputs. Timings are observations and are not
expected to be identical. A checksum change after a code change requires
correctness review; checksum stability alone does not prove correctness.

The crate's unit tests additionally compare randomized and moving-boundary
queries with a brute-force oracle. Harness-specific tests rerun smaller versions
of all three scenarios, verify deterministic checksums/counts, verify exact
single-hotspot overflow, and exercise CLI bounds and JSON-Line construction.

## What this does not prove

The headline entity number is resident state inside one synthetic,
single-threaded, in-process reference microcell. It is not 10,000 fully
interactive players. The harness contains no:

- combat, abilities, projectiles, buffs, AI, navigation, collision, terrain,
  line-of-sight, objectives, inventory, scripts, or persistence;
- gateway, socket, QUIC, packet codec, encryption, retransmission, congestion,
  interest-set fanout, per-client serialization, bandwidth, or slow-client
  backpressure;
- cross-cell ghost exchange, ownership handoff, worker coordination, failure
  injection, process isolation, shared memory, NUMA placement, or parallel job
  scheduling;
- Unity client, DOTS, animation, rendering, asset streaming, input, or end-user
  frame-time measurement; or
- sustained soak, memory/allocator profiling, cache-miss counters, production
  telemetry, hostile traffic, or representative gameplay traces.

Consequently, even a sub-millisecond synthetic state update does **not** prove
a 30 Hz or 60 Hz production server tick, 10,000-player battle capacity, network
capacity, one-million-player concurrency, or client frame rate. The timer also
does not include the complete tick wall clock or safety headroom. Default query
timings use a 256-ID output ceiling: an overflowing query still scans and counts
all exact matches, but it neither materializes nor sorts all 10,000 IDs and it
does not serialize or fan them out. The harness's role is to catch regressions
in this narrow reference primitive, quantify candidate-set degeneration, and
guide the next representative benchmark.

## Required next gates

Before making an RvR capacity statement, add deterministic headless gameplay
bots and representative combat/ability traces; full relevance and snapshot
fanout; loopback and real-network transports with loss, jitter, duplication and
reordering; multi-cell boundary traffic and crash-injected handoffs; CPU, cache,
memory, queue, packet, and bandwidth telemetry; bounded overload/degradation;
and sustained p50/p95/p99/max results on named production-class hardware.
Server evidence must then be paired with an independently measured Unity client
hardware matrix. Simulation time must remain fixed; fidelity and admission may
degrade only through explicit product rules.

The follow-on [`MULTICELL_BATTLE_HARNESS.md`](MULTICELL_BATTLE_HARNESS.md)
now supplies host-local synthetic multi-cell, fidelity, wire-budget, handoff,
deadline, and overload evidence. It does not satisfy the real transport,
gameplay, failure, soak, or client gates listed above.
