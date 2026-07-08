# Champion Mode Visual Direction

## Goal

Champion Mode should feel like the premium first playable slice for adult MMORPG players, not a debug test arena. The current visual direction is dark heroic fantasy: high-contrast lighting, readable combat state, a strong boss silhouette, restrained UI panels, and direct access to build/customization controls.

## Reference Learnings

- AION 2-style combat references point toward action-focused boss readability, clean hotbars, visible class/loadout identity, high-impact movement/combat presentation, strong character creation, and encounter decisions built around timing and positioning.
- Infinity Kingdom and Lords Mobile references reinforce that persistent progression games need dense but readable status surfaces: resources, timers, formation/build state, hero/team identity, event entry points, and quick access controls.
- For the 20+ adult target, Another Life should avoid toy-like debug presentation, overstuffed novelty UI, and pressure-first monetization patterns. The quality bar is serious fantasy readability: clean combat state, meaningful failure/clear feedback, attractive character expression, and systems that feel deep before they feel busy.
- Another Life should use these products as UX/gameplay references only. Do not copy their art, UI skin, names, icons, lore, or monetization framing.

## Current Champion Mode Pass

- Replace the plain test floor with an Obsidian Citadel arena built from procedural Unity primitives, fog, rim lights, pillars, combat lanes, and boss dais.
- Replace debug text with a structured combat HUD: player vitals, boss frame, boss HP/break/enrage state, skill hotbar, combat feed, action controls, movement pad, and appearance rack.
- Replace the single debug capsule champion with a layered procedural hero model: hidden root capsule renderer, facial structure, hair variants, plated armor pieces, robe/cape variants, style-specific weapons, offhands, emissive trims, and PBR material tuning.
- Replace the visible boss cylinder with a layered encounter silhouette: hidden root renderer, mantle, torso plates, core, faceplate, horns, shoulders, claws, back shards, orbit shards, aura ring, and boss-owned glow lights.
- Add baseline combat feedback: configurable follow camera, impact shake, floating combat text, boss hit reactions, break/enrage callouts, and stronger skill-impact response.
- Improve skill VFX polish with layered slash edges, wider shockwave rings, particle noise/size curves, and pooled primitive scale/fade animation.
- Polish the runtime HUD with bordered panels, a contained combat feed, boss state strip, hotbar accent bars, radial cooldown overlays, and tighter movement-pad labels.
- Improve runtime weather mood with layered falling particles, ground mist, horizon haze, wind-driven emission pulses, and subtle directional-light gusts.
- Upgrade the appearance rack into a lightweight character creator surface with current build summary, live color swatches, randomize/reset, helmet/cape toggles, and saved-state feedback.
- Add runtime-only combat goals, encounter timer, and clear grading so Champion Mode has a visible session loop without entering narrative/quest ownership.
- Add generated runtime combat audio cues for attacks, impacts, dodges, skill casts, heals, boss warnings, guard breaks, and encounter clears until authored audio assets arrive.
- Improve mobile/PC camera feel with mouse-wheel zoom, right-side touch orbit, pinch zoom, and UI-aware touch filtering.
- Add a runtime defeat state: player death locks combat input, stops auto-combat, marks the encounter as failed, shows a centered retry panel, and gives immediate floating text/audio/camera feedback.
- Upgrade boss slam telegraphs from generic warning rings to truthful impact zones with direction lines, edge ticks, impact bursts, and evade feedback when the player exits the marked area before damage resolves.
- Add combat impact feedback with short hit pauses, red damage flash, and low-health edge pulse so successful hits and incoming danger feel more physical without blocking mobile controls.
- Add a short runtime encounter intro that locks controls, frames the boss objective, counts down, and starts the timer on engage so the arena opens like a real combat mode instead of a debug sandbox.
- Add appearance inspection mode from the customization rack: close camera framing, combat input lock, manual mode, and safe resume so players can examine detailed character changes without leaving Champion Mode.
- Add an encounter clear recap panel with grade, time, guard/enrage summary, build feedback, retry, Kingdom return, and inspection actions so wins resolve like a complete MMO encounter loop.
- Keep character appearance controls visible but contained so customization supports the fantasy without overwhelming combat. The current model remains runtime-procedural so customization and save compatibility can harden before production mesh assets are imported.
- Keep all narrative, NPC, quest, dialogue, and storyline ownership outside this pass.

## Next Quality Bar

- Replace procedural blockout geometry with real mesh/texture assets for the player champion, boss, floor, pillars, and skill icons once the part names and customization contract stop moving.
- Add hit pause, richer telegraph decals, and animation-driven skill-impact timing after the UI/arena layout is stable.
- Add a proper character creator scene once the combat first impression feels worth keeping.
- Expand the failure loop into build advice, death recap stats, and difficulty retry options after combat tuning is stable.

## Reference Study Links

- AION 2 Steam page: https://store.steampowered.com/app/3393110/AION_2/
- Infinity Kingdom official site: https://infinitykingdom.gtarcade.com/m/en/
- Infinity Kingdom Google Play page: https://play.google.com/store/apps/details?id=com.gtarcade.ioe.global
- Lords Mobile Google Play page: https://play.google.com/store/apps/details?id=com.igg.android.lordsmobile
