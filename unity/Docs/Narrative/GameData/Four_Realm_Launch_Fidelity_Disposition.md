# Four-Realm Launch Narrative Fidelity Disposition

**Status date:** 2026-07-29
**Primary Codex mode:** narrative/content
**Source packet:** `al_narrative_four_realm_launch_source_v001`
**Runtime catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json`
**Tracking:** issue #173 and the four-realm launch objective
**Reviewed baseline:** `main@463b5b8`
**Disposition:** `CHANGES REQUIRED`

## Review Boundary

This A3 disposition compares the approved four-realm launch source with the
merged bounded realm-hook work through PRs #293, #312, #318, #321, #332,
#338, and #346.

Broader save migration, post-commit events, multiple-profile support, character
creation, shared storage, balance, scenes, and release approval remain outside
this source-review PR.

The 2026-07-29 refresh also re-reviewed the intervening realm-selection work on
current `main`, including platform-safe catalog URI loading, heraldry, safe-area
layout, and mobile presentation. Those changes improve delivery and visual
identity. Merged PR #346 also removes the audited Champion-mode Crownlands
substitutions and requires committed realm context. The remaining work does not
add the pre-commit warning, wire account/sub-character authority, replace
hard-coded player-facing copy, or present a visible failure/retry state. The
disposition therefore remains `CHANGES REQUIRED`.

## Source Rules

The binding narrative and product meaning is:

- exactly four launch realms: `crownlands`, `stonehold`, `eldergrove`, and
  `umbral`;
- realm browsing before commitment is preview-only and grants no state;
- commitment binds the account to one realm for the current product phase;
- every sub-character inherits that account realm;
- shared storage remains same-realm account storage;
- cross-realm character creation and in-place realm change are rejected;
- the player sees the durable-allegiance warning before commitment;
- player-facing realm names, descriptions, selection lines, and unavailable
  states resolve from approved content/localization authority;
- an unavailable or invalid realm never becomes Crownlands by fallback.

## Fidelity Results

| Area | Result | Current evidence |
| --- | --- | --- |
| Stable realm identity | Pass | `RealmCatalogRuntime` accepts only the four approved lowercase IDs and their exact legacy enum mappings. |
| Realm ordering | Pass | The parser requires four unique `realmOrder` references and publishes them in source order. |
| Realm Gem references | Pass | Exactly two stable, globally unique lowercase Realm Gem IDs are required per realm. |
| Lock policy parsing | Pass | Account scope, same-realm sub-characters/storage, cross-realm rejection, and no post-commit realm change are validated as one policy block. |
| Bounded launch load | Pass | The launch loader is one-shot, bounded to 32 KiB, and publishes an immutable four-entry snapshot. |
| First local-profile commit | Pass for the bounded slice | Valid first commit persists once; same-realm retry is idempotent; different-realm selection is rejected; invalid identity and failed persistence fail closed. |
| Runtime definition consistency | Pass for the bounded slice | A committed-valid identity requires a matching runtime realm definition; `CurrentRealmId` returns `None` when the definition is unavailable. |
| Sub-character rule | Partial | `RealmCharacterConstraint` expresses the correct same-realm predicate, but no production character-creation or account-profile consumer invokes it. |
| Commitment presentation | Changes required | The first realm-card click immediately calls `TrySelectRealm`; the approved `realm.lock.warning` text is not shown before mutation. |
| Player-facing realm authority | Changes required | Selection cards use `LocalGameDataService` fallback names/descriptions and hard-coded command-profile labels rather than approved catalog/localization content. |
| Failure and recovery copy | Changes required | Selection failure only writes a technical code to the Unity log; no visible localization-backed unavailable/retry state is presented. |
| Downstream realm fidelity | Pass for the Champion slice | Merged PR #346 requires committed-valid realm context, configures Champion actors explicitly, and removes the five audited Crownlands substitutions. |

## Required Corrections

### A3-FR-01: Warn Before Irreversible Commitment

The source key `realm.lock.warning` means:

> This account will be bound to the chosen realm. Future characters on this
> account must belong to the same realm.

The selection UI currently treats the first click as the commit request and
shows `COMMAND ACCEPTED` only after persistence succeeds. Engineering must add
a debounced pre-commit confirmation that identifies the selected realm,
communicates the durable account and sub-character consequence, and permits
cancellation without save mutation. Release UI must resolve the approved key
through the localization boundary rather than duplicate the draft literal in
runtime code.

### A3-FR-02: Enforce Account and Sub-Character Continuity

The merged service proves one committed realm on one current local save. It
does not yet prove an account-wide authority shared by multiple character
profiles, and the same-realm predicate is referenced only by tests.

Before account realm-locking is claimed complete, the character-creation and
sub-character creation path must consume the committed account identity,
reject cross-realm requests before mutation, and preserve editor state on
rejection. Shared storage must use the same authority so no cross-realm profile
or storage path can bypass the lock.

### A3-FR-03: Consume Approved Player-Facing Source

`RealmCatalogRuntime` publishes the approved stable ID and display name, but
the selection UI enumerates runtime fallback definitions. Those definitions
contain separate hard-coded descriptions and perk prose, while the UI adds
unapproved labels such as `FORTRESS ECONOMY` and `GROWTH ENGINE`.

Engineering must route realm name, selection summary, selection line, and
warning/unavailable copy through an approved catalog/localization adapter.
Technical fallback data may remain for safe diagnostics, but it must not
silently become narrative or balance authority.

### A3-FR-04: Resolved Champion Downstream Consumption

Merged PR #346 resolves the audited Champion slice. Missing or invalid committed
realm context now fails closed before arena allocation, while valid context is
configured explicitly on the player, skill caster, boss, and bots. The five
previous Crownlands substitutions are absent from current `main`. This pass is
limited to Champion-mode consumers and does not establish account-wide
character or shared-storage authority.

### A3-FR-05: Present Failure and Retry Meaning

Catalog, save, definition, and different-realm failures currently stop
navigation but report only internal technical codes. The player needs a
visible, localization-ready state that distinguishes retryable unavailability,
an already committed same realm, and a rejected different realm. Copy must not
promise realm transfer, partial reset, or paid recovery that the product does
not support.

## Acceptance for Re-Review

A later A3 fidelity pass can return `pass` when:

- the approved warning appears before the first durable mutation and cancel is
  non-mutating;
- realm presentation resolves from approved source/localization references;
- account identity is authoritative across character creation, same-realm
  sub-characters, and shared storage;
- cross-realm creation is rejected visibly without losing editor state;
- all authoritative consumers fail closed instead of substituting Crownlands;
- unavailable, retry, same-realm, and different-realm outcomes have distinct
  player-facing meaning;
- save/reload and integrated launch evidence confirms the selected realm and
  its narrative identity remain stable;
- the user completes the required irreversible-profile UX playtest and
  approves the release presentation.

## Impact

This disposition adds documentation only. It changes no runtime, catalog,
save, scene, asset, dependency, performance, memory, package-size,
install-size, or device-compatibility behavior. No shared file is touched.
