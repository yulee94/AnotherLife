# GS-01 through GS-05 Windows Player certification — 2026-09-02

PC-first Windows Player evidence for GS-01..05. Android/mobile rows remain
**DEFERRED/BLOCKED** to Kanban task `t_7b530af7`. This package is not mobile
readiness.

## Player

- Unity `6000.3.22f1`
- Target `StandaloneWindows64`
- Quality `pc_high_60`
- Built-in Render Pipeline
- Build ID `al-gs-20260902T025101Z-7628ad8f4241-StandaloneWindows64`
- Source commit `7628ad8f4241c3ac4f416af9e43d8964976b16e5`

## Runs

Warmup 20s, measurement 40s, 1920x1080, 30 fps, FFmpeg `gdigrab`. GS-03 has two
comparable repetitions (`g03a`, `g03b`). Large still/video/profiler artifacts stay
in `C:/ALBenchmarkEvidence` and are not committed.

| Scene | Run | Anchor | Seed | Status | Result SHA-256 |
| --- | --- | --- | ---: | --- | --- |
| GS-01 | g01a | class_reveal | 901011 | target-platform-evidence-ready-for-review | `13a1b7266293fbc69cdcc41eeee5b647d660952abd2c1a3e7569bb9d3ac90152` |
| GS-02 | g02a | distant_approach | 902021 | target-platform-evidence-ready-for-review | `831b205e7381f7fe3dd703af729e0ed5cec08e4d6a2a85dd09b0760473d216d2` |
| GS-03 | g03a | boss_entry | 903031 | target-platform-evidence-ready-for-review | `6d4bdad9a6397c554add1d4e1b8840b1f8516fd336baaf6944f19b8d1a673427` |
| GS-03 | g03b | boss_entry | 903031 | target-platform-evidence-ready-for-review | `1de4328d60098787fda664d32839ea8593888430112029b4ba343aa91250653f` |
| GS-04 | g04a | hud_combat | 904041 | target-platform-evidence-ready-for-review | `4584d5114fb5b63eb815d4d2d07b27060d2596c1609c2f90d9838cac543df3a6` |
| GS-05 | g05a | city_overview | 905051 | target-platform-evidence-ready-for-review | `5b87280aebda8ebd74e87ab8b6d668663e2cb8094d9395a10ea3094d76f944a5` |

## Validation

```text
python tools/benchmarks/validate_golden_scene_evidence.py C:/ALBenchmarkEvidence \
  --require-scenes GS-01,GS-02,GS-03,GS-04,GS-05 \
  --require-repeat GS-03
```

Result: `Golden-scene evidence validation PASSED: 6 package(s), scenes=GS-01,GS-02,GS-03,GS-04,GS-05`.

Windows device temperature and thermal state remain explicit unsupported capabilities
with platform reasons. Battery was sampled. No third-party comparator media is included.
