# Phase 1 NVS-01 Status

**Status date:** 2026-07-15  
**Integration branch:** `main`  
**Audited current-main head:** `1342f4194261450fe8cff3e529eddf29c6c7bb1e`  
**Roadmap state:** Phase 1 remains paused behind QuestDefinition authority issue #156 and the red Phase 0/1 foundation gate  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

`AGENTS.md` is authoritative. This record separates source presence, issue state, merge state, validation evidence, and player-visible completion. A draft PR, a green compile, or a merged specification is not implementation acceptance.

## Current control summary

- The active product milestone remains NVS-01.
- No approved A1 narrative packet is active; the archived OMEN_1 material remains historical reference only.
- #156 remains the first blocking technical gate. Its complete validator contract is now merged, but PR #189 has not implemented or canonically validated it.
- The canonical Unity workspace is `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`; evidence from duplicate checkouts is blocked validation.
- `Bootloader.cs` is exclusively soft-locked by draft PR #203.
- Android dependency reproducibility issue #159 is complete through merged PR #191.
- The #163 economy contract is complete through merged PR #215; implementation PR #214 is draft/blocked and does not satisfy it.
- Terrestrial source-design PR #217 is draft/blocked pending technical manifest/LFS/import corrections and user creative review.
- Ten implementation/tooling/source-design PRs are open and all are currently draft/blocked.

## Ownership state

The later user instruction returning all delivery responsibility to Codex is authoritative and is recorded by merged PR #205 and `Ownership_Decision_Record.md`.

Current model:

- GPT — coordination, specifications, state/contract/save/test design, review, status, risk, sequencing, and merge readiness;
- Codex narrative/content — narrative source and fidelity;
- Codex terrestrial-design — terrestrial visual-design source and fidelity;
- Codex engineering — Android, Unity, runtime, assets/import, saves, builds, tests, CI, and tooling;
- user — final product, creative, visual-design, balance, irreversible-profile, playtest, milestone, and release approval.

Android Studio and Unity are tools. `android-studio/` and `gemini/` are retired prefixes for new work.

## Completed foundation records and changes

- #145 / PR #147 — Unity dialogue-choice compilation repair.
- PR #190 — Android `Quest` positional compatibility.
- PR #192 — repository quality-gate policy.
- PR #197 — save semantic compatibility and candidate-selection policy.
- PR #198 — Bootloader service-stack integrity specification.
- PR #200 — Unity release-command containment specification.
- PR #202 — durable realm-selection specification.
- PR #205 — latest ownership decision restored and recorded.
- PR #207 — representative PlayMode profile-isolation specification.
- #159 / PR #191 — Android dynamic dependency versions removed; Material pinned to validated `1.4.0`.
- PR #213 — Phase 1 gate status and risk register refresh.
- PR #215 — transaction-safe resource and Warzone Credit integrity specification.
- PR #216 — economy implementation/status dependency refresh.
- PR #218 — complete QuestDefinition asset-authority validator specification.

A merged specification is not implementation completion.

## Open pull requests

| PR | Issue | Scope | Current disposition | Shared lock |
| --- | --- | --- | --- | --- |
| #189 | #156 | QuestDefinition authority safeguards | **Draft / blocked** | none |
| #195 | #161 | Android debug-route release gating | **Draft / blocked** | none |
| #203 | #153 | Bootloader service-stack lifecycle | **Draft / blocked** | `Bootloader.cs` |
| #208 | #178 | Unity prototype-command containment | **Draft / blocked** | none |
| #209 | #127 | profile-safe representative PlayMode smoke | **Draft / blocked** | none |
| #210 | #155 | repository/Android quality-gate workflow | **Draft / blocked** | none |
| #211 | #136 | narrative relationship save-default regression | **Draft / blocked** | none |
| #212 | #152 | quest save compatibility | **Draft / blocked** | none |
| #214 | #163 | economy resource/credit integrity | **Draft / blocked** | none |
| #217 | #194 | terrestrial design-source foundation | **Draft / blocked** | none |

### #189 — QuestDefinition authority safeguards

Accepted direction:

- `AL.Data.Definitions.Narrative.QuestDefinition` remains the authority;
- authoritative GUID `c385b2b183b74184ca75eeffbe2256ef` is preserved;
- removed root GUID `226022aa7500f3e4abc8ac3757707ad8` must not reappear;
- exactly one production type and project-wide valid typed-asset discovery are enforced;
- the historical/current serialized field schema is proven equivalent.

