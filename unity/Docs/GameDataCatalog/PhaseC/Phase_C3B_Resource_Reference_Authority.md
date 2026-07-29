# Phase C3B Resource Reference Authority

## Document control

| Field | Value |
| --- | --- |
| Tracked issues | `#183`; related economy contract `#163` |
| Phase | `Phase C3B — resource-reference convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `181703ab06f76e0935fc36bbcd33c9cfad9dca58` |
| Binding specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Economy contract | `unity/Docs/Economy_Integrity_Spec.md` |
| Phase C2 technical source | `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source.json` |
| Phase C3A realm decision | `unity/Docs/GameDataCatalog/PhaseC/Phase_C3A_Realm_Authority_Convergence.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Production eligibility | Blocked |
| User balance, activation, playtest, and release approval | Pending |

This decision defines one exact technical reference vocabulary for the ten
resources already accepted by `ResourceRules.WalletResources`. It allows the
four future common realm records to carry deterministic `rare_resource_id`
values without deriving identity from enum names or inventing balance data.

This document is source and coordination evidence only. It does not create a
resource catalog, change the six-family production set, alter a save value, or
make the realm family or issue `#183` complete.

## 1. Scope and non-goals

This phase decides:

- one canonical lower-snake-case stable ID for every supported wallet
  resource;
- the exact mapping between each stable ID, current `ResourceType` name,
  current numeric value, wallet order, and core or optional-rare
  classification;
- the four exact realm-to-rare-resource stable references;
- strict resolution, rejection, provenance, and future migration behavior;
- the minimum future schema/generator work required to validate
  `rare_resource_id`.

This phase does not:

- emit a resource artifact, `catalog-set.json`, or any other production bytes;
- add a seventh production family to the six-family catalog set;
- edit `ResourceType`, `ResourceRules`, `GameDataSixFamilySchemas`, the Phase
  C2 JSON source, services, saves, scenes, UI, tests, packages, or workflows;
- define starting balances, rates, prices, costs, rewards, capacity, rarity
  weights, production profiles, territory values, or conversion rules;
- author display names, abbreviations, localization keys, icons, colors, or
  other presentation copy;
- approve realm capability profiles or resource balance;
- switch runtime authority or claim final user approval.

## 2. Authority form

The accepted authority is a **closed technical reference vocabulary** over the
existing typed wallet authority. It is not a separately authored gameplay or
balance catalog.

`ResourceRules.WalletResources` remains the current supported wallet set and
canonical wallet order. `ResourceRules.IsCoreResource(...)` and
`ResourceRules.IsRareResource(...)` remain the current typed classifications.
This decision adds reviewed stable cross-catalog identities to those existing
types.

A future common realm schema/generator must validate `rare_resource_id`
against an immutable exact registry implementing this table. That bounded
registry may be compiled code or deterministic generated input, but it must:

- expose the exact bidirectional mappings in this document;
- expose a version and pinned source provenance;
- preserve the record order below;
- reject rather than normalize unknown input;
- be the only resolver used for `rare_resource_id`.

The first common realm artifact must use this bounded registry rather than
introducing a seventh family. Elevating resources into an independently
versioned production family would require a separate reviewed schema,
source-of-truth, manifest, migration, and authority-switch decision. It cannot
happen implicitly and cannot silently coexist as a competing ID authority.

## 3. Canonical resource reference set

The record order is exactly the current wallet order:

| Order | Canonical stable ID | Exact legacy enum | Numeric value | Classification |
| ---: | --- | --- | ---: | --- |
| 1 | `food` | `ResourceType.Food` | `0` | Core |
| 2 | `wood` | `ResourceType.Wood` | `1` | Core |
| 3 | `stone` | `ResourceType.Stone` | `2` | Core |
| 4 | `gold` | `ResourceType.Gold` | `3` | Core |
| 5 | `mana_stone` | `ResourceType.ManaStone` | `4` | Core |
| 6 | `ore` | `ResourceType.Ore` | `5` | Core |
| 7 | `deep_ore` | `ResourceType.DeepOre` | `6` | Optional rare |
| 8 | `world_sap` | `ResourceType.WorldSap` | `7` | Optional rare |
| 9 | `royal_sigil` | `ResourceType.RoyalSigil` | `8` | Optional rare |
| 10 | `dark_crystal` | `ResourceType.DarkCrystal` | `9` | Optional rare |

The stable IDs are explicit technical identities. Runtime or generation code
must not recreate them by applying a case conversion, word splitter, naming
policy, reflection rule, or `Enum.ToString()` transformation.

The numeric values are compatibility evidence because `ResourceType` is
persisted in current wallet rows. The current enum declarations are implicit,
but the resulting values `0` through `9` are fixed by this reviewed mapping.
Reordering, inserting, renumbering, or renaming an enum member requires an
explicit save-compatibility and catalog-reference migration before it can
become authority.

