# Save Profile Identity and Dynamic Write Authority

**Status date:** 2026-07-30

**Status:** binding coordination contract; runtime implementation is not included

**Reviewed baseline:** `main@4bd458086c63da42f2a76d8219a2ee18cb1a5b50`

**Primary Codex mode:** coordination/review

**Owning issue:** #137

**Dependent issues:** #134, #163, #165, #168, #176, #177, and #183

**User-owned decisions:** destructive both-invalid profile reset, irreversible profile replacement, integrated playtest, and release approval

This contract defines one privacy-safe persistent local profile identity and one
fail-closed source of current write authority. It closes the design gap between
the crash-safe file transaction already accepted under #137 and domain planners
that need to know which verified profile generation they are preparing to
change.

It does not add a save field, migrate a profile, enable offline progress, change
economy or progression values, wire a production service, or authorize repair
or reset. Historical ownership wording in older save documents is superseded by
`AGENTS.md` and `Ownership_Decision_Record.md`; their accepted technical safety
requirements remain binding.

## 1. Current-source finding

The reviewed baseline has a strong bounded file transaction, but it does not
yet expose a safe identity/authority contract:

- `SaveGameData` has format, schema, and initialization versions, but no stable
  player-profile identity.
- the strict semantic validator's recognized top-level field set also omits
  `ProfileId`. Adding a serialized field without the matching schema and
  validator migration would make otherwise current data preserved-unknown and
  read-only rather than safely writable;
- `LocalSaveGameService` owns a private `_profileWritable` Boolean. Its value
  can change after load, recovery-witness drift, failed cleanup, deletion, or
  `CommitUncertain`, but consumers cannot query that live state.
- `SaveLoadDisposition.IsWritable` describes the most recent load decision. It
  is historical evidence, not a lease and not current operation authority.
- the public `ISaveGameService` exposes a mutable `CurrentSave` reference and
  status strings, but no profile identity, verified-generation fingerprint,
  service epoch, or
  dynamic write-authority snapshot;
- the internal candidate store is a valuable clone/persist/verify prototype,
  but its result still exposes a mutable `SaveGameData` object and is not a
  public immutable authority receipt;
- production constructors for `LocalResourceService` and
  `LocalWarzoneCreditService` inject `() => true`, so their mutation guard is
  not derived from the save service's current verified state;
- `LocalResourceService` keys its in-memory production remainder only by the
  production source's `ProfileIdentity`. It cannot distinguish the same source
  profile applied to a different player profile;
- pure progression and boss-reward contracts already carry a `ProfileId`, but
  no current runtime source can prove that it is the selected persistent local
  profile;
- the current NVS-01 persistence path under #134 uses
  `ISaveGameCandidateStore`, but its plan/callback contract is not bound to a
  profile identity, generation fingerprint, service epoch, or serialized
  single-writer lease;
- the accepted notification session queue distinguishes enqueue from
  presentation, but durable success notifications still need the identity and
  commit authority of the transaction that produced them.

These gaps do not invalidate the accepted save-recovery work. They block safe
composition of that work with economy, production, progression, rewards,
relationships, and notification delivery.

## 2. Required identity separation

The persistent local profile identity is named `ProfileId`. It identifies one
logical local player profile across valid primary, backup, temp, previous, and
quarantine-backed recovery generations.

`ProfileId` is not any of the following:

- an account, login, email, platform, advertising, installation, device, or
  analytics identity;
- a realm ID, character/class/champion ID, save-file path, slot index, catalog
  ID, source packet ID, content version, operation ID, or correlation ID;
- a production, cost, duration, battle, reward, terrain, or presentation
  profile ID;
- a file hash, save revision, timestamp, generation fingerprint, or service
  epoch.

Changing realm, class, appearance, equipment, chapter, source catalog, or device
settings must not change `ProfileId`. Copying a valid generation during the
same logical profile transaction must preserve it. A confirmed full deletion
retires it for the active service session. No caller may intentionally reuse,
restore, or supply it to a later new-profile transaction.

### 2.1 Canonical format

The first runtime implementation must use one bounded canonical representation:

