# QuestDefinition Asset Authority Validation Specification

**Status date:** 2026-07-15  
**Tracking issue:** #156  
**Implementation PR:** #189  
**Specification owner:** GPT  
**Implementation owner:** Codex engineering mode  
**Audited baseline:** `4a39febff80a6e1bd5ecf6b34a902399ee02fba9`  
**Unity target:** `2022.3.62f3`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

## 1. Goal

Establish one deterministic editor/CI gate that proves every current or future Unity quest-definition asset uses the authoritative `QuestDefinition` type and script GUID, including assets that Unity can no longer resolve through `AssetDatabase.FindAssets("t:QuestDefinition")` because their script reference is missing, zeroed, removed, or incorrect.

The validator must protect serialized identity and field compatibility without authoring quest content, changing narrative meaning, rewriting assets, or assuming one folder is the only quest-asset location.

## 2. Binding authority

The only approved Unity `QuestDefinition` identity is:

```text
source path: Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs
full type:   AL.Data.Definitions.Narrative.QuestDefinition
script GUID: c385b2b183b74184ca75eeffbe2256ef
menu path:   AL/Narrative/Quest
```

The removed identity is:

```text
historical path: Assets/AL/Scripts/Data/Definitions/QuestDefinition.cs
historical type: AL.Data.Definitions.QuestDefinition
removed GUID:    226022aa7500f3e4abc8ac3757707ad8
historical menu: AL/Data/Quest
```

The removed source is not restored and the authoritative `.meta` GUID is not replaced.

## 3. Historical serialized-field contract

Direct inspection of pre-PR-124 base `accc94032eb57c9f4db1887378852bd089edeb8f` proves the removed root definition and the current narrative definition declare the same serialized fields in the same order and types:

| Order | Field | Type |
| --- | --- | --- |
| 1 | `Id` | `System.String` |
| 2 | `Title` | `System.String` |
| 3 | `Description` | `System.String` |
| 4 | `Type` | `AL.Core.QuestType` |
| 5 | `TargetValue` | `System.Int32` |
| 6 | `IsHidden` | `System.Boolean` |
| 7 | `RequiredItemId` | `System.String` |
| 8 | `Trigger` | `AL.Core.TriggerCondition` |
| 9 | `ConflictHint` | `System.String` |
| 10 | `RewardResources` | `System.Collections.Generic.List<AL.Data.Runtime.ResourceData>` |
| 11 | `RewardCredits` | `System.Int32` |
| 12 | `RewardXP` | `System.Int32` |

This means a valid old-root asset requires a script-reference identity migration but no field transformation. A field mismatch in the authoritative type or an unexpected serialized field in an existing candidate asset is a blocking migration-review diagnostic; it must not be silently ignored or dropped by reserialization.

## 4. Current preconditions

The repository currently uses:

```text
unity/ProjectSettings/EditorSettings.asset
m_SerializationMode: 2
```

`2` is Force Text. The authority validator relies on deterministic Unity YAML inspection.

The validation suite must fail visibly if:

- the serialization mode is not Force Text;
- a candidate asset is binary or cannot be read;
- YAML document boundaries or required Unity metadata cannot be parsed.

Do not silently fall back to typed-only discovery when serialized inspection is unavailable.

## 5. Scope

### 5.1 Production scan

Scan every relevant file under the complete Unity project `Assets` tree, without assuming a quest folder:

```text
.asset
.prefab
.unity
.controller
.overrideController
.playable
.anim
.mat
.meta
```

The candidate-object parser applies to YAML documents that can contain `MonoBehaviour` / ScriptableObject records. The repository-wide removed-GUID scan applies to all listed text-serialized file classes.

The production quest-asset scan must enumerate every `.asset` file directly from disk as well as every path returned by the AssetDatabase. This catches files that exist but no longer import as their intended type.

### 5.2 Non-goals

Do not:

