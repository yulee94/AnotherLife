# Realm Selection Integrity Specification

**Status date:** 2026-07-15  
**Specification owner:** GPT  
**Runtime implementation owner:** Codex  
**Narrative/content owner:** Android Studio  
**Tracking issue:** #173  
**Baseline `main`:** `0bfae4c89be56fcb2e1db1180513694d0b136272`

## 1. Product decision

AnotherLife supports **one committed realm per local profile** for the current product phase.

Before committing, the player may inspect and compare all valid playable realms without changing the save. After a realm is durably committed:

- requesting the same realm is idempotent and returns an “already selected” result;
- requesting a different realm is rejected without mutation;
- changing realm requires an explicit new-profile/reset flow after #137 provides verified full deletion and #177/#178 provide safe confirmation and feedback;
- in-place realm transfer is not supported in the current milestone.

This policy provides the best current user experience because it prevents silent orphaning, duplication, or conversion of realm-specific progress while preserving full freedom before the first commitment.

A future story-supported realm transfer may be designed as a separate product feature with a complete migration matrix. This specification does not pre-authorize it.

## 2. Goal

Create a durable, validated, one-time realm identity that:

- accepts only a defined playable realm;
- cannot commit `None` or an undefined/future enum as a normal realm;
- is consistent across `CurrentRealmId`, the resolved realm definition, save data, scene flow, and NVS-01 eligibility;
- commits once through a recoverable save boundary;
- does not reinitialize state on duplicate requests;
- cannot change in memory if persistence fails;
- exposes typed results and one post-commit event;
- preserves old and forward-version saves according to the merged save semantic policy;
- does not invent realm lore, bonuses, resources, chapters, or narrative outcomes.

## 3. Non-goals

Do not:

- implement realm transfer or cross-realm migration;
- change realm names, descriptions, bonuses, starting resources, rare-resource values, art, or narrative meaning;
- add NVS-01 dialogue or runtime state;
- redesign the realm-selection screen;
- implement full profile deletion;
- seed territories, Gems, world events, quests, or chapters from hard-coded service fallbacks;
- modify Champion combat or terrestrial design;
- treat `RealmId.None` as a valid playable selection;
- silently map an unknown realm to Crownlands or another realm;
- call `CreateNewSave(id)` as a substitute for an atomic selection transaction.

## 4. Verified current behavior

`LocalRealmService.SelectRealm(RealmId id)` currently:

```text
if no CurrentSave:
    CreateNewSave(id)
else:
    CurrentSave.SelectedRealm = id
    Save()
log success
```

Consequences:

- `RealmId.None` and undefined enum values are accepted;
- no realm definition is required;
- an existing committed realm can be overwritten indefinitely;
- no result distinguishes first selection, duplicate, invalid, failed save, or rejected change;
- in-memory identity changes before durable persistence succeeds;
- root profile initialization and realm selection are conflated;
- no idempotency/transaction identity exists;
- downstream systems can observe a selected ID whose definition is null;
- NVS-01 prerequisites cannot distinguish preview from committed identity.

## 5. Authority and prerequisites

### 5.1 Technical realm authority

Production selection requires a validated realm definition from the authoritative game-data catalog established by #183.

At minimum, the catalog query must distinguish:

```text
FoundValid
UnknownId
UnavailableCatalog
InvalidCatalog
UnsupportedVersion
NotPlayable
```

A nullable `GetRealm(id)` alone is insufficient as the final result contract, though it may be adapted during transition.

### 5.2 Narrative/content authority

Android Studio owns:

- realm names and descriptions;
- lore and culture;
- player-facing selection copy;
- story consequences and realm-specific Chapter 1 content;
- localization keys.

Codex owns validation, transaction, persistence, query APIs, event delivery, and runtime integration. GPT owns this transition/migration contract.

### 5.3 Required upstream foundation

Full production implementation is blocked by:

- #137 — clone/validate/persist/publish save transaction and schema/init metadata;
- #183 — one validated realm-definition authority.

A narrow interface/result prototype may be prepared earlier, but no PR may claim durable selection completion until both foundations are accepted.

## 6. Persisted model

Use the minimum fields required to distinguish preview, uncommitted, committed, and migrated identity.

Recommended save fields, added only under a declared `SaveGameData.cs` lock:

