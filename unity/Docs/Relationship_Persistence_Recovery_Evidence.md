# Relationship Persistence and Recovery Evidence

## Invariants

- A relationship transaction is serialized as one document containing the complete save composition and its idempotency receipts.
- Publication uses a durable temporary file followed by an atomic replace/move; no field or receipt is published separately.
- Reload constructs an isolated immutable transaction state and publishes it only after the complete document parses, legacy identities migrate safely, and snapshots validate.
- A retry with the same transaction identity and semantic fingerprint returns the restored receipt and does not reapply any delta.
- Unsupported stable rows remain preserved, while exact legacy aliases and approved legacy labels migrate to canonical stable narrative IDs.
- A shared store lock plus byte-for-byte generation compare-and-swap prevents concurrent coordinators from overwriting a newer composition.
- Unsupported envelopes and forward save schemas fail closed without rewriting durable bytes.

## Injected fault matrix

| Fault point | Automated assertion | Observed recovery |
| --- | --- | --- |
| Before validation | `FaultBeforeDurablePublicationReloadsCompletePriorState(BeforeValidation)` | Complete prior affinity, faction, and empty receipt set reload. |
| During persistence, after the temporary document is flushed but before atomic publication | `FaultBeforeDurablePublicationReloadsCompletePriorState(DuringPersistence)` | Complete prior composition reloads; no staged consequence is visible. |
| After durable write, before acknowledgement | `FaultAfterDurableWriteReloadsCompleteCommitAndRetryIsExactlyOnce` | Complete committed composition and receipt reload; retry is `AlreadyCommitted` and values remain unchanged. |
| During reload | `FaultDuringReloadPublishesNothingAndNextReloadIsComplete` | Faulting load returns no partial snapshot; next load returns the complete durable composition. |
| Process/coordinator restart | `AtomicFileStoreSurvivesCoordinatorRestartWithReceiptIntact` | Atomic file document reloads with both relationship changes and receipt; replay remains exactly once. |
| Stale concurrent generation | `StoreCompareAndSwapRejectsStaleGeneration` | The stale writer is rejected and the newer complete document remains unchanged. |
| Unsupported envelope | `UnsupportedEnvelopeFailsClosedWithoutReplacingBytes` | Reload rejects the version and preserves the exact input bytes. |

## Compatibility coverage

- `LegacyLabelsMigrateAndCommittedCompositionReloadsAsOneState` starts from legacy `Captain Valerius` and `FACTION_VEIL_WATCH`, migrates to `npc_valerius` and `faction_veil_watch`, preserves an unknown future row, commits both domains, and reloads one complete composition.
- `CurrentStableIdSaveReloadsWithoutIdentityDrift` reloads current canonical IDs unchanged.

## Reproduction

Run with Unity 2022.3.62f3:

```sh
Unity -batchmode -nographics -projectPath unity \
  -runTests -testPlatform EditMode \
  -testFilter AL.Tests.EditMode.Relationships.RelationshipPersistenceRecoveryTests \
  -testResults relationship-persistence.xml
```

The focused persistence fixture passed 9/9 tests, and the complete relationship EditMode suite passed 52/52 on 2026-08-17. The implementation and tests are in:

- `Assets/AL/Scripts/Core/Relationships/RelationshipPersistence.cs`
- `Assets/AL/Tests/EditMode/Relationships/RelationshipPersistenceRecoveryTests.cs`