Binding completion contract:

```text
unity/Docs/QuestDefinition_Asset_Authority_Validation_Spec.md
merged PR #218 at 1342f4194261450fe8cff3e529eddf29c6c7bb1e
```

Still required:

- rebase PR #189 onto current `main` and consume the merged specification;
- add Force-Text disk/YAML scanning that detects quest-shaped assets even when `t:QuestDefinition` cannot resolve them;
- parse exact `m_Script` fileID/GUID and distinguish missing, zero, removed, unrelated, and malformed references;
- validate every `.asset` document/subasset by local file ID;
- lock the exact historical 12-field schema/menu/type/GUID contract;
- reject missing/unexpected fields, blank IDs, duplicate IDs, and wrong runtime types;
- run the complete non-imported malformed-YAML matrix and one valid create/import/reimport/full-field round trip;
- update the authority record/inventory;
- run canonical Unity 2022.3.62f3 compile, complete/focused EditMode, reimport, missing-script, GUID, diff, and final-status evidence.

No A1, #183 production authority, or production Player work may claim a trusted Unity asset baseline before #156 is complete.

### #195 — Android narrative-debug release gating

Accepted direction:

- compile-time `BuildConfig.DEBUG` gate;
- typed allowed/rejected route result;
- deterministic historical-route removal and fallback;
- resolved `NavEntry` identity;
- preview trigger seam independent of archived A1 IDs.

Still required:

- retain the visible release rejection notice after back-stack sanitization instead of clearing it on the second Compose effect pass;
- add stabilized shell/state or Compose coverage for the durable one-shot notice;
- rebase onto current `main` and rerun unit, debug, release, and diff validation.

### #203 — Bootloader service-stack lifecycle

The architectural direction is valid, but acceptance-critical work remains:

- load ownership must commit only after `Load()` succeeds and support deterministic retry/failure state;
- pause/quit must prove save-service identity against the marker;
- publication must remain rollback-capable through post-install verification;
- malformed marker maps must never throw;
- marker data and required service inventory must be immutable;
- missing, mismatched, phase, version, registration, and service diagnostics must remain distinct;
- construction, publication, marker, lifecycle, load, save, drift, two-Bootloader, and no-service fault tests must be complete;
- branch must rebase and validate from the canonical workspace.

`Bootloader.cs` remains locked to PR #203. No other branch may edit it.

### #208 — Unity prototype-command containment

The obvious Kingdom credit, Realm Gem, Wishgate, War Drill, reset, and Champion-deployment command handlers were removed from the command deck, but containment is incomplete:

- `Update()` still completes building and research progress automatically;
- dashboard reads seed buildings, quests, territories, Realm Gems, and Wishgate state;
- `Start()` owns an additional save-load path;
- Champion Arena still contains a recurring proximity-credit grant that can continue after boss death/clear;
- tests do not prove controller startup/refresh/update non-mutation, release hierarchy, accessibility, reload idempotency, missing-service safety, or release reachability across Champion direct grants;
- validation is from a duplicate workspace and the branch is behind current `main`.

The first containment PR must make every release controller lifecycle honest and non-mutating. Switching a direct grant to the typed economy primitive is not authorization.

### #209 — profile-safe representative PlayMode smoke

The external snapshot and scene-smoke direction is correct, but cleanup is not yet safe:

- helper-test teardown can restore the default `Time.timeScale` value when global state was never captured;
- original profile files can be restored while scene destruction is still deferred;
- the fallback creates another scene rather than proving the representative scene is unloaded;
- required cleanup timeout, assertion/log failure, second-run, operation-fault, timestamp/attribute, and post-cleanup service tests are incomplete;
- the hard editor/process/OS crash limitation and incoming scene state need explicit disposition;
- branch must rebase and validate from the canonical workspace.

Until corrected PR #209 merges, no other PR may cite it as passing PlayMode evidence.

### #210 — repository/Android quality-gate workflow

Positive evidence exists: live run `29389236741` passed `policy / classify`, `repository / hygiene`, and `android / unit-debug`, and retained the Android artifact.

Still required:

- use `.github/anotherlife-policy.yml` as the actual policy source or test deterministic drift;
- handle PR body/readiness events and correct PR/push base-head ranges;
- implement owner-mode/branch mapping, full impact declarations, Build Settings, completion-link, current-head, lock, ownership chronology, and complete path reporting gates;
- fail closed when diff discovery fails;
- harden action-SHA, permission, forbidden-path, checkout-credential, timeout, Android transcript/cache, and KSP/AWT checks;
- use `Refs #155`, not close #155;
- rebase onto current `main`, rerun live CI, execute the intentional passing/failing proof PR matrix, and later verify branch protection.