```csharp
public RealmId SelectedRealm;
public bool RealmSelectionCommitted;
public string RealmSelectionTransactionId;
public int RealmCatalogVersionAtSelection;
```

Equivalent structured state is acceptable.

### 6.1 Field meaning

#### `SelectedRealm`

- committed playable realm when `RealmSelectionCommitted == true`;
- `None` when no realm has been committed;
- an undefined raw enum value from old/forward data is invalid/forward data, not automatically `None`.

#### `RealmSelectionCommitted`

- false: no authoritative realm choice exists;
- true: `SelectedRealm` must resolve to one valid playable definition and transaction metadata must be coherent.

#### `RealmSelectionTransactionId`

- stable nonblank one-time identity for the successful first commit;
- duplicate delivery with the same transaction ID returns the prior committed outcome;
- a different transaction requesting another realm cannot replace the committed identity.

A locally generated GUID/string is acceptable if persisted with the candidate and supplied consistently through retry/recovery.

#### `RealmCatalogVersionAtSelection`

- version of the validated realm catalog used at commit;
- used for diagnostics/migration, not as permission to ignore current missing definitions;
- unsupported future version follows the save forward-compatibility policy.

## 7. New-profile and preview states

### 7.1 Profile creation

A new local profile may exist before realm selection with:

```text
SelectedRealm = None
RealmSelectionCommitted = false
```

New-profile baseline creation and realm selection are two separate operations, even when the UI presents them consecutively.

Do not infer an uncommitted clean profile solely from `SelectedRealm == None`. Validate schema/init metadata and profile coherence through #137.

### 7.2 Preview

Realm browsing/preview is transient UI state:

- not written to `SaveGameData`;
- does not initialize realm resources, quests, Gems, territories, advisors, chapters, or world state;
- may request read-only catalog presentation data;
- survives ordinary UI recomposition as local screen state where appropriate;
- does not emit `RealmSelected` or unlock NVS-01.

### 7.3 Commit confirmation

The final UI may ask for confirmation before committing. The technical service accepts a request only when the UI has chosen to commit; it does not own the final player-facing wording.

Repeated button taps must collapse to one active request/transaction.

## 8. Selection request and result

Replace `void SelectRealm(RealmId id)` with a typed request/result, or add a compatible typed method while deprecating the void path.

Suggested request:

```text
transactionId
requestedRealmId
expectedProfileId or save generation when available
source = InitialRealmSelection
realmCatalogVersion
```

Suggested result statuses:

```text
Committed
AlreadyCommittedSameRealm
RejectedDifferentRealm
InvalidRealm
RealmDefinitionUnavailable
ProfileUnavailable
ProfileNotEligible
SaveFailedPreviousPreserved
DuplicateRequest
TransactionMismatch
ForwardSchemaReadOnly
InitializationFailed
```

Each result includes:

```text
requestedRealmId
committedRealmId when available
transactionId
catalogVersion
mutationOccurred
persisted
technicalCode
```

Player-facing copy is resolved separately through #177/localization.

## 9. Validation rules

A request is valid only when:

- transaction ID is nonblank and well-formed;
- requested realm is one of the approved playable IDs:
  - Stonehold;
  - Eldergrove;
  - Crownlands;
  - Umbral;
- requested realm is not `None`;
- requested enum value is defined;
- authoritative catalog is loaded and supported;
- definition exists and is marked playable;
- current save is supported and writable;
- profile state is eligible for initial selection;
- no different realm has already been committed;
- any required initialization plan validates before mutation.

Validation happens before changing any save-backed object.

## 10. Profile eligibility

### 10.1 Eligible

A profile is eligible when:

- schema is supported/writable;
- `RealmSelectionCommitted == false`;
- `SelectedRealm == None`;
- profile initialization metadata is coherent;
- no realm-specific state has already been durably initialized by a prior/malformed transaction;
- no active realm-dependent encounter/transaction exists.

### 10.2 Already committed same realm

When committed realm equals requested realm:

- return `AlreadyCommittedSameRealm`;
- no save mutation;
- no event;
- no reinitialization;
- no duplicate rewards/resources/unlocks;
- result may expose the original transaction ID for diagnostics.

