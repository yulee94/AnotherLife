# Phase C3A Realm Authority Convergence

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | `#183` |
| Phase | `Phase C3A — realm-family authority convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `bd10300ba71cfe6e2f778883b83461403617017f` |
| Frozen inventory baseline | `7e4ed2828e4e6df9dd33bdab3b3e4560651e45b7` |
| Binding specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Phase C1 source | `unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json` |
| Phase C2 technical source | `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source.json` |
| Current specialized source | `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Production eligibility | Blocked |
| User final creative, balance, activation, playtest, and release approval | Pending |

This decision reconciles the current realm sources without producing a common
family artifact or changing runtime authority. It fixes the future common
realm record order, preserves exact identity mappings and cross-catalog
references, classifies every material current-main delta since the Phase B
inventory, and dispositions the three Phase C2 realm blockers.

This document is source and coordination evidence only. It does not make the
realm family, the six-family catalog set, or issue `#183` complete.

## 1. Scope and non-goals

This phase decides:

- the deterministic order and exact identity of the four future common realm
  records;
- the source precedence between authored content, technical mappings, approved
  visual source, the current specialized realm catalog, and a later generated
  common family artifact;
- the exact world-boundary references that the accepted common realm snapshot
  must retain;
- the allowed rare-resource migration evidence;
- the exact neutral Arcane Axis asset references available to a later
  non-wired generator;
- shadow comparison, rollback, provenance, and approval boundaries.

This phase does not:

- emit `catalog-set.json`, a common `realms` artifact, or any other production
  catalog bytes;
- change `GameDataSixFamilySchemas`, create or wire `GameDataCatalogStore`, or
  register a loader;
- edit `IGameDataService`, `LocalGameDataService`, `Bootloader`, saves, scenes,
  UI, runtime consumers, tests, assets, packages, or workflows;
- change the authority of `RealmCatalogRuntime` or the legacy runtime-created
  `RealmDefinition` objects;
- approve or infer realm perks, capability profiles, rare-resource balance,
  final colors, realm placement, asset atlases, or gameplay behavior;
- claim final user approval or release completion.

## 2. Binding realm record decision

### 2.1 Deterministic order

The future common realm family record order is:

```text
crownlands
stonehold
eldergrove
umbral
```

This is the authored order in `al_realm_catalog.json` and the order published
by its specialized immutable snapshot. The Phase C1 content map and Phase C2
technical map list their realm entries in legacy enum order
`stonehold, eldergrove, crownlands, umbral`; those arrays remain valid source
evidence but do not control generated common-family record order after this
decision.

Validators and generators must consume the explicit order above. They must not
sort by enum value, display text, dictionary enumeration, path, locale, hash,
or incidental source-array order.

### 2.2 Exact identity and reference matrix

| Record order | Canonical ID | Exact legacy enum | Numeric value | Name reference | Description reference | Inner realm | Main gate | Outer warzone | Safe rare-resource evidence |
| ---: | --- | --- | ---: | --- | --- | --- | --- | --- | --- |
| 1 | `crownlands` | `RealmId.Crownlands` | `3` | `realm.crownlands.name` | `realm.crownlands.description` | `inner_crownlands` | `gate_crownlands_meridian` | `warzone_crownlands` | `ResourceType.RoyalSigil` |
| 2 | `stonehold` | `RealmId.Stonehold` | `1` | `realm.stonehold.name` | `realm.stonehold.description` | `inner_stonehold` | `gate_stonehold_faultline` | `warzone_stonehold` | `ResourceType.DeepOre` |
| 3 | `eldergrove` | `RealmId.Eldergrove` | `2` | `realm.eldergrove.name` | `realm.eldergrove.description` | `inner_eldergrove` | `gate_eldergrove_greenveil` | `warzone_eldergrove` | `ResourceType.WorldSap` |
| 4 | `umbral` | `RealmId.Umbral` | `4` | `realm.umbral.name` | `realm.umbral.description` | `inner_umbral` | `gate_umbral_ashvein` | `warzone_umbral` | `ResourceType.DarkCrystal` |

