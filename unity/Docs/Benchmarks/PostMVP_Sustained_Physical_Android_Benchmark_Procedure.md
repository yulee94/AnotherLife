# Sustained Physical Android Benchmark Procedure

**Status:** Execution procedure; it does not replace provisional device claims or set
final measured values

**Applies to:** Binding-floor and Android-diversity evidence for the post-MVP golden
benchmark suite

**Governing specification:**
[`PostMVP_Graphics_Benchmark_Spec_2026-08-25.md`](PostMVP_Graphics_Benchmark_Spec_2026-08-25.md)

**Scorecard:**
[`Templates/PostMVP_Golden_Scene_Scorecard.md`](Templates/PostMVP_Golden_Scene_Scorecard.md)

## 1. Certification boundary

This procedure collects reproducible sustained evidence from an installed AnotherLife
Android Player on physical retail hardware. It does not select a final floor, replace a
provisional candidate, change a performance threshold, or grant creative approval.

An emulator result cannot certify the binding floor. An Editor capture, desktop
simulation, device specification, chipset name, or inference from another device using
the same SoC/GPU also cannot certify the floor. Certification applies only to the exact
physical device SKU, RAM/storage configuration, OS/build, graphics API, Player build,
game-data fingerprint, preset, display/power setup, and workload recorded in the
evidence package.

The governing specification currently treats the 30 FPS frame-time and hitch limits as
`PROVISIONAL`. This procedure applies those limits without converting them into final
release values.

## 2. Required roles and tools

Assign one operator and one reviewer. The reviewer checks manifests, hashes, traces, and
scorecards but need not be present during capture.

Before testing, record the exact versions of:

- Android Debug Bridge (`adb`) and the Android platform tools;
- Unity and the Player build pipeline;
- Unity Profiler or Profile Analyzer used to export distributions;
- Android system-trace/FrameTimeline tooling used for delivered-frame pacing;
- any project capture, scenario-runner, metrics-export, or hashing utility.

Do not enable Unity Deep Profiling. It changes workload cost and is not certification
evidence.

## 3. Evidence-unit identity

One evidence unit covers exactly one:

`build × device × golden scene × workload revision × graphics preset × graphics API`

Do not combine devices, scenes, presets, graphics APIs, or build revisions in one
scorecard. Use this stable unit ID:

```text
<build-id>_<device-sku>_<gs-id>_<workload-rev>_<preset>_<api>_<run-set-utc>
```

Use UTC timestamps in file names and manifests. Redact or hash device serial numbers;
retain the same stable pseudonymous device ID across repetitions.

### 3.1 Build identity

Record before installation:

- package/application ID, semantic version, and Android version code;
- source commit and whether the source tree was clean;
- build ID and CI/build-job URL or immutable local build record;
- SHA-256 of every installed APK or split APK, or of the source AAB plus the generated
  device APK set;
- Unity version, scripting backend, target architecture, build type, and stripping mode;
- whether `Development Build`, profiler connection, script debugging, or other
  instrumentation is enabled;
- catalog fingerprint and all content/addressables/catalog revisions;
- quality configuration revision, frame-rate target, frame-pacing setting, and adaptive
  quality configuration;
- golden-scene revision, deterministic seed, anchor/route ID, scenario-runner revision,
  and workload manifest SHA-256.

Use the same content, code, scene, seed, preset, and adaptive-quality configuration for
all repetitions in an evidence unit.

### 3.2 Player variants

A binding package contains both of the following when raw Unity instrumentation is
needed:

1. **Instrumented Player:** a Player build with the minimum instrumentation required for
   raw CPU/GPU, memory, allocation, renderer, and streaming evidence. Deep Profiling and
   script debugging remain off.
2. **Release-equivalent Player:** the same commit, catalogs, assets, scene revisions,
   graphics settings, scripting backend, architecture, and stripping policy intended
   for the candidate release, without development-only profiling overhead. Use Android
   FrameTimeline/system traces and project telemetry for delivered pacing and sustained
   confirmation.