### #211 — narrative relationship save-default regression

The isolated service mutation/reload test is useful, but #136 still requires:

- omitted-fields `JsonUtility.FromJson<SaveGameData>` coverage;
- repeated-normalization and serialize/deserialize idempotency;
- preservation of representative unrelated save fields;
- current-main rebase;
- canonical-workspace Unity compile and complete EditMode evidence.

Do not close #136 or unblock its side of #137 until the corrected head is reviewed.

### #212 — quest save compatibility

The current implementation contradicts the merged save policy:

- it removes duplicate rows and keeps the first instead of preserving and disabling the full duplicate group;
- it deletes null/blank rows during ordinary queries;
- it seeds Q1–Q5 from an empty legacy list;
- unknown `SQ_` rows remain operational and unsupported side-quest IDs can be accepted;
- rejected claim/progress paths can mutate or save;
- contradictory-state, no-side-effect, definition-return, exact-preservation, and query-idempotency tests are incomplete;
- canonical-workspace Unity evidence is missing.

The latest SHA change was a rebase only; reviewed service blobs are unchanged. #152 must operate through a non-mutating compatibility view. Data-changing repair and candidate replacement remain #137 responsibilities.

### #214 — economy resource/credit integrity

PR #215 merged the binding contract at `unity/Docs/Economy_Integrity_Spec.md`. PR #214 was created before it and implements the opposite repair policy:

- reads initialize null wallets, delete null rows, and rewrite negative balances;
- duplicate resource rows are summed into the first row;
- duplicate overflow is clamped to `long.MaxValue`, which can fabricate value;
- negative Warzone Credits are clamped to zero from ordinary reads;
- missing resources are created without core/optional/unsupported classification;
- no typed read/mutation result or no-save transaction primitive exists;
- legacy nested save remains the only credit path;
- live production delta, dependency validation, atomic batch, remainders, and event behavior remain uncorrected;
- tests encode prohibited repair and omit the required matrix;
- Unity evidence is duplicate-workspace exit `199` with no XML.

The latest SHA change was a rebase only; reviewed service blobs are unchanged. Required correction is a contract-first rewrite: preserve malformed evidence, disable mutation, implement typed no-save primitives and compatibility wrappers, enforce core/rare authority, make reads pure, stage production atomically, rebase to current `main`, and validate canonically.

### #217 — terrestrial design-source foundation

The packet has an appropriate source-mode boundary and useful base profile intent, but it is not yet technically ready for user creative review or issue closure:

- change `Fixes #194` to `Refs #194`; user approval and later engineering fidelity remain outstanding;
- rebase onto current `main`;
- render/link all three concept sheets directly in the PR for user review;
- prove Git LFS retrieval with `git lfs fsck` and a clean/fresh checkout, then verify actual hashes/dimensions;
- store immutable media type, dimensions, SHA-256, source version, and prompt/provenance reference in the manifest;
- add visual source for all declared variants or mark non-standard variants proposed/pending user approval;
- add deterministic manifest schema/semantic validation;
- mark working labels and biome entries as non-player-facing, non-runtime design intent;
- either run canonical Unity import for PNGs kept under `Assets` or move pure previews under Docs;
- record explicit source-review/user-approval/runtime-blocked readiness state.

The latest SHA change was a rebase only; the manifest and design-source blobs are unchanged. GPT has not approved creative fidelity. User visual approval remains mandatory before engineering integration or production use.

## Active gate: #156

Before merged PR #124, two QuestDefinition identities existed:

```text
AL.Data.Definitions.QuestDefinition
GUID 226022aa7500f3e4abc8ac3757707ad8

AL.Data.Definitions.Narrative.QuestDefinition
GUID c385b2b183b74184ca75eeffbe2256ef
```

The narrative type/GUID authority and historical field equivalence are accepted. Completion now means implementing the merged PR #218 YAML/subasset/schema validator contract and passing canonical Unity evidence.

## Save and persistence dependency

```text
#136 accepted normalization evidence
          +
#152 non-mutating quest compatibility
          +
#163 typed non-repairing economy implementation
          ↓
#137 crash-safe candidate selection, recovery, repair, deletion, and persistence
```