There are no realm aliases in this phase.

`RealmId.None`, numeric `0`, undefined enum values, blank IDs, unknown stable
IDs, case variants, and normalized or fuzzy matches are invalid. They never
resolve to a record and never fall back to Crownlands or another realm.

The common realm snapshot must retain `innerRealmId`, `mainGateId`, and
`outerWarzoneId` equivalents. The current Phase C2 realm schema does not
represent these fields, so a later engineering phase must extend and test the
schema before generation. This document records the required values but does
not change the schema.

### 2.3 Player-facing source boundary

The exact common-family name and description strings remain the Phase C1
content-map values behind the references above. They are not copied from
`al_realm_catalog.json` display names and are not derived from enum names.

The following current values are explicitly excluded from technical capability
or balance authority:

- percentage and perk prose embedded in `LocalGameDataService`;
- selection UI labels such as `FORTRESS ECONOMY`, `GROWTH ENGINE`,
  `ROYAL COMMAND`, and `SHADOW WARFARE`;
- `identityPillars`, `starterClassBias`, and continuity prose in the
  specialized narrative catalog;
- realm-specific architecture motion, material, layout, or visual profiles;
- provisional colors, visual-review values, and hard-coded UI colors.

They may remain in their owning source or presentation paths. A later common
realm generator must not encode them as gameplay capability profiles.

## 3. One authored/generated relationship

The retained source relationship is:

1. `phase-c-six-family-content-map.json` is the authored source for the exact
   common-family player-facing name and description references and English
   strings.
2. `al_realm_catalog.json` is the authored specialized four-realm launch source
   for realm order, stable launch IDs, exact legacy runtime names,
   `innerRealmId`, `mainGateId`, `outerWarzoneId`, realm-gem references, and
   account-selection continuity.
3. `phase-c-six-family-technical-source.json` is the non-production technical
   mapping and blocker record. This C3A decision supersedes only its realm
   mapping-array order; the four enum/value/content-reference mappings remain
   unchanged.
4. The approved Arcane Axis design and neutral runtime exports are the visual
   source for future realm asset references. They do not supply gameplay,
   color, atlas, or runtime-surface authority.
5. A future common `realms` family artifact is generated deterministically
   from the pinned sources and this reviewed decision. It is never hand-edited
   and never self-authorizes source meaning.

`al_realm_catalog.json` and `RealmCatalogRuntime` remain the sole specialized
selection-catalog source and snapshot until an owning engineering migration
explicitly switches that consumer. A future common family artifact may exist
only as a non-published shadow candidate until all required fields validate
and the switch is accepted.

There must never be two silently competing live realm authorities. The later
switch must choose one of these outcomes:

- the specialized selection artifact is retired and its required selection
  fields move into, or are deterministically derived from, the common selected
  source; or
- the specialized artifact remains a deterministic derivative of the same
  reviewed inputs and is no longer independently authored.

Keeping both artifacts independently editable or publishing both snapshots as
realm-definition authority is rejected.

## 4. Current-main delta since the Phase B inventory

The Phase B inventory froze `main` at
`7e4ed2828e4e6df9dd33bdab3b3e4560651e45b7`. The classifications below cover
the material realm source and consumer changes through the audited current
main.