If the same transaction is replayed after reload, return the previously committed outcome or equivalent duplicate-safe status.

### 10.3 Already committed different realm

- return `RejectedDifferentRealm`;
- preserve all state;
- no save;
- no event;
- direct the presentation layer toward the future explicit new-profile/reset path;
- never run partial migration or cleanup.

### 10.4 Ambiguous legacy state

Examples:

- `RealmSelectionCommitted == false` but a playable `SelectedRealm` exists;
- committed flag true with `None` or unknown realm;
- realm-specific progression exists while selected realm is missing;
- duplicate/partial transaction metadata;
- definition missing for a supposedly committed realm.

Classify through #137’s semantic candidate model:

- prefer cleaner backup;
- preserve degraded profile when possible;
- do not allow a new selection until an approved migration resolves identity;
- do not reset or infer Crownlands.

## 11. Initial selection transaction

Required order:

```text
receive request
→ validate request and catalog
→ validate current profile eligibility
→ clone current save
→ prepare selected realm and commit metadata on clone
→ prepare only approved realm-derived baseline state
→ validate entire candidate
→ durably persist and verify through #137
→ publish clone as CurrentSave
→ publish one committed realm-selection event
→ allow scene/NVS eligibility progression
```

### 11.1 Approved first-commit mutations

The first implementation may mutate only:

- `SelectedRealm`;
- selection committed flag/state;
- transaction/catalog metadata;
- neutral technical fields explicitly required by an approved catalog/migration.

Do not invent or alter:

- common resource balances;
- rare-resource amounts;
- building/research/troop state;
- territories or Realm Gems;
- Wishgate/Warmaster/equipment;
- reputation/factions/persona;
- quests, chapters, story nodes, or NVS consequences;
- customization;
- world events.

If another system requires realm-derived initialization, its issue must supply a validated, idempotent preparation step before it joins this transaction.

### 11.2 Save failure

On any persistence/verification failure:

- discard the mutated clone;
- retain/publish the prior current save and realm identity;
- return `SaveFailedPreviousPreserved`;
- emit no realm-selected event;
- trigger no navigation, NVS offer, resources, or downstream initialization;
- expose stable diagnostics.

## 12. Event contract

Publish one event only after verified durable commit.

Suggested payload:

```text
transactionId
previousRealmId = None
newRealmId
realmCatalogVersion
profileGeneration or save revision
committedAtUtc
```

Requirements:

- event exactly once per successful first selection;
- no event for preview;
- no event for same-realm duplicate;
- no event for rejected different realm;
- subscriber exception cannot undo or prevent the core committed state;
- each subscriber handles duplicate event delivery by transaction ID where its own state changes;
- event contains technical IDs, not player-facing copy.

## 13. Query contract and consistency

Current properties may remain for compatibility, but add a typed snapshot/query result such as:

```text
RealmIdentityStatus
realmId
resolvedDefinition
selectionCommitted
catalogVersionAtSelection
transactionId
```

Minimum statuses:

```text
Uncommitted
CommittedValid
CommittedDefinitionUnavailable
InvalidPersistedIdentity
ForwardSchemaReadOnly
ProfileUnavailable
```

### Consistency guarantees

- `CommittedValid` always has a playable non-None ID and matching definition;
- a non-None ID with missing definition is never reported as a valid current realm;
- `CurrentRealmId` and `CurrentRealm` cannot silently disagree;
- query methods do not mutate, initialize, migrate, or save;
- no query substitutes Crownlands;
- runtime callers requiring authority must check committed-valid status rather than only `RealmId != None`.

## 14. NVS-01 mapping

Approved D10/D12/D15 require one committed playable realm before `OMEN_1` is offered.

Technical eligibility is:

```text
RealmIdentityStatus == CommittedValid
AND selection transaction durably persisted
AND approved post-realm prologue start condition
```

Not sufficient:

- a previewed realm card;
- a non-None raw enum value;
- a null/missing definition;
- an in-memory selection whose save failed;
- a fallback Crownlands context;
- a degraded/forward-schema read-only profile.

After successful NVS-01 report completion, #133 maps `CH1_REALM_INTRO` to this immutable committed realm. It must not read an independently mutable preview state.

## 15. Scene and UI behavior

### Realm-selection UI

