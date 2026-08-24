# First-session UI/UX/HUD — honest BDO gap list

This is the MVP assessment for Boot → realm selection → character creation →
first-session 3D. Black Desert Online is the presentation benchmark, not a
parity claim.

## Closed for this slice

- Realm selection is an authored four-card ceremonial screen. Arcane Axis
  heraldry, people, silhouette, and material language carry identity without
  relying on color.
- Choosing a card only opens the binding ritual. The realm persists and opens
  character creation only after **BIND THIS REALM**; **WITHDRAW** is safe.
- Character creation keeps the committed realm and people visible while class,
  appearance, username, validation, and the adult preview share one surface.
- The 3D HUD, quest tracker, recap, and Shared Menu use the same stone, metal,
  typography, spacing, and hit-target contract. No player-facing debug Kingdom
  shortcut is part of the first-session route.
- Focused tests pin the authored realm prefab, explicit commit, realm-locked
  creator, CharacterCreation destination, ChampionArena handoff, and shared
  non-LegacyRuntime font contract.

## Still below BDO

- Realm select has no commissioned illustrated realm tableaux, character
  vignettes, layered parallax, authored transition animation, or final audio.
- Character creation is a compact MVP editor, not BDO's sculpting suite. The
  preview body, equipment presentation, poses, animation, camera controls, and
  lighting remain below final-production quality.
- Creator and 3D HUD still assemble substantial uGUI chrome at runtime. Frames
  are flat plates and rails rather than final engraved, textured, animated
  assets.
- The shared face is an OS fallback font, not a commissioned display/text
  family with complete localization coverage.
- The 3D HUD lacks final skill icons, buff/CC trays, stamina and durability,
  party/guild frames, target-of-target, a full combat log, and final telegraph
  art. See `ChampionHud_BdoGapList.md` for the HUD-specific detail.
- No sign-off yet for ultrawide, 4K, gamepad-only navigation, screen reader,
  color-vision accessibility, localization expansion, or mobile layouts.

## Verdict

This pass is a coherent adult-fantasy MVP candidate and removes the debug-stack
presentation from the accepted first-session UI path. It is not BDO parity and
should not be described as final-production UI.