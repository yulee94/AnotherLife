# First-User Experience Evidence Coverage — 2026-08-12

Status: A1-approved coordination/review evidence and P0 no-go record; draft PR; not a production, runtime, visual, player, merge, or release approval.

Baseline: main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0.

Related coordination record: #463.

This document publishes the approved read-only A7 evidence-coverage audit. It records what the retained first-user media and previs artifacts can prove, what they cannot prove, and the smallest ordered evidence plan. No machine-local binary, render, scene, media file, cache, or generated output is copied into the repository.

## Authority and master contract

The planning-approved editorial records are:

| Record | Bytes | SHA-256 | Approval ceiling |
|---|---:|---|---|
| FIRST_RUN_CINEMATIC_60S_EDL_V001.md | 18,669 | 370ef3936164de570641bad2f36d54cacafc502ae41e228b0d955b40027f895e | Editorial planning only |
| FIRST_RUN_CINEMATIC_DEPENDENCY_LEDGER_V001.md | 20,098 | d331d06d6f4f651b3e168cc4337e29bfba949c4f80b0f9adce32d565dafefaeb | Dependency planning only |

The master film contract is exactly 1,440 frames at 24 FPS over half-open [0000,1440), totaling 60.000 seconds. Container or probe tolerance is measured separately and can never authorize adding or dropping a master frame.

Both planning records remain DRAFT / NOT APPROVED / NOT IN UNITY and record zero approved production moving clips.

Canonical planning Shot030 is the eight-second party beat over [0312,0504). The retained twelve-second Cindermaw orbit over [0312,0600) is nonconforming reference evidence. It must not be silently cropped, retimed, or substituted.

## Evidence vocabulary

- PresentValid: qualifying current evidence for the complete row and declared scope.
- PresentNonProductionOnly: bounded technical, pilot, storyboard, or previs evidence that cannot establish production acceptance.
- Missing: the row applies, but no qualifying evidence exists.
- Stale: evidence conflicts with the current planning contract or has been superseded.
- Inapplicable: the isolated artifact cannot supply that row. It never waives the integrated project requirement.

Abbreviations in the matrix are PV, PNP, M, S, and IA.

## M / D / H separation

| Class | Current coverage | Meaning |
|---|---|---|
| M — Machine | Bounded non-production fragments only | Hashes, local frame mappings, camera/framing measurements, preliminary luminance and adjacent-frame metrics, and static aspect/grayscale proofs |
| D — Device | 0 | No supported physical-device playback, decoder, GPU, lifecycle, thermal, aspect, accessibility, controller, or touch run |
| H — Human | 0 | No A1 production-fidelity acceptance, accessibility-led acceptance, comprehension approval, user creative approval, or integrated player playtest approval |

No M result replaces a D or H gate. All numeric latency, performance, comprehension, and sample-count thresholds remain proposed AnotherLife acceptance targets until measured and explicitly accepted by A1 or the user.

## Exact retained artifact ledger

The paths below identify machine-local retained evidence only. The files are not part of this PR.

### Shot010 — 5.5-second Blender technical pilot

Local evidence identity: al-intro-cinematic-draft-v001/motion_sources/AL_FR_MOTION_SRC_010_MATERIAL_AWAKEN_V001.

State: TECHNICAL PILOT PASS / DRAFT / NOT APPROVED / NOT IN UNITY / NOT FINAL MASTER.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| Provenance and QA record | 11,394 | 1c030623660535177cf8ddb7a35c6134bbdbeb9acc3944229cacfa5dd4d78e65 |
| Selected Blender scene | 1,388,542 | b86f2ff66fc3240c188cedcf30218d796dd1747284feca2dafd170864342d264 |
| 132-frame manifest | 23,284 | 288e5482b7972f48518e263d20e9cd1ad6f566aa1aebe4d7510e790aa94ce4cf |
| QA summary | 4,295 | 460034c836ae59351ad8b77cc8731ab529b50d0504a0f602bd3eda8b201ce3f6 |
| Encode probe | 1,424 | d1d84e87865ca3d251bac8578c447e85c8675e3fab7899d7bd75f3c5e02db143 |
| H.264 review MP4 | 494,082 | 23648d2be1021aada4e3ecc371b2cdeb647595ad675ec9eac112d5c523a79b9a |
| Color contact sheet | 664,045 | 7a6b9e68f07bb3f30f367e2e7e9dc70527f62a6598ec598f0acd47cbab77c830 |
| 390 x 844 contain proof | 79,020 | 6f79f1fb29ed5b8581d188014c2646738c4189818e1eebf3f1698c75861d999c |

