# AnotherLife Post-MVP Graphics Benchmark Specification

**Status:** Active owner direction; numeric thresholds marked `PROVISIONAL` remain
engineering hypotheses until measured and accepted

**Decision date:** 2026-08-25

**Quality authority:** [`../PostMVP_Graphics_And_UI_Quality_Standard.md`](../PostMVP_Graphics_And_UI_Quality_Standard.md)

**Broad systems benchmark:** [`../AnotherLife_Competitive_Experience_Benchmark.md`](../AnotherLife_Competitive_Experience_Benchmark.md)

**Source manifest:** [`PostMVP_Graphics_Benchmark_Source_Manifest_2026-08-25.json`](PostMVP_Graphics_Benchmark_Source_Manifest_2026-08-25.json)

**Scorecard template:** [`Templates/PostMVP_Golden_Scene_Scorecard.md`](Templates/PostMVP_Golden_Scene_Scorecard.md)

## 1. Purpose

This specification turns the owner-approved post-MVP visual direction into repeatable,
auditable evidence. It defines the benchmark ladder, target-device classes, five golden
scenes, capture protocol, mandatory pass/fail gates, and owner approval package.

It does not claim parity with a reference product. Reference products provide bounded
questions and comparison material only. AnotherLife must remain original.

## 2. Decision summary

- Market position: premium AA stylized realism with selected near-AAA hero moments.
- Binding performance floor: representative 2022–2023 mid-range Android class.
- Frame-rate contract: stable 30 FPS on the Android floor; 60 FPS modes on high-end
  mobile and PC.
- Scaling: adaptive resolution, LOD, streaming, and upscaling are encouraged when
  transitions remain unobtrusive.
- Evidence: golden-scene comparisons, instrumented captures, usability/accessibility
  checks, and blinded player feedback.
- Authority: objective gates pass first; the owner gives final creative approval.
- Production response to constraint: reduce breadth and reuse modular content before
  allowing visible quality to fall.

## 3. Benchmark ladder

| Level | Reference | Bounded use | Explicit non-use |
| --- | --- | --- | --- |
| Minimum floor | AnotherLife golden scenes | Cohesive premium-AA finish, truthful gameplay information, stable target-device performance | Current MVP appearance is not the final floor |
| Direct scalable-action comparator | Wuthering Waves | Cross-platform open-world presentation, action readability, mobile quality scaling, controller/touch comparison | Not evidence for realm-vs-realm population, server capacity, or identical art direction |
| Strategic-management comparator | Infinity Kingdom | City-management density, visible progression, compact state and action surfaces | Do not copy its art style, city layout, heroes, dragons, economy, or monetization |
| Aspirational specialist | Black Desert | Character creator depth, character presentation, information-rich interface and map polish | Do not copy UI skins, layouts, fonts, icons, costumes, character shapes, or its configurable-HUD policy |
| Aspirational specialist | THRONE AND LIBERTY | Landscape composition, architectural landmarks, capital/fortress scale, mass-combat effect-source control | Do not claim mobile suitability or AnotherLife capacity from its PC/console presentation |

Black Desert's official creator guide documents detailed hair, face, body, color,
material, pose, history, save/load, and preview controls; these are comparison categories,
not a copied feature list.[1] Its official interface guide documents movable/optional
windows and presets, but AnotherLife's owner instead chose a fixed authored HUD with
minimal general customization.[2] Its official world-map guide describes a 3D map with
layered information, navigation, location, transport, surroundings, character, and
management functions; this supports information-hierarchy questions without making its
layout or feature count an AnotherLife requirement.[15]

Wuthering Waves is officially presented across Android and other platforms and publishes
device requirements, but it is an open-world action comparator rather than proof of
massive realm-vs-realm concurrency.[3] Large-battle capacity and interest management must
be proven by AnotherLife load tests and the broader competitive benchmark.

THRONE AND LIBERTY's world-design material is useful for authored vistas, landmark
silhouettes, material/architecture contrast, and visual storytelling.[4] Its official
update history also demonstrates source-scoped VFX visibility controls for large combat,
which supports AnotherLife's protected-information approach without copying the setting
names or UI.[5]

Infinity Kingdom's official store presentation connects city rebuilding, growth, armies,
and world-map activity; AnotherLife uses it only to ask whether management state and city
change are immediately legible.[6]

