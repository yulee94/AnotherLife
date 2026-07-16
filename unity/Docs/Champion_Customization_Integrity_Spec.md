# Champion Customization Integrity Specification

**Status:** Binding GPT technical specification for issue #184  
**Status date:** 2026-07-16  
**Audited base:** `371cc019c7a4526b8b20c145104c994d5c49a056`  
**Primary implementation owner:** Codex engineering  
**Option, preset, profile-label, and localization owner:** Codex narrative/content  
**Visual-design and model-fidelity owner:** Codex terrestrial-design where applicable  
**Specification/review owner:** GPT  
**Final visual, product, balance, playtest, and release approval:** User  
**Canonical Unity workspace:** `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`

## 1. Purpose

This specification defines the authoritative technical boundary for Champion appearance customization:

- one versioned, hash-identified production catalog source;
- strict catalog, option, preset, color, scale, and model-capability validation;
- preservation of raw committed compatibility state, including unresolved future IDs;
- separation of raw committed state from effective presentation and editable draft state;
- deterministic synchronous and asynchronous catalog loading;
- immutable query snapshots and typed failure/status results;
- reversible visual preparation and application;
- crash-safe, rollback-capable persistence through the accepted #137 boundary;
- explicit operation identity, stale-plan rejection, event delivery, and notification handoff;
- content/localization and visual-design ownership separation;
- complete compatibility, downgrade, fault, Player-packaging, and evidence requirements.

It replaces the current implicit flow:

```text
live save-backed ChampionCustomizationState
→ controller mutates fields directly
→ Save()
→ normalize the same live object against catalog or hard-coded arrays
→ apply whatever model parts happen to exist
→ void/bool result and hard-coded English profile summary
```

and the current startup flow:

```text
try direct file load
→ apply saved state immediately
→ if catalog unavailable, normalize against hard-coded fallback arrays
→ later UnityWebRequest callback may install a catalog and reapply appearance
```

with:

```text
versioned catalog load result
→ immutable validated catalog snapshot
→ immutable raw committed compatibility snapshot
→ non-destructive resolution into an effective presentation snapshot
→ explicit editable draft
→ validated stale-safe appearance/persistence plan
→ reversible model application
→ one candidate persistence/verification operation
→ published committed snapshot
→ typed event and notification once
```

This specification does **not**:

- remove or add an appearance option;
- change current colors, body scales, flags, or preset composition;
- author names, summaries, lore, class meaning, or localization copy;
- redesign the Champion model, materials, shaders, VFX, animation, scene, or forge UI;
- implement cloud/profile synchronization;
- authorize production catalog migration before #183;
- authorize save-schema changes before the corrected #137 boundary;
- promote ChampionArena into the first Player build profile;
- change combat or item balance.

## 2. Binding dependencies and phase authorization

### 2.1 Related contracts

Customization integrity consumes rather than duplicates:

```text
unity/Docs/Game_Data_Catalog_Authority_Spec.md
unity/Docs/Save_Semantic_Compatibility_Policy.md
unity/Docs/Notification_Delivery_Contract_Spec.md
unity/Docs/Champion_Combat_Encounter_Integrity_Spec.md
unity/Docs/Production_Scene_Player_Build_Spec.md
```

If the exact Champion combat document path changes during integration, the merged PR #235 contract remains authoritative.

### 2.2 Dependency sequence

```text
pure customization contract/validator/planner phase
          ↓
#156 trusted Unity asset baseline
          +
#183 versioned customization catalog authority
          +
Codex narrative/content option/preset labels and localization references
          +
validated model-capability snapshot
          ↓
reversible real-model preview adapter and controller draft flow
          +
corrected #137 candidate persistence, operation ledger, rollback, and deletion
          ↓
save metadata/migration and durable customization commit
          +
#177 committed-result notification delivery
          ↓
#223/#150 production scene and Player packaging/accessibility evidence
          +
corrected #127 profile-safe PlayMode evidence
          ↓
#135 Android host lifecycle validation and user integrated visual acceptance
```

### 2.3 First phase may proceed now

The first Codex engineering phase is intentionally pure and nonmutating:

```text
codex/customization-contract-planner
```

It may implement immutable technical models, validators, compatibility resolution, edit/draft planning, reversible fake-adapter planning, stable diagnostics, and focused tests.

It must not edit or change behavior in:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalSaveGameService.cs
unity/Assets/AL/Scripts/Core/Bootloader.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/ChampionMode/Customization/ChampionCustomizationController.cs
unity/Assets/AL/Scripts/ChampionMode/Customization/CharacterCustomizationCatalog.cs
unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json
unity/SharedContracts/Schemas/al-character-customization.schema.json
unity/SharedContracts/Fable/AnotherLife.Contracts.fs
models, materials, shaders, prefabs, scenes, UI, Android, or authored content
```

The first phase performs no save mutation, real model mutation, event, notification, catalog-source migration, or production switch.

## 3. Verified current-source baseline

### 3.1 Live save state is the editor buffer

`ChampionCustomizationController.GetState()` returns:

```text
ServiceLocator.Get<ISaveGameService>().CurrentSave.ChampionCustomization
```

Every cycle, toggle, reset, randomization, and preset operation mutates that live save-backed object before validation, visual application, or persistence success is known.

Consequences:

- cancel is impossible because there is no independent draft;
- a failed save leaves the in-memory committed state changed;
- another service/caller can observe partially edited fields;
- rapid input creates multiple live-state transitions and file writes;
- a later normalization pass can rewrite fields the user did not explicitly edit;
- tests cannot distinguish proposed, previewed, applied, persisted, or published state.

### 3.2 Persistence occurs before normalization and application

Current `SaveAndApply()` is:

```text
Save()
→ ApplySavedCustomization()
```

`ApplySavedCustomization()` then:

```text
NormalizeState(liveState)
→ apply body/style/colors/flags
→ rebind procedural motion and surface response
```

Therefore:

- invalid or unsupported values can be written before normalization;
- save success can be followed by application failure;
- persistence can claim a visual state that was never applied;
- application can mutate the live state after persistence, leaving disk and memory different;
- there is no inverse visual snapshot or rollback path;
- save failure is not caught or converted into a typed result.

### 3.3 Startup can destructively normalize before the authoritative catalog arrives

Current startup:

```text
Awake:
  ensure procedural model
  refresh renderer cache
  try synchronous direct-file catalog load

