# Save Semantic Compatibility and Candidate Selection Policy

**Status date:** 2026-07-15  
**Policy owner:** GPT  
**Implementation owners:** Codex under #136, #152, #163, and #137  
**Baseline `main`:** `df50874f05d8d2ff245cad274c72a67972ad7478`  
**Current phase:** Phase 0/1 data-safety foundation

This policy resolves the cross-issue decisions required before save normalization, quest compatibility, economy integrity, and crash-safe persistence can be implemented independently without producing contradictory repair behavior.

It defines:

- what is a compatible omission;
- what unknown future data must be preserved;
- what malformed data disables only one subsystem;
- when a cleaner backup outranks the primary;
- when a data-changing repair is allowed;
- when a profile must remain read-only/degraded rather than being reset;
- how quest, resource, reputation, faction, and persona state are treated;
- how offline progress and automatic save interact with validation;
- the merge order and shared-file lock.

It does not implement code, change narrative meaning, choose quest rewards, change currency balances, or authorize NVS-01 state.

## 1. Product and safety principles

1. **Preserve the player profile before preserving one malformed subsystem.** A corrupt quest or wallet record must not automatically reset unrelated realm, customization, equipment, or progression data.
2. **Never fabricate value while repairing.** Missing established-profile resources do not receive new-profile starting balances; duplicates are not silently summed; negative values are not converted into gains.
3. **Preserve stable unknown data.** A nonblank unknown quest/content ID may belong to a newer build or a downgraded profile and must not be deleted merely because the current build lacks its definition.
4. **Malformed records do not participate in gameplay.** Null, blank, duplicate, non-finite, overflowed, or contradictory records are excluded from active operations until a reviewed migration resolves them.
5. **Prefer a cleaner valid backup over a malformed primary, but not over legitimate future data.** Unknown stable IDs alone do not make a candidate worse than an older backup.
6. **Data-changing repair is explicit and observable.** Preserve the original bytes first, record diagnostics, repair a clone, durably install it, and never call the result an ordinary primary load.
7. **Do not automatically rewrite a save whose schema is newer than the runtime.** Forward-version profiles are read-only/degraded until a compatible reader exists.
8. **Offline progress is a mutation, not a read.** It runs only on a writable validated clone and becomes current only after durable persistence succeeds.
9. **No nested service save may defeat a larger transaction.** #136, #152, and #163 must expose safe behavior that #137 and later #133 can compose.
10. **A warning is not a pass.** Validation status, disabled domains, selected candidate, repair actions, and unperformed migration remain testable and visible.

## 2. Required save metadata

Issue #137 is authorized to add the minimum metadata needed to distinguish legacy, current, and forward-version profiles. Any edit requires the `SaveGameData.cs` shared-file lock.

Recommended fields:

```csharp
public int SaveSchemaVersion;
public int ProfileInitializationVersion;
```

### `SaveSchemaVersion`

- `0`: legacy save with no explicit schema field;
- current supported version: assigned to new profiles and successfully migrated legacy profiles;
- lower supported version: migrate through explicit ordered migrations;
- higher version: preserve and load only through the forward-compatibility policy; do not auto-save or apply offline progress.

The exact first current value is Codex-owned, but it must be a positive constant and covered by tests. Do not derive schema compatibility from application version strings.

### `ProfileInitializationVersion`

This distinguishes an intentionally initialized profile from an empty/truncated collection.

- New-profile creation assigns the current initialization version after all baseline state is prepared.
- Legacy `0` does not mean “new profile.” It means “unknown legacy initialization state” and requires migration validation.
- Empty collections alone never authorize reseeding.
- A legacy profile receives the current initialization version only after the migration proves its baseline state is coherent or records an explicit degraded repair.

Do not use `SelectedRealm == None`, an empty wallet, an empty territory list, or an empty gem list as the sole new-profile signal.

## 3. Candidate validation outcomes

Primary, backup, previous, and any explicitly considered recovery generation are read and classified independently from immutable raw bytes. Temp files are never active candidates until the current save operation validates them.

Use equivalent typed outcomes to the following ordered set:

