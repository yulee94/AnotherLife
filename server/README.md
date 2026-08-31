# AnotherLife authoritative server foundation

Status: engine-free protocol and state-machine prototype; not connected to
Unity, the Internet, persistence, or production orchestration.

This workspace isolates authoritative-server primitives from `native/`, whose
libraries cross the Unity FFI boundary. `al_server_core` currently provides:

- an explicit little-endian, version-one inter-cell frame with caller-buffer
  encoding, explicit nonzero world/instance scope, a distinct directed-route
  generation, an inclusive expiry tick, and borrowed allocation-free decoding;
- strict magic, version, header, kind, flag, payload-length, identity, truncation,
  trailing-byte, route-context, expiry, and configurable/absolute size validation;
- single-writer `(entity, owner cell, ownership epoch)` fencing;
- read-only ghost authority, age, future-tick, duplicate, and reordering rules;
- a bounded deterministic handoff with an inclusive ready deadline, exact
  cutover tick, idempotent ready acknowledgement, epoch increment, and no phase
  that exposes two writers;
- a deterministic integer fixed-tick microcell reference with SoA component
  arrays, bounded immutable-intent reduction, atomic next-state commit, and a
  bounded uniform-grid radius-query index with canonical entity ordering;
- provider-neutral identity, placement, deployment, platform, capacity, retry,
  failure, and observability contracts, plus executable persistence, simulation,
  social, economy, security/abuse, and observability ports, with no provider SDK
  types or implementations; and
- a separate disposable `al_provider_adapter_stub` crate proving that adapter
  code can depend on the core contract without the core depending on an adapter;
  and
- a synthetic-only `al_provider_adapter_playfab_spike` crate mapping the neutral
  placement, deployment, capacity, failure, and observation contracts to an
  injected PlayFab MPS boundary, with no concrete credential/network transport
  and no runtime operationalization path.

The crate forbids unsafe Rust. The codec never casts a byte buffer to a packed
structure and never relies on host endianness or Rust ABI layout.

There is no public unscoped decoder. Each decode requires a `ReceiveContext`
derived from an authenticated inter-cell peer/directory binding. The encoded
world, running instance, source cell, destination cell, and directed-route epoch
must match that context exactly; stale and future route epochs reject separately.
The receiver also supplies its authoritative fixed tick, and a frame is rejected
after its inclusive deadline. Therefore a syntactically valid frame delivered to
another instance, cell route, or expired queue cannot expose an accepted payload.

## Boundary decision

This slice deliberately does not select or deploy QUIC, Tokio, Kubernetes,
Cilium/XDP, Pulsar/NATS/Kafka, Dragonfly/Valkey, TiDB/PostgreSQL, or object
storage. Those belong behind later measured adapters:

| Future boundary | Contract required before implementation |
| --- | --- |
| External/client transport | Authenticated session, congestion/backpressure, expiry, baseline recovery, MTU, replay, and version negotiation |
| Inter-cell transport | Bounded delivery queues, authenticated peers, route generations, duplicate/reorder policy, and backpressure |
| Cell directory/leases | Single writer, monotonic epoch fencing, failure detector assumptions, recovery, and split-brain tests |
| Durable persistence | Idempotent asynchronous intents/checkpoints; no database read or write in movement/combat ticks |
| Event/log pipeline | Non-causal telemetry, audit, and replay only; broker availability must not stall an active tick |
| Orchestrator | Process placement and lifecycle only; never the per-tick ownership protocol or packet router |

The active simulation should keep canonical hot state in memory. TiDB or
PostgreSQL may later own durable transactional state, Dragonfly or Valkey may
hold reconstructible cache/presence, and object storage may hold immutable
snapshots and artifacts. None is allowed to become a synchronous combat-loop
dependency. A separate decision and representative benchmark must justify each
adapter.

## Validate

From the repository root:

```sh
python tools/architecture/validate_mmo_contracts.py .
python -m unittest discover -s tools/architecture -p 'test_*.py'
python tools/architecture/validate_mmo_bakeoff_plan.py . --record evidence/microsoft_playfab/<run-id>/run-record.json
cargo fmt --manifest-path server/Cargo.toml --all -- --check
cargo clippy --manifest-path server/Cargo.toml --workspace --all-targets -- -D warnings
cargo test --manifest-path server/Cargo.toml --workspace --all-targets
cargo doc --manifest-path server/Cargo.toml --workspace --no-deps
cargo build --manifest-path server/Cargo.toml --workspace --release
cargo run --manifest-path server/Cargo.toml --release -p al_server_core --example codec_throughput -- 1000000
cargo run --manifest-path server/Cargo.toml --release -p al_server_core --example spatial_grid_candidates -- 5000 1000
cargo run --manifest-path server/Cargo.toml --release -p al_server_core --example authoritative_capacity_harness
cargo run --manifest-path server/Cargo.toml --release -p al_server_core --example multicell_battle_harness
```

The examples are local primitive benchmarks, not evidence of networking,
complete-game simulation, rendering, concurrency, or battle capacity. The
spatial benchmark reports spread and single-hotspot candidate counts specifically
so dense-grid degeneration remains visible. Production gates still require
fuzzing, cross-platform golden fixtures, hostile-input load, deterministic
replay, packet loss/reordering, handoff crash injection, and representative
workloads.

The more comprehensive `authoritative_capacity_harness` emits JSON Lines for
deterministic spread, boundary-dense, and single-hotspot workloads. Its default
run uses 10,000 entities and reports state-update timings, query timings,
candidate and exact-match counts, and fail-closed result-buffer overflow. Read
[`CAPACITY_HARNESS.md`](CAPACITY_HARNESS.md) before interpreting any number.

`multicell_battle_harness` adds deterministic multi-cell ownership migration,
tiered interest/fidelity, conservative observer-cohort cell filtering, reusable
compact test-page construction, fanout accounting, border ghosts, explicit
handoff payloads, fixed tick deadlines, and bounded overload behavior. It still
represents synthetic state in one process. Its exact evidence boundary and
output contract are documented in
[`MULTICELL_BATTLE_HARNESS.md`](MULTICELL_BATTLE_HARNESS.md).