| Delta class | Current paths | Disposition |
| --- | --- | --- |
| Legacy definition source | `LocalGameDataService.cs`, `IGameDataService.cs`, `RealmDefinition.cs` | Still effective. The service still creates four mutable `ScriptableObject` definitions in legacy enum insertion order, exposes them through nullable lookups/live enumeration, and leaves `Icon` null. Realm copy is unchanged. |
| Specialized launch source and loader | `al_realm_catalog.json`, `RealmCatalogRuntime.cs` | Added. The bounded loader validates four stable IDs, exact enum-name mappings, selection policy, two unique gem IDs per realm, and publishes specialized authored order. It does not implement the common manifest/envelope or the full common realm schema. |
| Split selection authority | `LocalRealmService.cs`, `IRealmService.cs`, `RealmSelectionContracts.cs`, `RealmCharacterConstraint.cs` | Added/hardened. A committed identity now requires both a specialized catalog entry and a legacy runtime definition. This fails closed but confirms two live inputs rather than one common realm authority. |
| Selection presentation | `RealmSelectionController.cs`, `RealmSelectionCard.cs` | Still enumerates `IGameDataService.GetAllRealms()` and renders legacy name/description values. It adds hard-coded command labels, colors, and serialized Arcane Axis emblems. It does not enumerate the specialized snapshot or resolve common content references. |
| Save/profile/boot integration | `ISaveGameService.cs`, `SaveGameData.cs`, `SaveSemanticCandidateValidation.cs`, `LocalSaveGameService.cs`, `Bootloader.cs`, `BootController.cs`, `KingdomSceneController.cs` | Identity persistence and invalid-state handling were hardened. These paths consume or preserve `RealmId`; they do not define realm records or authorize fallback mappings. |
| NVS integration | `Nvs01CatalogModels.cs`, `Nvs01CatalogValidator.cs`, `INvs01QuestRuntime.cs`, `Nvs01ProgressPersistence.cs` | Adds realm/profile gates and checks the specialized catalog version. It is a downstream narrative consumer, not realm-family source. |
| Safe rare-resource evidence | `ResourceRules.cs` | Adds `TryGetRareResourceForRealm`, which rejects `None` and undefined realms. The older `GetRareResourceForRealm` still maps unsupported values to `RoyalSigil` and is excluded from shadow or generation authority. |
| Champion realm context | `ChampionRealmContext.cs`, `ChampionArenaSceneController.cs`, `ChampionController.cs`, `SkillCaster.cs`, `BossDummyAI.cs`, `BotChampionAI.cs`, `RvrBotSpawner.cs`, `ChampionEncounterPlanner.cs` | Current Champion work requires explicit committed realm context and removes audited Crownlands substitutions. These consumers use realm identity but do not define common realm content, capabilities, or assets. |
| Realm gem and territory use | `LocalRealmGemService.cs`, `TerritoryContractPlanner.cs` | Uses exact realm identity in owning domain contracts. Neither path creates a realm definition or supplies a common capability profile. |
| Kingdom and architecture use | `CityLayoutEngine.cs`, `KingdomBuildingLayout.cs`, `KingdomVisualizer.cs`, `ArchitectureConstructionAnimationController.cs`, `ArchitectureConstructionAnimationProfile.cs`, `KingdomBuildingConfirmedLevelTransition.cs`, `KingdomBuildingModelCatalog.cs` | Realm-specific layout and presentation behavior remains in the architecture/kingdom domain. It is not a common realm capability or asset-reference catalog. |
| Editor architecture generators | The 17 changed realm-named builders under `unity/Assets/AL/Scripts/Editor/Architecture/` | Author or validate building visual assets only. They are not realm definition sources and cannot supply common realm gameplay profiles. |
| Cross-cutting typed context | `NotificationContracts.cs`, `NotificationValidation.cs` | May carry or validate realm-related context as data. It does not query or define realm-family authority. |
| Specialized narrative catalogs | `al_realm_gem_wishgate_content_catalog.json`, `al_relationship_authority_content_catalog.json`, `al_world_atlas_narrative_catalog.json` and their source handoffs | Reference canonical realm IDs for their owning narrative domains. They remain source/evidence and do not become common realm-definition artifacts. |
| World topology contract | `World_Atlas_Topology_And_Query_Contract.md` | Requires the accepted realm snapshot to retain the exact inner-realm, main-gate, and outer-warzone references above. Realm-to-ring-slot/compass placement remains unresolved. |
| Common six-family schema/source | `GameDataSixFamilySchemas.cs`, `phase-c-six-family-technical-source.json`, `Phase_C_Six_Family_Technical_Handoff.md` | Added but unwired. The realm schema requires rare-resource, capability-profile, and asset references but currently omits the required world-boundary references. |