All implementations consume `Save_Semantic_Compatibility_Policy.md`. Queries do not perform data-changing repair; unknown stable data is preserved; malformed domains are disabled; candidate selection and clone → persist → publish remain #137.

## Definition and progression dependency

```text
#156 trusted asset/type authority
          ↓
#183 versioned immutable game-data authority
          ↓
#165 definition-backed building/research/training integrity
```

Current `LocalGameDataService` does not define `ManaShrine` or `Mine`, exposes no research query, and returns `null` for all troop lookups. Before #183, #165 may fail closed but must not invent temporary IDs, maximum levels, troop definitions, or balance.

## Terrestrial source and integration dependency

```text
#194 / PR #217 technically complete source packet
          ↓
user creative approval
          ↓
#156 trusted Unity asset baseline + #183 game-data authority + owning runtime issue
          ↓
separate Codex engineering integration and fidelity review
```

Terrestrial profile IDs, working labels, variant intent, biome tags, concept images, and source versions are not gameplay, spawn, AI, reward, save, lore, or runtime catalog authority.

## Validation and quality-gate dependency

```text
#209 safe PlayMode implementation
          +
#210 proven Phase A repository/Android gates
          +
#150 production Player build path
          ↓
reliable automated/manual Unity and release evidence
```

A skipped, duplicate-workspace, unavailable, cancelled, or `continue-on-error` check is not passing evidence.

## NVS-01 chain

```text
#156 trusted QuestDefinition authority
  ↓
#128 Codex narrative/content A1
  ↓
#133 GPT G1
  ↓
accepted focused technical foundations
  ↓
#134 Codex engineering C1–C4
  ↓
G2 GPT → A2 Codex narrative/content → U1 user
```

The archived packet is history, not approved A1. A1 must encode D1–D16, including offer/acceptance, deployment node, transient failure/retry, Tear acquisition, manual report, atomic consequences, abandonment, localization, and exact resume.

## Production lanes

- #150 remains blocked by #156; normal Unity Build Settings still lack the approved production scene flow.
- #135 remains deferred until standalone NVS-01 and #150 are proven.
- #178 containment may continue only within focused controller/UI/release-reachability boundaries.
- #173 implementation follows accepted #137/#183 prerequisites; its merged specification does not authorize early mutation work.
- #165 full reconnection follows accepted #163 and #183; earlier containment may only fail closed.
- terrestrial runtime work follows technical source completion, user creative approval, #156/#183, and a separately approved owning issue.

## Shared-file state

Current lock:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs — PR #203
```

Currently unlocked designated files:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring a designated file holds the lock. No conflict resolution may discard valid services, fields, assets, contracts, or registrations.

## Evidence rules

Issue closure, PR merge, source presence, one-platform compilation, skipped checks, or documentation alone are insufficient. Evidence must match the risk:

- build risk — exact commands, exit codes, logs, and compiler scan;
- asset risk — GUID/reference inventory, LFS retrieval, import/reimport, malformed/missing-script scan, hashes, dimensions, and field preservation;
- test risk — discovered totals and retained XML/log artifacts;
- save/economy/reward risk — normal, recovery, fault, duplicate, overflow, reload, event/save-count, and idempotency matrices;
- contract risk — valid/invalid data and implemented producer/consumer proof;
- packaging risk — actual Player/export build and launch transition;
- source/design risk — approved packet fidelity, rendered references, provenance, version/hash mapping, accessibility, and user decision;
- player-experience risk — integrated user playtest.

## Immediate next actions

```text
1. Implement merged PR #218 in PR #189 and run canonical Unity evidence to clear #156.
2. Rewrite PR #214 against the merged economy specification and run canonical evidence.
3. Correct PR #209 cleanup ordering before any branch consumes PlayMode evidence.
4. Harden PR #210 against the merged policy, run proof PRs, and retain live artifacts.
5. Correct PRs #211 and #212 against the save semantic policy before #137 begins.
6. Remove PR #208 hidden controller/direct-credit mutations and prove non-mutation/release reachability.
7. Fix PR #195 durable rejection notice and rebase/revalidate Android.
8. Complete PR #203 transaction-safe lifecycle and full fault matrix while retaining the lock.
9. Correct PR #217 technical manifest/LFS/import/variant review gates, then obtain user creative approval.
10. Do not activate #183 implementation, #165 reconnection, A1, G1, #137, #134, terrestrial runtime, or production Player claims before their prerequisites pass.
```
