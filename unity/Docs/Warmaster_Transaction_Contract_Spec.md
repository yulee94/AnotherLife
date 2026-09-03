# Warmaster Catalog, Entitlement, and Transaction Integrity Specification

**Status:** Binding Codex coordination/review specification for issue #171
**Status date:** 2026-09-03
**Audited base:** `78811dd4f35a75e2f82cc421fa0d2af82420e13a`
**Coordination/review owner:** Codex
**Implementation owner:** Codex engineering
**Narrative/content source owner:** Codex narrative/content
**Final balance, visual, integrated-playtest, and release approval:** User
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

## 1. Purpose

This specification defines the fail-closed boundary for Warmaster catalog
authority, saved-state validation, purchases, set entitlement, equipment,
replay, persistence, recovery, and post-commit presentation.

It replaces the current implicit flow:

```text
caller supplies piece ID and price
-> live credits mutate
-> live Warmaster state mutates
-> one save is attempted
-> every non-primary result is treated as rollback
-> a Boolean or log line reports the outcome
```

with:

```text
approved immutable catalog snapshot
+ validated immutable saved-state snapshot
+ authorized profile-scoped operation
-> pure deterministic application plan
-> clone accepted profile generation
-> apply one definition-owned debit and Warmaster mutation
-> persist and verify one candidate transaction
-> publish an immutable committed receipt
-> emit one typed post-commit intent
```

This specification does not approve a price, completion threshold,
experience award, level rule, equipment stat, visual asset, production
catalog, save migration, service registration, gameplay caller, or release.

## 2. Binding dependencies and delivery sequence

Warmaster work consumes, rather than duplicates, these authorities:

```text
unity/Docs/Game_Data_Catalog_Authority_Spec.md
unity/Docs/Economy_Integrity_Spec.md
unity/Docs/Save_Profile_Identity_And_Write_Authority_Spec.md
unity/Docs/Save_Semantic_Compatibility_Policy.md
unity/Docs/Notification_Delivery_Contract_Spec.md
unity/Docs/Ownership_Decision_Record.md
unity/Docs/Narrative/GameData/Warmaster_Content_Source_Handoff.md
```

The authorized order is:

```text
this coordination specification
-> pure engine-free planner using injected fixture authority
-> approved Warmaster technical family and balance decisions through #183
-> profile-bound candidate persistence and migration through #137/#450
-> typed economy application through #163
-> service and caller migration
-> typed post-commit presentation through #177
-> Player/device evidence and user approval
```

Only the pure planner may proceed before the production dependencies. It must
not read a file, mutate a save, register a service, grant value, emit a
notification, or contain a fallback production catalog.

## 3. Verified current-source baseline

### 3.1 Accepted protections

Merged PR #311 already ensures that current `LocalWarmasterService`:

- returns a detached state copy;
- does not initialize lists during queries;
- rejects blank/unknown prototype piece IDs and nonpositive prices;
- rejects null, blank, duplicate, negative, and contradictory current rows;
- stages the typed no-save Warzone Credit spend before one save;
- restores ordinary in-memory failures;
- does not charge or add experience again for an already-owned piece;
- counts unique known prototype pieces rather than raw list length.

No current production caller reaches `PurchasePiece`, `UnlockSet`, or
`EquipSet`. That containment must remain in place until this specification's
production phases are accepted.

### 3.2 Remaining unsafe behavior

Current runtime still:

- hard-codes one set ID and ten piece IDs;
- hard-codes a completion count and experience increment;
- accepts a caller-controlled Warzone Credit price;
- lets `UnlockSet` bypass piece or entitlement proof;
- treats a stored Boolean or raw piece count as True Warmaster authority;
- has no catalog revision, profile operation ID, state revision, ledger, or
  receipt;
- maps `CommitUncertain` to an ordinary in-memory rollback even though disk
  may contain the charge and piece;
- cannot distinguish a newly committed purchase from a duplicate;
- cannot reconcile legacy flag/list/unlock/equip disagreement.