Changed terrestrial, architecture handoff, quest/chapter, world, and visual
source documents that contain realm names remain in their owning source modes.
Their presence does not create a common realm record, alias, capability
profile, rare-resource identity, or asset mapping unless this decision names
it explicitly.

## 5. Realm blocker disposition

| Blocking ID | C3A disposition | Evidence and required next step |
| --- | --- | --- |
| `realms.rare_resource_catalog` | **Open, narrowed** | The four exact enum relations in the identity matrix are accepted migration and shadow-comparison evidence only through `TryGetRareResourceForRealm`. There is no reviewed common resource catalog or stable-ID resolver that can prove a future `rare_resource_id` resolves. The fallback-returning method is prohibited evidence. A later coordination/engineering decision must define exact stable resource IDs, aliases if any, and a resolvable immutable authority without changing balance. |
| `realms.capability_profiles` | **Open** | No approved common capability-profile records exist. Perk prose, UI command labels, starter-class bias, identity pillars, Champion behavior, architecture profiles, and realm-specific visual treatments are not substitutes. A separate source/balance decision must define exact profile IDs and behavior before production generation. |
| `realms.asset_refs` | **Source resolved for a future shadow artifact; runtime activation remains pending** | The exact tintable Arcane Axis flat sprites below are selected as the future realm record `asset_ref` values. Their geometry and neutral cross-platform derivatives are owner-approved. This does not approve final colors, atlases, first consuming surface, runtime loading, residency, compression changes, or device budgets. |

Because the rare-resource and capability-profile blockers remain open, the
realm family remains `artifactDisposition: blocked_required` and
`productionEligible: false`.

### 5.1 Exact future `asset_ref` values

| Canonical realm ID | Future `asset_ref` | Unity GUID | Raw PNG SHA-256 |
| --- | --- | --- | --- |
| `crownlands` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Crownlands_Flat_256_v001.png` | `ba4dfcc7b514049f79f6ec3424193b46` | `f5c7e351ec930aac69f6df02d03034bc38c465ed8dfa787dd4feba044f33f82b` |
| `stonehold` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Stonehold_Flat_256_v001.png` | `94d8d9e2cf04a4b769c213a13c164b8e` | `53d220dc8b938d212963286133ca39e1968fa1421126559dd56bdfde9c437946` |
| `eldergrove` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Eldergrove_Flat_256_v001.png` | `53001b27fd9d14914984211765be4391` | `1d45fc8fba82ebb3fdc1c4f819026ea8e45b11c248378371c7b2b6923c6e0cac` |
| `umbral` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Umbral_Flat_256_v001.png` | `a426041e03b0742999a34b8b5e198406` | `a9daefa3ea6445ba2db680dad92a456db75becebec8848c678b29d5ea2c85aaa` |

The approved micro derivatives remain presentation companions, not alternative
realm record identities:

| Canonical realm ID | Micro derivative | Unity GUID | Raw PNG SHA-256 |
| --- | --- | --- | --- |
| `crownlands` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Crownlands_Micro_32_v001.png` | `f0a1c4e7a626e49b49887b6cb2db6cbb` | `5f604f91b3e18a891154421bb2b339a5c9b0ffa4a3a127e2410732e34e8390c3` |
| `stonehold` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Stonehold_Micro_32_v001.png` | `b4af53fe46dd84ddd820d414ebbb8cd2` | `7b3446d52e09bff87d007d1e118283db75de37827c72bbf27a94cc084045d547` |
| `eldergrove` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Eldergrove_Micro_32_v001.png` | `2efa55572f30240d780dd067c90483a1` | `3aba07673473d2d8cc15827a2f3b02880441d6ae4c82742deeda19b1f3d6e768` |
| `umbral` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Umbral_Micro_32_v001.png` | `76771735bbc6546d4a5d685c12dcca00` | `ccb01ebc5eb68fbccd9951bd038fd99bca55626d83f9e00345f658065e9e8578` |

