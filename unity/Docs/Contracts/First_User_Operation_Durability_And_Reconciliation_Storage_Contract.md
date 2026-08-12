# First-User Operation Durability and Reconciliation Storage Contract

Status: planning contract for A1 review
Version: `first-user-durability-storage/1`
Source baseline: `main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0`
Primary delivery mode: Codex coordination/review
Routing: Refs #467; related non-closing boundaries #137 and #450

## 1. Decision and approval boundary

This document specifies a provider-neutral physical persistence and crash-consistency boundary beneath an already validated first-user semantic request. The validated semantic core and its fixed 32-byte `SemanticRequestFingerprint` are opaque inputs. This document does not add, name, derive, count, default, or validate unresolved product-domain fields.

A1 retains the `Q/W` ingress and primary-barrier protocol:

- `Q` is a principal-scoped primary write fence and application watermark.
- `W` is keyed durable evidence that a validated operation was registered, bound, terminalized, or explicitly settled without a binding.
- `NOT_FOUND_AFTER_BARRIER` requires a matching `W=SETTLED_NO_BIND` record plus fenced primary evidence. An empty primary read is never sufficient.

This is a planning decision only. It does not select or approve a database, cloud, KMS, IdP, cryptographic algorithm/profile, retention duration, deletion law, regional residency policy, provider, server runtime, deployment, migration, public error mapping, or production operation.

No backend, network service, database schema, Unity runtime, Android code, save file, local `ProfileId`, shared-lock file, or user data is changed by this contract.

## 2. Required properties

An implementation conforming to this contract must provide all of the following:

1. Strictly serializable admission and terminalization, or a demonstrably equivalent linearizable-primary transaction model.
2. One immutable operation binding per authenticated tenant/principal scope and protected operation commitment.
3. One immutable semantic fingerprint per binding.
4. Exactly one base terminal: `COMMITTED` xor `TERMINAL_REJECTED`.
5. Atomic success across authoritative domain effects, ledger, receipt/proof, audit, outbox, binding locator, ingress evidence, and primary watermark.
6. Durable rejection only for stable post-admission decisions proven under the primary lock.
7. Exact terminal replay without re-executing business effects.
8. Monotonic compensation and suppression overlays that never rewrite the base terminal.
9. Suppression precedence over compensation during reconciliation.
10. Contiguous ledger revisions, ordinals, hash links, and deterministic batch membership.
11. One stable logical outbox identity with at-least-once physical delivery tolerance.
12. Principal- and tenant-scoped lookup, indexes, locks, errors, and logs.
13. Fail-closed key rotation, proof verification, corruption handling, restore, and retention boundaries.
14. Zero persistence and zero operation lookup for transport, parser, schema, static-semantic, or fingerprint-construction failures.

## 3. Threat and failure model

The contract assumes arbitrary process termination, connection loss, transaction-result uncertainty, worker duplication, broker acknowledgement loss, primary failover, stale replicas, clock skew, key rotation, malformed or missing stored artifacts, transaction-consistent backup restore, and retry storms.

The contract does not assume exactly-once network delivery, trustworthy client timestamps, trustworthy client identity fields, reliable wall clocks for ordering, or a durable client response. Client-provided tenant, principal, server record IDs, authoritative revisions, and proof metadata are never authority.

The authenticated server context supplies tenant and principal scope. The raw operation ID is accepted transiently only after strict header preflight. It is never persisted, echoed in a response body, placed in a URL, copied to an index, or written to diagnostics or logs.

## 4. Physical notation and bounds

The storage shapes below are SQL-like and vendor-neutral. An implementation may use equivalent native types only when every bound, uniqueness rule, transition, and atomicity assertion remains executable.

| Logical type | Required bound |
| --- | --- |
| `RecordId` | exactly 16 opaque bytes; server generated; never reused |
| `TenantScopeRef` | exactly 32 protected bytes from authenticated context |
| `PrincipalScopeRef` | exactly 32 protected bytes from authenticated context |
| commitment, fingerprint, digest | exactly 32 bytes |
| key/contract/schema version | integer `1..65535` |
| safe sequence/revision/epoch | integer `0..9007199254740991` |
| batch ordinal | integer `0..4095` and less than batch event count |
| server time | UTC microseconds `0..9007199254740991`; never ordering authority alone |
| discriminator/schema token | ASCII `^[A-Z][A-Z0-9_]{0,63}$` |
| receipt response bytes | `1..1048576` protected bytes |
| terminal response bytes | `1..65536` protected bytes |
| ledger private payload | `0..65536` protected bytes |
| audit safe metadata | `0..16384` canonical bytes |
| proof bytes | `1..4096` bytes |

Closed discriminator domains are stored as checked numeric values or an equally closed native domain. Open strings are not permitted for state or kind columns.

`Q.writeSequence`, ledger batch IDs, and record IDs are application-owned logical values. A database transaction ID, log sequence number, provider cursor, replica position, or worker lease ID must never become a public operation, receipt, ledger, or event identity.

## 5. Storage ownership and record matrix

### 5.1 `PrincipalFence` (`Q`)

Purpose: serialize all first-user operations for one authenticated principal and expose an application watermark for primary reconciliation.

Required shape:

```text
PRIMARY KEY (tenantScopeRef, principalScopeRef)
tenantScopeRef            TenantScopeRef       immutable
principalScopeRef         PrincipalScopeRef    immutable
ingressSequence           SafeSequence         monotonic
writeSequence             SafeSequence         monotonic
leaderEpoch               SafeSequence         monotonic
rowRevision               SafeSequence         monotonic CAS
```

Only checked compare-and-swap updates are allowed. Every state-changing transaction increments `writeSequence` exactly once and stamps all records created by that transaction with the resulting value. `ingressSequence` increments only when a new `W` is registered.

### 5.2 `OperationIngressEvidence` (`W`)

Purpose: durable keyed evidence for admission and the only basis for a strict not-found-after-barrier disposition.

```text
PRIMARY KEY (
  tenantScopeRef,
  principalScopeRef,
  commitmentKeyVersion,
  operationIdCommitment
)
UNIQUE (tenantScopeRef, principalScopeRef, evidenceId)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  evidenceId,
  semanticRequestFingerprint
)

evidenceId                 RecordId
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
commitmentKeyVersion       UInt16
operationIdCommitment      Commitment32
semanticRequestFingerprint Fingerprint32
state                      REGISTERED | BOUND | TERMINAL |
                           SETTLED_NO_BIND | RETENTION_BOUNDARY
ingressSequence            SafeSequence
attemptFence               SafeSequence
lastWriteSequence          SafeSequence
settledWriteSequence       SafeSequence?
bindingId                  RecordId?
settleReason               closed discriminator?
settlementDigest           Digest32?
createdAtServer            SafeTimestamp
```

Immutable columns: identity/scope, key version, commitment, fingerprint, ingress sequence, and creation time.

Allowed transitions:

```text
ABSENT -> REGISTERED
REGISTERED -> BOUND
BOUND -> TERMINAL
REGISTERED -> SETTLED_NO_BIND
TERMINAL -> RETENTION_BOUNDARY             policy-gated
SETTLED_NO_BIND -> RETENTION_BOUNDARY      policy-gated
```

No transition leaves `TERMINAL`, `SETTLED_NO_BIND`, or `RETENTION_BOUNDARY` except the two explicit policy-gated boundary transitions above. `BOUND` and `TERMINAL` require `bindingId`; `SETTLED_NO_BIND` forbids it and requires a settlement sequence, reason, digest, and fence that disables every earlier worker. `settlementDigest` is absent before settlement and immutable afterward.

### 5.3 `OnboardingOperationBinding` (`B`)

Purpose: immutable identity/fingerprint binding and base terminal locator.

```text
PRIMARY KEY (bindingId)
UNIQUE (tenantScopeRef, principalScopeRef, evidenceId)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  bindingId,
  semanticRequestFingerprint
)
FOREIGN KEY (
  tenantScopeRef,
  principalScopeRef,
  evidenceId,
  semanticRequestFingerprint
) REFERENCES W (
  tenantScopeRef,
  principalScopeRef,
  evidenceId,
  semanticRequestFingerprint
)

bindingId                   RecordId
evidenceId                  RecordId
tenantScopeRef              TenantScopeRef
principalScopeRef           PrincipalScopeRef
semanticRequestFingerprint  Fingerprint32
commitContractVersion       UInt16
admissionDigest             Digest32
state                       IN_PROGRESS | COMMITTED | TERMINAL_REJECTED
attemptFence                SafeSequence
admittedWriteSequence       SafeSequence
admittedAtServer            SafeTimestamp
terminalWriteSequence       SafeSequence?
terminalAtServer            SafeTimestamp?
receiptId                   RecordId?
receiptResponseDigest       Digest32?
receiptCoreDigest           Digest32?
receiptProofDigest          Digest32?
ledgerBatchId               RecordId?
ledgerBatchDigest           Digest32?
authoritativeRevision       SafeSequence?
terminalDecisionId          RecordId?
terminalDecisionDigest      Digest32?
```