### 3.1 `Valid`

- structure parses;
- schema is supported;
- required objects exist;
- all enabled domain invariants pass;
- no normalization or repair is required.

### 3.2 `CompatibleNormalized`

The candidate needs only non-destructive backward-compatible defaults, such as:

- a missing top-level list becoming an empty list when that list did not exist in the legacy schema;
- missing `Reputation`, `FactionReputations`, or `LordPersona` becoming their approved empty/default values;
- missing nested optional collections becoming empty;
- a known ordered schema migration adding a newly introduced optional field with a neutral value.

No existing non-null value is replaced. No reward, starting balance, completion, chapter unlock, or content state is invented.

### 3.3 `CompatiblePreservedUnknown`

The candidate contains stable data the current build does not understand but can preserve safely, for example:

- a nonblank unknown quest ID;
- an unknown future equipment, faction, NPC, territory, or content ID in a known record shape;
- an explicitly optional unknown enum/profile whose raw value can round-trip safely.

Rules:

- preserve the record;
- exclude unsupported operations;
- do not reward, progress, classify, or mutate it;
- do not prefer an older backup solely because the primary contains preserved unknown IDs;
- if the entire schema version is newer than supported, apply the separate forward-schema read-only rule.

### 3.4 `DegradedMalformed`

The profile is structurally readable, but one or more records are ambiguous or unsafe:

- null list element;
- blank stable ID;
- duplicate stable identity;
- negative or overflowed currency/count/level where not supported;
- non-finite affinity or customization value;
- contradictory claimed/completed or custody state;
- missing established-profile core definition/state;
- malformed timestamp or impossible invariant.

Rules:

- preserve the raw candidate;
- disable the affected record group or domain;
- no mutation, reward, spending, progress, offline production, or classification from affected data;
- prefer a `Valid`, `CompatibleNormalized`, or `CompatiblePreservedUnknown` backup over this primary;
- if no cleaner generation exists, retain the overall profile in degraded mode rather than silently resetting everything.

### 3.5 `RepairableWithDataChange`

A deterministic reviewed repair can produce a safe candidate, but it changes or discards malformed data. Examples may include removing records with no stable identity or setting an invalid negative currency to a neutral value after all safer recovery generations fail.

A repair is allowed only when:

1. no cleaner primary/backup candidate exists;
2. original bytes are preserved in quarantine;
3. repair occurs on a clone;
4. every changed path and old/new value is recorded in diagnostics;
5. the repaired candidate passes full validation;
6. durable install succeeds;
7. load status reports semantic repair, not ordinary load;
8. the repair is covered by a specific migration/test rule.

No generic “clamp everything,” “drop all unknowns,” or “take the first duplicate” repair is approved.

### 3.6 `Invalid`

- unreadable/empty file;
- JSON does not produce a save object;
- unsupported structure cannot be preserved;
- required top-level candidate is absent;
- repair cannot produce a coherent profile;
- file cannot be read or safely quarantined because of I/O/permission failure.

An I/O access failure is not equivalent to content corruption and must not trigger destructive fallback.

## 4. Candidate selection algorithm

### 4.1 Read without mutation

For each generation:

1. retain raw bytes and source path;
2. parse into a candidate clone;
3. inspect schema/init metadata;
4. apply only in-memory non-destructive normalization for classification;
5. produce validation outcome, diagnostics, disabled domains, and proposed migrations;
6. do not update timestamps, apply offline progress, rotate files, or publish `CurrentSave` yet.

### 4.2 Ranking

Cleaner outcome ranks are:

```text
Valid
> CompatibleNormalized
> CompatiblePreservedUnknown
> DegradedMalformed
> RepairableWithDataChange
> Invalid
```

Selection rules:

