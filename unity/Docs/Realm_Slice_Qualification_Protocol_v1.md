# AnotherLife Realm-Slice Qualification Protocol

**Control version:** `1.0.0`

**Control identity:** `RSQ-PROTOCOL-v1.0.0`

**Status:** Active qualification contract

**Owning task:** `t_d0e4bb3c`

**Approval dependency:** `t_0648ce23`

## 1. Purpose and authority

This protocol defines the reusable, fail-closed qualification contract for exactly four
realm slices in this immutable order:

1. `Stonehold`
2. `Eldergrove`
3. `Crownlands`
4. `Umbral`

Every realm requires independent packaged-build evidence for `Adventure3D` and
`Kingdom2_5D`. Evidence from one presentation mode cannot qualify, waive, replace,
complete, or be copied into the other mode. Shared build or device identity is allowed;
shared findings, manifests, pass states, media, and owner decisions are not.

This protocol specializes, but does not weaken, these authorities:

- [Gate 0 Evidence Governance and Stage-Gate Controls](Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md)
- [Post-MVP Graphics and UI Quality Standard](PostMVP_Graphics_And_UI_Quality_Standard.md)
- [Post-MVP Graphics Benchmark Specification](Benchmarks/PostMVP_Graphics_Benchmark_Spec_2026-08-25.md)
- [Golden-scene Runtime Capture Contract](Benchmarks/GoldenScene_Runtime_Capture_Contract.md)
- [Integrated Deterministic QA](Integrated_Deterministic_QA.md)
- [Release Evidence Package and Rollback Runbook](Release_Evidence_And_Rollback_Runbook.md)
- [Schema 1 Save Migration and Recovery Policy](Schema1_Save_Migration_And_Recovery_Policy.md)
- [Cross-Mode Menu and Kingdom Navigation Contract](Cross_Mode_Menu_Kingdom_Navigation_Contract.md)
- [Accessibility and Multi-Input Verification](UI/Accessibility_And_Multi_Input_Verification.md)

When an external release, capacity, performance, accessibility, platform, or content
authority owns an exact threshold, this protocol records that authority and the measured
result; it does not invent or silently copy a replacement threshold. An unresolved,
provisional-but-unapproved, inaccessible, or conflicting threshold makes the affected
check `FAIL_CLOSED`.

## 2. Normative terms and identities

`MUST`, `MUST NOT`, `REQUIRED`, and `FAIL_CLOSED` are normative.

Canonical values are case-sensitive:

- realms: `Stonehold`, `Eldergrove`, `Crownlands`, `Umbral`;
- modes: `Adventure3D`, `Kingdom2_5D`;
- mode namespaces: `3d`, `2_5d`;
- locales: `en-US`, `ko-KR`;
- technical result: `PASS`, `FAIL`, `FAIL_CLOSED`;
- execution state: `NOT_RUN`, `RUNNING`, `COMPLETE`, `BLOCKED`;
- owner decision: `APPROVE`, `REVISE`, `REJECT`, `REOPEN`.

One qualification candidate has an immutable identity:

```text
RSQ-<realm>-<mode-namespace>-<candidate-revision>-<sequence>
```

One evidence packet has an immutable identity:

```text
RSQ-EV-<realm>-<mode-namespace>-<candidate-revision>-<sequence>
```

Corrections create a new candidate or packet identity and point to the superseded record.
No file or record named `latest`, `final`, or `approved-new` is admissible.

## 3. Qualification run envelope

Every matrix row is a separate check execution. Before launch, the harness MUST freeze and
record all fields below. A blank, `TBD`, inferred, or mutable value blocks launch.

| Field | Required value |
| --- | --- |
| `protocolId` | `RSQ-PROTOCOL-v1.0.0` |
| `candidateId`, `evidencePacketId` | Immutable identities from section 2 |
| `realm`, `realmOrdinal` | Exact canonical realm and ordinal `1` through `4` |
| `mode`, `modeNamespace` | Exact canonical mode and its namespace |
| `checkId`, `scenarioId`, `scenarioVersion` | Exact matrix/check and deterministic fixture identity |
| `sourceRevision`, `sourceDirty` | 40-character Git commit and `false` |
| `buildId`, `buildManifestSha256`, `artifactTreeSha256` | Exact packaged Player identity from a verified clean build |
| `sceneCatalogSha256`, `contentCatalogSha256`, `narrativeCatalogSha256` | Exact packaged catalog identities |
| `saveFixtureId`, `saveFixtureSha256`, `saveSchemaDisposition` | Exact cloned fixture; never a live player save |
| `seed`, `logicalClockUtc` | Fixed values supplied by the versioned scenario |
| `platform`, `deviceId`, `osVersion`, `graphicsApi`, `qualityPreset` | Exact target and configuration |
| `viewport`, `renderScale`, `refreshRate` | Exact display/capture state |
| `locale`, `inputClass`, `accessibilityPreset` | One declared value per run |
| `operator`, `independentReviewer` | Named identities; reviewer did not implement the candidate |
| `startedUtc`, `completedUtc` | Actual ISO 8601 UTC bounds |

The current PC-first foundation permits Windows Player qualification only. Android,
physical-device, mobile, or cross-platform claims remain blocked until their owning gate
is explicitly reactivated and satisfied. A Windows packet cannot be relabeled as Android
or mobile evidence.

### 3.1 Deterministic scenario fixtures

The harness implementation MUST provide versioned fixtures with these semantic contracts.
The packet records each fixture's actual immutable ID and hash; the labels below do not
claim that an unimplemented fixture already exists.