```text
alp_<32 lowercase hexadecimal characters>
```

The 32 hexadecimal characters encode 128 nonzero bits. Comparisons are ordinal
and case-sensitive. Uppercase, braces, hyphens inside the payload, whitespace,
all-zero payloads, non-ASCII text, alternate prefixes, and values of any other
length are invalid.

The prefix separates this identity from the many content `ProfileId` values
already present in the project. The value is opaque; code must not parse realm,
time, device, account, source, or gameplay meaning from it.

Production generation must use an injected implementation backed by a
platform-supported cryptographically strong random source. Tests must inject
deterministic 128-bit values. Time, `GetHashCode`, `System.Random`, device IDs,
paths, realm IDs, and player-entered data are forbidden inputs.

The creation transaction rejects equality with any identity still present in
recognized evidence or retained by the active service session, then retries
generation at most eight times. Exhaustion fails creation without publishing or
writing a candidate. Deterministic tests must force collision and exhaustion.

Full verified deletion intentionally leaves no persistent identity tombstone:
retaining one would contradict the deletion/privacy contract. Therefore the
post-restart guarantee is collision resistance from fresh 128-bit production
entropy, not mathematically provable global non-reuse. No product behavior may
claim a stronger guarantee.

## 3. Creation and retirement

One `ProfileId` is minted only while creating a first generation after two
stable, bounded inventories prove that every recognized profile artifact is
missing.

Recognized evidence includes primary, backup, temp, canonical and legacy
previous files, recovery markers, and retained quarantine/transaction evidence.
An inaccessible, oversize, changed-during-read, forward, degraded, malformed,
or both-invalid artifact is evidence; it is not "missing."

The required creation order is:

```text
inventory all recognized evidence
-> prove all missing
-> create the complete neutral/new-profile clone
-> mint one ProfileId
-> validate identity and every initialized domain
-> persist through the existing candidate transaction
-> reopen and verify installed generations
-> publish the profile and Writable authority
```

Rules:

1. A failed mint, validation, persistence, or verification publishes no writable
   profile and cannot expose the candidate identity as committed.
2. Realm selection or onboarding initialization may update the same verified
   new profile, but cannot mint another identity or replace an established
   profile.
3. The legacy `CreateNewSave` entry point must eventually become a guarded
   compatibility operation. It cannot overwrite unresolved evidence, regenerate
   `ProfileId`, or act as a hidden reset.
4. A confirmed complete deletion publishes `Deleted`, clears all in-memory
   authority and identity, and verifies that every profile artifact was removed.
5. A later all-missing creation requests fresh identity entropy, rejects any
   identity still known in the active session, and follows the bounded
   collision rule above.
6. Automatic replacement of both-invalid evidence remains prohibited until the
   user makes the irreversible-profile decision recorded by #137.

## 4. Existing-profile migration

Current schema-1 profiles have no `ProfileId`. Adding the field therefore
requires a separately reviewed schema migration under the exclusive
`SaveGameData.cs` soft lock.

The schema bump, serialized field, strict recognized-field rule, identity
validator, candidate classification, and migration tests are one atomic
implementation. They cannot land as independently writable intermediate
states. Version 1 without an identity becomes typed `MigrationRequired`;
absence, blank, or malformed identity in the post-migration current schema is
invalid and never neutral-normalized.

Only an explicitly supported, selected, writable legacy generation may migrate:

```text
read and classify every generation
-> select authority under the existing semantic policy
-> preserve the selected raw bytes
-> clone the selected profile
-> mint exactly one ProfileId
-> apply the ordered schema migration
-> validate every domain and the new identity
-> persist through the migration-aware transaction
-> verify the exact transitional migration ledger twice
-> publish the migrated clone and Writable authority
```

Migration rules:

- one transaction mints one identity; primary and backup are never migrated
  independently;
- the selected legacy authority, not timestamp order or first file found,
  determines the logical profile;
- post-migration current-schema generations must contain a valid `ProfileId`;
- all post-migration generations asserted as current profile authority must
  contain the same identity;
- conflicting nonblank identities are `RecoveryRequired`; do not choose first,
  newest, primary, or majority;
