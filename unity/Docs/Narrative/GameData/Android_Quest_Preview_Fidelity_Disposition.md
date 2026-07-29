# Android OMEN_1 Quest Preview Narrative Fidelity Disposition

**Status date:** 2026-07-29
**Primary Codex mode:** narrative/content
**Reviewed baseline:** `main@fd84798`
**Source packet:** `al_narrative_quest_preview_source_v001`
**Canonical quest source:** `omen1-a1-2026-07-22-v002`
**Tracking:** issue #186 and merged PRs #336, #309, and #353
**Disposition:** `PASS FOR THE DEBUG-ONLY READ-ONLY PREVIEW`

## Review Boundary

This disposition compares the approved quest-preview source handoff and
canonical `OMEN_1` catalog with the Android implementation on current `main`.
It reviews source identity, player-facing meaning, authority limits, failure
meaning, and release visibility.

The pass is deliberately narrow. It does not approve a production quest route,
authoritative progress, quest acceptance, Unity launch, reward mutation,
notification delivery, save persistence, or the later Android-to-Unity bridge
tracked by issue #135. Those capabilities require engineering evidence and a
new A3 fidelity review before player exposure.

## Fidelity Results

| Area | Result | Current evidence |
| --- | --- | --- |
| Canonical identity | Pass | The debug loader accepts only `OMEN_1`, NVS-01 schema version 1, packet version `omen1-a1-2026-07-22-v002`, and the approved canonical SHA-256. |
| Presentation source identity | Pass | The content parser requires catalog version `0.1.0`, catalog ID `al_quest_preview_content_catalog`, source packet ID `al_narrative_quest_preview_source_v001`, and issue #186 scope. |
| Quest title and description | Pass | The preview copy must equal the canonical `OMEN_1` localization for “The First Signal” and its approved Sky Castle description. |
| Speaker identity | Pass | Captain Valerius and the Veil Watch liaison role are resolved from the canonical quest catalog rather than duplicated Android simulation data. |
| Objective identity and order | Pass | The three stable objective IDs resolve to canonical text and must retain talk, arena, then report order. |
| Reward identity | Pass for the read-only preview | The displayed summaries are exactly Celestial Tear, 500 Gold, and Valerius affinity +5. No claim control or mutation exists. |
| Reward timing | Preserved in source; future presentation gate | The source retains Tear-on-arena-success and Gold/affinity-on-report timing. The current debug view presents a flat “Approved rewards” inventory and makes no grant or timing claim. Any player-facing or actionable view must expose the distinction. |
| Location meaning | Pass | `SKY_CASTLE` is validated as an internal marker while the UI displays “Sky Castle Anomaly” and its approved unavailable-hook summary. |
| Action meaning | Pass for the read-only preview | No Deploy, Retry, Present, Start Story, Locate, or Claim control is rendered. Generic Start Story, generic claim, and internal-marker actions remain prohibited. |
| Progress authority | Pass | The preview exposes no progress bar or inferred lifecycle state and declares that it cannot change quest progress, rewards, or saves. |
| Runtime availability | Pass | The ready view visibly states that the hook is requested and cannot change progress until Unity returns an authoritative result. Source-validation failure produces a visible nonmutating unavailable state. |
| Legacy Android rows | Pass | `OMEN_2` and Android Q1-Q4 rows remain hidden legacy/demo input and cannot publish as approved source. |
| Release boundary | Pass | The full preview/parser exists only in debug source; release sanitizes the Quest route and composes no quest-preview content or action. |
| Internal identifiers | Pass for the declared role | Ready-state player-facing copy does not expose `SKY_CASTLE` or other internal IDs. Technical validation detail is confined to the debug-only unavailable view. |

## Future-Role Guards

These are not blockers for the current read-only developer preview because the
corresponding behavior is absent. They become required before the role expands:

1. Validate each action's exact semantic action, required capability or state,
   and availability status against the approved source. Resolving an action ID
   and label alone is not enough for a launcher.
2. Validate `progressModel.validStates` against the canonical state roster
   before accepting any runtime snapshot or presenting lifecycle progress.
3. Validate the exact reward ID, trigger, order, and non-claim policy. A future
   player-facing view must distinguish the Celestial Tear acquired on arena
   success from Gold and affinity applied at report conclusion.
4. Continue to resolve action, status, location, and reward copy through
   approved localization authority when those elements become interactive or
   release-visible.
5. Require typed request/result correlation, duplicate handling, visible
   failure, persistence, and restoration evidence before enabling issue #135
   launch behavior.

## Re-Review Triggers

A3 narrative/content re-review is required if any change:

- exposes the Quest route outside debug builds;
- adds progress, lifecycle status, action controls, or runtime results;
- changes title, description, speaker, objective, location, reward, action, or
  unavailable copy;
- changes `OMEN_1` packet version/hash or preview source version;
- introduces a manual claim, generic Start Story, internal marker label, or
  Android-authored progression state;
- connects Android to Unity or changes reward timing and report meaning.

## Acceptance Status

The merged Android implementation faithfully consumes the approved source for
its declared debug-only read-only role. Issue #186's deceptive Start/Claim and
parallel-progress risks are absent from that role. This disposition does not
claim the production launcher, runtime bridge, integrated quest loop, or final
player experience is complete.

Final release wording, route availability, integrated preview/launcher UX, and
playtest approval remain user gates.

## Validation

- Passed JSON/reference assertions for three unique preview records, four
  unique resolved actions, twenty unique localization entries, exact objective
  order/copy, exact reward order/timing, required prohibitions, and canonical
  SHA-256 `b22c166310617657cf9716f988e697d4c4992b4d1877b6fd4d0a3311af9a9a1f`.
- Passed static UI evidence: zero debug-view matches for generic Start Story,
  claim, internal Sky Castle ID, inferred progress, or actionable quest
  controls; zero quest-preview content/loader matches in the release
  implementation; release route sanitization remains present.
- Passed
  `.\gradlew.bat --no-daemon --no-configuration-cache --console=plain
  :app:testDebugUnitTest :app:assembleDebug :app:assembleRelease`.
  Result: 39 JVM tests, 0 failures, 0 errors, 0 skipped; debug and unsigned
  release APK assembly succeeded.
- Not run: Android instrumentation/device accessibility replay. This
  documentation-only PR retains merged PR #353's API 35 evidence and changes no
  executable behavior.

## Impact

This disposition adds documentation only. It changes no Android or Unity code,
catalog, save, route, reward, scene, asset, dependency, performance, memory,
package-size, install-size, or device-compatibility behavior. No shared file is
touched.