| Scenario contract | Deterministic setup |
| --- | --- |
| `RSQ-3D-ARRIVAL-v1` | Clean cloned profile; realm-bound slice spawn; fixed weather/time; fixed population; ordered anchors `slice_spawn`, `capital_threshold`, `capital_landmark`, `civic_entry`, `civic_objective`, `slice_return`; no random encounters outside the seed. |
| `RSQ-3D-COMBAT-v1` | Fixed player build/loadout, target set, hostile telegraph script, party-support state, objective-contest state, effect-source ladder, and defeat/recovery sequence. |
| `RSQ-2_5D-KINGDOM-v1` | Cloned early/middle/mature private-kingdom snapshots; fixed grid, resources, bounded queue, accepted receipts, neighboring selectable targets, construction/failure states, and camera anchors. |
| `RSQ-2_5D-STRATEGIC-COMBAT-v1` | Fixed strategic encounter projection with self, party, hostile, objective, route, allegiance, highest-threat, result, and unavailable/reconnect states; no authority-changing command is issued. |
| `RSQ-CROSS-MODE-v1` | `LordshipUnlocked` fixture; exact `Adventure3D -> Kingdom2_5D -> Adventure3D` switch, view switch, rejection, idempotent retry, interruption, HUD collapse/restoration, and authoritative snapshot checks. |
| `RSQ-SAVE-CONTINUITY-v1` | Cloned pre-schema and schema-1 fixtures from the approved manifest; fixed actions and logical clock; primary/backup/quarantine hashes captured before and after save/restart. |
| `RSQ-NARRATIVE-<realm>-vN` | Realm-bound catalog path containing entry, objective, branch/consequence, completion, persistence, restart, and resume checkpoints. The exact path and catalog hash are mandatory; absent realm content is `BLOCKED`, not substituted from another realm. |
| `RSQ-LOCALE-v1` | Same scenario state and seed run separately under `en-US` and `ko-KR`, with pseudo-key/missing-font detection, text-bound capture, caption/subtitle capture, and input-glyph verification. |

A fixture revision, seed, anchor, clock, actor set, save, or authoritative catalog change
creates a new scenario version. It cannot be changed between 3D and 2.5D runs while
claiming equivalence.

### 3.2 Run cube

Every matrix row MUST be executed for `en-US` and `ko-KR`. Mode-appropriate runs MUST
cover keyboard/mouse and controller on Windows. Touch is mandatory only for an admitted
touch platform, but remains `BLOCKED` rather than `PASS` when a gate requires that
platform. Required accessibility presets are `default`, `text-200`, `reduced-motion`,
`reduced-flash`, `reduced-vfx`, `audio-off-captions`, and `non-color` where applicable.
The active stage/device authority determines the required device and quality tiers.

Each mode writes to a non-overlapping root:

```text
<evidence-root>/<candidate-id>/<realm>/<mode-namespace>/<locale>/<check-id>/<run-id>/
```

The harness MUST reject any path collision, cross-mode artifact reference, or manifest
whose `mode` disagrees with its namespace. A common raw build manifest may be referenced
by hash, but no check output may be physically shared or counted twice.

## 4. Universal evidence and result contract

Every check row below inherits these mandatory outputs:

1. Packaged Player `Player.log`, launcher/harness log, and the row-specific structured log.
2. A row manifest containing the complete run envelope, exact commands, exit states,
   artifact paths, byte sizes, SHA-256 values, and collection UTC bounds.
3. The row-specific screenshots and/or continuous video captured from the packaged build.
4. `expectedResult`, `observedResult`, `executionState`, `technicalResult`, `reasonCode`,
   `defectIds`, `artifactIds`, `reviewer`, `reviewedUtc`, and `supersedes` fields.
5. Raw outputs. A collage, score, summary, or edited highlight without its raw source is
   not evidence.

`PASS` requires `executionState=COMPLETE`, every required artifact present and hash-valid,
the expected result satisfied, no unresolved defect affecting the check, and an
independent reviewer signature. `FAIL` means the run completed and contradicted an
expected result. `FAIL_CLOSED` means evidence, authority, setup, capability, provenance,
identity, reviewer, or required field is missing, stale, inaccessible, or conflicting.
`BLOCKED` is an execution state and always produces technical result `FAIL_CLOSED`.
`NOT_RUN`, skipped, unsupported, cancelled, and not-applicable are never passes for a
mandatory row.

### 4.1 Defect disposition

Every observed deviation receives a durable defect ID, severity, owner, affected realm,
mode, locale/platform, reproduction fields, evidence links, and one disposition:
`OPEN`, `FIXED_AWAITING_RERUN`, `VERIFIED_FIXED`, `ACCEPTED_NON_GATE`, or `DUPLICATE`.

- `P0`: save loss/corruption, narrative-state corruption, wrong realm/order, security or
  authority breach, crash, critical interaction unreachable, or false advancement.
- `P1`: mandatory readability/accessibility/input/performance failure, material visual
  divergence, missing gameplay truth, or repeatable progression blocker.
- `P2`: non-blocking visible defect that still contradicts a mandatory expected result.
- `P3`: observation outside the matrix expectation and outside qualification scope.

`P0`, `P1`, or `P2` cannot be open when their row is `PASS`. Owner preference cannot waive
an objective failure. `ACCEPTED_NON_GATE` is allowed only for a `P3` with explicit scope
and rationale; it does not change an observed matrix failure to pass. A fix requires a
new complete row run; edited evidence or a partial spot check cannot close the row.

## 5. Independent `Adventure3D` evidence matrix

All rows are mandatory for each realm. `L` means the universal logs plus the named
structured log. `M` means raw packaged-build media; exact filenames and hashes are listed
in the row manifest.