- Primary wins when it is `Valid`, `CompatibleNormalized`, or `CompatiblePreservedUnknown`.
- A cleaner backup wins over a `DegradedMalformed`, `RepairableWithDataChange`, or `Invalid` primary.
- If primary and backup have the same loadable rank, prefer primary; do not choose by an untrusted timestamp alone.
- A `CompatiblePreservedUnknown` primary beats an older `Valid` backup because unknown stable data may be legitimate newer content.
- A forward-schema primary is preserved and exposed read-only; a supported cleaner backup may be offered as a recovery option but must not silently replace the newer profile.
- Temp is never chosen over primary/backup merely because it is newer.
- Previous/fallback files are considered only in the documented fallback recovery order.

### 4.3 No valid clean generation

If no candidate is cleaner than `DegradedMalformed`:

1. preserve/quarantine the original candidate bytes;
2. load the best structurally coherent profile in degraded/read-only mode where safe;
3. disable affected domains;
4. do not apply offline progress or auto-save;
5. expose recovery status and diagnostics;
6. run a data-changing repair only when an issue-approved rule exists;
7. create a new profile only when no candidate can safely preserve a coherent profile.

The presence of one malformed list does not by itself authorize `CreatedNewAfterUnrecoverableCorruption`.

## 5. Load and save status contract

Keep load and save observability separate as required by #137.

Minimum load statuses remain:

```text
None
LoadedPrimary
RecoveredFromBackup
CreatedNew
CreatedNewAfterUnrecoverableCorruption
RecoveryFailed
```

Add or expose equivalent detail for:

```text
LoadedPrimaryNormalized
LoadedPrimaryWithPreservedUnknown
LoadedPrimaryDegraded
RecoveredBySemanticRepair
LoadedForwardSchemaReadOnly
```

This may be one expanded enum or a base status plus a validation disposition. The API must let tests and UI distinguish them.

Minimum save statuses remain:

```text
None
SavedPrimary
SaveFailedPreviousPreserved
```

Also expose:

- candidate validation outcome;
- selected source path/generation;
- disabled domain IDs;
- diagnostic codes and affected record paths;
- whether the profile is writable;
- whether offline progress ran;
- whether a repair changed data;
- whether raw original evidence was quarantined.

Do not use one mutable string as the only contract.

## 6. Quest-state policy for #152

### 6.1 Null quest list

- legacy omission: normalize to an empty list;
- do not seed generic quest progress solely because the list is empty;
- definition-backed initialization belongs to an explicit migration/runtime path.

### 6.2 Null element

- classify the quest collection as malformed;
- queries and mutations skip it safely;
- no progress, completion, reward, event, or story consequence;
- preserve original raw bytes;
- a later data-changing repair may remove it only after quarantine and diagnostics.

### 6.3 Blank quest ID

- malformed and unsupported;
- never expose, progress, complete, or reward;
- do not reinterpret it as another quest;
- treat like a disabled record pending backup/repair.

### 6.4 Unknown nonblank quest ID

- compatible preserved unknown data;
- preserve every state field;
- exclude from current active/progress/claim operations;
- `ClaimReward` returns a visible unsupported result and changes nothing;
- if a matching definition returns in a later build, `IsClaimed` and completion state must still prevent duplicate reward.

### 6.5 Duplicate quest ID

Duplicates are ambiguous semantic corruption. Do not merge by max progress, sum, first, or last.

- mark the full duplicate-ID group disabled;
- grant no progress/reward/event from any member;
- prefer a cleaner backup;
- if no cleaner backup exists, preserve the group in degraded mode;
- an explicit later migration may reconcile only with an issue-approved rule and quarantined original evidence.

### 6.6 Contradictory known state

Examples:

- claimed but not completed;
- negative progress;
- progress beyond a definition without an approved over-completion rule;
- completed state that violates required definition constraints.

Disable mutation/reward for the affected quest and classify it malformed. Do not silently normalize into a rewardable state.

### 6.7 Service requirements

`LocalQuestService` and `SideQuestService` must:

- never dereference null records;
- never call string methods on a null/blank ID;
- use checked definition lookups;
- build a duplicate-ID set before active operations;
- preserve unknown states;
- exclude malformed/unknown/duplicate states from progress and reward;
- emit no normal completion/reward event on rejected operations;
- return or log stable diagnostics until a typed NVS result contract exists.

## 7. Relationship-field policy for #136 and #176

