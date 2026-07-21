# QuestDefinition Authority Record

**Status date:** 2026-07-21
**Owner:** Codex
**Issue:** #156
**Roadmap phase:** Phase 0 recovery gate before trusted Unity source/asset baseline

## Decision

`AL.Data.Definitions.Narrative.QuestDefinition` is the single authoritative Unity quest definition type.

Authoritative script:

```text
Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs
GUID c385b2b183b74184ca75eeffbe2256ef
```

The removed root definition is not restored:

```text
AL.Data.Definitions.QuestDefinition
GUID 226022aa7500f3e4abc8ac3757707ad8
```

## Serialized field contract

The authoritative narrative type preserves the historical root serialized schema exactly. The validator locks these twelve fields in declaration order and exact CLR type:

```text
Id: System.String
Title: System.String
Description: System.String
Type: AL.Core.QuestType
TargetValue: System.Int32
IsHidden: System.Boolean
RequiredItemId: System.String
Trigger: AL.Core.TriggerCondition
ConflictHint: System.String
RewardResources: System.Collections.Generic.List<AL.Data.Runtime.ResourceData>
RewardCredits: System.Int32
RewardXP: System.Int32
```

Any missing, unexpected, reordered, or type-changed serialized quest field now fails the authority gate until a migration specification updates this record.

## Inventory

Current-head tracked repository search (`git grep -il`) finds the narrative GUID `c385b2b183b74184ca75eeffbe2256ef` in exactly:

```text
unity/Assets/AL/Editor/Validation/QuestDefinitionAssetAuthorityValidator.cs   (AuthoritativeGuid constant)
unity/Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs.meta    (the authoritative script meta)
unity/Docs/QuestDefinition_Asset_Authority_Validation_Spec.md                 (binding specification)
unity/Docs/QuestDefinition_Authority_Record.md                                (this record)
```

Current-head tracked repository search finds the removed root GUID `226022aa7500f3e4abc8ac3757707ad8` in exactly:

```text
unity/Assets/AL/Editor/Validation/QuestDefinitionAssetAuthorityValidator.cs   (RemovedRootGuid constant)
unity/Docs/QuestDefinition_Asset_Authority_Validation_Spec.md                 (binding specification)
unity/Docs/QuestDefinition_Authority_Record.md                                (this record)
```

Neither GUID occurs in any serialized Unity file under `unity/Assets` other than `QuestDefinition.cs.meta` itself, and the repository tracks zero `.asset` quest candidates, so the migrated-asset count by old GUID is 0.

The regression tests read both GUIDs from `QuestDefinitionAssetAuthorityValidator.AuthoritativeGuid` and `QuestDefinitionAssetAuthorityValidator.RemovedRootGuid`, so the constants in the validator remain the single executable source for authority and old-root detection. Future valid `QuestDefinition` ScriptableObject assets are expected to reference the narrative GUID in their serialized `m_Script` field.

`Assets/AL/ScriptableObjects/Quests` currently contains no tracked quest `.asset` files requiring GUID migration.

Under Codex-only governance the validation workspace recorded by `Phase_1_NVS_01_Status.md` supersedes the historical spec section 17 worktree path; every evidence run must report its exact workspace path, base commit, head commit, branch, and clean/dirty state.

Current source references to QuestDefinition-family types are:

```text
Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs
Assets/AL/Scripts/Data/Definitions/Narrative/SideQuestDefinition.cs
Assets/AL/Scripts/Data/Definitions/Narrative/SkillSoulQuestDefinition.cs
Assets/AL/Scripts/Kingdom/Quests/LocalQuestService.cs
Assets/AL/Scripts/Kingdom/Quests/SideQuestService.cs
Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
```

Current QuestDefinition-family `CreateAssetMenu` entries are:

```text
AL/Narrative/Quest
AL/Data/SideQuest
AL/Narrative/SkillSoulQuest
```

No tracked schema, catalog, editor importer, or editor generator currently creates serialized `QuestDefinition` assets.

## Safeguard

`QuestDefinitionAssetAuthorityValidator` and `QuestDefinitionAssetAuthorityTests` validate the selected authority in EditMode:

- the authoritative script path keeps GUID `c385b2b183b74184ca75eeffbe2256ef`;
- the loaded class is `AL.Data.Definitions.Narrative.QuestDefinition`;
- the `CreateAssetMenu` path remains `AL/Narrative/Quest`;
- exactly one production `ScriptableObject` type named `QuestDefinition` is discoverable;
- the authoritative `QuestDefinition` derives from `ScriptableObject` in the production runtime assembly;
- the authoritative serialized field contract remains identical to the historical twelve-field schema by name, order, and exact CLR type;
- Unity serialization mode is Force Text before disk YAML scanning;
- every relevant serialized Unity text file is scanned for the removed root GUID;
- every `.asset` YAML document is parsed directly from disk, including documents Unity cannot resolve through typed `AssetDatabase.FindAssets`;
- quest candidates are detected by authoritative GUID, removed GUID, editor class identifier, or the strict full-field signature;
- `m_Script` references distinguish missing, malformed, duplicate-key, unexpected-key, wrong file ID, missing/wrong type, malformed GUID, zero file ID, zero GUID, removed GUID, and non-authoritative GUID states; a mapping containing any key other than `fileID`, `guid`, and `type` fails deterministically;
- the production-type filter is proven by tests: a `QuestDefinition` fixture in the EditMode test assembly and emitted `AL.Editor*`/`*.Tests`-namespace types in a production-named assembly do not count, while a second production-shaped `QuestDefinition` type does count and fails validation;
- `QuestDefinitionAssetAuthorityValidator.LogProjectAuthoritySnapshot` is invocable through `-executeMethod` to emit the full `[QDA-EVIDENCE]` production-scan counters and diagnostics for retained batch-mode evidence, exiting nonzero on failure;
- candidate YAML class ID and root object name are validated as Unity MonoBehaviour/ScriptableObject serialization;
- authoritative quest documents map to loaded objects by local file ID, including subassets;
- required fields, unexpected fields, blank IDs, and duplicate IDs fail deterministically;
- malformed candidates are counted separately from fully valid assets; `ValidAssetCount` means the script reference, YAML metadata, field contract, loaded-object mapping, authoritative type/script, and nonblank unique ID all passed;
- non-imported malformed YAML fixtures cover the required broken-reference matrix;
- one temporary valid authoritative quest asset is created, imported, reimported, mapped by local file ID, field-compared, and deleted during EditMode validation.

## Migration Result

No tracked serialized assets required mutation. The safe migration is therefore to preserve the narrative script GUID as authority and add regression validation that fails if either the removed root GUID reappears or future quest assets bind to the wrong definition type.

Rollback decision: no automatic YAML rewrite, asset deletion, GUID regeneration, or narrative/content mutation is authorized by this gate. A future failing asset must be reviewed as a migration task with the exact diagnostic path and local file ID from the validator.