### 3.3 Rejected implementation lineage

Closed PR #506 is not authority and must not be revived or cherry-picked. It
created a production-named technical catalog before this specification,
before #183 source completion, and before the user's balance approvals.
Its green mechanical validation did not authorize its price, threshold,
experience, slot, stat, or activation fields.

The later pure planner may reuse general validation lessons, but it must use
test fixtures only and must fail closed when an approved production catalog
or required balance field is unavailable.

## 4. Authority and ownership boundaries

### 4.1 Technical authority

Codex engineering owns:

- immutable contract types;
- strict validation and deterministic canonicalization;
- checked arithmetic;
- stale-state and stale-catalog detection;
- application plans and candidate-state construction;
- operation ledgers and receipt verification;
- persistence/recovery integration;
- typed event and notification intents;
- tests and retained evidence.

### 4.2 Narrative/content authority

The merged Warmaster source owns the retained set and piece identities,
localization keys, draft names, summaries, and meaning boundaries. Runtime
code must not convert those drafts into price, power, progression, equipment
statistics, entitlement, or release authority.

### 4.3 Balance authority

The user owns approval of:

- price and currency amount for every purchasable piece;
- the required completion rule;
- level and experience progression rules;
- completion grants and auto-unlock/auto-equip behavior;
- equipment statistics and gameplay power.

Observed current literals are evidence of legacy behavior, not approval.
They must not appear as defaults in the new catalog, planner, migration, or
tests. Tests use conspicuously synthetic fixture values and assert only that
definition-owned values are followed exactly.

## 5. Terminology

### 5.1 Catalog snapshot

An immutable, fully validated technical authority with one schema version,
content version, source revision, canonical hash, set membership, and every
required approved rule. Partial catalogs publish no snapshot.

### 5.2 Saved-state snapshot

An immutable detached view of one profile's Warmaster fields, state revision,
catalog binding, and bounded transaction ledger. It is not a mutable save.

### 5.3 Entitlement

Typed authority to unlock a Warmaster set. It is derived only from a complete
approved rule or a verified migration record. A Boolean, raw list length,
display name, or caller assertion is not entitlement authority.

### 5.4 Prepared plan

A deterministic immutable description of expected state, candidate state,
definition-owned economy debit, ledger append, and post-commit intent. A plan
has applied no value and is not a successful purchase.

### 5.5 Verified receipt

An immutable result created only after an adapter proves that the exact
candidate profile generation persisted and reloaded with the expected state,
economy, ledger, revision, and fingerprint.

### 5.6 Exact replay

A request with the same profile, operation identity, canonical request
fingerprint, catalog binding, and expected semantic result. It returns the
existing verified record or receipt with zero debit, mutation, save, event,
or notification.

### 5.7 Collision

Reuse of an operation or event identity with any different semantic input.
It is an integrity failure, never a duplicate success.

## 6. Stable identities and canonical comparison

Required identities are:

```text
catalogSchemaVersion
catalogContentVersion
catalogSourceRevision
catalogHash
setId
pieceId
currencyId
profileId
actorId
operationId
eventId
correlationId
entitlementId when an external entitlement is approved
rewardApplicationId when a completion grant is approved
notificationCorrelationId after commit
```

Catalog-owned technical IDs use the stable catalog grammar approved by #183.
Runtime/profile identities use their owning authority grammar. Every identity
is ordinal and case-sensitive. Code must not trim, lowercase, normalize,
fuzzy-match, or fall back to a display name.

Unknown nonblank catalog identities in an existing save are preserved as
opaque future data and excluded from current calculations. Unknown identities
in a new request reject.

Canonical request, state, plan, ledger, and receipt hashes use:

- UTF-8;
- invariant numeric formatting;
- explicit enum numeric values or stable tokens;
- ordinal collection ordering defined by the accepted snapshot;
- length-prefixed fields to prevent concatenation ambiguity;
- lowercase 64-hex SHA-256 output;
- an explicit contract-version prefix.