Allowed transitions:

```text
IN_PROGRESS -> COMMITTED
IN_PROGRESS -> TERMINAL_REJECTED
```

Commit-time assertions:

- `IN_PROGRESS`: every terminal locator column is null.
- `COMMITTED`: receipt, proof, ledger batch, digest, sequence, time, and authoritative revision are complete; all rejection columns are null.
- `TERMINAL_REJECTED`: terminal decision, digest, sequence, and time are complete; all committed columns are null.
- `COMMITTED` and `TERMINAL_REJECTED` are immutable.
- Composite foreign keys bind every locator to the exact stored digest; a partial locator is invalid.

Every nullable locator tuple uses `MATCH FULL` semantics: either every member is absent or every member is present. The following scope-prefixed candidate keys and deferred composite foreign keys, or an exactly equivalent commit-time constraint, are mandatory:

- committed `B -> R` binds scope, binding, receipt ID, response digest, core digest, ledger-batch ID, and ledger-batch digest;
- committed `B -> P` binds the same receipt identity plus proof digest;
- committed `B -> LB` binds scope, binding, batch ID, and batch digest;
- rejected `B -> T` binds scope, binding, fingerprint, terminal-decision ID, and decision digest;
- `P -> R` and `R -> LB` bind the same scoped IDs and digests;
- `R`, `P`, `T`, and `LB` each bind back to the same scoped `B` candidate key.

The executable candidate keys are the field tuples printed above and in the fixture catalog. The foreign-key source and target field names are explicit even where the physical column names differ (`B.receiptResponseDigest -> R.responseBodyDigest`, `B.receiptProofDigest -> P.proofDigest`, and each `ledgerBatchDigest -> LB.batchDigest`). A conforming schema cannot replace these tuples with an unscoped ID-only key.

All circular terminal relationships are inserted in one atomic resource and checked at transaction commit. Application-only post-write validation is nonconforming. No unscoped locator probe, uniqueness test, or constraint error may become a cross-tenant or cross-principal existence oracle.

### 5.4 `CommittedReceiptBody` (`R`)

Purpose: immutable exact successful response.

```text
PRIMARY KEY (receiptId)
UNIQUE (tenantScopeRef, principalScopeRef, bindingId)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  bindingId,
  receiptId,
  responseBodyDigest,
  receiptCoreDigest,
  ledgerBatchId,
  ledgerBatchDigest
)

receiptId                   RecordId
bindingId                   RecordId
tenantScopeRef              TenantScopeRef
principalScopeRef           PrincipalScopeRef
semanticRequestFingerprint  Fingerprint32
commitContractVersion       UInt16
storedHttpStatus            checked integer
exactResponseBytes          protected bounded bytes
responseBodyDigest          Digest32
receiptCoreDigest           Digest32
authoritativeRevision       SafeSequence
ledgerBatchId               RecordId
ledgerBatchDigest           Digest32
createdWriteSequence        SafeSequence
createdAtServer             SafeTimestamp
```

The exact stored status/body is returned on original success and every exact replay. Replay annotations, current policy, current time, or worker identity must not alter these bytes.

### 5.5 `ReceiptProof` (`P`)

Purpose: immutable integrity/proof material pinned to the exact receipt and historical verification profile.

```text
PRIMARY KEY (tenantScopeRef, principalScopeRef, receiptId)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  bindingId,
  receiptId,
  responseBodyDigest,
  receiptCoreDigest,
  ledgerBatchId,
  ledgerBatchDigest,
  proofDigest
)

receiptId                   RecordId
bindingId                   RecordId
tenantScopeRef              TenantScopeRef
principalScopeRef           PrincipalScopeRef
responseBodyDigest          Digest32
receiptCoreDigest           Digest32
ledgerBatchId               RecordId
ledgerBatchDigest           Digest32
proofProfileVersion         UInt16
proofKeyVersion             UInt16
proofBytes                  bounded bytes
proofDigest                 Digest32
createdAtServer             SafeTimestamp
```

Historical rows are never silently re-signed or rewritten during key rotation.

An available trusted verifier returning a bad proof is `PROOF_MISMATCH`. An unavailable or retired historical verifier is `VERIFICATION_UNAVAILABLE`/`UNKNOWN`; it is not a mismatch. A known-compromised verification key is `SECURITY_INTEGRITY_FAULT` and quarantines the result. All three dispositions are zero-mutation and never re-sign historical bytes.

### 5.6 `TerminalDecision` (`T`)

Purpose: immutable exact durable rejection.

```text
PRIMARY KEY (terminalDecisionId)
UNIQUE (tenantScopeRef, principalScopeRef, bindingId)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  bindingId,
  terminalDecisionId,
  semanticRequestFingerprint,
  terminalDecisionDigest
)

terminalDecisionId          RecordId
bindingId                   RecordId
tenantScopeRef              TenantScopeRef
principalScopeRef           PrincipalScopeRef
semanticRequestFingerprint  Fingerprint32
decisionSchemaVersion       UInt16
decisionSchemaDigest        Digest32
decisionCode                closed discriminator
storedHttpStatus            checked integer
exactResponseBytes          protected bounded bytes
terminalDecisionDigest      Digest32
decidingAuthoritativeRevision SafeSequence
createdWriteSequence        SafeSequence
createdAtServer             SafeTimestamp
```

Only stable decisions established after binding under the primary transaction may be stored. Timeouts, signer failure, connection loss, unavailable dependencies, serialization aborts, or unknown transaction results are never converted to a terminal rejection.

### 5.7 Ledger head, batch, and event (`H`, `LB`, `LE`)

`LedgerStreamHead`:

```text
PRIMARY KEY (tenantScopeRef, principalScopeRef, streamKind, streamId)
tenantScopeRef            TenantScopeRef
principalScopeRef         PrincipalScopeRef
nextRevision               SafeSequence
headEventDigest            Digest32?
lastWriteSequence          SafeSequence
rowRevision                SafeSequence
```

`LedgerBatch`:

```text
PRIMARY KEY (ledgerBatchId)
UNIQUE (tenantScopeRef, principalScopeRef, bindingId, batchKind, batchRevision)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  bindingId,
  ledgerBatchId,
  batchDigest
)

ledgerBatchId              RecordId
bindingId                  RecordId
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
batchKind                  FIRST_USER_COMMIT | COMPENSATION | SUPPRESSION
batchRevision              SafeSequence
eventCount                 integer 1..64
firstOrdinal               constant 0
lastOrdinal                eventCount - 1
eventSchemaVersion         UInt16
eventSchemaDigest          Digest32
batchDigest                Digest32
committedWriteSequence     SafeSequence
committedAtServer          SafeTimestamp
```

`ledgerBatchId` is a public logical commit-group identity and never a provider transaction ID.

`LedgerEvent`:

```text
PRIMARY KEY (tenantScopeRef, principalScopeRef, ledgerBatchId, batchOrdinal)
UNIQUE (tenantScopeRef, principalScopeRef, eventId)
UNIQUE (tenantScopeRef, principalScopeRef, streamKind, streamId, streamRevision)

eventId                    RecordId
ledgerBatchId              RecordId
batchOrdinal               integer 0..4095
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
streamKind                 closed discriminator
streamId                   RecordId
streamRevision             SafeSequence
eventType                  pinned closed semantic discriminator
eventSchemaDigest          Digest32
protectedPayloadBytes      bounded bytes
payloadDigest              Digest32
previousStreamEventDigest  Digest32?
eventDigest                Digest32
```

Commit-time ledger assertions:

- exactly `eventCount` event rows exist for each batch;
- ordinals are contiguous `0..eventCount-1`;
- every stream revision is the immediately allocated revision;
- every previous digest matches the prior event for that stream;
- each touched head advances exactly once to the final event;
- the batch digest binds the ordered event-digest list;
- no event or head may be renumbered, truncated, or reconstructed from request data after commit.

