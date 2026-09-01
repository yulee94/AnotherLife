# AnotherLife native foundation

Status: additive benchmark foundation; not connected to Unity or gameplay authority.

`al_nav_kernel` is the first bounded native-compute experiment. It exposes a
versioned C ABI for deterministic weighted-grid path batches while keeping every
grid, query, result, output point, and scratch allocation owned by the caller.
There are no process-wide handles, mutable globals, catalog copies, save fields,
or embedded gameplay values.

## Responsibility boundary

| Stays in Unity/C# | Candidate native work after measurement and review |
| --- | --- |
| Scene and lifecycle ownership, input, cameras, physics, animation, presentation, UI, and accessibility | Large pure-compute batches with stable byte/struct inputs |
| Catalog loading, schema validation, designer-visible IDs, tuning, and migration | Deterministic path queries and offline navigation validation |
| Gameplay rules, quest/economy state, save authority, and network orchestration | Catalog-derived bake analysis and bounded procedural-data generation |
| Unity NavMesh integration and movement application | Server-side reuse of the same deterministic validation kernels |
| Server decisions and anti-cheat response policy | Hashing/integrity primitives and spatial interest-query math after separate threat/performance reviews |

Rust does not make an offline client authoritative or impossible to reverse.
Client binaries and client-held secrets remain observable to a sufficiently
privileged adversary. Native code is appropriate here only when measurement,
memory safety, deterministic reuse, or platform isolation justify its build and
support cost.

## Current ABI

The public header is
[`al_nav_kernel.h`](al_nav_kernel/include/al_nav_kernel.h). ABI v1 uses only C
fixed-width integers, `size_t`, and pointers:

- `al_nav_abi_version_v1` returns `0x00010000`.
- Published v1 function signatures, structure layouts, and numeric status values
  are immutable. An incompatible change requires new suffixed symbols and a new
  ABI value; callers must compare the runtime value before submitting work.
- `al_nav_scratch_words_v1` reports exactly four `uint32_t` words per grid cell.
- `al_nav_find_paths_v1` validates one immutable weighted grid and processes a
  caller-provided query array in order.
- A cell value of `0` is blocked; `1..255` is the integer cost to enter it.
- Movement is four-way. Neighbor order and heap tie-breaking are fixed, so equal
  inputs produce an equal selected path and cost without floating-point math.
- Every successful path includes both start and goal. The start-cell cost is not
  charged; each subsequently entered cell is charged once.
- Common structural failures are returned by the function. A structurally valid
  batch returns `AL_NAV_STATUS_OK`; each query result then reports success,
  blocked/out-of-bounds/no-path, unsupported flags, cost overflow, or insufficient
  point capacity independently.
- An undersized point buffer never receives a partial path. The affected result
  reports the required `point_count`; later queries may still use remaining
  capacity. `out_points_written` counts only complete paths.
- All pointers with nonzero lengths must be valid, naturally aligned, mutually
  non-overlapping for the duration of the call, and owned by the caller. Null is
  accepted only for a corresponding zero-length optional array. No declared
  region may exceed `PTRDIFF_MAX` bytes.
- The kernel stores no pointer after return and performs no callback or I/O.

Every fallible export is wrapped in `catch_unwind` for unwind-enabled development
and release profiles. Release explicitly uses `panic = "unwind"`, allowing the
boundary to translate an ordinary Rust panic to `AL_NAV_STATUS_PANIC`; no unwind
crosses the C ABI. Process aborts, allocator failures, undefined behavior from a
caller violating the pointer contract, and builds that override the panic strategy
remain unrecoverable. This is a last boundary, not a substitute for validation
and tests.

## Build and measure

From the repository root:

```sh
cargo fmt --manifest-path native/Cargo.toml --all -- --check
cargo clippy --manifest-path native/Cargo.toml --workspace --all-targets -- -D warnings
cargo test --manifest-path native/Cargo.toml --workspace --all-targets
cargo build --manifest-path native/Cargo.toml --workspace --release
cargo run --manifest-path native/Cargo.toml --release -p al_nav_kernel --example throughput
```

The example compares one batched ABI call with repeated one-query ABI calls over
the same synthetic open grid. It is a smoke benchmark, not evidence that Rust is
faster than C#, Burst, Unity NavMesh, or a server implementation. Grid dimensions,
query count, and iterations are command-line inputs; no game catalog or balance
data is compiled into the kernel.

The current Unity integration decision is **not justified / do not integrate**.
See [`UNITY_INTEGRATION_GATE.md`](UNITY_INTEGRATION_GATE.md) for measured evidence,
missing comparisons, and the exact gate required before adding `DllImport` or a
binary under `Assets/Plugins`.

## Integration gates

Do not add a Unity `DllImport`, copy a binary into `Assets/Plugins`, or make this a
runtime dependency until all of these gates have owners and green evidence:

1. Benchmark equivalent production-shaped work against the current C#/Unity
   approach, including transition overhead, allocations, frame time, and memory.
2. Add ABI-layout, symbol, and packaging checks on Windows x64, macOS arm64/x64,
   Linux x64, Android arm64, and the approved iOS static-library targets,
   including loader paths/install names, signing, and final linked size.
3. Add sanitizer/fuzz or equivalent malformed-input coverage and deterministic
   cross-platform fixtures.
4. Define cancellation, job scheduling, and a managed fallback before any
   frame-critical Unity call site.
5. Validate server authority explicitly: clients may request or predict paths,
   but the server owns competitive movement, visibility/interest, and outcomes.
6. Keep designer bakes and procedural outputs versioned, reviewable data. The
   native kernel must never silently become a second catalog authority.
7. Threat-model integrity uses separately. Signed manifests, platform signing,
   online authority, telemetry policy, and recovery remain distinct layers; a
   native hash or obfuscated value alone is not anti-cheat.