Wall clock, frame count, process hash codes, random values, mutable object
identity, and localized text are prohibited hash inputs.

## 7. Bounded immutable catalog contract

### 7.1 Catalog publication status

```text
Ready
Unavailable
UnsupportedVersion
Malformed
Incomplete
ApprovalMissing
```

Only `Ready` publishes a non-null catalog snapshot. Every other status
publishes no partial set or piece lookup.

### 7.2 Catalog snapshot fields

Equivalent immutable shape:

```text
schemaVersion
contentVersion
sourceRevision
catalogHash
currencyBinding
ordered sets
ordered pieces
approval record/revision
```

Each set definition contains:

```text
setId
ordered member piece IDs
completion rule
unlock rule
equip rule
optional completion grant rule
```

Each piece definition contains:

```text
pieceId
owning setId
definition-owned price
optional progression rule
availability state
```

The pure planner does not define production values. Its injected fixture
catalog must provide all required fields. Missing approval or a missing rule
returns `ApprovalMissing`/`Unavailable`, never zero or a current literal.

### 7.3 Catalog validation

Publication rejects:

- missing or duplicate set/piece IDs;
- duplicate membership or one piece in multiple sets unless explicitly
  supported by a future schema;
- member references to missing pieces;
- piece ownership that disagrees with set membership;
- blank, unknown, or unsupported currency identity;
- nonpositive, overflowing, or unapproved prices;
- missing or unapproved completion/progression/equip rules;
- inconsistent content version, revision, hash, provenance, or approval;
- extra required members, partial records, null rows, or unsupported enums;
- mutable caller-owned collections after publication.

The initial pure contract is bounded to 64 sets, 1,024 pieces, 1,024 member
references, a 128-byte UTF-8 ceiling per technical/opaque ID, and one
definition-owned currency debit per purchase. These are safety limits, not
game-balance recommendations.

### 7.4 Catalog drift

Every request binds exact schema version, content version, source revision,
and catalog hash. Any change before application or verification returns
`StaleCatalog`. The adapter must re-plan against the new authority; it may not
substitute new price, membership, or progression values into an old plan.

## 8. Immutable saved-state contract

The validated snapshot contains:

```text
status
profileId
stateRevision
catalog binding, if previously migrated
ordered purchased piece IDs
ordered unlocked set IDs
equipped set ID or empty
stored True Warmaster flag as historical evidence only
level
experience
ordered transaction records
isComplete
```

The snapshot copies every input collection. It cannot expose a live save row.

Initial planner bounds are:

- at most 1,024 purchased-piece rows;
- at most 64 unlocked-set rows;
- at most 256 transaction records;
- checked signed 64-bit snapshot/state revisions;
- checked arithmetic for every balance/progression candidate;
- exactly zero or one equipped set ID.

## 9. State validation and compatibility disposition

### 9.1 Status

```text
Valid
MigrationRequired
UnsupportedReadOnly
Unavailable
Malformed
CommitUncertain
```

Only `Valid` may produce a mutation plan. `MigrationRequired` preserves all
evidence but produces no purchase, unlock, or equip plan. `CommitUncertain`
requires authoritative reconciliation before any new operation.

### 9.2 Malformed state

State is malformed when it contains:

- a null list or row in a claimed complete snapshot;
- a blank identifier;
- duplicate purchased, unlocked, operation, or event identity;
- negative level or experience;
- invalid/overflowing revision;
- an equipped set that is not unlocked;
- a known piece associated with the wrong set/catalog binding;
- a supported ledger record with contradictory operation/result fields;
- more rows than the contract bound;
- current supported state not backed by exactly one accepted ledger result
  after migration to the new contract.

Ordinary validation does not delete, merge, choose first/last, clamp, reorder,
or rewrite malformed state.

### 9.3 Unknown-future preservation

Stable unknown set/piece/ledger rows are copied byte/identity-exact into the
candidate representation when the adapter can preserve them safely. They are
excluded from current ownership count, completion, unlock, equip, price,
progression, and notification calculations.