### 5.8 `AuditStreamHead` (`AH`) and `AuditEntry` (`A`)

`AuditStreamHead` makes tenant-scoped audit allocation executable under concurrent principals:

```text
PRIMARY KEY (tenantScopeRef)
tenantScopeRef              TenantScopeRef       immutable
nextAuditSequence           SafeSequence         monotonic
headAuditDigest             Digest32?
lastWriteSequence           SafeSequence         monotonic
rowRevision                 SafeSequence         monotonic CAS
```

Every transaction that appends `A` locks `AH`, allocates `A.auditSequence = AH.nextAuditSequence`, requires `A.previousAuditDigest = AH.headAuditDigest` (null exactly when the sequence is zero), inserts `A`, and CAS-advances `AH` to the next sequence, new digest, write sequence, and revision in the same transaction. Restore verifies the contiguous range `0..nextAuditSequence-1` and the final digest. Concurrent principals remain independent until they append tenant audit evidence, when they serialize briefly on `AH` without reversing the global lock order.

Purpose: privacy-minimal immutable operational evidence.

```text
PRIMARY KEY (auditEntryId)
UNIQUE (tenantScopeRef, auditSequence)

auditEntryId               RecordId
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
auditSequence              SafeSequence
bindingId                  RecordId?
overlayId                  RecordId?
kind                       COMMITTED | TERMINAL_REJECTED |
                           COMPENSATED | SUPPRESSED | RETENTION_MARKED |
                           INTEGRITY_INCIDENT
actorPseudonym             purpose-separated protected bytes
safeMetadataBytes          bounded canonical bytes
safeMetadataDigest         Digest32
privatePayloadRef          protected opaque ref?
privatePayloadDigest       Digest32?
previousAuditDigest        Digest32?
auditDigest                Digest32
createdAtServer            SafeTimestamp
```

Audit safe metadata is an allowlist, not free-form text. It must not contain raw input, full operation commitment, semantic fingerprint, raw handle, presentation, network/device data, proof bytes, or cross-scope detail.

### 5.9 `OutboxMessage` (`O`)

Purpose: atomic logical publication after an authoritative transaction.

```text
PRIMARY KEY (logicalMessageId)
UNIQUE (tenantScopeRef, principalScopeRef, sourceKind, sourceId, publicationKind)

logicalMessageId           RecordId
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
sourceKind                 LEDGER_BATCH | SUPPRESSION | COMPENSATION
sourceId                   RecordId
publicationKind            closed discriminator
safePayloadBytes           bounded canonical bytes
payloadDigest              Digest32
state                      PENDING | PUBLISHED | DEAD_LETTER
claimEpoch                 SafeSequence
attemptCount               SafeSequence
createdWriteSequence       SafeSequence
availableAtServer          SafeTimestamp
publishedAtServer          SafeTimestamp?
```

Immutable source, identity, and payload columns are inserted atomically with the source terminal/overlay. Delivery updates are monotonic. A worker may reclaim only with a greater fenced claim epoch while the row is still pending. Mark-before-publish is prohibited.

The system promises one stable logical identity, not exactly-once physical delivery. A consumer must atomically couple a unique inbox claim for `logicalMessageId` with its effect, or prove the effect independently idempotent.

### 5.10 `CompensationRecord` (`C`)

Purpose: monotonic correction after a committed base without rewriting accepted history.

```text
PRIMARY KEY (compensationId)
UNIQUE (tenantScopeRef, principalScopeRef, bindingId, compensationRevision)
UNIQUE (tenantScopeRef, principalScopeRef, bindingId, compensationCommandCommitment)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  compensationId,
  compensationDigest
)

compensationId             RecordId
bindingId                  RecordId
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
compensationRevision       SafeSequence
compensationCommandCommitment Commitment32
expectedReceiptId          RecordId
expectedReceiptResponseDigest Digest32
expectedReceiptCoreDigest  Digest32
expectedReceiptProofDigest Digest32
expectedBaseLedgerBatchId  RecordId
expectedBaseLedgerBatchDigest Digest32
expectedBaseLocatorDigest  Digest32
reasonCode                 closed discriminator
reasonDigest               Digest32
state                      constant APPLIED
resultDigest               Digest32
ledgerBatchId              RecordId
ledgerBatchDigest          Digest32
compensationDigest         Digest32
createdWriteSequence       SafeSequence
completedWriteSequence     SafeSequence
```

Version 1 permits only `ABSENT -> APPLIED`. `C=APPLIED`, the authoritative reversal, one deterministic compensation batch, exact base and overlay locators, audit plus `AH` advance, outbox, and `Q` advance commit atomically. `createdWriteSequence` equals `completedWriteSequence`. A stable denial or application failure persists no `C`, ledger, audit, outbox, or domain mutation. An unknown commit result is exactly the prior committed base or the complete applied set. Durable `REQUESTED`/`FAILED` states, asynchronous workers, and two-phase apply/fail schedules are outside version 1.

Scope-prefixed composite foreign keys bind `C` to exact `B`, base `R/P/LB`, and its `LB(kind=COMPENSATION)`. `expectedBaseLocatorDigest` is recomputed from those direct locators at commit; the base terminal is never rewritten.

### 5.11 `SuppressionRecord` (`S`)

Purpose: irreversible logical denial after authorized deletion/suppression while retaining authorized evidence.

```text
PRIMARY KEY (suppressionId)
UNIQUE (tenantScopeRef, principalScopeRef, bindingId)
UNIQUE (
  tenantScopeRef,
  principalScopeRef,
  suppressionId,
  suppressionDigest
)

suppressionId              RecordId
bindingId                  RecordId
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
deletionEpoch              SafeSequence
reasonCode                 closed discriminator
reasonDigest               Digest32
expectedBaseLocatorDigest  Digest32
expectedReceiptId          RecordId
expectedReceiptResponseDigest Digest32
expectedReceiptCoreDigest  Digest32
expectedReceiptProofDigest Digest32
expectedBaseLedgerBatchId  RecordId
expectedBaseLedgerBatchDigest Digest32
coveredCompensationId      RecordId?
coveredCompensationDigest  Digest32?
state                      LOGICALLY_SUPPRESSED |
                           ERASURE_PENDING
ledgerBatchId              RecordId
ledgerBatchDigest          Digest32
suppressionDigest           Digest32
createdWriteSequence       SafeSequence
updatedWriteSequence       SafeSequence
```

The version 1 transition is `LOGICALLY_SUPPRESSED -> ERASURE_PENDING`; no transition restores access. `ERASURE_CONFIRMED` is reserved for a future contract and is not claimed by the version 1 fixture catalog. That future contract must specify external key-destruction proof, provider failure/unknown results, idempotency, backup/restore, and its verified bridge. Suppression is authoritative immediately at the initial transaction commit and outranks compensation regardless of wall-clock time.

Scope-prefixed composite foreign keys bind `S` to exact `B`, base `R/P/LB`, optional `C` using `MATCH FULL`, and its `LB(kind=SUPPRESSION)`.

### 5.12 `RetentionTombstone` (`X`)

Purpose: immutable metadata for a policy-authorized retention boundary. Version 1 does not claim completed cryptographic erasure.

```text
PRIMARY KEY (tombstoneId)

tombstoneId                RecordId
tenantScopeRef             TenantScopeRef
principalScopeRef          PrincipalScopeRef
targetKind                 INGRESS_SETTLEMENT |
                           COMPENSATION | SUPPRESSION
targetBindingId            RecordId?
targetEvidenceId           RecordId?
targetSettlementDigest     Digest32?
targetCompensationId       RecordId?
targetCompensationDigest   Digest32?
targetSuppressionId        RecordId?
targetSuppressionDigest    Digest32?
originalLocatorDigest      Digest32
suppressionId              RecordId?
erasurePolicyId            bounded protected ref
destroyedKeyRef            bounded protected ref?
destroyedKeyVersion        UInt16?
evidenceDigest             Digest32
createdWriteSequence       SafeSequence
createdAtServer            SafeTimestamp
```