- preview does not mutate save;
- commit button is debounced/disabled while transaction pending;
- success navigation occurs only for `Committed` or already-committed same-realm resume behavior;
- save/catalog/eligibility failure leaves player on selection screen with visible status;
- a committed different realm cannot be selected; UI explains that a new profile is required through localized copy later;
- no raw enum/internal transaction ID appears in release copy.

### Boot/Kingdom/Champion

- scene controllers consume committed-valid identity;
- invalid/uncommitted context produces unavailable behavior rather than Crownlands substitution;
- #150 owns scene availability and routing;
- #178 disables realm-dependent mutation commands until valid identity and domain results exist;
- #180 distinguishes explicit demo context from authoritative realm context.

## 16. New-profile/reset relationship

Changing realm is implemented by creating a separate clean profile, not by editing `SelectedRealm`.

Before any production reset/new-profile UX exists:

- #137 must delete all profile generations and report typed success/failure;
- #177 must show blocking failure/success status;
- #178 must place reset outside the command deck with explicit confirmation;
- no new profile is created until deletion is verified;
- cancellation/failure preserves the current committed realm and scene.

A future multiple-profile feature may allow creating another profile without deleting the current one. This is outside the current scope but compatible with one realm per profile.

## 17. Old-save migration

### 17.1 Legacy save with defined playable `SelectedRealm`

If the save predates committed metadata but otherwise validates:

- migrate as already committed to that realm;
- generate deterministic migration transaction metadata or a stable migration identity;
- preserve all existing state;
- emit no “new selection” gameplay rewards/event during migration;
- persist only through #137’s explicit migration path;
- record catalog version and diagnostics.

### 17.2 Legacy save with `SelectedRealm == None`

- do not assume a fresh profile solely from `None`;
- if profile initialization and absence of realm-specific state prove it is an uncommitted legacy profile, migrate to uncommitted status;
- otherwise classify degraded/ambiguous and block selection pending recovery/migration;
- no generic reseeding.

### 17.3 Undefined/unknown realm enum

- preserve raw data under save compatibility rules;
- never reinterpret as `None` or a playable realm;
- forward-schema/read-only or degraded behavior applies;
- no selection, offline realm production, scene progression, or NVS eligibility.

### 17.4 Missing current definition

- committed identity is unavailable, not erased;
- preserve realm ID/metadata;
- block authoritative realm-dependent mutations;
- prefer a cleaner backup only when the save semantic policy ranks it cleaner for more than legitimate unknown content;
- a catalog repair/reintroduction may restore `CommittedValid` without duplicating selection.

## 18. Failure and duplicate matrix

| Condition | Result | Mutation | Event/navigation |
| --- | --- | --- | --- |
| valid first selection | Committed | persisted once | one event, then navigation |
| same request while pending | DuplicateRequest/pending | none extra | none extra |
| same transaction after commit | duplicate/previous result | none | no second event |
| new transaction, same realm | AlreadyCommittedSameRealm | none | resume allowed, no event |
| different realm after commit | RejectedDifferentRealm | none | no navigation to reinitialize |
| None/undefined realm | InvalidRealm | none | none |
| missing/invalid catalog | RealmDefinitionUnavailable | none | none |
| save unavailable/degraded | ProfileUnavailable/NotEligible | none | none |
| save write/verify failure | SaveFailedPreviousPreserved | prior state retained | none |
| subscriber throws after commit | committed with diagnostic | state remains | other subscribers continue where possible |
| forward schema | ForwardSchemaReadOnly | none | none |

## 19. Required tests

### 19.1 Request validation

- each four playable realms commits from an eligible profile;
- `None` rejected;
- undefined enum rejected;
- missing definition;
- non-playable definition;
- unavailable/invalid/unsupported catalog;
- blank/duplicate/mismatched transaction ID;
- null save/game-data service;
- forward-schema read-only profile.

### 19.2 Preview and commit

- preview each realm changes no save bytes/state/events;
- first commit changes only approved selection fields;
- candidate validates before persist;
- durable reload returns same committed valid realm;
- event exactly once after persistence;
- navigation waits for committed result;
- rapid repeated click produces one transaction.

### 19.3 Idempotency and lock policy