There are no aliases in this phase.

## 4. Exact resolution and rejection rules

### 4.1 Stable ID to enum

Resolution accepts only an exact canonical ID from the table and returns the
paired defined, supported `ResourceType`.

The resolver rejects:

- null, blank, or whitespace-only values;
- leading or trailing whitespace;
- case variants such as `Food` or `MANA_STONE`;
- separator variants such as `mana-stone` or `mana stone`;
- enum-shaped values such as `ManaStone` or `ResourceType.ManaStone`;
- display names, localized names, abbreviations, or fuzzy matches;
- unknown IDs and future IDs not present in the accepted registry version.

The resolver must not trim, case-fold, normalize punctuation, parse an enum
name, use a default value, or substitute the first record.

### 4.2 Enum to stable ID

Reverse resolution accepts only one of the ten defined values that is also
present in `ResourceRules.WalletResources`. It returns the exact paired stable
ID from the table.

An undefined numeric enum value is rejected even when it can be cast to
`ResourceType`. Stable unknown numeric rows may continue to round-trip in
serialized save data under the economy integrity contract, but they:

- do not resolve to a stable ID;
- are not treated as supported, core, or rare;
- cannot satisfy a catalog reference;
- are never reinterpreted as numeric `0` or `food`.

If the enum declaration, wallet membership/order, classification helpers, and
this table disagree, validation fails closed. No source wins through fallback.

## 5. Realm rare-resource references

The future common realm records use the following exact stable references:

| Canonical realm ID | Exact realm enum/value | Required `rare_resource_id` | Exact resource enum/value |
| --- | --- | --- | --- |
| `crownlands` | `RealmId.Crownlands` / `3` | `royal_sigil` | `ResourceType.RoyalSigil` / `8` |
| `stonehold` | `RealmId.Stonehold` / `1` | `deep_ore` | `ResourceType.DeepOre` / `6` |
| `eldergrove` | `RealmId.Eldergrove` / `2` | `world_sap` | `ResourceType.WorldSap` / `7` |
| `umbral` | `RealmId.Umbral` / `4` | `dark_crystal` | `ResourceType.DarkCrystal` / `9` |

`ResourceRules.TryGetRareResourceForRealm(...)` is the accepted current
shadow-comparison evidence for these four relations. Its boolean result is
binding: when it returns `false`, the out value is non-authoritative and must
be ignored.

`ResourceRules.GetRareResourceForRealm(...)` is prohibited as generation,
validation, migration, or fallback authority because it maps unsupported realm
values to `RoyalSigil`.

`RealmId.None`, undefined realm values, unknown stable realm IDs, case or
separator variants, and normalized or fuzzy matches never produce a resource
reference. In particular, none of those inputs may resolve to
`royal_sigil`.

These mappings establish identity only. They do not authorize realm production
rates, starting balances, reward amounts, territory bonuses, or gameplay
capabilities.

## 6. Source relationship and precedence

The retained relationship is:

1. `Enums.cs` supplies the currently persisted `ResourceType` members and
   numeric values.
2. `ResourceRules.cs` supplies the supported wallet set, wallet order,
   core/optional-rare classification, and safe four-realm relation.
3. `Economy_Integrity_Spec.md` supplies the accepted malformed-wallet,
   optional-resource, unknown-enum, and no-balance-invention boundaries.
4. The Phase C2 technical source records the future realm
   `rare_resource_id` requirement and the unresolved blocker.
5. The Phase C3A decision fixes future realm record identity/order and accepts
   only the safe rare-resource relation as migration evidence.
6. This C3B decision supplies the stable ID layer that none of those sources
   previously defined.

If an earlier source changes, this decision does not adapt automatically. The
registry and any generated realm artifact remain blocked until a reviewed
revision reconciles the changed name, value, order, classification, or
relation.

Player-facing resource meaning remains outside this technical vocabulary.
Enum names and stable IDs are not approved display copy or localization keys.

## 7. Phase C2 blocker disposition

| Blocking ID | C3B disposition | Evidence and required next step |
| --- | --- | --- |
| `realms.rare_resource_catalog` | **Identity source resolved; implementation gate pending** | The ten stable resource IDs and four realm references are now exact and balance-neutral. The current Phase C2 schema still treats `rare_resource_id` as an unqualified stable reference, and no immutable registry/resolver implements this decision. A later engineering phase must add the bounded validator, its tests, version/provenance capture, and update the technical blocker record before generation. |
| `realms.capability_profiles` | **Open, unchanged** | This phase supplies no capability/profile IDs or balance. A separate source and user balance decision remains required. |
| Realm schema world-boundary fields | **Open, unchanged** | Phase C3A requires exact inner-realm, main-gate, and outer-warzone fields that the current schema does not represent. |