One checked target discriminator activates exactly one typed ID/digest pair and forbids the other four nullable target columns. `INGRESS_SETTLEMENT` also requires `targetBindingId` absent; `COMPENSATION` and `SUPPRESSION` require it present. Each kind has its own complete scope-prefixed composite foreign key to `W`, `C`, or `S`, plus a scope-prefixed conditional unique constraint over its complete active source tuple. `originalLocatorDigest` must equal the selected typed target digest. Generic polymorphic target lookup and unscoped `UNIQUE(targetKind,targetId)` are prohibited. A settlement target binds scope plus `W.evidenceId + W.settlementDigest`; compensation and suppression targets bind scope plus `targetBindingId`, typed ID, and exact overlay digest.

Version 1 does not authorize physical deletion of immutable locator rows or claim completed cryptographic erasure. It records a mark-only boundary while keeping every foreign-key target. Any future physical-row deletion or confirmed erasure requires a new reviewed contract version that preserves locator integrity and suppression precedence.

### 5.12a Executable scoped constraint appendix

The following tuples are literal candidate keys. `scope` below expands exactly to `(tenantScopeRef, principalScopeRef)` and is physically present in every listed record:

```text
W:  UNIQUE(scope, evidenceId, semanticRequestFingerprint)
W:  UNIQUE(scope, evidenceId, settlementDigest)
B:  UNIQUE(scope, bindingId)
B:  UNIQUE(scope, bindingId, semanticRequestFingerprint)
R:  UNIQUE(scope, bindingId, receiptId, responseBodyDigest,
           receiptCoreDigest, ledgerBatchId, ledgerBatchDigest)
P:  UNIQUE(scope, bindingId, receiptId, responseBodyDigest,
           receiptCoreDigest, ledgerBatchId, ledgerBatchDigest, proofDigest)
T:  UNIQUE(scope, bindingId, terminalDecisionId,
           semanticRequestFingerprint, terminalDecisionDigest)
LB: UNIQUE(scope, bindingId, ledgerBatchId, batchDigest)
C:  UNIQUE(scope, bindingId, compensationId, compensationDigest)
S:  UNIQUE(scope, bindingId, suppressionId, suppressionDigest)
```

The literal `MATCH FULL`, deferred/commit-time foreign-key mappings are:

```text
B(scope,evidenceId,semanticRequestFingerprint)
  -> W(scope,evidenceId,semanticRequestFingerprint)

B.COMMITTED(scope,bindingId,receiptId,receiptResponseDigest,
            receiptCoreDigest,ledgerBatchId,ledgerBatchDigest)
  -> R(scope,bindingId,receiptId,responseBodyDigest,
       receiptCoreDigest,ledgerBatchId,ledgerBatchDigest)
B.COMMITTED(scope,bindingId,receiptId,receiptResponseDigest,
            receiptCoreDigest,ledgerBatchId,ledgerBatchDigest,
            receiptProofDigest)
  -> P(scope,bindingId,receiptId,responseBodyDigest,
       receiptCoreDigest,ledgerBatchId,ledgerBatchDigest,proofDigest)
B.COMMITTED(scope,bindingId,ledgerBatchId,ledgerBatchDigest)
  -> LB(scope,bindingId,ledgerBatchId,batchDigest)
B.TERMINAL_REJECTED(scope,bindingId,terminalDecisionId,
                    semanticRequestFingerprint,terminalDecisionDigest)
  -> T(scope,bindingId,terminalDecisionId,
       semanticRequestFingerprint,terminalDecisionDigest)

P(scope,bindingId,receiptId,responseBodyDigest,receiptCoreDigest,
  ledgerBatchId,ledgerBatchDigest)
  -> R(scope,bindingId,receiptId,responseBodyDigest,receiptCoreDigest,
       ledgerBatchId,ledgerBatchDigest)
R(scope,bindingId,ledgerBatchId,ledgerBatchDigest)
  -> LB(scope,bindingId,ledgerBatchId,batchDigest)
R/P/T/LB(scope,bindingId) -> B(scope,bindingId)
```

Both `C` and `S` carry direct physical base-locator columns. Each has commit-time links to `B(scope,bindingId)`, the complete `R` and `P` tuples above, and `LB(scope,bindingId,expectedBaseLedgerBatchId,expectedBaseLedgerBatchDigest)`. Each also links `(scope,bindingId,ledgerBatchId,ledgerBatchDigest)` to an `LB` whose checked kind is respectively `COMPENSATION` or `SUPPRESSION`. `S(scope,bindingId,coveredCompensationId,coveredCompensationDigest)` optionally links with `MATCH FULL` to `C(scope,bindingId,compensationId,compensationDigest)`. For both overlays, the constraint recomputes `baseSuccessLocatorDigest` from the direct base columns and requires exact equality to `expectedBaseLocatorDigest`.

Typed tombstone constraints are separate by discriminator and cannot probe a generic target:

```text
INGRESS_SETTLEMENT:
  X(scope,targetEvidenceId,targetSettlementDigest)
    -> W(scope,evidenceId,settlementDigest)
  targetBindingId MUST be null

COMPENSATION:
  X(scope,targetBindingId,targetCompensationId,targetCompensationDigest)
    -> C(scope,bindingId,compensationId,compensationDigest)

SUPPRESSION:
  X(scope,targetBindingId,targetSuppressionId,targetSuppressionDigest)
    -> S(scope,bindingId,suppressionId,suppressionDigest)
```

Each discriminator has a conditional `UNIQUE` over its complete scoped source tuple. The checked discriminator requires exactly its active typed ID/digest pair, requires overlay `targetBindingId` only for overlay kinds, makes every inactive typed field null, and requires `originalLocatorDigest` to equal the active target digest. Constraint errors are mapped only after authenticated scope selection and are nondisclosing.

### 5.13 Key lifecycle metadata

Optional external metadata may expose only purpose, version, and closed lifecycle state:

```text
ACTIVE_USE | RETAINED_LOOKUP_OR_VERIFY | COMPROMISED | RETIRED_UNAVAILABLE
```

Key material is never stored in these records. No KMS, provider, algorithm, or rotation schedule is selected here.

## 6. Global indexes, isolation, and immutability

1. Every lookup-visible operational candidate key and index begins with `tenantScopeRef, principalScopeRef` unless the table is reached only through a mandatory scoped composite parent FK. `AH` and the tenant-global audit-sequence key are the sole exceptions because tenant scope is their complete authorization/chain scope. Surrogate primary keys may exist only behind these scoped access paths and may never be probed before authenticated scope selection.
2. There is no global commitment or fingerprint lookup.
3. Candidate commitments are computed transiently for every retained key version, sorted by `(keyVersion, commitment)`, and probed under `Q`.
4. More than one retained-version match is an integrity fault. No arbitrary winner is selected.
5. Existing rows are never silently re-keyed, re-HMACed, rebound, re-signed, or reinterpreted under a newer contract.
6. Immutable tables deny ordinary `UPDATE` and `DELETE` to runtime roles.
7. Monotonic tables expose only checked state/CAS procedures or equivalent constrained writes.
8. The terminal xor, locator completeness, digest equality, and ledger continuity assertions run at transaction commit. A provider-specific implementation must document the equivalent executable mechanism.
9. All records needed for the success transaction must share one atomic resource. Cross-resource success is prohibited until an independently specified atomic coordinator exists.

## 7. Digest and proof framing

This contract fixes preimages and exclusions without selecting a cryptographic profile.

`FrameV1(domain, fields...)` is:

1. UTF-8 ASCII domain tag followed by one zero byte.
2. For each field in declared order: unsigned 16-bit big-endian field ordinal, unsigned 32-bit big-endian byte length, then exact bytes.
3. An absent optional field uses length `0xffffffff` and no bytes.
4. Unsigned integers use exactly eight big-endian bytes.
5. Fixed-size IDs/digests use their exact bytes.

`Digest32(profileVersion, frame)` returns exactly 32 bytes using the immutable profile pinned by `commitContractVersion`. `KeyedCommitment32` and receipt proof generation likewise use pinned external profiles. Selecting those profiles is outside this document.

Required preimages:

