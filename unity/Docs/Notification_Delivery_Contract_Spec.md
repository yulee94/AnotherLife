# Typed Notification Delivery, Localization, and Acknowledgement Specification

**Status date:** 2026-07-16  
**Tracking issue:** #177  
**Specification owner:** GPT  
**Contract/queue/presentation owner:** Codex engineering mode  
**Notification content/localization owner:** Codex narrative/content mode  
**Final player-facing/product approval:** user  
**Audited baseline:** `c1be5373d50016441b3a2d92acda9eb08761e07c`  
**Validated Unity target:** `2022.3.62f3`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`  
**Catalog authority:** `unity/Docs/Game_Data_Catalog_Authority_Spec.md`  
**Persistence authority:** `unity/Docs/Save_Semantic_Compatibility_Policy.md` and issue #137

## 1. Goal

Create a typed, localization-ready, deterministic notification pipeline that distinguishes:

```text
an authoritative domain outcome
→ a notification request accepted by the queue
→ content successfully resolved
→ a presenter becoming available
→ visible presentation
→ acknowledgement/dismissal/expiry
→ optional durable outbox/history state
```

The pipeline must never claim that a message was delivered merely because a technical service called `Debug.Log`, formatted a string, or successfully enqueued a request.

The first implementation is intentionally phased:

```text
session contract/queue and receipts
→ approved notification-definition/content source
→ visible per-scene presentation
→ durable outbox/history after #137
→ focused caller migrations after their owning transactions are safe
```

This specification does not author player-facing copy, replace domain result contracts, redesign the complete HUD, add push/cloud notifications, or make a notification compensate for a failed save/reward transaction.

## 2. Binding decisions

1. **Domain outcome and notification delivery are separate contracts.** A reward/save/world-state operation succeeds or fails on its own authoritative result; notification state cannot redefine it.
2. **The low-level notification API accepts a stable definition ID and typed parameters, not arbitrary player-facing strings.**
3. **Severity, category, channel, acknowledgement policy, durability, parameter schema, and localization/content reference belong to an immutable notification definition.** Callers do not invent these ad hoc.
4. **Player-facing notification copy, tone, localization keys, and authored action labels belong to Codex narrative/content mode and final user approval where required.**
5. **Codex engineering owns technical IDs, schema, queue, deduplication, lifecycle, safe parameter formatting, presentation mechanics, persistence adapter, accessibility mechanics, and tests.**
6. **Technical diagnostic codes remain separate from notification definition IDs and player copy.**
7. **Publish acceptance is not visible delivery.** The enqueue result and delivery receipt are different immutable results.
8. **A console log is a diagnostic fallback only.** It never produces `Presented`, `Acknowledged`, or `Delivered` status.
9. **Current raw-string methods remain only as temporary compatibility wrappers.** They do not become the authoritative API and may not be used by new callers.
10. **No arbitrary Unity rich text enters the typed pipeline.** Definition content and parameters are escaped/validated according to the presentation channel.
11. **No arbitrary scene name, URL, object reference, or delegate enters a notification action.** Actions use registered typed action IDs and validated payloads.
12. **Correlation/idempotency is mandatory for blocking, durable, transaction-result, recovery, reward, world-state, and integration notifications.**
13. **Duplicate correlation delivery is deterministic.** The same request is deduplicated; conflicting payload for the same correlation is rejected visibly.
14. **Queue state survives presenter absence and scene transitions.** A presenter is a consumer, not the authoritative queue owner.
15. **Exactly one active presenter registration exists per presentation capability/channel.** Duplicate registration fails visibly.
16. **Presentation uses realtime/unscaled timing.** `Time.timeScale == 0` cannot freeze expiry, dismissal, or acknowledgement mechanics unexpectedly.
17. **Blocking errors never auto-expire.** They remain pending/presented until acknowledged, superseded by an explicit related outcome, or removed through a reviewed recovery rule.
18. **Transient informational items may expire according to their approved presentation definition.** Expiry does not imply acknowledgement.
19. **A queue-capacity event cannot silently discard a blocking item.** Critical overflow returns a rejection and emits a stable high-severity diagnostic.
20. **The first queue is session-only.** It can be implemented before #137 and does not edit save data.
21. **Durable outbox/history is a later adapter after #137.** Any `SaveGameData.cs` edit requires the shared-file lock and full migration/deletion/fault evidence.
22. **Durable transaction notifications are persisted with the owning transaction/outbox, not appended after a successful domain save with no recovery path.**
23. **Low-level economy, save, resource, catalog, or validation services do not directly format success messages.** The owning orchestrator publishes after a committed result.
24. **Notification failure does not roll back an already committed domain result.** It returns a separate notification failure/receipt and preserves any durable outbox entry.
25. **Notification success never hides a failed domain result.** A failure request uses a definition that matches the actual authoritative failure.
26. **Missing localization/content is a typed resolution failure.** Development may show an approved technical fallback; release never exposes raw keys, stack traces, file paths, or internal IDs as normal player copy.
27. **Every definition has an explicit parameter schema.** Unknown, missing, wrong-type, non-finite, oversized, or unsafe parameters reject the request before queue mutation.
28. **String parameters are data, not markup.** They are escaped and bounded. Raw player names or private identifiers are not persisted unless a separate privacy/content decision permits it.
29. **A persisted notification stores definition ID/version and typed parameters, not rendered localized text.** It can re-render in the current locale while preserving semantic identity.
30. **Definition-version migration is explicit.** A removed/unsupported definition cannot silently render unrelated fallback content.
31. **Queue/history retention is bounded and deterministic.** Debug logs are not copied into player history.
32. **Accessibility is part of the delivery contract.** Severity is not color-only; reduced motion, focus, input, text scaling, safe areas, and platform announcement capability are tested.
33. **Presentation may coexist with combat.** Nonblocking toasts/banners cannot obscure critical controls/telegraphs; blocking modals appear only for outcomes whose definition requires acknowledgement.
34. **The UI presenter does not own domain state, save state, localization source, or notification definition authority.**
35. **The notification service does not use `DontDestroyOnLoad` as a hidden lifecycle workaround.** The queue service follows the accepted service-stack lifecycle; scene presenters attach/detach through registrations.
36. **No `Bootloader.cs` edit occurs while PR #203 holds its lock.** The first contract/queue PR retains constructor compatibility and requires only later rebase of PR #203.
37. **The visible presenter follows committed production scenes after #223.** It does not promote `Assets/Test.unity` or depend on test-only Build Settings.
38. **#150 Player smoke validates visible/queued delivery only after the presenter phase exists.** It is not required for the first pure queue PR.
39. **#183 provides the versioned notification-definition/content catalog envelope when that implementation phase is available.** Before then, tests may use an injected immutable fake resolver, not a hidden production hard-coded catalog.
40. **No direct Android bridge/push implementation is included.** #135 later maps bridge outcomes into the same typed request contract.
41. **User approval is not inferred from technical presentation.** Narrative tone/copy and integrated player experience remain user-gated where required.

## 3. Verified current baseline

### 3.1 Current interface

```csharp
public interface INotificationService
{
    void ShowMessage(string message);
    void ShowError(string error);
    void ShowResourceGain(ResourceType type, long amount);
}
```

Risks:

- raw strings cross the technical/presentation boundary;
- methods return no acceptance or delivery result;
- no definition, severity, category, correlation, timestamp, expiry, durability, action, or parameter validation exists;
- no queue or history exists;
- arbitrary rich text can be passed;
- `ShowResourceGain` combines domain data with presentation formatting;
- callers cannot distinguish no presenter, queue rejection, content failure, or visible delivery.

### 3.2 Current implementation

`LocalNotificationService` writes:

```text
[Notification] <message>
[Notification] <error>
[Notification] +<amount> <resource>
```

to the Unity Console through `Debug.Log`/`Debug.LogWarning`.

There is no runtime presentation, acknowledgement, queue, deduplication, localization resolver, accessibility announcement, persistence, or delivery receipt.

### 3.3 Verified injected publishers

Current `Bootloader` constructs one `LocalNotificationService` and injects it into:

```text
WorldStateService
LocalBossLootService
```

It also registers the service as `INotificationService`.

No current verified publisher calls `ShowResourceGain`; the method remains unused compatibility surface until a complete repository inventory proves otherwise.

### 3.4 `WorldStateService`

Current order:

```text
mutate ActiveEventId / CurrentEffect
→ format raw rich-text English by enum
→ ShowMessage(raw text)
→ invoke OnWorldStateChanged
```

Problems:

- technical runtime is a competing narrative/localization source;
- notification failure can interrupt the subsequent event callback;
- duration/persistence/commit are absent;
- an announcement can claim an effect that is not consumed by gameplay;
- there is no correlation/revision/deduplication identity;
- no acknowledgement or visible delivery exists.

Migration waits for #172. The world-state transaction publishes only after validated/persisted state transition and uses the committed event/revision correlation ID.

### 3.5 `LocalBossLootService`

Current order:

```text
award Warzone Credits (nested save)
→ roll/mutate equipment
→ conditional save
→ format raw English with boss/item/player names
→ ShowMessage(...)
```

Problems:

- credits/equipment may be partial before notification;
- invalid requests fabricate fallback reward data;
- duplicate requests can repeat rewards and messages;
- notification claims success without one durable committed result;
- raw player display name may enter logs/player copy without privacy classification;
- technical service owns player-facing strings.

Migration waits for #168. The committed reward orchestrator publishes one result notification after the reward/application ledger and save succeed, using the stable result/correlation ID.

### 3.6 Future required publishers

| Outcome owner | Example notification need | Publication rule |
| --- | --- | --- |
| #137 save/recovery | recovered backup, read-only/degraded profile, unrecoverable reset, save failure | durable outbox/recovery result after candidate selection/install; blocking/recoverable severity as mapped |
| #163 economy and callers | invalid request, insufficient balance, committed gain/spend | low-level service returns typed result; owning UI/orchestrator decides whether to publish |
| #168 boss loot | committed loot/credits, no-loot, application failure | publish after one durable result; duplicate result one receipt |
| #169 Realm Gem/Wishgate | entitlement earned/consumed, invalid claim, commit failure | publish from owning committed transaction, not raw service void call |
| #172 world state | start/end/cancel/unavailable effect | publish after committed revision; no hard-coded service copy |
| #176 relationships | committed affinity/faction/persona change and unavailable/malformed result | publish from owning transaction/presentation layer |
| #133/#134 NVS-01 | hook unavailable, retry, report consequence, commit failure/success | stable semantic result/correlation and approved source content |
| #183 catalogs | required catalog unavailable/unsupported | blocking technical definition with approved player fallback; no raw catalog/path detail |
| #150 scenes/Player | required feature/scene unavailable | only after a runtime product outcome; build logs are not player notifications |
| #135 bridge | host/session/route/result unavailable | bridge result maps to a predefined notification definition |

## 4. Ownership and authority model

### 4.1 Technical notification definition

Codex engineering owns equivalent fields:

```text
definitionId
schemaVersion
contentVersion
severity
category
defaultChannel
allowedChannels
priority
acknowledgementPolicy
durabilityPolicy
deduplicationPolicy
expiryPolicy
parameterSchema
actionSchema
privacyClass
sourceSystems
```

Codex coordination/review mode reviews outcome-to-definition mapping and failure/transaction semantics.

### 4.2 Player-facing content

Codex narrative/content owns:

```text
localization key(s)
player-facing title/body/action label
tone
plural/gender/select rules
approved emphasis semantics
long-form/short-form variants
```

The content source references the technical `definitionId` and parameter names. Engineering cannot author replacement story/tone when content is missing.

### 4.3 User approval

The user approves:

- unresolved player-facing tone/product decisions;
- blocking modal experience when product-sensitive;
- notification density/priority changes that materially affect play;
- final integrated accessibility/player experience;
- release acceptance.

### 4.4 Catalog relationship

After the #183 catalog foundation, notification definitions/content use its manifest/envelope/version/hash/provenance rules.

Before that implementation is ready, the queue accepts an injected immutable `INotificationDefinitionResolver` in tests and development infrastructure. Production does not silently fall back to an unversioned hard-coded dictionary and call it catalog authority.

## 5. Stable identifiers

### 5.1 Definition IDs

New IDs use:

```text
^al_notify_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

