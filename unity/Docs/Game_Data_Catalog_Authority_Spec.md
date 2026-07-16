# Game-Data Catalog Authority and Immutable Query Specification

**Status date:** 2026-07-15  
**Tracking issue:** #183  
**Specification owner:** GPT  
**Implementation owner:** Codex engineering mode  
**Narrative/content source owner:** Codex narrative/content mode  
**Terrestrial source owner:** Codex terrestrial-design mode  
**Final product/creative approval:** user  
**Audited baseline:** `12f33e9a57f4740af0a907a5e460f18dadef1b61`  
**Validated Unity target:** `2022.3.62f3`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`  
**Required upstream:** #156 trusted QuestDefinition/serialized-asset authority  
**Shared implementation file:** `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`

## 1. Goal

Create one explicit, versioned, deterministic, strictly validated technical authority for AnotherLife game definitions and expose it through immutable, typed, pure runtime queries.

The authority must make these states distinguishable rather than returning `null` or silently creating fallback content:

- valid record found;
- unknown stable ID;
- intentionally optional record absent;
- catalog not loaded;
- catalog unavailable;
- catalog malformed;
- unsupported catalog version;
- catalog/source hash mismatch;
- record invalid or blocked by a missing reference;
- legacy alias resolved;
- explicit development fallback in use.

This specification prevents technical services from becoming a competing narrative, visual-design, balance, or product authority. It does not approve new content, rename player-facing concepts, rebalance values, activate archived narrative, or authorize runtime use of unapproved source-design packets.

## 2. Binding decisions

1. **`LocalGameDataService` is not itself the authored source.** It becomes a validated immutable query service over one catalog-set snapshot.
2. **Runtime-created mutable `ScriptableObject` fallbacks are not production authority.** Production queries never create definitions or state.
3. **Catalog families may remain separate physical files.** One catalog-set manifest selects them deterministically and prevents competing silent sources.
4. **Every packaged catalog has an explicit identity and version.** Parse success alone is not acceptance.
5. **Production has no silent fallback.** Missing, invalid, unsupported, or hash-mismatched required data makes the affected catalog unavailable.
6. **Development fallback is explicit and observable.** It is allowed only in Editor/development contexts, reports `DevelopmentFallback`, and cannot be cited as production validation.
7. **Runtime queries return immutable snapshots and typed outcomes.** They never expose mutable `ScriptableObject` instances, backing arrays, dictionaries, or save objects.
8. **Queries are pure.** They do not load, create, normalize, migrate, save, reorder, or mutate definitions or player state.
9. **Player-facing names, descriptions, lore, dialogue, quest meaning, realm identity copy, and localization keys belong to Codex narrative/content source plus user approval.** Engineering validates references but does not author replacement prose.
10. **Terrestrial concept/profile source belongs to Codex terrestrial-design plus user approval.** PR #217 IDs, labels, biome tags, and images remain source-review intent until its own gates and a separate runtime integration pass.
11. **Technical schema, loaders, validators, immutable query APIs, generated artifacts, hashes, packaging, and tests belong to Codex engineering after this specification.**
12. **Numeric balance does not change in #183.** Existing values may be migrated only byte/value-equivalently unless a separate approved balance issue changes them.
13. **Stable IDs are opaque technical identities, never display strings by implication.** New IDs use lower snake case; existing IDs are preserved or migrated only through explicit alias/migration tables.
14. **Aliases are observable.** Alias resolution returns the canonical ID and diagnostic; it is never a silent case-insensitive or whitespace-normalized match.
15. **Catalog validation is whole-snapshot.** A required-family error blocks that family; required cross-family reference failure blocks the catalog set when safe partial use cannot be proven.
16. **Unknown fields or records are handled only through declared schema/version policy.** A parser may not ignore drift and still report a fully valid catalog.
17. **Packaged source hashes are verified from raw UTF-8 bytes.** A manifest cannot claim an artifact it did not load.
18. **The catalog service has an explicit lifecycle.** Before `Ready`, queries report pending/unavailable status rather than using fallback definitions.
19. **Bootloader integration does not enter PR #203’s locked file.** Catalog foundation may proceed separately; runtime publication/lifecycle integration occurs only after the active `Bootloader.cs` lock is released or through a separately approved non-overlapping seam.
20. **#156 completes before production quest/chapter authority migration.** This document may merge as a specification; no #183 implementation may claim trusted quest assets before #156 passes.
21. **#137 owns player-save candidate selection and repair.** #183 validates definition availability and references; it does not repair player state.
22. **A green compile is not catalog acceptance.** Required evidence includes malformed, missing, unsupported, duplicate, reference, packaging, reload, lifetime, and Player-build cases.

## 3. Verified current baseline

### 3.1 `IGameDataService`

The current interface exposes:

```text
GetRealm(RealmId)
GetAllRealms()
GetBuilding(string)
GetTroop(string)
GetChampion(string)
GetSkill(string)
```

Every result is nullable or a live enumerable. It exposes no:

- load/readiness state;
- catalog identity/version/provenance;
- typed failure reason;
- research lookup;
- chapter/quest/boss/equipment lookup;
- immutable snapshot contract;
- diagnostic inventory.

### 3.2 `LocalGameDataService`

The constructor synchronously executes:

```text
InitializeFallbackData()
InitializeAutomatedContent()
InitializeStoryData()
```

It creates mutable runtime `ScriptableObject` definitions and stores some in dictionaries. Other created objects are discarded.

Verified current behavior:

- realm definitions are generated in memory and returned directly;
- building definitions are generated in memory and returned directly;
- research defaults are stored privately and cannot be queried through `IGameDataService`;
- chapters are instantiated and discarded;
- sixteen skill-soul quests are instantiated and discarded;
- `GetTroop`, `GetChampion`, and `GetSkill` always return `null`;
- dictionary assignment silently overwrites duplicate keys;
- no catalog/source/version/hash/load result exists.

### 3.3 Current family inventory

| Family | Current technical source | Current runtime exposure | Verified risk | Owning migration/review |
| --- | --- | --- | --- | --- |
| Realms | Four runtime-created `RealmDefinition` objects | `GetRealm`, live `GetAllRealms` values | mutable definitions, hard-coded copy/perks, no provenance, undefined/`None` ambiguity | #183 foundation; #173 selection; narrative/content review |
| Buildings | Fifteen runtime-created `BuildingDefinition` objects | `GetBuilding` | display-name derivation, fixed max 10, duplicate overwrite, consumers reference absent IDs | #183 then #165 |
| Research | Eight private `ResearchState` defaults using display strings as IDs | no game-data query | state mixed with definition, no max/effect catalog, Android ID mismatch, query-created save state | #183 then #165 |
| Troops | `TroopDefinition` type exists | `GetTroop` always `null` | training has no authoritative definition or supported-type result | #183 then #165 |
| Champions | `ChampionDefinition` type exists | `GetChampion` always `null` | no authoritative record, mutable base-skill array, hard-coded runtime paths | #183 then #180 |
| Skills | `SkillDefinition` type plus separate loadouts/hard-coded slots | `GetSkill` always `null` | competing authorities, no behavior/presentation separation, partial fallback | #183 foundation; #180 complete validation |
| Bosses | `BossDefinition` type and runtime controller data | no game-data query | mutable tuning, loot-table references, fallback boss behavior | shared envelope; #168/#180 implementation |
| Equipment | `EquipmentDefinition` type and boss loot lists | no game-data query | mutable definitions, unstable/fallback IDs, no provenance/version | shared envelope; #168 implementation |
| Chapters | runtime-created `ChapterDefinition` objects, then discarded | none | hard-coded broad chapter/lore authority, conflicting IDs, no retained graph | narrative/content source; #128/#133 after #156 |
| Quests | authoritative Unity type direction under #156; local services also create prototype definitions | no central immutable query | asset identity still gated, runtime construction, reward/reference drift | #156 then #128/#133/#152 |
| Side quests | `SideQuestDefinition` type; side-quest service has no validated catalog | none | arbitrary IDs can become operational; player-facing copy in technical shape | narrative/content + #152/#183 |
| Skill-soul quests | sixteen runtime-created objects, then discarded | none | hard-coded copy, chapter/skill references unresolved, object lifetime leak risk | narrative/content; #183/#180 later |
| Customization | StreamingAssets JSON plus hard-coded controller defaults | separate nullable loader/controller path | version ignored, mutable arrays, silent fallback, async downgrade overwrite | common loader result pattern; #184 implementation |
| Terrestrials | PR #217 design source packet | no approved runtime catalog | source may be mistaken for spawn/gameplay authority | user approval then separate #183/runtime integration |

### 3.4 Verified current IDs and gaps

Current generated building IDs:

```text
TownHall
Farm
LumberMill
Quarry
GoldMine
Barracks
Academy
Market
Storehouse
Forge
Stable
Workshop
Embassy
Wall
Watchtower
```

Current production/resource code also references:

```text
ManaShrine
Mine
```

Those two IDs are not present in the current game-data service. #183 must not silently invent definitions, values, maximum levels, names, or localization for them. Until an approved source defines them, affected consumers report unavailable/invalid definition state.

Current private research IDs:

```text
Steel Forging
Plate Armor
Advanced Masonry
Irrigation
Ballistics
Logistics
Trade Routes
Arcane Study
```

These are display strings used as technical identity. Android/shared sources use forms such as `steel_forging`. The foundation must preserve current saves and behavior while introducing an explicit canonical-ID/legacy-alias decision. Do not rename persisted research rows by string replacement.

Current generated story inventory includes:

- 29 chapter objects: four records for each of C1, C2, C3, C7, C10, C11, and C12, plus `C_OMEN`;
- 16 skill-soul quest objects;
- all are discarded after construction and are not a valid runtime catalog.

The archived chapter/quest material is historical source, not approved NVS-01 A1 or production content.

## 4. Authority model

### 4.1 Common catalog set

Production loads one catalog-set manifest. The set names every packaged family artifact, its role, requiredness, version, and immutable hash.

Recommended path:

```text
unity/Assets/StreamingAssets/GameData/catalog-set.json
```

Recommended family paths:

```text
unity/Assets/StreamingAssets/GameData/Catalogs/realms.v1.json
unity/Assets/StreamingAssets/GameData/Catalogs/buildings.v1.json
unity/Assets/StreamingAssets/GameData/Catalogs/research.v1.json
unity/Assets/StreamingAssets/GameData/Catalogs/troops.v1.json
unity/Assets/StreamingAssets/GameData/Catalogs/champions.v1.json
unity/Assets/StreamingAssets/GameData/Catalogs/skills.v1.json
```

The exact physical split may change when the inventory proves another retained source is safer. The logical rules do not change:

- one selected artifact per family;
- no implicit secondary production source;
- no hard-coded content merged into a partially loaded catalog;
- manifest order is deterministic and retained in diagnostics;
- every loaded artifact is hash-verified before publication.

Boss, equipment, chapter, quest, customization, world, and terrestrial catalogs may join later through separate reviewed family entries. Their absence from the first foundation cannot be represented as implemented support.

### 4.2 Catalog-set manifest contract

Equivalent fields are required:

```text
gameId
catalogSetId
schemaVersion
contentVersion
minimumRuntimeCatalogVersion
sourceRevision
artifacts[]
```

Each artifact entry includes:

```text
family
catalogId
relativePath
schemaVersion
contentVersion
required
sha256
mediaType
sourceMode
sourceRevision
```

Rules:

- `gameId` is exactly the reviewed AnotherLife identifier;
- manifest `schemaVersion` is a positive supported integer;
- `catalogSetId`, catalog IDs, and family values are unique and nonblank;
- paths are relative, normalized, remain under the approved packaged root, and cannot contain traversal;
- SHA-256 is lower-case 64-character hex over the raw packaged bytes;
- required artifacts must all load and validate before the set becomes `Ready`;
- optional absence is explicit and queryable;
- duplicate family selection is invalid;
- manifest and artifact ordering is deterministic;
- source revision identifies the authored/generated input commit or packet, not merely the build date.

### 4.3 Family catalog envelope

Each family file contains equivalent fields:

```text
gameId
catalogId
family
schemaVersion
contentVersion
sourceRevision
records[]
```

A family catalog does not self-authorize its player-facing meaning. Narrative/localization/design provenance belongs in approved source references and manifest metadata.

### 4.4 Source ownership by field

| Field kind | Authority |
| --- | --- |
| stable technical ID, schema, version, references, enum/profile IDs, packaging, hash | Codex engineering after GPT specification |
| player-facing name, title, description, lore, quest text, realm identity copy, localization key mapping | Codex narrative/content plus user approval where required |
| terrestrial silhouette, anatomy, scale, materials, motion, design variants | Codex terrestrial-design plus user approval |
| balance/tuning values | existing approved value or separately approved balance issue/user decision |
| save migration and compatibility | GPT specification + Codex engineering under #137/owning issue |
| final product/creative/release acceptance | user |

Engineering may copy an already approved source value into a generated artifact. It may not improvise missing copy, lore, names, visual meaning, spawn behavior, or balance.

## 5. Stable ID and alias policy

### 5.1 New IDs

New technical string IDs use:

```text
^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