Differences between the two variants must be enumerated. Instrumented results do not
substitute for release-equivalent sustained confirmation. If one build provides all
required evidence without development-only overhead, record that fact and its capture
method.

## 4. Physical-device inventory

Create a device inventory before the first run. Capture source output where the OS
exposes it; do not fill unknown fields by assuming that all devices with the same retail
name are identical.

Record:

- manufacturer, retail model, exact model/SKU/product code, and region/carrier variant;
- stable pseudonymous device ID and physical-access owner/location;
- physical RAM, advertised storage, available storage before install, SoC, CPU, and GPU;
- Android version, security patch level, build fingerprint, kernel, and GPU driver when
  exposed;
- supported and selected graphics API plus Vulkan/OpenGL ES version;
- panel resolution, selected display resolution, selected refresh mode, and variable
  refresh behavior;
- battery design capacity, reported health/capacity/cycle count when exposed, and any
  unresolved battery-health limitation;
- root/bootloader state and every OS, vendor game-mode, thermal, or power-management
  modification;
- case state, mount/handheld state, external cooling, charger state, and ambient
  environment.

A field that cannot be measured is `UNKNOWN`, not an inferred value. An unknown field
that can materially affect performance blocks a binding-floor claim until the reviewer
accepts another direct measurement source.

## 5. Frozen workload manifest

Testing cannot start until the selected golden scene has a versioned workload manifest.
The manifest must state:

- golden-scene and scene revision;
- deterministic seed and starting anchor/save/catalog state;
- exact camera route, inputs, interaction sequence, and loop duration;
- actor counts and full/fallback/nameplate/animation tiers;
- VFX source counts and categories, particle limits, shadows, foliage, decals, weather,
  post-processing, streaming boundaries, and LOD transitions;
- network topology and fixed test-service/save state when the scene requires a server;
- expected loading screens or non-gameplay transitions that are excluded from gameplay
  hitch interpretation;
- graphics preset, resolution/render-scale policy, upscaler, LOD/view-distance settings,
  frame-rate target, adaptive-quality policy, and Android frame-pacing state;
- capture markers used to align profiler, Android, telemetry, video, and operator notes.

The workload must run without operator improvisation. A human may replay a written input
timeline, but an automated deterministic runner is preferred. Record input source and
runner version. If the exact start state or action sequence cannot be reproduced, mark
the evidence unit `BLOCKED` rather than substituting free play.

### 5.1 Required sustained workload by golden scene

Each workload is a continuously repeated interaction loop, not a static idle soak.

| Scene | Sustained loop must include |
| --- | --- |
| GS-01 — Character creator/class reveal | Orbit/zoom; representative face, hair, eye, skin, body, and material edits; preview pose; undo/redo; reset/randomize; save feedback; class-reveal transition; final close view. |
| GS-02 — Capital arrival | Distant approach; threshold reveal; street traversal; landmark and elevated vista anchors; the declared lighting/weather state; representative population; streaming and LOD boundaries in both directions. |
| GS-03 — Combat/major boss/local RvR stress | The approved solo/party/local-density state; movement and combat input; target changes; maximum accepted hostile, allied, support, cosmetic, and ambient effect sources; hostile telegraphs; objective contest and recovery states. |
| GS-04 — HUD/minimap/world-map stress | Exploration, boss, party/squad, objective, chat/notification, map-open, filter/zoom/recenter, input-focus, and recovery states while the declared gameplay load remains active. |
| GS-05 — Private-kingdom 2.5D management | Continuous pan/zoom; selection and neighboring-target handling; inspector/dock; representative construction/upgrade, completion, insufficient-resource, loading, stale/offline, rollback/failure, and cross-mode HUD transition states using authoritative test data. |

Accessibility and aspect-ratio captures required by the benchmark remain separate
scorecard gates. When an accessibility mode materially changes runtime cost, create a
separate evidence unit for the worst accepted mode; do not switch modes during a timed
run unless the frozen workload explicitly tests that transition.