Mutation rejects as `Unsupported` when unknown-future evidence:

- collides with the requested set/piece/operation/event;
- reserves the next revision or another required ledger identity;
- cannot be retained exactly;
- makes the supported state ambiguous.

### 9.4 Legacy entitlement matrix

The current flag, piece list, unlocked list, and equipped ID are historical
evidence. They are not independently authoritative.

| Historical state | Disposition |
| --- | --- |
| Flag false, incomplete known pieces, set locked, nothing equipped | Eligible for explicit supported migration if every other field is valid |
| Flag true without complete approved proof | `MigrationRequired`; never silently grant |
| Complete known pieces with flag false | `MigrationRequired`; never silently revoke or grant |
| Set unlocked without complete approved proof | `MigrationRequired` |
| Set equipped while locked | `Malformed` |
| Set equipped/unlocked with ambiguous completion evidence | `MigrationRequired` |
| Unknown future rows with otherwise coherent known state | Preserve and exclude; mutation only if no collision/ambiguity |
| Duplicate, blank, null, negative, or contradictory evidence | `Malformed` |

A later migration specification must define exact accepted historical vectors,
catalog/version binding, witness format, retry behavior, and rollback. The
pure planner does not perform migration.

## 10. Operation request contract

Common request fields:

```text
operation kind
profileId
actorId
operationId
eventId
correlationId
setId
pieceId when applicable
expected state revision
expected economy revision when applicable
expected catalog binding
observed authoritative time only if an approved rule requires time
optional prior verified receipt
```

The caller does not provide:

- price;
- currency amount;
- completion threshold;
- experience award;
- level result;
- entitlement truth;
- notification copy;
- candidate state;
- commit result.

Operation kinds are:

```text
PurchasePiece
UnlockSet
EquipSet
```

No catch-all mutation operation is allowed.

### 10.1 Authorization

An injected authority validates the exact actor/profile/operation tuple before
planning. Status is `Allowed`, `Denied`, or `Unavailable`. Denied and
unavailable results mutate nothing and reveal no mutable state.

### 10.2 Revision checks

The request must bind the current Warmaster state revision, catalog binding,
and—when purchasing—the exact economy wallet revision. A mismatch is stale,
not a best-effort re-plan. Revision increments are checked and overflow
returns a typed failure.

## 11. Purchase semantics

A purchase plan requires:

- a `Ready` exact catalog;
- `Valid` exact saved state;
- authorized actor/profile;
- known available piece and owning set;
- definition-owned approved price and currency binding;
- sufficient immutable wallet balance at the expected revision;
- no operation/event collision;
- checked candidate arithmetic;
- available ledger capacity.

The candidate contains exactly:

- one currency debit intent for the catalog price;
- one new purchased-piece identity;
- the exact definition-owned progression result, if an approved progression
  rule exists;
- any completion-derived entitlement transition explicitly approved by the
  catalog;
- one transaction record;
- at most one post-commit semantic notification intent.

The piece is not owned and credits are not spent merely because a plan exists.

If the piece is already owned:

- an exact operation/receipt replay returns `AlreadyCommitted` with existing
  evidence;
- a different operation requesting the same owned piece returns
  `AlreadyOwned` with zero plan/debit/progression/notification;
- reuse of an operation/event identity with different semantics returns
  `Conflict`.

Insufficient funds returns a typed result and performs no partial mutation.
No other wallet, resource, caller price, or legacy compatibility balance may
be used as fallback.

## 12. Unlock semantics

`UnlockSet` requires one exact catalog-defined entitlement route:

- complete ownership of the exact required member identities under the bound
  catalog; or
- a separately approved verified entitlement record defined by a future
  catalog schema.

Raw list count, the historical Boolean, level, experience, display name, and
caller assertion cannot unlock a set.

Already-unlocked exact semantics return `NoChange` or `AlreadyCommitted` as
appropriate. Unlocking a different or unknown set, an incomplete set, or an
ambiguous legacy state fails closed.