Changing any selected path, GUID, or protected geometry requires a new
coordination and visual-source disposition. A build/import process must verify
the committed GUID and bytes before publishing an artifact that references
them.

## 6. Pinned provenance

Hashes below are lower-case SHA-256 over the exact committed file bytes visible
at the audited current main. Future generation must hash committed raw bytes,
not newline-normalized or reserialized content.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json` | `2119c89bfa985a0a3e273042cf086a99a49b45b0` | `33321936662b98f9c18edf4122ad163053d1aff3017b06556cad694420e9e8d8` | Specialized authored order, IDs, selection continuity, world-boundary and gem references |
| `unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353` | Exact common-family content references and source strings |
| `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Enum/value mappings, observed rare-resource anchors, blocker record |
| `unity/Assets/AL/Art/Designs/FourRealmHeraldry.md` | `8efa49b7e3ceb7a55d297b0bd71603e3ecf255c4` | `02d97d5b1b8d7ca3e51701ab910c9b5a53e363709e3226e8f762ed5134b8bcc4` | Owner-approved Arcane Axis design boundary |
| `unity/Assets/AL/Art/Heraldry/RuntimeExports/README.md` | `8efa49b7e3ceb7a55d297b0bd71603e3ecf255c4` | `5c8a85971fbd8982722986bf16c8dc4b07a53bfc37a637f0d8dfaaa8c1ceb22c` | Neutral sprite matrix, checksums, and import/runtime limits |
| `unity/Docs/World_Atlas_Topology_And_Query_Contract.md` | `a97d5e5cf0afc0701ff57d6d110ce056632b4eec` | `022b57137d2ea7900da36a0519536c60a21359a05ec0262d671579a05fe24dc0` | Requirement to retain exact inner-realm, main-gate, and outer-warzone references |
| `unity/Assets/AL/Scripts/Core/Enums/Enums.cs` | audited at `bd10300ba71cfe6e2f778883b83461403617017f` | `36e3c430d97c39ca6f487b1a682353f157d808052de28919d766b2bba6190d4a` | Current exact enum names and values |
| `unity/Assets/AL/Scripts/Core/ResourceRules.cs` | audited at `bd10300ba71cfe6e2f778883b83461403617017f` | `26a907e49afeecd8c741c6d9ed9bd12549a735a3798184509ede001e551bf087` | Safe and fallback rare-resource migration behavior |
| `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` | audited at `bd10300ba71cfe6e2f778883b83461403617017f` | `7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9` | Current effective legacy definition and copy baseline |
| `unity/Assets/AL/Scripts/RealmSelection/RealmCatalogRuntime.cs` | audited at `bd10300ba71cfe6e2f778883b83461403617017f` | `68172745c215935b6c3a3da668c67f740c5430d5938524e219ed41dbac0c7299` | Current specialized parser, ordering, and publication behavior |

A later generated artifact must record:

- the eventual merged revision of this decision;
- every consumed source path, revision, and raw hash above;
- the generator/tool revision and deterministic command;
- the generated artifact raw hash;
- an explicit user-approval state that remains separate from merge state.

Generation must fail when a pinned source byte changes without an updated,
reviewed decision.

## 7. Shadow comparison gate

Before any common realm snapshot is published to gameplay, a later engineering
phase must:

1. resolve the rare-resource stable-ID authority and all capability-profile
   references;
2. extend the realm schema for the three required world-boundary references;
3. generate the four records twice from clean inputs and prove identical bytes,
   hashes, record order, and ordered diagnostics;
4. validate exact canonical ID, enum name/value, content reference,
   world-boundary reference, asset reference, and safe rare-resource relation
   for every record;
5. resolve every content reference to the exact Phase C1 string and report any
   difference from the current `LocalGameDataService` string;
6. compare the candidate authored order and specialized fields against
   `al_realm_catalog.json`;
7. query `None`, undefined enums, unknown IDs, case variants, and malformed
   references and prove typed rejection with no Crownlands fallback;
