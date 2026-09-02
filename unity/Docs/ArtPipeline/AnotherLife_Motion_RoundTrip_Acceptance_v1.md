# AnotherLife Motion Round-Trip Acceptance v1

Status: bounded-engineering Unity acceptance for the three representative rigs and motion libraries. This is not production admission, owner creative approval, or combat-readability approval.

## Scope

Fresh-import Champion, NPC, and Slagwhistle from the cleaned Blender FBX sources, bind runtime PlayableGraph controllers, and fail closed when catalogs or imported assets are incomplete or over mobile budgets.

| Subject | Rig export | Motion export |
|---|---|---|
| Champion | `champion_vanguard_rig_clean_v002.fbx` | `champion_vanguard_motion_v001.fbx` |
| NPC | `covenant_sentinel_rig_clean_v003.fbx` | `covenant_sentinel_motion_v001.fbx` |
| Fantasy beast | `slagwhistle_burrower_rig_clean_v002.fbx` | `slagwhistle_motion_v001.fbx` |

Authority remains catalog-driven: `al_rig_motion_standard.json`, `al_required_motion_manifest.json`, the rig-cleanup manifest, and `al_motion_library_catalog.v1.json`. Generated Unity copies under `Assets/AL/Generated/MotionRoundTrip/` are test artifacts, not source.

## Automated gates

Python contract (`tools/blender/al_motion_roundtrip_contract.py`) and Unity EditMode builder (`MotionRoundTripAcceptanceBuilder`) both check:

- required motions and skill phases
- duplicate or unstable catalog identifiers
- unsupported skeleton profiles
- invalid bone names or hierarchy
- scale and axis errors
- missing roots or sockets
- skin influence, deforming-bone, and animated-transform budgets
- animation memory, resident-clip, and compression budgets
- missing, dropped, or duplicated events
- invalid hitbox windows and event order
- incompatible root-motion settings

Measurable Unity probes also record trajectory loop error, planted contact / foot sliding, and transition pose deltas. Intentionally incomplete catalogs and budget overages must fail closed.

## How to run

From the repository root:

```text
python tools/blender/al_motion_roundtrip_contract.py
python -m unittest tools.blender.test_al_motion_roundtrip_contract
```

In Unity 6000.3.22f1:

- Menu: `Another Life/Motion/Build Round-Trip Acceptance`
- EditMode: `AL.Tests.EditMode.Animation.MotionRoundTripAcceptanceTests`

The builder writes `Assets/AL/Generated/MotionRoundTrip/MotionRoundTripAcceptance.unity` and `Logs/MotionRoundTrip/motion_roundtrip_acceptance_report.json`.

## Visual-review checklist

Use the acceptance scene at normal speed. Record PASS / FAIL / BLOCKED per representative. Do not hide a failure behind a weighted score.

1. Deformation — extreme poses keep volume; no collapsing elbows, knees, jaws, or beast limb folds; seams stay closed.
2. Clipping — no persistent mesh self-intersection, weapon/body pierce, or ground sink on planted contacts.
3. Facial motion — if a face exists, it tracks the clip without exploding shapes; Slagwhistle has no face and is N/A.
4. Hair / cloth / weapon attachments — every required socket parents a marker; attachments follow the socket without detach or scale inversion.
5. Non-humanoid contacts — Slagwhistle limb contacts plant, push, and release without hover, skate, or invented standing rest.

Owner visual and combat-readability approval remain separate gates.

## Intentional non-production gaps

- Champion and NPC production-rights records remain unresolved.
- Motion clips are engineering samples, not owner-approved combat reads.
- Slagwhistle attack, special attack, defeat, burrow, and standing rest stay blocked.
- This gate does not admit assets to runtime production catalogs.