They are:

- case-sensitive ordinal strings;
- stable after publication;
- never generated from display text at runtime;
- never trimmed, lowercased, or guessed by a query;
- unique within their family;
- family-qualified through the query type rather than ad hoc prefixes where possible.

### 5.2 Existing IDs

Existing persisted and runtime-referenced IDs are not silently renamed.

For each family, the migration record must classify every current ID as:

```text
canonical unchanged
canonicalized through explicit migration
legacy alias retained
invalid/unapproved consumer reference
```

An alias table includes:

```text
legacyId
canonicalId
introducedVersion
retirementVersion or null
migrationIssue
```

Alias rules:

- exact ordinal match only;
- no alias chains or cycles;
- one legacy ID maps to exactly one canonical ID;
- aliases cannot shadow another canonical ID;
- query result reports `AliasResolved` and both IDs;
- save migration is performed only by the owning migration path, not by a read query;
- display strings are not accepted as aliases merely because they resemble a current label.

### 5.3 Enum-backed identities

For `RealmId`, `TroopType`, class, slot, target, resource, and similar enums:

- require `Enum.IsDefined` or equivalent supported-value validation;
- reject sentinel `None` when the family contract requires a committed identity;
- unknown serialized numeric values are not reinterpreted as zero/default;
- catalog records use the reviewed enum name/value plus a stable technical ID where cross-platform contracts require one;
- enum/string mapping is deterministic and tested in Android/shared consumers when applicable.