- an existing valid identity is never silently replaced;
- a missing identity in one generation and a valid identity in another is
  resolved only by an issue-approved, evidence-preserving migration rule;
- forward-schema, degraded, repairable-with-data-change, inaccessible,
  commit-uncertain, and both-invalid profiles are not rewritten to add an ID;
- rollback restores the exact prior bytes and prior authority. A partially
  installed identity never becomes writable authority.

### 4.1 Exact transitional ledger

The first successful schema-1 migration has one explicit transitional state:

```text
Primary  = validated post-migration current-schema bytes with ProfileId
Backup   = exact selected schema-1 predecessor bytes without ProfileId
Temp     = missing
Previous = missing
Marker   = one bounded migration witness
```

The witness binds a contract version, migration operation ID, selected legacy
source generation, exact predecessor SHA-256/byte count, new `ProfileId`, target
schema/init versions, and exact migrated-candidate SHA-256/byte count. It
contains no raw save data or path. Two complete bounded inventories must match
this state before publication.

The schema-1 backup is a proven rollback predecessor, not a co-current identity
candidate. It is excluded from identity-conflict selection only while the exact
witness, primary, and backup all match. A missing/malformed marker, hash/length
drift, extra temp/previous evidence, or different current primary is
`RecoveryRequired` and non-writable.

The next successful ordinary save after migration must verify the transitional
ledger at entry, then finish in the normal state:

```text
Primary  = new current-schema generation with the same ProfileId
Backup   = exact prior current-schema generation with the same ProfileId
Temp     = missing
Previous = missing
Marker   = missing after verified cleanup
```

Only that verified transition may intentionally consume the schema-1
predecessor. `DeleteSave()` includes the witness. Existing invalid/alternate
evidence remains in its already-approved bounded quarantine role and is not
silently reclassified or cleaned by this migration.

Migration status remains distinct from an ordinary load. A general current
ledger with two post-migration generations requires matching identities;
schema-1 evidence is exempt only in the exact witnessed transitional state
above.

## 5. Generation fingerprint and service epoch

`ProfileId` answers "which logical profile?" It does not answer "which verified
generation may this plan mutate?"

Every writable authority snapshot therefore includes a
`VerifiedGenerationFingerprint`. It is a deterministic bounded lowercase
64-hex SHA-256 value derived by the save service from the exact verified
canonical authority and its transaction role/state. Its framed input binds at
least:

- authority-contract version;
- `ProfileId`;
- save format/schema/initialization versions;
- exact serialized primary identity;
- required backup identity;
- temp/previous/recovery-witness disposition;
- the last accepted commit/recovery state.

Callers treat the fingerprint as opaque. It is not player-facing, is not
`ProfileId`, and is not an authorization secret. It changes when any framed
committed state changes and cannot derive from `LastSavedTimestamp` alone.

A deterministic fingerprint can remain identical after an exact reload. Every
writable snapshot therefore also includes an ephemeral `AuthorityEpoch`: a
bounded nonzero 128-bit value for the current service publication, canonically
exposed as 32 lowercase hexadecimal characters. It is never serialized into the
save and changes on service construction, load/reconciliation publication,
accepted commit publication, deletion, or authority revocation.

One process-local injected epoch allocator supplies all save-service instances.
The production representation combines one cryptographically generated 64-bit
process nonce with one checked, strictly increasing process-wide 64-bit
publication counter. Allocation is serialized and bounded to constant state;
counter exhaustion fails closed. The allocator rejects zero, noncanonical, or
non-increasing scripted candidates and retries at most eight times. Exhaustion
publishes `Unavailable` and no `Writable` snapshot.

This construction guarantees uniqueness among all issued epochs in one process,
including service replacement, without an unbounded issued-value set. Exact
reload bytes still receive a later counter. Across a process restart, stale
objects cannot survive; the new nonce supplies probabilistic separation rather
than a cross-process durable identity claim. Tests inject nonce/counter output
and force repeat, regression, zero, overflow, eight-attempt exhaustion, reload,
and service-replacement cases.