Automatic unlock from the final piece is allowed only when an approved
catalog rule explicitly selects it. Otherwise purchase and unlock remain
separate operations. The pure planner must test both fixture policies without
choosing a production policy.

## 13. Equip semantics

`EquipSet` requires:

- a known set in the exact catalog;
- a verified unlocked entitlement in valid current state;
- any approved catalog equip prerequisites;
- no stale revision or operation/event collision.

Equipping the current set is `NoChange`. Equipping an unknown, locked,
ambiguous, unavailable, or removed set rejects. The planner never grants
ownership or unlock authority as a side effect of equip.

Auto-equip on completion is prohibited unless a user-approved catalog rule
explicitly selects it. No production default may preserve the current
implicit auto-equip behavior by accident.

## 14. Pure application plan

Equivalent immutable plan fields:

```text
operation kind
request fingerprint
expected catalog binding
expected profile/state/economy revisions
candidate Warmaster snapshot and revision
optional exact economy debit intent
candidate ledger records
new transition record
optional typed completion-entitlement intent
optional post-commit notification correlation
candidate state hash
plan hash
```

Plan objects hold no `SaveGameData`, `WarmasterState`, wallet row,
`UnityEngine.Object`, mutable collection, callback, service, or filesystem
reference.

The pure planner may return a plan only. It cannot return a committed receipt,
publish success, or execute an economy mutation.

## 15. Result contract

Planner and later adapter statuses are explicit:

```text
Prepared
AlreadyOwned
AlreadyCommitted
NoChange
InvalidRequest
Unauthorized
Ineligible
InsufficientFunds
StaleState
StaleEconomy
StaleCatalog
UnknownDefinition
ApprovalMissing
MigrationRequired
Unsupported
Unavailable
Malformed
Conflict
Overflow
PersistenceFailed
PreviousPreserved
CommitUncertain
Committed
```

`Committed` is adapter-only and requires a verified receipt. A pure planner
cannot produce it. `PreviousPreserved` and `CommitUncertain` are not ordinary
rollback or success.

Diagnostics contain stable technical code, subject ID, and nonlocalized
developer message. Ordering is deterministic by code then ordinal subject ID.
Player-facing text is resolved later through #177.

## 16. Transaction ledger and replay

Each supported transaction record binds:

```text
operationId
eventId
correlationId
profileId
operation kind
request fingerprint
catalog binding
set/piece/currency identities
definition-owned debit and progression fingerprints
resulting Warmaster/economy revisions
resulting state hash
plan hash
commit generation fingerprint
post-commit notification correlation
```

Rules:

1. Operation and event IDs are unique within the retained bounded ledger.
2. Exact operation replay is classified before stale revision checks so a
   committed caller can recover the original outcome after later operations.
3. Same operation with changed payload is `Conflict`.
4. Same event with another operation is `Conflict`.
5. An unknown-future colliding record is `Unsupported`.
6. A ledger record never authorizes a value not bound by its verified catalog
   and candidate hashes.
7. Ledger compaction, archival, and retention beyond the initial 256-record
   bound require a separate durable policy; the planner fails closed at cap.

## 17. Persistence and commit verification

Later production application uses one profile-bound serialized candidate
boundary:

```text
reverify exact authorities and revisions
-> clone accepted profile generation
-> apply plan to clone
-> verify clone matches plan
-> persist candidate
-> reload authoritative result
-> verify profile, catalog, wallet, Warmaster state, ledger, and generation
-> mint immutable receipt
-> publish committed state
-> release post-commit intent once
```

No live object is mutated before the candidate is accepted. No nested service
saves are allowed. Purchase debit and Warmaster state commit together or
neither is published as committed.

### 17.1 Persistence outcome mapping

| Persistence evidence | Required result |
| --- | --- |
| Rejected before mutation | `PersistenceFailed`; prior published state unchanged |
| Exact verified rollback | `PersistenceFailed`; prior published state unchanged |
| Prior generation preserved but active result unresolved | `PreviousPreserved`; reload/recovery required |
| Exact candidate verified | `Committed`; mint receipt and publish |
| Any uncertain active-file result | `CommitUncertain`; freeze new operations and reconcile same operation |