| Check | Deterministic setup | Expected result | Required packaged-build logs | Required screenshots/video | Defect disposition | Required pass/fail fields |
| --- | --- | --- | --- | --- | --- | --- |
| `RSQ-3D-REN-001` Rendering | Run `RSQ-3D-ARRIVAL-v1` at identical arrival anchors for every admitted quality tier, supported lighting extreme, and locale. | No missing/fallback/debug asset; grounded material classes remain distinct without emission; lighting preserves form, route, telegraphs, and realm identity; LOD/streaming transitions do not erase silhouettes or pop into invalid state. | `L`: `render.jsonl`, streaming/LOD events, missing-asset/fallback-font scanner, catalog/hash binding. | Still at every anchor/tier/lighting state; continuous threshold-to-landmark video showing LOD and streaming transitions. | Any divergence is `FAIL`; missing tier/capture/hash is `FAIL_CLOSED`; unapproved visual direction is at least `P1` and routes to owner `REVISE`. | `missingAssetCount`, `fallbackCount`, `lodInvalidCount`, `streamingErrorCount`, `materialReadPass`, `realmIdentityPass`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-CAM-001` Camera | Replay the fixed anchor route, combat target changes, collision/occlusion probes, recenter, shake-off, reduced-motion, and restore points with the same input script. | Camera reaches each anchor within declared tolerance, keeps player/target/actionable threat visible, avoids clipping/unstable correction, honors reduced shake/motion, and restores the exact saved camera state. | `L`: `camera.jsonl` with requested/actual pose, obstruction, target visibility, correction, shake, and restore digest. | Continuous route/combat video plus before/after restore stills with anchor overlay recorded separately from clean media. | Any lost target, critical occlusion, clipping through traversable structure, unstable correction, or failed restore is `FAIL`; missing tolerance authority is `FAIL_CLOSED`. | `anchorPassCount`, `anchorFailCount`, `targetVisiblePass`, `criticalOcclusionCount`, `restoreDigestMatch`, `reducedMotionPass`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-NAV-001` Navigation | Drive ordered anchors `slice_spawn -> capital_threshold -> capital_landmark -> civic_entry -> civic_objective -> slice_return`; include stairs/ramps/doors, one streamed boundary, one recovery/repath, and required enterable interior. | Each anchor is reached in order without teleport, stuck state, collision bypass, inaccessible required door, invalid nav surface, or loss of input/camera authority; return route is valid. | `L`: `navigation.jsonl` with anchor order, position, route length, transition, collision, door/interior, recovery, and timeout records. | Continuous traversal video; stills at each semantic anchor and any collision/nav failure. | Inaccessible critical interaction, route bypass, skipped anchor, or non-traversable required interior is `P0` and `FAIL`; absent realm route fixture is `FAIL_CLOSED`. | `requiredAnchors`, `visitedAnchors`, `orderMatch`, `teleportCount`, `stuckCount`, `bypassCount`, `criticalInteractionReachable`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-CMB-001` Combat readability | Run `RSQ-3D-COMBAT-v1` through solo, party, density/effect ladder, every hostile telegraph, support field, objective contest, defeat, and recovery under default and reduced-effect presets. | Player, target, attackers, hostile telegraphs, damage direction, legal action/control state, party-support fields, and objective truth remain identifiable; input-to-visible response and effect source remain attributable; accessibility presets retain threat truth. | `L`: `combat_readability.jsonl`, action/telegraph timeline, input-response samples, actor/effect counts, objective/control-state events. | Uncut encounter video per preset/locale; stills at peak accepted load, each major telegraph, defeat/revive, and contested objective. | Any hidden/misleading threat, missing legal-action state, removed accessibility truth, or unreadable objective is `P1` and `FAIL`; absent authored telegraph fixture is `FAIL_CLOSED`. | `telegraphsExpected`, `telegraphsObserved`, `hiddenThreatCount`, `actionStatePass`, `supportFieldPass`, `objectiveTruthPass`, `inputResponsePass`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-UI-001` UI composition | Run the 3D exploration/combat states with GS-04 compositions for every admitted viewport, safe-area extreme, locale, text scale, and input class. | Vitals, target, highest threat, immediate action, party, objective, route, allegiance, captions, and focus remain readable; protected central combat scan path remains unobstructed; no clipping/overlap/fallback glyph. | `L`: `ui_composition.jsonl`, layout bounds, safe area, text bounds, focus path, overlap and protected-region checks. | Still for each composition and stress state; continuous focus/navigation video for each input class. | Any protected-cue obstruction, critical clipping, missing semantic state, or unreachable focused action is `P1` and `FAIL`; missing form factor required by the active gate is `FAIL_CLOSED`. | `viewportPass`, `safeAreaPass`, `protectedScanPathPass`, `criticalOverlapCount`, `clippedCriticalTextCount`, `focusPathPass`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-PERF-001` Performance | Run the active authority's warmup and measured soak for `RSQ-3D-ARRIVAL-v1` and `RSQ-3D-COMBAT-v1` on every required physical/device tier; bind the exact accepted threshold-set ID. | Every active frame-time, hitch, memory, allocation/GC, streaming, LOD, thermal, battery, rendering, density, and input-response criterion passes without hiding protected information or visible quality oscillation. | `L`: `performance.json`, raw telemetry, raw Unity profiler, quality/adaptive-scaling events, thermal/power/device capability records. | Timestamp-synchronized full soak video or required sampled video; start/peak/end stills at fixed anchors and quality state. | Any threshold miss or unavailable mandatory capability is `FAIL`; missing/unapproved threshold set, short soak, Editor-only trace, or nonphysical evidence where physical is required is `FAIL_CLOSED`. | `thresholdSetId`, `warmupSeconds`, `measuredSeconds`, `sampleCount`, `frameTimePercentiles`, `hitchResult`, `memoryResult`, `streamingResult`, `thermalResult`, `inputResponseResult`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-SAVE-001` Save continuity | Clone `RSQ-SAVE-CONTINUITY-v1`; load/migrate as applicable, complete fixed 3D movement/combat/narrative actions, save, terminate, relaunch the same build, reload, repeat idempotently, and compare expected state/hashes. | Approved progress, realm, avatar, quest, inventory/equipment, map disclosure, mode eligibility, and 3D return state survive exactly; schema policy holds; no live save is touched; failure paths preserve bytes and evidence. | `L`: `save_continuity.jsonl`, load/migration/write/recovery status, before/after generation hashes, ledger/marker hashes, restart and idempotence records. | Video from load through post-restart verification; stills of visible pre-save and restored states; no raw save bytes or PII in media/logs. | Any loss, silent reset/downgrade, cross-realm contamination, unsafe write, or unexplained hash/state mismatch is `P0` and `FAIL`; unavailable fixture/generation evidence is `FAIL_CLOSED`. | `fixtureId`, `schemaBefore`, `schemaAfter`, `stateDigestBefore`, `stateDigestAfter`, `generationHashesPreserved`, `idempotencePass`, `liveSaveTouched`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-NAR-001` Narrative execution | Run `RSQ-NARRATIVE-<realm>-vN` in the packaged Player through entry, objective, branch/consequence, completion, save, restart, and resume for each locale. | Runtime follows the exact catalog path in order; visible objective/dialogue/state agrees with events and persisted state; no skipped, duplicated, stale, wrong-realm, or editor-only narrative path occurs. | `L`: `narrative.jsonl`, checkpoint/order events, objective state, choice/consequence, catalog keys/hashes, save/resume linkage, missing-localization-key scanner. | Uncut path video per locale; stills at entry, choice, completion, and resumed checkpoint. | Wrong realm/order, state regression, disconnected packaged path, or persistence mismatch is `P0` and `FAIL`; missing authored realm scenario or catalog authority is `FAIL_CLOSED`. | `expectedCheckpointIds`, `observedCheckpointIds`, `orderMatch`, `duplicateCount`, `wrongRealmCount`, `persistResumeMatch`, `catalogMatch`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-INP-001` Input | Replay movement, camera, target, all four skill/action slots, interact, menu, map, cancel/back, and mode-switch request using every input class required by the admitted platform. | Every critical action is reachable, activates once, reports the same authoritative state, uses correct glyph/prompt, survives device change, and leaves no competing input maps or stuck focus. | `L`: `input.jsonl` with device, binding/action, phase, authoritative result, glyph, focus owner, active input maps, and duplicate/missed activation counts. | Continuous video per input class and one device-switch sequence; still of binding/glyph states. | Any supported input unable to reach/activate a critical interaction, duplicate activation, wrong authority, or simultaneous conflicting map is `P0` and `FAIL`; required unavailable hardware is `FAIL_CLOSED`. | `requiredActions`, `observedActions`, `missedCount`, `duplicateCount`, `glyphPass`, `deviceSwitchPass`, `exclusiveInputMapPass`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-ACC-001` Accessibility | Replay arrival, traversal, combat, modal, captions, and critical interactions separately and combined under every required accessibility preset in both locales. | 200% text remains readable; focus is visible/restored; critical states are non-color-only; reduced motion/flash/VFX suppresses only nonessential effects; audio-off captions preserve meaning; remapped inputs retain reachability. | `L`: `accessibility.jsonl`, preset state, semantic cue inventory, contrast/non-color checks, focus path, caption events, reduced-effect source states, remap results. | Still for default versus each preset at fixed anchors; continuous reduced-effect combat and keyboard/controller focus videos. | Loss of gameplay truth, unreadable/clipped critical text, color-only meaning, absent caption parity, unsafe flash/motion, or unreachable remap is `P1` and `FAIL`; unsupported mandatory capability is `FAIL_CLOSED`. | `text200Pass`, `focusPass`, `nonColorPass`, `reducedMotionPass`, `reducedFlashPass`, `reducedVfxPass`, `audioOffCaptionPass`, `remapPass`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-LOC-EN-001` English presentation | Run `RSQ-LOCALE-v1` with `locale=en-US` across arrival, combat, menu/map, narrative, save feedback, error/reconnect, and result states at every required viewport/text scale. | Approved English strings, captions, plural/parameter values, prompts, realm terms, and narrative order render without raw keys, fallback glyphs, truncation, overlap, or contradictory meaning. | `L`: `localization.jsonl`, resolved key/value identifiers, font/fallback scanner, text bounds, parameter/plural checks, caption timing, locale-change events. | Still of every text-heavy state and error/result state; continuous narrative/menu/caption video. | Any raw/missing key, critical truncation, fallback glyph, incorrect parameter/plural, mistimed caption, or meaning conflict is `P1` and `FAIL`; unapproved copy is `FAIL_CLOSED`. | `locale`, `keysExpected`, `keysResolved`, `rawKeyCount`, `fallbackGlyphCount`, `criticalTruncationCount`, `captionTimingPass`, `semanticReviewPass`, `technicalResult`, `reasonCode`. |
| `RSQ-3D-LOC-KO-001` Korean presentation | Repeat the exact `RSQ-LOCALE-v1` state, seed, anchors, viewports, and text scales with `locale=ko-KR`; use the approved Korean font/copy authority. | Approved Korean strings and captions preserve gameplay/narrative meaning and hierarchy with correct glyph coverage, line breaking, parameters, prompts, and timing; no English fallback except explicitly approved proper nouns. | `L`: `localization.jsonl`, resolved key/value identifiers, Hangul/font/fallback scanner, text bounds, parameter checks, caption timing, unintended-English scanner. | Same state-for-state still/video inventory as the English row, captured independently from the Korean packaged run. | Any missing Hangul, unintended English, raw key, critical truncation, fallback glyph, parameter error, mistimed caption, or semantic divergence is `P1` and `FAIL`; absent approved Korean copy/font is `FAIL_CLOSED`. | `locale`, `keysExpected`, `keysResolved`, `hangulCoveragePass`, `unintendedEnglishCount`, `fallbackGlyphCount`, `criticalTruncationCount`, `captionTimingPass`, `semanticReviewPass`, `technicalResult`, `reasonCode`. |