Every plan or mutation preparation that can reach persistence binds:

```text
(ProfileId, AuthorityEpoch, ExpectedGenerationFingerprint)
```

The save boundary rechecks all three against current exact authority before
candidate preparation and immediately before disk mutation. Mismatch is typed
stale/unavailable and non-mutating.

The durable operation record written inside the candidate may store the
**expected pre-commit** generation fingerprint as causal evidence. It must not
store the post-commit fingerprint inside bytes used to calculate that same
fingerprint. After durable verification, an immutable runtime receipt returns:

```text
ProfileId
ExpectedGenerationFingerprint
CommittedGenerationFingerprint
CommittedAuthorityEpoch
operation/result identity
```

`CommittedGenerationFingerprint` is calculated after persistence and is not
embedded recursively in that generation. Durable replay after restart relies
on the saved operation/result identity, payload fingerprint, expected causal
fingerprint, and freshly verified current authority—not a persisted service
epoch.

## 6. Dynamic write-authority contract

The first engineering phase must expose an immutable bounded snapshot through a
small interface equivalent to:

```csharp
public interface IProfileWriteAuthorityProvider
{
    ProfileWriteAuthoritySnapshot GetCurrentAuthority();
}
```

The exact type names may change during implementation, but the semantics may
not. The snapshot contains:

```text
contractVersion
status
profileId
authorityEpoch
verifiedGenerationFingerprint
saveSchemaVersion
profileInitializationVersion
hasSelectedSourceGeneration
selectedSourceGeneration
diagnosticCodes (bounded)
```

The provider must not expose raw save bytes, file paths, mutable
`SaveGameData`, or an unqualified Boolean.

### 6.1 Required status set

Use an append-only typed status equivalent to:

| Status | Meaning |
| --- | --- |
| `Writable` | A current supported profile and its exact canonical generation are verified for candidate preparation. |
| `MissingProfile` | No published profile identity exists; creation may be considered only through the all-missing transaction. |
| `MigrationRequired` | Coherent supported legacy evidence exists but does not yet have the required identity/schema migration. |
| `ForwardSchemaReadOnly` | Newer schema evidence is preserved and cannot be mutated by this runtime. |
| `DegradedReadOnly` | A coherent degraded profile may be inspected but not mutated. |
| `RecoveryRequired` | Conflicting, ambiguous, or unresolved generations require recovery. |
| `CommitUncertain` | A mutation may have reached disk but exact authority is not proven. |
| `Deleted` | A previously published profile was fully and verifiably deleted for this service session. |
| `Unavailable` | The provider, current state, identity, epoch, fingerprint, or required verification is missing, invalid, or threw. |

Only `Writable` permits mutation preparation. Every other value is fail-closed.
Unknown future enum values are also unavailable.

### 6.2 Snapshot field invariants

`contractVersion` is present and recognized for every snapshot. Other fields
obey:

| Status | `ProfileId` | Epoch / generation fingerprint | Versions | Selected source |
| --- | --- | --- | --- | --- |
| `Writable` | required and canonical | both required and canonical | exact current schema/init | required |
| `MissingProfile` | empty | empty | zero | absent |
| `MigrationRequired` | empty | empty | observed supported legacy values | required |
| `ForwardSchemaReadOnly` | empty | empty | safely parsed forward values | required |
| `DegradedReadOnly` | empty | empty | safely observed values | required |
| `RecoveryRequired` | empty | empty | zero or safely observed diagnostic values | absent because no authority is selected |
| `CommitUncertain` | empty | empty | zero or last safely observed diagnostic values | absent because no authority is proven |
| `Deleted` | empty | empty | zero | absent |
| `Unavailable` | empty | empty | zero | absent |

Forward/degraded raw evidence may contain an identity unknown to the current
validator. It remains preserved internally and is not exposed as a current
`ProfileId`. Non-writable snapshots cannot leak a prior epoch/fingerprint for
accidental reuse.

`diagnosticCodes` contains at most 16 unique ordinal-sorted ASCII codes, each
1–96 characters and restricted to `A-Z`, `0-9`, `_`, `.`, and `-`. Invalid,
duplicate, or over-limit provider data makes the whole snapshot `Unavailable`;
it is not truncated into a misleading partial authority. Every non-writable
status has at least one stable reason code, including missing and deleted.