```text
operationIdCommitment = KeyedCommitment32(
  commitmentProfileVersion,
  FrameV1("AL.OPERATION.COMMITMENT.v1",
    tenantScopeRef,
    principalScopeRef,
    rawOperationIdUtf8))

settlementDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.INGRESS.SETTLEMENT.v1",
    tenantScopeRef,
    principalScopeRef,
    evidenceId,
    commitmentKeyVersion,
    operationIdCommitment,
    semanticRequestFingerprint,
    ingressSequence,
    attemptFence,
    settledWriteSequence,
    settleReason))

receiptCoreDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.RECEIPT.CORE.v1", receiptCoreBytes))

receiptProofPreimage = FrameV1(
  "AL.RECEIPT.PROOF.v1",
  commitContractVersion,
  receiptId,
  bindingId,
  semanticRequestFingerprint,
  authoritativeRevision,
  ledgerBatchId,
  ledgerBatchDigest,
  receiptCoreDigest)

responseBodyDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.RECEIPT.RESPONSE.v1", exactResponseBytes))

terminalDecisionDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.TERMINAL.v1",
    terminalDecisionId,
    bindingId,
    semanticRequestFingerprint,
    decisionSchemaDigest,
    decidingAuthoritativeRevision,
    storedHttpStatus,
    exactResponseBytes))

eventDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.LEDGER.EVENT.v1",
    eventId,
    ledgerBatchId,
    batchOrdinal,
    streamKind,
    streamId,
    streamRevision,
    eventSchemaDigest,
    payloadDigest,
    previousStreamEventDigest))

ledgerBatchDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.LEDGER.BATCH.v1",
    ledgerBatchId,
    bindingId,
    batchKind,
    batchRevision,
    eventCount,
    orderedConcatenationOfEventDigests))

auditDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.AUDIT.v1",
    tenantScopeRef,
    principalScopeRef,
    auditEntryId,
    auditSequence,
    bindingId,
    kind,
    safeMetadataDigest,
    privatePayloadDigest,
    previousAuditDigest))

outboxPayloadDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.OUTBOX.v1",
    logicalMessageId,
    sourceKind,
    sourceId,
    publicationKind,
    safePayloadBytes))

baseSuccessLocatorDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.BASE.SUCCESS.LOCATOR.v1",
    tenantScopeRef,
    principalScopeRef,
    bindingId,
    receiptId,
    responseBodyDigest,
    receiptCoreDigest,
    receiptProofDigest,
    ledgerBatchId,
    ledgerBatchDigest))

compensationDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.COMPENSATION.v1",
    tenantScopeRef,
    principalScopeRef,
    compensationId,
    bindingId,
    compensationRevision,
    compensationCommandCommitment,
    expectedReceiptId,
    expectedReceiptResponseDigest,
    expectedReceiptCoreDigest,
    expectedReceiptProofDigest,
    expectedBaseLedgerBatchId,
    expectedBaseLedgerBatchDigest,
    expectedBaseLocatorDigest,
    reasonCode,
    reasonDigest,
    asciiAppliedState,
    resultDigest,
    ledgerBatchId,
    ledgerBatchDigest,
    createdWriteSequence,
    completedWriteSequence))

suppressionDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.SUPPRESSION.v1",
    tenantScopeRef,
    principalScopeRef,
    suppressionId,
    bindingId,
    deletionEpoch,
    reasonCode,
    reasonDigest,
    expectedBaseLocatorDigest,
    state,
    coveredCompensationId,
    coveredCompensationDigest,
    ledgerBatchId,
    ledgerBatchDigest,
    createdWriteSequence,
    updatedWriteSequence))

evidenceDigest = Digest32(
  digestProfileVersion,
  FrameV1("AL.RETENTION.TOMBSTONE.v1",
    tenantScopeRef,
    principalScopeRef,
    tombstoneId,
    targetKind,
    targetBindingId,
    selectedTypedTargetId,
    selectedTypedTargetDigest,
    originalLocatorDigest,
    suppressionId,
    erasurePolicyId,
    destroyedKeyRef,
    destroyedKeyVersion,
    createdWriteSequence,
    createdAtServer))
```

The listed order is the exact one-based field ordinal order. `selectedTypedTarget*` is the single pair activated by `targetKind`, not a concatenation of nullable alternatives. Optional fields use the `FrameV1` absent marker. No digest includes itself. `expectedBaseLocatorDigest` is recomputed from the independently constrained direct locators. Proof bytes and fields inserted into the final response are excluded from `receiptCoreBytes`; `responseBodyDigest` is computed only after the final response body is encoded.

Write-only secrets affect persistence only through their separately approved purpose commitment. `writeOnly` in an API schema is not redaction and creates no permission to log or store the secret.

## 8. Lock and CAS order

Every authoritative transaction uses this exact order:

1. `Q(tenantScopeRef, principalScopeRef)`.
2. All candidate `W` rows for retained commitment-key versions, ordered by `(keyVersion, operationCommitment)`.
3. Matching `B`.
4. Principal first-user uniqueness/CAS guard and opaque semantic authority rows, ordered by stable physical key.
5. Touched `H` rows ordered by `(streamKind, streamId)`.
6. Tenant `AH`, when the transaction appends audit evidence.
7. Existing terminal/overlay locators ordered by scoped logical ID.
8. Inserts and domain updates.
9. Outcome/fence CAS updates are last in the declared write order: terminal/overlay state first, then `B`/`W` as applicable, then `AH`, and finally `Q`. `AH` is never advanced before its `A` row or before a later insert in the same transaction.

No retry, worker, compensation, suppression, retention, restore, or repair path may reverse this order. Concurrent principals may proceed independently until a transaction appends tenant audit, when it briefly serializes on `AH`. Distinct operation commitments for one principal serialize at `Q` and the first-user uniqueness guard.

## 9. Transaction blueprints

### 9.1 Static-invalid request

Authentication, transport/header shape, duplicate-header preflight, raw operation-ID lexical bounds, parser duplicate-member rejection, strict schema, static semantic validation, authoritative-CAS shape, and semantic fingerprint construction run before any operation lookup.

Failure leaves zero operation-owned persistent records. It performs no commitment derivation, `Q/W/B` lookup, collision check, audit, terminal decision, or domain lookup.

### 9.2 Valid ingress registration

1. Derive candidate commitments for every retained lookup-key version in transient memory.
2. Begin a primary serializable transaction.
3. Lock `Q`, then candidate `W` rows in canonical order.
4. More than one match is an integrity fault.
5. Existing same commitment and different fingerprint returns an internal nondisclosing collision with zero mutation before domain lookup.
6. Existing same fingerprint returns the current stored state with zero mutation.
7. If absent, increment `Q.ingressSequence` and `Q.writeSequence`; insert `W=REGISTERED` with immutable fingerprint, ingress sequence, attempt fence zero, and resulting write sequence.
8. Commit.
9. Only after commit may the server issue an authenticated opaque reconcile token.

The token binds authenticated scope, commitment/key version, fingerprint, ingress sequence, required write sequence, and token-proof version. It contains no raw operation ID or provider transaction identity.

### 9.3 Binding admission

1. Lock `Q`, candidate `W`, and the selected evidence row.
2. Require `W=REGISTERED`, exact fingerprint, and current attempt fence.
3. Insert one `B=IN_PROGRESS` with immutable fingerprint/admission digest.
4. Increment `W.attemptFence` and `Q.writeSequence`.
5. CAS `W REGISTERED -> BOUND`, attach `bindingId`, and stamp the resulting sequence.
6. Commit.

A crash before commit leaves `W=REGISTERED`; after commit it leaves `W=BOUND` and `B=IN_PROGRESS`.

### 9.3a Settlement without a binding

This is the only version 1 path to `W=SETTLED_NO_BIND`:

1. Authenticate the exact tenant/principal scope and validate the fingerprint and reconcile token before persistence.
2. On the linearizable primary, begin a serializable transaction and lock `Q`, then every retained-version `W` candidate.
3. Require exactly one matching `W=REGISTERED`, no `B` or terminal under any retained-version candidate, the exact expected attempt fence, a valid token ingress/required sequence, primary catch-up, and a fenced prior leader.
4. Increment `Q.leaderEpoch`; CAS `W.attemptFence` above every issued worker fence; allocate the one new `Q.writeSequence` value.
5. From those final fenced values compute `settlementDigest = Digest32(profile, FrameV1("AL.INGRESS.SETTLEMENT.v1", ...))` using the exact preimage in section 7.
6. Atomically CAS `W REGISTERED -> SETTLED_NO_BIND`, keep `bindingId` null, store the closed `settleReason` and `settlementDigest`, and set `settledWriteSequence = lastWriteSequence = Q.writeSequence`.
7. CAS `Q`, commit, then issue updated authenticated barrier evidence.

