# Phase C8A Skill Authority Convergence

## Document control

| Field | Value |
| --- | --- |
| Tracked issues | [#183](https://github.com/yulee94/AnotherLife/issues/183), [#180](https://github.com/yulee94/AnotherLife/issues/180) |
| Phase | `Phase C8A — skill-family authority convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `b5426cc9b52cbb06bb3c3987f597867fbe010f42` |
| Frozen v001 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Current v003 candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v003` |
| Current v003 raw SHA-256 | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` |
| Legacy skill/weather catalog version | `0.3.0` |
| Binding specifications | `Game_Data_Catalog_Authority_Spec.md`, `Champion_Combat_Encounter_Integrity_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Skill family disposition | `blocked_required` |
| Production eligibility | `false` |
| User content, gameplay, balance, VFX, audio, activation, playtest, and release approval | Pending |

This decision preserves four exact skill identities and four exact English
name references while refusing to promote a partial JSON overlay, slot-index
switch, unused role strings, prototype effect concepts, generated placeholder
prefabs, synthesized tones, mutable `ScriptableObject` fields, or test
fixtures into complete production skill authority.

This is a versioned status overlay. It does not edit v001, v002, or v003,
create or publish a skill catalog, loadout, behavior profile, targeting
profile, presentation profile, VFX/audio reference map, balance revision,
registry, save migration, or runtime integration. It removes no blocker and
does not make #180 or #183 complete.

## 1. Scope and non-goals

C8A decides:

- which exact skill IDs, content references, and source strings are already
  preserved;
- how the nine-field observed rows remain useful migration evidence without
  becoming newly accepted balance or behavior;
- why the current loader produces silent hybrid authority;
- why current gameplay meaning is owned by slot position rather than skill
  identity or the loaded `role`;
- the boundary between content names, presentation profiles, VFX concepts,
  generated prototype prefabs, procedural effects, and audio references;
- the boundary between the legacy JSON Schema/Fable duplicate, the isolated
  six-family schema, and the pure Champion combat contracts;
- the status of every frozen skill blocker.

C8A does not:

- rename the four skills, create aliases, add descriptions or lore, or invent
  additional skill IDs;
- accept current slot order as a complete loadout identity, class ownership,
  unlock policy, input policy, or future optional-slot policy;
- create production behavior, target, resource, cooldown, presentation,
  VFX, audio, availability, or balance profile IDs;
- treat `role`, `vfxKey`, `skillEffects.key`, a prefab filename, a tone key,
  an input fixture, or a slot index as a skill ID;
- approve cooldown, mana, cast time, range, power, multiplier, heal, damage,
  area, break, target, or cancellation behavior;
- edit `SkillDefinition`, `LocalGameDataService`, `SkillLoadoutCatalog`,
  `SkillCaster`, `SkillEffectFactory`, `SaveGameData`, a source JSON, schema,
  generated contract, prefab, scene, workflow, package, or dependency;
- integrate the accepted C1 combat contracts into the arena or overlap the
  world-presentation work in draft PR
  [#341](https://github.com/yulee94/AnotherLife/pull/341).

## 2. Source precedence

Later skill-source and engineering work must consume these sources in order:

1. The Phase C content packet and content map own the four exact English name
   references and strings, and explicitly own no gameplay meaning.
2. The frozen v001 technical source owns the four exact canonical IDs, empty
   alias arrays, current observed rows, empty unavailable anchors, and seven
   blocker IDs.
3. The current v003 source inherits the v001 mappings and unavailable anchors
   and owns the current `blocked_required` disposition.
4. The binding specifications own immutable record/loadout shape, behavior
   and slot separation, target authority, validation, provenance, atomic
   publication, fallback status, and source-approval requirements.
5. The current JSON, mutable Unity components, generated assets, and
   procedural presentation own exact observed migration behavior only.
6. The isolated six-family schema and pure C1 Champion combat contracts own
   reusable validation shapes. Their fixture IDs are not production source.
7. The legacy JSON Schema and Fable records describe an earlier shared-file
   shape. They do not override the binding specifications or prove runtime
   validation.

No lower-precedence source may fill a higher-precedence absence. Matching
names do not make a partial loadout complete, a working slot switch does not
make `role` authoritative, and a generated effect prefab does not establish a
reviewed per-skill presentation or asset reference.

## 3. Exact identity and content authority

The frozen v001 mappings and the Phase C content map preserve exactly:

| Canonical ID and technical anchor | Content reference | Exact English source | Aliases |
| --- | --- | --- | --- |
| `realm_strike` | `skill.realm_strike.name` | `Realm Strike` | none |
| `renewing_guard` | `skill.renewing_guard.name` | `Renewing Guard` | none |
| `warzone_burst` | `skill.warzone_burst.name` | `Warzone Burst` | none |
| `warmaster_breaker` | `skill.warmaster_breaker.name` | `Warmaster Breaker` | none |

The content-map disposition for each row is `verbatim_preserved`. The skills
family has four mappings and zero unavailable anchors. The v003 source
inherits those exact fields rather than replacing them.

This authority is intentionally narrow:

- the canonical IDs and exact content-reference associations are preserved;
- the English strings are initial source text, not proof of a runtime
  localization service;
- there are no approved descriptions or lore entries;
- engineering must not derive a new ID from a content reference or display
  string;
- no case-folded, punctuation-normalized, slot-derived, role-derived,
  VFX-derived, or filename-derived alias is accepted.

The content packet explicitly leaves slot, role, behavior, target, VFX,
presentation, cooldown, mana, cast time, range, power, bot multiplier,
effects, weather, audio, and gameplay meaning out of content scope.

## 4. Exact observed rows and balance boundary

The v001 technical source and legacy JSON retain the current nine-field rows:

| Skill ID | Legacy slot | Role token | Cooldown | Mana | Cast | Range | Power | Bot multiplier | VFX key |
| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `realm_strike` | `0` | `melee_damage` | `4` | `20` | `0.05` | `2.6` | `150` | `0.72` | `realm_slash` |
| `renewing_guard` | `1` | `self_heal_guard` | `8` | `30` | `0.35` | `0` | `180` | `0` | `renewing_guard` |
| `warzone_burst` | `2` | `area_damage` | `10` | `45` | `0.45` | `4.2` | `115` | `0.72` | `warzone_shockwave` |
| `warmaster_breaker` | `3` | `elite_break_damage` | `14` | `60` | `0.65` | `3.4` | `260` | `0.72` | `warmaster_breaker` |

These values are exact migration evidence. They permit a later comparison to
detect accidental drift. They do not approve:

- the current values or acceptable ranges as production balance;
- the role strings as complete behavior profiles;
- slot order as skill meaning;
- `vfxKey` as a behavior, presentation-profile, or asset-reference ID;
- a zero range or multiplier as a complete target/effect rule;
- a four-row array as a complete versioned loadout.

Any accepted value change or explicit acceptance of these values requires a
separate reviewed balance decision. C8A preserves the evidence and keeps
`skills.balance_acceptance` open.

## 5. Current definition, loader, and runtime boundary

### 5.1 Legacy definition, service, and save state

`SkillDefinition` is a mutable `ScriptableObject` shape with:

```text
Id
DisplayName
Icon
SkillTargetType TargetType
Cooldown
Power
```

No committed `.asset`, prefab, or scene references the
`SkillDefinition.cs.meta` GUID at the audited main. The shape lacks
schema/content/catalog versions, behavior, resource/cooldown policy,
presentation profile, complete numeric fields, source revision, and raw
hash. `LocalGameDataService.GetSkill(string)` always returns `null`, and no
production caller resolves a skill through that service.

`SaveGameData` stores no skill-loadout ID/version, skill definition version,
cooldown snapshot, action identity, or skill action receipt. Current runtime
slot state therefore cannot be represented as persistent definition
authority.

### 5.2 Partial catalog and hybrid application

`al_skill_weather_catalog.json` has version `0.3.0` and contains the same four
loadout rows. The current loader does not make that file authoritative:

- it parses `version` but neither validates nor returns it;
- any nonempty `skillLoadouts` array succeeds;
- it retains no game, catalog-set, loadout, class/profile, source revision,
  raw hash, or trusted provenance identity;
- it returns mutable row arrays;
- a file/read/parse/web failure warns and continues toward hard-coded
  defaults;
- direct-file and UnityWebRequest paths have different timing and no request
  generation, cancellation, supersession, or disposed-owner guard.

`SkillCaster` begins with four hard-coded arrays and applies each returned row
field by field:

- null or out-of-range rows are skipped;
- missing slots retain every hard-coded field;
- blank ID, display-name, or VFX values retain the old field;
- non-finite numeric values retain the old field;
- negative numeric values clamp to zero;
- a later duplicate slot overwrites earlier fields;
- duplicate skill IDs are not rejected;
- the loaded `role` field is never applied or queried.

The result can contain fields from several unreported authorities while the
component reports that shared loadouts were applied. This violates the
binding requirement that a production loadout and every referenced
definition/profile validate as one immutable snapshot before publication.

### 5.3 Slot-index behavior and action lifecycle

Current execution switches on slot position:

| Slot | Current hard-coded behavior and presentation call |
| ---: | --- |
| `0` | damage targets; spawn realm slash; play generic skill-cast tone |
| `1` | heal self; spawn renewing guard; play heal tone |
| `2` | damage targets; spawn warzone shockwave; play generic skill-cast tone |
| `3` | damage targets; spawn warmaster breaker; play heavy-skill tone |

Changing a loaded ID or role does not change this switch. Moving an ID to
another slot changes its behavior. Skill meaning is therefore not owned by a
stable skill or behavior-profile identity.

The current cast flow also spends mana before starting the coroutine,
resolves the slot effect after a wait, and begins cooldown only after effect
resolution. Cancelling the coroutine does not refund the spent mana, start
the cooldown, or publish a typed terminal action receipt. C8A neither changes
nor approves this lifecycle; #180 remains the implementation owner.

### 5.4 Target authority

`SkillDefinition.TargetType` is not populated by any committed definition and
is not consulted by `SkillCaster`. Current damage targeting uses:

- `Physics.OverlapSphere`;
- a `Dummy_` GameObject-name prefix;
- `BossDummyAI` and `BotChampionAI` component lookup;
- Unity instance IDs for duplicate suppression;
- realm-enum comparison for bot hostility.

Physics may later discover candidate runtime handles, but the binding
contract requires each target to resolve through a stable participant
identity in the same encounter/attempt and pass team, target-profile,
life-state, range, shape, line-of-sight, and action-ledger rules. Current
name/component/instance-ID behavior is migration evidence, not target
authority.

## 6. Presentation, VFX, and audio boundary

### 6.1 Presentation content

The four name references in section 3 are ready for a future presentation
profile. No current source supplies production presentation-profile IDs,
description/lore references, icon references, per-skill degradation policy,
or a complete localization runtime.

A future presentation profile must keep content/localization and visual/audio
references separate from behavior. Missing presentation may degrade only
through an explicit visible policy; it cannot substitute another skill's
meaning.

### 6.2 Competing VFX key surfaces

The repository contains three different VFX-oriented surfaces:

1. skill-row keys: `realm_slash`, `renewing_guard`,
   `warzone_shockwave`, and `warmaster_breaker`;
2. four realm effect-concept keys in the same JSON:
   `stonehold_forge_burst`, `eldergrove_healing_bloom`,
   `crownlands_royal_strike`, and `umbral_curse_mark`;
3. four generated prefab filenames matching the realm effect concepts.

`SkillLoadoutCatalog` represents only `version` and `skillLoadouts`; it does
not represent or load the JSON effect or weather arrays. `GetSkillVfxKey`
has no external caller. `SkillCaster` directly invokes procedural factory
methods by slot, and `SkillEffectFactory` does not resolve the generated
prefabs or catalog keys through `Resources`, Addressables, or another
catalog-owned asset reference.

`SkillEffectsAndWeather.md` explicitly calls the generated assets prototypes
for blocking and gameplay testing, not final production art. The concepts,
prefabs, row keys, and procedural calls are all valuable visual migration
evidence. None supplies a reviewed per-skill VFX profile and stable production
asset reference.

### 6.3 Procedural audio

The audited Unity `Assets/AL` tree contains zero committed `.wav`, `.mp3`,
`.ogg`, `.aif`, `.aiff`, or `.flac` files. `RuntimeCombatAudio`, defined
inside `SkillEffectFactory.cs`, creates short `AudioClip` tones at runtime
with generic keys such as:

```text
skill_cast
heavy_skill
heal
impact
```

Those tones are current feedback behavior, not per-skill approved audio
assets or catalog references. They do not resolve
`skills.audio_asset_refs`.

## 7. Schema and contract boundary

### 7.1 Legacy shared-file schema and Fable duplicate

`al-skill-weather.schema.json` requires top-level `version`,
`skillLoadouts`, `skillEffects`, and `weatherProfiles`. Its loadout array has
`minItems: 4` but no `maxItems`. Each row requires the observed fields,
restricts slots to `0..3`, and gives numeric fields a minimum of zero.

It does not enforce:

- a supported catalog/loadout version or catalog identity;
- exactly four rows;
- unique slots or unique IDs;
- nonblank strings;
- behavior, target, presentation, VFX, audio, or content-reference
  resolution;
- source revision/hash/provenance;
- atomic publication or explicit fallback status.

No repository test executes this JSON Schema. The Fable
`SkillWeatherCatalog`, `SkillLoadout`, and `SkillEffect` records duplicate the
legacy shape, are not generated from the schema, have no drift check, and
have no Unity or Android runtime consumer. Neither surface proves production
validity.

### 7.2 Isolated six-family schema

`GameDataSixFamilySchemas` defines a nonempty skills family with required:

```text
name_ref
behavior_profile_id
presentation_profile_id
target_type
cooldown_seconds
power
mana_cost
cast_time_seconds
range_meters
vfx_asset_ref
audio_asset_ref
```

Its tests prove structural field rules and fixture validation. The registry
contains no real skill records or loader registration and is not wired to
`SkillCaster` or `LocalGameDataService`. The schema does not itself provide
the missing profiles, assets, reviewed numeric ranges, source candidate, or
production loadout.

### 7.3 Pure C1 Champion combat contracts

PR [#290](https://github.com/yulee94/AnotherLife/pull/290) provides immutable
`CombatSkillDefinition`, `CombatSkillSlotBinding`, `CombatSkillLoadout`,
`ValidatedCombatSkillLoadoutSnapshot`, and
`CombatContractReferenceCatalog` shapes plus strict pure validation.

The accepted contracts establish important engineering behavior:

- skill behavior and targeting are stable references, not slot meaning;
- fixed-unit numeric values, schema/content versions, source revision, and
  raw hash are retained;
- the initial playable loadout validates exactly four unique slots `0..3`;
- loadout ID, Champion/class profile ownership, input and optional
  availability references are explicit;
- the loadout, skills, references, and expected hashes publish atomically;
- source collections are frozen before publication.

Their tests deliberately use structural fixture IDs such as `skill.one`,
`skill.two`, `loadout.test`, `combat.behavior.damage`,
`combat.target.hostile`, `combat.resource.standard`,
`combat.cooldown.standard`, and `combat.presentation.test`. These values
prove contract behavior only. They are not production skill, behavior,
target, policy, presentation, loadout, class, balance, or asset records.

The pure contracts are the required destination for later #180 integration.
They do not authorize manufacturing the missing source needed to populate
them.

## 8. Open integration boundary

Draft PR [#341](https://github.com/yulee94/AnotherLife/pull/341) currently
changes arena world presentation, camera behavior, evidence tooling, tests,
and presentation documents. It does not change `SkillCaster`,
`SkillLoadoutCatalog`, the skill catalog, the six-family source, or this C8A
document, so it does not overlap this coordination-only slice.

Later runtime skill integration must still rebase over the accepted arena
state and use the C1 contracts rather than create a second loadout or
encounter authority path. C8A makes no runtime edit and places no lock on the
draft PR.

## 9. Blocker disposition

The literal v003 skills row remains unchanged and `blocked_required`.
Current effective dispositions are:

| Blocking ID | C8A disposition |
| --- | --- |
| `skills.slot_policy` | **Observed and contract precursors exist; remains open.** The four current slots and C1 exact-`0..3` validation are preserved, but no production loadout ID/version, Champion-or-class owner, reviewed input binding, availability/unlock mapping, or accepted source publication exists. |
| `skills.behavior_profiles` | **Open.** Role tokens and the slot switch show current effects, but `role` is unused, meaning changes with slot, and no stable reviewed behavior-profile records exist. |
| `skills.presentation_profiles` | **Content precursor exists; remains open.** Four names/references are ready, but no complete presentation-profile IDs, icon/content resolution, VFX/audio mappings, or degradation policies exist. |
| `skills.target_authority` | **Open.** The unused legacy target enum and name/component/instance-ID runtime scan do not supply stable target profiles or participant-registry rules. |
| `skills.audio_asset_refs` | **Open.** Generic synthesized runtime tones are not reviewed per-skill audio assets or stable asset references. |
| `skills.vfx_asset_refs` | **Prototype precursors exist; remains open.** Row keys, realm concept keys, generated placeholder prefabs, and procedural factory calls are not mapped into reviewed per-skill VFX profiles or stable production asset references. |
| `skills.balance_acceptance` | **Exact migration evidence exists; remains open.** Current numeric rows are preserved but are not user-approved balance, reviewed bounds, or proof of valid behavior/target combinations. |

No skill blocker is removed from a technical-source candidate in C8A. The
four canonical identities and names are preserved source; the complete
technical records and a production loadout remain unavailable.

## 10. Source and engineering gates

A complete skill source, registry slice, or runtime authority switch is not
yet authorized. Before source authoring can complete, reviewed inputs must
provide:

1. one stable production loadout ID, schema/content version, Champion-or-class
   owner, exact slot/input bindings, and availability/unlock policy;
2. one stable behavior profile per skill with complete effect semantics that
   remain invariant when slot position changes;
3. one compatible target profile per skill, including disposition, intent,
   area/range/line-of-sight, participant, and duplicate-hit rules;
4. one presentation profile per skill that consumes the existing content
   reference and names exact icon, VFX, audio, and visible degradation
   policy;
5. exact approved production VFX/audio asset references or an explicitly
   approved procedural-reference policy;
6. an accepted decision for every numeric value and its reviewed validation
   bounds, including contradictory behavior/target/range rejection;
7. user approval for visible content, gameplay meaning, balance, VFX/audio
   fidelity, and final activation where required.

After those inputs exist, separate focused work may:

1. author a non-wired skill/loadout source candidate with the four preserved
   IDs and content references;
2. validate exact identity, uniqueness, order, versions, references, fixed
   units, hashes, provenance, and dependency completeness;
3. map that source into the accepted immutable C1 contract shapes;
4. replace the partial overlay with typed all-or-explicit-development-fallback
   loading under #180;
5. integrate target/action/cooldown behavior and presentation asset loading
   through one validated snapshot;
6. add any required save migration and Player/Android packaging evidence only
   in their owning phases.

Those slices must not infer source from slots, role strings, VFX/tone keys,
prefab filenames, enum values, procedural switches, or test fixtures. A
missing or invalid production snapshot must remain visibly unavailable
rather than silently hybridize with hard-coded arrays.

## 11. Pinned current evidence

Hashes are lower-case SHA-256 over exact committed bytes at the audited main.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `Game_Data_Catalog_Authority_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d8a0e2fdcd4e98bbb6379a8f2b7d7c733f869bd95f99112e1635f25dafb7b74d` | Skill identity, schema, slot, profile, validation, publication, and fallback contract |
| `Champion_Combat_Encounter_Integrity_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `5a3b133b0a49138fd3e76a286fd731e38923c888f452fc9cfdaf8595f9e1a1e8` | Combat skill/loadout, target, action, atomic-publication, and migration contract |
| `Game_Data_Source_Inventory.md` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `3e7e1ad01471d5e1b9aed2e07d613de3435a91b8520bb1185cc52aefe8f03622` | Current split authority, exact keys, consumers, generated assets, and drift inventory |
| `Phase_C_Six_Family_Source_Packet.md` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `aa63db30d2342e95e81d3bd54225bd3fa774ce0eab88136fe1eaf042c6d4a1a2` | Four exact names/content references and explicit gameplay exclusions |
| `Phase_C_Six_Family_Technical_Handoff.md` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `719ed1c09c39074bf7041edde87131b27e889c3e9b55fa11676fba32b871caf0` | Four exact IDs/observed rows and production-field requirements |
| `phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Frozen mappings, empty aliases/anchors, observed rows, and seven blocker IDs |
| `phase-c-six-family-technical-source-v003.json` | `779e7363fca9ffed9e412f43cc74b20665fa4e9c` | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` | Current inherited skill source and blocker state |
| `phase-c-six-family-content-map.json` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353` | Exact four content references and English strings |
| `al_skill_weather_catalog.json` | `0d2b176e2577bbeb85589855909021ad44289874` | `cc53bc7f876a718bbee048c983994ece0b9dd98d70fcc9bd6779e687cc50e3dc` | Legacy four-row loadout plus separate effect/weather concepts |
| `al-skill-weather.schema.json` | `0d2b176e2577bbeb85589855909021ad44289874` | `2211ef1298a839c3202330b5acab02900aca83127eae62d9d67088f29cf1f38f` | Incomplete legacy shared-file validation shape |
| `AnotherLife.Contracts.fs` | `de169bea281d1158909083a7a63069439d719b7d` | `561089cab8e50dcb16f262847bd256158fa711063eb85d3685b6358f44716c74` | Ungenerated Fable duplicate records with no runtime consumer |
| `SkillEffectsAndWeather.md` | `6d621c02e4182f1508ec8fd51e8bd3f7e1e2712c` | `279b6d5b931453effb969be5e70bcafd90ed07e4e9b3cfccfc3ca562759d84d9` | Prototype VFX concepts and explicit non-production-art status |
| `SkillDefinition.cs` | `a9bffb60a463fad7759ce02e45dff4ac7f8425c7` | `4959a43426c0fd5e956ce240ddeb71fee283384e1501f895b76f654bd7256dc0` | Uninstantiated mutable legacy definition shape |
| `LocalGameDataService.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9` | Always-null skill lookup |
| `SkillLoadoutCatalog.cs` | `c48870b246084e7188a2cac294c69cc96c74fd1f` | `90210d1b3cabe7183d185d82c300f563f182609ae707f5ec7c40a6d67804982c` | Version-ignoring, mutable, any-nonempty legacy loader |
| `SkillCaster.cs` | `8a5b2675a5963e66cf27018368f3f396a82b3b78` | `3bbde91fcaf610b61c597652d702a0873574f2ffa9e09890bd24b4097b6fecde` | Hard-coded arrays, field overlay, slot behavior, targeting, and cast lifecycle |
| `SkillEffectFactory.cs` | `7edc3c28110a66bc228f03dda7e725ca14e49cd3` | `f67baca7e2a9e749a3ee1fe65d700ea60678d652cb70b0b8df8f4c145dd8ae19` | Procedural VFX and synthesized generic runtime audio |
| `SaveGameData.cs` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `bacfac499e8f2ac359a104054f5aef5f795f58f184c9febeb666ed6a69a15fbf` | Current absence of persistent skill/loadout/action authority |
| `GameDataSixFamilySchemas.cs` | `a2e6a9a0dddfb7522d880d4db9d17222adcbbffe` | `3c759d9ea2f1b2d6aca53d1e5f213bf0edb057eb0751bf3c9bfe9ae94b15d9bb` | Non-wired required skill record shape |
| `GameDataSixFamilySchemaTests.cs` | `a2e6a9a0dddfb7522d880d4db9d17222adcbbffe` | `a74d4c1c6c1de795ffa081d952b26902ff7227e147652b9f30ae9fd516e5f799` | Structural six-family schema fixtures, not production records |
| `CombatProfileContracts.cs` | `0280ea9a49a2fa9693d43cb0ae1b426ff00b726c` | `c964357b4c507aa1c13627f662328b359137f37f39f9fd0dbc8bb9b41276dbda` | Immutable skill/loadout/reference and atomic snapshot contracts |
| `CombatSkillAndLoadoutValidationTests.cs` | `0280ea9a49a2fa9693d43cb0ae1b426ff00b726c` | `7e85e90d807ec90d0f49dbc17fc2a0264a83d076458ff670370a1ba6def37e32` | Pure validation coverage with non-production fixtures |
| `CombatContractTestData.cs` | `0280ea9a49a2fa9693d43cb0ae1b426ff00b726c` | `91916e86dea4d9df2726d3ac6a306db8fdaebe7f7b2758c64f1e5922b272ec9a` | Exact fixture IDs proving shape rather than source authority |

Any drift in a pinned source byte, one of the four identities/references,
observed values, loader failure mode, slot behavior, prototype boundary,
contract shape, or missing-authority conclusion blocks later skill work until
a reviewed superseding decision reconciles it.

## 12. Validation and acceptance

- [x] all four v001 mappings, technical anchors, empty alias arrays, and empty
  unavailable anchors are preserved;
- [x] all four exact content references and English strings remain
  `verbatim_preserved`;
- [x] content scope remains limited to names, with no invented description,
  lore, behavior, presentation, or gameplay meaning;
- [x] all nine observed fields per skill are recorded as migration evidence
  rather than approved balance or behavior;
- [x] the v003 `blocked_required` disposition and all seven literal blocker
  IDs remain unchanged;
- [x] `SkillDefinition` remains uninstantiated and `GetSkill` remains
  unresolved;
- [x] version ignoring, any-nonempty success, mutable arrays, per-field
  fallback, missing-row retention, duplicate overwrite, and non-finite/
  negative handling remain visible;
- [x] the loaded `role` and externally readable VFX key are not promoted into
  behavior or presentation authority;
- [x] slot-index behavior, cast/cancel ambiguity, and name/component/instance
  target lookup remain migration issues owned by #180;
- [x] name references, VFX row keys, realm effect keys, generated prototype
  prefabs, procedural factory calls, and synthesized tones remain separate;
- [x] the legacy JSON Schema/Fable duplicate, isolated six-family schema, and
  pure C1 contracts are not mistaken for populated production source;
- [x] pure-contract fixture IDs are not promoted into production records;
- [x] zero blocker, source JSON, schema, registry, definition, runtime, save,
  asset, scene, workflow, package, dependency, or production output changed;
- [x] production eligibility, runtime authority, and user approval state
  remain unchanged.

## Impact

This phase adds one coordination document only. It adds no runtime code,
managed assembly bytes, Player content, asset duplication, allocation,
frame-loop work, network call, package, install byte, or device requirement.
Unity, Android, Player, PlayMode, device, profiler, package-size, audio, VFX,
and visual evidence are not applicable to this documentation-only decision.