The realm family remains `artifactDisposition: blocked_required` and
`productionEligible: false`. This document narrows one source blocker; it does
not remove the blocker from the committed Phase C2 JSON, generate an artifact,
or authorize runtime publication.

## 8. Required future engineering handoff

Before a future common realm artifact can cite these references, engineering
must:

1. implement one immutable exact resource-reference registry from this table;
2. expose typed exact ID-to-enum and enum-to-ID resolution with observable
   rejection;
3. validate all ten enum definitions, numeric values, wallet positions, and
   classifications during registry construction or focused validation;
4. validate all four realm relations through the rejecting
   `TryGetRareResourceForRealm(...)` path;
5. make `rare_resource_id` validation use only that registry;
6. add focused tests for every canonical value and every rejection class in
   Section 4;
7. record registry version, this decision's merged revision, source revisions,
   and raw hashes in generator provenance;
8. update the Phase C2 technical source and ordered diagnostics only after the
   implementation exists and passes;
9. keep the realm artifact blocked on capability profiles and the Phase C3A
   schema extension;
10. perform the complete Phase C3A shadow-comparison gate before any authority
    switch.

Tests must include `ResourceType` values below `0` and above `9`, `RealmId.None`,
an undefined realm value, all case/separator/whitespace variants, and proof
that no rejection becomes `food` or `royal_sigil`.

## 9. Pinned provenance

Hashes below are lower-case SHA-256 over the exact committed file bytes visible
at the audited current main. Future generation must hash committed raw bytes,
not newline-normalized or reserialized content.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `unity/Assets/AL/Scripts/Core/Enums/Enums.cs` | `15b8712c83e5ddac218a86d2cadcd8ed517a1434` | `36e3c430d97c39ca6f487b1a682353f157d808052de28919d766b2bba6190d4a` | Current exact resource enum names and numeric values |
| `unity/Assets/AL/Scripts/Core/ResourceRules.cs` | `ca435003168d1013a863850ea09f42b2d4dd6c5b` | `26a907e49afeecd8c741c6d9ed9bd12549a735a3798184509ede001e551bf087` | Supported order, classification, and safe/fallback realm behavior |
| `unity/Docs/Economy_Integrity_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d12099838d1afb626520a3e0c6e44d8d6ffeeb7f7ccdc4681256b3dc563a2ca5` | Accepted wallet, optional-resource, unknown-enum, and balance boundaries |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataSixFamilySchemas.cs` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `7e7a791f011dca7bd755dc305e060c7a451418877ae5a9cf79492437047f70aa` | Current required realm `rare_resource_id` field without a resource family binding |
| `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Current observed realm/resource anchors and blocker record |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C3A_Realm_Authority_Convergence.md` | `27e2d477ad98f131593b991c85ce8388d9216641` | `4d220976127f1c80c407a42ce987aa50a38dbee268ecc7a80258f3178c77925f` | Accepted future realm identity/order and safe rare relation |
| `unity/Docs/Game_Data_Catalog_Authority_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d8a0e2fdcd4e98bbb6379a8f2b7d7c733f869bd95f99112e1635f25dafb7b74d` | Stable-ID, provenance, reference, and no-fallback rules |

A later implementation or generated artifact must record:

- the eventual merged revision of this decision;
- every consumed source path, revision, and raw hash above;
- registry/schema/generator revision and deterministic command;
- generated artifact raw hash when an artifact exists;
- explicit user-approval state separate from merge and hosted-check state.

Any pinned byte change, enum/order/classification mismatch, or source-revision
drift fails validation until a reviewed update supersedes this decision.

## 10. Rollback and approval boundary

Because this phase changes documentation only, rollback is removal or
supersession of this decision before an implementation consumes it. A later
implementation must keep its own registry/schema change independently
revertible and must not require save rewriting merely to withdraw an
unpublished realm candidate.

No user creative or balance approval is required to preserve the existing ten
technical resource identities in this coordination document. User approval is
still required for any new balance, rate, capability profile, player-facing
copy, production publication, integrated playtest acceptance, or release
decision.

Merge status, local validation, or a green hosted check does not equal runtime
activation or final approval.

## 11. C3B acceptance checklist

- [x] Ten supported wallet resources have exact stable IDs.
- [x] Exact enum names, numeric values, wallet order, and classifications are
  pinned.
- [x] Four future realm `rare_resource_id` values are exact.
- [x] Unknown, undefined, normalized, case-variant, and fallback identity is
  rejected.
- [x] Save-compatible unknown numeric rows remain preserved but cannot resolve.
- [x] No aliases, balance, copy, rates, profiles, or production artifact are
  introduced.
- [x] The relationship to `ResourceRules`, the economy contract, Phase C2, and
  Phase C3A is explicit.
- [x] Exact source revisions and raw hashes are pinned.
- [x] Future resolver/schema/test/provenance work is explicit.
- [x] Runtime authority and user approval remain unchanged.
