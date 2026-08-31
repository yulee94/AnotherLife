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

The repository does not install a licensed runtime video encoder. Therefore the default
video facility is fail-closed and writes `AL-GS-VIDEO-UNSUPPORTED`. A platform integration
may inject `IGoldenSceneVideoCaptureFacility`; the session still owns anchor reapplication,
drift checks, UI exclusion or benchmark-required routing during synchronous frame capture,
naming, hashing, and manifest linkage.

`GoldenSceneNativeProfilerCaptureFacility` uses Unity native binary profiler logging and
writes the Unity-assigned raw `.raw` artifact required for Unity Profiler review. If `Profiler.supported`
is false, logging is already active, start/finalization fails, or the resulting file is
missing/empty, the manifest records an explicit unsupported/error result instead of a raw
capture claim. Deep Profiling is never enabled.

A completed `GoldenSceneTelemetryReport` may be supplied at finalization to retain the raw
project telemetry JSON. If none is supplied, the manifest records
`AL-GS-TELEMETRY-NOT-PROVIDED` and remains incomplete.

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