## 4. Reference-capture rules

1. Use legally obtained current builds, official screenshots, official trailers, or
   official guides.
2. Record product, version/build, platform, graphics preset, resolution, capture date,
   scene/screen, and source URL.
3. Store URLs and AnotherLife observations by default; do not commit third-party media
   binaries unless rights and repository policy explicitly permit it.
4. Crop only to isolate a comparison question. Never remove logos or attribution to make
   third-party material look like AnotherLife work.
5. Compare principles and outcomes, not pixel similarity.
6. Every comparison row states `borrow`, `adapt`, and `avoid`.
7. A benchmark image never approves an AnotherLife asset.

## 5. Device matrix

### 5.1 Binding Android floor

Use a representative **2022–2023 upper-mid-range Android class** with:

- 6 GB RAM as the minimum test configuration; include an 8 GB variant when available;
- approximately FHD+ landscape rendering surface;
- Vulkan support;
- a 2022–2023 6 nm-class mid/high-tier SoC family;
- production thermal behavior, not an emulator-only result.

The Samsung Galaxy A54 5G is the provisional primary physical anchor because Samsung's
2023 specification exposes 6 GB and 8 GB variants, an FHD+ 120 Hz display, and a 5,000
mAh battery.[7] Android Central's report on Canalys' 2023 shipment ranking places the
Galaxy A54 5G among that year's ten most-shipped phone models worldwide, making it a
defensible market-relevant anchor rather than an obscure development handset.[14]
Samsung classifies Galaxy A as a mid-range line, identifies A54 as the 2023 successor to
A53, and identifies the A54 processor as Exynos 1380.[16] Samsung's processor
documentation identifies that family as a four-performance/four-efficiency CPU with a
five-core Mali-G68 GPU; these architectural facts do not substitute for sustained device
measurement.[17]

The provisional physical candidate set is:

| Candidate | Role | Evidence boundary |
| --- | --- | --- |
| Galaxy A54 5G, lowest available physical-RAM SKU | Primary binding-floor candidate | Market-relevant 2023 Exynos/Mali device; requires heat-soaked AnotherLife evidence |
| Galaxy A34 5G, 6 GB/128 GB where available | Secondary 2023 cross-SoC candidate | Third-party specifications identify Dimensity 1080, Mali-G68 MC4, FHD+, and 6 GB configurations.[19] MediaTek's official platform page supplies the SoC architecture, not device performance.[9] |
| Galaxy A53 5G, lowest available physical-RAM SKU | Conservative 2022 regression candidate | Samsung launched the model in 2022 with a 5 nm processor and positions the A series as its most popular Galaxy category; no stable AnotherLife result is implied.[18] |

These three candidates do not satisfy OEM diversity by themselves. Before beta, the
survey must add at least one non-Samsung Adreno-class physical device around Snapdragon
778G or comparable capability. Qualcomm describes Snapdragon 778G as a 6 nm high-tier
platform with an Adreno 642L GPU and gaming-oriented features.[8]

`PROVISIONAL`: Final device SKUs depend on regional availability, physical acquisition,
Unity/graphics-API compatibility, sustained thermal tests, and owner release-market
selection. An emulator cannot certify the floor.

### 5.2 Additional tiers

| Tier | Purpose | Selection rule |
| --- | --- | --- |
| Android floor | Binding 30 FPS and usability gate | 2022–2023 physical mid-range class above |
| Android diversity | Vendor/GPU/thermal variance | At least one additional OEM and a different GPU family |
| High-end mobile | Optional 60 FPS quality mode | Current supported flagship-class physical device selected before beta |
| PC floor | Stable desktop gameplay and content-authoring comparison | Final Windows minimum selected before closed alpha |
| PC high | 60 FPS high-quality presentation ceiling | Representative discrete-GPU system selected before golden-scene finalization |

No device is inferred to pass from chipset name alone. OEM cooling, memory, drivers,
resolution, power policy, and operating-system state remain part of the test record.

## 6. Performance contract

Unity recommends frame-time budgets rather than average FPS alone and identifies 33.33
ms for 30 FPS and 16.66 ms for 60 FPS.[13] Every performance record therefore includes
frame-time distributions, not only an average.

### 6.1 Mandatory measurements