## 6. Typed load and query contracts

Names may vary, but equivalent immutable contracts are required.

### 6.1 Service lifecycle

```text
Uninitialized
Loading
Ready
ReadyWithOptionalGaps
DevelopmentFallback
Unavailable
Invalid
UnsupportedVersion
Disposed
```

Required service-state data:

```text
status
catalogSetId
schemaVersion
contentVersion
sourceRevision
sourceKind
loadedArtifactIds
missingOptionalFamilies
diagnostics
startedAtUtc
completedAtUtc
```

Production gameplay may use definitions only in `Ready` or a specifically approved `ReadyWithOptionalGaps` state whose missing families are irrelevant to the operation.

`DevelopmentFallback` is never equivalent to `Ready` for release evidence.

### 6.2 Load status

```text
LoadedPackaged
LoadedDevelopmentFallback
MissingManifest
MissingArtifact
ReadFailed
MalformedJson
InvalidEnvelope
UnsupportedVersion
HashMismatch
InvalidRecord
CrossReferenceFailure
Cancelled
TimedOut
Disposed
```

The load result contains ordered diagnostics and does not throw for expected invalid input.

### 6.3 Query status

```text
Found
AliasResolved
OptionalAbsent
UnknownId
CatalogPending
CatalogUnavailable
CatalogInvalid
UnsupportedVersion
RecordInvalid
ReferenceUnavailable
```