### 6.3 State transitions

- supported exact load/recovery may publish `Writable` only after the selected
  identity and canonical generation are verified;
- an accepted commit publishes a new immutable `Writable` snapshot with the
  same `ProfileId`, a new epoch, and the verified committed fingerprint;
- a verified exact duplicate may retain the generation fingerprint but still
  uses the current service epoch;
- known verified rollback may restore the exact pre-operation snapshot;
- incomplete cleanup, witness drift, identity conflict, or uncertain mutation
  cannot restore `Writable`;
- `CommitUncertain` remains frozen until a fresh bounded reload/reconciliation
  proves one exact authority;
- load start, delete start, and migration start revoke the prior snapshot before
  performing I/O;
- successful complete deletion publishes `Deleted`; failed deletion publishes
  `RecoveryRequired` or `Unavailable`, never `Deleted`;
- a snapshot from before service replacement, reload, deletion, or commit is
  stale even when a caller still holds the object.

The provider may retain the latest immutable verification result to avoid
rehashing on every UI read. Consumers may not cache only `Writable`, and the
save boundary still performs the required immediate exact rechecks.

### 6.4 Provider failure

Missing providers, thrown exceptions, null snapshots, invalid versions,
malformed IDs/epochs/fingerprints, and inconsistent fields map to typed
unavailable behavior.
They never fall back to `true`.

Compatibility constructors may remain temporarily for source compatibility,
but production construction must obtain the real provider from the save service
or fail closed. Tests may inject explicit fake snapshots.

Before the identity migration, coherent version-1 profiles report
`MigrationRequired`; the provider phase must not temporarily reinterpret them as
writable merely to preserve existing constructor behavior.

## 7. Serialized mutation boundary

The save authority owns one process-local, non-reentrant mutation gate. It
serializes `Save`, candidate commit, migration, recovery, load, create, and
delete across:

```text
acquire gate without waiting indefinitely
-> validate expected ProfileId/AuthorityEpoch/generation fingerprint
-> verify the current canonical ledger
-> reserve one bounded post-commit publication slot
-> reserve PublicationSequence and one fresh non-reusable AuthorityEpoch
-> clone the published save
-> invoke one bounded pure preparation callback
-> validate the complete candidate
-> recheck exact disk authority immediately before mutation
-> persist and reopen/verify
-> calculate the committed fingerprint
-> atomically publish save + fingerprint + reserved epoch and enqueue receipt
-> release gate in finally
-> request ordered receipt/event drain outside the gate
```

If the gate is held, a concurrent or reentrant attempt returns typed `Busy` or
`Unavailable` without invoking its preparation callback, mutating memory/disk,
or blocking the main thread. Callbacks perform no service lookup, nested save,
file/network I/O, event publication, or unbounded work. A callback exception
rejects its candidate and releases the gate.

No two callers may pass the same expected authority and write concurrently.
External disk drift during a lease still fails the immediate exact recheck.
`CommitUncertain` freezes the gate's writable authority before it is released.

The authority owns a bounded 64-receipt FIFO and one non-reentrant dispatcher.
A slot, checked monotonically increasing in-process `PublicationSequence`, and
fresh epoch are reserved before callback or disk mutation. Lack of capacity,
sequence exhaustion, or epoch-allocation exhaustion returns typed
`PublicationBackpressure`/`Unavailable` with zero callback, disk change, or
receipt. Failed preparation releases the queue slot; its sequence/epoch remain
consumed gaps and are never reused.

After successful durable verification and committed-fingerprint calculation,
one no-fail critical-section update publishes the save, fingerprint, reserved
epoch, and immutable success receipt together. No observer can see only half
of that state. A post-I/O failure instead atomically publishes its fail-closed
authority status and terminal failure receipt without presenting the reserved
epoch as committed. Every terminal result that follows disk mutation enqueues
its immutable receipt while the mutation gate is still held.