### 7.1 Missing top-level fields

The approved backward-compatible normalization is:

```text
Reputation == null            → empty list
FactionReputations == null    → empty list
LordPersona == null           → default object
```

This is `CompatibleNormalized` and must preserve every existing non-null value.

### 7.2 Malformed entries

- null NPC/faction entry: malformed, skip all operations on it;
- blank ID: malformed, no entry creation or mutation;
- duplicate ID: disable the duplicate group, do not select first;
- unknown nonblank ID: preserve but reject unsupported mutations until the authoritative catalog exists;
- non-finite affinity: malformed and not classifiable;
- overflowed faction/persona value: malformed; no wrap or classification.

### 7.3 Round-trip proof

#136 is complete only when real service mutation after normalization survives durable save/reload without resetting unrelated values and normalization is idempotent.

The narrow #136 PR should not invent catalogs, thresholds, labels, idempotency fields, or the larger #176 transaction seam.

## 8. Resource and Warzone Credit policy for #163

### 8.1 New-profile starting values

Starting balances are assigned only by explicit new-profile creation after a coherent initialization transaction.

They are never assigned because:

- a loaded wallet list is empty;
- one resource type is missing;
- a save is malformed;
- a realm is `None`;
- a service query requests an unknown/missing resource.

### 8.2 Missing entries in an established profile

- newly introduced optional/rare resource with an approved migration: add exactly the neutral migration value, normally `0`;
- missing core resource without a versioned migration: malformed wallet; prefer backup or disable wallet;
- never add current new-profile amounts to an established profile during generic normalization.

This replaces the unsafe assumption that `EnsureResource(..., startingAmount)` is valid for every loaded profile.

### 8.3 Null resource entry

- malformed;
- service calls skip safely and fail the wallet validation result;
- no income/spend/reward until a cleaner generation or approved repair exists.

### 8.4 Duplicate resource type

- malformed and ambiguous;
- do not sum, select first/last, or take min/max;
- disable wallet mutations;
- prefer a cleaner backup;
- preserve original evidence for an explicit migration.

### 8.5 Negative balance

- invalid for current resource/credit currencies;
- never spendable and never used as a base for income;
- no generic silent clamp;
- a repair-to-zero rule may be approved only after cleaner generations fail, with quarantine, diagnostics, and exact tests.

### 8.6 Unknown enum/resource type

- preserve when the raw enum value can round-trip;
- exclude from known-resource operations;
- do not reinterpret as the enum default;
- forward-schema policy applies if safe preservation is not proven.

### 8.7 Mutation amounts and arithmetic

- add requires positive amount; zero is a documented no-op; negative is rejected;
- consume/spend requires strictly positive amount; zero/negative is rejected;
- checked `long`/`int` arithmetic; overflow/underflow changes nothing;
- malformed wallet changes nothing and emits no normal changed event;
- no service-level save is added if it would defeat #133’s later atomic consequence transaction.

### 8.8 Warzone Credits

Apply the same signed amount, nonnegative stored balance, overflow, duplicate-delivery, and failure semantics to Warzone Credits. A negative spend must never add credits.

## 9. Current chapter and initialization defaults

A blank `CurrentChapterId` requires a deterministic compatibility rule, but generic save normalization must not invent story progress.

Until approved A1/G1 migration exists:

- new profiles may retain the current explicitly defined startup value used by the product flow;
- legacy blank chapter is classified through the versioned legacy migration, not by unconditional fallback during every read;
- no realm-specific Chapter 1 unlock is inferred from a blank value;
- #133 later owns NVS-01 chapter/context migration.

Codex must document the exact legacy rule and test that it does not unlock or complete content.

## 10. Forward-schema policy

When `SaveSchemaVersion` is greater than the maximum supported version:

- preserve raw primary and backup bytes;
- do not automatically rewrite, rotate, normalize destructively, or apply offline progress;
- expose `LoadedForwardSchemaReadOnly` or equivalent;
- make unsupported mutation services unavailable;
- preserve stable known fields only for read-only display when safe;
- never silently downgrade to an older backup without an explicit recovery choice/policy;
- do not create a new profile over the newer save;
- logs/player diagnostics must not expose private paths or raw data.