- CPU and GPU frame time: p50, p90, p95, and p99;
- count and duration of gameplay hitches;
- target and delivered frame rate plus frame pacing;
- input-to-visible-response samples for combat actions;
- system memory, Unity memory, graphics memory estimate, and peak allocation;
- allocations per frame and garbage-collection events;
- draw calls/batches, triangles/vertices, overdraw risks, and active renderer count;
- texture residency/streaming, shader compilation, and asset-streaming stalls;
- full/fallback/nameplate actor counts and animation update tiers;
- particle count, VFX source category, shadow, foliage, decal, and post-process state;
- resolution/render scale, upscaler, LOD, view distance, and quality preset;
- device temperature/thermal status, power state, battery delta, and test duration;
- build ID, catalog fingerprint, scene seed, device model, OS, driver/API, and capture tool.

Unity's Profiler supports collecting performance information from intended release
platforms, so editor-only evidence is insufficient.[12] Profiling must establish a
baseline before changes, track budgets during development, and prove the result after
changes.[13]

### 6.2 Provisional pass hypotheses

These are starting engineering hypotheses, not permanent release thresholds:

| Mode | Candidate frame-time gate | Candidate hitch gate | Soak |
| --- | --- | --- | --- |
| Android floor 30 FPS | p95 at or below 33.33 ms; p99 reported and investigated | No unexplained gameplay stall at or above 100 ms | At least 20 minutes after representative scene warm-up |
| High-end mobile/PC 60 FPS | p95 at or below 16.66 ms; p99 reported and investigated | No unexplained gameplay stall at or above 100 ms | At least 20 minutes after representative scene warm-up |

A run fails if target FPS is achieved only before thermal throttling, by hiding protected
information, or through visually obvious oscillation. Android guidance recommends
representative device tiers, fine-grained quality levers, smooth transitions, and long
sessions to observe thermal stabilization.[10]

Android optimized frame pacing must be enabled and verified for target builds unless a
documented incompatibility is accepted. Android's frame-pacing guidance explains that
correct pacing matters independently from average frame rate and that Unity integrates
the capability.[11]

## 7. Graceful-degradation contract

Adaptive scaling may independently reduce:

1. ambient and cosmetic particles;
2. noncritical damage-number frequency;
3. distant shadow reach and soft-shadow quality;
4. reflection, fog, weather, decal, and secondary light density;
5. foliage density and distant small props;
6. texture mip bias within accepted material-read limits;
7. distant animation frequency and noncritical model detail;
8. render scale within the accepted image-stability range.

It may not reduce or remove:

- the player, current target, attackers, or actionable nearby actors;
- hostile telegraph truth, collision truth, or objective state;
- party/raid role-critical support fields;
- non-color allegiance and accessibility signals;
- a realm's defining silhouette, structural identity, or navigation landmark;
- text below the approved readable size;
- touch targets below the approved interaction size.

Scale one lever at a time where feasible. Avoid abrupt preset jumps, obvious LOD popping,
texture thrash, exposure pumping, UI rescaling, and rapidly oscillating render scale.

## 8. Golden benchmark suite

### GS-01 — Character creator and class reveal

**Questions:** Does the subject reach the close-camera bar? Are skin, hair, eyes, cloth,
metal, and magic distinct? Is customization deep but understandable? Does class and realm
identity survive without labels?

**Required captures:**

- default and two deliberately diverse characters;
- face, hair, eye, skin, body, and material edits;
- live orbit/zoom, preview pose, reset, randomize, undo/redo, and save feedback;
- phone, tablet, PC, reduced-motion, text-scale, and color-vision states;
- class reveal transition and final still;
- baseline Android and high-quality PC performance traces.

### GS-02 — Capital arrival

**Questions:** Is the arrival beautiful, grand, navigable, inhabited, and realm-specific?
Do streaming and LOD transitions preserve the city silhouette and authored reveal?

**Required captures:**

- distant approach, threshold reveal, street-level navigation, landmark view, and one
  elevated vista;
- day/night or the supported lighting extremes;
- empty, normal, and stress population states;
- low/medium/high quality with identical camera anchors;
- baseline Android thermal/streaming trace and high-quality PC capture.

### GS-03 — Open-world combat and major boss

**Questions:** Is combat responsive and spectacular without hiding threats? Does the boss
retain silhouette and mechanic clarity under maximum accepted effect load?

