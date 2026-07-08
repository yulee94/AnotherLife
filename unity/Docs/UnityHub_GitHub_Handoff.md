# Another Life Unity Handoff

Project path:

`C:\Users\MY\AndroidStudioProjects\AnotherLife\unity`

GitHub target:

`https://github.com/yulee94/AnotherLife`

Use this one project only. The old `AnotherLifeUnity` duplicate project and temporary Codex worktrees were removed to keep the C drive and GitHub view simple.

## What Is In This Project

- Unity mobile-first prototype source under `Assets/AL`.
- Core data, service, battle, champion mode, quest, research, warmaster, and realm war scripts.
- Realm selection UI scripts.
- Local save and offline prototype service structure.
- Design package for modular Champions, skill effects, and weather:
  - `Assets/AL/Art/Designs/ModularChampionCustomization.md`
  - `Assets/AL/Art/Designs/SkillEffectsAndWeather.md`
  - `Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json`
  - `Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json`
- Unity editor generator:
  - `Assets/AL/Scripts/Utilities/ALDesignAssetGenerator.cs`
- Fable-compatible shared contracts:
  - `SharedContracts/README.md`
  - `SharedContracts/Schemas/al-character-customization.schema.json`
  - `SharedContracts/Schemas/al-skill-weather.schema.json`
  - `SharedContracts/Fable/AnotherLife.Contracts.fs`

## Generate Starter Design Assets In Unity

1. Open `C:\Users\MY\AndroidStudioProjects\AnotherLife\unity` in Unity Hub.
2. Let Unity import the project.
3. In the Unity top menu, choose `Another Life > Generate Design Assets`.
4. Generated prototype assets will be created under `Assets/AL/Art/Generated`.
5. Use these as blockout prefabs while final Blender/FBX character and VFX assets are produced.

## Important Testing

Before building the Kingdom scene deeper, test:

1. Project opens without C# compile errors.
2. Boot or Test scene can enter play mode.
3. Realm selection cards display and can save a selected realm.
4. Local save file is created in `Application.persistentDataPath`.
5. Generated design assets can be created from the editor menu.

## Fable Compatibility

Your co-developer can use the JSON catalogs without referencing Unity assemblies. The Fable-facing contract files are under `SharedContracts/`.

Recommended flow:

1. Treat `Assets/AL/StreamingAssets/GameData/*.json` as shared source data.
2. Validate those files with `SharedContracts/Schemas/*.schema.json`.
3. In a Fable/F# tool, reference or copy `SharedContracts/Fable/AnotherLife.Contracts.fs`.
4. Keep cross-tool data fields as plain strings and arrays so Unity, Fable, and backend tools can all read the same catalogs.
