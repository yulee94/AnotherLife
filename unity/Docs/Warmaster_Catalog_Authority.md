# Warmaster technical catalog authority

Status: inactive and unavailable pending explicit user balance approval

Issue: #171
Dependencies: #183 catalog publication, #137/#450 profile-bound persistence, #163 typed Warzone Credit mutation

## Authority boundary

`Assets/AL/StreamingAssets/GameData/al_warmaster_technical_catalog.json` is the single technical input document for Warmaster identity, membership, purchase eligibility, Warzone Credit prices, progression thresholds, completion reward identity, equipment slots, and stat modifiers.

`Assets/AL/StreamingAssets/GameData/al_warmaster_content_catalog.json` remains the PR #339 narrative/content authority for display and summary keys only. It is not purchase, balance, entitlement, equipment, or stat authority. `WarmasterSetDefinition` is a legacy presentation asset and is not technical or production authority.

`AL.Warmaster.Catalog.WarmasterCatalogValidator` is the publication gate. Downstream purchase, reward, equipment, and presentation code may consume only a non-null `WarmasterCatalogSnapshot` returned with `WarmasterCatalogValidationStatus.Valid`. It must not consume the mutable input, the content catalog, `WarmasterSetDefinition`, or constants in `LocalWarmasterService` as authority.

The checked-in technical document is intentionally inactive and incomplete. Null balance and equipment fields are approval blockers, not zero values or defaults. It cannot produce a snapshot, enable purchases, or grant a completion reward.

## Stable identifiers

The retained set identifier is:

- `prototype_true_warmaster`

The complete canonical piece identifier set is:

- `warmaster_piece_01`
- `warmaster_piece_02`
- `warmaster_piece_03`
- `warmaster_piece_04`
- `warmaster_piece_05`
- `warmaster_piece_06`
- `warmaster_piece_07`
- `warmaster_piece_08`
- `warmaster_piece_09`
- `warmaster_piece_10`

These identifiers preserve merged PR #339 and current-save compatibility. Player-facing code must resolve PR #339 localization keys and must not expose these internal identifiers as release copy. A rename requires an explicit catalog revision and save alias/migration plan; silent replacement is forbidden.

## Activation contract

A catalog is publishable only when all conditions are true:

1. `catalogId` is exactly `al_warmaster_catalog` and `schemaVersion` is exactly `1`.
2. `revision` is nonblank and identifies the approved immutable input revision.
3. `activation` is `Active` only after the user has explicitly approved every player-facing balance field.
4. The one canonical set and all ten canonical pieces are present exactly once with exact membership.
5. Every required field passes validation.
6. #183 publishes the immutable technical snapshot without silent fallback.
7. Runtime mutations use #163 for typed Warzone Credit operations and #137/#450 for profile-bound persistence; this catalog never mutates currency or saves.

Missing, inactive, malformed, unsupported, incomplete, duplicate, or unknown data returns no snapshot. `CanPurchase` and `CanGrantCompletionReward` remain false. A previously accepted snapshot must not be retained after an activation/revision reload failure unless a separately approved lifecycle contract explicitly preserves that exact immutable revision.

## Authoritative technical fields

Set fields:

- `id`: stable set identity.
- `pieceIds`: exact membership.
- `requiredPieceCount`: user-approved number of unique owned canonical pieces required for completion. It must be positive and no greater than membership count.
- `completionRewardId`: stable entitlement/reward operation input. It does not itself grant or persist a reward.

Piece fields:

- `id`, `setId`: stable identity and membership.
- `warzoneCreditPrice`: user-approved positive integer consumed by the #163 transaction; callers must never supply or override price.
- `requiredOwnedPieceCount`: user-approved nonnegative count required before the piece is eligible; it must be below the completion threshold.
- `purchaseExperienceAward`: user-approved nonnegative progression input. The existing hard-coded `25` is not approved authority.
- `equipmentSlotId`: canonical slot consumed by the #137/#450-compatible equipment/loadout path.
- `statModifiers`: complete canonical stat inputs consumed exactly once by the established stat pipeline. Duplicate stat IDs, missing values, and zero modifiers reject publication.

A unique equipment slot is required per piece in schema 1. If the intended product design permits mutually exclusive alternatives in one slot, that is a schema and user-design decision and must be reviewed before activation rather than inferred by a worker.

## Ownership of downstream behavior

This catalog owns data only. It deliberately does not duplicate:

- #163 currency balance, checked mutation, atomicity, or event authority;
- #137/#450 profile identity, save ledger, migration, rollback, replay, or commit-uncertain authority;
- #183 manifest loading, immutable publication, lifecycle, or global catalog activation authority;
- the canonical equipment/loadout and derived-stat calculation pipeline.

The purchase worker must identify a piece and catalog revision, resolve the accepted snapshot, and use its price and eligibility threshold. The equipment worker must use accepted slot/stat inputs and established ownership/loadout services. Neither worker may activate the checked-in pending document or substitute fixture values.

## Explicit user approval items

All values below remain unresolved and must stay null/inactive in production source until the user records approval:

- each of the ten Warzone Credit prices;
- the set completion `requiredPieceCount` (the current hard-coded `10` is historical behavior, not approval);
- each piece's `requiredOwnedPieceCount` purchase gate;
- each piece's `purchaseExperienceAward` (the current hard-coded `25` is historical behavior, not approval);
- completion reward/entitlement identity and exact semantics;
- equipment slot taxonomy and compatibility rules;
- every stat identifier and modifier amount;
- the decision whether completion auto-equips anything;
- activation revision and production activation approval.

Test fixtures use conspicuous synthetic positive values solely to exercise validation. They are not balance recommendations and must never be copied into the technical catalog.

## Validation evidence

`WarmasterCatalogValidatorTests` covers a complete active fixture and fail-closed rejection for duplicate IDs, nonpositive prices, invalid completion and per-piece thresholds, incomplete entries, missing approved price, inactive catalogs, missing canonical pieces, and unknown IDs. Every rejection proves both purchase and completion reward gates remain disabled and no immutable snapshot is published.

This slice does not wire a loader or runtime consumer, change `LocalWarmasterService`, activate purchases, migrate saves, or assign final player-facing values. Those are downstream tasks ordered behind approval and their owning systems.