A query result contains:

```text
status
family
requestedId
canonicalId when known
immutable value when found
catalogId
contentVersion
sourceRevision
diagnostics
```

### 6.4 Legacy compatibility methods

Current nullable methods may remain temporarily only to keep consumers compiling:

```text
GetRealm
GetAllRealms
GetBuilding
GetTroop
GetChampion
GetSkill
```

Requirements:

- implement them as pure wrappers over typed queries;
- return detached immutable-compatible values or `null` only at the legacy boundary;
- never create content or state;
- never hide a fallback as a valid record;
- mark or document them as migration-only;
- inventory every caller and owning migration issue;
- add no new integrity-sensitive caller to a nullable legacy method.

The final interface uses typed family queries and immutable enumerations.

## 7. Immutable runtime snapshots

### 7.1 General rule

Runtime definitions are plain immutable data or immutable interfaces. They are not live `ScriptableObject` instances.

Every snapshot:

- copies scalar values;
- copies arrays/lists into read-only collections;
- copies nested records recursively;
- exposes no mutable dictionary/list/array backing store;
- contains stable ID, catalog/version, and source revision;
- uses deterministic equality appropriate for tests;
- cannot mutate the loaded catalog through casting.

Unity object references such as `Sprite` are represented by validated stable asset references/addresses in the catalog snapshot. Loading a presentation asset is a separate result and must not mutate the definition.

### 7.2 Catalog-set publication

Build the complete candidate snapshot off to the side:

```text
read manifest
→ verify paths/hashes
→ parse family files
→ validate envelopes/records
→ validate IDs/aliases
→ validate cross-references
→ construct immutable snapshots
→ run final whole-set validation
→ publish one snapshot reference
```

Failure before final publication leaves the previous valid snapshot unchanged. Initial failure leaves the service unavailable rather than partially populated.

Reload, if supported, follows the same prepare/verify/publish model and increments a catalog revision. Queries in progress retain either the old or new immutable snapshot, never a partially changed mixture.

## 8. Loading and platform behavior

### 8.1 Packaged source

The offline production baseline loads only packaged repository-controlled artifacts. No network is required.

The loader preserves the useful current cross-platform seam:

- direct file read where StreamingAssets is a normal file path;
- `UnityWebRequest` or equivalent packaged-resource read where required by the platform;
- identical bytes feed the same parser, hash check, and validator;
- both paths return the same typed load statuses.

### 8.2 Timeout, cancellation, and disposal

- every asynchronous/platform request has a bounded timeout;
- cancellation is explicit during scene/application shutdown;
- a late callback cannot publish into a disposed or replaced service;
- loader exceptions become typed diagnostics;
- optional diagnostic logging does not expose private local paths in player-facing output.

### 8.3 Initialization integration

Do not start asynchronous catalog loading from a constructor that cannot report or await completion.

Use a narrow initialization boundary such as:

```text
IGameDataCatalogLoader.Load(...)
→ GameDataCatalogLoadResult
→ LocalGameDataService.Publish(snapshot)
```

The first catalog-foundation PR may implement loader/validator/snapshot/query behavior without editing `Bootloader.cs`. Runtime service-stack integration occurs after PR #203 releases its lock or through a separately reviewed initialization dependency.

Consumers must tolerate `CatalogPending`/unavailable results without substituting Crownlands, prototype definitions, or hard-coded content.

## 9. Validation contract

### 9.1 Manifest/envelope validation

Reject/report:

- missing or wrong `gameId`;
- blank/duplicate catalog IDs;
- missing/zero/unsupported schema version;
- blank content/source version;
- duplicate family entries;
- invalid requiredness;
- path traversal, absolute path, or path outside approved root;
- missing file;
- malformed SHA-256;
- hash mismatch;
- media type mismatch;
- manifest/artifact identity mismatch;
- nondeterministic duplicate selection.

### 9.2 Record validation

Every family validator rejects/reports:

- null record;
- blank ID;
- duplicate canonical ID;
- alias collision/cycle;
- invalid enum/profile ID;
- missing required field;
- unsupported unknown field under the current strict-version policy;
- non-finite numeric value;
- out-of-range or contradictory numeric value;
- missing required localization/content reference;
- missing asset reference;
- missing behavior/profile reference;
- unresolved cross-family reference;
- duplicate generated asset identity;
- wrong source revision/provenance where generated artifacts are involved.

Diagnostics contain:

```text
code
severity
catalogId
family
recordId
fieldPath
messageKey or technical message
action
blocksFamily
blocksCatalogSet
```

Ordering is deterministic by manifest order, record order/canonical ID, field path, and code.

### 9.3 Schema synchronization

Where a JSON schema, Fable contract, Android model, generated C# model, or other duplicate shape exists:

- choose one source contract;
- generate consumers deterministically or validate drift in CI/editor tests;
- execute schemas as tests rather than treating them as documentation;
- fail when generated artifacts differ from committed output;
- record source-to-generated paths and hashes.

Do not keep a schema, Fable type, Kotlin type, and C# DTO silently divergent.

## 10. Family-specific requirements

### 10.1 Realms

Required technical fields include equivalent:

```text
id
realmId
localization/display references
rareResourceId or profile reference
technical capability/profile references
asset references
```

Rules:

- exactly one record per supported production realm;
- `RealmId.None` is not a selectable realm definition;
- undefined enum values fail validation;
- query of `None`/undefined returns a typed invalid/unknown result;
- no silent Crownlands fallback;
- player-facing realm names/descriptions/perks remain source-mode/localization data;
- current numeric perks are not changed by #183;
- #173 consumes the immutable realm result and version/provenance.

### 10.2 Buildings

Required fields include equivalent:

```text
id
localization reference
maxLevel
technical production/profile references
cost/duration profile references where later approved
asset reference
```

Rules:

- current valid IDs/values are preserved until an approved migration;
- `maxLevel` is positive and bounded;
- duplicate IDs fail;
- display name is not generated by string replacement;
- read query never creates a `BuildingState`;
- saved state and definition remain separate;
- `ManaShrine` and `Mine` remain unavailable until explicitly defined by approved source/tuning; they are not auto-added in #183;
- #165 validates state/cost/timer against the immutable definition.

### 10.3 Research

There is currently no dedicated research definition/query. #183 adds one.

Required fields include equivalent:

```text
id
legacyAliases
localization reference
maxLevel
cost profile
duration profile
effect profile references
prerequisite research IDs
```

Rules:

- current display-string IDs are inventoried and mapped explicitly;
- new technical IDs are not inferred from display text at runtime;
- aliases are exact and observable;
- prerequisite graph is acyclic;
- max level and all numeric/profile references validate;
- `GetStatBonus` later consumes a validated effect profile rather than hard-coded display-string lookup;
- query never creates `ResearchState`;
- #165 owns state mutation and balance-preserving migration.

### 10.4 Troops

Required fields include equivalent:

```text
id
troopType
localization reference
baseAttack
baseDefense
technical training/profile references
asset reference
```

Rules:

- one supported record per production troop identity where required;
- enum values are defined and unique;
- finite/checked bounded stats;
- missing catalog/record makes training unavailable;
- no fallback definition is created from the enum name;
- #165 validates training destination/cost/count against the definition.

### 10.5 Champions

Required fields include equivalent:

```text
id
realmId
classFamily
localization reference
portrait/model references
baseSkillIds
technical stat/profile references
```

Rules:

- arrays become immutable ID lists;
- realm/class values are defined;
- every skill reference resolves in the same accepted catalog set;
- missing/invalid champion data makes the selection/encounter unavailable;
- no runtime-created placeholder is represented as production content;
- #180 owns finite combat validation and encounter behavior.

### 10.6 Skills

Separate technical behavior from presentation:

```text
id
behaviorProfileId
presentationProfileId
localization reference
targetType
cooldown
power
mana/cast/range fields when supported
vfx/audio asset references
```

Rules:

- stable skill ID is not a slot index;
- behavior profile is not a VFX key;
- required slots and optional-slot policy are explicit;
- duplicate/missing slot/ID fails validation;
- every numeric value is finite and within a reviewed range;
- a partial external catalog is not silently combined with hard-coded slots and reported as valid;
- #180 completes action lifecycle and gameplay-range validation.

### 10.7 Bosses and equipment