Audit validation: all 132 selected source PNG frames and all 79 retained rejected diagnostic PNG frames matched their manifests with zero missing or mismatched entries.

Evidence ceiling: exact [0000,0132), 132 frames at 24 FPS, 960 x 540 review media only. There is no platform master, runtime playback, physical-device, integrated transition, caption/narration, or H evidence.

### Thirty-eight-second still previs

Local evidence identity: al-intro-cinematic-draft-v001.

State: DRAFT / PREVIS / STORYBOARD REFERENCE / NOT FINAL MOTION / NOT APPROVED / NOT IN UNITY.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| index.html | 30,258 | e7316860b43ef305c85f5b73ed675961dc1ca6797f689ebcd808b867e3b736ad |
| Composition QA | 11,105 | 408612c30e65f6dc0e99d579442b7c1b000b461044b44794530be4401f49074f |
| Screenshot manifest | 6,107 | b460b33be26699b78c8deb6ca7154f756e536e086f4d774283af762d11ac907b |
| Landscape contact sheet | 2,459,254 | 9822f76c8f6247f20140a0aa13902753b999c0344b74f571c4b77615061c716c |
| 390 x 844 review sheet | 559,356 | 689c2d0f4fe5084ec3f03099072076f06f3040476ebb26c0bc93c83228548539 |

Evidence ceiling: two logical browser layouts at 38.000 seconds using still PNG sources and CSS/GSAP transforms. No video export, container, codec, frame rate, frame count, audio track, or caption track exists. The export directory is empty. The artifact is stale for the 60-second master and current clean-picture planning, while its reduced-motion query, grayscale sheets, and portrait geometry remain non-production test-method references.

### Shot030 — Cindermaw rigid previs v004

Local evidence identity: meshy_output/20260812_015128_cindermaw-salamander-umbral-el_019ff1bc/shot030_cindermaw_rigid_previs_v004.

State: NON_PRODUCTION_SHOT030_RIGID_PREVIS_ONLY.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| Blender scene | 8,004,727 | 6c4b83d2a3fa6f4e19d0dc9a51adb831adc757d9b8c121aed0285b32fc6fd1bd |
| Human report | 4,384 | ca4c1bf8aae8eaabecdf961186b42657725f80998c03bb574134f8eb5784afa9 |
| Machine report | 32,418 | aadb8d46a9f746e9d50452efa62055da6e17e7fef041f0c1d536dfdff5f151d6 |
| Saved-scene QA | 274,209 | a46ecc63bf7971f195c43e4d0b5f2cd2a12c70645f2bcb39727400f10e44675e |
| Hash manifest | 4,694 | b967f54bdfff0674144a3752b2b130bd0393511d44b268259f759ff615899b28 |
| First review still | 219,301 | e4f29e918cac1bc4d64f4f87a222bd4397ede115be7f38dffec07094d83875fa |
| Last review still | 219,668 | 1d2ba7796cb26c24f452ffd63aabfb753e9e12a0f088cb06cb6f6d7a5a2632f4 |

Audit validation: all 20 manifest-listed files and immutable inputs matched. Camera QA evaluated 288 frames; only eight 640 x 360 review stills were rendered.

Evidence ceiling: nonconforming timing/content for P0-01. Quantified orbit, stable-horizon, roll, closure, and safe-framing results remain PNP camera evidence only. The canonical party, action, environment, weather, VFX, audio, full sequence, adjacent-shot cut, runtime, device, and H evidence are absent.

### Shot070 — Vaeloryn rigid staging v004

Local evidence identity: meshy_output/20260812_010849_vaeloryn-celestial-wish-dragon_019ff195/shot070_vaeloryn_staging_v004.

