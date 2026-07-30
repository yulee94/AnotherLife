# Notification Source-to-Runtime Mapping Contract

**Issue:** #177

**Status date:** 2026-07-30

**Primary Codex mode:** coordination/review

**Reviewed baseline:** `main@461494e9ae133e2a44fb4e60cfae0ed41a70ee94`

**Runtime contract authority:** `unity/Docs/Notification_Delivery_Contract_Spec.md`

**Approved source authority:** `unity/Assets/AL/StreamingAssets/GameData/al_notification_content_catalog.json`

## Purpose

This contract closes the technical mapping gap between the approved notification
content source from PR #340 and the immutable notification contracts and bounded
session queue from PR #384.

It does not change the approved source, implement a production catalog loader,
register a runtime resolver, present UI, migrate a publisher, or persist a
notification. It defines the exact input-to-contract conversion that a later
dormant engineering adapter must implement and validate before any production
activation can be reviewed.

## Authority boundary

- Codex narrative/content mode owns the approved definition IDs, source IDs,
  action IDs, localization keys, copy, parameter names, acknowledgement intent,
  and source-facing category labels.
- Codex coordination/review mode owns this deterministic mapping from approved
  source fields to the already accepted runtime contract.
- Codex engineering mode may implement only this mapping and its validation. It
  must not substitute IDs, copy, categories, actions, or presentation behavior.
- The user still owns final copy, notification density, visible priority,
  blocking acknowledgement experience, integrated playtest, and release
  approval.
- Terrestrial design is unrelated. The former A2 worktree, PR #369, terrestrial
  branches, and terrestrial source assets remain untouched.

## Source identity

The only accepted v1 input is:

| Field | Required value |
| --- | --- |
| Path | `unity/Assets/AL/StreamingAssets/GameData/al_notification_content_catalog.json` |
| Packet ID | `al_narrative_notification_content_source_v001` |
| Catalog ID | `al_notification_content_catalog` |
| Catalog version | `0.1.0` |
| UTF-8 file length | `11,786` bytes |
| SHA-256 | `13ba706cb89039171d28805f2484fe923fdcb408cc367e882828e6fac78fa58f` |
| Sources | exactly `6` |
| Actions | exactly `3` |
| Definitions | exactly `11` |
| Draft localization entries | exactly `31` |

The source file remains byte-identical in this coordination slice and in the
first engineering adapter slice. A changed version, byte length, hash, packet
identity, or count is not an in-place upgrade. It requires a new reviewed source
packet and a versioned mapping revision.

The hash pin is a source-review guard for the dormant v1 adapter. It is not a
replacement for the #183 production manifest/envelope, provenance, atomic
publication, rollback, or packaging authority.

## Confirmed incompatibilities

Current source cannot be converted by the current runtime without an explicit
correction:

1. Source actions use `al_notify_action_*`; current runtime validation accepts
   only `al_action_*`.
2. The seven source-facing category strings do not directly match the broader
   runtime category enum.
3. The source intentionally omits technical definition fields such as bounded
   priority, expiry, deduplication, privacy, eviction, typed parameter schemas,
   per-definition action membership, and replacement edges.
4. The source declares actions globally but does not associate retry or recovery
   detail actions with individual definitions or provide an owning handler/route
   contract.
5. The #183 common production catalog authority does not yet publish a
   notification family or production notification consumer.

Production therefore remains fail-closed through
`UnavailableNotificationDefinitionResolver`.

## Canonical identifier policy

### Definition and source IDs

- Definition IDs remain the exact approved `al_notify_*` values.
- Source-system IDs remain the exact approved `al_source_*` values.
- Matching is ordinal and case-sensitive.
- No trimmed, case-folded, renamed, or best-effort alias is accepted.

### Action IDs

The exact approved action IDs are canonical end to end:

```text
al_notify_action_acknowledge
al_notify_action_retry_operation
al_notify_action_open_recovery_details
```

The later engineering slice must update runtime action validation to accept:

