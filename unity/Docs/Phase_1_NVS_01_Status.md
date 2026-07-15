# Phase 1 NVS-01 Status

**Status date:** 2026-07-15  
**Integration branch:** `main`  
**Audited current-main head:** `a6232e63c807f055cc43b302ad4e62b846c236ca`  
**Roadmap state:** Phase 1 remains paused behind QuestDefinition asset authority issue #156  
**Ownership transition:** issue #193, GPT–Codex–user model

`AGENTS.md` is authoritative. This record separates source presence, issue state, merge state, validation evidence, and player-visible completion.

## Current repository state

### Completed foundation changes

- PR #147 restored Unity dialogue-choice compilation with one mechanical source fix. Issue #145 is complete.
- PR #190 restored Android `Quest` positional-constructor compatibility.
- PR #192 merged the staged repository quality-gate policy.
- PR #197 merged `Save_Semantic_Compatibility_Policy.md`, resolving shared candidate-selection and malformed-data rules required by #136, #152, #163, and #137.

### Ownership correction

PR #196 assigned terrestrial design to Gemini and retained the old Android Studio workstream. The user subsequently revoked that model.

Issue #193 and branch `gpt/consolidate-codex-ownership` now define the replacement:

- GPT — coordination, specification, review, status, risk, and sequencing;
- Codex narrative/content mode — all narrative source;
- Codex terrestrial-design mode — all terrestrial visual-design source;
- Codex engineering mode — Android, Unity, runtime, assets/import, save, build, tests, CI, and tooling;
- user — final creative, product, playtest, and release approval.

`android-studio/` and `gemini/` are retired branch prefixes. Android Studio and Unity remain tools only.

### Open pull requests

#### PR #189 — QuestDefinition authority safeguards

- Issue: #156
- State: draft
- Owner: Codex engineering
- Shared locks: none
- Current blockers:
  - branch must be rebased onto current `main`;
  - authority tests must allow valid quest assets to reference the authoritative script GUID;
  - quest-asset discovery must cover the full project;
  - exactly one production `QuestDefinition` type must be enforced;
  - full source/asset/generator/importer/schema/catalog inventory must be posted to #156;
  - Unity `2022.3.62f3` compile, corrected EditMode totals, reimport, and missing-script evidence remain required.

PR #189 must not merge while these requirements or Unity evidence are missing.

#### PR #191 — Android dependency reproducibility

- Issue: #159
- State: draft
- Owner: Codex engineering
- Shared locks: none
- Scope: pin the consumed dynamic dependency and remove unused dynamic aliases.
- Required before merge: rebase onto current `main`, review complete resolved-graph evidence, confirm Android test/debug build evidence, and ensure no opportunistic dependency upgrade is included.

#### PR #195 — Android narrative-debug release gating

- Issue: #161
- State: draft
- Owner: Codex engineering
- Shared locks: none
- Scope: remove the Debug route/trigger surface from release while preserving a labeled non-authoritative debug path.
- Required before merge: focused diff review, debug/release route tests, `testDebugUnitTest`, debug assembly, release assembly or exact signing blocker, and rebase onto current `main`.

### Shared-file state

Current designated soft locks: **none**.

Shared integration files:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring one holds the lock.

## Active Phase 0/1 gate

### #156 — QuestDefinition authority and serialized asset identity

The pre-PR #124 repository had two script identities:

```text
AL.Data.Definitions.QuestDefinition
GUID 226022aa7500f3e4abc8ac3757707ad8

AL.Data.Definitions.Narrative.QuestDefinition
GUID c385b2b183b74184ca75eeffbe2256ef
```

The expected authority is the surviving narrative type/GUID when no tracked asset depends on the removed root identity. Completion still requires:

1. complete two-GUID occurrence inventory;
2. all QuestDefinition source, asset, generator, importer, schema, catalog, and `CreateAssetMenu` paths;
3. one authoritative production type/GUID decision and rollback record;
4. deterministic guards for duplicate types, GUID drift, missing scripts, and duplicate quest IDs;
5. Unity compile, reimport, missing-script scan, and EditMode/editor evidence.

No clean A1 packet or production Player build may claim a trusted asset baseline until #156 is complete.

## Parallel foundations

These may proceed on non-overlapping focused branches:

- #127 — profile-safe PlayMode smoke;
- #136 — reputation/faction/persona normalization and round trip;
- #152 — null/blank/unknown/duplicate quest-state compatibility;
- #153 — complete Bootloader service-stack readiness;
- #155 — repository/Android CI and staged Unity validation;
- #159 — Android dependency reproducibility through PR #191;
- #161 — release debug-route gating through PR #195;
- #163 — resource and Warzone Credit mutation integrity.

Issue #197 is policy, not implementation. It supplies the shared save candidate and repair rules these lanes must consume.

## Save-hardening dependency

```text
#136 + #152 + #163 policy-compatible foundations
                 ↓
#137 crash-safe local save and semantic candidate selection
```

Verified #137 gaps still include unsafe backup rotation, conflated load/save status, incomplete first-generation backup, incomplete deletion, unsafe ordinary-I/O fallback, offline-progress rollback, quarantine handling, and missing fault matrices.

Any future `SaveGameData.cs` change requires its shared lock and must follow `Save_Semantic_Compatibility_Policy.md`.

## NVS-01 chain

### #128 — A1 packet

After #156, Codex narrative/content mode creates the clean D1–D16-faithful packet on:

```text
codex/narrative-nvs-01-a1
```

The merged archive packet is historical source only and remains inconsistent with approved start context, deployment node, failure/retry, Tear timing, manual report, consequence timing, abandonment, localization, and D16 resume behavior.

### #133 — G1

GPT reviews the approved A1 packet and publishes the implementation specification. GPT does not rewrite or repair narrative source.

### #134 — C1–C4

Codex engineering implements only after G1 and its named foundations are ready. Known areas include validated content loading, deterministic quest/dialogue state, encounter session/result identity, persistence/migration, atomic consequences, visible failure, and complete tests.

### Review and acceptance

```text
A1 Codex narrative/content
→ G1 GPT
→ C1–C4 Codex engineering
→ G2 GPT
→ A2 Codex narrative/content fidelity
→ U1 user playtest
```

A2 is a source-fidelity disposition, not independent technical approval. GPT review and user acceptance remain mandatory.

## Production and bridge lanes

- #150 remains blocked by #156. Current normal Unity Build Settings have no production scene list.
- #135 remains deferred until standalone NVS-01 and #150 are proven. The Android reflection host is not yet a packaged, mounted, session-correlated end-to-end bridge.

## Evidence rules

A task is complete only when its own acceptance criteria pass. The following are insufficient alone:

- issue closure;
- PR merge;
- source/test file presence;
- Android build evidence for a Unity change;
- compilation without asset, persistence, duplicate-safety, packaging, or player-visible evidence;
- skipped/unavailable validation;
- documentation without an implemented producer/consumer;
- a prototype inherited from a rejected or obsolete branch.

Every completion report identifies base/head SHA, files changed, commands, exit codes, test totals, artifacts, blocked validation, owner mode, shared locks, compatibility decisions, and remaining risk.

## Immediate next actions

```text
1. Merge issue #193 governance correction after documentation review.
2. Rebase and repair PR #189; keep it draft until Unity evidence passes.
3. Review PR #191 and PR #195 as independent Codex engineering lanes.
4. Keep #128 and #150 blocked until #156 is complete.
5. Continue save/economy/PlayMode foundations only through focused non-overlapping PRs.
```