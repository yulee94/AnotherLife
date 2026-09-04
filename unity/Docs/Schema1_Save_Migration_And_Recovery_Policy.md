# Schema-1 Save Migration and Recovery Policy

Status: owner-approved on 2026-09-01 for the schema-1-only scope.

## Supported compatibility

- Save format authority is `anotherlife.local-save`.
- Runtime save schema is `1`.
- The only approved upgrade is historical pre-schema `0` to schema `1`.
- Schema `1` loads and round-trips without a migration.
- Schema versions greater than `1` remain authoritative, byte-preserved, and read-only. The client must not replace them with a schema-1 backup or create a new profile over them.
- Schema-2/ProfileId activation remains owned by GitHub issue #450 and is not part of this policy.

## Recovery and rollback

- A malformed or truncated primary may be replaced only by an exact, current-schema, writable backup after the backup and resulting recovery target are verified.
- Invalid primary bytes are retained in hash-linked quarantine. Interrupted recovery resumes only from a recognized exact ledger.
- A checksum/hash mismatch in recovery or migration evidence fails closed as `RecoveryRequired`; no canonical generation is modified.
- When primary and backup are both invalid, both remain byte-exact for explicit recovery. The runtime does not auto-reset, delete, replace, or discard either generation.
- Failed migration or save transactions preserve the prior generation or report commit uncertainty with retained evidence; retry must be idempotent.

## Checked-in evidence

`Assets/AL/Tests/EditMode/Fixtures/SaveSchema1/manifest.json` pins every fixture by SHA-256 and records its expected compatibility outcome. `SaveSchema1GoldenFixtureTests` exercises the checked-in pre-schema upgrade, schema-1 round trip, truncation recovery, recovery-marker checksum rejection, future-schema downgrade rejection, and idempotent retry.

Existing persistence regression coverage remains the authority for injected write/move/read failures and crash windows, including:

- `BothInvalidSaveFilesArePreservedForExplicitRecovery`
- `QuarantinedRecoveryResumesExactIntermediateLedgerWithoutRepeatingSteps`
- `RecoveryStageWriteFailureBeforeMutationCanRetrySafely`
- `AtomicReplaceAfterMutationWithUnverifiedRollbackIsCommitUncertain`
- `MoveFallbackWindowClaimsPreviousPreservedOnlyAfterVerifiedRollback`

Migration diagnostics expose stable status/message codes and bounded typed dispositions. Raw save contents and private paths must not be emitted as telemetry.