The adapter must never restore old memory and invite a fresh purchase after
`CommitUncertain`. Reconciliation uses the same operation identity and exact
plan; it either recovers the committed receipt, proves rollback, or remains
frozen.

## 18. Verified receipt and post-commit intent

Receipt construction is internal to the trusted application adapter. Public
callers cannot mint or alter receipts.

The receipt binds:

```text
transaction record
verified profile generation fingerprint
verified catalog binding
verified Warmaster revision
verified economy revision when applicable
receipt hash
```

Only a valid verified receipt may release an event or notification. The
post-commit intent contains stable technical event/category/correlation data,
not player-facing prose. Its identity is deterministic from the committed
semantic operation and ledger record.

Exact replay returns the same intent identity but does not enqueue it again.
Delivery deduplication is owned by #177; the Warmaster adapter still records
whether the semantic intent was already released.

## 19. Failure and retry matrix

| Boundary | Failure | Required behavior |
| --- | --- | --- |
| Catalog load | Missing, malformed, partial, unsupported, unapproved | No snapshot and `Unavailable`/`ApprovalMissing`/`Malformed` |
| State load | Missing authority | `Unavailable`, zero plan |
| State validation | Legacy ambiguity | `MigrationRequired`, preserve evidence |
| State validation | Contradiction/duplicate/null/negative | `Malformed`, preserve evidence |
| Authorization | Denied/unavailable | `Unauthorized`/`Unavailable`, zero plan |
| Planning | Stale catalog/state/wallet | Typed stale result, zero plan |
| Planning | Arithmetic overflow | `Overflow`, zero plan |
| Planning | Existing owned piece | `AlreadyOwned`, zero debit/progression |
| Replay | Exact committed operation | `AlreadyCommitted`, return existing evidence |
| Replay | Same key, changed semantics | `Conflict`, zero mutation |
| Candidate apply | Any target mismatch | Abort before persist |
| Persist | Rejected/verified rollback | Prior state remains authoritative |
| Persist | Previous preserved | Recovery required; no success/event |
| Persist | Commit uncertain | Freeze and reconcile same operation |
| Verify | Candidate mismatch | `CommitUncertain` or corruption; no receipt |
| Post-commit subscriber | Throws/fails | Committed state remains valid; retry delivery by correlation |

## 20. Required pure-planner tests

### 20.1 Catalog and identity

- missing, malformed, unsupported, incomplete, and unapproved catalog;
- duplicate set/piece/member identities;
- wrong membership and missing references;
- caller-owned input mutation after snapshot construction;
- exact catalog revision/hash and stale drift;
- blank, case-changed, oversize, control-character, and unknown IDs;
- missing price, currency, completion, progression, unlock, or equip rule;
- catalog size at every exact bound and bound plus one.

### 20.2 State and migration

- initial valid state;
- null lists/rows, blank and duplicate IDs;
- negative level/experience and revision overflow;
- every flag/piece/unlock/equip disagreement in section 9.4;
- equipped-but-locked contradiction;
- supported state/ledger mismatch;
- unknown-future rows preserved and excluded;
- unknown-future identity/revision collision rejected;
- immutable deterministic snapshot ordering.

### 20.3 Purchase

- valid definition-owned debit and candidate state;
- caller cannot supply or override price;
- insufficient, malformed, unavailable, and stale wallet;
- already owned under a new operation;
- exact operation replay before and after later transactions;
- operation/event collision with changed payload;
- completion reached/not reached using injected fixture policy;
- fixture auto-unlock selected and not selected;
- progression arithmetic boundary and overflow;
- ledger capacity boundary;
- no plan exposes a committed receipt or notification success.

### 20.4 Unlock and equip

