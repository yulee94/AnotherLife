# Unity native navigation integration gate

Decision: **do not integrate `al_nav_kernel` into the Unity player yet.**

Date evaluated: 2026-08-25. This decision does not remove the experiment; it
keeps the ABI and correctness reference available while preventing an
unmeasured native dependency from entering the playable game.

## Evidence available

The current kernel has useful engineering properties:

- caller-owned immutable grid/query input, caller-owned result/point/scratch
  buffers, no retained pointer, callback, I/O, mutable global, catalog copy, or
  gameplay authority;
- a versioned C header using fixed-width C types and explicit ABI/status values;
- deterministic four-way integer weighted-grid paths and fixed tie-breaking;
- bounded structural validation, no partial result on point-buffer overflow,
  and a panic guard for unwind-enabled builds;
- nine unit/reference tests, including generated small grids compared with a
  slow oracle; and
- macOS arm64 static and dynamic release artifacts in the local validation run.

The latest local default throughput smoke on the named development host used a
72 by 72 open grid, 192 deterministic queries per iteration, and 20 iterations:

- batched ABI shape: 6,586.6 queries/s over 0.583 s;
- repeated one-query ABI shape: 6,609.7 queries/s over 0.581 s;
- descriptive batch/per-query ratio: 0.997x; and
- equal semantic checksum: `8726475455292878307`.

Artifact:
`archive/local-run/native/al-nav-integration-gate-2026-08-25.txt`.

This only compares two call shapes into the same Rust implementation. In this
sample batching was not faster. The result says nothing about C#, Burst, Unity
AI Navigation/NavMesh, real world geometry, scheduling, or end-to-end frame
time, and laptop burst timings are not a release target.

## Missing justification

Unity integration is blocked because there is no production-shaped comparison
against the managed implementation the game would otherwise ship. In
particular, there is no evidence for:

- lower p95/p99 frame/job time than a Burst/Jobs or Unity navigation baseline on
  representative terrain, obstacles, agents, query batches, and path lengths;
- acceptable C# to native transition, marshaling, pinning, scheduling,
  cancellation, and result-application cost;
- a supported use case that Unity AI Navigation does not already solve more
  safely for the current playable MVP;
- Windows x64, macOS arm64/x64 universal, Linux x64, Android arm64, and approved
  iOS static-library ABI/layout/symbol/loader/signing/package checks;
- sanitizer/fuzz coverage, cross-platform golden fixtures, hostile caller tests,
  or a managed behavioral fallback;
- final binary/install-size, memory, power, and broad-device impact; or
- lifecycle ownership for domain reload, Editor/player differences, shutdown,
  jobs in flight, and plugin load failure.

## Gate to reconsider

Reconsider a Unity native plug-in only when one named gameplay or bake workload
meets all of these conditions:

1. The same catalog-derived fixtures and semantics run through a clear C# or
   Burst reference, the Rust ABI, and a slow correctness oracle.
2. Release builds on representative low/mid/high devices show a repeatable,
   material p95/p99 improvement after transition, scheduling, allocation, and
   result-application costs—not merely kernel throughput.
3. The user accepts the build, platform, install-size, debugging, and operational
   cost for that measured benefit.
4. Every supported architecture passes ABI layout, symbol export, loader,
   signing, package, and cross-platform golden tests.
5. Cancellation, bounded job queues, deadlines, fallback, telemetry, and native
   failure behavior are implemented and tested without blocking Unity's main
   thread.
6. Sanitizer/fuzz or equivalent hostile-input coverage is green.
7. Competitive authority remains server-side; the client library contains no
   durable secret, trusted currency/state, or anti-cheat decision that depends
   on obscurity.

Until then, do not add `DllImport`, copy a binary into `Assets/Plugins`, or make
the native kernel a player/build dependency. It may continue as an isolated
correctness experiment or an offline bake/validation candidate if that separate
workflow earns its own benchmark and ownership.
