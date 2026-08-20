# Champion HUD — honest BDO gap list

Authored 3D HUD + Shared Menu chrome for the first-session inner realm.
This is the gap versus Black Desert Online's adult action-fantasy HUD, not a
claim that the current pass matches it.

## Closed by this pass

- Vitals, target/boss frame, hotbar, combat feed, and recap live on one
  PresentationChrome token set (same type/frame language as realm-select and
  character-create).
- Shared Menu is the only 3D→2.5D entry. Kingdom Management is visible and
  LockedNarrative until Proof of Worth / lordship. No first-session debug
  "Kingdom" scene button.
- Pause / defeat / clear recap open or point at that same chrome.
- Touch already filtered HUD hits; mouse look now also ignores UI, unlocked
  cursor, menu, and recap.
- Quest HUD has a reserved `QuestHudSlot` (owned by t_2ce18b60).

## Still below BDO

- Runtime-built uGUI plates, not illustrated frames / engraved metal / crest
  inlay. No authored skill icons — colored squares and text remain.
- No damage-type colors, CC/buff tray, stamina, or equipment-durability row.
- Target frame is the encounter boss widget, not a general target-of-target /
  party/guild frame language.
- Telegraphs are still procedural primitives (owned outside this card).
- Combat feed is one line, not a scrollable combat log.
- Shared Menu is a single Kingdom Management row + Resume, not BDO's full
  system menu (inventory, skills, quest, map, settings).
- Fonts are OS fallbacks (Segoe / Noto / Arial), not a commissioned display
  face. Batchmode `-nographics` cannot raster overlay uGUI + 3D for a true
  in-editor contact sheet.
- Champion body and citadel/capital remain TEMPORARY greybox / procedural
  bind. That is world/presentation ownership, not this HUD card.