## 6. Controlled device setup

Use the following binding-floor setup unless the approved workload manifest names a
different shipping configuration. Any deviation is recorded before the run and repeated
for all repetitions in that evidence unit.

1. Update neither OS nor game between repetitions. Reboot after any unavoidable update
   and start a new evidence unit.
2. Disable automatic app/OS updates, cloud backup, screen recording, overlays,
   notifications, auto-rotate, adaptive brightness, and unrelated background workloads.
3. Keep network type, access point, server endpoint, and account/save fixture fixed.
   Record observed latency/loss when network behavior can change the workload.
4. Set display resolution explicitly. For the binding 30 FPS run, lock the display to
   60 Hz unless the approved shipping configuration requires another mode. Record panel
   resolution, display mode, and Player output/render resolution separately.
5. Set fixed brightness to `200 ± 20 nit` when a meter is available. Otherwise record
   the exact device slider percentage and keep it unchanged. Disable screen timeout.
6. Use the intended standard shipping power mode. Disable battery saver, adaptive power
   saver, vendor performance boosters, RAM expansion, and automated game optimization
   unless one is explicitly part of the shipping configuration. Record every relevant
   switch and capture settings screenshots.
7. Remove the case, place the device on the same non-insulating stand, keep it unplugged,
   and use no external fan or active cooling. If the product must instead be certified
   in-hand or cased, define that as a separate evidence unit.
8. Use a controlled room target of `23 ± 2 °C`. Record ambient temperature at run start
   and end. A run outside the declared band is invalid for cross-device comparison and
   must be repeated.
9. Before each run, require battery charge from 50% through 80%, Android
   `THERMAL_STATUS_NONE (0)`, and at least one exposed temperature channel no more than
   5 °C above ambient, all sustained for five continuous minutes. Recharge and cool only
   between runs.
10. Confirm sufficient free storage, disconnect the charger, close unrelated apps, and
    take pre-run battery, thermal, process, memory, display, and settings captures.

Do not cool below ambient, use an active cooler, charge during capture, or clear a
thermal warning merely to obtain a better result. If a required thermal signal is not
exposed, document the substitute sensor/tool before testing; otherwise the thermal gate
is `BLOCKED`.

## 7. Install and preflight

For a new evidence unit:

1. Hash the build artifacts and workload manifest.
2. Install the exact Player variant and verify package version, version code, and split
   APK set on the device.
3. Clear app data only if the workload manifest requires a clean fixture. Record the
   action; do not clear caches between repetitions to erase representative streaming or
   shader behavior.
4. Load or provision the deterministic test account/save and confirm its fingerprint.
5. Launch once and verify the build ID, catalogs, scene revision, seed, preset, API,
   resolution, frame-rate target, frame-pacing state, and adaptive-quality state in the
   captured diagnostics overlay/log.
6. Verify capture-tool clocks against UTC and place a shared marker in Unity, Android,
   telemetry, video, and operator notes.
7. Run a two-minute untimed preflight. If the app, profiler connection, input runner,
   telemetry, or trace capture is incomplete, fix it before collecting a repetition.

Capture first-install/first-traversal shader and streaming hitches separately. They are
required streaming evidence but are not mixed into the heat-soaked frame distribution.
Warm-up must never be used to hide an unresolved first-session defect.

## 8. Run protocol

Collect **three valid repetitions** for every evidence unit. Never select only the best
run.

For each repetition:

1. Restore the controlled starting battery/thermal/ambient conditions in Section 6.
2. Capture pre-run inventory, settings, memory, battery, and thermal snapshots.
3. Start video, logs, project telemetry, Unity profiling when applicable, and Android
   FrameTimeline/system tracing. Insert synchronization marker `RUN_START`.
4. Execute the exact scene loop for **five minutes of warm-up**. Warm-up is not included
   in the sustained distribution, but all raw logs and thermal data remain retained.
