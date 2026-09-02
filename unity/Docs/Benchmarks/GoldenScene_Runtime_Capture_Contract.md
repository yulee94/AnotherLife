# Golden-scene runtime capture contract

`GoldenSceneRuntimeCapture` exports synchronized AnotherLife evidence for any resolved
GS-01 through GS-05 `GoldenSceneSetup`. It is a Player-runtime component; Editor output is
development evidence only and does not certify a target platform.

## Output boundary

The default root is `Application.persistentDataPath/BenchmarkEvidence`, outside the
repository. A caller may provide an ignored CI artifact directory, but the runtime rejects
paths under `Assets`. Each run receives one directory and stable filenames containing the
scene ID, seed, anchor ID, and run ID:

```text
scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001/
  scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001_still.png
  scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001_video.mp4
  scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001_profiler.raw
  scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001_telemetry.json
  scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001_manifest.json
```

Only files actually produced receive `captured` status, a SHA-256, and a byte size. Missing,
unsupported, or failed facilities receive `unsupported` or `error` records with diagnostic
codes and reasons; they never receive invented paths, hashes, or certification claims.
`isComplete` is true only when exactly one still, video, raw profiler artifact, and telemetry
artifact are captured, the video artifact spans the requested duration, at least the requested
frame-rate-by-duration frame count was captured, and no anchor drift is recorded.
Artifact timestamps cover their actual write/finalization windows rather than reusing a
pre-finalization timestamp.

## Camera and UI behavior

The resolved catalog anchor is reapplied before the still and every requested video frame.
The capture is rejected if the camera differs after rendering/capture. Non-UI captures
disable all active Canvases during the isolated render. UI capture must be requested as
`RequiredByBenchmark` with a requirement reference and is allowed only for GS-01, GS-04,
and GS-05, whose governing benchmark rows explicitly require UI/device compositions.

The still facility renders the configured camera to PNG without changing the Built-in
Render Pipeline. It temporarily routes screen-overlay Canvases through the resolved camera
only when benchmark-required UI is requested, then restores every Canvas and render target.

## Video and profiler capabilities

The repository does not install or redistribute a runtime video encoder. Therefore the default
video facility is fail-closed and writes `AL-GS-VIDEO-UNSUPPORTED`. Windows Player runs may
select an operator-installed `ffmpeg.exe` with `--al-gs-ffmpeg <absolute-path>`. The runner uses
FFmpeg `gdigrab` against the Player window, writes MP4 `yuv420p` media, and never adds the
executable to the evidence package. The path must identify an existing file named `ffmpeg.exe`;
all other platforms remain unsupported through this CLI integration. The session still owns
anchor reapplication, drift checks, UI exclusion or benchmark-required routing across the entire
external capture interval, naming, hashing, and manifest linkage. The capture scheduler catches up
missed video-frame ticks so a slower Player loop still meets `videoFrameRate * duration` instead of
failing closed on hitch-induced under-counting.

`GoldenSceneNativeProfilerCaptureFacility` uses Unity native binary profiler logging and
writes the Unity-assigned raw `.raw` artifact required for Unity Profiler review. If `Profiler.supported`
is false, logging is already active, start/finalization fails, or the resulting file is
missing/empty, the manifest records an explicit unsupported/error result instead of a raw
capture claim. Deep Profiling is never enabled.

A completed `GoldenSceneTelemetryReport` may be supplied at finalization to retain the raw
project telemetry JSON. If none is supplied, the manifest records
`AL-GS-TELEMETRY-NOT-PROVIDED` and remains incomplete.

The runtime telemetry source uses Unity profiler counters where Unity exposes an authoritative
counter. It derives GC events from `System.GC.CollectionCount`, texture backlog from Unity's
streaming texture APIs, and scene-density values from one-second active-hierarchy snapshots.
Native allocation and shader-compilation event counts come from the `UnsafeUtility.Malloc` and
`Shader.CompileGPUProgram` profiler markers. LOD-transition counts compare each active LOD group's
renderer-visibility signature between snapshots. Scene systems may publish a more specialized
value through `GoldenSceneTelemetryGaugeRegistry`; a published value replaces the corresponding
generic snapshot and records its own source. Zero remains a measured value (for example, no
particles or streaming backlog), while unavailable APIs remain explicit `unsupported` capabilities.

## Provenance boundary

Every manifest pins source manifest
`al.postmvp.graphics_benchmark_sources.2026-08-25` and records
`thirdPartyMediaIncluded: false`. Capture policy rejects any request that claims third-party
media. Comparator URLs and concise observations remain in the benchmark source manifest;
no comparator binary enters this pipeline.

## Runtime use

1. Load and validate `al_golden_scene_catalog.json`.
2. Resolve the exact scene, anchor, preset, and seed with
   `GoldenSceneConfigurationResolver.TryResolve`.
