# GS-04 UI/HUD/map release-readiness scorecard

## Scope and result

This scorecard validates the integrated production UI/HUD/map contract for golden scene
`GS-04` (`hud_minimap_map_stress`). It is an objective development-layout gate. It is not a
physical-device performance certification or owner creative approval.

**UI/HUD/map objective gate: PASS.** All automated layout, authority, compatibility, and
accessibility checks listed below pass. The deterministic evidence matrix is complete for the
four supported form factors and four required scenario groups.

**Target-platform benchmark certification: NOT CLAIMED.** The direct Editor runner attempt is
recorded under limitations instead of being converted into synthetic runtime evidence.

## Identity

| Field | Value |
| --- | --- |
| Golden scene | `GS-04` |
| Scene revision | `1` |
| Scenario / anchor / seed | `hud_minimap_map_stress` / `hud_combat` / `904041` |
| Source baseline | `9f023c70648b8a7b527b6e26cf3bb51461da9c0a` plus this task's reviewed PR diff |
| Catalog fingerprint | Validated from `al_golden_scene_catalog.json`; runtime contract tests pass |
| Unity version | `6000.3.22f1` |
| Evidence class | Deterministic development layout/accessibility evidence |
| Supported form factors | Phone landscape 2400×1080; tablet landscape 2732×2048; PC 16:9 1920×1080; PC ultrawide 3440×1440 |
| Capture date / operator | 2026-09-02 / `hermes-kanban` |
| Evidence manifest | `unity/Docs/UI/Evidence/GS-04/AL_GS04_Release_Readiness_Evidence_Manifest.json` |
| Generator | `tools/ui/validate_ui_release_readiness.py` |

The manifest hashes the 16 SVGs and computes one input digest over the exact production token,
HUD composition, HUD component, map-disclosure, and generator files. Re-running the command
must reproduce the same SVG hashes and input digest.

## Evidence matrix

Each row exists for every supported form factor listed above.

| Scenario | Required coverage | Evidence suffix | Automated assertions |
| --- | --- | --- | --- |
| Dense combat | hostile telegraph, player/target/party/objective/route/allegiance density, protected combat read | `_dense_combat.svg` | Six persistent surfaces remain outside the projected scan path; the transparent hostile world-cue layer exactly occupies it |
| Accessibility stress | 200% text, extreme safe area, dense rows, reduced motion/flash/VFX | `_accessibility_stress.svg` | Text scale is `2.0`; safe-area projection is explicit; persistent surfaces remain outside the reprojected scan path; reduced-effect flags are true |
| Expanded map | modal expanded map, minimap inset, objective/allegiance agreement, route surface rule | `_expanded_map.svg` | Shared objective and allegiance IDs appear on both surfaces; the server-owned route is world-map-only and is intentionally absent from the minimap; map presentation is modal rather than gameplay HUD |
| Input/focus paths | touch, controller, keyboard, submit/cancel, restoration | `_input_focus_paths.svg` | All three input modes and one authoritative state are recorded; modal containment and restoration path are named |

The supported ultrawide set currently contains one authored layout, `PcUltrawide` at 3440×1440.
Its actionable HUD remains clamped to the centered reading band rather than stretching to the
physical corners.

## Mandatory objective gates

`PASS` below means automated repository evidence exists. `N/A` means this task did not alter or
claim that domain. `EXTERNAL` is a separately owned target-device or human gate and is not
silently treated as passed.

| Gate | Result | Evidence | Notes |
| --- | --- | --- | --- |
| UI/HUD hierarchy and central scan path | PASS | 16 SVGs; release-readiness validator; `AL.Tests.EditMode.UI` | 48 persistent-slot projections across the dense and accessibility matrices remain outside the protected path; hostile cues use the transparent world-cue layer |
| Phone/tablet/PC composition | PASS | Four form factors × four scenarios | Each form factor uses its authored coordinates and safe-area rules, not a scaled clone |
| Supported ultrawide composition | PASS | `AL_GS04_PcUltrawide_*.svg` | Centered 16:9 reading band is retained at 3440×1440 |
| Dense combat and hostile telegraphs | PASS | `_dense_combat.svg`; UI EditMode tests | Telegraph remains world-space/transparent and semantic critical state remains visible |
| Minimap/world-map agreement | PASS | `_expanded_map.svg`; map authority tests | Objective `map_objective_crossroads_control` and allegiance `allegiance_stonehold` are shared; route `route_stonehold_to_accordant` is correctly world-map-only and omitted from the minimap; authority owner remains `server` |
| Text/UI scaling and extreme safe areas | PASS | `_accessibility_stress.svg`; UI EditMode tests | 200% text and explicit cutout/overscan insets are validated for every form factor |
| Contrast and color-independent state | PASS | production tokens/components; UI and world-map EditMode tests | Labels, frames, patterns, and shapes carry state independently of color |
| Reduced motion/flash/VFX | PASS | accessibility scenario metadata; world-map EditMode tests | Nonessential pulses/flash/VFX are suppressed without removing semantic combat warnings |
| Input navigation/focus | PASS | `_input_focus_paths.svg`; world-map EditMode and PlayMode tests | Touch, controller, and keyboard paths include initial focus, containment, submit/cancel, and valid restoration |
| Data authority | PASS | manifest inputs; shared-contract validation | Canonical catalogs remain under `unity/Assets/AL/StreamingAssets/GameData/`; no code-owned gameplay/map truth was added |
| Existing-save compatibility | PASS | scoped diff plus focused tests | No save schema, serialized save field, migration, or persistence behavior changed |
| Provenance and rights traceability | PASS | benchmark source manifest; deterministic in-repo generator | Evidence uses repository-owned tokens, geometry, labels, and catalogs only |
| Originality/non-copy review | PASS | source manifest and SVG inspection | Comparator sources remain directional references; no comparator screenshot, font, icon, skin, layout, or binary was imported |
| Production font binary | EXTERNAL | evidence manifest limitation | Layout uses production token metrics but the commissioned font binary is not present |
| Physical Android performance/thermal certification | EXTERNAL | sustained benchmark procedure | Requires a PR-identical Player build and target hardware; no desktop inference is substituted |
| Owner creative disposition | EXTERNAL | owner-reserved authority | Objective evidence does not approve final visual taste |
| 3D scene/material/animation/VFX quality gates | N/A | task scope | This scorecard validates GS-04 UI/HUD/map integration only |