The exact write set is `Q+W`. It creates no `B/R/P/T/LB/LE/H/AH/A/O/C/S/X` or domain effect. An unknown commit reconciles to the exact prior `REGISTERED` state or the complete `SETTLED_NO_BIND` state. Any failed precondition returns `UNKNOWN`, `BARRIER_TIMEOUT`, a nondisclosing scope result, or `INTEGRITY_FAULT` as appropriate, with zero mutation and no retry/new-key authority.

### 9.4 New successful terminal

Read/lock set:

```text
Q, every candidate W, B, first-user uniqueness guard,
opaque semantic/CAS/authority rows, touched H rows,
  deterministic-ID uniqueness probes, tenant AH,
  and scoped terminal/overlay locator probes
```

One finalization transaction:

1. Require exact fingerprint, `W=BOUND`, `B=IN_PROGRESS`, and current attempt fence.
2. Re-evaluate every dynamic precondition and authoritative CAS under lock.
3. Allocate contiguous stream revisions from `H`.
4. Compute complete batch membership, event/batch digests, exact receipt bytes, receipt digests/proof, audit digest, and logical outbox identity.
5. Apply the opaque authoritative domain effect.
6. Insert `LB`, every `LE`, and CAS every touched `H`.
7. Insert `R`, `P`, `A`, and `O=PENDING`.
8. CAS `B IN_PROGRESS -> COMMITTED` with the complete locator/digest tuple.
9. CAS `W BOUND -> TERMINAL`.
10. CAS `AH` to the allocated audit sequence/digest, increment/stamp `Q.writeSequence`, and commit.

That commit is the sole authoritative commit point. A response is sent only afterward. No broker, cache, analytics, or other nontransactional call occurs inside finalization.

### 9.5 New durable terminal rejection

A durable rejection requires a stable post-admission fact proven under primary locks. In one transaction:

```text
read/lock Q, W, B, exact deciding authority rows, AH, and scoped locators
insert immutable T
insert minimal A
CAS B IN_PROGRESS -> TERMINAL_REJECTED with complete locator/digest
CAS W BOUND -> TERMINAL
CAS AH to the allocated audit sequence/digest
increment/stamp Q.writeSequence
commit
```

It writes no opaque success effect, ledger batch/event, receipt/proof, or outbox message.

### 9.6 Same key and fingerprint while in progress

A live duplicate performs no mutation and starts no second command transaction. It returns `IN_PROGRESS` or waits for a bounded primary result.

An orphan may resume only after the primary barrier fences the earlier leader/session, locks `Q/W/B`, proves no terminal or committed finalization, CAS-increments the attempt fence, and receives the exact validated opaque semantic core whose fingerprint equals the binding. A fingerprint is not enough to reconstruct request content.

If `W=REGISTERED` has no `B`, recovery may bind only from an exact re-presented validated core, or fence all possible workers and transition to `SETTLED_NO_BIND`. The latter is not automatically reopened.

### 9.7 Same key and fingerprint after terminal

Use a linearizable primary read. Verify binding/fingerprint/admission digest, locator completeness, exact response/decision digest, historical proof, ledger batch membership and stream chains, then overlay integrity and precedence. Return exact stored terminal status/body. Do not revalidate current business policy, append audit, resend outbox, or mutate timestamps.

### 9.8 Same key and different fingerprint

After static validation, inspect only scoped ingress/binding evidence. Return an internal collision disposition with zero mutation and before any business lookup. External status/body mapping is parameterized and nondisclosing.

### 9.9 Concurrent distinct keys for one principal

Distinct `W/B` rows may exist. Finalization serializes on `Q` and the principal first-user uniqueness guard. One operation can commit the complete success set. A loser rechecks after the winner and may atomically create its own stable terminal rejection plus minimal audit. A serialization abort resumes under its existing binding; it never blindly repeats domain effects.

### 9.10 Unknown transaction outcome

Connection loss never implies commit or rollback and never authorizes a new operation key:

- ingress commit: either no evidence or complete `Q+W`;
- binding commit: either `W=REGISTERED`, or `W=BOUND+B`;
- success commit: either prior in-progress state or the entire success set;
- rejection commit: either prior in-progress state or the entire rejection set;
- compensation/suppression: either exact prior state or the complete append.

Resolve only through the primary barrier. Partial visibility is corruption, not a valid crash outcome.

### 9.11 Compensation append

For a verified committed base without suppression, version 1 is synchronous only:

1. Lock in the global order and verify base receipt/proof and current overlays.
2. Exact compensation-command replay returns zero mutation.
3. In one transaction apply the compensating opaque domain delta, insert immutable `C=APPLIED`, one deterministic one-event ledger batch, audit plus `AH` CAS, outbox, and head/watermark advances.
4. Commit without modifying `B`, `R`, `P`, or the original ledger batch.

A stable denial/application failure leaves the base unchanged and persists no `C`. Unknown commit resolves to the exact prior base or the complete applied overlay. Version 1 has no durable `REQUESTED` or `FAILED` state and no asynchronous compensation worker.

### 9.12 Suppression append

1. Lock and verify the base and any compensation.
2. Exact suppression replay returns zero mutation.
3. In one transaction insert logical access denial, `S=LOGICALLY_SUPPRESSED`, one deterministic one-event ledger batch, audit plus `AH` CAS, outbox, and head/watermark advances.
4. Keep the base terminal and compensation immutable.

Logical suppression is authoritative at commit and immediately wins reconciliation. Policy-controlled erasure may follow idempotently and can never restore access.

### 9.13 Mark-only retention boundary

Version 1 starts from a verified `S=LOGICALLY_SUPPRESSED` base. Acquire locks in the global order: `Q`, retained-version `W`, `B`, no opaque/H locks, `AH`, then scoped base/S/typed-X locator probes. Verify there is no existing `X`, allocate one `Q` write sequence and one audit sequence, compute the new suppression/tombstone/audit digests, then atomically:

- insert typed `X(targetKind=SUPPRESSION)` bound to the new `S` digest;
- insert one `A`;
- CAS `S LOGICALLY_SUPPRESSED -> ERASURE_PENDING`;
- CAS `W TERMINAL -> RETENTION_BOUNDARY`;
- CAS `AH`, then CAS `Q` and commit.

The exact delta is monotonic `W/S`, `+1 X`, `+1 A`, and `AH/Q` advance. It has zero domain, `C`, `R/P/T`, `LB/LE/H`, or `O` change. Exact replay is zero mutation. Unknown commit is the exact prior suppressed state or the full marked state; partial visibility is corruption. This is a retention marker, not retention-duration, deletion-law, key-destruction, or erasure-confirmation approval.

## 10. Primary barrier and reconciliation

### 10.1 Caller inputs and authorization

The caller supplies:

- authenticated tenant/principal context;
- raw operation ID transiently in the protected header;
- exact opaque semantic fingerprint;
- last authenticated reconcile token, when available.

Tenant and principal are never accepted from request-body authority. The server validates raw-token/header/fingerprint/token shape without persistence, derives all retained-version commitments transiently, and searches only within the authenticated composite scope.

### 10.2 Barrier protocol

1. Verify that a supplied token binds the same authenticated scope, protected commitment/key version, fingerprint, ingress sequence, and required write sequence.
2. Route to the current authoritative primary. Caches, replicas, read followers, and eventually consistent indexes are prohibited.
3. Fence the prior primary/leader. After failover, wait until the new primary has applied at least the token's required application write sequence.
4. Begin a serializable primary transaction and lock `Q`, all candidate `W`, then `B`.
5. Require `Q.writeSequence >= token.requiredWriteSequence`.
6. Resolve state and verify locator/ledger/overlay integrity in that same snapshot.
7. Return the disposition and updated authenticated barrier evidence.

Timeout, uncertain leader fencing, missing watermark, inability to acquire the keyed lock, unavailable retained key, ambiguous match, or incomplete failover catch-up returns `UNKNOWN`/`BARRIER_TIMEOUT`. It never returns absence and never authorizes takeover.

### 10.3 Exact `NOT_FOUND_AFTER_BARRIER` evidence

Every condition is mandatory:

- valid server-issued token for the authenticated scope, key commitment/version, and fingerprint;
- matching durable `W` exists;
- `W.state=SETTLED_NO_BIND`;
- `W.settledWriteSequence >= token.requiredWriteSequence`;
- `W.attemptFence` disables every earlier worker/leader;
- current primary is fenced and applied through that sequence;
- serializable all-retained-version read finds no `B`, terminal, or covering tombstone;
- evidence and required key versions remain within the guaranteed reconciliation horizon;
- no ambiguity, corruption, or partial locator exists.