A future explicit downgrade migration can change this policy for a known version pair.

## 11. Offline progress and load publication

Required order:

```text
read raw generations
→ validate/classify candidates
→ select candidate
→ clone selected supported writable data
→ validate all domains needed by offline progress
→ apply checked offline progress to clone
→ prepare and durably persist candidate
→ verify primary/backup
→ publish clone as CurrentSave
→ expose load and save statuses separately
```

Rules:

- no offline progress on forward-schema, degraded wallet, degraded building/research state, or recovery-failed profile;
- no offline progress before candidate selection;
- failure to persist the progressed clone leaves the pre-progress data current or read-only;
- retrying load cannot apply the same interval repeatedly;
- resource arithmetic follows #163;
- building/research completion follows their later validated contracts;
- file recovery itself emits no quest/reward/progression callback.

## 12. Automatic save after load

Automatic save is allowed only when:

- schema is supported;
- candidate is writable;
- all applied normalization is non-destructive or an approved explicit migration;
- offline progress/migration candidate passes full validation;
- raw original is preserved before any data-changing repair;
- durable write and verification succeed.

Do not auto-save:

- forward-schema profiles;
- degraded malformed profiles without an approved repair;
- profiles whose disabled domain would be destructively rewritten;
- after recovery when the repair install failed;
- merely to replace a load/recovery message.

## 13. Save-write and backup interaction

The semantic policy composes with #137’s file algorithm:

1. Validate current primary before backup rotation.
2. Never copy malformed/unvalidated primary bytes over a cleaner backup.
3. Validate the temp candidate semantically, not only syntactically.
4. Final installed primary and expected backup are reopened and revalidated.
5. Ordinary I/O errors do not trigger unsupported-operation fallback.
6. Load status survives internal repair/progress save.
7. Quarantine retention and full deletion include semantic-repair evidence files.

A semantically malformed but structurally parseable primary is not “known-good backup” material.

## 14. Diagnostics and player behavior

Before #177 provides final in-game delivery, every non-normal outcome needs stable diagnostic data.

Suggested fields:

```text
code
severity
sourceGeneration
validationOutcome
recordPath
stableIdWhenAvailable
actionTaken
domainDisabled
rawEvidencePreserved
writable
```

Do not include full local file paths or raw save content in player-facing text.

Release behavior:

- keep the profile available where safe;
- disable unsafe action buttons/domains;
- do not show successful reward/spend/progress state;
- expose a recoverable/blocking status through #177 later;
- never silently create a new profile while a coherent degraded profile exists.

## 15. Required candidate-selection tests

At minimum:

| Primary | Backup | Expected |
| --- | --- | --- |
| Valid | any | primary |
| CompatibleNormalized | Valid | primary after non-destructive migration |
| CompatiblePreservedUnknown | Valid | primary, unknown records preserved |
| DegradedMalformed | Valid | recover backup |
| RepairableWithDataChange | CompatibleNormalized | recover backup |
| Invalid | Valid | recover backup |
| DegradedMalformed | DegradedMalformed | preserve/select deterministic primary degraded; no offline/save |
| RepairableWithDataChange | Invalid | quarantine original, apply only approved repair, verify |
| Invalid | Invalid | new profile only when neither can preserve coherent profile |
| Forward schema | older Valid | preserve forward profile read-only; no silent downgrade |
| I/O inaccessible primary | Valid backup | recovery failure unless primary can be safely preserved; no destructive overwrite |

Also test same-rank primary preference and no timestamp-only candidate switching.

## 16. Required domain tests

### Quests

- null list;
- null element;
- blank ID;
- unknown nonblank ID preserved;
- duplicate ID group disabled;
- contradictory known state;
- unknown claimed state later regains definition without duplicate reward;
- no event/reward/progress from rejected records;
- save/reload preserves supported and unknown states.

### Relationships

- missing three fields normalize;
- existing values preserved exactly;
- idempotent normalization;
- real mutation and durable round trip;
- null/blank/duplicate/non-finite state rejected without misleading classification.

