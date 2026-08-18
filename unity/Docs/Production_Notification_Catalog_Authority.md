# Production Notification Catalog Authority

**Issue:** #177

**Consumers:** #168, #169, #172, #176

**Dependencies:** #183 and #137/#450

**Primary mode:** Codex engineering

## Source of truth

The production notification authority is the following pair:

1. `unity/Assets/AL/StreamingAssets/GameData/al_notification_content_catalog.json` is the source-owned definition and draft-content packet. It owns notification IDs, source IDs, localization keys and text, parameter names, severity, channel, acknowledgement intent, and durability intent.
2. `unity/Assets/AL/StreamingAssets/GameData/al_notification_production_catalog.json` is the production inventory and activation boundary. It pins the source packet by path, IDs, version, byte length, and SHA-256; lists every currently required available definition; records consumer coverage; and records requirements that are explicitly blocked.

`Notification_Source_Runtime_Mapping_Contract.md` owns deterministic source-to-runtime conversion. `NotificationContentCatalogResolver` is the strict immutable adapter. Neither runtime code, a consumer-specific planner, a scene, nor fallback strings may create a second catalog or reinterpret an unavailable entry.

The production manifest does not by itself activate the resolver. Resolver registration, persistence, presentation, and producers are downstream tasks. Until their dependencies pass, those paths remain fail-closed.

## Available v1 definitions

| Definition | Source | Known consumers | Availability |
| --- | --- | --- | --- |
| `al_notify_save_recovered_backup` | `al_source_save` | #177, #137, #450 | available in source; durable activation blocked |
| `al_notify_save_profile_degraded` | `al_source_save` | #177, #137, #450 | available in source; durable activation blocked |
| `al_notify_save_unrecoverable` | `al_source_save` | #177, #137, #450 | available in source; durable activation blocked |
| `al_notify_operation_unavailable` | `al_source_nvs` | #177, #176 | available, session-only |
| `al_notify_reward_committed` | `al_source_boss_loot` | #177, #168, #169 | available, session-only; not a substitute for unauthored domain-specific outcomes |
| `al_notify_reward_failed` | `al_source_boss_loot` | #177, #168, #169 | available in source; durable activation blocked |
| `al_notify_world_event_started` | `al_source_world_state` | #177, #172 | available, session-only |
| `al_notify_world_event_ended` | `al_source_world_state` | #177, #172 | available, session-only |
| `al_notify_bridge_unavailable` | `al_source_bridge` | #177, #169, #172, #176 | available, session-only |
| `al_notify_catalog_unavailable` | `al_source_catalog` | #177, #183 | available in source; durable activation blocked |
| `al_notify_content_unavailable` | `al_source_catalog` | #177, #168, #169, #172, #176, #183 | available, session-only |

The three approved action identities remain source data, not per-definition runtime actions:

- `al_notify_action_acknowledge`
- `al_notify_action_retry_operation`
- `al_notify_action_open_recovery_details`

Queue acknowledgement is a direct queue transition. The v1 runtime definition action lists remain empty until an owning route contract exists.

## Explicitly blocked requirements

The manifest is authoritative for these blocks and validator-enforced dependency references:

- **Production packaging and common authority — #183.** No approved notification-family common envelope, provenance manifest, atomic publication/rollback path, packaged consumer, or activation decision exists. The source packet may be parsed in tests, but production resolver registration must not infer this missing authority.
- **Durable outbox — #137/#450.** `DurableUntilAcknowledged` definitions cannot degrade to the session queue. They require profile-bound writable authority, crash-safe storage, migration, replay, acknowledgement, retention, and recovery.
- **Boss reward intents — #168.** The planner currently uses `boss_reward.credits_committed`, acquisition-policy IDs, and `boss_reward.explicit_no_reward`. These are not approved `al_notify_*` identities and have no source-owned notification copy. They must not be redirected to generic reward definitions.
- **Wishgate outcomes — #169.** No approved typed Wishgate outcome IDs or source-owned notification copy exist. Generic reward text is not a semantic fallback.
- **World-event cancellation — #172.** The planner can emit `al_notify_world_event_cancelled`, while the approved packet only contains start/end definitions. Cancellation remains unavailable until a new source packet and mapping revision add it.
- **Relationship commit — #176.** No approved typed relationship-commit definition or copy exists. The operation-unavailable definition may report unavailability but cannot stand in for a success result.

World-event catalog values such as `notification.world_event.siege` are world-event source references, not typed notification definition IDs. A future producer maps an approved event plus transition to a canonical definition and a bounded localization-reference parameter; it must never treat those source references as catalog IDs.

## Authoring and ownership

1. Narrative/content mode authors or changes player-facing IDs, copy, localization references, parameter names, acknowledgement intent, and semantic meaning in a versioned source packet.
2. Coordination/review mode updates the source-to-runtime mapping when a source field, technical mapping, or dependency changes.
3. Engineering updates the production manifest and strict adapter only after the source and mapping are accepted. Engineering does not invent missing copy, IDs, or aliases.
4. The user retains final copy, density, priority, visual presentation, acknowledgement UX, integrated playtest, activation, and release approval.

A new entry requires a new unique canonical `al_notify_lowercase_snake_case` ID. Existing IDs are immutable. Semantic changes require a source version and packet revision; they are not made in place behind an unchanged identity. Consumers persist stable definition IDs and payload contracts, never rendered text.

## Validation and versioning

Run:

```text
python3 tools/notifications/validate_production_notification_catalog.py
python3 -m unittest -v tools/notifications/test_validate_production_notification_catalog.py
```

Validation rejects:

- duplicate JSON members, source IDs, manifest entries, required IDs, and blocked requirement IDs;
- malformed IDs and issue references;
- missing required entries or unreviewed extras;
- entry/source mismatch or order drift;
- missing source bytes, malformed JSON, and source identity/hash/version drift;
- blocked requirements without explicit consumers, dependencies, reasons, and activation gates;
- any resolution policy that permits fallback content.

The strict C# adapter separately validates the complete source structure, localization membership, placeholders, exact enums/mappings, contradictions, technical definitions, and atomic publication. A version, packet, count, order, byte-length, or hash change is unsupported until the source, manifest, mapping, adapter pins, and tests move together in one reviewed version step.

## Resolution and failure contract

Resolution is exact, ordinal, and case-sensitive:

1. Load the production manifest.
2. Verify the pinned source bytes and source identity.
3. Build the strict adapter atomically; publish zero definitions on any error.
4. Resolve only an exact manifest entry ID.
5. Validate the caller source ID and payload contract before queue mutation.
6. Reject blocked durability or unavailable localization authority before queue mutation.

Unknown IDs, missing files, invalid data, missing localization, unsupported versions, unavailable dependencies, and blocked requirements produce explicit typed failures with diagnostics. They never select a nearby entry, generic reward copy, raw internal text, or an unrelated “content unavailable” notification as replacement content. A diagnostic notification may only be emitted as its own separately requested and valid semantic event; it is not a resolver fallback.

## Impact

The catalog is bounded text data: 11 available definitions and 6 explicit blocked requirements. Validation is one-shot and deterministic; lookup remains O(1) in the strict adapter. This change adds no frame loop, network request, binary asset, package, scene, save mutation, or production registration. Runtime memory, Player package/install delta, and physical-device behavior remain unmeasured until downstream activation.