**Required captures:**

- solo, party, and increasing local-density ladders;
- target, attacker, party, allied, hostile, cosmetic, and ambient VFX categories;
- every major hostile telegraph with audio on and off;
- reduced motion, reduced flashes, color-vision simulation, and low effects;
- defeat, revive/recovery, objective contest, and result states;
- baseline Android sustained trace and 60 FPS-mode trace.

This scene does not certify full realm-vs-realm capacity. Distributed server load,
interest management, network behavior, and physically achievable local concentration
require separate systems tests.

### GS-04 — HUD, minimap, and map stress

**Questions:** Can a player identify vitals, target, highest threat, immediate action,
party state, objective, route, and allegiance without the HUD hiding combat?

**Required captures:**

- phone, tablet, 16:9 PC, and supported ultrawide composition;
- exploration, boss, party, squad/RvR, map-open, chat/notification, and recovery states;
- minimap zoom/filter progression and world-map agreement;
- minimum/maximum text scale, safe-area extremes, controller/touch/keyboard focus;
- grayscale and common color-vision simulations;
- five-second comprehension and interaction-time sessions.

### GS-05 — Private-kingdom 2.5D management

**Questions:** Is the kingdom attractive, organized, inhabited, continuously explorable,
and immediately manageable? Do upgrades, resources, authoritative actions, construction,
selection, and unavailable states remain legible without blocking the city view?

**Required captures:**

- representative early, middle, and mature kingdom states using approved gameplay data;
- Stonehold first, then the same acceptance anchors for Eldergrove, Crownlands, and
  Umbral when each realm slice exists;
- idle life, building selection, contextual inspector, construction/upgrade,
  completion, insufficient-resource, loading, stale/offline, and failure states;
- continuous pan/zoom, controlled camera behavior, occlusion handling, and neighboring
  target selection;
- phone, tablet, 16:9 PC, and supported ultrawide compositions;
- minimum/maximum text scale, safe-area extremes, controller/touch/keyboard focus,
  grayscale, color-vision simulation, and reduced-motion states;
- low/medium/high quality at identical camera anchors;
- the authoritative normal-navigation and placement composition with at least 60% of
  the safe-area viewport unobstructed, including 150–200% text scaling;
- the actual bounded construction queue, pre-placement rotation, active-construction
  cancellation, private-grid minimap, accepted-receipt progress, and cross-mode HUD
  collapse/restoration states;
- baseline Android sustained performance, thermal, memory, draw-call, streaming, and
  interaction-response traces.

This scene approves management presentation and 2.5D visual quality only. It does not
approve unimplemented mechanics, economy balance, monetization, layout freedom, or a
separate visual state that can disagree with gameplay authority.

## 9. Visual and usability gates

Every golden scene is evaluated with binary mandatory gates. A 1–5 comparison note may
help diagnose gaps but cannot average away a failed mandatory item.

### 9.1 Mandatory visual gates

- primary read is clear at intended gameplay distance;
- realm, role, category, and threat do not depend on color alone;
- materials remain distinct without emission;
- lighting preserves navigation, anatomy, and telegraphs;
- animation has believable weight, contact, transitions, and response;
- VFX preserves protected information;
- UI uses final or explicitly approved production typography and iconography;
- composition works on each required device class;
- no placeholder primitive, fallback font, debug label, missing asset, or temporary plate
  appears in the approval package;
- provenance and originality review pass.

### 9.2 Mandatory accessibility gates

- scalable text/UI and safe areas;
- color-independent state and accepted contrast;
- reduced motion, shake, flash, and VFX modes;
- audio-off semantic parity and captions/subtitles where applicable;
- remapping and touch/controller/keyboard navigation;
- stable focus order/restoration;
- no accessibility mode removes gameplay truth.

### 9.3 Mandatory usability gates

- five-second still test identifies required combat or management state;
- PvP HUD does not cover the protected central scan path;
- minimap and world map agree on selected objective, route, and allegiance;
- critical action and state changes produce visible acknowledged feedback;
- error, unavailable, loading, reconnecting, and rollback states are explicit;
- blinded participants can complete the defined task without comparator branding or
  coaching.

## 10. Blinded feedback protocol

1. Remove product names from the question and randomize presentation order; do not remove
   third-party attribution from stored source material.
