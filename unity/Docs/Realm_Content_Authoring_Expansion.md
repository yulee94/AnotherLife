# Realm Content Authoring Expansion — Eldergrove, Crownlands, Umbral

Status: landed on the Stonehold authoring systems (kanban task `t_959ae7de`).
Companion: `unity/Docs/GameDataCatalog/Content_Authoring_Template.md`,
`unity/Docs/Stonehold_Content_Authoring_Records.md`,
`unity/Docs/Stonehold_Content_Authoring_Audit.md`.

This document does **not** invent names, stats, costs, or lore. It records that
the three remaining realms reuse the validated Stonehold slice: the same 8
research + 4 skill name-only records, the same troop absence, and a champion
absence that differs only by the realm visual-precursor citation.

## 1. What is authored

| Realm | Slice | Research | Skills | Troops | Champions |
| --- | --- | ---: | ---: | ---: | --- |
| stonehold (already on main) | `unity/Docs/Narrative/GameData/stonehold-content-authoring-map.json` | 8 name-only | 4 name-only | 0 (`not_authored_unavailable`) | 0 + Stonehold Vanguard precursor |
| eldergrove | `unity/Docs/Narrative/GameData/eldergrove-content-authoring-map.json` | same 8 | same 4 | 0 | 0 + Eldergrove Vanguard precursor |
| crownlands | `unity/Docs/Narrative/GameData/crownlands-content-authoring-map.json` | same 8 | same 4 | 0 | 0 + Crownlands Vanguard precursor |
| umbral | `unity/Docs/Narrative/GameData/umbral-content-authoring-map.json` | same 8 | same 4 | 0 | 0 + Umbral Vanguard precursor |

Research IDs (realm-agnostic): `steel_forging`, `plate_armor`, `masonry`,
`irrigation`, `ballistics`, `logistics`, `trade_routes`, `arcane_study`.

Skill IDs (realm-agnostic): `realm_strike`, `renewing_guard`, `warzone_burst`,
`warmaster_breaker`.

Every mechanical field remains `blocked_required`. Troops and champions stay
explicit absence markers — not empty artifacts reported as complete.

## 2. What is still blocked

- `authority.productionEligible` is `false` on every slice.
- Loader wiring stays `blocked`. The six-family production schema requires
  concrete mechanics this source cannot yet satisfy; C7A/C8A still forbid
  editing `LocalGameDataService`, `SkillCaster`, and `SkillLoadoutCatalog`.
- No production catalog is emitted under `StreamingAssets/GameData` for
  research, troops, champions, or skills. The Phase C generation gate must
  continue to refuse (`Test-PhaseCSixFamilyTechnicalSource.ps1
  -RequireProductionEligible` exits nonzero).
- User creative and balance acceptance remain `pending`.

## 3. Reusable systems

- Template: `unity/Docs/GameDataCatalog/Content_Authoring_Template.md`
- Parameterized validator: `tools/game-data/Test-RealmContentAuthoringMap.ps1`
- Stonehold reference validator (unchanged): `tools/game-data/Test-StoneholdContentAuthoringMap.ps1`
- Byte-stability: `unity/.gitattributes` pins `*-content-authoring-map.json` to `unity-json` (`eol=lf`)

## 4. Validation

```text
pwsh tools/narrative/Test-PhaseCSixFamilyContentMap.ps1
pwsh tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1
pwsh tools/game-data/Test-StoneholdContentAuthoringMap.ps1
pwsh tools/game-data/Test-RealmContentAuthoringMap.ps1 -RealmId stonehold -SlicePath unity/Docs/Narrative/GameData/stonehold-content-authoring-map.json -ChampionVisualPrecursor "Stonehold Vanguard"
pwsh tools/game-data/Test-RealmContentAuthoringMap.ps1 -RealmId eldergrove -SlicePath unity/Docs/Narrative/GameData/eldergrove-content-authoring-map.json -ChampionVisualPrecursor "Eldergrove Vanguard"
pwsh tools/game-data/Test-RealmContentAuthoringMap.ps1 -RealmId crownlands -SlicePath unity/Docs/Narrative/GameData/crownlands-content-authoring-map.json -ChampionVisualPrecursor "Crownlands Vanguard"
pwsh tools/game-data/Test-RealmContentAuthoringMap.ps1 -RealmId umbral -SlicePath unity/Docs/Narrative/GameData/umbral-content-authoring-map.json -ChampionVisualPrecursor "Umbral Vanguard"
pwsh tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1 -RequireProductionEligible
```

The last command must refuse (exit 1). That is the generation gate working.

## 5. Follow-up that this task does not do

Authoring actual troop/champion gameplay records, or filling blocked mechanical
fields, requires a separate creative + balance decision from the owner. Do not
fabricate those values to make the catalogs look complete.
