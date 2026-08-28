# Post-MVP Golden Scene Scorecard

Copy this template once per scene, build, platform, device, and graphics preset. Do not
merge 3D and 2.5D approval into one scorecard.

## Identity

| Field | Value |
| --- | --- |
| Golden scene | `GS-01 / GS-02 / GS-03 / GS-04 / GS-05` |
| Scene revision | |
| Build ID / commit | |
| Catalog fingerprint | |
| Unity version | |
| Platform / device / OS | |
| CPU / GPU / RAM | |
| Graphics API | |
| Resolution / render scale / upscaler | |
| Quality preset | |
| Capture date and operator | |
| Deterministic seed / anchor | |
| Thermal/power starting state | |

## Reference boundary

| Comparator | Version/platform/source | Borrow | Adapt for AnotherLife | Avoid |
| --- | --- | --- | --- | --- |
| Black Desert | | | | |
| Wuthering Waves | | | | |
| THRONE AND LIBERTY | | | | |
| Infinity Kingdom | | | | |

Delete comparator rows that are irrelevant to the scene. Never score pixel similarity.

## Mandatory objective gates

Use `PASS`, `FAIL`, `BLOCKED`, or `N/A` with evidence. `N/A` requires a reason.

| Gate | Result | Evidence | Notes / corrective action |
| --- | --- | --- | --- |
| Intended frame-rate/frame-time contract | | | |
| Frame pacing and hitch contract | | | |
| Sustained thermal behavior | | | |
| Memory and allocation budget | | | |
| Streaming/residency behavior | | | |
| LOD/impostor/quality transitions | | | |
| Primary read and gameplay silhouette | | | |
| Realm/role/threat identity beyond color | | | |
| Material distinction without emission | | | |
| Lighting and navigation clarity | | | |
| Animation weight/contact/transitions | | | |
| VFX protected-information contract | | | |
| UI/HUD hierarchy and central scan path | | | |
| Phone/tablet/PC composition as required | | | |
| Minimap/world-map agreement as required | | | |
| Text/UI scaling and safe areas | | | |
| Contrast and color-independent state | | | |
| Reduced motion/shake/flash/VFX | | | |
| Audio-off/caption semantic parity | | | |
| Input navigation/remapping/focus | | | |
| Provenance and rights traceability | | | |
| Originality/non-copy review | | | |
| No placeholder/debug/fallback presentation | | | |

**Objective gate result:** `PASS / FAIL / BLOCKED`

Any mandatory `FAIL` prevents an objective pass. A `BLOCKED` item remains visible and
prevents final approval unless the owner explicitly removes that criterion from scope.

## Performance record

| Metric | p50 | p90 | p95 | p99 | Peak / count | Budget | Result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| CPU frame time (ms) | | | | | | | |
| GPU frame time (ms) | | | | | | | |
| Delivered frame time (ms) | | | | | | | |
| Input-to-visible response (ms) | | | | | | | |
| Gameplay hitches | | | | | | | |
| System memory | | | | | | | |
| Unity memory | | | | | | | |
| Graphics memory estimate | | | | | | | |
| Allocations / GC | | | | | | | |

| Additional field | Value |
| --- | --- |
| Draw calls / batches | |
| Triangles / vertices | |
| Active full/fallback/nameplate actors | |
| Particle/VFX counts by source | |
| Texture residency / streaming stalls | |
| Shader compilation events | |
| Thermal status/headroom | |
| Battery delta and duration | |
| Quality-scaling events | |
| Raw capture paths | |

## Five-second comprehension test

| Required identification | Correct participants | Total | Common error |
| --- | ---: | ---: | --- |
| Player state / selected subject | | | |
| Target / selected building | | | |
| Highest threat / blocking state | | | |
| Immediate legal action | | | |
| Objective / route / next result | | | |

## Blinded task feedback

| Field | Value |
| --- | --- |
| Protocol/questionnaire revision | |
| Participant count | |
| Device/input distribution | |
| Task completion | |
| Error/hesitation themes | |
| Confidence themes | |
| Preference themes | |
| Raw anonymized record | |

Feedback is diagnostic. It cannot override a mandatory objective failure or owner ruling.

## Comparative diagnostic notes

Use 1–5 only to locate gaps. These values are not averaged into approval.

| Dimension | AnotherLife note | Comparator note | Diagnostic level | Required change |
| --- | --- | --- | ---: | --- |
| Composition / primary read | | | | |
| Character / architecture fidelity | | | | |
| Materials / lighting | | | | |
| Animation / camera | | | | |
| VFX / combat clarity | | | | |
| UI/HUD / map clarity | | | | |
| Kingdom density / management clarity | | | | |
| Accessibility finish | | | | |
| Originality / AnotherLife identity | | | | |

## Known gaps and exclusions

-

## Independent owner dispositions

| Mode | Objective gate | Owner disposition | Required revision / approval note | Date |
| --- | --- | --- | --- | --- |
| 3D | `PASS / FAIL / BLOCKED / N/A` | `APPROVE / REVISE / REJECT / N/A` | | |
| 2.5D | `PASS / FAIL / BLOCKED / N/A` | `APPROVE / REVISE / REJECT / N/A` | | |

Approval in one row never approves the other. No weighted average, benchmark score, or
participant vote overrides a mandatory gate or owner `REVISE`/`REJECT`.