After release, any caller may request a drain, but one dispatcher consumes FIFO
order only. Commit B can enqueue after commit A yet cannot publish before A,
even if B's caller reaches the dispatcher first. Subscriber exceptions are
isolated, recorded with bounded diagnostics, and do not reorder or replay the
receipt. No subscriber runs under the mutation gate. Service replacement cannot
activate a new authority until the prior gate is retired and its queued
receipts are drained or explicitly reconciled.

This is a bounded in-process ordering mechanism, not a durable outbox. A process
crash after commit but before presentation is recovered from the durable domain
ledger by that domain's later #137/#177 integration; this session queue alone
does not claim delivery.

Deterministic tests run two same-snapshot callers with controlled overlap, a
reentrant callback, callback exception, every persistence fault window, load or
delete racing a commit, retry after release, publication capacity/sequence
exhaustion, forced epoch exhaustion during reservation, subscriber failure, and
an A-release/B-commit overlap where B calls the dispatcher first. The epoch
exhaustion fixture proves callback, disk, and receipt counts remain zero. The
matrix proves commit-order publication, no lost update, no deadlock, no stale
event, and gate/slot release on every path.

## 8. Domain-consumer rules

Before a post-migration profile can publish consumer-visible `Writable`, the
implementation inventories every production read/mutation of `CurrentSave`,
every `Save()` call, and every `ISaveGameCandidateStore` caller on then-current
`main`. Each live mutator must either use the serialized identity-bound
candidate transaction or return typed unavailable. Known current paths include
resource, Warzone Credit, and NVS-01 mutation; the inventory, not this list, is
authoritative.

There may be no merged window in which the new schema publishes `Writable`
while a direct or tokenless current mutator remains enabled. Rollout commits may
add dormant contracts, adapters, and tests independently, but activation is one
atomic cutover train.

### 8.1 Economy and Warzone Credits (#163)

- replace the unconditional production `() => true` gate with the dynamic
  provider;
- reads may return typed read-only results where safe;
- additions, spends, production ticks, and saves require a valid current
  `Writable` snapshot;
- a mutation cannot be applied to the published in-memory save and then discover
  at `Save()` that authority was unavailable;
- direct legacy wrappers remain compatibility-only until they route through one
  candidate transaction;
- provider loss, epoch drift, or generation-fingerprint drift returns typed
  profile-not-writable/stale state and emits no normal balance event.

Production remainder state is keyed exactly by:

```text
(ProfileId, productionProfileId, sourceRevision)
```

Changing any member, losing authority, reloading, deleting, or replacing the
save service clears the old remainder. A source `productionProfileId` is not a
player `ProfileId`.

### 8.2 Progression (#165)

Research/training requests, plans, active orders, completion receipts, and replay
checks bind the persistent `ProfileId`, authority epoch, and expected generation
fingerprint in addition to their existing content/profile identities. A correct
pure plan against a stale save generation is noncommittable. No
definition/profile identity can stand in for the local profile identity.

### 8.3 Boss rewards (#168)

Reward computation may remain pure, but application context and durable ledger
records bind the exact persistent profile identity and expected pre-commit
generation fingerprint; the runtime application context also binds the
ephemeral authority epoch. Success cannot enqueue before the save transaction
verifies and publishes the committed generation. Preview, stale, cross-profile,
commit-uncertain, and previous-preserved results never grant or announce
rewards.

### 8.4 Relationships (#176)

Relationship mutation uses the same authority snapshot and candidate boundary.
A valid relationship record does not imply that the containing profile is
writable. Missing or degraded providers fail without affinity/faction/persona
events.

### 8.5 Notifications (#177)

The session queue can report unavailable/failure outcomes without persistence.
A success notification for a durable mutation requires the committed receipt's
`ProfileId` and generation identity. Enqueue accepted, presented, acknowledged,
and durably committed remain separate states. Player-facing content never
includes `ProfileId`, the authority epoch, generation fingerprints, raw hashes,
or file paths.

### 8.6 NVS-01 persistence (#134)