- rewrite YAML;
- delete an asset;
- regenerate asset GUIDs;
- create authored quest content;
- change `QuestDefinition.cs` or its `.meta` unless a separately reviewed blocker proves the authority record is wrong;
- modify save data, Build Settings, runtime services, catalogs, Android, dialogue, rewards, or gameplay;
- treat #133/#183 future catalogs as already implemented.

## 6. Validator architecture

Use one editor-only validator that returns immutable ordered diagnostics and is callable from both tests and a future quality gate.

Names may vary, but use equivalent roles:

```text
QuestDefinitionAssetAuthorityValidator
QuestDefinitionAuthoritySnapshot
QuestDefinitionAuthorityDiagnostic
QuestYamlDocument
QuestAssetCandidate
```

Expected placement:

```text
unity/Assets/AL/Editor/Validation/QuestDefinitionAssetAuthorityValidator.cs
unity/Assets/AL/Editor/Validation/QuestDefinitionAssetAuthorityValidator.cs.meta
```

A test-internal helper is acceptable only if the production project scan is still reusable by the complete EditMode suite and later #155 automation. Do not add runtime/player assembly references.

### 6.1 Result shape

A validation result contains at least:

```text
succeeded
serializationMode
scriptPath
scriptGuid
scriptTypeFullName
candidateCount
validAssetCount
malformedCandidateCount
removedGuidOccurrenceCount
duplicateIdCount
diagnostics[]
```

Each diagnostic contains:

```text
stable code
severity
project-relative path
yaml document/local file ID when available
message
expected value when applicable
actual value when safely available
```

Diagnostics are sorted deterministically by path, local file ID, then code.

## 7. Stable diagnostic codes

Use stable codes equivalent to:

```text
AL-QDA-SERIALIZATION-MODE
AL-QDA-AUTHORITATIVE-SCRIPT-MISSING
AL-QDA-AUTHORITATIVE-GUID-CHANGED
AL-QDA-AUTHORITATIVE-TYPE-MISMATCH
AL-QDA-DUPLICATE-PRODUCTION-TYPE
AL-QDA-YAML-UNREADABLE
AL-QDA-YAML-UNPARSEABLE
AL-QDA-SCRIPT-REFERENCE-MISSING
AL-QDA-SCRIPT-FILEID-ZERO
AL-QDA-SCRIPT-GUID-ZERO
AL-QDA-REMOVED-ROOT-GUID
AL-QDA-NONAUTHORITATIVE-SCRIPT
AL-QDA-AUTHORITATIVE-ASSET-UNLOADABLE
AL-QDA-LOCAL-FILEID-UNRESOLVED
AL-QDA-ASSET-TYPE-MISMATCH
AL-QDA-ID-FIELD-CONTRACT
AL-QDA-BLANK-ASSET-ID
AL-QDA-DUPLICATE-ASSET-ID
AL-QDA-SERIALIZED-FIELD-CONTRACT
AL-QDA-UNEXPECTED-SERIALIZED-FIELD
AL-QDA-REQUIRED-SERIALIZED-FIELD-MISSING
AL-QDA-MENU-PATH-MISMATCH
```

The validator reports paths and technical IDs only. It does not emit player-facing copy.

## 8. Script and production-type validation

### 8.1 Authoritative script

Require:

- exact source path exists;
- `AssetDatabase.AssetPathToGUID` equals the authoritative GUID;
- the path loads as `MonoScript`;
- `MonoScript.GetClass()` is non-null;
- exact full type name matches;
- exact type derives from `ScriptableObject`;
- the type is in a production runtime assembly, not a test/editor fixture assembly;
- the current `CreateAssetMenuAttribute.fileName` and `.menuName` match the authority record.

### 8.2 Exactly one production type

Use `TypeCache` or an equivalent complete production-assembly scan.

Filter deliberately:

- include project production assemblies containing runtime definition types;
- exclude test assemblies, editor-only fixture types, and package test fixtures;
- find every `ScriptableObject` type whose simple name is `QuestDefinition` or whose authority role is explicitly registered as such.