3. Apply the setup and create a matching `GoldenSceneIdentityRecord`.
4. Add `GoldenSceneRuntimeCapture` to a persistent runner object and call `BeginCapture`.
5. Supply a platform video facility and a running telemetry collector when available.
6. Wait for `ManifestReady`; inspect `isComplete`, `durationRequirementMet`, every artifact
   status, and `anchorConsistency` before using the evidence.

The manifest is evidence metadata, not an approval or certification decision.

## Command-line benchmark runner

`GoldenSceneBenchmarkRunner` starts only when `--al-gs-run` is present. It validates the
canonical catalog, resolves one exact scene/anchor/preset/seed, applies the Built-in pipeline
quality setup, runs bounded warmup and measurement windows, invokes runtime telemetry and
capture, maps every scorecard template row to `pass`, `fail`, or `unavailable`, then publishes
one atomic result directory. Example Player invocation:

```text
AnotherLife.exe --al-gs-run \
  --al-gs-scene GS-03 --al-gs-anchor boss_entry \
  --al-gs-quality pc_high_60 --al-gs-seed 903031 \
  --al-gs-warmup-seconds 300 --al-gs-measurement-seconds 1200 \
  --al-gs-width 1920 --al-gs-height 1080 --al-gs-video-fps 30 \
  --al-gs-ui excluded --al-gs-output C:/captures \
  --al-gs-ffmpeg C:/Tools/ffmpeg/bin/ffmpeg.exe \
  --al-gs-run-id run-0001 --al-gs-operator automation \
  --al-gs-certification target-platform
```

Use `--al-gs-certification development` for development evidence. Editor execution rejects
`target-platform`; an Editor run can never certify a target platform. Each Player build embeds
its build ID, source commit, catalog fingerprint, Unity version, build target, and required
`Built-in Render Pipeline` identity. Runtime validation fails closed if that metadata differs
from the packaged catalog, running Unity version, build target, or render pipeline. Player
results also record the executable's non-empty 32-hex `Application.buildGUID`; Editor results
use the explicit non-certifying `editor-not-applicable` marker.

The runner writes into a hidden staging directory and exposes the final directory only after
all required metadata files exist. The atomic package contains the runtime capture artifacts
plus:

```text
runtime-identity.json
telemetry.json
capture-manifest.json
scorecard.json
scorecard.md
benchmark-result.json
```

`benchmark-result.json` links the exact identity, raw telemetry and capability/error records,
artifact statuses, provenance declaration, and scorecard. Unsupported video or profiler
capabilities remain explicit and keep certification evidence incomplete; they are never filled
with inferred or invented values. Windows Player device APIs are platform-aware: battery level,
device temperature, and thermal state may be recorded as `unsupported` only when each capability
has zero samples and a non-empty platform reason, while start/end/device-sample records remain
present. Android certification still requires those three capabilities to be supported with
samples.

## Certifying-package validation

Run the repository validator against the external evidence root after capture and before a
package is cited in a scorecard or approval record:

```text
python tools/benchmarks/validate_golden_scene_evidence.py C:/ALBenchmarkEvidence \
  --require-scenes GS-01,GS-02,GS-03,GS-04,GS-05 \
  --require-repeat GS-03
```

The validator discovers result directories recursively and fails closed unless each package is
Player-build, target-platform evidence ready for review. It checks all identity fields, the
Built-in Render Pipeline boundary, source-manifest provenance, exact artifact linkage, byte sizes
and SHA-256 values, still-image framing, complete raw evidence, required metric capabilities,
and nearest-rank p50/p90/p95/p99 values reproduced from measured raw samples. A required repeat
must retain the same build, device pseudonym, seed, anchor/camera state, quality settings, media
framing, aggregate metric schema, and capability schema. Performance values may vary between
repetitions; identity or schema drift may not.

Large still, video, profiler, telemetry, and device evidence remains in the external evidence
root (normally `Application.persistentDataPath/BenchmarkEvidence` or an ignored CI artifact
directory). Do not add those generated binaries to the repository. Commit only approved small
manifests, hashes, stable evidence URIs, and summaries when a later gate explicitly requires them.

The strict validator intentionally rejects development packages that record unsupported video,
profiler, actor-density, streaming, or other mandatory capabilities. It applies the explicit
Windows device-API policy above without inventing device readings. Such incomplete packages
remain useful diagnostics but are not certifying evidence. Current execution is PC-first:
GS-01 through GS-05 are certified as Windows Player evidence, with representative repetitions
using identical build, device pseudonym, seed, anchor, quality, and media settings. Every Android
row is explicitly deferred/blocked to Kanban task `t_7b530af7`; no Windows result is evidence of
mobile readiness. Android-floor certification still requires the physical-device procedure,
three valid repetitions, five-minute warmups, and 20-minute measured soaks; short Player smoke
runs do not satisfy that procedure.