The executable catalog represents those predicates as the closed `NFAB_VALID` barrier profile. It also contains a committed `REGISTERED -> SETTLED_NO_BIND` settlement schedule and negative profiles for missing/mismatched token or scope, low settled sequence, insufficient attempt fence, replica/not-caught-up primary, retained-key coverage failure, a present binding/terminal/tombstone, expired horizon, multiple matches, and corruption. Only `NFAB_VALID` may yield `NOT_FOUND_AFTER_BARRIER`.

`NOT_FOUND_AFTER_BARRIER` is informational only. It grants no automatic retry, new key, uniqueness bypass, or suppression bypass. Ordinary primary absence without matching settled evidence is `UNPROVEN_ABSENCE`/`UNKNOWN`.

### 10.4 Reconciliation precedence

Structural integrity is checked before semantic disposition:

```text
valid suppression
  > valid compensation
  > valid committed or rejected base terminal
  > in progress
  > settled without binding
  > unproven absence
```

Corruption at any higher-precedence layer produces `INTEGRITY_FAULT`; reconciliation must not fall through to a lower layer.

| Durable state | Internal disposition |
| --- | --- |
| no token/evidence/binding | `UNPROVEN_ABSENCE`; no retry authority |
| `W=REGISTERED`, no `B` | `IN_PROGRESS` or `RECOVERY_REQUIRED` |
| valid `W=SETTLED_NO_BIND` barrier | `NOT_FOUND_AFTER_BARRIER` |
| `W=BOUND`, missing `B` | `INTEGRITY_FAULT` |
| `B=IN_PROGRESS` | `IN_PROGRESS`; takeover only after fence |
| valid rejected base | exact terminal rejection |
| valid committed base, no overlay | exact committed receipt |
| committed plus applied compensation | base receipt retained; `COMPENSATED` |
| any committed base plus valid suppression | `COMMITTED_THEN_SUPPRESSED` |
| retention boundary/tombstone | explicit retention-qualified outcome, never ordinary absence |

## 11. Crash and fault matrix

Legend: `Z` no operation-owned state; `REG` registered evidence; `IP` bound/in-progress; `FULL-C` complete success; `FULL-T` complete rejection; `PRE` exact prior overlay state.

| Fault point | Durable state | Required action | Prohibited action |
| --- | --- | --- | --- |
| before or during static validation | `Z` | return static failure | lookup, bind, audit |
| after fingerprint, before ingress transaction | `Z` | unproven absence only | not-found-after-barrier |
| after `Q` CAS or `W` insert, before ingress commit | `Z` after rollback | none | token issuance |
| connection loss during ingress commit | `Z` or `REG` | primary barrier | assuming result |
| ingress committed, response/token lost | `REG` | scoped lookup and replacement token | duplicate `W` |
| any settlement write before settlement commit | `REG` after rollback | retry same fenced settlement | exposing partial settled evidence |
| connection loss during settlement commit | `REG` or complete `SETTLED_NO_BIND` | primary barrier | inferring not-found from absence |
| settlement committed, token lost | complete `SETTLED_NO_BIND` | issue replacement authenticated evidence after exact primary read | reopen with a new key |
| binding writes before binding commit | `REG` after rollback | retry same binding CAS | exposing uncommitted `B` |
| connection loss during binding commit | `REG` or `IP` | primary barrier | blind second insert |
| binding committed before finalization | `IP` | fenced continuation with exact core | new logical operation |
| any success write before final commit | `IP` after rollback | fenced continuation | partial repair/replay |
| connection loss during success commit | `IP` or `FULL-C` | primary barrier | inference |
| success commit acknowledged but response lost | `FULL-C` | exact receipt replay | re-execution |
| any rejection write before rejection commit | `IP` after rollback | re-evaluate stable decision | partial terminal |
| connection loss during rejection commit | `IP` or `FULL-T` | primary barrier | converting transient failure |
| overlay write before overlay commit | `PRE` | retry fenced command | partial overlay |
| connection loss during overlay commit | `PRE` or complete overlay | primary barrier | blind second effect |
| compensation denial or apply failure before commit | `PRE` | return stable denial/failure | durable `C=REQUESTED/FAILED` |
| compensation connection loss during commit | base or complete `C=APPLIED` overlay | primary barrier | asynchronous partial completion |
| any retention-mark write before commit | suppressed `PRE` | retry exact marked-boundary command | partial `W/S/X/A` repair |
| connection loss during retention-mark commit | suppressed `PRE` or complete retention boundary | primary barrier | claiming completed erasure |
| broker accepts, acknowledgement lost | delivery may exist; outbox pending | republish same logical ID | new ID/effect |
| worker marks published after commit, response lost | outbox published | read row, no new publish | republish as new event |
| duplicate worker or consumer delivery | unchanged source rows | consumer dedupe | second domain effect |
| stale replica reports absence | primary may contain terminal | route primary/unknown | definitive absence |
| failover before old primary is fenced | unknown | fence and catch up | takeover or not-found |
| new primary below required write sequence | unknown | wait or return timeout | reading absence |
| wall-clock/lease skew | no state inference | primary lock and attempt CAS | time-only takeover |
| terminal locator target missing/partial | integrity fault | exact trusted restore/quarantine | re-execution/regeneration |
| receipt/terminal digest mismatch | integrity fault | exact trusted restore | re-signing changed bytes |
| proof missing/invalid | verification failure | pinned historical verifier/restore | silent replacement |
| ledger gap/hash/batch mismatch | integrity fault | halt publication and exact restore | renumber/reconstruct |
| required outbox missing after terminal | committed plus integrity incident | controlled same-ID repair if separately approved | rerun domain command |
| corrupt suppression | integrity fault | exact restore | fall through to base |
| corrupt compensation without suppression | integrity fault | exact restore | ordinary committed result |
| retained lookup key unavailable | unknown/key unavailable | restore approved access | treat as absent |
| multiple retained-version matches | integrity fault | investigate/quarantine | choose arbitrarily |
| historical proof verifier unavailable/retired | verification unavailable/unknown | restore approved verifier access | report mismatch or silently re-sign |
| historical proof key known compromised | security integrity incident/quarantine | approved incident process | return receipt or silently re-sign |
| evidence outside retention boundary | retention-qualified unknown | policy response | retry authority |
| body inaccessible without covering tombstone | integrity fault | exact restore/quarantine | never-committed inference |

No valid crash point permits a partially durable domain effect, ledger, receipt, audit, outbox, or terminal locator. If such a combination is observed, it is corruption or an atomicity breach.

Corrupt `C` or `S` is checked before overlay precedence. It never falls through to the valid base or to the other overlay, never mutates the corrupt record, and is represented by dedicated injected-corruption vectors/profiles/fixtures.

## 12. Outbox publication semantics

The outbox row is inserted in the same final transaction as its source terminal/overlay. Workers claim with a checked epoch, publish the stable logical ID, and only then CAS the row to `PUBLISHED`.

Physical delivery is at least once. Exactly-once logical application exists only when a consumer atomically persists a unique `(consumerId, logicalMessageId)` inbox claim with its effect, or the effect is independently idempotent. The producer never creates a replacement logical ID after an unknown publish result.

An outbox repair may recreate a missing row only under a separately authorized integrity procedure that verifies the immutable source, recreates the exact same logical ID/payload/digest, and appends an integrity audit. It never re-runs the authoritative command or creates a new ledger event.

## 13. Key custody and rotation boundary

1. New ingress evidence uses one active commitment-key version.
2. Lookup computes every retained-version commitment transiently and locks all candidates under `Q` before inserting.
3. Existing evidence retains its original key version/output forever; no silent rebinding.
4. Required lookup-key retention spans the entire idempotency, reconciliation, backup/restore, and suppression horizon. The duration remains policy-controlled.
5. If a required old key is unavailable or compromised and no authorized tombstone resolves the state, admission/reconciliation fails closed as unknown.
6. Receipt proofs pin profile and key version. New signing uses the active signer; historical verification uses retained verification material.
7. Historical receipts are never silently re-signed. Compromise produces an explicit incident/unverifiable result pending a separately approved recovery protocol.
8. Barrier-token verification material is retained through its guaranteed barrier horizon; unavailable verification makes reconciliation unknown.
9. Keys, raw operation IDs, secrets, and credentials are absent from database rows, backups governed by this packet, logs, metrics, traces, and exception messages.

