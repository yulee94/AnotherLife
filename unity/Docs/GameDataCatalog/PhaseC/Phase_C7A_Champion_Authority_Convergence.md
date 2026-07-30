# Phase C7A Champion Authority Convergence

## Document control

| Field | Value |
| --- | --- |
| Tracked issues | [#183](https://github.com/yulee94/AnotherLife/issues/183), [#180](https://github.com/yulee94/AnotherLife/issues/180), [#184](https://github.com/yulee94/AnotherLife/issues/184) |
| Phase | `Phase C7A — champion-family authority convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `fb0e582841d47fb7f17595408728ec174f4bcccd` |
| Frozen v001 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Current v003 candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v003` |
| Current v003 raw SHA-256 | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` |
| Binding specifications | `Game_Data_Catalog_Authority_Spec.md`, `Champion_Combat_Encounter_Integrity_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Champion family disposition | `blocked_required` |
| Production eligibility | `false` |
| User product, identity, balance, visual-fidelity, activation, playtest, and release approval | Pending |

This decision reconciles the empty Champion source family with the approved
four-realm visual direction, the customization source precursors, current
Unity runtime behavior, and the pure Champion encounter contracts merged in
PR [#290](https://github.com/yulee94/AnotherLife/pull/290). It preserves the
approved visual work as a valuable source precursor while refusing to turn
concept-sheet labels, customization presets, demo object names, hard-coded
combat values, generic skill slots, or planner fixtures into production
Champion records.

This is a versioned status overlay. It does not edit v001, v002, or v003,
create a Champion source candidate, registry, artifact, combat profile, or
loadout, change a schema or runtime service, select a final model or portrait,
publish a catalog, or make #180, #183, or #184 complete.

## 1. Scope and non-goals

C7A decides:

- that the current Champion family contains zero canonical mappings,
  unavailable anchors, content entries, or committed definition records;
- the exact boundary between approved four-realm visual direction and
  production identity, class, portrait, model, or runtime-asset authority;
- the exact boundary between customization option/preset source and a
  Champion definition;
- which mutable definition fields, save fields, runtime object paths, combat
  values, skill slots, and pure-planner fixtures are migration evidence
  rather than production source;
- the current conflict and drift inside observed combat values;
- the status of every frozen Champion blocker.

C7A does not:

- invent one or more canonical Champion IDs, aliases, display names, or
  localization keys;
- treat `Player_Champion`, a prefab filename, a concept-sheet filename,
  `Vanguard`, a realm name, a customization preset, or a test fixture as a
  record ID;
- assign a Champion to a realm, `ClassFamily`, or subclass;
- approve a narrative identity, face/body range, final portrait, production
  mesh, texture set, topology, rig, shader, measured runtime budget, or
  runtime model reference;
- approve current health, mana, regeneration, attack, movement, dodge,
  targeting, cooldown, mana-cost, power, or loadout values as balance;
- select base skills from current slot order or populate
  `ChampionDefinition.BaseSkills`;
- change Champion identity, appearance, combat behavior, skills, balance,
  encounter behavior, presentation, or saved state;
- edit `ChampionDefinition`, `LocalGameDataService`, `SaveGameData`,
  `ChampionCombat`, `ChampionArenaSceneController`,
  `ProceduralChampionModelBuilder`, `SkillCaster`, a source JSON, schema,
  asset, prefab, scene, workflow, package, or dependency.

## 2. Source precedence

Later Champion-source work must consume these sources in order:

1. The Phase C1 content packet and map own the explicit conclusion that no
   Champion content record is authored.
2. The frozen v001 candidate owns the empty mappings, empty unavailable
   anchors, and six blocker IDs.
3. `FourRealmChampionAnchor.md` and its approved image sheets own bounded
   realm visual direction only.
4. The customization technical and content catalogs own option/preset
   composition and presentation-reference source only.
5. Current Champion Unity types and runtime paths own exact observed
   migration behavior only.
6. The binding specifications own immutable record/profile shape,
   validation, provenance, migration, encounter resolution, publication, and
   user-approval requirements.
7. PR #290 owns pure Champion encounter planning and transition behavior. It
   owns no production Champion definition, combat profile, skill loadout, or
   balance value.

No lower-precedence source may fill a higher-precedence absence. In
particular, a visually approved realm comparison does not create a record,
and a working procedural arena does not prove that its object name, floats,
or four slot IDs are authoritative.

## 3. Exact record and identity absence

The literal current source state is:

| Source surface | Exact Champion state |
| --- | --- |
| Frozen v001 technical source | `mappings: []`; `unavailableAnchors: []` |
| Current v003 technical source | inherits the empty v001 fields; `artifactDisposition: blocked_required` |
| Phase C1 content map | `sourceStatus: not_authored_unavailable_requiredness_unresolved`; `entries: []` |
| Committed `ChampionDefinition` assets | zero |
| `LocalGameDataService.GetChampion(string)` | always returns `null` |

`ChampionDefinition` is an uninstantiated mutable `ScriptableObject` shape:

```text
Id
DisplayName
Realm
Family
Portrait
BaseSkills[]
```

The type is not a record. It has no content version, source revision/hash,
catalog-set identity, immutable collection boundary, combat-profile
reference, skill-loadout identity, or model reference. No production caller
successfully resolves an instance through the game-data service.

Consequently there is no exact alias table, supported-record set, authored
order, player-facing Champion name, or definition-to-save migration. File
names, Unity object names, enum strings, localized text, and hashes must not
be transformed into missing canonical IDs.

## 4. Approved visual precursor boundary

`FourRealmChampionAnchor.md` version `0.2` is explicitly:

```text
Owner-approved visual source direction; production implementation not yet approved
```

It approves:

- a four-realm comparison across Stonehold, Eldergrove, Crownlands, and
  Umbral;
- the same adult Vanguard role, scale, camera, and approximate rig envelope
  for comparison;
- realm silhouettes, equipment zones, construction language, macro material
  families, restrained magical focal points, and mobile readability;
- one anchor image plus four multi-angle turnaround/material sheets as
  concept and modeling guidance.

That approval is preserved without narrowing or weakening it. It is a
valuable precursor for later asset authoring and review.

It does not approve:

- a narrative Champion identity, canonical record ID, display name, or
  localization reference;
- a production `RealmId` or `ClassFamily` assignment;
- a final face/body range, portrait, runtime model, mesh, maps, topology,
  rig, shader, or measured runtime budget;
- a per-record asset address or a definition-to-asset mapping;
- combat stats, base skills, loadout ownership, or progression meaning.

`Vanguard` is a `SubclassId` in current Unity code, not one of the
`ClassFamily` values `Warrior`, `Mage`, `Ranger`, or `Assassin`. The visual
comparison label therefore cannot satisfy `champions.realm_class_assignments`
by string association. Even a future Vanguard-to-Warrior design decision
would require an explicit reviewed record mapping.

## 5. Customization and save-state boundary

The technical customization catalog defines appearance options, realm
presentation entries, quality targets, and forge presets. The content catalog
maps those existing option and preset IDs to localization keys and draft
source copy. Their handoff explicitly prohibits preset wording from implying
class ownership, realm transfer, combat stats, item entitlement, or story
progression.

Those sources may later compose the appearance of a separately resolved
Champion. They do not define:

- which Champion records exist;
- a Champion display-name/localization reference;
- realm or class ownership;
- a combat profile or base-skill list;
- a portrait/model reference owned by a Champion record.

The current save stores `SelectedRealm` and a
`ChampionCustomizationState` containing appearance option IDs, colors, and
two toggles. It stores no Champion definition ID, definition version, combat
profile ID, skill-loadout ID, class assignment, or portrait/model reference.
Appearance state therefore cannot be used as an implicit Champion identity.

Final visible wording and integrated character-creation presentation remain
user-gated under #184.

## 6. Runtime, asset, combat, and skill evidence

### 6.1 Generic runtime presentation

The current arena creates a Capsule, names it `Player_Champion`, adds
`ChampionCombat`, `SkillCaster`, and `ChampionController`, and then calls
`ProceduralChampionModelBuilder.EnsureModel`. The object name is runtime
presentation state, not a stable catalog ID.

The repository also contains the generated generic prefab
`AL_ModularChampion_Base.prefab`. No current runtime path resolves that
prefab through a Champion record or loads it as a per-record asset reference.
The procedural builder and generic prefab are useful implementation
precursors; neither satisfies `champions.asset_refs`.

### 6.2 Exact observed combat values

Current `ChampionCombat` serialized defaults are:

| Field | Current code value | Authority disposition |
| --- | ---: | --- |
| maximum health | `1000` | migration evidence only |
| maximum mana | `100` | migration evidence only |
| mana regeneration per second | `7.5` | migration evidence only |
| attack power | `50` | migration evidence only |

The binding encounter specification's observed inventory says mana
regeneration is `10/second`, while current code is `7.5`. It also records the
unresolved `ChampionCombat` attack-power `50` versus
`ChampionController` basic-attack damage `125` conflict. These are source
drift and competing-authority evidence, not grounds to choose a winner.

The required immutable combat profile concept includes stable identity,
schema/content/catalog versions, fixed-unit health, mana, regeneration, and
attack values, behavior/movement/dodge/targeting profile references, source
revision, and raw hash. Current mutable floats do not supply that profile.

### 6.3 Generic slot IDs are not base skills

`SkillCaster` currently exposes four ordered slot IDs:

| Slot | Runtime ID |
| ---: | --- |
| 0 | `realm_strike` |
| 1 | `renewing_guard` |
| 2 | `warzone_burst` |
| 3 | `warmaster_breaker` |

The arena attaches that generic component without resolving a
`ChampionDefinition`. No populated `ChampionDefinition.BaseSkills` array or
Champion-owned immutable skill-loadout record exists. The skills family is
also independently `blocked_required`. Slot presence, order, names, combat
values, and realm-dependent behavior therefore do not satisfy
`champions.base_skill_refs`.

## 7. Pure planner boundary

PR #290 supplies pure immutable Champion encounter definition, request,
resolution, state-transition, retry, and correlation behavior. Its contract
requires stable identities such as:

```text
ChampionDefinitionId
ChampionCombatProfileId
SkillLoadoutId
```

Its tests deliberately use structural fixtures such as `champion.test`,
`champion.combat.test`, `loadout.test`, `boss.test`, and `rules.test`. These
values prove validation and state-machine behavior only. They do not
constitute production records, aliases, profiles, loadouts, or balance.

Future integration must consume the accepted planners rather than create a
second encounter-authority path. It cannot instantiate an authoritative
encounter from real Champion source until the family and its referenced
skills/assets/profiles are complete.

## 8. Blocker disposition

The literal v003 Champion row remains unchanged and `blocked_required`.
Current effective dispositions are:

| Blocking ID | C7A disposition |
| --- | --- |
| `champions.records` | **Open.** No stable canonical ID, alias, supported-record decision, order, committed `ChampionDefinition` asset, or resolvable service record exists. |
| `champions.localization` | **Open.** Customization option/preset keys do not name a Champion identity. No Champion display-name reference is authored. |
| `champions.realm_class_assignments` | **Open.** The four-realm Vanguard comparison is visual direction, not a per-record `RealmId`/`ClassFamily` mapping. `Vanguard` is not a `ClassFamily` value. |
| `champions.asset_refs` | **Approved precursor exists; remains open.** Concept sheets and a generic procedural model/prefab exist, but no per-record final portrait/model reference or approved production asset exists. |
| `champions.base_skill_refs` | **Open.** Four generic runtime slot IDs are not a populated per-record base-skill list or immutable loadout, and the referenced skills family remains incomplete. |
| `champions.stat_profiles` | **Partial migration evidence exists; remains open.** Current `1000`, `100`, `7.5`, and `50` floats are unapproved, incomplete, and affected by documented `7.5`/`10` drift plus the `50`/`125` attack conflict. |

No Champion blocker is removed from a technical-source candidate in C7A.
The family cannot produce one complete immutable definition while record,
identity, localization, assignment, final asset, skill, and combat-profile
decisions are absent.

## 9. Source and engineering gates

A complete Champion source or registry slice is not yet authorized. Before
source authoring can complete, reviewed product/content/balance/visual input
must provide:

1. the supported Champion record set, stable canonical IDs, authored order,
   and exact legacy aliases where any exist;
2. one localization/name reference for every record;
3. one explicit realm and class-family assignment for every record, with
   separate subclass meaning where required;
4. one final approved portrait/model reference policy and exact reference for
   every record;
5. one ordered base-skill/loadout mapping whose referenced skill definitions
   are themselves complete and valid;
6. one immutable Champion combat profile per required record, including a
   reviewed resolution for regeneration `7.5` versus `10` and attack `50`
   versus `125`, plus all required behavior-profile references;
7. user approval for identity, visible copy, visual fidelity, supported
   records, and final balance.

After those inputs exist, separate focused work may:

1. author the Champion source packet without runtime changes;
2. produce a non-production immutable technical candidate with exact
   identities, assignments, asset references, skill references, profiles,
   and provenance;
3. validate deterministic order, uniqueness, references, fixed-unit bounds,
   hashes, dependency completeness, and drift;
4. only later integrate the accepted source through the PR #290 encounter
   planners.

Those slices must not infer source from filenames, object names, enum names,
concept labels, customization presets, hard-coded runtime values, or test
fixtures. They must not wire `GetChampion`, migrate a save, replace the
procedural path, or activate an authoritative encounter before the complete
family and its dependencies pass.

## 10. Pinned current evidence

Hashes are lower-case SHA-256 over exact committed bytes at the audited main.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `Game_Data_Catalog_Authority_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d8a0e2fdcd4e98bbb6379a8f2b7d7c733f869bd95f99112e1635f25dafb7b74d` | Champion record, identity, publication, and absence contract |
| `Champion_Combat_Encounter_Integrity_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `5a3b133b0a49138fd3e76a286fd731e38923c888f452fc9cfdaf8595f9e1a1e8` | Combat profile, loadout, encounter, migration-conflict, and provenance contract |
| `Game_Data_Source_Inventory.md` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `3e7e1ad01471d5e1b9aed2e07d613de3435a91b8520bb1185cc52aefe8f03622` | Current record absence, hard-coded path, and consumer inventory |
| `Phase_C_Six_Family_Source_Packet.md` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `aa63db30d2342e95e81d3bd54225bd3fa774ce0eab88136fe1eaf042c6d4a1a2` | Explicit no-authored-content decision |
| `Phase_C_Six_Family_Technical_Handoff.md` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `719ed1c09c39074bf7041edde87131b27e889c3e9b55fa11676fba32b871caf0` | Required fields, absence policy, and generation refusal |
| `phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Frozen empty mappings/anchors and blocker IDs |
| `phase-c-six-family-technical-source-v003.json` | `779e7363fca9ffed9e412f43cc74b20665fa4e9c` | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` | Current inherited Champion source and blocker state |
| `phase-c-six-family-content-map.json` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353` | Empty Champion content and unavailable status |
| `FourRealmChampionAnchor.md` | `8efa49b7e3ceb7a55d297b0bd71603e3ecf255c4` | `402a6cc0d5d1230c8375f95051c9931f53de36876db9bd11c37face47d9ca0f5` | Approved visual-source scope and explicit production exclusions |
| `Champion_Customization_Label_Source_Handoff.md` | `1aeb42f3fa5795f1a6e4e6408f5d008190813369` | `9d7da86a9e263bc7aaf92fcd6fc9dbf149eabeb786cc2918c4e1f9fd2e51891d` | Customization presentation ownership and identity guardrails |
| `al_character_customization_catalog.json` | `b5a9d98ea46021641f059756c599c859ed5e470d` | `3c0e265d947fa0e62c3042a4614a2dd50cdb36ee8e0272071ca2d241fdc8ab24` | Technical appearance options and presets, not Champion records |
| `al_character_customization_content_catalog.json` | `1aeb42f3fa5795f1a6e4e6408f5d008190813369` | `ced64c0d9cba02d4e24d73984fbb814f928e476bf2c01a0c3bef54f11b78c844` | Customization localization references, not Champion identity localization |
| `ChampionDefinition.cs` | `a9bffb60a463fad7759ce02e45dff4ac7f8425c7` | `8c85a74acdb88505256199218ad3158ca3556472a145de75805b72856c6d1b22` | Uninstantiated mutable legacy definition shape |
| `LocalGameDataService.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9` | Always-null Champion lookup |
| `SaveGameData.cs` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `bacfac499e8f2ac359a104054f5aef5f795f58f184c9febeb666ed6a69a15fbf` | Realm/customization state and missing Champion source identity |
| `ChampionCombat.cs` | `92446f30078623e1c3eb2c4b82d1eb52e2cbf2ae` | `1e99993061ba0d69dc4848e3fffaac29f3e47839a6c2287054428ea7d5ed7d89` | Exact mutable health, mana, regeneration, and attack defaults |
| `ChampionArenaSceneController.cs` | `8a5b2675a5963e66cf27018368f3f396a82b3b78` | `984e4ec9ec97e36f2e2e764412986d2886696992a729a542d1f8fe72ecae17c7` | Capsule construction and procedural runtime composition |
| `ProceduralChampionModelBuilder.cs` | `ac4c4a8a9019fc44dc0864d369539b3d1943f07c` | `8878ee21a45e583fd8e770347fe95aa259de8950a9bc93d8e8723ac218084e5a` | Generic procedural appearance path |
| `SkillCaster.cs` | `8a5b2675a5963e66cf27018368f3f396a82b3b78` | `3bbde91fcaf610b61c597652d702a0873574f2ffa9e09890bd24b4097b6fecde` | Four generic runtime slot IDs and tuning arrays |
| `ChampionEncounterPlanner.cs` | `0280ea9a49a2fa9693d43cb0ae1b426ff00b726c` | `eb1e1aa35692b5884b6b355992e6d4cae7ffc2112884b3243f95b905660b17ab` | Pure immutable encounter identity and planning contract |
| `ChampionEncounterPlannerTests.cs` | `0280ea9a49a2fa9693d43cb0ae1b426ff00b726c` | `0d221e07f709a55cef881d8e6b17b6e118cca5094155704a6b37c7fefa183f44` | Fake structural fixtures, explicitly not production source |

Any drift in a pinned source byte, empty-family conclusion, approval boundary,
runtime value, slot identity, save field, or missing-authority conclusion
blocks later Champion work until a reviewed superseding decision reconciles
it.

## 11. Validation and acceptance

- [x] v001 empty Champion mappings and unavailable anchors are preserved;
- [x] the v003 `blocked_required` disposition and all six literal blocker IDs
  remain unchanged;
- [x] the content map remains explicitly unauthored with zero entries;
- [x] zero stable production IDs, localization references, committed
  `ChampionDefinition` records, and resolvable service records are confirmed;
- [x] the owner-approved four-realm visual direction is preserved while its
  production exclusions remain explicit;
- [x] visual `Vanguard` wording is not promoted into a `ClassFamily`
  assignment;
- [x] customization options, presets, labels, save state, generic prefab,
  procedural model, and arena object name are not promoted into Champion
  identity or asset authority;
- [x] exact `1000`, `100`, `7.5`, and `50` runtime defaults are recorded as
  migration evidence rather than newly approved balance;
- [x] regeneration `7.5`/`10` drift and attack `50`/`125` conflict remain
  unresolved and visible;
- [x] the four runtime slot IDs and pure-planner fixtures are not promoted
  into base-skill or loadout authority;
- [x] no record, localization, assignment, asset reference, base skill,
  stat profile, or missing source is inferred;
- [x] v001, v002, and v003 remain unchanged;
- [x] production eligibility, runtime authority, and user approval state
  remain unchanged;
- [x] no registry, schema, artifact, runtime, save, combat, customization,
  model, asset, scene, workflow, package, dependency, or production output
  changed.

## Impact

This phase adds one coordination document only. It adds no runtime code,
managed assembly bytes, Player content, asset duplication, allocation,
frame-loop work, network call, package, install byte, or device requirement.
Unity, Android, Player, PlayMode, device, profiler, package-size, and visual
evidence are not applicable to this documentation-only decision.