Require exactly one result and the exact authoritative full name.

Tests may define fixture classes with similar names without being counted as production types.

## 9. Reflection field-schema guard

Before scanning assets, validate the authoritative type’s declared serialized contract.

Use `BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly`, sort by metadata declaration order, and include fields Unity serializes:

- public instance fields not marked nonserialized;
- non-public fields marked `[SerializeField]` if any are introduced later.

Require the exact 12 historical fields, order, and types from section 3.

At minimum, `Id` must be explicitly proven to:

- exist;
- be an instance field;
- have type `System.String`;
- be public or otherwise intentionally Unity-serialized;
- remain in the expected serialized contract.

Do not use optional access such as:

```csharp
GetField("Id")?.GetValue(...)
```

A missing or changed field is a schema failure, not a blank asset ID.

If the authoritative type intentionally changes later, require a new migration specification and update this record before changing the guard.

## 10. Unity YAML document parser

### 10.1 Document boundaries

Normalize line endings and split Unity YAML on document headers equivalent to:

```text
--- !u!<classId> &<localFileId>
```

Preserve:

- Unity class ID;
- local file ID;
- root object name (`MonoBehaviour`, etc.);
- exact project-relative source path;
- document text;
- top-level serialized field keys.

A `.asset` may contain a main object and subassets. Validate every candidate document, not only the main asset.

### 10.2 Exact `m_Script` parsing

For candidate-capable documents, parse an exact mapping equivalent to:

```text
m_Script: {fileID: 11500000, guid: <32 hex chars>, type: 3}
```

Do not use a broad substring match as the only parser.

Classify explicitly:

- no `m_Script` field;
- `fileID: 0`;
- missing/blank GUID;
- all-zero GUID;
- removed root GUID;
- authoritative GUID;
- another GUID;
- malformed mapping.

### 10.3 Top-level field extraction

Extract only the direct serialized fields of the YAML object. Do not interpret nested `RewardResources` entries as top-level fields.

Ignore Unity infrastructure fields equivalent to:

```text
m_ObjectHideFlags
m_CorrespondingSourceObject
m_PrefabInstance
m_PrefabAsset
m_GameObject
m_Enabled
m_EditorHideFlags
m_Script
m_Name
m_EditorClassIdentifier
```

Compare remaining top-level keys to the exact QuestDefinition field contract.

## 11. Quest candidate classification

A YAML object is a quest-definition candidate when any one of these is true:

1. its script GUID is the authoritative GUID;
2. its script GUID is the removed root GUID;
3. `m_EditorClassIdentifier`, when populated, identifies either the historical or authoritative QuestDefinition full name;
4. it has the complete strict QuestDefinition serialized-field signature from section 3.

The strict field-signature rule exists to catch an intended quest asset whose script identity is missing or replaced. It must require the complete expected field set, not one or two generic fields such as `Id` and `Title`.

A non-quest control object with only a partial overlap is not a candidate.

If a future schema adds fields, update the approved schema before changing candidate classification.

## 12. Candidate validation rules

### 12.1 Removed root GUID

Any serialized occurrence of the removed root GUID is blocking, including a quest asset, prefab, scene, controller, playable, or metadata reference.

Report every exact path and document/local file ID. Do not rewrite it automatically.

### 12.2 Missing or zero script

A quest-shaped candidate with no usable script reference is blocking.

Distinguish:

- absent mapping;
- file ID zero;
- GUID zero/blank;
- malformed mapping.

### 12.3 Non-authoritative script

A quest-shaped candidate referencing any non-authoritative, non-removed GUID is blocking. Report the actual GUID and path without assuming the referenced script is safe.

### 12.4 Authoritative script

For every candidate using the authoritative GUID:

1. load all assets/subassets at the path with `AssetDatabase.LoadAllAssetsAtPath`;
2. use `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` or equivalent to map the YAML local file ID to the exact loaded object;
3. require an object for the candidate document;
4. require its exact runtime type to be the authoritative type;
5. require `MonoScript.FromScriptableObject` to resolve to the authoritative script path/GUID;
6. require every expected serialized field to be present;
7. reject unexpected top-level serialized fields pending migration review;
8. require a nonblank trimmed `Id`;
9. require each `Id` to be globally unique across all authoritative quest-definition objects, including subassets.

Do not validate only `LoadMainAssetAtPath`; a valid or malformed quest may be a subasset.

### 12.5 Unexpected serialized fields

Because the historical and current field contracts are identical, any extra top-level serialized quest field is not part of the approved migration.

Report it as blocking until reviewed. This prevents Unity reserialization from silently discarding unknown historical data.

### 12.6 Missing required fields

A candidate missing one of the 12 required serialized fields is blocking even if the runtime type loads.

## 13. Valid asset round-trip fixture

Create one temporary valid authoritative `QuestDefinition` asset under a unique test folder inside `Assets`, then clean it in `finally`/teardown.

Requirements:

- use `ScriptableObject.CreateInstance` with the authoritative type;
- populate every serialized field with representative non-default values, including one reward resource;
- call `AssetDatabase.CreateAsset`, save, and force import;
- capture a field snapshot before reimport;
- validate the production scanner finds it by path/local file ID;
- require authoritative GUID/type and nonblank unique ID;
- force reimport;
- compare every field exactly after reimport;
- delete the asset/folder and refresh in guaranteed cleanup;
- no generated fixture remains in the committed diff or developer project.

Use a unique fixture ID so it cannot collide with real project assets.

## 14. Malformed YAML fixture matrix

Do not import deliberately broken assets into the real `Assets` tree.

Use the pure YAML parser/classifier against synthetic strings or copies stored under an external temporary directory.

Minimum fixtures:

| Fixture | Expected result |
| --- | --- |
| valid authoritative script + full fields | candidate valid at parser level |
| removed root GUID + full fields | `AL-QDA-REMOVED-ROOT-GUID` |
| no `m_Script` + full fields | `AL-QDA-SCRIPT-REFERENCE-MISSING` |
| `fileID: 0` + full fields | `AL-QDA-SCRIPT-FILEID-ZERO` |
| zero GUID + full fields | `AL-QDA-SCRIPT-GUID-ZERO` |
| unrelated GUID + full fields | `AL-QDA-NONAUTHORITATIVE-SCRIPT` |
| malformed script mapping + full fields | YAML/script diagnostic |
| authoritative GUID + one required field missing | required-field diagnostic |
| authoritative GUID + unexpected field | unexpected-field diagnostic |
| partial generic fields only | not a quest candidate |
| multiple YAML documents, one candidate subasset | exact local file ID reported |
| CRLF and LF forms | deterministic equivalent result |

Fixture diagnostics must be stable and deterministically ordered.

## 15. Duplicate and blank ID fixtures

Using valid temporary authoritative assets or a pure loaded-object seam, test:

- one valid nonblank ID;
- blank string;
- whitespace-only string;
- two assets with the same exact ID;
- case-different IDs, with the chosen ordinal policy stated explicitly;
- duplicate IDs across a main asset and subasset if Unity supports the fixture form.

Use ordinal exact identity unless a separately approved quest-ID policy states otherwise. Do not silently trim and persist an ID.

## 16. Repository inventory requirements

The final PR and issue #156 comment must include current-head output for:

- every occurrence of the removed GUID;
- every occurrence of the authoritative GUID;
- every source reference to either historical/current type;
- every `CreateAssetMenu` path in the QuestDefinition family;
- every editor generator/importer that can create quest definitions;
- every current `.asset` candidate and its local file IDs/IDs, or exact zero count;
- every schema/catalog/shared-contract reference relevant to quest definitions;
- authoritative script/type/menu/schema result;
- exact migrated asset count by old GUID;
- rollback decision.