The common envelope/query/result model applies, but full production migration remains in #168/#180.

- no invalid request or missing table creates fallback loot;
- stable boss/equipment IDs and references are required;
- drop rates and stats validate without silent clamp;
- loot-table item references resolve;
- player-facing names/lore remain narrative/content source;
- no boss/equipment migration is bundled into the first six-family #183 foundation.

### 10.8 Chapters and quests

- #156 controls Unity QuestDefinition type/GUID/schema/asset safety;
- #128 supplies approved A1 narrative source;
- #133 specifies runtime state/contract/reference behavior;
- #183 supplies common catalog envelope/query/provenance mechanics only;
- current runtime-created broad chapters and skill-soul quests are not production authority;
- archived file presence does not activate content;
- no technical catalog PR authors replacement chapter/quest copy;
- chapter/quest graph validation requires unique IDs, resolved dialogue/objective/reward/chapter references, and approved source revision.

### 10.9 Customization

`CharacterCustomizationCatalog` may reuse the common load/result/hash/version/diagnostic pattern, but #184 owns its exact families, async save compatibility, future-ID preservation, and apply/commit ordering.

#183 must not normalize customization state or change appearance options.

### 10.10 Terrestrials

PR #217 source artifacts may become inputs only after:

```text
technical source-packet review complete
→ user creative approval
→ immutable source hashes/schema/provenance complete
→ #156 trusted asset baseline
→ #183 catalog family/integration specification
→ owning world/spawn/runtime issue
```

Until then:

- working display labels are non-localized review labels;
- profile/variant IDs are source-design IDs, not runtime spawn IDs;
- biome tags are design-intent tags, not production catalog references;
- no AI, spawn, combat, reward, save, or narrative behavior is inferred.

## 11. Fallback and unavailable behavior

### 11.1 Production

When required catalog data is unavailable or invalid:

- do not create fallback ScriptableObjects;
- do not substitute another realm/record;
- do not seed player state;
- do not continue a mutation requiring the definition;
- return typed unavailable/invalid status;
- keep last previously accepted immutable snapshot only when a reviewed hot-reload policy explicitly permits it;
- surface technical/player-safe unavailable feedback through #177 where needed.

### 11.2 Editor/development

A development fallback may exist only when:

- compiled or configured out of normal release authority;
- source and values are deterministic and versioned;
- status is `DevelopmentFallback`;
- diagnostics identify every fallback family;
- tests prove a Player/release build cannot report it as packaged production data;
- no real profile migration/save is based on fallback-only content unless an isolated test profile is used.

### 11.3 Optional content

Optional absence differs from failure:

- optional family/record is declared optional in the manifest/schema;
- query returns `OptionalAbsent`;
- the caller must have an explicit no-content path;
- absence does not authorize a fabricated reward, placeholder definition, or unrelated fallback.

## 12. Migration from current runtime fallbacks

### 12.1 Inventory freeze

Before changing authority, record:

- every current generated realm/building/research/chapter/soul-quest value;
- every current definition type and `CreateAssetMenu` path/GUID;
- every consumer lookup ID;
- every StreamingAssets catalog/schema/Fable/Kotlin/C# path;
- every hard-coded skill/loadout/world/customization source;
- every current saved-state ID/type that references definitions;
- every generated/imported asset path and provenance.

The inventory becomes a retained Markdown/JSON artifact with source commit.

### 12.2 Shadow validation

Before switching consumers:

1. load the proposed catalog set;
2. validate it fully;
3. compare every current approved ID/value against the current runtime baseline;
4. report exact differences;
5. reject unapproved content/balance drift;
6. run queries without publishing to gameplay;
7. prove deterministic hash and ordering across two clean runs.

### 12.3 Authority switch

Only after accepted shadow evidence:

- publish immutable catalog snapshot;
- make `LocalGameDataService` query it;
- remove or compile-gate production runtime-created fallback source;
- migrate consumers family by family;
- keep explicit legacy wrappers temporarily;
- do not delete old source until every persisted ID/reference migration and rollback path is proven.

### 12.4 Rollback

Rollback restores the previous catalog set and service implementation without rewriting player saves.

Requirements:

- previous catalog artifact hashes retained;
- aliases/migrations are backward-aware;
- a newer unsupported catalog does not overwrite or reinterpret save state;
- rollback cannot silently map unknown IDs to defaults;
- source-mode content changes have their own rollback/provenance record.

## 13. Implementation sequence and locks

### Phase A — merged specification

This document only. No runtime, content, asset, save, scene, or catalog change.

### Phase B — catalog foundation

Branch:

```text
codex/game-data-catalog-foundation
```

Expected scope:

- common manifest/envelope models;
- typed load/query/diagnostic results;
- pure manifest/family validators;
- immutable snapshot types;
- packaged-source abstraction with file/UnityWebRequest seams;
- deterministic schema/hash tests;
- current-source/consumer inventory record.

Default prohibition:

- do not edit `Bootloader.cs`;
- do not switch `LocalGameDataService` yet if #156 or the shared-file lock sequence is incomplete;
- do not author family content or balance.

### Phase C — six-family technical catalog source

Use separate source-mode and engineering PRs when player-facing fields are involved.

- Codex narrative/content supplies or approves localization/content references without broad lore rewrites;
- Codex engineering encodes exact approved existing technical IDs/values and validators;
- user approves any unresolved creative/product/balance decision;
- generated artifacts retain provenance and hashes.

### Phase D — `LocalGameDataService` migration

Prerequisites:

- #156 accepted;
- Phase B accepted;
- catalog source accepted;
- `LocalGameDataService.cs` lock declared;
- PR #203/Bootloader integration path no longer conflicts.

Scope:

- replace runtime-created authority with immutable snapshot queries;
- add typed queries and legacy wrappers;
- add service readiness/provenance;
- remove silent production fallback;
- prove lifecycle/object-count stability.

### Phase E — consumer migrations

Separate focused PRs:

```text
#173 realm
#165 building/research/troop
#180 champion/skill
#168 boss/equipment
#184 customization
#181 world/terrestrial integration
#128/#133 chapter/quest
```

No family migration may silently absorb another issue’s state, save, reward, combat, narrative, or scene behavior.

## 14. Expected file boundaries

Phase B likely adds:

```text
unity/Assets/AL/Scripts/Core/Interfaces/GameData/**
unity/Assets/AL/Scripts/Data/Catalogs/**
unity/Assets/AL/Scripts/Services/GameData/**
unity/Assets/AL/Tests/EditMode/GameDataCatalog/**
unity/Docs/Game_Data_Source_Inventory.md
schema/contract files in the existing approved schema location
```

Phase C may add:

```text
unity/Assets/StreamingAssets/GameData/catalog-set.json
unity/Assets/StreamingAssets/GameData/Catalogs/**
matching schemas/generated contracts/provenance records
```

Phase D may change:

```text
unity/Assets/AL/Scripts/Core/Interfaces/IGameDataService.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
focused tests
```

`LocalGameDataService.cs` is a designated shared file. The first approved open implementation PR declaring it holds the lock.

Do not change by default:

```text
Bootloader.cs while PR #203 holds its lock
SaveGameData.cs or LocalSaveGameService.cs
scenes or Build Settings
narrative packets/content in an engineering PR
terrestrial source in an engineering PR
balance values
Android navigation/UI
unrelated gameplay services
```

## 15. Required tests

### 15.1 Manifest and loading

- valid catalog set from direct file path;
- valid identical bytes through UnityWebRequest/platform seam;
- missing manifest;
- malformed JSON;
- wrong game ID;
- blank/duplicate catalog ID;
- unsupported manifest version;
- duplicate family;
- invalid/traversal path;
- missing required artifact;
- missing optional artifact;
- read failure;
- artifact identity mismatch;
- SHA-256 mismatch;
- media type mismatch;
- timeout;
- cancellation;
- disposed/late callback cannot publish;
- deterministic diagnostic ordering.

### 15.2 Record and alias validation

For every first-phase family:

- valid representative record;
- null record;
- blank ID;
- duplicate ID;
- invalid new-ID pattern;
- valid legacy ID classification;
- alias success;
- alias collision;
- alias cycle;
- alias shadowing canonical ID;
- invalid enum;
- missing required field;
- unsupported unknown field/version drift;
- non-finite numeric;
- negative/out-of-range numeric;
- missing localization/content reference;
- missing asset/profile reference;
- cross-family missing reference;
- deterministic canonical ordering.

### 15.3 Immutability and queries

- `Found` with exact catalog/version/provenance;
- `AliasResolved` exposes requested and canonical IDs;
- `UnknownId`;
- `OptionalAbsent`;
- pending/unavailable/invalid/unsupported results;
- returned collections cannot be cast/mutated to change backing state;
- nested arrays/lists are immutable copies;
- repeated queries allocate/behave according to documented policy and never mutate;
- legacy nullable wrappers create/save nothing;
- no query creates player building/research/troop/quest state;
- enumeration order is deterministic.

### 15.4 Whole-set publication and lifetime

- all required families valid publishes once;
- one required-family failure publishes nothing;
- optional gap produces exact approved status;
- cross-family failure blocks publication;
- previous snapshot remains after failed reload;
- successful reload atomically switches revision;
- concurrent/in-flight queries see old or new snapshot only;
- repeated service creation does not leak runtime `ScriptableObject` definitions;
- discarded chapter/soul-quest object count no longer grows;
- dispose/cancellation is deterministic.

### 15.5 Family regressions

#### Realms

- four current realms preserved without copy/tuning change;
- `None` and undefined reject;
- no Crownlands fallback;
- rare-resource/profile reference resolves;
- immutable enumeration.

#### Buildings

