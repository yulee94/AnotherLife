# QuestDefinition Authority Record

**Status date:** 2026-07-16
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

Pre-change tracked repository search found no serialized Unity asset references to the removed root GUID. The only tracked occurrence of `226022aa7500f3e4abc8ac3757707ad8` was documentation describing the recovery risk. The regression tests read the removed-root GUID from `QuestDefinitionAssetAuthorityValidator.RemovedRootGuid`, so the constant in the validator remains the single executable source for old-root detection.

Pre-change tracked repository search found the narrative GUID only in:

```text
unity/Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs.meta
unity/Docs/Phase_1_NVS_01_Status.md
```

The regression tests read the narrative GUID from `QuestDefinitionAssetAuthorityValidator.AuthoritativeGuid` to lock the authority decision without duplicating the literal in test code. Future valid `QuestDefinition` ScriptableObject assets are expected to reference this same GUID in their serialized `m_Script` field.

`Assets/AL/ScriptableObjects/Quests` currently contains no tracked quest `.asset` files requiring GUID migration.

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
- `m_Script` references distinguish missing, malformed, duplicate-key, wrong file ID, missing/wrong type, malformed GUID, zero file ID, zero GUID, removed GUID, and non-authoritative GUID states;
- candidate YAML class ID and root object name are validated as Unity MonoBehaviour/ScriptableObject serialization;
- authoritative quest documents map to loaded objects by local file ID, including subassets;
- required fields, unexpected fields, blank IDs, and duplicate IDs fail deterministically;
- malformed candidates are counted separately from fully valid assets; `ValidAssetCount` means the script reference, YAML metadata, field contract, loaded-object mapping, authoritative type/script, and nonblank unique ID all passed;
- non-imported malformed YAML fixtures cover the required broken-reference matrix;
- one temporary valid authoritative quest asset is created, imported, reimported, mapped by local file ID, field-compared, and deleted during EditMode validation.

## Migration Result

No tracked serialized assets required mutation. The safe migration is therefore to preserve the narrative script GUID as authority and add regression validation that fails if either the removed root GUID reappears or future quest assets bind to the wrong definition type.

Rollback decision: no automatic YAML rewrite, asset deletion, GUID regeneration, or narrative/content mutation is authorized by this gate. A future failing asset must be reviewed as a migration task with the exact diagnostic path and local file ID from the validator.