The historical field comparison in section 3 must be included in `QuestDefinition_Authority_Record.md`.

## 17. Reimport and missing-script evidence

Run from the canonical workspace only:

```text
D:\260711\MY\AndroidStudioProjects\AnotherLife\unity
```

Required evidence:

1. batch import/C# compile using Unity `2022.3.62f3`;
2. complete EditMode suite XML/totals;
3. focused authority validator totals;
4. successful valid-asset create/import/reimport/field-roundtrip fixture;
5. complete malformed YAML fixture matrix;
6. production scan result;
7. log search for missing/unloadable script/import failures;
8. final GUID/source/asset inventory;
9. `git diff --check origin/main...HEAD`;
10. final clean repository status.

Do not report a licensing IPC failure, exit 199, missing XML, duplicate workspace, or skipped fixture as passing evidence.

## 18. Expected implementation boundary

Expected PR #189 files after correction:

```text
unity/Assets/AL/Editor/Validation/QuestDefinitionAssetAuthorityValidator.cs
unity/Assets/AL/Editor/Validation/QuestDefinitionAssetAuthorityValidator.cs.meta
unity/Assets/AL/Tests/EditMode/QuestDefinitionAssetAuthorityTests.cs
unity/Assets/AL/Tests/EditMode/QuestDefinitionAssetAuthorityTests.cs.meta
unity/Docs/QuestDefinition_Authority_Record.md
```

If the helper remains test-only, document why it is still reusable by the project-wide EditMode gate. No runtime assembly may depend on UnityEditor code.

Do not edit:

```text
QuestDefinition.cs
QuestDefinition.cs.meta
LocalQuestService.cs
SideQuestService.cs
LocalGameDataService.cs
SaveGameData.cs
Bootloader.cs
scenes or Build Settings
Android/narrative packets
```

unless a newly verified blocker is first returned to GPT for sequencing.

No designated shared-file lock is expected.

## 19. Acceptance criteria

- [ ] Authoritative script path/GUID/type/menu remain exact.
- [ ] Exactly one production QuestDefinition type exists.
- [ ] The complete historical/current serialized field contract is exact.
- [ ] Force Text serialization is verified.
- [ ] Every `.asset` YAML document is considered, including subassets and unloadable files.
- [ ] Typed discovery is not the only candidate-discovery mechanism.
- [ ] Missing, zero, removed, and non-authoritative script references fail distinctly.
- [ ] Authoritative candidates map by local file ID to the exact loaded authoritative object.
- [ ] Required fields are present and unexpected fields are blocked for review.
- [ ] Every authoritative quest asset has a nonblank globally unique ID.
- [ ] The removed GUID has zero production serialized occurrences.
- [ ] A valid asset survives import/reimport with every field preserved.
- [ ] The malformed YAML fixture matrix passes.
- [ ] The complete repository inventory and rollback decision are recorded.
- [ ] Canonical Unity compile/EditMode/reimport/missing-script evidence passes.
- [ ] No narrative, save, gameplay, catalog, Android, scene, Build Settings, or unrelated change is included.

## Codex handoff

```text
Codex engineering: correct PR #189 from current main using unity/Docs/QuestDefinition_Asset_Authority_Validation_Spec.md. Keep the narrative QuestDefinition type/GUID unchanged. Add a Force-Text YAML validator that scans every .asset document, detects quest-shaped assets even when t:QuestDefinition cannot resolve them, parses exact m_Script identity, maps authoritative documents to loaded objects by local file ID, locks the historical 12-field schema, rejects blank/duplicate IDs and unexpected fields, and covers the full non-imported malformed-YAML matrix plus one valid create/reimport/field-roundtrip fixture. Run all evidence from D:\260711\MY\AndroidStudioProjects\AnotherLife\unity. Do not author quests or edit runtime/save/shared/scene/Android files.
```