State: NON_PRODUCTION_SHOT070_RIGID_PREVIS_ONLY.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| Blender scene | 9,956,317 | f7e6901541787fd5948386644c4f7da1249ebec87358100fbe74535c8c55d07a |
| Human report | 4,701 | e8a5c5348aa5f9c58751151042f93dc8c4e572a4389b1cd181f30e0137985a32 |
| Machine report | 17,987 | 784edf867d5a58671b0e8653193a9dcf90c96dbe6e4d00a75bb23e345a0f1739 |
| QA JSON | 14,140 | 2da1603f1ccbd0d3319d513f2bc04947ea12a86cbbcc4db4ac953b8239a68d28 |
| Base hash manifest | 4,042 | 15124cdf5c552b529eddf9ad3acf5a12e0c0eea6db3f18861453b04b6c6dc79c |
| QA hash manifest | 3,165 | 6893ffb391fc28073131977529824b7107cb0a2a86bd0ae031eca56d9e971b37 |
| Retry evidence | 1,062 | 9626e7bbc9f727b7604abde86c7fcc11d871fdba480630ce38f0fe77275e8b1a |
| First review still | 57,185 | 3857a75a6303f5167c9a9f1989dee1237e7e8f23ac2b9d0920714b08c7c48610 |
| Last review still | 182,248 | 01b93d808ed5fcbc116b13a3920c381586e7451341abfb69d4b6df374e161ab7 |

Audit validation: 17 base-manifest entries and 16 QA-manifest file entries matched. Only six 640 x 360 review stills exist.

Evidence ceiling: the [1080,1248) allocation is consistent, but the dragon remains rigid and unrigged. All eight Gems and the environment, weather, VFX, and audio are non-renderable or unimplemented locators. There is no production sequence, encode, device, runtime, or H evidence.

### Four-realm dragons NON_FILM review v001

Local evidence identity: meshy_output/four_realm_dragons_non_film_review_v001.

State: NON_FILM_FOUR_REALM_DRAGON_RIGID_REVIEW_ONLY.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| Blender scene | 38,673,345 | 3f6ad19f4f3c4cd373e77e840e1f770593681e2136555678eb2bf709b67d0aaa |
| Human report | 1,099 | 633bbd587173e7b30e9f0cfd447109f0fc99fe25743b108c9adf3886f01a479a |
| Machine report | 423,805 | f1f02eebbc5e711dfb630974079bc5f18391fce3df0b78d4daca3d072cd20815 |
| Hash manifest | 13,082 | 44c66e03aa7fb7d161d6853b4b1a476a1837006dc7ddf0267010e4338fdc8aec |
| First review still | 582,160 | 1edcd9674841b42be996efde29ad487c7b3702c3d1ec4764624e599a19118498 |
| Last review still | 580,551 | 2bbd72ab91f378acc5d12d76e004308b564c4baba58a7b4b8d6513a4a986c568 |

Audit validation: all 17 artifact entries and 20 source-input entries matched.

Evidence ceiling: twelve static review stills and a reproducible NON_FILM package. It has no canonical film-shot assignment and does not advance production film, runtime, accessibility, device, gameplay, or H acceptance.

## P0 / P1 classification matrix

| Approved evidence row | Shot010 | 38s still | Shot030 | Shot070 | Four dragons | Aggregate ceiling |
|---|---|---|---|---|---|---|
| P0-01 Film envelope/lifecycle | PNP | S | S — nonconforming | PNP | IA | PNP/S; no master |
| P0-02 Truthful Loading Complete | IA | IA | IA | IA | IA | M |
| P0-03 Comprehension/captions/narration | M | S | M | M | IA | M |
| P0-04 Photosensitivity/reduced motion | PNP | PNP | PNP | PNP | IA | PNP |
| P0-05 Camera/audio/weather/VFX handoff | PNP | PNP | PNP | PNP | IA | PNP |
| P0-06 Draft/durable commit separation | IA | IA | IA | IA | IA | M |
| P0-07 Failure/retry trust | IA | IA | IA | IA | IA | M |
| P0-08 Mobile readability/input parity | PNP | PNP | M | M | IA | PNP |
| P0-09 Lifecycle/low-memory/decoder recovery | M | M | M | M | IA | M |
| P0-10 3D prologue/time-to-control | IA | IA | IA | IA | IA | M |
| Mandatory P0 evidence packet | PNP | PNP | PNP | PNP | PNP — NON_FILM only | PNP/incomplete |
| P1-01 Replay safety | M | M | IA | IA | IA | M |
| P1-02 Localization stress | IA | M | IA | IA | IA | M |
| P1-03 Endurance | M | M | IA | IA | IA | M |
| P1-04 Device breadth | M | M | M | M | IA | M |
| P1-05 Extended description | M | M | M | M | IA | M |
| P1-06 Privacy-safe telemetry | IA | IA | IA | IA | IA | M |

There is no PV cell. Every PNP cell is M-only technical or previs evidence; none includes D or H.