2. Ask task questions, not preference-only questions.
3. Record participant device, familiarity, completion, errors, hesitation, and stated
   confidence.
4. Keep participant comments separate from objective profiler data.
5. Version the sample and questionnaire with the build.
6. Treat small samples as diagnostic, not statistically conclusive.
7. No player vote overrides accessibility, technical gates, originality, or the owner's
   creative decision.

## 11. Evidence package

Each review package contains:

- build ID, commit, catalog fingerprint, Unity version, platform, and device matrix;
- exact golden-scene revision and deterministic anchor/seed information;
- same-anchor stills and short videos for required quality/aspect/accessibility states;
- raw profiler captures and a summarized performance table;
- memory, thermal, battery, streaming, LOD, and hitch evidence;
- completed scorecards with every mandatory failure visible;
- benchmark `borrow/adapt/avoid` notes and source URLs;
- asset provenance, rights, and originality disposition;
- blinded feedback record;
- known gaps and explicit exclusions;
- separate owner disposition rows for 3D and 2.5D: `APPROVE`, `REVISE`, or `REJECT`.

## 12. Gate sequence

```text
quality standard locked
→ reference/source manifest current
→ device matrix physically available
→ capture pipeline reproducible
→ golden scenes deterministic
→ objective performance/readability/accessibility/provenance gates pass
→ blinded diagnostic feedback complete
→ independent 3D owner disposition
→ independent 2.5D owner disposition
→ broad realm production may proceed
```

Approval is invalid if a mandatory failure is omitted, a comparator is described as
parity, only Editor evidence exists for a target-device claim, or a benchmark capture is
presented as an AnotherLife asset.

## Sources

[1] https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=5 — Black Desert Adventurer Guide: Customization
[2] https://blackdesert.pearlabyss.com/Asia/en-US/Game/Wiki?_masterWikiNo=7 — Black Desert Adventurer Guide: Interface
[3] https://wutheringwaves.kurogames.com/en/main/news/detail/5281 — Wuthering Waves Version 3.6 Update and System Requirements
[4] https://playthroneandliberty.com/en-us/news/articles/wilds-of-talandre-world-design — THRONE AND LIBERTY: Wilds of Talandre World Design
[5] https://www.playthroneandliberty.com/en-ca/news/articles/throne-and-liberty-update-1-3-0 — THRONE AND LIBERTY Update 1.3.0
[6] https://play.google.com/store/apps/details?id=com.gtarcade.ioe.global — Infinity Kingdom on Google Play
[7] https://news.samsung.com/global/the-samsung-galaxy-a54-5g-and-galaxy-a34-5g-awesome-experiences-for-all — Samsung Galaxy A54 5G and A34 5G specifications
[8] https://qualcomm.com/news/releases/2021/05/qualcomm-announces-new-snapdragon-778g-5g-mobile-platform-showcases-mass — Qualcomm Snapdragon 778G announcement
[9] https://www.mediatek.com/products/tablets/mediatek-dimensity-1080 — MediaTek Dimensity 1080 specifications
[10] https://developer.android.com/games/optimize/adpf/best-practices-adpf — Android ADPF best practices
[11] https://developer.android.com/games/sdk/frame-pacing — Android Frame Pacing library
[12] https://docs.unity3d.com/6000.3/Documentation/Manual/Profiler.html — Unity 6.3 Profiler manual
[13] https://unity.com/how-to/best-practices-for-profiling-game-performance — Unity profiling best practices
[14] https://www.androidcentral.com/phones/samsung-apple-canalys-2023-most-shipped-report — Android Central summary of Canalys 2023 most-shipped phones
[15] https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=21 — Black Desert Adventurer Guide: World Map
[16] https://www.samsung.com/us/explore/mobile/buying-guide/best-samsung-galaxy-a-series-phone — Samsung Galaxy A Series buying guide
[17] https://semiconductor.samsung.com/us/processor/mobile-processor/exynos-1380 — Samsung Exynos 1380
[18] https://news.samsung.com/global/galaxy-a53-5g-and-galaxy-a33-5g-awesome-mobile-experiences-open-to-everyone — Samsung Galaxy A53 5G and A33 5G announcement
[19] https://www.gsmarena.com/samsung_galaxy_a34-12074.php — GSMArena Samsung Galaxy A34 specifications
