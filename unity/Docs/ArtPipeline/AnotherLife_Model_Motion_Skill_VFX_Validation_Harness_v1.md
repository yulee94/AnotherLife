# AnotherLife Model, Motion, and Skill-VFX Validation Harness v1

Status: fail-closed validation contract. This harness does not generate production
assets, admit sources, or replace owner creative/visual approval.

Owning task: `t_d1345d15`.

## Purpose

Validate Blender sources, Unity imports, catalog bindings, skill-phase
presentation, and player-build evidence with an explicit `PASS`, `FAIL`, or
`BLOCKED` verdict per model and per skill. A weighted score is forbidden and
cannot hide missing walking, running, attacking, special attack, or cast/use
motion or effect.

Representative kinds are mandatory:

- Champion
- NPC
- Fantasy beast
- Monster

A missing representative is `FAIL`, not an omitted row.

## Authority

| Artifact | Role |
| --- | --- |
| `al_model_motion_skill_vfx_harness.v1.json` | Check matrix and verdict policy |
| `al_required_motion_manifest.json` | Motion keys and required sets |
| `al_motion_library_catalog.v1.json` | Bound clips for current representatives |
| `skill_weather.v1.json` | Packaged skill motion/VFX modules |
| Existing round-trip/rig contracts | Source health; this harness composes them |

Owner `APPROVE` / `REVISE` / `REPLACE` remains a separate gate. A harness `PASS`
never records creative approval.

## Verdicts

- `PASS` — every required axis and check for that subject is present and valid.
- `FAIL` — a required motion/effect axis is missing or a check failed.
- `BLOCKED` — evidence is absent (no sidecar, no player-build capture, no thermal
  packet). Blocked is not a pass.

Aggregate rule: any `FAIL` then `FAIL`; else any `BLOCKED` then `BLOCKED`; else
`PASS`.

## How to run

From the repository root:

```text
python -m unittest unity.SharedContracts.Tests.test_model_motion_skill_vfx_harness
python unity/SharedContracts/Tests/test_model_motion_skill_vfx_harness.py
python tools/validation/al_model_motion_skill_vfx_harness.py --repo-root .
```

The CLI writes `unity/Logs/ModelMotionSkillVfx/harness_report.json` and a sibling
Markdown report for owner visual/creative review. Live repository evaluation is
expected to stay non-`PASS` until a monster representative exists, required
motion axes are bound, and player-build presentation evidence is attached.

Unity EditMode: `AL.Tests.EditMode.Validation.ModelMotionSkillVfxHarnessTests`.

## Intentional failure fixtures

The unit tests prove these fail closed before realm qualification:

- Champion missing walking
- NPC missing running
- Beast missing attacking
- Monster missing special attack
- Cast/use motion or telegraph/accessibility effect missing
- Monster representative omitted
- Weighted score present
- Player-build or mesh evidence absent → `BLOCKED`

## Out of scope

- Production Meshy/Blender asset generation
- Owner creative approval
- Weakening missing-motion or protected-cue failures
- Treating engineering sample clips as admitted runtime content