### Resources/credits

- new profile gets baseline once;
- empty established wallet does not receive starting amounts;
- approved new optional resource migrates with zero;
- null/duplicate/negative/unknown record;
- signed mutation validation;
- checked overflow/underflow;
- invalid operation emits no normal event/save;
- cleaner backup wins over ambiguous wallet.

### Persistence/offline

- forward schema remains byte-preserved/read-only;
- degraded candidate is not auto-saved;
- offline progress runs on clone and publishes only after save success;
- repeated failed load cannot duplicate production/timer completion;
- load and save statuses remain distinct;
- data-changing repair preserves original quarantine and records exact changes.

## 17. Merge order and shared-file lock

### Independent first lanes

1. **#136** — narrow missing-field normalization plus real relationship mutation/save/reload evidence. No `SaveGameData.cs` change is expected if current fields already exist.
2. **#152** — safe quest/side-quest consumption, duplicate-set blocking, checked definitions, preserved unknown states, and focused tests. Avoid broad persistence rewrite.
3. **#163** — reject signed/overflow exploits and unsafe wallet operations. It may add validators/results without seeding/repairing save data.

### Persistence lane

4. **#137** begins after #136 and #152. It takes the `SaveGameData.cs` soft lock only if adding the schema/init metadata authorized here. It implements candidate classification/selection, statuses, file safety, fault injection, semantic validation, offline clone/persist/publish, deletion, and approved repair behavior.

### Later integrations

5. #163 save migration behavior rebases onto #137 if it requires persistent wallet repair metadata.
6. #176, #165, #166, #168, #169, #171, #172, #173, #174, #177, #180, #181, #183, and #184 extend the validator through focused domain policies; they do not weaken this candidate-selection model.
7. #133 defines NVS-01 state, transaction ledger, and chapter/context migration after the save foundation is accepted.

Do not run parallel edits to `SaveGameData.cs`.

## 18. Definition of done for this foundation

- #136 proves non-destructive normalization and durable relationship-field round trip.
- #152 makes malformed/unknown/duplicate quest state non-crashing and non-rewarding.
- #163 prevents signed and overflow economy exploits and refuses ambiguous wallets.
- #137 ranks primary/backup candidates semantically, preserves future/unknown data, avoids false new-profile creation, and implements durable crash-safe writes/recovery/deletion.
- Forward schema is never automatically rewritten.
- New-profile starting values are never used as generic loaded-save repair.
- Offline progress cannot duplicate after failed persistence.
- Every data-changing repair preserves original evidence and is explicit.
- Full candidate, domain, fault, recovery, deletion, and reload matrices pass in Unity 2022.3.62f3.
- No narrative meaning, reward amount, balance, Android source, terrestrial design, or unrelated feature is changed.

# Handoffs

## Codex — #136

```text
Implement only the compatible missing-field normalization and real reputation/faction/persona mutation-save-reload tests from issue #136 and Save_Semantic_Compatibility_Policy.md. Preserve non-null data and leave larger duplicate/range/transaction behavior to #176.
```

## Codex — #152

```text
Implement safe quest-state consumption from Save_Semantic_Compatibility_Policy.md: skip null/blank records, preserve unknown nonblank IDs, disable duplicate-ID groups, use checked definitions, and grant no progress/reward/events for unsupported data. Do not collapse duplicates or rewrite the save format.
```

## Codex — #163

```text
Implement signed amount, checked arithmetic, null-safe, and ambiguous-wallet rejection from issue #163 and Save_Semantic_Compatibility_Policy.md. Do not seed starting balances into established profiles or silently repair duplicate/negative wallets.
```

## Codex — #137

```text
After #136 and #152 merge, implement the typed candidate validation/selection, schema/init metadata, crash-safe file algorithm, separate statuses, forward-schema read-only behavior, clone-persist-publish offline progress, quarantine/repair evidence, and full deletion defined by issue #137 and Save_Semantic_Compatibility_Policy.md. Declare the SaveGameData.cs lock before editing it.
```