Start:
  ApplySavedCustomization()
  if catalog still null, start async UnityWebRequest load
```

When direct file access is unavailable, `ApplySavedCustomization()` runs while `_catalog == null`. The option getters then use hard-coded arrays. `NormalizeState(...)` writes hard-coded fallbacks into the live saved object.

A valid ID introduced by a newer catalog can therefore be replaced in memory before the current catalog result is known. The next user edit calls `Save()` and can persist that destructive downgrade.

### 3.4 Late asynchronous results have no lifetime or revision protection

The asynchronous loader accepts one callback and the controller tracks only `_catalogLoadStarted`.

There is no:

- request generation ID;
- controller lifetime ID;
- cancellation token;
- timeout result;
- expected catalog/source revision;
- expected raw-state revision;
- stale callback rejection;
- scene/object disposal guard;
- replacement-controller ownership check.

A late callback can install a catalog and reapply appearance after gameplay or another controller state has advanced.

### 3.5 Production authority is silently hybridized

The controller contains hard-coded production-looking authority for:

```text
9 body preset IDs
5 hair style IDs
6 armor style IDs
9 face-mark IDs
5 weapon IDs
5 offhand IDs
5-entry primary/hair/skin/eye/accent palettes
9 named forge presets and their complete colors/options/flags
body scales
profile labels
```

The StreamingAssets catalog contains overlapping and additional records. When catalog data is absent, incomplete, malformed, or does not contain one record, the controller silently falls back to code.

One appearance can therefore be assembled from multiple unreported authorities:

```text
catalog body scale
+ hard-coded style option
+ hard-coded color fallback
+ hard-coded preset
+ current procedural-model part naming
```

The runtime does not expose which source/version/hash produced the effective appearance.

### 3.6 Catalog acceptance is incomplete

`CharacterCustomizationCatalog.TryParse(...)` accepts a catalog when only these families are nonempty:

```text
bodyPresets
hairStyles
armorStyles
```

It does not require or validate:

- exact game identity;
- catalog identity;
- supported schema or content version;
- source revision or raw SHA-256;
- required remaining option/color/preset families;
- null records;
- blank or duplicate IDs;
- exact case and ID format;
- duplicate display/localization references;
- preset cross-references;
- exact RGB/scale array length;
- finite numeric values;
- color range;
- body-scale bounds;
- deterministic record ordering;
- schema/Fable/runtime-model drift;
- unsupported additional fields;
- model capability references.

File, parse, and network failures log a warning saying runtime defaults will be used and return `false`/`null`. Callers cannot distinguish missing file, transport failure, malformed JSON, unsupported version, semantic violation, cancellation, or disposal.

### 3.7 Schema/runtime model drift is already present

The JSON schema and Fable catalog require:

```text
realms
qualityTargets
```

`CharacterCustomizationCatalogData` does not declare those fields. Unity `JsonUtility` can ignore data that the runtime model does not represent.

The schema also requires many families that the runtime parser does not require. A JSON file can satisfy the runtime parser while violating the retained schema, and the runtime can discard schema-required values silently.

The future validator must execute the retained schema or deterministically prove an equivalent generated contract. Schema, JSON source, C# model, Fable model, and loader acceptance cannot drift independently.

### 3.8 Numeric invalidity is hidden through fallback or clamping

Current preset color behavior:

```text
array null/short → hard-coded fallback color
ordinary values → Mathf.Clamp01(channel)
```

Current body scale behavior:

```text
array null/short → hard-coded body scale
ordinary values → Mathf.Max(0.1f, channel)
```

This does not reject `NaN`, infinity, or unsupported intent explicitly. It also turns invalid or out-of-range source data into a different appearance while representing the operation as success.

### 3.9 Saved state carries no source or migration identity

`ChampionCustomizationState` contains:

```text
6 raw option IDs
15 raw RGB float channels
capeEnabled
helmetEnabled
```

It contains no:

- state schema version;
- catalog set/catalog/content version;
- catalog raw hash;
- alias/migration version;
- source preset provenance;
- last committed operation ID;
- compatibility status;
- raw/effective distinction;
- explicit edit mask;
- last known valid source identity.

### 3.10 Query methods can mutate and misrepresent state

`GetAppearanceSummary()` calls `NormalizeState(state)` on the live backing object, so a presentation query can rewrite profile data.

Color queries return raw `Color` values without validity or source status. Missing state returns hard-coded colors, which can make unavailable state look valid.

The summary:

- formats raw technical IDs into English;
- infers profile labels such as `Dreadknight`, `Oracle`, `Duelist`, `Arcanist`, `Nightblade`, and `Vanguard` from option combinations;
- treats those inferred labels as player-facing identity without content/catalog authority;
- returns no catalog/version/compatibility/application status.

### 3.11 Presets overwrite all fields and then save immediately

Catalog presets and hard-coded presets directly replace every option, color, and flag. There is no:

- preview;
- explicit changed-field mask;
- warning that unresolved raw fields will be replaced;
- cancellation;
- stale catalog/model check;
- partial preset policy;
- source revision;
- operation identity;
- application rollback.

### 3.12 Randomization is nondeterministic and immediately durable

`RandomizeAppearance()` uses `UnityEngine.Random`, changes all appearance fields, flags, and colors, then immediately saves.

It has no randomization operation ID, seed, source revision, preview/cancel state, or deterministic test vector.

This specification does not approve or remove randomization. It requires randomization to operate as an explicit draft edit with a captured seed before later production use.

### 3.13 Model capability is assumed through names

The controller activates/deactivates and scales large sets of procedural GameObject parts by string name. Catalog records do not declare required parts/capabilities, and the application layer does not return which required changes succeeded.

A save/catalog can claim an option even when the current model lacks the required part, renderer, material slot, anchor, or compatible scale behavior.

## 4. Authority and ownership

### 4.1 Codex engineering

Owns:

- catalog envelope and technical IDs;
- schema/version/hash/provenance validation;
- immutable technical snapshots;
- load lifecycle/results and cross-platform transport;
- saved-state compatibility classification;
- alias/migration mechanics;
- effective-presentation resolution;
- draft/edit/commit state machines;
- reversible model adapter and capability validation;
- candidate persistence integration;
- events, diagnostics, tests, tools, and evidence.

### 4.2 Codex narrative/content

Owns:

- option and preset display names;
- profile/archetype labels and summaries;
- localization keys and typed parameter meaning;
- authored identity meaning;
- player-facing unavailable/migration/commit copy;
- whether an appearance choice implies class, realm, role, or story meaning.

Technical services return stable IDs and statuses, not inferred player-facing identity.

### 4.3 Codex terrestrial-design

Owns, where applicable:

- approved model silhouette and proportion intent;
- surface/material/color design intent;
- compatible visual variants;
- model-part and presentation-fidelity source;
- later fidelity review of engineering consumption.

This specification does not create or approve visual source.

### 4.4 User

Retains final approval of:

- visible appearance choices and presets;
- any body-scale/color/design changes;
- model fidelity and UI experience;
- destructive migration or removal of a player choice;
- integrated Player/Android visual acceptance;
- milestone and release acceptance.

## 5. Terminology

### 5.1 Raw committed compatibility state

The exact durable technical values last committed for the profile. Unknown future IDs and malformed evidence remain visible to compatibility logic and are not overwritten by a display fallback.

### 5.2 Validated catalog snapshot

An immutable, versioned, hash-identified catalog accepted after transport, syntax, schema, semantic, cross-reference, numeric, and source validation.

### 5.3 Model capability snapshot

An immutable description of the currently bound model adapter: supported option families, part IDs, material/color channels, anchors, flags, scale capability, revision, and source identity.

### 5.4 Effective presentation snapshot

A non-durable rendering decision derived from raw state, validated catalog, model capabilities, and compatibility policy. It can contain placeholders for unavailable raw fields but never replaces raw state implicitly.

### 5.5 Draft

An explicit editable copy based on one raw/effective/catalog/model revision. It tracks exactly which fields the user or a preset changed.

### 5.6 Preview

A reversible visual application of a draft. Preview is not durable and emits no committed event or notification.

### 5.7 Appearance plan

An immutable stale-safe plan containing proposed raw state, effective presentation, model application operations, inverse operations, persistence operation identity, and expected revisions.

### 5.8 Committed customization receipt

A typed receipt proving the candidate state persisted and verified, the committed snapshot published, and required event/outbox records were staged exactly once.

### 5.9 Preserved unknown

A nonblank stable ID or raw field value not resolvable by the current supported catalog. It is preserved in raw state, excluded from unsupported mutation, and represented through an explicit unavailable status.

### 5.10 Placeholder

A safe non-authoritative presentation used because the exact raw selection cannot currently render. A placeholder is never saved as though selected by the user.

## 6. Stable identity and version contract

### 6.1 Catalog-set identity

The customization artifact participates in the #183 catalog set. Required identity:

```text
gameId
catalogSetId
catalogId = character_customization
familyId = champion_customization
schemaVersion
contentVersion
sourceRevision
rawSha256
requiredness
packagedRelativePath
```

Existing source field:

```text
game = "Another Life"
```

is display-like legacy identity. Migration must map it explicitly to the approved stable `gameId`; it is not sufficient as the production identity by itself.

### 6.2 ID rules

Technical IDs are:

- non-null and nonblank;
- case-sensitive;
- valid UTF-8 within shared byte limits;
- free of control characters and leading/trailing whitespace;
- unique within the correct family and globally where the contract requires;
- never derived from display text, Unity object names, list positions, enum names, or hashes;
- stable across content revisions unless an explicit alias/migration record exists.

### 6.3 Catalog and model revisions

A planner captures:

```text
catalogSetId
catalog contentVersion
catalog rawSha256
modelCapabilityId
modelCapabilityRevision
rawStateRevision
saveCandidateRevision
```

Any mismatch before application or persistence returns `StalePlan`. It does not silently recompute or rebase while the user believes one preview is being committed.

### 6.4 Alias records

An alias record contains:

```text
familyId
oldId
newId
introducedIn
retiredIn
reasonCode
preserveOldRawUntilCommit
requiresUserConfirmation
```

Aliases are explicit and versioned. No lowercasing, whitespace trimming into another ID, fuzzy matching, display-name matching, nearest-color matching, or first-record selection is allowed.

## 7. Catalog artifact contract

### 7.1 Required families

The initial catalog defines explicit requiredness for:

```text
body presets
hair styles
armor styles
primary colors
hair colors
skin colors
eye colors
accent colors
face marks
weapon styles
offhand styles
forge presets
character/model slots or capability references
```

A family cannot be considered valid because another family exists.

Optional future families must be declared optional in the envelope and fail unsupported rather than being silently ignored.

### 7.2 Immutable catalog snapshot

Conceptual shape:

```csharp
public sealed class CustomizationCatalogSnapshot
{
    public CatalogIdentity Identity { get; }
    public IReadOnlyList<BodyPresetDefinition> BodyPresets { get; }
    public IReadOnlyList<StyleOptionDefinition> HairStyles { get; }
    public IReadOnlyList<StyleOptionDefinition> ArmorStyles { get; }
    public IReadOnlyList<ColorOptionDefinition> PrimaryColors { get; }
    public IReadOnlyList<ColorOptionDefinition> HairColors { get; }
    public IReadOnlyList<ColorOptionDefinition> SkinColors { get; }
    public IReadOnlyList<ColorOptionDefinition> EyeColors { get; }
    public IReadOnlyList<ColorOptionDefinition> AccentColors { get; }
    public IReadOnlyList<StyleOptionDefinition> FaceMarks { get; }
    public IReadOnlyList<StyleOptionDefinition> WeaponStyles { get; }
    public IReadOnlyList<StyleOptionDefinition> OffhandStyles { get; }
    public IReadOnlyList<ForgePresetDefinition> ForgePresets { get; }
    public IReadOnlyDictionary<string, AliasDefinition> Aliases { get; }
    public CustomizationPolicySnapshot Policy { get; }
}
```

All lists, dictionaries, records, RGB values, scale triples, and nested references are defensively copied and immutable.

### 7.3 Player-facing source

Catalog technical records reference content keys:

```text
displayNameKey
summaryKey
accessibilityLabelKey
optional descriptionKey
```

Raw English `displayName` and `summary` in the current catalog are migration evidence. They do not remain technical-service authority after content migration.

### 7.4 Schema and generated-contract parity

Required retained proof:

```text
JSON source
↔ JSON schema
↔ C# transport model
↔ immutable runtime model
↔ Fable/shared contract
↔ deterministic semantic validator
```

The build/test must fail when:

- a schema-required property is not represented intentionally;
- a runtime-required property is absent from the schema;
- an enum/family/version set drifts;
- generated artifacts are stale;
- unknown production properties would be silently discarded;
- C# and Fable field types disagree.

### 7.5 Unknown properties

Production parsing is fail-closed for unknown properties unless the schema explicitly defines an extension container and compatibility policy. `JsonUtility` silently ignoring fields is not accepted validation.

## 8. Option-family validation

For every option family, reject:

- null record;
- blank ID;
- duplicate ID;
- invalid ID encoding/length;
- missing required content key;
- duplicate or invalid ordering key where ordering is authoritative;
- unsupported source/content version;
- invalid model-capability reference;
- cross-family ID used in the wrong field;
- alias cycle or alias collision;
- default ID absent from the family.

Validation diagnostics are deterministic and include:

```text
code
catalogId/contentVersion/rawSha256
familyId
recordId or index when ID is unavailable
fieldPath
expected rule
actual classification without unsafe raw payload
```

No validator mutates or removes source records.

## 9. Color contract

### 9.1 Current compatibility authority

Current saves store direct RGB channel values, not palette IDs. For compatibility:

- exact finite direct RGB values are the raw committed color selection;
- catalog palette IDs are selectable conveniences and optional provenance;
- runtime must not infer a palette ID through nearest-color matching;
- selecting a palette writes the exact approved RGB triple and may record the palette ID as provenance;
- custom exact RGB values remain valid when policy permits them.

### 9.2 Initial non-HDR policy

The current appearance path is non-HDR. Each channel must be:

```text
finite
0.0 <= value <= 1.0
```

`NaN`, positive/negative infinity, and out-of-range values are invalid. They are not clamped into a new intended color.

A future HDR/emissive family requires a separately versioned policy and user-approved visual/balance implications.

### 9.3 Atomic color field

Each logical color is one three-channel value. If any channel is missing or invalid, the entire logical color is invalid for production resolution.

Compatibility resolution may display an explicit placeholder color, but raw channels remain preserved until an approved migration/explicit edit.

### 9.4 Exact array shape

Catalog and preset RGB arrays contain exactly three values. Length `<3` or `>3` rejects.

## 10. Body-scale contract

### 10.1 Source authority

Body scale is derived from a validated body preset definition; current saves do not store direct scale.

### 10.2 Validation

Each scale component is:

- finite;
- strictly positive;
- within the versioned, source-owned technical/profile bounds approved for that catalog;
- compatible with the target model capability snapshot.

This specification does not invent new appearance bounds. The first catalog migration must inventory the exact current scale values and publish the approved bound policy. Until then, out-of-profile values reject rather than being clamped with `Mathf.Max(0.1f, ...)`.

### 10.3 Whole-vector validity

A scale triple is accepted or rejected as a whole. No per-channel clamp creates a different body preset.

## 11. Forge preset contract

### 11.1 Preset identity

A preset has:

```text
presetId
content keys
catalog/version/hash identity
complete or explicitly partial field mask
option references
exact color values or color-option references
flags
required model capabilities
```

### 11.2 Cross-reference validation

Every referenced ID resolves to the correct option family in the same compatible catalog snapshot. Every color satisfies the color contract. Every required model capability exists or the preset is unavailable.

### 11.3 Draft-only application

Selecting a preset creates a draft. It does not mutate raw committed state or save immediately.

The draft records exactly which fields the preset changes. A complete preset may intentionally replace all declared fields; a partial preset may change only its explicit mask. Missing fields are never filled from hard-coded code silently.

### 11.4 Preserved unknown warning boundary

If a preset would replace a field containing a preserved unknown raw ID, the plan records that destructive replacement explicitly. UI/content decides whether confirmation is required. No hidden normalization occurs.

### 11.5 Provenance is non-authoritative

A committed state may record the preset ID used to create a draft. Subsequent explicit edits do not cause the preset to overwrite those edits on load.

## 12. Model-capability contract

### 12.1 Capability snapshot

The real model adapter publishes an immutable snapshot:

```text
modelCapabilityId
revision
source model/prefab identity and hash where available
supported option families
supported body-scale behavior
supported named part IDs
supported material/color channels
supported cape/helmet flags
required renderer/material slots
anchors and optional capabilities
```

### 12.2 No GameObject-name authority in core plans

Core customization planning uses stable capability IDs. The adapter may map those IDs to current GameObject/renderer/material implementation details, but string scene/object names are not catalog authority.

### 12.3 Availability

An option can be:

```text
Available
UnavailableMissingCapability
UnavailableWrongModelRevision
UnavailableCatalogPending
UnavailableCatalogInvalid
UnavailablePreservedUnknown
```

Unavailable does not mean replace raw state.

### 12.4 Verification

A prepared model plan lists every required operation and expected postcondition. Application succeeds only if all required operations apply and verify. Optional cosmetic operations are explicitly optional and reported separately.

## 13. Catalog load lifecycle

### 13.1 States

```text
NotStarted
Loading
Ready
Failed
Cancelled
Disposed
```

`Ready` includes exact source/version/hash identity. `Failed` includes a stable failure code.

### 13.2 Failure codes

At minimum:

```text
MissingFile
TransportFailure
Timeout
Cancelled
Disposed
EmptyContent
MalformedJson
WrongGameId
WrongCatalogId
UnsupportedSchemaVersion
UnsupportedContentVersion
RawHashMismatch
SchemaViolation
SemanticViolation
CrossReferenceViolation
NumericViolation
ContractDrift
PackagingPathMismatch
InternalFailure
```

### 13.3 Direct-file and UnityWebRequest parity

Both transport paths consume the same raw bytes and run the same hash, syntax, schema, semantic, and immutable-publication pipeline.

A catalog accepted through one path must have the same snapshot hash and diagnostics through the other path.

### 13.4 No production fallback

Missing/invalid production catalog results in an explicit unavailable/degraded state. Hard-coded option/preset arrays may remain temporarily only behind an explicit development compatibility mode with visible provenance and may not count as production/acceptance evidence.

### 13.5 Publication

The service publishes a new validated snapshot atomically after full validation. A failed reload leaves the prior validated snapshot available only if policy explicitly permits last-known-good in-session use and reports the failed reload separately. It never publishes a partially parsed snapshot.

## 14. Asynchronous lifetime and race contract

Each load request captures:

```text
loadRequestId
requestGeneration
controller/service lifetimeId
expected catalog set/source path
expected application target ID
cancellation token
start deadline
```

Completion is accepted only when:

- the owner is alive and not disposed;
- generation is current;
- request ID matches;
- target/model identity is current;
- result is fully validated;
- no newer catalog result has published;
- no active commit plan requires a conflicting revision.

Late/stale completion returns or records `StaleResultIgnored`. It does not mutate controller, draft, raw state, model, or save.

Disposal cancels pending work and prevents callbacks from acting on destroyed/replaced objects.

## 15. Raw committed state contract

### 15.1 Current fields preserved

Current technical defaults are migration evidence and must not drift silently:

```text
bodyPresetId = average
hairStyleId = short
armorStyleId = realm_basic
faceMarkId = none
weaponStyleId = sword
offhandStyleId = shield
primary = (0.20, 0.40, 1.00)
hair = (0.08, 0.06, 0.04)
skin = (0.72, 0.56, 0.42)
eye = (0.25, 0.58, 0.92)
accent = (0.85, 0.62, 0.18)
capeEnabled = true
helmetEnabled = false
```

This record does not approve changing those values.

### 15.2 Future durable metadata

After corrected #137 and an explicit `SaveGameData.cs` lock, the durable model must support equivalent metadata:

```text
customizationStateSchemaVersion
lastValidatedCatalogSetId
lastValidatedCatalogContentVersion
lastValidatedCatalogRawSha256
lastAppliedAliasMigrationVersion
optional sourcePresetId for provenance
lastCommittedCustomizationOperationId
state revision
```

The exact serialization shape follows save compatibility constraints and old-save fixtures.

### 15.3 Raw IDs remain exact

Blank IDs are malformed. Nonblank unresolved IDs are preserved unknown unless an explicit alias/migration resolves them.

Compatibility reads never replace raw IDs with defaults merely because the current catalog cannot resolve them.

### 15.4 No backing-object exposure

Services/controllers do not return the mutable `ChampionCustomizationState`. They return immutable snapshots and typed status.

## 16. Saved-state validation and compatibility status

### 16.1 Domain status

```text
Valid
ValidLegacyNoMetadata
NeedsAliasMigration
PreservedUnknown
Malformed
CatalogPending
CatalogUnavailable
ModelCapabilityUnavailable
FutureSchemaUnsupported
```

Status is computed without mutation or saving.

### 16.2 Field status

Each field reports:

```text
RawValidResolved
RawValidAliasAvailable
RawPreservedUnknown
RawBlankInvalid
RawNumericInvalid
RawUnsupportedFutureSchema
EffectivePlaceholder
```

### 16.3 Domain isolation

A malformed logical field does not authorize mutation of another valid field. The whole customization commit can still be blocked until required fields resolve, but diagnostics preserve exact per-field evidence.

### 16.4 Legacy metadata absence

An old save with current fields but no catalog metadata is not automatically malformed. It is classified `ValidLegacyNoMetadata` when all raw values satisfy the current compatibility contract. Metadata may be added only during an explicit successful commit/migration through #137.

## 17. Effective presentation resolution

### 17.1 Pure resolution

```text
raw committed snapshot
+ validated catalog snapshot or explicit unavailable status
+ model capability snapshot
+ compatibility policy
→ effective presentation result
```

Resolution mutates nothing.

### 17.2 Resolved field

When a raw ID resolves and model capability exists, effective presentation uses that exact option.

### 17.3 Alias-available field

The effective presentation may preview the alias destination while preserving the raw old ID until an explicit migration/commit. Status identifies the alias.

### 17.4 Preserved unknown field

The resolver selects an explicit family placeholder or safe model baseline for presentation only. The result contains both:

```text
rawId = exact preserved unknown
presentedOptionId = placeholder technical ID
status = EffectivePlaceholder
```

The placeholder is never copied into proposed raw state unless the user explicitly selects it as a real supported option.

### 17.5 Catalog pending/unavailable

A newly created model may display a neutral technical baseline, but the UI/status must show pending/unavailable. It must not claim that baseline is the player’s committed appearance.

If a prior valid effective snapshot is safely retained in the same session/model revision, policy may keep it while a reload is pending. The retained snapshot’s source identity remains visible.

## 18. Immutable query contract

Conceptual query result:

```csharp
public sealed class CustomizationQueryResult
{
    public CustomizationQueryStatus Status { get; }
    public RawCustomizationSnapshot RawCommitted { get; }
    public EffectiveAppearanceSnapshot EffectivePresentation { get; }
    public CatalogIdentity Catalog { get; }
    public ModelCapabilityIdentity Model { get; }
    public IReadOnlyList<CustomizationDiagnostic> Diagnostics { get; }
}
```

Queries:

- do not normalize;
- do not create state;
- do not save;
- do not apply visuals;
- do not format player-facing labels;
- do not expose mutable arrays, dictionaries, save rows, ScriptableObjects, renderers, or materials.

## 19. Draft and edit contract

### 19.1 Draft creation

A draft captures:

```text
draftId
base raw-state revision
base catalog identity/hash
base model-capability revision
base effective snapshot
working proposed raw fields
explicit changed-field mask
source operation/preset/random seed provenance
creation time for diagnostics only
```

### 19.2 Edit requests

Typed requests include:

```text
SelectOption(familyId, optionId)
SelectExactColor(colorFieldId, rgb)
SelectPaletteColor(colorFieldId, colorOptionId)
SetFlag(flagId, bool)
ApplyPreset(presetId)
ResetToApprovedDefaults
RandomizeWithSeed(seed, allowedFamilies)
Undo/Redo when later UI supports it
```

### 19.3 Result states

```text
AppliedToDraft
NoChange
RejectedCatalogPending
RejectedCatalogInvalid
RejectedUnknownOption
RejectedWrongFamily
RejectedNumericInvalid
RejectedUnavailableCapability
RejectedPreservedUnknownReplacementNeedsConfirmation
RejectedStaleDraft
RejectedDisposed
```

### 19.4 Explicit changes only

An edit changes only its declared field(s). Other raw fields—including preserved unknown IDs—remain byte/semantically exact.

### 19.5 No immediate persistence

Cycle/toggle/reset/random/preset UI operations update and preview the draft. Persistence occurs only through an explicit commit checkpoint.

## 20. Preview and reversible model adapter

### 20.1 Preparation

The adapter prepares:

```text
appearancePlanId
model target ID/revision
required operations
optional operations
expected postconditions
prior visual snapshot/inverse plan
proposed effective snapshot hash
```

Preparation performs no real mutation.

### 20.2 Application

Application returns:

```text
AppliedAndVerified
RejectedStaleModel
RejectedMissingCapability
FailedRequiredOperation
FailedVerification
Disposed
```

### 20.3 Rollback

Every successful preview/application produces a usable inverse until commit is finalized or preview is cancelled.

Rollback is attempted when:

- user cancels draft;
- later validation fails;
- persistence fails;
- commit becomes uncertain and reconciliation policy requires prior visual restoration;
- scene/controller disposal occurs before commit publication.

Rollback failure is a blocking visible technical state. It is not swallowed.

### 20.4 Preview has no authoritative side effect

Preview:

- does not mutate raw committed state;
- does not save;
- does not write operation ledger;
- does not emit committed events or notifications;
- does not imply content/user approval.

## 21. Commit transaction

### 21.1 Request

```text
customizationOperationId
profile/save identity token
expected raw-state revision
expected save candidate revision
expected catalog identity/hash
expected model identity/revision
validated draft ID/hash
required notification/outbox policy
```

### 21.2 Preparation sequence

Before mutation:

1. re-query raw committed snapshot;
2. validate save/profile/service availability;
3. verify catalog and model revisions;
4. validate every proposed raw field;
5. resolve proposed effective presentation;
6. prepare reversible real-model application;
7. create a candidate save clone;
8. apply proposed raw state and metadata to the clone only;
9. validate complete candidate semantics;
10. prepare operation-ledger/event/outbox records;
11. return one immutable commit plan.

### 21.3 Preferred commit order

The initial production order is:

```text
prepare everything
→ apply required visual plan reversibly
→ verify visual postconditions
→ persist and verify candidate through corrected #137
→ publish committed save/raw snapshot
→ finalize visual application
→ publish typed committed event/notification receipt once
```

This prevents persistence from claiming an appearance the current required model could not apply.

### 21.4 Persistence failure

If persistence fails before durable commit:

- restore the prior visual snapshot;
- keep prior committed raw/save state authoritative;
- return typed failure;
- emit no success event/notification;
- preserve diagnostics.

### 21.5 Commit uncertainty

If #137 reports commit uncertainty:

- do not retry blindly;
- reconcile by `customizationOperationId` and persisted candidate/ledger identity;
- avoid applying a second time;
- keep controls blocked or explicitly degraded until reconciliation;
- present a visible typed recovery state through #177 later.

### 21.6 Visual rollback failure

If persistence fails and visual rollback fails:

- save remains prior authoritative state;
- runtime marks presentation `DivergedRequiresRebuild`;
- controller disables further commit operations;
- a full model rebuild/reapply from committed state is attempted only through a typed recovery path;
- failure remains visible and logged with stable diagnostics.

### 21.7 Event and notification order

After verified persistence and publication:

1. publish `CustomizationCommittedEvent` with operation ID and source identities;
2. enqueue typed #177 notification when policy requires;
3. update UI from the committed query result.

Subscriber or presenter failure does not roll back a durable commit and is reported separately.

## 22. Idempotency and replay

### 22.1 Exact replay

Same `customizationOperationId` + same plan hash + same profile/catalog/model semantic identity returns the existing committed receipt and performs no model/save/event/notification mutation.

### 22.2 Correlation conflict

Reuse of an operation ID with different proposed raw state, catalog hash, model revision, field mask, or plan hash rejects as an integrity error.

### 22.3 Same-value edit

Selecting the already committed/effective value returns `NoChange`; it performs no preview rebuild or save unless a separately justified recovery/reapply operation is requested.

## 23. Controller migration contract

The production controller becomes presentation/input orchestration rather than save/catalog authority.

Responsibilities:

- subscribe to typed catalog/service status;
- request immutable query snapshots;
- open/close a draft;
- translate UI actions into typed edit requests;
- request preview/application through the adapter;
- request explicit commit/cancel;
- render stable content keys/status;
- dispose subscriptions and cancel outstanding work.

It does not:

- hold hard-coded production option/preset arrays;
- return/mutate save-backed state;
- call `Save()` per button;
- normalize during a query;
- infer player-facing archetype names;
- silently use defaults after production catalog failure;
- accept stale async callbacks;
- represent Console logging as delivery.

## 24. Content and localization contract

Technical results provide:

```text
option/preset/profile technical IDs
content keys
field/status/failure codes
typed safe parameters
```

Codex narrative/content supplies approved localized strings. Technical code does not construct profile labels from combinations or format technical IDs into player-facing copy.

Missing content keys follow the #177 development/release fallback policy and do not substitute unrelated archetype names.

## 25. Privacy and diagnostics

Player-facing status must not expose:

- local StreamingAssets/save paths;
- raw exceptions or stack traces;
- full catalog JSON;
- filesystem/user names;
- unsafe internal object names.

Technical logs use stable codes and may include sanitized catalog/version/hash prefixes, operation IDs, family/field IDs, and failure phase.

## 26. First pure planner implementation boundary

Branch:

```text
codex/customization-contract-planner
```

### 26.1 Allowed

- immutable catalog identity/option/preset/policy models;
- strict catalog semantic validator operating on test transport objects;
- immutable raw-state/effective-presentation/model-capability snapshots;
- saved-state compatibility classifier;
- explicit alias and preserved-unknown resolution;
- color/scale/preset validators;
- immutable draft/edit/result models;
- pure edit and commit-plan builders;
- fake reversible model adapter with stale/failure/rollback seams;
- deterministic diagnostic ordering;
- current source/schema/Fable/controller/model-capability inventory;
- EditMode tests and fixed vectors.

### 26.2 Prohibited

- production controller or loader edits;
- production catalog/schema/Fable edits;
- real save state/service edits;
- Bootloader or shared-file edits;
- real model/render/material/part mutation;
- UI/scene/Android changes;
- content/localization changes;
- new options/presets/colors/scales;
- runtime production switch;
- issue closure.

### 26.3 Expected focused paths

Prefer new isolated paths equivalent to:

```text
unity/Assets/AL/Scripts/ChampionMode/Customization/Contracts/**
unity/Assets/AL/Tests/EditMode/Customization/**
unity/Docs/Customization_Source_Inventory.md
```

Do not create overlapping parallel authorities in generic shared files.

## 27. Later implementation phases

### Phase C — catalog authority and loader

After #156/#183:

- approved envelope and catalog identity;
- schema/C#/Fable parity;
- strict raw-byte/hash/schema/semantic validation;
- direct-file/UWR typed parity;
- no production fallback;
- immutable publication and diagnostics;
- Codex narrative/content keys.

### Phase D — real model adapter and preview

- stable model-capability inventory;
- reversible real-model plan/application/verification;
- controller draft/preview/cancel behavior;
- no persistence yet unless #137 integration is ready;
- visual fidelity review.

### Phase E — save metadata/migration and durable commit

After corrected #137 and declared `SaveGameData.cs` lock:

- old-save fixtures;
- metadata fields and defaults;
- alias/preserved-unknown migration;
- candidate clone application;
- operation ledger/idempotency;
- commit uncertainty and rollback;
- deletion coverage.

### Phase F — notification, Player, Android, and acceptance

- #177 committed delivery;
- #223/#150 packaging and launch;
- corrected #127 profile-safe PlayMode;
- #135 host lifecycle;
- long-text/accessibility/reduced-motion UI validation;
- user integrated visual acceptance.

## 28. Required test matrix

### 28.1 Catalog envelope and transport

- valid direct-file raw bytes;
- valid UnityWebRequest raw bytes;
- identical snapshot/hash across paths;
- missing file;
- transport error;
- timeout;
- cancellation;
- disposed owner;
- empty content;
- malformed JSON;
- wrong game/catalog/family ID;
- unsupported schema/content version;
- hash mismatch;
- packaging path mismatch;
- deterministic failure ordering.

### 28.2 Schema and contract parity

- valid retained schema;
- schema-required field absent from C# model;
- C# required field absent from schema;
- Fable type/name drift;
- unknown/additional property;
- stale generated artifact;
- `realms`/`qualityTargets` represented or explicitly split by an approved migration;
- JSON accepted by schema but rejected by runtime semantic validator for the correct reason;
- JSON accepted by runtime transport but rejected by schema.

### 28.3 Family records

For every option family:

- valid records;
- null record;
- blank ID;
- duplicate ID;
- invalid characters/length;
- wrong family reference;
- missing default;
- alias cycle/collision;
- deterministic ordering;
- missing content key;
- model-capability unavailable.

### 28.4 Color validation

- exact boundary 0 and 1;
- valid interior values;
- `NaN` per channel;
- positive/negative infinity;
- below 0/above 1;
- array null;
- length 0/1/2/4;
- no clamp into acceptance;
- preserved invalid raw color with effective placeholder;
- palette selection writes exact RGB;
- no nearest-palette inference.

### 28.5 Body scale

- current valid scale inventory;
- zero/negative channel;
- `NaN`/infinity;
- missing/extra channel;
- outside approved catalog bound;
- missing model capability;
- whole-vector rejection without partial clamp.

### 28.6 Presets

- every valid current preset as migration vector;
- blank/duplicate preset ID;
- invalid option reference per family;
- invalid color;
- missing required model capability;
- complete versus partial field mask;
- preserved-unknown replacement warning;
- preset creates draft only;
- subsequent user edit is not overwritten by preset provenance.

### 28.7 Raw saved-state compatibility

- current exact default;
- every current option family ID;
- blank ID;
- unknown stable future ID;
- explicit alias old ID;
- alias cycle rejected at catalog validation;
- old save with no metadata;
- future schema unsupported;
- invalid color channels;
- missing customization object;
- prior valid catalog unavailable;
- raw state unchanged by classification/query;
- immutable snapshot cannot mutate backing object.

### 28.8 Effective presentation

- all fields resolved;
- one preserved unknown field;
- multiple preserved unknown fields;
- catalog pending;
- catalog invalid;
- model capability missing;
- alias preview while raw old ID remains;
- placeholder never enters proposed raw state implicitly;
- retained prior valid effective snapshot identity;
- deterministic effective hash.

### 28.9 Draft and edits

- create draft from current revisions;
- select each option family;
- exact color edit;
- palette color edit;
- cape/helmet flags;
- same-value no-op;
- invalid/wrong-family option;
- edit while catalog pending;
- stale catalog/model/raw revision;
- explicit partial changed-field mask;
- preserved unknown in untouched field survives;
- preset edit;
- reset draft;
- seeded random draft deterministic vector;
- cancel restores prior preview.

### 28.10 Fake reversible adapter

- prepare success without mutation;
- apply/verify success;
- stale model rejection;
- missing capability;
- failure at each required operation;
- optional operation failure classification;
- verification failure;
- rollback success;
- rollback failure;
- disposal before apply;
- disposal after preview;
- exact prior visual snapshot restoration.

### 28.11 Persistence integration later

- valid apply/persist/verify/publish;
- visual apply failure before save;
- candidate validation failure;
- persistence failure before replacement;
- commit uncertainty;
- post-persist verification failure;
- rollback visual success/failure;
- exact replay;
- operation correlation conflict;
- stale plan;
- save service missing/partial/replaced;
- event subscriber failure;
- notification enqueue/presenter failure;
- no duplicate save/event/notification;
- reload returns the same committed raw/effective identity.

### 28.12 Async and lifecycle

- direct load wins before Start;
- async path pending without destructive normalization;
- late callback after newer generation;
- late callback after controller destroy;
- late callback after model replacement;
- catalog reload during draft;
- catalog reload during prepared plan;
- scene unload during preview;
- cancellation and timeout;
- no callback mutates disposed state.

### 28.13 Player/accessibility later

- StreamingAssets packaged and hash-valid in Windows Player;
- Android/UnityWebRequest packaging later;
- pending/invalid/unavailable status visible;
- long localized names/summaries;
- large text and safe area;
- keyboard/controller/touch input;
- non-color-only selection/status;
- reduced-motion preview;
- no combat control/telegraph obstruction;
- profile-safe PlayMode and no developer-profile mutation.

## 29. Source and migration inventory requirement

Before production catalog migration, publish a machine-readable inventory of:

- every current hard-coded option ID by family;
- every current catalog option ID by family;
- every current hard-coded and catalog preset;
- exact current body-scale triples;
- exact current palette/preset RGB triples;
- default IDs/colors/flags;
- duplicates and source-only records;
- catalog/schema/C#/Fable field differences;
- procedural model capabilities/part mappings;
- every controller/UI caller and save/query path;
- every old-save fixture/version available;
- exact proposed unchanged/alias/preserved/removed classification.

No option is removed or renamed silently during migration.

## 30. File and lock boundary

### 30.1 First pure phase

No designated shared-file lock.

### 30.2 Later save phase

Any edit to:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
```

requires the exclusive shared-file lock and current-main overlap review.

### 30.3 Later catalog/service phase

If customization is integrated through `LocalGameDataService.cs`, the PR must declare that designated shared-file lock. Prefer a catalog service boundary that avoids unrelated shared-file expansion.

### 30.4 Prohibited bundling

Do not combine:

- save recovery redesign;
- Combat/skills/boss behavior;
- new visual source;
- broad forge/HUD redesign;
- scene authoring/Build Settings;
- Android embedding;
- item grades/VFX tiers;
- narrative content;
- balance changes.

## 31. Canonical validation evidence

Every implementation PR records:

```text
current main/base SHA
head SHA
changed-file inventory
shared locks
exact canonical workspace
Unity 2022.3.62f3 command
exit code
compiler/error scan
focused and complete EditMode totals/XML
PlayMode totals/XML when in phase
Player output/BuildReport when in phase
git diff --check origin/main...HEAD
final status
all unavailable/deferred checks
```

Catalog/Player phases additionally record:

```text
raw catalog SHA-256 and byte length
schema/content/source versions
packaged relative path
file and UnityWebRequest parity
schema/C#/Fable drift results
valid/invalid vector totals
Player packaged-file/hash verification
```

Save phases additionally record:

```text
old-save fixtures
candidate/rollback/fault/deletion matrix
operation ledger/idempotency evidence
save/event/notification counts
commit-uncertain reconciliation
```

Evidence from the duplicate workspace is not accepted.

## 32. Acceptance criteria

### Contract/planner phase

- [ ] Immutable catalog, raw-state, effective-presentation, capability, draft, edit, plan, result, diagnostic, and event models exist.
- [ ] Strict family, ID, alias, preset, color, scale, and saved-state validators pass the complete focused matrix.
- [ ] Preserved unknown and alias resolution are non-destructive.
- [ ] Placeholder presentation never becomes raw proposed state implicitly.
- [ ] Draft edits change only explicit fields.
- [ ] Fake reversible adapter proves stale/failure/rollback behavior.
- [ ] No production source, save, model, controller, UI, or content behavior changes.

### Catalog phase

- [ ] One #183 catalog source is authoritative and hash/version identified.
- [ ] Direct-file and UnityWebRequest paths return identical typed validated results.
- [ ] Missing/invalid production catalog is visible and never silently hybridized with hard-coded defaults.
- [ ] JSON/schema/C#/Fable/validator drift fails deterministically.
- [ ] Player-facing labels resolve from Codex narrative/content source.

### Model/controller phase

- [ ] Queries are immutable and nonmutating.
- [ ] Controller uses drafts and explicit commit/cancel instead of live save mutation/per-click save.
- [ ] Real model application is prepared, verified, and reversible.
- [ ] Async completion cannot act on stale/destroyed/replaced owners.
- [ ] Preserved future IDs survive unavailable catalog/model state and unrelated edits.

### Save/transaction phase

- [ ] Old saves migrate without destructive query-time normalization.
- [ ] Sufficient source/version/operation metadata persists through an explicit compatible schema.
- [ ] Validation/application/persistence/publication has one recoverable boundary.
- [ ] Save failure preserves prior committed state and restores prior visual state.
- [ ] Commit uncertainty reconciles without duplicate application.
- [ ] Exact replay is idempotent and correlation conflict rejects.
- [ ] Events/notifications occur once after durable commit.

### Integrated acceptance

- [ ] Catalog/source, Player packaging, PlayMode profile isolation, async/fault/downgrade, accessibility, and Android lifecycle evidence pass.
- [ ] No existing valid option/preset/color/scale is removed or changed without explicit approval.
- [ ] User approves integrated visual behavior before milestone/release acceptance.

## 33. Implementation handoff

First branch:

```text
codex/customization-contract-planner
```

PR references:

```text
Refs #184
```

Do not use `Fixes #184` for the pure planner phase. Issue #184 remains open through catalog authority, real model/controller integration, save migration/transaction behavior, Player/Android evidence, fidelity review, and user acceptance.

The first PR body must state:

- no save mutation;
- no production controller/loader/catalog/schema/Fable change;
- no real model/UI/content change;
- no shared lock;
- exact source inventory and tests;
- current-base/head evidence;
- which later phases remain blocked.
