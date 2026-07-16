# QuestDefinition Authority Record

**Status date:** 2026-07-15
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

## Inventory

Pre-change tracked repository search found no serialized Unity asset references to the removed root GUID. The only tracked occurrence of `226022aa7500f3e4abc8ac3757707ad8` was documentation describing the recovery risk. The new regression test intentionally keeps this GUID literal so the old root script reference remains detectable.

Pre-change tracked repository search found the narrative GUID only in:

```text
unity/Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs.meta
unity/Docs/Phase_1_NVS_01_Status.md
```

The new regression test also intentionally keeps this GUID literal to lock the authority decision. Future valid `QuestDefinition` ScriptableObject assets are expected to reference this same GUID in their serialized `m_Script` field.

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

`QuestDefinitionAssetAuthorityTests` validates the selected authority in EditMode:

- the authoritative script path keeps GUID `c385b2b183b74184ca75eeffbe2256ef`;
- the loaded class is `AL.Data.Definitions.Narrative.QuestDefinition`;
- exactly one production `ScriptableObject` type named `QuestDefinition` is discoverable;
- serialized Unity assets do not reference the removed root GUID;
- any future quest assets discovered by `AssetDatabase.FindAssets("t:QuestDefinition")` load as the narrative type, reference the authoritative script GUID, and do not duplicate non-empty quest IDs.

## Migration Result

No tracked serialized assets required mutation. The safe migration is therefore to preserve the narrative script GUID as authority and add regression validation that fails if either the removed root GUID reappears or future quest assets bind to the wrong definition type.
