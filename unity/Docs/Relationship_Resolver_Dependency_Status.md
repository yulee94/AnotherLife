# Relationship Resolver Dependency Status

**Tracking:** #176
**Implementation phase:** production source resolver plus pure immutable snapshot/planner APIs
**Reviewed:** 2026-08-16 against `main@2383b61`

## Implemented boundary

This phase consumes the approved relationship source packet without changing it:

- `Assets/AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json`
- source packet `al_narrative_relationship_authority_source_v001`
- exact bytes: `20313`
- SHA-256: `ed85e4a2796c277a8223aaf0ae75332fd6437e1f81a4520cf1ae867ab2992b8d`

`RelationshipCatalogResolver` validates the bounded, byte-exact packet and publishes the six NPC and five faction identities atomically. Canonical IDs and exact legacy aliases resolve ordinally; blank, guessed, display-name, case-folded, trimmed, unknown, unsupported, unavailable, or invalid input fails closed. Published identities, ID lists, aliases, numeric snapshots, persona snapshots, diagnostics, and plans are immutable defensive views.

`RelationshipSnapshotBuilder` creates deterministic, source-revision-bound views without retaining source rows. Supported aliases canonicalize before duplicate checks; unknown nonblank IDs are preserved as unsupported evidence; malformed rows disable planning. `RelationshipPlanner` is pure and revision-bound. Affinity preserves finite `[-100,100]` clamp semantics, faction arithmetic is checked Int32, sparse supported rows plan from zero, disabled identities reject, and no API in this phase mutates save state, persists, emits events, or enqueues notifications.

## Dependency reconciliation

### #137 / #450 — persistence and writable profile authority

Both issues remain open. #137 has accepted recovery and dormant authority foundations, but consumer-visible writable activation and identity installation remain incomplete. #450 remains externally blocked and retains the exclusive `SaveGameData.cs` and `Bootloader.cs` locks plus sensitive `LocalSaveGameService.cs` scope.

Consequently this phase does not introduce a save-backed adapter, production mutation registration, durable replay ledger, clone/persist/publish behavior, or commit-uncertain workaround. Those belong to later relationship mutation/integration work after the accepted #137/#450 boundary exists.

### #177 — notifications

The immutable notification contracts and dormant source adapter are merged, but production delivery remains fail-closed and #177 remains open for activation, durable history, presentation, device evidence, and user approval.

Consequently relationship plans contain no player-facing copy and emit/enqueue nothing. A later committed transaction may map typed committed results to #177 only after durable publication.

### #183 — game-data authority

The general six-family production catalog issue remains open and blocked for unresolved family inputs and whole-set activation. The relationship-specific narrative packet from #347 is nevertheless merged, bounded, hashable, and explicitly ready for #176 engineering consumption.

This phase therefore consumes only that exact relationship packet through an isolated strict resolver. It does not claim whole-set #183 activation, alter source content, infer missing records, or register unrelated catalog families.

### #467 — first-user vertical journey

#467 remains open and explicitly does not yet provide production onboarding/save authority or final journey acceptance. Its current Valerius work is an Editor-only offered/pending presentation slice, not the report transaction.

Consequently this phase preserves the approved future consequence (`GRANT_VALERIUS_AFFINITY_5` → `npc_valerius`, delta `5`) as source intent but does not apply it. Atomic NVS report composition remains external work under its owning transaction and persistence dependencies.

## Current caller/interface inventory

The legacy production interfaces and services remain unchanged in this phase:

- `IReputationService` / `ReputationService`
- `IFactionService` / `FactionService`
- `IPersonaService` / `PersonaService`

They still contain immediate-save/mutable/hard-coded compatibility behavior and are not evidence for the new contract. No new caller may use them for a one-time narrative consequence. Consumer migration, service adapters, label migration, durable mutation, and NVS transaction proof are separate downstream tasks after this resolver/snapshot boundary is accepted.

## Non-claims

This phase does not claim save mutation, persistence, durable idempotency, notification delivery, production registration, consumer migration, NVS consequence completion, whole-set catalog activation, integrated playtest, milestone approval, or release readiness.

Runtime impact is bounded to one strict catalog construction and pure operation-scoped snapshots/plans. There is no per-frame work, new package, binary asset, scene change, or device-specific dependency.