## Ordered evidence plan

1. Freeze and index; promote nothing.
   Preserve the exact hashes and classifications above. Keep the 38-second still previs and four-dragon review excluded from production picture. Keep Shot030 as nonconforming camera reference. Do not repair, extend, upscale, retime, or compose these artifacts into a master claim.

2. Bind source authority before new media.
   Require one accepted identity, rights, and source packet for every exact EDL row, including its half-open master allocation and separate transition-handle policy. Shot030 requires a new source-complete party/cast/impact candidate at exactly [0312,0504). No unapproved asset substitution or invented canon.

3. Prove the production source pipeline on Shot010 first.
   In a separately authorized production lane, create rather than upscale the exact 132 master frames over [0000,0132). Retain immutable source and render logs, accepted native output/aspect responsibilities, source/rights lineage, reduced-motion behavior, and declared audio/caption/description responsibilities. Run independent frame/decode/PTS, clean-picture, photosensitivity-pattern, grayscale, and mobile-safe-area checks.

4. Produce the remaining seven source-complete rows only after the Shot010 pipeline passes.
   Every row must match its exact interval and required content using full motion rather than interval stills. Shot030 needs the canonical five-person party/action/environment. Shot070 needs eight separately visible accepted Gem identities, accepted Vaeloryn performance, environment, weather, VFX, and audio. Adjacent-row continuity cannot add or drop master frames.

5. Assemble the first qualifying master.
   Require exactly 1,440 frames at 24 FPS over [0000,1440), immutable source/build/media/settings hashes, complete frame/PTS and audio/PTS reports, caption/narration/description manifests, photosensitivity and reduced-motion results, and a continuity diff.

6. Collect D evidence and integrated journey M/D evidence.
   Run the approved physical-device, decoder, GPU, aspect, lifecycle, low-memory, fault, and accessibility matrix. Then test truthful Loading Complete independence, draft versus durable commit, conflict/retry trust, mobile focus/touch/controller parity, replay/endurance, and film-to-3D-prologue time-to-control. Retain the complete mandatory P0 evidence packet.

7. Keep H gates separate and last.
   A1 source/fidelity disposition, accessibility-led testing, comprehension/player evidence, and user creative and integrated-playtest approval remain mandatory. Statistical or machine passage never replaces H.

## Validation performed for this audit

- Verified the planning-approved EDL and ledger exact sizes and SHA-256 identities.
- Verified the retained artifact identities listed above.
- Verified 132 selected and 79 rejected Shot010 frames against their manifests.
- Verified all 20 Shot030 manifest entries.
- Verified 17 Shot070 base entries and 16 Shot070 QA file entries.
- Verified all 17 four-dragon artifacts and 20 source inputs.
- Opened representative current-run still/contact-sheet evidence for identity confirmation only.
- Confirmed no production film, target-device run, integrated runtime test, or H disposition exists.
- Confirmed the repository target path was absent at the authorized base before publication.
- No Unity, Blender, HyperFrames render, Meshy task, paid tool, credit, local Git checkout, shared-file lock, or binary-copy operation was performed for this publication.

## Approval limits and non-claims

- A1 approved this audit as read-only evidence and a P0 no-go review only.
- This document does not approve cinematic or visual quality, canon, source fidelity, models, animation, camera direction, pacing, music, voice, weather, VFX, accessibility, photosensitivity safety, runtime behavior, device compatibility, player comprehension, commercial rights, merge readiness, or release.
- Planning approval of the EDL and dependency ledger does not approve their media, implementation, or creative result.
- Retained DCC credit or cost fields do not establish upstream Meshy cost, source rights, or commercial-use permission.
- No retained artifact becomes production-ready because its files or hashes are internally consistent.
- Partial, rejected, stale, nonconforming, PrototypeOnly, and NON_FILM results retain those classifications.
- Co-developer visibility is provided through this draft PR and linked record #463. A separate @rslee94 review is not a mandatory readiness gate.
- A7 does not self-approve. A1 and user gates remain in force.
- Meshy task and credit authority remains A1-only.

## Current disposition

- Coordination/review evidence: approved and published for source-mode review.
- First-user P0: blocked.
- PresentValid P0/P1 evidence: none.
- Physical-device evidence: D=0.
- Human/user evidence: H=0.
- Shared-file locks: none.
- Runtime, visual, creative, player, merge, and release acceptance: not granted.