`Nvs01SaveGameMutationCommitter` already uses the internal candidate store, but
its request/plan is tokenless on the reviewed baseline. Before any
post-migration profile can publish `Writable`, NVS plans and exact-replay checks
must bind the same profile, service epoch, and expected generation fingerprint.
Its candidate callback cannot change `ProfileId`. Existing NVS operation and
payload fingerprints remain domain identities; they do not replace save
authority.

### 8.7 Game-data authority (#183)

Catalog/source identities and revisions remain independent inputs. They are
bound alongside, never substituted for, the local identity. Offline production
and progression remain blocked until #183 supplies accepted technical source
snapshots for every affected domain.

## 9. Mutation and publication sequence

Every durable domain integration follows:

```text
read immutable authority snapshot
-> require Writable and validate ProfileId/AuthorityEpoch/expected fingerprint
-> build a pure plan against cloned domain snapshots
-> enter the serialized save candidate boundary
-> recheck the exact expected authority triple
-> clone current verified save
-> apply and validate the plan once
-> persist through the existing file transaction
-> reopen and verify canonical authority
-> publish the committed save, committed fingerprint, and fresh epoch
-> publish one typed domain receipt/event
-> enqueue any success presentation
```

No consumer may:

- mutate `CurrentSave` before acquiring and rechecking authority;
- infer authority from non-null `CurrentSave`, `HasSave()`, realm selection,
  last load status, last save status, or a previous Boolean;
- perform an independent nested save inside a larger result transaction;
- accept a plan for one profile against another profile or generation;
- let a candidate callback add, remove, normalize, or replace `ProfileId`;
- expose the candidate store's mutable `PublishedSave` as an authority receipt;
- publish success after `PreviousPreserved`, `CommitUncertain`, rejection, or
  unverifiable duplicate;
- intentionally restore, copy, or caller-supply a known retired identity after
  deletion or reset.

## 10. Recovery, reload, and conflict matrix

At minimum, implementation tests must prove:

| Condition | Required authority |
| --- | --- |
| all evidence missing before creation | `MissingProfile`; no ID until committed creation |
| valid current profile, exact canonical ledger | `Writable` |
| valid legacy profile without ID | `MigrationRequired` |
| forward schema | `ForwardSchemaReadOnly` |
| coherent degraded profile | `DegradedReadOnly` |
| conflicting IDs across relevant generations | `RecoveryRequired` |
| inaccessible or changed-during-read authority evidence | `Unavailable` or `RecoveryRequired` |
| failed mutation with exact verified rollback | original `Writable` snapshot restored |
| cleanup incomplete or disk outcome ambiguous | `CommitUncertain` |
| witness drift before commit | non-writable; every generation preserved |
| exact committed duplicate | same profile, verified current authority, no duplicate effect |
| stale epoch or fingerprint after another commit/reload | rejected/non-mutating |
| epoch allocator repeats/regresses/exhausts | bounded retry then `Unavailable`; no `Writable` |
| publication queue/sequence unavailable before mutation | typed backpressure; no callback or disk change |
| commit B finishes before A's drain request | A receipt/event still publishes before B |
| full verified deletion | `Deleted`; in-session identity retired and persistent identity evidence removed |
| partial deletion | non-writable recovery state; identity/evidence not forgotten |
| provider missing/throws/returns malformed snapshot | typed unavailable |

Tests also cover two profiles using the same content/production source, the same
profile using a changed source revision, service replacement, reload, delete,
rollback, case/length/character validation, deterministic injected identity
generation, collision handling, schema-1-to-current fault windows, strict
recognized-field handling, and malformed or missing current-schema identity.

## 11. Privacy, performance, and device constraints

- `ProfileId`, authority epochs, and generation fingerprints remain local
  technical data; they are not displayed, used for advertising, or logged in
  full in player-facing output.
- deletion must remove identity-bearing primary, backup, temp, previous,
  marker, and quarantine evidence under the accepted #137 policy;
- diagnostics use bounded codes rather than raw paths or serialized content;
- authority snapshots and diagnostic lists are immutable and bounded;
- identity/fingerprint hashing and epoch rotation occur at
  load/save/recovery boundaries, not per frame;