5. Without pausing, changing settings, reconnecting the charger, or restarting the
   scene, insert `MEASURE_START` and execute the same loop for **at least 20 continuous
   measured minutes**.
6. Sample thermal status, exposed temperatures, battery, clocks, quality state,
   resolution/render scale, memory, and actor/VFX counts at least once per minute without
   changing the workload.
7. Insert `MEASURE_END`, stop the workload, and immediately capture post-run memory,
   thermal, battery, settings, crash/ANR, and process state.
8. Save raw captures before reviewing them. Hash every artifact and write the hash and
   byte size into the run manifest.
9. Record interruptions, visible hitches, quality oscillation, LOD/streaming defects,
   input failures, OS notifications, tool disconnects, or operator deviations with UTC
   time and capture marker.
10. Cool/recharge between repetitions. Do not begin the next repetition until the same
    controlled start criteria are met.

A capture-tool disconnect, OS interruption, operator deviation, scene/seed mismatch,
charging event, out-of-band ambient condition, or missing required raw artifact makes a
run `INVALID`; repeat it and retain the invalid run with its reason. A valid run that
misses a performance or quality gate is `FAIL`, not `INVALID`, and cannot be discarded.

Stop safely and retain the partial evidence if Android reports severe/critical thermal
status, the app crashes or produces an ANR, the OS shuts down the workload, battery drops
below 15%, or the device exhibits unsafe swelling/temperature behavior. Such a stop is a
valid failure unless an independently evidenced external cause invalidated the run.

## 9. Mandatory measurements and derivation

Use the continuous interval from `MEASURE_START` through `MEASURE_END`. Preserve raw
per-frame data; calculate distributions from the full interval, not from selected clips.
Document the analysis script/version and formulas.

Report for each repetition and for the three-run set:

- CPU, GPU, and delivered frame time p50, p90, p95, and p99;
- delivered frame rate, missed/varying presentation intervals, and visible frame-pacing
  pattern;
- every gameplay frame or stall at or above 100 ms, including duration, timestamp,
  scene phase, and classified cause;
- sampled input-to-visible response for the declared combat/interaction actions;
- system, Unity, and peak memory plus graphics-memory estimate;
- per-frame allocations and every garbage-collection event;
- draw calls/batches, triangles/vertices, active renderers, full/fallback/nameplate actor
  counts, and animation update tiers;
- particle/VFX counts by source plus shadow, foliage, decal, weather, reflection, fog,
  post-processing, and light state;
- texture residency, shader compilation, asset-streaming stalls, and LOD/quality
  transition events;
- physical/output resolution, render scale, upscaler, preset, LOD, and view distance;
- each adaptive-quality event with trigger, old/new state, duration, and recovery;
- thermal status/headroom and exposed temperatures over time;
- battery start/end/delta, current/energy data when exposed, power mode, and measured
  duration;
- crashes, ANRs, Android low-memory kills, driver errors, and capture-tool errors.

Do not derive an unexposed hardware value from the chipset name. Mark it `UNKNOWN` and
preserve the direct source used for every recorded identity or measurement.

## 10. Required artifacts

Retain this immutable directory for each evidence unit:

```text
<evidence-unit-id>/
  README.md
  build/
    build-identity.json
    artifact-hashes.sha256
    installed-packages.txt
  device/
    inventory.json
    os-build-and-driver.txt
    display-power-settings/
    battery-health.txt
  workload/
    workload-manifest.json
    workload-manifest.sha256
    deterministic-fixture-record.json
  run-01/
  run-02/
  run-03/
  invalid-runs/
  analysis/
    per-frame-data.csv
    percentile-and-hitch-summary.csv
    thermal-battery-quality-timeline.csv
    analysis-tool-versions.txt
  scorecard.md
```

Each valid or invalid run directory retains:

- raw Unity Profiler capture and exported marker/frame data when applicable;
- raw Android system/FrameTimeline/Perfetto trace;
- timestamped `logcat`, crash, and ANR records;
- project telemetry and scenario-runner log;
- pre-run, per-minute, and post-run thermal/battery/power samples;
- pre-run and post-run memory/process captures plus peak-memory evidence;
- settings screenshots and diagnostic-overlay screenshot;
- full-run device video or a synchronized video sufficient to audit pacing, visual
  oscillation, workload execution, and visible defects;
- operator notes, deviations, interruptions, and the run verdict;
- SHA-256 and byte size for every retained artifact.

Complete one copy of the governing scorecard for the evidence unit and link every result
to a raw artifact and timestamp/range. Store large binaries in the approved evidence
store; commit only the small manifest, scorecard, hashes, stable evidence URI, and
approved summary unless repository policy explicitly permits raw trace/video assets.
Retain evidence for as long as any floor or release decision cites it.

## 11. Pass/fail interpretation

### 11.1 Per repetition

For the provisional Android-floor 30 FPS contract, a repetition can pass only when:

- CPU, GPU, and delivered-frame p95 are each at or below 33.33 ms;
- p99 values are reported and investigated;
- no unexplained gameplay stall is at or above 100 ms;
- Android frame pacing is enabled/verified unless a documented accepted incompatibility
  exists, and no repeated visible pacing defect or obvious oscillation occurs;
- the target is maintained through the measured heat-soaked interval rather than only
  before throttling;
- adaptive scaling, if used, stays within the graceful-degradation contract and does not
  oscillate visibly;
- protected gameplay information, readability, accessibility, realm identity, and
  approved image-stability limits remain intact;
- mandatory memory, streaming, LOD, thermal, battery, crash/ANR, and artifact evidence is
  present; and
- every mandatory scorecard gate is `PASS` or justified `N/A`, with no `FAIL` or
  unresolved `BLOCKED` item.

An explained hitch remains visible in the scorecard. Classification does not excuse a
stall that obscures gameplay truth, causes a missed legal action, or violates another
mandatory gate.

### 11.2 Evidence unit

All three valid repetitions must pass. A valid failed run is not replaced by an extra
better run. If results are inconsistent, the evidence unit fails pending a new build or
an explicitly versioned investigation set.

### 11.3 Device and binding-floor claim

A device passes the candidate build/preset only after every required golden-scene
evidence unit passes on the same exact configuration. A missing scene, unresolved
thermal field, missing raw trace, or blocked mandatory gate makes the device result
`BLOCKED`, not a provisional pass.

A binding-floor claim requires all physically selected binding candidates to be reported
honestly, including failures, and must use the governing approval process to replace any
provisional claim. Results from a higher-RAM variant, another OEM, another SKU, a device
with the same chipset, an emulator, Editor, or a cold short run cannot be substituted for
an unmeasured candidate.

Samsung-only evidence does not satisfy the required pre-beta OEM/GPU diversity. The
selected non-Samsung Adreno-class physical device receives the same procedure and cannot
be certified by Snapdragon/Adreno naming alone.

A technical pass does not grant final creative approval. Separate 3D and 2.5D owner
`APPROVE`, `REVISE`, or `REJECT` dispositions remain required by the quality standard.

## 12. Reviewer completion checklist

The reviewer signs the evidence unit only after confirming:

- all identities, settings, scene steps, tools, hashes, and timestamps are present;
- three valid 20-minute measured repetitions follow five-minute warm-ups from controlled
  starts;
- no valid failed run was reclassified or omitted;
- raw frame, pacing, memory, streaming, thermal, battery, log, and video evidence opens
  and matches the summary;
- percentile and hitch calculations reproduce from retained per-frame data;
- the scorecard exposes every mandatory failure/blocker;
- no emulator, Editor, chipset-name inference, or another SKU is used as certification;
- provisional versus measured conclusions are labeled explicitly; and
- no final device claim or final measured threshold was changed by this procedure.

Record reviewer name, UTC date, procedure revision, disposition
`PASS / FAIL / BLOCKED`, and corrective action in `README.md` and the scorecard.