```text
^al_notify_action_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

The old test-only `al_action_*` prefix is not an approved alias and must fail
closed. A hidden source-to-runtime rename would create two identities for one
action and is prohibited.

## Exact source-enum mappings

### Severity

| Source value | Runtime value | Queue priority |
| --- | --- | ---: |
| `info` | `Information` | 30 |
| `success` | `Success` | 40 |
| `warning` | `Warning` | 60 |
| `recoverable_error` | `RecoverableError` | 80 |
| `blocking_error` | `BlockingError` | 100 |

These priorities are a deterministic dormant queue profile, not approval of
live notification density or visual prominence. Visible integration must retain
the monotonic safety ordering and return material presentation changes to the
user gate.

### Category

| Source value | Runtime value |
| --- | --- |
| `save_recovery` | `SaveRecovery` |
| `operation_availability` | `ContentAvailability` |
| `reward_result` | `Reward` |
| `world_state` | `WorldState` |
| `bridge` | `Integration` |
| `catalog` | `ContentAvailability` |
| `content_resolution` | `ContentAvailability` |

The source value remains source identity and provenance. The runtime value is a
broader technical queue classification. Unknown values, alternate spellings,
and enum-name strings in the source fail closed.

### Channel

| Source value | Runtime default and only allowed channel |
| --- | --- |
| `toast` | `Toast` |
| `acknowledgement` | `Acknowledgement` |

The first mapping exposes a singleton allowed-channel list. A caller cannot
escalate or downgrade the approved source channel.

### Durability

| Source value | Runtime value |
| --- | --- |
| `session_only` | `SessionTransient` |
| `future_durable_outbox` | `DurableUntilAcknowledged` |

`DurableUntilAcknowledged` is recognized technical intent, not active durable
delivery. Until #137 provides the accepted adapter and save authority, enqueue
must return `RejectedDurabilityUnavailable` without queue mutation.

## Common technical profile

Every mapped definition uses:

| Field | v1 value |
| --- | --- |
| `schemaVersion` | `1` |
| `contentVersion` | `1` |
| `allowedChannels` | singleton list containing `defaultChannel` |
| `deduplicationPolicy` | `ByCorrelationAndDefinition` |
| `requiresCorrelation` | `true` |
| `expiryPolicy` | `None`, `0` seconds, `expireWhilePresenterUnavailable=false` |
| predecessor IDs | empty |
| successor IDs | empty |

The source does not approve a visible expiry/density profile. `None` is the
non-invented dormant mapping. A later presenter integration must add a reviewed
versioned presentation profile before transient toasts become production
visible.

Acknowledgement and capacity rules are derived exactly:

- `requiresAcknowledgement=true` maps to `Required`;
- `requiresAcknowledgement=false` maps to `None`;
- only `SessionTransient` definitions with acknowledgement `None` set
  `allowCapacityEviction=true`;
- durable, required, or blocking definitions set
  `allowCapacityEviction=false`.

Privacy is:

- `ProfilePrivate` for the three `al_notify_save_*` definitions and their
  `profile_label` parameter;
- `PublicGameplay` for the other eight definitions and their parameters;
- no source parameter maps to `SensitiveTechnical` or raw technical text.

## Parameter profile

All seven source parameter names map to required `LocalizationReference`
values, never preformatted prose. The dormant v1 mapping keeps every parameter
nonpersistable until an approved localization-reference membership authority
exists.

| Parameter | Runtime kind | Maximum UTF-8 bytes | Privacy | Persistable |
| --- | --- | ---: | --- | --- |
| `profile_label` | `LocalizationReference` | 256 | `ProfilePrivate` | false |
| `operation_name` | `LocalizationReference` | 256 | `PublicGameplay` | false |
| `reward_summary` | `LocalizationReference` | 256 | `PublicGameplay` | false |
| `event_name` | `LocalizationReference` | 256 | `PublicGameplay` | false |
| `route_label` | `LocalizationReference` | 256 | `PublicGameplay` | false |
| `catalog_label` | `LocalizationReference` | 256 | `PublicGameplay` | false |
| `content_label` | `LocalizationReference` | 256 | `PublicGameplay` | false |

For every row:

- `required=true`;
- numeric minima/maxima are absent;
- `persistable=false` in dormant v1;
- markup, raw paths, exception text, stack traces, credentials, email addresses,
  raw player names, and internal IDs are prohibited;
- parameter names must exactly match the source definition and body
  placeholders;
- unknown, missing, duplicate, wrong-kind, blank, oversized, or unsafe values
  reject before queue mutation.

The first engineering correction must make
`NotificationParameterValueKind.LocalizationReference` enforce this exact
ordinal grammar:

```text
^[a-z][a-z0-9]*(?:_[a-z0-9]+)*(?:\.[a-z][a-z0-9]*(?:_[a-z0-9]+)*)+$
```

The grammar excludes whitespace, `@`, drive/URI punctuation, path separators,
markup, and single-segment internal IDs. Grammar alone is not content
authority. Before any resolver is registered with a production queue, every
reference must also resolve by exact ordinal membership through one injected,
immutable localization-reference authority built from approved catalog data.
Unknown, invalid, or authority-unavailable references fail before queue
mutation; an arbitrary grammar-valid key is not accepted as player content.

The dormant adapter may use a bounded fake membership authority in tests, but
the production default remains unavailable. Phase E may set a parameter
persistable only through a new mapping version after membership, privacy,
migration, retention, and deletion behavior are accepted with #137.

Changing a parameter to `SafeDisplayText` requires a new source and coordination
review; the adapter may not infer that downgrade.

## Action profile and membership

### Catalog action declarations

| Action ID | Source kind | v1 execution disposition |
| --- | --- | --- |
| `al_notify_action_acknowledge` | `Acknowledge` | presenter affordance invokes the queue-owned `Acknowledge` transition |
| `al_notify_action_retry_operation` | `RetryOperation` | unavailable pending owning contract |
| `al_notify_action_open_recovery_details` | `OpenRecoveryDetails` | unavailable pending owning contract |

The retry and recovery-detail declarations and localization labels are valid
source inventory, but no definition currently authorizes them. They must not be
materialized as `NotificationActionDefinition` values until a source revision
associates them with exact definitions and an owning engineering contract pins
payload schema, handler identity, replay behavior, route availability, and
acknowledgement behavior.

Acknowledgement uses one path only. `al_notify_action_acknowledge` is the
source/content identity for the presenter's required acknowledgement
affordance. After a valid presenter registration has placed a record in
`Presented`, the presenter invokes the queue-owned `Acknowledge` receipt
transition with the exact instance and registration token. It does not create a
`NotificationActionDefinition`, does not call `InvokeAction`, and does not use
`INotificationActionRegistry`.

The affordance is visible only for definitions whose source says
`requiresAcknowledgement=true`:

```text
al_notify_save_recovered_backup
al_notify_save_profile_degraded
al_notify_save_unrecoverable
al_notify_reward_failed
al_notify_catalog_unavailable
```

Every v1 definition has an empty runtime action list. There is no inferred
retry or details button, and the production
`UnavailableNotificationActionRegistry` remains unchanged. Before visible
activation, tests must prove presented/stale-registration/replay/already-applied
acknowledgement behavior and that the action registry is never invoked for the
acknowledgement command.

## Complete v1 definition mapping

Common fields use the profile above. `Ack` is the acknowledgement policy,
`Durability` is the runtime policy, and `Evict` is capacity-eviction permission.

| Definition ID | Source | Severity | Category | Channel | Ack | Durability | Priority | Privacy | Evict | Parameter | Ack affordance |
| --- | --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- | --- |
| `al_notify_save_recovered_backup` | `al_source_save` | `Warning` | `SaveRecovery` | `Acknowledgement` | `Required` | `DurableUntilAcknowledged` | 60 | `ProfilePrivate` | false | `profile_label` | yes |
| `al_notify_save_profile_degraded` | `al_source_save` | `RecoverableError` | `SaveRecovery` | `Acknowledgement` | `Required` | `DurableUntilAcknowledged` | 80 | `ProfilePrivate` | false | `profile_label` | yes |
| `al_notify_save_unrecoverable` | `al_source_save` | `BlockingError` | `SaveRecovery` | `Acknowledgement` | `Required` | `DurableUntilAcknowledged` | 100 | `ProfilePrivate` | false | `profile_label` | yes |
| `al_notify_operation_unavailable` | `al_source_nvs` | `Warning` | `ContentAvailability` | `Toast` | `None` | `SessionTransient` | 60 | `PublicGameplay` | true | `operation_name` | none |
| `al_notify_reward_committed` | `al_source_boss_loot` | `Success` | `Reward` | `Toast` | `None` | `SessionTransient` | 40 | `PublicGameplay` | true | `reward_summary` | none |
| `al_notify_reward_failed` | `al_source_boss_loot` | `RecoverableError` | `Reward` | `Acknowledgement` | `Required` | `DurableUntilAcknowledged` | 80 | `PublicGameplay` | false | `reward_summary` | yes |
| `al_notify_world_event_started` | `al_source_world_state` | `Information` | `WorldState` | `Toast` | `None` | `SessionTransient` | 30 | `PublicGameplay` | true | `event_name` | none |
| `al_notify_world_event_ended` | `al_source_world_state` | `Information` | `WorldState` | `Toast` | `None` | `SessionTransient` | 30 | `PublicGameplay` | true | `event_name` | none |
| `al_notify_bridge_unavailable` | `al_source_bridge` | `Warning` | `Integration` | `Toast` | `None` | `SessionTransient` | 60 | `PublicGameplay` | true | `route_label` | none |
| `al_notify_catalog_unavailable` | `al_source_catalog` | `BlockingError` | `ContentAvailability` | `Acknowledgement` | `Required` | `DurableUntilAcknowledged` | 100 | `PublicGameplay` | false | `catalog_label` | yes |
| `al_notify_content_unavailable` | `al_source_catalog` | `Warning` | `ContentAvailability` | `Toast` | `None` | `SessionTransient` | 60 | `PublicGameplay` | true | `content_label` | none |

Each definition allows exactly its listed source ID. `titleKey`, `bodyKey`, and
the matching draft-localization entries remain content-resolver source; the
technical definition resolver validates their coverage but does not render or
return player-facing prose. Every v1 runtime `Actions` list is empty.

## Atomic adapter behavior

The first engineering slice may add one unregistered, injected adapter/resolver.
It must use these states:

| Condition | Resolver behavior |
| --- | --- |
| Not initialized | `CatalogPending` |
| Source absent or unreadable | `CatalogUnavailable` |
| Unsupported packet/catalog/mapping version | `UnsupportedVersion` |
| Hash, structure, count, type, reference, mapping, placeholder, or definition validation failure | `InvalidDefinition` for the catalog; publish zero definitions |
| Valid catalog, unknown exact definition ID | `UnknownId` |
| Valid catalog, known exact definition ID | `Found` with one immutable definition |

Validation and publication are atomic. One invalid row prevents the entire
resolver snapshot from becoming available. A previous accepted immutable
snapshot may remain readable only when an explicit later reload contract
defines that behavior; the first dormant adapter has no live reload.

The adapter must detect duplicate JSON members before ordinary object
materialization. It must reject missing, extra, null, wrong-type, reordered
identity arrays when order is authoritative, duplicate, case-variant, unknown,
or unreferenced values. It must not use permissive enum parsing, trimming,
case-folding, fallback defaults, partial publication, or last-key-wins JSON
behavior.

## #183 relationship and activation boundary

The current source file is an approved source packet, not a complete #183
production catalog family.

Production loading remains blocked until a focused #183 phase provides or
explicitly incorporates:

- a versioned notification-family envelope and manifest entry;
- raw and semantic hashes plus provenance;
- deterministic packaging and address/path authority;
- whole-set validation and atomic publication;
- rollback/shadow comparison;
- compatibility and unsupported-version behavior;
- production consumer registration and lifecycle;
- current-main Player/package/device evidence.

The dormant adapter may accept injected source bytes in EditMode tests. It must
not read an alternate hard-coded dictionary, register in `Bootloader`, replace
`UnavailableNotificationDefinitionResolver`, or claim production catalog
authority.

## Resource and optimization bounds

The first adapter must:

- accept at most `16,384` source bytes and require the exact v1 length/hash;
- parse once at construction, never per frame or per `Resolve`;
- retain at most 6 sources, 3 action declarations, 11 definitions, 31
  localization entries, and their bounded immutable indexes;
- use ordinal dictionaries for O(1) exact definition lookup;
- allocate no gameplay-frame polling, coroutine, timer, network request, asset,
  texture, audio, scene object, or persistent record;
- add no package/dependency;
- avoid retaining duplicate raw/rendered content when validation completes;
- declare measured managed assembly, Player/package, install, allocation, and
  low-end-device impact when a production consumer is later introduced.

The dormant C# implementation will add expected nonzero managed IL and bounded
construction-time allocations. Exact Player/package/install and runtime-memory
impact remain unmeasured until a Player build includes the adapter; no
zero-impact claim is permitted.

## Required engineering validation

### Positive

- exact source identity and all counts pass;
- all 11 exact IDs resolve;
- every resolved value passes
  `NotificationValidation.ValidateDefinition`;
- all source-to-runtime fields match the complete mapping table;
- all three canonical action IDs pass the corrected action-ID validator;
- the five required definitions expose the queue-owned acknowledgement
  affordance and every runtime `Actions` list is empty;
- localization references reject malformed grammar and reject unknown or
  authority-unavailable membership before queue mutation;
- the direct acknowledgement transition covers presented, stale registration,
  replay, and already-applied input without invoking the action registry;
- source JSON before and after the test run is byte-identical;
- future-durable requests return `RejectedDurabilityUnavailable`;
- the default production service still resolves through the unavailable
  resolver.

### Negative

Persisted or generated fixtures must reject:

- stale `al_action_*`, case variants, blank, oversized, duplicate, or unknown
  action IDs;
- every unknown category and every swapped category mapping;
- missing, duplicate, extra, null, or wrong-type root and nested fields;
- unsupported version, packet ID, catalog ID, byte length, or hash;
- duplicate/unknown source, definition, action, or localization IDs;
- bad source references and missing localization references;
- localization-reference grammar, membership, parameter/placeholder order,
  name, kind, bound, privacy, and persistability drift;
- source acknowledgement/channel/durability contradictions;
- unassociated retry/detail actions materialized into a definition;
- unknown enum spellings, permissive case changes, trimmed IDs, and partial
  catalog publication;
- mutation of caller-owned bytes/collections and nondeterministic output across
  repeated or reversed validation inputs where ordering is not source
  authority.

### Regression

- focused notification tests pass;
- complete EditMode tests pass or every unrelated baseline failure is reproduced
  exactly and owned;
- repository classify, hygiene, fixture, GUID, and `git diff --check` gates
  pass;
- no designated shared file, source catalog, scene, save, Android path,
  presenter, publisher, or registration file changes.

## Delivery order

```text
approved source #340
  -> runtime contract/queue #384
  -> this coordination mapping
  -> dormant strict adapter/resolver
  -> A1 exact-head integration review
  -> #183 production envelope/packaging
  -> visible content resolver/presenter
  -> #137 durable outbox/history
  -> focused publisher migrations
  -> user copy/visual/playtest approval
```

No later step is implied complete by an earlier merge.

## Current disposition

- Phase: source/runtime notification authority convergence.
- Mapping acceptance: ready for coordination/source-mode review after exact
  current-main validation.
- Runtime activation: blocked.
- Shared locks: none.
- User approval: not required for this dormant technical mapping; all
  player-visible and release gates remain open.
- Next Codex mode after merge: engineering, limited to the unregistered bounded
  adapter and validator correction described above.