- no polling loop, background network request, new package, asset, or content
  payload is authorized;
- the provider should add only small managed code/data. Exact Player,
  IL2CPP/linker, installed-size, allocation, latency, and physical-device impact
  must be measured by the engineering PRs that implement it.

## 12. Ordered implementation

1. **This coordination PR:** one document, no runtime or shared-file change.
2. **Dormant authority foundation:** without publishing production
   `Writable`, add the immutable snapshot types, fingerprint/epoch framing,
   serialized non-reentrant candidate boundary, immutable commit receipt,
   provider failure handling, and focused concurrency/fault tests. Version-1
   profiles remain `MigrationRequired`; schemas and shared files do not change.
3. **Atomic current-mutator cutover:** under the `SaveGameData.cs` lock, one
   integration train must:
   - inventory every then-current `CurrentSave`, `Save()`, and candidate-store
     mutator;
   - convert resource and Warzone Credit changes to the serialized candidate
     transaction or explicitly disable them;
   - bind the current #134 NVS committer to profile/epoch/fingerprint;
   - add the ProfileId field, schema, strict validator, witnessed migration,
     collision generator, conflict recovery, and deletion behavior;
   - activate the production provider only after those consumers pass.

   Stacked preparation PRs may remain unmerged behind the dormant boundary, but
   no intermediate `main` commit may publish a writable post-migration profile
   to a direct or tokenless mutator.
4. **Later domain transactions:** after accepted #183 source and the owning
   #163/#165/#168/#176 contracts, bind newly activated plans to the same
   identity/epoch/fingerprint and use clone -> persist -> verify -> publish.
5. **Offline progress:** only after production, progression, and catalog
   snapshots are accepted. Prove repeated/failed load cannot duplicate value or
   timer completion.
6. **Durable notification/history:** reuse the same identity and authority after
   #137 recovery semantics; do not create a parallel outbox identity.
7. **Player/device acceptance:** complete Player lifecycle, supported-device,
   low-storage, timing/memory, privacy/deletion, and integrated playtest evidence.

No later phase may weaken the existing semantic candidate ranking, exact
recovery evidence, forward-schema preservation, or commit-uncertain freeze.
If an implementation chooses to edit `Bootloader.cs` rather than deriving the
provider from the injected save service, it must separately acquire that
file's exclusive soft lock.

## 13. Acceptance for this contract

- [x] Persistent profile identity is separated from account, device, realm,
  content, source, and operation identities.
- [x] Creation, migration, conflict, retirement, and deletion rules are explicit.
- [x] Current-generation authority is distinct from persistent identity.
- [x] All non-writable and provider-failure states fail closed.
- [x] Mutation plans bind exact profile, service epoch, and expected generation
  fingerprint without a circular post-commit hash.
- [x] One serialized non-reentrant boundary prevents same-authority lost updates.
- [x] Epoch allocation is process-unique, bounded, collision-tested, and
  fail-closed.
- [x] Bounded post-commit dispatch preserves commit order outside the mutation
  gate.
- [x] Per-status snapshot field and diagnostic invariants are explicit.
- [x] The witnessed legacy-backup transition and its intentional cleanup are
  explicit.
- [x] Activation cannot expose `Writable` before all current mutators are safe.
- [x] Full deletion and probabilistic post-restart collision resistance are
  distinguished honestly.
- [x] Economy remainder identity binds profile, production profile, and source
  revision.
- [x] Existing recovery, rollback, and commit-uncertain rules remain binding.
- [x] Cross-issue consumer order is explicit.
- [x] Performance, privacy, and unperformed device/build measurements are
  declared.
- [ ] Runtime provider implementation and focused tests merge.
- [ ] Serialized candidate/concurrency foundation and immutable receipts merge.
- [ ] Profile field/schema migration, strict validator, witnessed legacy
  transition, conflict recovery, and current-mutator cutover merge under the
  lock.
- [ ] Domain consumers use the candidate transaction and exact authority.
- [ ] Offline progress, durable history, lifecycle/device proof, and user gates
  complete.

Issue #137 remains open after this contract. This document authorizes no
destructive reset and records no user approval.