8. run the candidate without publishing it and prove current selection,
   profile, NVS, realm-gem, Champion, kingdom, territory, and UI behavior is
   unchanged;
9. retain the specialized snapshot and legacy service as the only live
   authority throughout shadow validation;
10. obtain the separate coordination, source-fidelity, and user gates required
    for the proposed switch.

The specialized catalog's account-lock policy, realm-gem references, and
narrative continuity remain in its owning `#173` scope unless a later reviewed
common schema deliberately absorbs them. Their absence from the Phase C2
common realm schema is not permission to drop them during consumer migration.

## 8. Rollback contract

Before the authority switch:

- rollback is simply removal or rejection of the unpublished shadow candidate;
- `RealmCatalogRuntime` and the legacy realm definitions remain unchanged;
- no save or player identity is rewritten.

For a later accepted switch:

- retain the previous manifest, common artifact, specialized artifact, raw
  hashes, generator revision, and service implementation;
- rollback restores the previous selected artifact and service implementation
  atomically;
- preserve numeric `RealmId` save values exactly;
- reject a newer unsupported or unknown realm instead of remapping it;
- do not write, normalize, migrate, or clear a save merely because the catalog
  rolled back;
- keep old source until every persisted reference and owning consumer passes
  backward-aware validation;
- record source-text rollback separately from technical artifact rollback.

A rollback must never reactivate the unsupported-value-to-Crownlands or
unsupported-value-to-`RoyalSigil` fallback as authority.

## 9. User approval state

| Decision | State |
| --- | --- |
| Exact four canonical IDs and legacy enum/value preservation | Technical compatibility decision; no new user creative decision |
| Authored record order `crownlands, stonehold, eldergrove, umbral` | Retained current approved source order |
| Arcane Axis flat/micro geometry | Project-owner approved on 2026-07-23 |
| Neutral cross-platform sprite derivatives | Approved source derivatives; exact future references pinned here |
| Current name/description strings | Preserved source; final product/release copy approval remains pending |
| Rare-resource relation | Existing migration evidence only; not new balance approval |
| Stable rare-resource IDs/catalog authority | Pending |
| Gameplay capability profiles | Pending source and user balance/product approval |
| Final realm colors, accessibility alternatives, atlas, first runtime surface, and device budgets | Pending |
| Realm-to-world ring slot or compass placement | Pending; no mapping is inferred |
| Common catalog generation and runtime activation | Pending |
| Irreversible profile UX, integrated playtest, and release | Pending user approval |

Merging this document does not satisfy any pending user gate.

## 10. Acceptance and next phase

Phase C3A is accepted when review verifies:

- [x] current main and the Phase B inventory baseline are pinned;
- [x] all four IDs, enum names, values, content references, and authored order
      are exact;
- [x] `None`, undefined, unknown, normalized, and fallback outcomes are
      prohibited;
- [x] exact inner-realm, main-gate, and outer-warzone references are retained;
- [x] the specialized authored source and future generated common artifact have
      one non-competing relationship;
- [x] every material realm source/consumer delta since Phase B is classified;
- [x] all three Phase C2 realm blockers have an explicit disposition;
- [x] source revisions, raw hashes, asset GUIDs, shadow comparison, rollback,
      and user-approval state are pinned;
- [x] no production artifact, runtime wiring, source rewrite, balance inference,
      shared-file edit, or authority switch is included.

The next safe work is another coordination/source step that resolves the exact
rare-resource stable-ID authority and capability-profile source. Only after
those decisions and the required schema extension may Codex engineering
propose a non-wired deterministic realm-family shadow artifact. The
`LocalGameDataService.cs` shared-file lock remains reserved for the later
focused runtime migration phase.

## Impact

This document adds no runtime code, generated catalog, texture, mesh, audio,
scene, save field, package, dependency, loader, allocation, frame loop, render
cost, build byte, install byte, or device behavior. Runtime performance,
memory, packaging, and compatibility are unchanged. Player-build, PlayMode,
device, and integrated playtest evidence are not applicable to this
documentation-only phase and remain required before activation.