## Automated verification record

| Check | Result |
| --- | --- |
| `python tools/ui/validate_ui_design_system.py` | PASS: 8 semantic states, 4 authored form factors, 7 reusable HUD components, 7 required slots each, protected scan paths clear, 4 SVG renders |
| `python tools/ui/validate_ui_release_readiness.py` | PASS: 4 form factors × 4 scenarios = 16 hashed SVGs; scan path, safe area, 200% text, reduced effects, authority agreement, and input paths recorded |
| Shared-contract schema compilation | PASS: 27/27 schemas |
| Shared-contract real catalog validation | GS-04/map catalogs PASS; one documented unrelated `al-world-event-content` known defect remains |
| `AL.Tests.EditMode.UI` | PASS: 26/26 |
| `AL.Tests.EditMode.WorldMap` | PASS: 71/71 |
| `AL.Tests.EditMode.Benchmarks` | PASS: 52/52 |
| `AL.Tests.PlayMode.WorldMap` | PASS: 18/18 |

The repository-wide shared-contract command currently exits nonzero because two pre-existing
realm-character-taxonomy determinism fixtures differ from their checked-in generated outputs.
This task changes none of those catalogs, fixtures, or generators; the defect is recorded rather
than hidden. The GS-04, map-disclosure, and schema compilation rows all pass.

## Reference boundary

| Comparator | Borrow | Adapt for AnotherLife | Avoid |
| --- | --- | --- | --- |
| Black Desert | Information hierarchy and readable action grouping | Fixed AnotherLife semantic slots and mobile-first safe-area rules | Skin, iconography, panel chrome, typography, or coordinate copying |
| Wuthering Waves | Large-scale encounter clarity and effect prioritization | Protected PvP scan path and reduced-effect semantic parity | Character, VFX, HUD, or world-map reproduction |
| THRONE AND LIBERTY | Large-world navigation and group-combat clarity | Server-owned map/minimap projections and stable route/objective IDs | Layout, map art, icons, or presentation skin |
| Infinity Kingdom | Management information grouping | AnotherLife kingdom/map authority and responsive composition contracts | Management layout, asset, or icon copying |

## Known gaps and exclusions

1. The 16 SVGs are deterministic layout/accessibility evidence, not screenshots from a physical
   target device. They intentionally do not claim frame rate, frame pacing, thermal, memory,
   streaming, touch latency, font rasterization, or hardware certification.
2. A direct Unity Editor development-runner attempt used the canonical `GS-04` scene, anchor,
   preset, and seed. It failed closed with
   `AL-GS-BENCHMARK-CAMERA-MISSING: ChampionArena` because direct scene launch had no committed
   realm identity, so `ChampionArenaSceneController` correctly disabled before creating its
   runtime camera/HUD. No empty-camera or fabricated runtime package was accepted as evidence.
3. The production font binaries are absent; fallback typography can validate hierarchy and
   wrapping but cannot certify final glyph metrics or language-specific visual quality.
4. Manual PR-identical-device rows in
   `unity/Docs/UI/Accessibility_And_Multi_Input_Verification.md` remain unchecked. They are
   required before any target-device release claim.
5. Five-second comprehension testing, blinded participant feedback, and owner creative approval
   remain external human gates. No automated score is used as a substitute.
6. The source-manifest availability update replaces retired Qualcomm and MediaTek URLs with
   current first-party pages. Those sources support platform-direction context only; they do not
   certify this build or any particular device.

## Reconnaissance and review boundary

The focused governing/evidence/test corpus contained 25,747 lines; 6,551 lines were read in full
(25.44%), with targeted symbol and acceptance-criterion searches across the remainder. This is
an honest coverage measure, not a claim that every repository line was reviewed.

Independent code/security review and required GitHub CI results are recorded in the PR rather
than pre-filled here. A red required check, review finding, authority drift, or changed evidence
hash invalidates this scorecard until the matrix is regenerated and the affected gates rerun.