- complete/incomplete exact membership;
- historical flag alone cannot unlock;
- verified supported entitlement path;
- already unlocked and already equipped;
- locked, unknown, removed, stale, and ambiguous set;
- equip never grants ownership or unlock;
- fixture auto-equip selected and not selected without production default.

### 20.5 Persistence model and receipt verification

- failure before candidate application;
- failure after debit staging but before persistence;
- verified rollback;
- previous-preserved and commit-uncertain results;
- retry/reload after every boundary;
- exact verified candidate creates one receipt;
- mismatched state, wallet, ledger, catalog, or generation creates no receipt;
- tampered receipt rejected;
- duplicate notification correlation cannot be released twice;
- deterministic hashes for equivalent input and separation for ambiguous input.

## 21. First engineering slice

The first engineering PR after this specification is limited to new files:

```text
unity/Assets/AL/Scripts/Warmaster/Planning/*.cs
unity/Assets/AL/Tests/EditMode/Warmaster/Planning/*.cs
required Unity .meta companions
```

It must:

- have no `UnityEngine` dependency;
- use injected immutable fixture catalog, wallet, actor, and compatibility
  authorities;
- implement validation and pure purchase/unlock/equip planning;
- implement deterministic replay classification and adapter-verification seam;
- keep every production dependency unavailable by default;
- contain no production values copied from current service literals or closed
  PR #506;
- leave `SaveGameData.cs`, `LocalWarmasterService.cs`, `IWarmasterService.cs`,
  `LocalSaveGameService.cs`, `Bootloader.cs`, catalogs, scenes, UI, and service
  registrations unchanged.

The engineering PR may claim only pure planning. It may not claim production
authority, durable persistence, migration, gameplay availability, balance,
presentation, Player readiness, or user approval.

## 22. Later integration gates

Production migration remains blocked until all are accepted:

1. #183 publishes a complete approved Warmaster technical family and catalog
   provenance;
2. the user approves required prices, progression, completion, unlock, and
   auto-equip/auto-grant rules;
3. #137/#450 provides profile-bound candidate persistence, exact verification,
   ledger retention, migration witnesses, and uncertain-commit recovery;
4. #163 provides the exact no-save economy candidate operation under the same
   profile transaction;
5. supported legacy Warmaster vectors and migrations are separately specified
   and verified;
6. current public service/callers are migrated without a bypass;
7. #177 presentation consumes only verified post-commit intent;
8. focused/full Unity, PlayMode, Player, Android, fault, performance, memory,
   package-size, and representative-device evidence passes;
9. the user records balance, visual, integrated-playtest, and release approval.

Any edit to a designated shared file requires its exclusive soft lock and a
fresh open-PR overlap audit.

## 23. Acceptance checklist

- [x] Definition-owned price replaces caller-owned price in the target contract.
- [x] Catalog, state, economy, operation, and receipt identities are explicit.
- [x] `AlreadyOwned` and `AlreadyCommitted` are distinct.
- [x] Exact replay and collision semantics are deterministic.
- [x] Unknown-future data is preserved and excluded without silent repair.
- [x] Ambiguous historical flag/list/unlock/equip evidence is `MigrationRequired`.
- [x] `CommitUncertain` freezes and reconciles rather than pretending rollback.
- [x] Success and presentation require an exact verified receipt.
- [x] Bounds, canonical hashes, fault behavior, and hostile tests are explicit.
- [x] No concrete production price, threshold, experience, level, stat, visual,
  or auto-equip decision is made.
- [x] The first engineering slice is isolated from save, service, catalog,
  scene, UI, and production registration.
- [ ] Pure planner implementation and focused tests merge.
- [ ] Production technical authority and user balance decisions merge.
- [ ] Migration, durable application, service/caller integration, and
  post-commit presentation merge.
- [ ] Player/device evidence and user approval complete.

## 24. Completion rule

Issue #171 remains open until the pure planner, approved production authority,
supported migration, profile-bound durable application, service/caller
migration, post-commit presentation, required validation, and applicable user
approval are accepted on current `main`.
