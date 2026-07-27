# Realm Gem Wishgate Source Handoff

**Packet ID:** `al_narrative_realm_gem_wishgate_source_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_realm_gem_wishgate_content_catalog.json`
**Related issue:** #169

## Source Intent

This packet defines the narrative/content authority for the eight Realm Gems and the Wishgate presentation boundary. It consumes `al_realm_catalog`, `al_world_atlas_narrative_catalog`, and the approved main quest line so engineering can later validate Realm Gem custody and the final wish path without inventing names, lore, or player-facing unavailable states.

The packet is deliberately non-authoritative for gameplay mutation. It does not earn, steal, return, consume, duplicate, grant, roll back, save, reward, notify, trade, chat, teleport, load scenes, or enforce PvP state.

## Stable Source Rules

- The eight approved Realm Gem IDs are exactly the two `realmGemIds` values per realm from `al_realm_catalog`.
- Realm Gems are sacred objective artifacts and custody markers, not permanent conquest trophies or account ownership proofs.
- Player-facing copy may describe temporary witnessed custody, contested custody, restoration duty, and eightfold signatures.
- Technical gem, realm, Wishgate, objective, and quest IDs remain debug-only; release presentation should use localization-facing names.
- The Wishgate source name is `Eightfold Concordance`; the guardian dragon name key resolves to `Vaeloryn`.
- The approved final wish emphases are `Bridges`, `Vigil`, and `Renewal`; they are epilogue/cosmetic emphasis source only until a future reward contract exists.
- The default runtime presentation is unavailable/nonmutating until #169 engineering owns eligibility, idempotency, save safety, notification delivery, rollback, and reward authority.

## Handoff Rules

Engineering should validate:

- catalog JSON parses successfully;
- catalog gem IDs exactly match the flattened `realmGemIds` from `al_realm_catalog`;
- each gem's `realmId` matches the owning realm in `al_realm_catalog`;
- all gem, custody state, Wishgate, and wish-emphasis IDs are unique;
- every display, summary, custody, signature, guardian-dragon, and status key resolves in `draftLocalization`;
- the Wishgate remains fail-closed until all eight-gem eligibility, one-time entitlement, reward, notification, save, rollback, and idempotency contracts are implemented;
- Realm Gem custody cannot imply territory ownership, permanent realm conquest, account realm change, resource grant, or unsupported cross-realm social rules.

## Acceptance Status

Source status: ready for Codex coordination/review and later #169 engineering consumption.
User gate: final Wishgate UX, reward balance, integrated objective play, and release playtest approval remain later gates.
