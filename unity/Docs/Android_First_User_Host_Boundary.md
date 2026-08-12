# Android First-User Host Boundary

**Status:** A1-approved planning contract; dormant and non-executable  
**Primary delivery mode:** coordination/review specification  
**Exact publication base:** main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0  
**Upstream issues:** [#135](https://github.com/yulee94/AnotherLife/issues/135), [#463](https://github.com/yulee94/AnotherLife/pull/463)  
**Runtime activation:** not authorized  
**Shared-file lock:** none

## 1. Purpose

This document defines the privacy-safe boundary between:

1. the approved first-user commit and local-projection state contracts;
2. a future native Android host/recovery surface; and
3. the dormant Android-to-Unity host foundation tracked by issue #135.

The boundary lets Android present progress, recovery, and host readiness without becoming an account, profile, receipt, persistence, or server authority. It preserves one authoritative onboarding operation across retries and lifecycle changes and prevents Compose or Unity route payloads from receiving sensitive identity or receipt material.

This is a planning contract only. It does not add Android code, Compose UI, backend transport, local projection, Unity routing, packaging, or production activation.

The A6 executable machine contract delivered after this planning work began remains partial and under review. This document consumes only the approved abstract proof boundary. It does not bind Android to provisional A6 DTO names, ledger names, request-field composition, or executable persistence behavior.

## 2. Approval Boundaries

A1 has approved this document as implementation-planning evidence.

This approval does not approve:

- production route registration or host mounting;
- server, authentication, account, profile, character, or local projection implementation;
- the partial A6 executable machine contract;
- MainActivity, navigation, shell, Gradle, manifest, resource, or Unity changes;
- visual design, player copy, Figma, #366, or #371;
- packaged Android-to-Unity round-trip behavior;
- player, milestone, release, or irreversible-profile acceptance.

The user retains final product, visual, player, milestone, and release approval. The authorized co-developer review requested through #463 is a required publication review, not automatic runtime approval.

## 3. Current Source Baseline

On the exact publication base:

- Android has the strict version-2 Unity request/outcome contract and session correlation.
- Android has a dormant Unity lifecycle/ownership host with bounded process-wide lease handling.
- The app does not have an authenticated server repository, profile repository, first-user commit repository, or local projection repository.
- The current Android shell does not mount the Unity host in a production route.
- Unity SaveAuthority contains local ProfileId, epoch, generation-fingerprint, and receipt concepts. Those C# fields are not an Android UI contract and must not be copied into Compose.
- Coroutines core and lifecycle-compose are already available. A future dormant adapter requires no new dependency.
- A3 first-user state names and the approved cursor shape are planning authority, not merged runtime APIs.

The absence of an Android backend or projection implementation is intentional. A future adapter must consume private ports rather than infer persistence or server behavior.

## 4. Binding Principles

1. One onboarding operation has one identity.
2. Server receipt verification and local projection verification are separate proofs.
3. A terminal cursor does not itself prove that a receipt is valid.
4. A verified server receipt does not itself prove that a local ProfileId projection exists.
5. A local ProfileId remains local projection authority and never crosses into Compose or server genesis/receipt composition.
6. Compose receives presentation state and generic intents only.
7. Host retries cannot repeat server genesis or local projection.
8. Lifecycle restart cannot create a new operation or key.
9. Unknown, contradictory, stale-current, or under-specified state fails closed.
10. The adapter exposes no create, commit, project, rollback, delete, or reset method.
11. Player-visible support data is a bounded code, never raw technical evidence.
12. A future engineering implementation remains dormant until separately activated.

## 5. Canonical A3 State Set

The adapter recognizes exactly these append-only state identifiers:

- first_user.commit_pending
- first_user.commit_recovery
- first_user.committed_projection_pending
- first_user.recovery_required
- first_user.commit_verified
- first_user.prologue_transition

Unknown identifiers fail closed. The adapter does not rewrite these identifiers or infer an unknown state from nearby data.

### 5.1 Meaning

| State | Binding meaning |
| --- | --- |
| first_user.commit_pending | The commit owner has an in-flight operation. The host adapter does not retry or replace it. |
| first_user.commit_recovery | The server outcome requires same-operation reconciliation. |
| first_user.committed_projection_pending | An immutable server receipt is verified, while local receipt-to-ProfileId projection is not yet verified. |
| first_user.recovery_required | A typed contract, receipt, revision, projection, or ownership conflict blocks forward progress. |
| first_user.commit_verified | Both server-receipt and local-projection proofs are verified; A3 may advance narrative state. |
| first_user.prologue_transition | The verified first-user state has entered the A3-owned transition to the 3D prologue. |

Commit verification and prologue transition are intentionally separate. Android cannot skip directly into Unity merely because commit proofs exist.

## 6. Single Operation Binding

The exact transport Idempotency-Key value and onboardingOperationId are one field, byte-for-byte. They are not two correlated fields.

Same-operation reconciliation binds:

- the current authenticated principal;
- the unchanged onboardingOperationId header value; and
- the unchanged semanticRequestFingerprint.

The adapter must not:

- generate a replacement operation ID;
- trim, case-fold, Unicode-normalize, URL-encode, parse, or JSON-round-trip the key;
- rebuild the key from another identifier;
- calculate or inspect the canonical semantic fingerprint;
- add local ProfileId to the fingerprint;
- accept AccountId as a substitute for the authenticated principal;
- retry under a different authenticated principal;
- synthesize receiptId or commitId before authoritative server return.

The authority port owns the authenticated session and exact transport. Android receives an opaque operation-binding handle whose equality preserves exact bytes and whose string form is always redacted.

receiptId and commitId may exist only after authoritative server return. commitId is not added to the local cursor defined below.

appearanceDraftReference and appearanceDigest remain blocked and non-authoritative. They are not adapter inputs and cannot satisfy commit, projection, or host readiness.

## 7. Context7 Local Cursor

The validated cursor contains exactly these conceptual fields:

- recordSchemaVersion
- commitContractVersion
- onboardingOperationId
- draftId
- semanticRequestFingerprint
- expectedAuthoritativeRevision
- expectedLocalProjectionRevision
- phase
- terminalReceiptDigest
- terminalReceiptId

All cursor data is private to the authority boundary. No cursor field is public to Compose.

### 7.1 Local Revision Variant

expectedLocalProjectionRevision is a closed variant:

~~~text
Applicable(opaqueRevision)
NotApplicable
~~~

Null or implicit absence is invalid. NotApplicable is legal only when the owning contract explicitly has no local compare-and-swap revision. Android never infers NotApplicable from missing data.

### 7.2 Phase Set

~~~text
Unspecified
PreparedForCommit
TerminalReceiptInstalled
Retired
~~~

Rules:

- Unspecified is invalid/default and always fails closed.
- PreparedForCommit requires source-validated operation, draft, fingerprint, and revision evidence. Terminal receipt fields are absent.
- TerminalReceiptInstalled requires an exact fixed terminalReceiptDigest.
- terminalReceiptId is required only when the approved server contract declares a Required locator policy.
- terminalReceiptId must be absent when the approved server contract declares a NotUsed locator policy.
- An ambiguous optional locator policy is invalid.
- Retired is reserved. Until A1 approves exact active semantics, it cannot reconcile or mount and maps to fail-closed recovery.
- Unknown phases or versions fail closed.
- A terminal field appearing before authoritative return is invalid.
- TerminalReceiptInstalled without the exact digest, or without a required locator, is invalid.

The cursor owner atomically installs terminal phase, canonical digest, and any required locator. The adapter cannot assemble a terminal cursor from partial callback data.

## 8. Separate Proof Types

The boundary keeps these proofs distinct.

### 8.1 Server Receipt Proof

~~~text
Absent
Pending
Verified(opaqueVerifiedTerminalReceiptHandle)
RecoveryRequired(reason)
~~~

Verified means the owning authority has:

- received an authoritative terminal server result;
- validated the immutable canonical receipt;
- verified the exact terminal digest;
- verified the terminal locator when one is required; and
- installed the canonical terminal evidence consistently with the cursor.

The adapter never receives the raw receipt body.

### 8.2 Local Projection Proof

~~~text
Pending
Verified(opaqueLocalProjectionHandle)
RecoveryRequired(reason)
~~~

The local projection owner may internally bind a ProfileId. The handle exposed to Android proves only the disposition. It contains no public ProfileId field and cannot be serialized into UI state.

### 8.3 Proof Ordering

~~~text
Prepared operation
-> authoritative server receipt verified
-> terminal cursor installed
-> local receipt-to-ProfileId projection pending
-> local projection verified
-> A3 commit verified
-> A3 prologue transition
-> host mount eligible
-> authoritative 3D route handoff ready
~~~

A later step never backfills authority into an earlier step.

## 9. Private Authority Ports

A future implementation uses private ports equivalent to:

~~~kotlin
internal interface FirstUserAuthorityPort {
    fun observeValidatedSnapshot(
        observer: (ValidatedFirstUserSnapshot) -> Unit
    ): CloseHandle

    fun reconcileSameOperation(
        cursor: ValidatedFirstUserCursorHandle,
        attempt: PrivateAttemptToken,
        callback: (SameOperationResult) -> Unit
    ): CancelHandle

    fun reconcileLocalProjection(
        receipt: VerifiedTerminalReceiptHandle,
        expectedLocalRevision: LocalProjectionRevisionExpectation,
        attempt: PrivateAttemptToken,
        callback: (ProjectionResult) -> Unit
    ): CancelHandle
}

internal interface FirstUserHostPort {
    fun mount(
        permit: OpaquePrologueHostPermit,
        generation: Long
    )

    fun retryHost(generation: Long)

    fun unmount(generation: Long)
}
~~~

The authenticated principal is captured by FirstUserAuthorityPort. It is not an adapter or Compose parameter.

### 9.1 Deliberate Negative API

The adapter has no method for:

- first-user genesis;
- account, profile, or character creation;
- a fresh server commit;
- key or fingerprint generation;
- raw receipt retrieval;
- local projection application;
- rollback;
- save;
- delete or reset;
- identity lookup;
- appearance-draft commit.

This absence is a security and idempotency property. A future API scan must enforce it.

## 10. Private Input Snapshot

The authority port emits a validated private snapshot equivalent to:

~~~text
ValidatedFirstUserSnapshot
  canonicalState
  cursor
  serverReceiptProof
  localProjectionProof
  sourceEmissionSequence
~~~

The snapshot is an immutable, bounded, non-Parcelable, non-Serializable object. Sensitive wrappers copy their underlying bytes, use content equality, and return redacted text from toString.

The adapter never writes this snapshot to:

- Compose state;
- SavedStateHandle;
- Bundle;
- clipboard;
- accessibility semantics;
- navigation arguments;
- Unity route payload;
- analytics;
- player-visible logs.

## 11. Compose-Facing Contract

Compose receives only a sanitized immutable model equivalent to:

~~~kotlin
data class FirstUserHostPresentation(
    val generation: Long,
    val phase: PresentationPhase,
    val progress: ProgressKind?,
    val retryAvailable: Boolean,
    val canExit: Boolean,
    val supportCode: HostSupportCode?,
    val hostSlot: HostSlotState,
    val handoff: SanitizedHandoffState
)
~~~

Allowed UI events:

~~~text
Retry(generation)
CopySupportCode(generation)
Exit(generation)
PredictiveBackCompleted(generation)
~~~

Compose sends one generic Retry intent. The private reducer selects the only effect legal for the current state. Compose never chooses server reconciliation, projection reconciliation, or host retry directly.

No public DTO, nested property, sealed subtype, callback, or event may carry:

- ProfileId;
- AccountId;
- CharacterId;
- raw server receipt;
- onboardingOperationId or Idempotency-Key;
- semanticRequestFingerprint;
- draftId;
- authoritative or local revision;
- receiptId or commitId;
- terminal receipt locator or digest;
- authenticated-principal data;
- exception, endpoint, path, or raw diagnostic text.

A public-API reflection test must walk the complete property graph and prove these fields and sensitive wrapper types are absent.

## 12. State Mapping

| A3 state | Required cursor and proof | Presentation | Allowed Retry effect | Host |
| --- | --- | --- | --- | --- |
| first_user.commit_pending | PreparedForCommit; no terminal fields; no verified receipt | CommitPending | none while the commit owner is in flight | never mount |
| first_user.commit_recovery | PreparedForCommit; unchanged binding; terminal not verified | CommitRecovery | same-operation reconciliation only | never mount |
| first_user.committed_projection_pending | TerminalReceiptInstalled; server Verified; local Pending | ProjectionPending | local projection reconciliation only | never mount |
| first_user.recovery_required | Typed recovery evidence | RecoveryRequired | only a source-approved exact reconciliation; otherwise none | never mount |
| first_user.commit_verified | TerminalReceiptInstalled; server Verified; local Verified | CommitVerified | none | do not mount yet |
| first_user.prologue_transition | Same verified proofs as commit_verified | PrologueTransition plus sanitized host substate | host-only when host failure is recoverable | mount permitted |

Any mismatch maps to RecoveryRequired. It is never coerced forward.

Examples of invalid combinations:

- TerminalReceiptInstalled without terminalReceiptDigest.
- A required terminal locator is absent.
- A forbidden locator is present.
- Server Verified while the cursor remains PreparedForCommit.
- Local projection Verified before server receipt verification.
- commit_verified or prologue_transition without both proofs.
- A current-sequence regression from terminal to prepared.
- A current-sequence regression from projection Verified to Pending.
- Unknown cursor or commit contract version.
- Retired without approved active semantics.

## 13. Same-Operation Reconciliation

A same-operation reconciliation:

1. uses the current authenticated principal;
2. sends the exact unchanged onboardingOperationId header bytes;
3. sends the exact unchanged semanticRequestFingerprint;
4. uses the expected authoritative revision through the authority owner;
5. never generates a new operation, receipt, commit, account, profile, or character identity.

Typed outcomes:

| Outcome | Adapter behavior |
| --- | --- |
| Verified authoritative receipt | Wait for the authority owner to atomically install terminal evidence and emit a fresh validated snapshot. |
| Exact duplicate receipt/digest/locator | Accept as zero-mutation verification. |
| Still pending | Remain in commit recovery without host access. |
| Transport/offline/session temporarily unavailable | Remain recoverable with the same operation only. |
| Known not found | Do not create or recommit; remain with the commit owner under commit recovery. |
| Principal, key, fingerprint, revision, digest, locator, or semantic conflict | RecoveryRequired; no retry unless the authority owner provides a narrower approved reconciliation. |
| Malformed or unknown outcome | Fail closed. |

A callback result never edits the cursor directly. The authority source publishes a new complete snapshot after durable installation or reconciliation.

## 14. Local Projection Reconciliation

Local projection reconciliation accepts only:

- a VerifiedTerminalReceiptHandle;
- an explicit local revision expectation; and
- one private attempt token.

The Android boundary exposes reconcile/verify, not apply/create.

Typed outcomes:

| Outcome | Adapter behavior |
| --- | --- |
| Pending | Remain in committed projection pending. |
| Exact already-projected duplicate | Accept Verified with zero mutation. |
| Newly verified projection | Wait for a fresh validated source snapshot. |
| Temporary local unavailability | Remain retryable without server genesis. |
| CAS conflict, commit uncertainty, corrupt evidence, or identity conflict | RecoveryRequired. |
| Unknown or malformed result | Fail closed. |

After projection Verified, lifecycle restart may re-query proof but cannot reapply projection. Host retry cannot invoke this port.

## 15. Retry and Exit Rules

The private reducer routes generic Retry exclusively:

| Current state | Effect |
| --- | --- |
| CommitRecovery | reconcileSameOperation |
| ProjectionPending | reconcileLocalProjection |
| Source-approved server recovery | reconcileSameOperation |
| Source-approved projection recovery | reconcileLocalProjection |
| Recoverable host failure during PrologueTransition | retryHost |
| CommitPending, Waiting, CommitVerified, terminal recovery, or retained-uncertain host cleanup | none |

Only one attempt token is active per generation. Repeated taps while one attempt is active are ignored.

Exit and predictive Back completion:

- close presentation and host ownership;
- do not cancel, erase, or roll back the authoritative operation;
- do not delete the terminal receipt or local projection;
- do not create a replacement operation;
- let a later launch resume from a freshly validated source snapshot.

Predictive Back cancellation has no side effect.

## 16. 3D Readiness

The adapter distinguishes mount eligibility from completed handoff.

### 16.1 Host Mount Eligible

All conditions are required:

- A3 state is exactly first_user.prologue_transition;
- cursor phase is TerminalReceiptInstalled;
- terminal digest and locator policy validate;
- immutable server receipt proof is Verified;
- local projection proof is Verified;
- generation is current;
- lifecycle ownership is valid;
- cursor is not Retired.

### 16.2 Three-Dimensional Handoff Ready

All Host Mount Eligible conditions are required, plus:

- the approved native host surface is Active; and
- its authoritative, correlated route handoff state is Ready.

onRouteDispatched proves only that Android sent a message to Unity. It does not prove scene readiness or handoff completion.

The coordinator may emit one identity-free observation:

~~~text
ThreeDHandoffReady(generation)
~~~

It carries no profile, account, character, receipt, operation, fingerprint, digest, locator, or revision data. It grants no save or gameplay authority.

The Unity route payload must not contain sensitive cursor or projection evidence. The bridge resultId is a bridge result identity and must never alias server receiptId or commitId.

## 17. Thread and Lifecycle Contract

### 17.1 Threading

- Authority and host callbacks may arrive on arbitrary threads.
- One injected main-thread dispatcher posts callbacks into one serialized, non-reentrant reducer mailbox.
- UI state and UI-facing effects are delivered on the Android main thread.
- External callbacks are not invoked while reducer state is locked or mid-transition.
- No polling or per-frame work is introduced.

### 17.2 Generations and Attempts

Every adapter instance, authority attempt, host attempt, and UI event carries an in-memory generation or private attempt token.

- A late prior-generation callback is ignored before state or effects change.
- A late prior-attempt callback in the current generation is ignored.
- A same-generation lower revision or illegal proof regression fails closed.
- Generation values are presentation correlation only and contain no identity.
- Generation and private attempt state are not durable authority.

### 17.3 Lifecycle

STARTED:

- attach one source subscription;
- request one complete current snapshot;
- perform no automatic commit or projection mutation.

STOPPED:

- detach presentation observation and cancel adapter-owned wait handles;
- delegate host pause/unmount behavior to the approved host lifecycle owner;
- preserve durable operation, cursor, receipt, and projection evidence;
- do not demote verified proof.

DESTROYED:

- close subscriptions;
- invalidate the generation;
- suppress late callbacks;
- delegate host teardown to the approved host owner;
- do not clear durable authority evidence.

Activity recreation:

- the old generation closes;
- the new generation re-queries the validated source;
- the process-wide host registry remains the one-player ownership guard.

Process death:

- no sensitive adapter state is restored from Bundle or SavedState;
- cursor, receipt, and projection owners reload and validate durable evidence;
- Android receives new opaque handles;
- no operation or key is invented.

Lifecycle stop/start, configuration change, reconnect, or host retry cannot trigger server genesis or projection replay.

## 18. Support-Code Privacy

Normal pending and verified states have no support code.

| Code | Meaning | Retry |
| --- | --- | --- |
| ALH-4101 | Same-operation reconciliation temporarily unavailable, offline, or authenticated session unavailable | only when source allows |
| ALH-4102 | Principal, header, fingerprint, or same-operation conflict | terminal |
| ALH-4201 | Local projection reconciliation temporarily unavailable | source-approved projection retry |
| ALH-4202 | Local projection uncertain or recovery required | no host |
| ALH-4301 | Cursor schema, version, phase, invariant, or unsupported Retired state | terminal |
| ALH-4302 | Terminal receipt proof, digest, or locator invalid | terminal |
| ALH-4303 | Authoritative/local revision conflict or illegal current-generation regression | terminal |
| ALH-4401 | 3D mount or handoff proof invariant not satisfied | fail closed |
| ALH-4001 | Unknown committed-handoff failure fallback | fail closed |

Existing host ALH-1xxx, ALH-2xxx, and ALH-3xxx mappings remain unchanged.

Only the ALH code is visible, copyable, or player-loggable. Never append:

- source diagnostics;
- identity;
- operation, key, or fingerprint;
- revision;
- receipt, locator, or digest;
- exception, class, method, endpoint, or path;
- principal or account facts.

Copy Support Code copies only the stable code.

## 19. Accessibility and Presentation Boundary

This adapter does not define visual design or final player copy. It supplies typed phases and support codes to the separately approved host/recovery surface.

The public model supports:

- one merged pending/progress announcement;
- one failure announcement;
- generic Retry, Copy Support Code, and Exit actions;
- predictable focus restoration after retry;
- no sensitive data in semantics, test tags, content descriptions, clipboard, or accessibility events;
- no timeout or automatic dismissal;
- reduced-motion presentation;
- the approved 48 dp target, safe-area, IME, 200-percent text, TalkBack, Switch Access, and predictive-Back rules from the host surface contract.

Active Unity gameplay accessibility remains a separate responsibility. The adapter cannot represent an inaccessible Unity scene as accessible merely because the native recovery overlay is accessible.

## 20. Required Future Tests

### 20.1 JVM Contract and Reducer

- all six canonical A3 state rows;
- every illegal state/proof cross-product;
- Unspecified and unknown phase/version rejection;
- Prepared cursor polluted by terminal fields;
- Terminal cursor missing or mismatching digest;
- Required versus NotUsed locator policy;
- explicit Applicable versus NotApplicable local revision;
- exact key-octet equality;
- no trim, case, normalization, encoding, or JSON transformation;
- unchanged opaque semantic fingerprint;
- no ProfileId parsing or fingerprint-field dependence;
- duplicate, pending, not-found, offline, principal-conflict, and semantic-conflict server results;
- pending, duplicate, verified, CAS-conflict, uncertain, and corrupt projection results;
- exclusive Retry routing;
- double-tap suppression;
- stale generation and stale attempt callbacks;
- source reordering and illegal current-sequence regression;
- mount eligibility versus completed handoff readiness;
- onRouteDispatched non-readiness;
- exhaustive support-code mapping;
- unknown failure fallback;
- public API and reflection graph with zero sensitive type/property exposure;
- redacted sensitive toString and log capture;
- no Parcelable, Serializable, SavedState, Bundle, navigation, or JSON serializer for sensitive wrappers;
- compile/API scan proving no genesis, commit, project, rollback, delete, or reset method.

### 20.2 Instrumentation and Fault Injection

- off-main authority callback to main-thread presentation;
- off-main host callback to main-thread presentation;
- START, STOP, DESTROY;
- Activity recreation;
- fake durable-source process reconstruction;
- late callback after close;
- reconnect;
- predictive Back completion and cancellation;
- concurrent Retry and Exit;
- one source subscription per active generation;
- one host permit/player owner;
- no sensitive value in Compose semantics, screenshot/test dump, SavedState, Bundle, clipboard, logcat, or accessibility nodes;
- host failure/retry invokes no authority port;
- projection retry invokes no server genesis;
- same-operation retry does not project twice;
- API 24 and API 35/36 debug/release/R8 smoke.

Fake-port evidence does not satisfy live backend, production route, packaged round-trip, physical-device, visual, player, milestone, or release acceptance.

## 21. Future Engineering Allowlist

A later dormant engineering child may add only these new files after exact A1 authorization:

- app/src/main/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryContract.kt
- app/src/main/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryReducer.kt
- app/src/main/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryAdapter.kt
- app/src/main/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryLifecycleBinding.kt
- app/src/test/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryContractTest.kt
- app/src/test/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryReducerTest.kt
- app/src/test/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryAdapterTest.kt
- app/src/androidTest/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryLifecycleTest.kt
- app/src/androidTest/java/com/example/anotherlife/ui/unity/FirstUserHostBoundaryPrivacyTest.kt

That child must:

- remain dormant;
- use fake private ports;
- add no dependency or asset;
- avoid every existing source/resource file;
- avoid MainActivity, routes, and production mounting;
- wait for A1 acceptance of the A6 executable machine contract;
- publish separately as an engineering draft PR;
- request A1 and authorized co-developer review.

This allowlist is not implementation authorization.

## 22. No-Touch List

Do not change under this planning contract:

- MainActivity;
- AdaptiveShell, navigation, or route registration;
- UnityView, UnityBridgeContract, UnityBridgeSession, or UnityHostLifecycle;
- Gradle, settings, manifest, themes, strings, or Android resources;
- Unity sender, receiver, scenes, prefabs, Build Settings, or generated exports;
- backend, authentication, account, profile, character, or transport implementation;
- local save schema, migration, projection, or persistence;
- Bootloader.cs;
- SaveGameData.cs;
- LocalGameDataService.cs;
- ProjectInitializer.cs;
- #366 or #371;
- Figma, visual assets, narrative source, terrestrial source, or Meshy credits;
- production route, packaging, or release state.

## 23. Validation for This Planning Publication

The focused documentation PR must prove:

- exact base is main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0;
- exactly this one new path is changed;
- the six A3 identifiers match the approved handoff exactly;
- Context7 cursor field names and phases match the approved handoff exactly;
- no partial A6 executable DTO or ledger naming is treated as authority;
- no appearance draft field becomes authoritative;
- no sensitive sample identity or raw receipt appears;
- no runtime, production, visual, player, or release claim appears;
- Markdown structure, links, tables, and code blocks are valid;
- git diff whitespace checks pass;
- repository classify and hygiene checks pass;
- hosted policy, classify, hygiene, and applicability gates reach terminal state;
- @rslee94 review is requested.

Android/Unity compile, runtime, device, backend, visual, and player checks are not applicable to this one-file planning change and must be reported as unperformed.

## 24. Completion Boundary

This document is complete when:

- the exact one-file draft PR is published;
- issue #135 and status PR #463 are linked;
- @rslee94 review is requested;
- A1 records exact-head disposition;
- every approval limit remains explicit.

Merging this document, if later approved, records a coordination contract only. It does not activate the Android adapter, the partial A6 executable machine contract, local projection, a production Unity route, backend/profile behavior, visual design, player acceptance, milestone acceptance, or release.