## 6. Independent `Kingdom2_5D` evidence matrix

These rows use separate `2_5d` manifests, logs, media, findings, defects, and approvals.
A corresponding `3d` artifact ID is invalid in any `artifactIds` field below.

| Check | Deterministic setup | Expected result | Required packaged-build logs | Required screenshots/video | Defect disposition | Required pass/fail fields |
| --- | --- | --- | --- | --- | --- | --- |
| `RSQ-2_5D-REN-001` Rendering | Run `RSQ-2_5D-KINGDOM-v1` for early/middle/mature snapshots at identical GS-05 anchors, all admitted quality tiers, lighting states, and locales. | Kingdom is realm-specific by structure/material, not palette alone; roofs/footprints/roads/entries/states remain legible; no missing/fallback/debug asset; LOD/atlas/streaming transitions preserve identity and state truth. | `L`: `render.jsonl`, atlas/residency/LOD/streaming events, missing-asset/fallback-font scanner, snapshot/catalog hashes. | Still at each maturity/anchor/tier/lighting state; continuous pan across the mature kingdom showing residency and LOD transitions. | Any state/identity loss or visual divergence is `FAIL`; missing maturity/tier/capture/hash is `FAIL_CLOSED`; unapproved visual direction is at least `P1` and owner `REVISE`. | `snapshotPassCount`, `missingAssetCount`, `fallbackCount`, `lodInvalidCount`, `residencyErrorCount`, `stateTruthPass`, `realmIdentityPass`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-CAM-001` Camera | Replay fixed pan/zoom bounds, edge clamps, anchor jumps, rotation if authorized, occlusion/roof handling, neighboring-target selection, recenter, reduced motion, and restore. | Camera remains controlled, continuous, bounded, stable, and readable; it reveals required interaction targets without exposing invalid space; occlusion handling and restore are deterministic; reduced motion is honored. | `L`: `camera.jsonl` with requested/actual pose, bounds, zoom, occluder state, selected target, correction, and restore digest. | Continuous pan/zoom/selection/restore video plus fixed-anchor before/after stills. | Escaping bounds, unstable motion, hidden critical target, invalid rotation, occlusion failure, or failed restore is `FAIL`; missing authority for a supported camera action is `FAIL_CLOSED`. | `panBoundsPass`, `zoomBoundsPass`, `invalidSpaceVisibleCount`, `selectionVisibilityPass`, `occlusionPass`, `restoreDigestMatch`, `reducedMotionPass`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-NAV-001` Navigation | Run `RSQ-CROSS-MODE-v1`: enter through shared menu, select neighboring buildings, switch `KingdomView <-> WorldMap`, exercise close/back and a rejected action, then return to 3D and re-enter. | All required views/targets are reachable in order; world map remains preview-only; mode/view/travel remain distinct; failed requests do not mutate state; one world instance and one input map are active after settling; focus/camera restore correctly. | `L`: `navigation.jsonl`, mode/view state machine events, request/result envelopes, focus owner, scene/world-instance inventory, snapshot digests. | Uncut cross-mode and subview journey video per input class; stills at each settled mode/view and rejection. | Inaccessible critical interaction, unauthorized travel/state mutation, co-resident worlds, skipped state, or failed return is `P0` and `FAIL`; absent unlock/lease fixture is `FAIL_CLOSED`. | `expectedStates`, `observedStates`, `orderMatch`, `unauthorizedMutationCount`, `activeWorldCountPass`, `exclusiveInputMapPass`, `focusRestorePass`, `snapshotDigestMatch`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-CMB-001` Combat readability | Run `RSQ-2_5D-STRATEGIC-COMBAT-v1` through default, peak accepted density, objective contest, result, unavailable/reconnect, non-color, and reduced-effect states. | Self, party, highest threat, hostile, allegiance, objective owner/progress/timer, route, actionable/unavailable state, and result are identifiable without color alone; strategic projection never contradicts authoritative combat state. | `L`: `combat_readability.jsonl`, projected authority digests, actor/marker/effect counts, objective/result timeline, stale/reconnect events. | Uncut strategic encounter video per preset/locale; stills at peak density, contest, result, and reconnect states. | Hidden/misleading threat or objective, stale authority presented as current, color-only allegiance, or actionable falsehood is `P1` and `FAIL`; absent strategic fixture/authority digest is `FAIL_CLOSED`. | `requiredSemanticStates`, `observedSemanticStates`, `hiddenThreatCount`, `objectiveTruthPass`, `routeTruthPass`, `nonColorPass`, `authorityDigestMatch`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-UI-001` UI composition | Run GS-05 normal navigation, placement, selection/inspector, queue, resource, loading, failure, map-open, and HUD transition states for every admitted viewport, safe area, locale, text scale, and input class. | At least the active authority's required unobstructed safe-area share remains visible; dock, inspector, queue, minimap, resource and status hierarchy are readable; combat HUD collapses/restores truthfully; no critical clipping/overlap/fallback glyph. | `L`: `ui_composition.jsonl`, safe-area/world-visible geometry, text bounds, focus path, component state, HUD transition and overlap checks. | Still for every composition/state including 150–200% text; continuous placement, queue, inspector, focus, and cross-mode HUD video. | Any critical obstruction, clipped action/state, false queue/resource state, failed HUD restoration, or unreachable control is `P1` and `FAIL`; missing required form factor is `FAIL_CLOSED`. | `worldVisibleShare`, `worldVisibleThresholdId`, `dockPass`, `inspectorPass`, `queueTruthPass`, `criticalOverlapCount`, `textScalePass`, `hudRestorePass`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-PERF-001` Performance | Run the active authority's warmup and measured soak over idle life, continuous pan/zoom, selection, placement preview, construction transition, maturity switch, and strategic-density state on each required device/tier. | Every active frame-time, hitch, memory, allocation/GC, atlas/residency, streaming, thermal, battery, rendering, and interaction-response criterion passes without hiding state, reducing targets below limits, or visible oscillation. | `L`: `performance.json`, raw telemetry, raw Unity profiler, atlas/residency/streaming and quality-scaling events, thermal/power/device capabilities. | Timestamp-synchronized full soak or required sampled video; start/peak/end stills at fixed anchors and UI state. | Threshold miss or unavailable mandatory capability is `FAIL`; missing/unapproved thresholds, short soak, Editor-only evidence, or nonphysical evidence where physical is required is `FAIL_CLOSED`. | `thresholdSetId`, `warmupSeconds`, `measuredSeconds`, `sampleCount`, `frameTimePercentiles`, `hitchResult`, `memoryResult`, `residencyResult`, `thermalResult`, `interactionResponseResult`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-SAVE-001` Save continuity | Clone `RSQ-SAVE-CONTINUITY-v1`; enter kingdom, apply one accepted receipt and fixed construction/state delta, save, terminate, relaunch, reload, verify, return to 3D, re-enter, then repeat idempotently. | Realm, private grid, structures/levels, resources, bounded queue, accepted receipt progress, mode eligibility, map/view state, and public-avatar snapshot relationship survive exactly; no private state becomes public-world geometry. | `L`: `save_continuity.jsonl`, receipt/delta IDs, before/after snapshot and generation hashes, mode-switch linkage, ledger/marker hashes, restart/idempotence records. | Video from entry/action through restart/cross-mode verification; stills of visible pre-save and restored kingdom/3D states; no raw save bytes or PII. | Loss, duplication, rollback drift, cross-realm contamination, private/public namespace leak, unsafe write, or unexplained mismatch is `P0` and `FAIL`; unavailable fixture/receipt evidence is `FAIL_CLOSED`. | `fixtureId`, `receiptIds`, `snapshotDigestBefore`, `snapshotDigestAfter`, `generationHashesPreserved`, `privatePublicIsolationPass`, `idempotencePass`, `liveSaveTouched`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-NAR-001` Narrative execution | Run `RSQ-NARRATIVE-<realm>-vN` through the accepted lordship predicate, menu availability, kingdom entry, one mode-appropriate objective/consequence, save, restart, resume, and return for each locale. | Unlock occurs only from the accepted narrative predicate; visible objective/dialogue/state agrees with catalog and persistence; no raw realm-selection flag, scene name, or local guess unlocks the mode; no skipped/duplicate/wrong-realm state. | `L`: `narrative.jsonl`, predicate inputs/result, checkpoint order, menu availability reason, objective/consequence, catalog hash, save/resume linkage. | Uncut unlock-to-entry and resume-to-return video per locale; stills at locked, available, objective, completion, and resumed states. | Unauthorized unlock, wrong realm/order, regression, disconnected packaged path, or persistence mismatch is `P0` and `FAIL`; missing authored realm scenario/predicate authority is `FAIL_CLOSED`. | `requiredPredicateId`, `predicateSourcePass`, `expectedCheckpointIds`, `observedCheckpointIds`, `orderMatch`, `unauthorizedUnlockCount`, `persistResumeMatch`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-INP-001` Input | Replay shared-menu entry/exit, pan, zoom, building select, dock, placement/rotation where authorized, inspector, queue, map/view switch, confirm, cancel/back, and focus restoration with every required input class. | Every critical action is reachable and activates once; keyboard `B` opens construction only in settled kingdom mode while controller `B` remains Back; information and authority do not differ by input; no conflicting input maps remain. | `L`: `input.jsonl` with device, binding/action, phase, result, glyph, focus owner, active input maps, duplicate/missed activation, and keyboard/controller `B` disposition. | Continuous video per input class and one device-switch sequence; stills of glyph/focus/action states. | Unreachable critical action, duplicate activation, wrong `B` behavior, input-based information advantage, or competing maps is `P0` and `FAIL`; required unavailable hardware is `FAIL_CLOSED`. | `requiredActions`, `observedActions`, `missedCount`, `duplicateCount`, `keyboardBPass`, `controllerBPass`, `informationParityPass`, `exclusiveInputMapPass`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-ACC-001` Accessibility | Replay kingdom navigation, placement, strategic state, map modal, captions, errors, and cross-mode transitions under every required accessibility preset in both locales. | 200% text and safe areas preserve critical management/strategic truth; focus is visible/contained/restored; states are non-color-only; reduced effects retain semantic warnings; audio-off captions and remapped controls preserve access. | `L`: `accessibility.jsonl`, preset state, semantic cue inventory, text/safe-area and focus checks, caption events, reduced-effect state, remap results. | Still for default versus each preset at fixed anchors; continuous reduced-effect strategic and keyboard/controller focus videos. | Loss of state truth, unreadable/clipped critical text, color-only meaning, absent caption parity, unsafe effect, or unreachable remap is `P1` and `FAIL`; unsupported mandatory capability is `FAIL_CLOSED`. | `text200Pass`, `safeAreaPass`, `focusContainRestorePass`, `nonColorPass`, `reducedMotionPass`, `reducedFlashPass`, `reducedVfxPass`, `audioOffCaptionPass`, `remapPass`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-LOC-EN-001` English presentation | Run `RSQ-LOCALE-v1` with `locale=en-US` across kingdom, strategic, map, narrative, save/receipt, loading, stale/offline, error, and result states at every required viewport/text scale. | Approved English labels, captions, quantities, times, parameters, prompts, realm terms, and narrative meaning render without raw keys, fallback glyphs, truncation, overlap, or contradictory authority. | `L`: `localization.jsonl`, resolved keys, font/fallback scanner, text bounds, parameter/plural/time checks, caption timing, locale events. | Still of every text-heavy and failure state; continuous narrative, construction/receipt, map, and caption video. | Raw/missing key, critical truncation, fallback glyph, bad parameter/plural/time, mistimed caption, or meaning conflict is `P1` and `FAIL`; unapproved copy is `FAIL_CLOSED`. | `locale`, `keysExpected`, `keysResolved`, `rawKeyCount`, `fallbackGlyphCount`, `criticalTruncationCount`, `parameterFormatPass`, `captionTimingPass`, `semanticReviewPass`, `technicalResult`, `reasonCode`. |
| `RSQ-2_5D-LOC-KO-001` Korean presentation | Repeat the exact `RSQ-LOCALE-v1` state, seed, anchors, viewports, and text scales with `locale=ko-KR`; use approved Korean font/copy authority. | Approved Korean labels and captions preserve management, strategic, and narrative meaning with correct Hangul coverage, line breaking, counters/parameters, prompts, and timing; no unintended English fallback. | `L`: `localization.jsonl`, resolved keys, Hangul/font/fallback scanner, text bounds, counter/parameter checks, caption timing, unintended-English scanner. | Same state-for-state still/video inventory as the English row, captured independently from the Korean packaged run. | Missing Hangul, unintended English, raw key, critical truncation, fallback glyph, parameter error, mistimed caption, or semantic divergence is `P1` and `FAIL`; absent approved Korean copy/font is `FAIL_CLOSED`. | `locale`, `keysExpected`, `keysResolved`, `hangulCoveragePass`, `unintendedEnglishCount`, `fallbackGlyphCount`, `criticalTruncationCount`, `parameterFormatPass`, `captionTimingPass`, `semanticReviewPass`, `technicalResult`, `reasonCode`. |

## 7. Packet completeness and independent review

A mode packet is complete only when:

- all twelve check IDs for that exact mode, realm, candidate, platform scope, and required
  run cube are present;
- the immutable packet manifest records `evidenceOwner`, `manifestSha256`,
  `evidenceOwnerSignature`, `signatureMethod`, and `signedUtc` before independent review;
  an unsigned packet is `FAIL_CLOSED`, and a digest or sidecar alone is not a signature;
- every row is `PASS` and independently reviewed;
- the packet manifest and every artifact hash verify;
- no artifact path or ID belongs to the other mode;
- all defects are dispositioned under section 4.1;
- the full integrated deterministic QA report for the same source/build is `passed`;
- save and narrative identities match the packaged Player;
- all evidence remains accessible through the governing retention window.

Independent reviewer dispositions are `PASS`, `FAIL`, or `FAIL_CLOSED`. The reviewer MUST
verify row counts programmatically, run-envelope equality, realm/mode namespaces, hashes,
raw-media duration and continuity, expected-versus-observed fields, and defect links.
The implementer or evidence owner cannot independently review the same packet.

A mode packet passing technical review does not approve its creative result and does not
advance a realm.

## 8. Four separate owner decisions per realm

After both mode packets pass independent review, the game owner records four separate,
immutable decisions against the exact candidate and packet IDs:

| Decision ID pattern | Subject | Allowed owner decision | Effect of `APPROVE` |
| --- | --- | --- | --- |
| `RSQ-OWNER-3D-<realm>-<sequence>` | Exact `Adventure3D` packet and its limitations | `APPROVE`, `REVISE`, `REJECT`, `REOPEN` | Approves only the named realm's 3D qualification evidence. |
| `RSQ-OWNER-2_5D-<realm>-<sequence>` | Exact `Kingdom2_5D` packet and its limitations | `APPROVE`, `REVISE`, `REJECT`, `REOPEN` | Approves only the named realm's 2.5D qualification evidence. |
| `RSQ-OWNER-CREATIVE-<realm>-<sequence>` | Side-by-side creative/visual coherence of both separately approved modes | `APPROVE`, `REVISE`, `REJECT`, `REOPEN` | Approves the named realm's creative/visual result; it does not merge mode evidence. |
| `RSQ-OWNER-ADVANCE-<realm>-<sequence>` | Authorization to open the next realm gate, or close the sequence after Umbral | Exact authorization from section 9 | Opens only the named next step. |

Every record includes owner identity, authoritative Kanban task/comment/event ID, exact
verbatim decision, candidate and packet IDs/hashes, limitations/non-waivers, UTC time, and
supersession links. A PR merge, green CI, technical pass, task completion, reaction,
silence, schedule, or prior realm decision is not owner approval.

`REVISE`, `REJECT`, or `REOPEN` leaves the affected gate closed. An owner cannot mark a
mode `APPROVE` while its technical packet is `FAIL` or `FAIL_CLOSED`.

## 9. Strict realm order and advancement vocabulary

Only one realm qualification gate may be open at a time.

| Realm | Entry requirement | Required authorization after approval |
| --- | --- | --- |
| `Stonehold` | Protocol approved; capture harness and evidence registry validated; no prior realm. | `ADVANCE_TO_ELDERGROVE` |
| `Eldergrove` | All four Stonehold owner records exist and Stonehold authorization is exactly `ADVANCE_TO_ELDERGROVE`. | `ADVANCE_TO_CROWNLANDS` |
| `Crownlands` | All four Eldergrove owner records exist and Eldergrove authorization is exactly `ADVANCE_TO_CROWNLANDS`. | `ADVANCE_TO_UMBRAL` |
| `Umbral` | All four Crownlands owner records exist and Crownlands authorization is exactly `ADVANCE_TO_UMBRAL`. | `COMPLETE_REALM_SEQUENCE` |

The advancement record is valid only after the same realm's 3D, 2.5D, and creative owner
decisions are all `APPROVE` and current. Pre-authoring or asset preparation may exist
under separate approved production authority, but later-realm qualification setup,
evidence collection, review, owner disposition, and gate advancement MUST remain closed.
No evidence captured while a realm gate was closed can be grandfathered into a pass.

If a prior realm reopens, any not-yet-exercised downstream authorization is suspended.
Already approved downstream evidence is reopened only when the trigger's impact analysis
shows dependency on the changed system/content; history is preserved in all cases.

## 10. Stop-ship rules

Any condition below sets the affected realm/mode to `STOP_SHIP`, freezes advancement, and
requires a durable incident/defect record plus containment:

1. Missing 3D or 2.5D packet, or any attempt to merge, average, share, overwrite, rename,
   cross-reference as a substitute, or approve the modes together.
2. Unapproved visual divergence from the owner-approved realm source, including a palette
   swap presented as structural identity or a mode that contradicts the other mode's
   approved creative direction.
3. Any critical interaction inaccessible through a supported required input or
   accessibility configuration.
4. Qualification, review, approval, or advancement attempted out of the strict realm
   order in section 9.
5. Save loss, corruption, silent reset/downgrade, cross-realm contamination, private/public
   namespace leak, unsafe rollback, or continuity mismatch.
6. Narrative skip, duplication, wrong-realm path, unauthorized unlock, persistence/resume
   regression, missing approved English/Korean meaning, or editor-only path presented as
   packaged evidence.
7. Realm advancement without current explicit owner approval for 3D, 2.5D,
   creative/visual result, and the exact advancement authorization.
8. Wrong, dirty, stale, unsigned/unreviewed, inaccessible, hash-mismatched, mixed-build,
   mixed-candidate, wrong-platform, wrong-locale, truncated, edited-only, or incomplete
   evidence.
9. Missing mandatory accessibility, input, performance, locale, screenshot, video,
   profiler, log, or defect-disposition evidence.
10. A mandatory expected result failed even when another row, realm, mode, device, locale,
    score, average, or owner preference is positive.

Containment is scoped: disable or revert only the unapproved realm or presentation path to
its last owner-approved baseline, keep the other mode unchanged when unaffected, preserve
all evidence and save generations, and rerun the complete impacted packet. Never delete
failed evidence, rewrite approval history, regenerate hashes to conceal drift, reset a
player profile, force-reset `main`, or infer destructive restore authority. If the last
approved baseline is unsafe, incompatible, or unverifiable, remain fail-closed.

## 11. Reopen triggers and impact rules

Every reopen creates a new candidate/gate record, retains the previous approval as
historical, identifies affected packets and downstream decisions, and requires complete
reruns for the impacted rows. The smallest provably complete scope may reopen; uncertainty
expands the scope and fails closed.

| Trigger | Minimum reopened scope | Required reruns/evidence |
| --- | --- | --- |
| Renderer, render pipeline, shader framework, lighting stack, quality-tier, upscaler, streaming, LOD, atlas, or residency change | Every affected platform/quality and mode using the changed path; both modes if shared. | Rendering, camera where framing changes, combat readability, UI composition, performance, accessibility, both locales, creative/visual owner review. |
| Camera controller, projection, FOV/zoom, collision, occlusion, shake, recenter, anchor, or restore change | Affected mode and every realm using the controller/preset; both modes if shared. | Camera, navigation, combat readability, UI composition, input, accessibility, media at all fixed anchors. |
| Realm art, model, texture, material, VFX, animation, environment, architecture, UI art, font, or approved source change | Exact realm/mode containing the asset; all realms/modes if shared. | Rendering plus any affected camera/navigation/combat/UI/performance/accessibility/locale rows; mode and creative owner decisions. |
| Combat rules, telegraph, effect source, target/party/objective projection, density ladder, control-state, or action timing change | Every mode/realm whose combat or strategic projection consumes the change. | Combat readability, UI composition, performance, input, accessibility, locale rows containing combat semantics. |
| Narrative catalog, key, predicate, branch, consequence, scene wiring, quest UI, persistence, or resume change | Every affected realm, both modes and both locales when they consume the changed path. | Narrative, save continuity when persisted data is touched, UI/input/accessibility, English and Korean; packaged Player and catalog identity proof. |
| Platform, device tier, OS, graphics API, input device, build target, Unity editor/exporter, or packaging change | Changed platform/device scope for every approved realm and both modes; no other platform is evidence for it. | Every matrix row required by the platform, exact build/equivalence evidence, capability records, and new platform-scoped owner decisions. |
| Accessibility token, contrast, text scale, safe area, focus, remapping, caption, reduced-motion/flash/VFX, semantic cue, or assistive path change | Every affected viewport/input/locale in both modes; all realms if component/token is shared. | Accessibility, UI composition, input, combat readability, camera where motion changes, rendering where cues change, English and Korean. |
| Save schema, fixture, migration/recovery, projection, receipt, snapshot, or cross-mode state change | Both modes and every realm/state using the changed data. | Both save-continuity rows, narrative where state is persisted, cross-mode navigation, old-save fixtures, failure/recovery/idempotence evidence, migration owner gate. |
| Evidence schema, harness, capture tool, encoder, profiler, scenario, seed, logical clock, threshold set, or reviewer-independence change | Every packet whose completeness or result depended on the changed control. | All affected rows in full; prior media or summaries cannot be rewrapped under the new version. |
| Missing/expired artifact, hash/access failure, incident, escaped defect, candidate/build/catalog drift, or explicit owner `REOPEN` | Exact dependent packet; expand through shared dependencies and downstream authorizations. | Replace through a new packet, rerun all impacted rows, independent review, and all invalidated owner decisions. |

A realm-specific art change does not automatically invalidate unrelated realms or the
other mode. A shared renderer, camera, combat, platform, localization, save, or
accessibility change normally does. The impact record MUST prove why any approval is
retained; absence of proof reopens it.

## 12. Qualification record template

Each realm record MUST contain these fields. Blank fields fail closed.

```text
Protocol ID and version:
Realm and ordinal:
Candidate ID and source/build/catalog identities:
Entry-gate authorization record:
Adventure3D packet ID, hash, reviewer, disposition:
Kingdom2_5D packet ID, hash, reviewer, disposition:
Cross-mode non-substitution validation:
Open/closed defect inventory:
Stop-ship incident inventory:
Adventure3D owner decision record:
Kingdom2_5D owner decision record:
Creative/visual owner decision record:
Advancement owner decision record and exact authorization:
Last approved realm/mode baseline IDs:
Reopen-trigger impact analysis:
Rollback/containment target and verification:
Final realm state: FAIL_CLOSED | STOP_SHIP | OWNER_REVIEW | APPROVED | REOPENED
Created/updated UTC:
Supersedes/superseded-by:
```

## 13. Operator closeout checklist

- [ ] Realm ordinal and entry authorization match the strict order.
- [ ] Candidate is clean, immutable, packaged, and identity-bound.
- [ ] Twelve `Adventure3D` rows exist and pass for the complete required run cube.
- [ ] Twelve `Kingdom2_5D` rows exist and pass for the complete required run cube.
- [ ] 3D and 2.5D namespaces, artifacts, findings, defects, reviews, and approvals are separate.
- [ ] Every row has deterministic setup, expected/observed result, packaged logs, raw media,
      hashes, defect disposition, and pass/fail fields.
- [ ] English and Korean are separate state-for-state packaged runs in both modes.
- [ ] Save, narrative, input, accessibility, and performance evidence matches the same build.
- [ ] Independent reviewers signed both complete mode packets.
- [ ] Owner 3D, owner 2.5D, owner creative/visual, and owner advancement decisions are separate,
      explicit, current, and bound to exact packet IDs.
- [ ] No stop-ship condition or reopen trigger remains unresolved.
- [ ] Only the exact authorized next realm is opened; after Umbral, the sequence is closed.

## 14. Versioning and change control

Protocol changes use semantic versioning. A major version changes authority, realm order,
mode separation, mandatory checks, owner decisions, or result semantics. A minor version
adds compatible fields or checks. A patch clarifies language without changing evidence
requirements. Every change includes an impact analysis and migration rule for open and
approved records.

No protocol version may merge 3D and 2.5D evidence, weaken packaged-build requirements,
turn missing evidence into a pass, infer owner approval, reorder realms, remove English or
Korean, or permit advancement without all four owner records. Such a change is invalid and
fails closed.

## 15. Recon coverage

The bounded qualification-authority corpus contained thirteen tracked documents and policy
or schema files totaling 2,947 lines. All 2,947 lines were read in full (100%). Binary
assets, generated evidence bodies, implementation source outside the named qualification
interfaces, and unrelated gameplay/design documents were not line-read and are not
included in that percentage.