This document selects no KMS, cloud, provider, key algorithm, digest algorithm, proof algorithm, storage region, or rotation duration.

## 14. Backup, restore, and retention integrity

Backups must be transaction-consistent across all records participating in the atomic boundary. Restore remains unavailable until all of these pass:

- restored `Q` sequences cover the advertised recovery point;
- every `W/B` relationship and terminal xor validates;
- every receipt/decision digest and retained proof validates;
- ledger batches, stream chains, and heads are contiguous;
- audit chains validate;
- compensation and suppression overlays are complete and precedence-safe;
- tombstones and erasure evidence are restored no earlier than their acknowledged sequences;
- outbox rows retain their original logical identities and safe delivery state;
- required historical verification/lookup metadata is available.

A restore that omits a later acknowledged suppression/tombstone must keep the affected scope unavailable; it must never resurrect authority. A restore with pending outbox rows republishes only the original logical IDs.

Backup cadence, media, geographic placement, retention duration, legal hold, cryptographic-erasure mechanism, and disaster-recovery targets are unresolved policy/provider decisions.

## 15. Server-to-local projection boundary

The server may hand off only the immutable exact receipt response and verification envelope required by the frozen API contract: receipt ID, core/full-response digests, proof profile/key version/proof bytes, authoritative revision, and ledger batch locator/digest.

Local projection must:

1. verify proof, digests, and trusted profile metadata before any local mutation;
2. apply `expectedLocalProjectionRevision` only as a local CAS;
3. identify an already-projected result by exact receipt ID, response digest/proof, and resulting local projection revision;
4. return zero local mutation for that exact replay;
5. reconcile with the primary when local apply is uncertain or its marker is corrupt;
6. rebuild only from a verified immutable receipt under a separately approved local recovery path.

The local projection revision and local `ProfileId` never cross the server boundary defined here. Local uncertainty never re-executes server authority or invents a new operation key. This document does not design or modify `SaveGameData`, `Bootloader`, any local profile schema, or issue #450 implementation.

## 16. Security and privacy controls

- Authentication determines tenant/principal scope; body/header identity claims do not.
- Wrong-principal and cross-tenant callers cannot lock, query, time, or distinguish another scope's commitment, retention state, or terminal.
- Public collision, unknown, wrong-scope, and retention response mapping remains parameterized and nondisclosing.
- Full commitments and fingerprints are sensitive and must not be general-purpose correlation IDs.
- Logs contain only bounded internal codes, ephemeral trace IDs, and policy-approved rotating purpose pseudonyms.
- Pseudonymous refs, commitments, digests, and tombstones may remain personal or sensitive data. This contract makes no contrary legal claim.
- Handle plaintext may exist only in a separately authorized identity domain; this durability layer does not copy it into bindings, audit, ledger indexes, or outbox metadata.
- Audit is append-only and allowlisted. Free-form operator notes are prohibited in this record set.
- Break-glass restore/repair requires separate authorization, dual-control policy where required, complete audit, and exact artifact verification. No policy details are approved here.

## 17. Executable fixture contract

`First_User_Operation_Durability_Fixtures.v1.json` is the executable catalog for this document. It contains exactly 115 stable fixture IDs: 112 executable scenario bodies plus three explicit alias/coverage IDs. It contains 20 named state vectors and 18 distinct record-count tuples; state or integrity may distinguish vectors whose counts coincide. The aliases do not add behavior or double-count execution:

- `DUR-PRV-001-CROSSPRINCIPAL -> DUR-BAR-004-WRONGPRINCIPAL`;
- `DUR-PRV-002-CROSSTENANT -> DUR-BAR-005-WRONGTENANT`;
- `DUR-KEY-006-MULTIMATCH -> DUR-BAR-006-MULTIMATCH`.

The catalog fixes:

- vector order `Q/W/B/R/P/T/LB/LE/H/AH/A/O/C/S/X`;
- canonical record-count states;
- allowed monotonic transitions;
- exact lock order;
- terminal xor and locator assertions;
- per-result expectations for every unknown commit alternative;
- exact settlement and primary-barrier predicates;
- deterministic ledger continuity and batch profiles;
- crash schedules and allowed before/after states;
- outbox logical deduplication;
- prohibited persistence/log fields;
- admission, replay, collision, concurrency, unknown-outcome, reconciliation, corruption, overlay, retention, rotation, privacy, outbox, barrier, restore, and local-projection fixtures.

Every concrete fixture must run all six oracles; aliases execute their canonical target and add only coverage attribution. Each allowed result also names an exact audit profile: zero entries or the complete contiguous tenant audit sequence, entry-kind order, previous-digest chain, and head-advance count for that vector.

1. `OR-COUNT`: exact vector and zero unexpected records/effects.
2. `OR-LEDGER`: exact zero or contiguous ordinals/revisions, prior-digest chain, declared count, deterministic batch membership/digest, and head advance.
3. `OR-RECEIPT`: exact zero or complete body/core digest, proof, locator, and authoritative revision verification.
4. `OR-PROHIBITED`: scan rows, indexes, diagnostics, exceptions, logs, fixtures, and serialization for forbidden data.
5. `OR-PRIVLOG`: only bounded nondisclosing code, ephemeral trace, and approved rotating pseudonym.
6. `OR-BARRIER`: exact settlement/token/scope/fingerprint/sequence/fence/primary/key-horizon/absence predicates, or an explicit non-NFAB result.

Any newly admitted opaque semantic field must change the semantic fingerprint. The mutation fixture intentionally names no product-domain field.

`ERASURE_CONFIRMED` is explicitly outside version 1 fixture scope. No state machine edge, outcome, or fixture in this catalog claims completed erasure.

## 18. Resource and optimization impact

This documentation change has no runtime, binary, package, install, memory, frame-time, device, or network effect by itself.

A future implementation must measure and bound:

- one `Q` lock plus retained-version `W` probes per admission/reconcile;
- write amplification for one binding, terminal artifacts, ledger batch/events, audit, and outbox;
- retained evidence/tombstone growth over the approved horizon;
- contention for concurrent operations on one principal;
- receipt/event payload storage and backup growth;
- outbox backlog, duplicate delivery, and consumer inbox retention;
- primary-barrier latency during ordinary load and failover;
- restore verification time and required historical key availability.

No cloud, database, server runtime, load, failover, backup-restore, profiler, Unity Player, package, install, or device measurement has been performed for this planning contract.

## 19. P0/P1/P2 implementation gates

P0 before backend implementation:

- retain executable `Q/W` ingress/settlement evidence or remove strict not-found-after-barrier;
- select one atomic transactional resource or independently specify an atomic coordinator;
- implement leader/attempt fencing and application write sequences;
- guarantee all-retained-key-version lookup and historical proof verification;
- adopt executable terminal-xor, locator, ledger, and immutability enforcement.

P1 before production use:

- approve retention/erasure horizon and key lifecycle;
- approve nondisclosing public error mapping;
- specify async compensation and erasure workers, if used;
- define backup, restore, incident, and break-glass controls;
- prove bounded resource ceilings and operational monitoring.

P2 before release readiness:

- provider-specific index/capacity tuning and load evidence;
- failover and disaster-recovery drills;
- archive/restore operational evidence;
- privacy/security review against selected regions and policies;
- measured service SLOs and cost/capacity budgets.

## 20. Publication and next dependency

This contract is coordination/review evidence only. It does not close #137, #450, or #467.

After A1 review, the smallest implementation dependency is a dedicated server-authority issue plus selection of a supported server runtime, one atomic transaction boundary, deployable key/proof profiles, and minimum reconciliation/retention guarantees. The first implementation slice should be an engine-free pure state machine and fault-injection conformance harness for `Q/W/B`, terminal xor, crash vectors, ledger continuity, primary barriers, and logical outbox deduplication before HTTP, cloud deployment, Unity, Android, or local-save integration.

## 21. Explicit nonclaims

This document does not approve or claim:

- a database, cloud, KMS, IdP, provider, server runtime, network topology, or deployment;
- a digest, signature, MAC, encryption, credential, or key-custody profile;
- retention duration, deletion law, legal hold, regional residency, export, or public error policy;
- exactly-once physical messaging;
- backend implementation, migration, scale, availability, security certification, or operational readiness;
- local-save compatibility or any `ProfileId`/projection implementation;
- product journey completeness, user approval, playtest acceptance, release readiness, or credit use.