- every current approved ID/value inventories exactly;
- absent `ManaShrine`/`Mine` return unknown/unavailable, not invented records;
- positive bounded max level;
- display name not derived at query time;
- query does not seed save state.

#### Research

- eight current IDs classified;
- exact alias behavior;
- no display-string fuzzy match;
- acyclic prerequisites;
- effect/profile reference resolves;
- query/stat read does not seed save state.

#### Troops

- supported enum/ID uniqueness;
- missing record blocks training lookup;
- no enum-name fallback;
- immutable technical stats.

#### Champions/skills

- champion realm/class/skill references resolve;
- skill IDs unique and not slot-index authority;
- behavior and presentation references separate;
- incomplete loadout is invalid rather than mixed with fallback;
- all numeric values finite.

### 15.6 Schema/generated-contract drift

- schemas parse and execute;
- source contract generates deterministic C#/Kotlin/Fable artifacts where used;
- committed generated files match regeneration;
- field/enum/ID drift fails;
- manifest hash/provenance updates are required when bytes change.

### 15.7 Packaging/integration

- canonical Unity compile/import;
- complete EditMode catalog tests;
- corrected #127 PlayMode startup reports catalog status/provenance without touching developer profile;
- #150 Player build contains every required catalog and hash-verifies it;
- Android target packaged path loads through the platform seam;
- release build cannot use development fallback;
- no network access required;
- no missing asset/reference/import diagnostics.

## 16. Required validation

Run from the canonical workspace only:

```powershell
$repo = "C:\Users\MY\Documents\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\GameDataCatalogCompile.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform EditMode -assemblyNames AL.EditMode.Tests `
  -testResults "$repo\unity\Logs\GameDataCatalogEditMode.xml" `
  -logFile "$repo\unity\Logs\GameDataCatalogEditMode.log"
```

Run the corrected profile-safe PlayMode suite after PR #209 is accepted. Run Player packaging/launch through #150 when the production source switch is proposed.

Report:

- exact base/head SHA;
- exact changed files and lock holder;
- manifest/catalog IDs and versions;
- raw artifact hashes;
- source-to-generated mapping;
- current and migrated ID/alias inventory;
- complete/focused test totals;
- direct-file and UnityWebRequest/path results;
- malformed/unsupported/hash/reference matrices;
- immutable-query and lifecycle tests;
- packaged Player catalog inventory;
- final `git diff --check origin/main...HEAD`;
- final repository status;
- every blocked/unperformed check.

Exit `199`, licensing failure, missing XML, duplicate-workspace execution, skipped suites, development fallback, or absent Player artifacts are blocked validation, not passing evidence.

## 17. Acceptance criteria

- [ ] One deterministic catalog-set manifest selects exactly one production source per implemented family.
- [ ] Every implemented catalog has explicit identity, schema/content version, source revision, and verified SHA-256.
- [ ] Production missing/invalid/unsupported data never silently becomes runtime fallback content.
- [ ] Typed load/readiness/query results distinguish every required state.
- [ ] Queries are pure and return immutable snapshots.
- [ ] No mutable runtime `ScriptableObject` or backing collection is exposed as authority.
- [ ] Existing IDs/values are inventoried and preserved or migrated through explicit aliases without content/balance drift.
- [ ] Realms, buildings, research, troops, champions, and skills have exact technical authority and validation.
- [ ] Missing `ManaShrine`/`Mine`, troop, champion, or skill definitions are visible blockers rather than invented data.
- [ ] Research display-string identity is replaced or retained only through a reviewed canonical-ID/alias plan.
- [ ] Chapter/quest authority remains gated by #156/#128/#133 and archive content is not activated.
- [ ] Customization and terrestrial source are not silently promoted to runtime authority.
- [ ] Schema/C#/Kotlin/Fable/generated artifacts cannot drift silently.
- [ ] Repeated initialization/reload/disposal has deterministic object and snapshot lifetime.
- [ ] Canonical Unity EditMode, corrected PlayMode, and Player packaging evidence pass when applicable.
- [ ] `LocalGameDataService.cs` lock and every downstream consumer migration are declared and released explicitly.
- [ ] No unapproved narrative, terrestrial design, balance, save, Android navigation, scene, or unrelated gameplay change is included.

## 18. Codex handoff

```text
Codex engineering: after #156 is accepted, implement issue #183 from current main using unity/Docs/Game_Data_Catalog_Authority_Spec.md. Begin with one focused catalog-foundation PR: common manifest/envelope, typed load/query/diagnostic results, immutable snapshots, strict validators, packaged file/UnityWebRequest seams, deterministic hash/schema tests, and a complete current source/consumer inventory. Do not edit Bootloader.cs, switch LocalGameDataService production authority, author player-facing content, promote PR #217 terrestrial data, change balances, or repair player saves in the foundation PR. Before the LocalGameDataService migration, declare its shared-file lock, coordinate with the accepted lifecycle path, use separately approved source-mode catalogs, and return canonical Unity evidence for Codex coordination/review.
```