Examples of technical identity only:

```text
al_notify_save_recovered_backup
al_notify_save_profile_degraded
al_notify_save_unrecoverable
al_notify_operation_unavailable
al_notify_reward_committed
al_notify_reward_failed
al_notify_world_event_started
al_notify_world_event_ended
al_notify_bridge_unavailable
al_notify_catalog_unavailable
al_notify_content_unavailable
```

These IDs do not contain final player copy.

### 5.2 Source-system IDs

New source IDs use:

```text
^al_source_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

Examples:

```text
al_source_save
al_source_boss_loot
al_source_world_state
al_source_nvs
al_source_bridge
```

### 5.3 Correlation IDs

A correlation ID is an opaque bounded string generated by the owning transaction/orchestrator.

Rules:

- nonblank when required by the definition;
- ordinal case-sensitive;
- maximum UTF-8 byte length: 128;
- no local path, email, access token, raw player name, exception, or secret;
- stable for retries/replay of the same semantic outcome;
- distinct for genuinely distinct outcomes;
- not generated by presentation time or random UI state.

Recommended structure:

```text
<domain>:<stable result/event ID>:<revision>
```

The structure is diagnostic convention, not parser authority.

### 5.4 Instance IDs and sequence

The queue assigns:

```text
notificationInstanceId
sessionSequence
```

only after validation/deduplication acceptance.

- `notificationInstanceId` is unique within the session/persistent outbox;
- `sessionSequence` is monotonic for deterministic ordering;
- neither replaces the semantic correlation ID.

## 6. Definition contract

Names may vary, but immutable definition data must be equivalent.

### 6.1 Severity

```text
Information
Success
Warning
RecoverableError
BlockingError
```

Severity is technical outcome importance, not arbitrary visual color.

### 6.2 Category

Minimum categories:

```text
System
SaveRecovery
ContentAvailability
Economy
Progression
Reward
WorldState
Integration
Connectivity
```

Additional categories require schema review; callers cannot supply free-form category strings.

### 6.3 Channel

```text
Toast
Banner
Acknowledgement
HistoryOnly
```

Definitions specify a default and allowed channels.

- `Toast` — transient nonblocking presentation;
- `Banner` — visible until expiry/dismissal according to definition;
- `Acknowledgement` — blocking/critical presentation requiring explicit user acknowledgement;
- `HistoryOnly` — no immediate visual interruption, retained when durability supports it.

A caller may request one allowed channel only when its use case needs a narrower approved presentation. It cannot escalate itself to `Acknowledgement` if the definition disallows it.

### 6.4 Acknowledgement policy

```text
None
Dismissible
Required
```

`BlockingError` requires `Required` unless the definition documents another reviewed recovery interaction.

### 6.5 Durability policy

```text
SessionTransient
SessionUntilAcknowledged
DurableUntilAcknowledged
DurableHistory
```

The first implementation supports only the session policies. Durable definitions are recognized but return `RejectedDurabilityUnavailable` until the #137 adapter exists, unless a caller uses an owning durable outbox already specified by its transaction.

### 6.6 Expiry policy

Equivalent fields:

```text
expiryMode: None | AfterPresentation | AfterOccurrence
realtimeDurationSeconds
expireWhilePresenterUnavailable
```

Rules:

- finite, nonnegative bounded duration;
- `Required` acknowledgement cannot expire automatically;
- duration comes from the approved definition/presentation profile, not a caller;
- expiry uses injected realtime clock;
- expired means no longer presentable, not acknowledged.

### 6.7 Priority

Use bounded integer priority:

```text
0..100
```

The definition supplies it. Same-priority ordering uses session sequence.

Priority does not allow a success toast to displace a blocking recovery message.

### 6.8 Deduplication policy

```text
None
ByCorrelation
ByCorrelationAndDefinition
ReplaceEarlierCorrelation
```

Transaction/recovery/reward/world-state definitions use correlation-based policy.

### 6.9 Privacy class

```text
PublicGameplay
ProfilePrivate
SensitiveTechnical
```

- `SensitiveTechnical` values never enter player content/history;
- persistent records may include only parameters allowed by the definition privacy schema;
- raw exceptions/paths are always diagnostic-only.

## 7. Typed parameter contract

### 7.1 Allowed value kinds

Equivalent immutable kinds:

```text
Int64
UInt64
DecimalString
Boolean
StableId
LocalizationReference
ResourceType
RealmId
TimestampUtc
DurationSeconds
SafeDisplayText
```

`SafeDisplayText` is exceptional and definition-gated. It is bounded, escaped, nonpersistent by default, and cannot contain markup.

Floating-point values are not passed raw to presentation. A domain formats an exact finite semantic value through a reviewed numeric parameter representation/profile.

### 7.2 Parameter schema

Each definition declares for every parameter:

```text
name
valueKind
required
maximumLength
minimum/maximum when numeric
allowedEnum/ID family
persistable
privacyClass
```

Rules:

- parameter names are unique lower snake case;
- unknown parameter rejects the request;
- missing required parameter rejects;
- wrong type rejects;
- non-finite/overflowed input rejects;
- string values are Unicode-normalized according to one documented form and bounded by UTF-8 bytes/code points;
- markup/control characters are escaped or rejected;
- no parameter is formatted with current culture before resolution;
- resolver applies locale-aware formatting from typed values.

### 7.3 Player name/privacy

A raw player display name is not used in a durable notification by default.

World/global announcements require a separately approved privacy/content contract. Current `LocalBossLootService` fallback `"Anonymous player"` and direct `PlayerDisplayName` interpolation are not retained as technical behavior.

## 8. Request contract

Equivalent immutable request:

```text
definitionId
sourceSystemId
correlationId
occurredAtUtc
parameters
requestedChannel or null
subjectReference or null
originDiagnosticCode or null
```

Rules:

- request contains no rendered title/body;
- timestamp comes from injected UTC clock;
- future/past bounds are validated;
- subject reference is an opaque validated stable ID, not a live Unity object;
- origin diagnostic is technical and never shown directly;
- definition resolution and parameter validation complete before queue mutation.

## 9. Enqueue result contract

Minimum statuses:

```text
AcceptedPending
AcceptedAlreadyPresent
AcceptedReplacedEarlier
RejectedServiceUnavailable
RejectedDefinitionUnavailable
RejectedUnsupportedDefinitionVersion
RejectedInvalidRequest
RejectedUnsafeParameter
RejectedCorrelationRequired
RejectedCorrelationConflict
RejectedDurabilityUnavailable
RejectedCapacity
RejectedPresenterPolicy
```

Required result data:

```text
status
definitionId
correlationId
notificationInstanceId when accepted
sessionSequence when accepted
existingInstanceId when deduplicated/replaced
diagnosticCode
queueChanged
```

No status is named `Delivered`.

## 10. Delivery receipt/state contract

Minimum states:

```text
PendingPresenter
PendingPresentation
Presented
Acknowledged
Dismissed
Expired
Superseded
DeliveryFailed
PersistencePending
PersistenceFailed
```

A receipt contains:

```text
notificationInstanceId
state
presenterId/channel when applicable
presentedAtUtc
completedAtUtc
deliveryAttempt
failureCode
```

Rules:

- state transitions are monotonic according to one state machine;
- `Presented` means the presenter confirmed visible attachment/render readiness;
- `Acknowledged` means explicit accepted user action;
- `Dismissed` is not acknowledgement when acknowledgement is required;
- `DeliveryFailed` retains a required notification for retry/persistence;
- console fallback never advances beyond `PendingPresenter`/`DeliveryFailed`;
- observers receive immutable state snapshots and cannot mutate queue records.

## 11. Queue state machine

### 11.1 Notification lifecycle

```text
Validated
→ PendingPresenter
→ PendingPresentation
→ Presented
→ Acknowledged | Dismissed | Expired | Superseded
```

Failures:

```text
PendingPresenter/Presentation
→ DeliveryFailed
→ retry PendingPresenter/Presentation
```

Durable adapter adds:

```text
PersistencePending
→ persisted pending/presented/acknowledged state
→ PersistenceFailed with retained recovery evidence
```

### 11.2 Session queue capacity

Initial exact capacity:

```text
64 active/pending session records
```

This is technical safety, not a player-visible density target.

When capacity is reached:

1. remove records already completed and outside any session history requirement;
2. remove expired transient records;
3. evict the oldest lowest-priority `SessionTransient` record only when its definition allows capacity eviction;
4. never evict `Required` acknowledgement, `BlockingError`, or session-until-acknowledged records;
5. if no safe eviction exists, reject the new request with `RejectedCapacity` and diagnostic `AL-NTF-CAPACITY`;
6. critical rejection emits one high-severity technical diagnostic and remains visible to the caller result.

### 11.3 Presentation concurrency

Default capabilities:

```text
one active Acknowledgement
one active Banner
one active Toast
```

Additional queued items wait by severity/priority/sequence.

No definition may open multiple blocking modals for the same correlation.

### 11.4 Ordering

Order by:

1. blocking/required acknowledgement;
2. severity;
3. definition priority descending;
4. occurrence time ascending;
5. session sequence ascending.

A newer low-severity item cannot starve an older recoverable error.

## 12. Deduplication and conflict semantics

### 12.1 Exact replay

When definition ID, correlation ID, source, and canonical typed parameters match an existing non-superseded record:

```text
AcceptedAlreadyPresent
```

Queue/history/presentation does not duplicate.

### 12.2 Correlation conflict

Same correlation but different definition/semantic parameter payload, when the policy does not explicitly replace:

```text
RejectedCorrelationConflict
AL-NTF-CORRELATION-CONFLICT
```

Existing record remains unchanged.

### 12.3 Replacement

`ReplaceEarlierCorrelation` is used only for explicit state progression such as:

```text
operation pending → operation succeeded/failed
world event started → ended/cancelled
content unavailable → restored
```

Definition metadata declares allowed predecessor/successor IDs.

Replacement:

- preserves audit linkage;
- marks earlier item `Superseded`;
- cannot silently replace an unacknowledged blocking error with a lower severity;
- uses the same semantic correlation family;
- produces one deterministic receipt transition.

### 12.4 Reload replay

After durable history exists, a replayed correlation returns the existing durable record/receipt. It does not append another notification or reannounce unless the definition explicitly allows a new delivery attempt after process restart.

## 13. Definition and content resolution

### 13.1 Definition resolver

```text
INotificationDefinitionResolver.Resolve(definitionId)
```

returns typed status:

```text
Found
UnknownId
CatalogPending
CatalogUnavailable
InvalidDefinition
UnsupportedVersion
```

The queue does not accept a request without a valid definition.

### 13.2 Content resolver

The presenter uses equivalent:

```text
INotificationContentResolver.Resolve(definition, parameters, locale, presentationVariant)
```

Result:

```text
Resolved
MissingContentKey
InvalidPlaceholderSchema
UnsafeRenderedContent
UnsupportedLocale
ContentCatalogUnavailable
```

Resolved output is immutable and contains only safe presentation fields:

```text
title
body
icon/reference
severity label/reference
action labels
accessibility announcement
```

### 13.3 Missing content

Development:

- emit `AL-NTF-CONTENT-MISSING` with definition ID;
- optionally present the approved generic technical fallback definition `al_notify_content_unavailable`;
- never interpolate the raw key into player copy as if localized.

Release:

- use only an approved localized generic fallback definition if available;
- otherwise return `DeliveryFailed` and retain required/durable notification;
- log the technical ID without raw private parameters;
- do not show raw internal key/path/stack trace.

### 13.4 Markup

Definitions may declare approved emphasis tokens interpreted by the presenter, but content files do not inject arbitrary Unity rich text.

Parameter values are always escaped. A parameter cannot open/close color, size, link, sprite, or other markup tags.

## 14. Presenter registration and scene lifecycle

### 14.1 Registration

Equivalent API:

```text
RegisterPresenter(presenter, capabilities) → PresenterRegistrationResult/token
UnregisterPresenter(token)
```

Statuses:

```text
Registered
RejectedDuplicateCapability
RejectedInvalidPresenter
RejectedServiceUnavailable
AlreadyUnregistered
```

Exactly one active presenter owns each capability set in the initial implementation.

### 14.2 Presenter lifecycle

- presenter attaches after its UI hierarchy is ready;
- service immediately offers pending compatible records;
- presenter unregisters in disable/destroy/scene unload;
- pending required records remain in queue;
- new scene presenter continues delivery;
- stale/late presenter callbacks are rejected by registration token/generation;
- presenter failure cannot corrupt queue state;
- duplicate presenters fail visibly instead of both rendering.

### 14.3 Production scenes

Visible presentation follows #223 committed production scenes and the #150 ShellFoundation profile.

A later presenter integration PR may add one presenter host or validated runtime-created presenter per production scene. It must:

- keep queue authority in the service;
- not edit `Bootloader.cs` while locked;
- not add Test to Build Settings;
- prove Boot/RealmSelection/Kingdom handoff;
- retain pending blocking notification through scene transitions;
- prove no duplicate UI root/presenter.

### 14.4 Test scene

`Assets/Test.unity` may host a test presenter only for safe #127 PlayMode tests. It is not production UI authority.

## 15. Presentation mechanics

### 15.1 Toast

- nonblocking;
- one active initially;
- definition-controlled realtime expiry;
- no required acknowledgement;
- queue resumes after scene presenter attachment;
- safe-area aware and does not cover core controls/telegraphs.

### 15.2 Banner

- persists according to definition/dismiss policy;
- supports dismissal through keyboard/controller/touch;
- may remain across presenter reattachment;
- uses icon/text/severity label, not color alone.

### 15.3 Acknowledgement

- used only by definitions requiring acknowledgement;
- focus is moved deterministically to the acknowledgement UI;
- background actions are blocked only as specified;
- dismissal/back input cannot bypass required acknowledgement;
- scene unload cannot silently clear the record;
- action/ack result is returned to the queue.

### 15.4 History

Initial session history is an immutable diagnostic/query view over accepted records, not a complete player inbox UI.

Durable player history arrives only after #137 and source/content approval. It has bounded retention and explicit UI later.

## 16. Typed notification actions

### 16.1 Action definition

Equivalent fields:

```text
actionId
actionLabelContentKey
actionKind
payloadSchema
allowedNotificationDefinitions
requiresAcknowledgement
```

Action kinds may include:

```text
Acknowledge
RetryOperation
OpenApprovedRoute
OpenRecoveryDetails
Dismiss
```

No action contains raw delegates, URLs, scene names, or object references.

### 16.2 Action registry

Engineering provides an injected registry that maps approved action IDs to typed handlers owned by the relevant system.

Invocation result:

```text
Applied
NoChange
RejectedUnavailable
RejectedInvalidPayload
RejectedStaleCorrelation
Failed
```

Rules:

- validate the notification is current/presented;
- validate action allowed by definition;
- validate payload and ownership;
- handler exception is isolated and returns failure;
- failed action does not acknowledge unless definition explicitly says acknowledgement is independent;
- navigation uses approved route IDs, not arbitrary scene strings;
- action labels remain narrative/localization source.

## 17. Technical logging, diagnostics, and privacy

Suggested stable diagnostics:

```text
AL-NTF-DEFINITION
AL-NTF-PARAMETER
AL-NTF-CORRELATION-REQUIRED
AL-NTF-CORRELATION-CONFLICT
AL-NTF-CAPACITY
AL-NTF-PRESENTER-DUPLICATE
AL-NTF-PRESENTER-FAILED
AL-NTF-CONTENT-MISSING
AL-NTF-CONTENT-UNSAFE
AL-NTF-ACTION
AL-NTF-PERSISTENCE
AL-NTF-LEGACY-RAW
```

Rules:

- logs include technical definition/correlation IDs only when privacy-safe;
- raw exception/stack/path appears only in protected technical logs, not player content/history;
- repeated identical queue/content/presenter failure is rate-limited by service revision/correlation;
- no diagnostic says “delivered” unless a presenter receipt reached `Presented`;
- console fallback identifies itself as fallback;
- development logging never mutates receipt state.

## 18. Legacy wrapper policy

Retain for compile compatibility:

```text
ShowMessage(string)
ShowError(string)
ShowResourceGain(ResourceType,long)
```

During Phase B:

- wrappers emit `AL-NTF-LEGACY-RAW`;
- they may log escaped technical text in development only;
- they return no authoritative delivery status and are not used by new code;
- they do not enter durable history;
- they do not accept rich text as trusted markup;
- caller inventory remains visible;
- mark obsolete without turning current warnings into build failure.

Remove/migrate wrappers only after verified callers move:

```text
WorldStateService → #172
LocalBossLootService → #168
ShowResourceGain → remove if inventory confirms no caller
```

No broad caller migration belongs in the first contract/queue PR.

## 19. Persistence and outbox after #137

### 19.1 Persisted semantic record

Equivalent fields:

```text
recordId
notificationSchemaVersion
definitionId
definitionVersion
sourceSystemId
correlationId
occurredAtUtc
parameters
state
acknowledgedAtUtc
dismissedAtUtc
expiresAtUtc
lastDeliveryAttemptUtc
deliveryAttemptCount
supersededByRecordId
```

Rendered text, icon object, stack trace, local path, and live action handler are not persisted.

### 19.2 Retention

Initial proposed bound:

```text
100 completed durable history records
plus all unacknowledged durable required records
```

The final player-visible retention/product experience remains user-reviewable. Engineering must:

- prune only completed records;
- order pruning deterministically oldest first;
- never prune unacknowledged blocking/recovery records merely to hit count;
- expose overflow/failure visibly;
- include all notification artifacts in full profile deletion under #137.

### 19.3 Transactional outbox

For a domain result requiring guaranteed eventual notification:

```text
validate/stage domain result
→ stage notification outbox record with same correlation
→ persist domain + outbox atomically/recoverably
→ publish committed state
→ presenter attempts delivery
→ persist acknowledgement/delivery state as required
```

If outbox persistence fails, the domain transaction follows its owning failure contract; it cannot claim a fully committed result when visible delivery is mandatory to the product contract.

For nonmandatory informational notifications, domain commit may succeed even if enqueue/presentation fails, but the returned result reports notification failure separately.

### 19.4 Save recovery notifications

#137 maps candidate outcomes to definitions after status is final:

- primary normalized/preserved unknown — usually nonblocking informational/history according to product approval;
- recovered from backup — durable recoverable warning until acknowledged;
- degraded/read-only profile — durable blocking/recoverable error according to available actions;
- unrecoverable new profile/reset — durable blocking error;
- save failure with prior generation preserved — recoverable error;
- forward-schema read-only — durable blocking/recoverable error.

No player notification includes raw file paths or raw save data.

## 20. Caller migration rules

### 20.1 General order

```text
validate request/domain state
→ stage/mutate transaction
→ persist/verify according to owning contract
→ publish authoritative result
→ create typed notification request
→ enqueue result returned separately
→ presenter receipt evolves asynchronously
```

### 20.2 World state (#172)

- remove all hard-coded message strings from `WorldStateService`;
- event definition/content supplies notification definition ID;
- publish start/end/cancel only after committed revision;
- correlation uses event instance/revision;
- duplicate event delivery produces one notification;
- unavailable technical consumer cannot announce a false gameplay effect;
- notification failure does not block state event subscribers after commit.

### 20.3 Boss loot (#168)

- compute and apply through one durable result/ledger first;
- definition parameters use stable boss/item/result IDs and approved content references;
- no fallback boss/item/player name formatting in technical service;
- one committed result correlation produces one notification group/summary;
- duplicate/replay emits no duplicate world announcement;
- failed/partial transaction uses failure definition, never success copy.

### 20.4 Economy (#163 and callers)

- low-level add/consume/spend returns typed result and does not notify;
- UI/orchestrator may map insufficient balance or committed result to a predefined definition;
- invalid programmer request may remain technical diagnostic rather than player notification;
- resource amount uses typed `ResourceType`/`Int64` parameter.

### 20.5 NVS-01 (#133/#134)

- definition IDs and source content are included in G1/A1 mappings;
- hook unavailable/retry/report commit outcomes have stable correlation IDs;
- report consequence success notification follows the atomic committed result;
- duplicate resume/result cannot duplicate notification;
- authored copy remains Codex narrative/content and source-versioned.

### 20.6 Bridge (#135)

- host/session/route/result errors map to predefined integration definitions;
- raw Android exception/class/path is diagnostic-only;
- duplicate lifecycle callbacks use session/correlation identity;
- Android presentation/push remains out of scope; Unity-visible queue uses the common contract.

## 21. Accessibility and UI requirements

### 21.1 Non-color communication

Each visible severity uses:

- readable text;
- icon/shape or severity label;
- optional color as secondary cue;
- contrast meeting the project's approved accessibility target.

### 21.2 Text and localization

- dynamic layout supports approved text scaling;
- long localization and plural/select expansion do not clip;
- right-to-left readiness is documented when localization scope reaches it;
- no fixed pixel assumptions that fail supported resolutions/safe areas;
- truncation never removes critical recovery meaning/action.

### 21.3 Input

- keyboard, controller, mouse, and touch where supported;
- predictable focus order;
- required acknowledgement cannot be bypassed by unsupported back/cancel handling;
- action targets have accessible labels;
- presentation does not steal focus for ordinary toasts.

### 21.4 Reduced motion

- no flashing;
- reduced-motion mode uses immediate/fade-only transitions as approved;
- timing/expiry unaffected by animation duration;
- no rapid stacking animation.

### 21.5 Accessibility announcement

Use an adapter/capability result for platform announcement/screen-reader integration.

Statuses:

```text
Announced
Unsupported
Unavailable
Failed
NotRequired
```

Unsupported platform capability is reported honestly; it does not prevent visual delivery when the definition allows it.

## 22. Implementation sequence

### Phase A — this merged specification

No executable/content/save/UI change.

### Phase B — typed contract and session queue

Branch:

```text
codex/notification-contract-queue
```

May begin from current main after #156 or in another explicitly reviewed non-overlapping window while preserving canonical validation.

Expected scope:

- immutable request/definition/result/receipt/action/diagnostic models;
- expanded `INotificationService` typed API;
- pure session queue/deduplication/capacity/state machine;
- injected clock/definition resolver seams;
- presenter registration contract with fake presenters;
- legacy wrappers retained/deprecated;
- focused EditMode tests;
- current caller inventory record.

Do not include:

- scene/UI presenter;
- player-facing content catalog;
- `SaveGameData.cs` or persistence;
- caller migration;
- `Bootloader.cs`;
- Android;
- narrative copy.

### Phase C — notification definition/content source

Separate focused source/engineering PRs after the relevant #183 catalog foundation:

- Codex engineering supplies schema/technical definitions/validation;
- Codex narrative/content supplies localization keys/copy/tone/action labels;
- source and generated artifacts retain version/hash/provenance;
- user approval applies to unresolved material player-facing choices.

### Phase D — visible presenter

Prerequisites:

- Phase B accepted;
- #223 committed production scenes;
- #150 scene descriptor/startup profile available or an equivalent approved integration point;
- notification content source accepted;
- no active shared-file conflict.

Scope:

- one validated presenter host per relevant production scene or reviewed runtime-created host;
- safe-area/layout/input/reduced-motion/accessibility behavior;
- presenter registration/handoff across Boot/RealmSelection/Kingdom;
- PlayMode and Player evidence;
- no caller migration beyond a focused test definition.

### Phase E — durable outbox/history

Prerequisites:

- #137 persistence/field/migration/deletion contract accepted;
- shared `SaveGameData.cs` lock declared;
- durable definition set approved.

Scope:

- persisted semantic records/outbox;
- recovery/migration/retention/deletion/fault tests;
- acknowledgement persistence;
- no broad domain caller migration.

### Phase F — focused caller migrations

Separate PRs under owning issues:

```text
#172 world state
#168 boss loot
#137 save recovery
#169 Wishgate/Realm Gem
#176 relationships
#133/#134 NVS-01
#135 bridge
other validated UI/orchestrators
```

## 23. Expected file boundary

Phase B likely changes/adds:

```text
unity/Assets/AL/Scripts/Core/Interfaces/INotificationService.cs
unity/Assets/AL/Scripts/Core/Interfaces/Notifications/**
unity/Assets/AL/Scripts/Services/Local/LocalNotificationService.cs
unity/Assets/AL/Tests/EditMode/Notifications/**
unity/Docs/Notification_Caller_Inventory.md
matching .meta files
```

Phase D likely adds:

```text
runtime presenter/view-model/UI files under a focused UI/Notifications path
production scene presenter integration or validated runtime host
PlayMode tests
```

Phase E may change:

```text
SaveGameData.cs with explicit shared lock
LocalSaveGameService normalization/migration/deletion only as required by #137
notification persistence adapter/tests
```

Prohibited in Phase B:

```text
Bootloader.cs
ServiceLocator.cs unless an independent reviewed need exists
SaveGameData.cs / LocalSaveGameService.cs
WorldStateService.cs
LocalBossLootService.cs
scenes/Build Settings
Android
narrative/localization copy
balance/rewards
```

## 24. Required tests

### 24.1 Definition validation

- valid each severity/category/channel/ack/durability/dedupe policy;
- blank/invalid/duplicate definition ID;
- unsupported schema/version;
- invalid channel/severity/ack combination;
- blocking definition without required acknowledgement;
- invalid expiry/priority/capacity-eviction policy;
- duplicate/blank parameter/action names;
- invalid privacy/persistability combination;
- invalid predecessor/successor replacement graph;
- deterministic diagnostic ordering.

### 24.2 Request/parameter validation

- valid request;
- unknown definition;
- catalog pending/unavailable/invalid;
- missing required parameter;
- unknown parameter;
- wrong parameter type;
- non-finite/overflow numeric input;
- oversized correlation/string;
- unsafe markup/control characters;
- sensitive value rejected from player/persistent parameter;
- invalid requested channel;
- timestamp outside bounds;
- no queue mutation on rejection.

### 24.3 Queue and ordering

- accept pending with no presenter;
- exact replay deduplicates;
- correlation conflict rejects;
- approved replacement supersedes;
- invalid replacement rejects;
- severity/priority/time/sequence ordering;
- one active item per initial channel capability;
- low item cannot starve blocking/recoverable error;
- repeated queries return immutable snapshots;
- throwing queue observer isolated.

### 24.4 Capacity

- fill to 64;
- remove completed/expired first;
- evict allowed oldest lowest transient;
- never evict blocking/required/session-until-acknowledged;
- reject when no safe eviction;
- critical rejection diagnostic once/rate-limited;
- capacity operation deterministic.

### 24.5 Receipt state machine

- pending → presented → acknowledged;
- pending → presented → dismissed;
- transient expiry before/after presentation;
- blocking cannot expire/dismiss as acknowledgement;
- presenter failure and retry;
- supersession;
- stale callback/token rejected;
- console fallback does not mark presented;
- timestamps/attempt counts exact and monotonic.

### 24.6 Presenter lifecycle

- register valid presenter;
- duplicate capability rejected;
- unregister/idempotent unregister;
- presenter destroyed/late callback;
- no presenter then attach drains queue;
- scene transition detach/reattach retains blocking item;
- duplicate scene presenter does not double-render;
- throwing presenter isolated;
- queue remains authoritative.

### 24.7 Content/localization

- valid locale/parameters;
- missing key;
- unsupported locale;
- placeholder name/type mismatch;
- plural/select formatting;
- parameter escaping;
- rich-text injection attempt;
- approved generic development/release fallback;
- raw key/path/stack never shown;
- content version mismatch;
- long/large text output.

### 24.8 Actions

- valid acknowledgement;
- valid retry/route action;
- action not allowed by definition;
- invalid/stale correlation;
- invalid payload;
- handler unavailable/throws;
- failed action does not acknowledge when prohibited;
- duplicate action invocation idempotent;
- no arbitrary URL/scene/object/delegate path.

### 24.9 Legacy wrappers

- each wrapper emits stable legacy diagnostic;
- escapes/raw text not treated as trusted markup;
- no typed `Presented` result;
- no durable history;
- no new caller introduced;
- current caller inventory exactly identifies WorldState/BossLoot and confirms ShowResourceGain usage/absence.

### 24.10 Persistence phase

- old save with no notification fields;
- null list/entry/definition;
- duplicate correlation records;
- unknown future definition preserved safely;
- migration/version mismatch;
- outbox persisted with owning transaction;
- save failure before/after outbox;
- reload/retry delivery once;
- acknowledgement persistence;
- bounded completed pruning;
- unacknowledged blocking never pruned;
- full #137 deletion;
- no rendered text/private path/stack persisted.

### 24.11 Presentation/accessibility

- each severity uses text plus non-color cue;
- safe area/supported small resolution;
- large text/long localization;
- keyboard/controller/mouse/touch;
- focus behavior for acknowledgement;
- toast does not steal focus;
- reduced motion/no flashing;
- combat/control overlap test;
- accessibility announcement supported/unsupported/failure result;
- blocking item survives scene transition.

### 24.12 Integration regressions

- backup recovery/unrecoverable save result;
- catalog unavailable;
- world-state start/end once;
- committed boss reward once;
- failed reward never success-notifies;
- duplicate result one notification;
- NVS hook unavailable/retry/report commit;
- bridge unavailable/session error;
- no low-level economy call auto-notifies;
- console-only environment reports pending/failure honestly.

## 25. Canonical validation

Phase B from canonical workspace:

```powershell
$repo = "C:\Users\MY\Documents\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\NotificationContractCompile.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform EditMode -assemblyNames AL.EditMode.Tests `
  -testResults "$repo\unity\Logs\NotificationContractEditMode.xml" `
  -logFile "$repo\unity\Logs\NotificationContractEditMode.log"
```

Presenter phase additionally runs:

- corrected #127 PlayMode suite;
- production scene presenter attach/detach tests after #223;
- #150 Windows Player build/isolated launch evidence where applicable;
- UI hierarchy/accessibility/large-text evidence.

Report:

- exact base/head SHA;
- changed files and lock state;
- definition/request/result/receipt/action schema versions;
- current caller inventory;
- every validation/queue/dedupe/capacity/state test row;
- focused/complete EditMode totals/XML;
- PlayMode/Player applicability and results;
- no raw-string new caller proof;
- no save/UI/caller migration in Phase B;
- final `git diff --check origin/main...HEAD`;
- final repository status;
- every blocked/unperformed check.

Duplicate-workspace, exit `199`, missing XML, skipped suite, console log represented as delivery, or development fallback represented as localized release evidence is blocked validation.

## 26. Acceptance criteria

- [ ] Authoritative API accepts stable definition IDs and typed parameters rather than raw player-facing strings.
- [ ] Definitions strictly control severity/category/channel/priority/ack/durability/dedupe/expiry/parameters/actions/privacy.
- [ ] Enqueue result and visible delivery receipt are separate and honest.
- [ ] Exact replay deduplicates and correlation conflicts fail visibly.
- [ ] Queue ordering/capacity is deterministic and never silently evicts required/blocking items.
- [ ] Pending required notifications survive presenter absence and scene transitions.
- [ ] Console fallback never claims visible delivery.
- [ ] Localization/content is source-owned, versioned, placeholder-validated, and safe from markup injection.
- [ ] Missing content has honest development/release fallback behavior without raw key/path/stack leakage.
- [ ] Actions are typed/registered/validated and cannot carry arbitrary routes/URLs/objects/delegates.
- [ ] Presentation is safe-area aware, input-accessible, non-color-only, reduced-motion safe, and large-text/localization ready.
- [ ] Initial contract/queue implementation edits no save, scene, UI, caller, or Bootloader files.
- [ ] Durable outbox/history waits for #137 and is recoverable, bounded, migratable, private, duplicate-safe, and fully deletable.
- [ ] Low-level services do not format success copy; owning committed-result orchestrators publish through focused migrations.
- [ ] Verified current raw callers migrate only under #172 and #168.
- [ ] Canonical compile and complete/focused tests pass with exact evidence.
- [ ] No unapproved narrative, reward, balance, save, Android, HUD redesign, scene promotion, or unrelated change is included.

## 27. Codex handoff

```text
Codex engineering: implement only Phase B of issue #177 from current main using unity/Docs/Notification_Delivery_Contract_Spec.md. Create codex/notification-contract-queue. Add immutable typed definition/request/enqueue-result/delivery-receipt/action/diagnostic contracts, an injected definition resolver and realtime clock, a deterministic 64-record session queue with correlation deduplication/capacity/state transitions, presenter registration seams with fake presenters, and complete EditMode tests. Retain/deprecate the three raw-string compatibility wrappers and inventory their callers. Do not edit Bootloader.cs, ServiceLocator.cs, saves, scenes, UI, Android, WorldStateService, LocalBossLootService, or narrative/localization content; do not add persistent history or migrate callers. Run canonical Unity validation and return one focused draft PR for Codex coordination/review.
```