- same transaction same session;
- same transaction after reload;
- different transaction same realm;
- different realm after commit;
- no reinitialization, save, event, resource, quest, Gem, territory, chapter, or reward duplication;
- direct attempt to mutate public save realm field is unavailable or caught by semantic validation where possible.

### 19.4 Persistence faults

Inject failure:

- before candidate serialization;
- temp write/validation;
- primary install;
- final verification;
- after durable commit before event;
- event subscriber throws.

Assert prior in-memory/persisted realm remains coherent and retry behavior is deterministic.

### 19.5 Old/invalid saves

- legacy valid selected realm migrates committed without selection rewards/events;
- legacy None clean uncommitted profile;
- legacy None with realm-specific progress becomes degraded/blocked;
- committed flag true with None;
- committed flag false with playable realm;
- undefined realm value;
- missing definition;
- forward catalog/schema;
- cleaner backup selection through #137.

### 19.6 Integration

- `CurrentRealmId` and resolved definition consistent;
- resource rare-realm query does not run for invalid/uncommitted identity;
- #178 command availability uses committed-valid result;
- #150 scene flow waits for commit;
- #180 authoritative Champion context never substitutes Crownlands;
- #128/#133 NVS eligibility only after durable commit;
- safe #127 PlayMode profile preservation and reload.

## 20. Expected file boundary

Likely:

```text
unity/Assets/AL/Scripts/Core/Interfaces/IRealmService.cs
unity/Assets/AL/Scripts/Services/Local/LocalRealmService.cs
new realm request/result/snapshot types
focused EditMode tests
focused PlayMode tests after #127
```

Potential shared files, only after prerequisites and explicit lock:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
```

Do not edit:

- Android narrative packet;
- scenes or Build Settings;
- resource/progression/territory/Gem logic;
- Champion combat;
- terrestrial design;
- broad save algorithms outside the #137 seam.

## 21. Implementation split and order

### Phase A — contract and pure validation

After #183 provides a validated realm query, Codex may add:

- typed request/result/snapshot;
- playable realm validation;
- same/different realm policy;
- pure tests.

No claim of durable completion until #137 exists.

### Phase B — persisted one-time commit

After #137:

- add committed metadata under the `SaveGameData.cs` lock;
- implement old-save migration;
- clone/persist/verify/publish transaction;
- event and scene/NVS integration;
- fault/reload/duplicate tests.

### Phase C — downstream consumers

- #178 command availability;
- #150 scene flow;
- #180 encounter/realm context;
- #128/#133 NVS eligibility and Chapter 1 mapping.

## 22. Acceptance criteria

- [ ] One committed realm per profile is the enforced product policy.
- [ ] Preview never mutates persisted identity.
- [ ] Only a defined playable realm can commit.
- [ ] `None`, undefined, missing-definition, degraded, and forward-version states cannot become authoritative.
- [ ] First commit is clone/validate/persist/verify/publish and changes only approved fields.
- [ ] Same-realm retries are idempotent.
- [ ] Different-realm requests are rejected without migration or mutation.
- [ ] Save failure cannot split in-memory and persisted identity.
- [ ] `CurrentRealmId` and resolved definition cannot silently disagree.
- [ ] Realm event occurs once after durable commit.
- [ ] Legacy valid selected realms migrate without duplicate initialization/reward.
- [ ] NVS-01 eligibility requires committed-valid identity.
- [ ] Realm-dependent runtime never silently substitutes Crownlands.
- [ ] Full validation, duplicate, old-save, fault, reload, and integration tests pass.
- [ ] No realm balance, narrative, Android, scene, terrestrial-design, or unrelated behavior changes.

# Codex handoff

```text
Codex: implement issue #173 according to Realm_Selection_Integrity_Spec.md after #137 and #183. The current product policy is one committed realm per profile: preview is transient, first commit is atomic and durable, same-realm retry is idempotent, and different-realm change is rejected in favor of a future explicit new-profile/reset flow. Add typed request/result/snapshot APIs, never commit None/undefined/missing definitions, migrate valid legacy selected realms without duplicate initialization, remove Crownlands substitution from authoritative consumers, and prove save-failure, duplicate, reload, and NVS eligibility behavior. Do not implement realm transfer, rebalance realms, rewrite narrative, change scenes, or touch terrestrial design.
```