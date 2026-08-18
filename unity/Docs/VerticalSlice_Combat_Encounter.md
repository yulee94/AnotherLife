# Vertical Slice — Champion Combat Encounter (Greybox)

Task: `t_b098710e`. One playable greybox champion duel inside the DemoInitializer arena,
using the local run state's selected champion against one hardcoded opponent, with
movement/positioning **or** turn actions, attack/defend/special, win/lose feedback, and a return
to the command loop. No catalog/save/determinism authority.

## What was built

| File (under `unity/Assets/AL/Scripts/VerticalSlice/`) | Responsibility |
| --- | --- |
| `SliceChampionProfile.cs` | Combat-relevant snapshot of the selected champion (id, name, class, stats). |
| `SliceOpponentProfile.cs` | The single hardcoded opponent (id, name, stats). |
| `SliceCombatResult.cs` | Terminal result of one duel (`CombatEncounterOutcome` + full stats). |
| `SliceRunState.cs` | Local in-memory run state seam (`SelectedChampion`, `LastCombatResult`). |
| `Combat/CombatEncounterConfig.cs` | Tuning surface (Inspector-exposed knobs + opponent AI). |
| `Combat/CombatEncounterSim.cs` | Pure, deterministic, turn-based simulation (unit-testable). |
| `Combat/GreyboxCombatEncounter.cs` | MonoBehaviour: UI, win/lose feedback, writes result, returns to loop. |

`unity/Assets/AL/Scripts/Utilities/DemoInitializer.cs` gains a **CHAMPION DUEL** button that
launches the encounter over the existing command board; **RETURN TO COMMAND** restores the board.

## The loop contract (shared seam)

`SliceRunState` is the slice's local run state. Combat only owns two of its fields and leaves the
rest to the sibling tasks (realm selection → boot task; character → character-creation task;
kingdom build + persistent snapshot → kingdom/save-reload tasks):

- **Read** `SliceRunState.SelectedChampion` (falls back to `SliceChampionProfile.CreateDefault()`
  when no champion has been selected yet — keeps the duel standalone-playable).
- **Write** `SliceRunState.LastCombatResult` on every terminal outcome (win / lose / aborted).

> Hotspot: `SliceRunState` and the `SliceChampionProfile`/`SliceCombatResult` shapes are the
> cross-task integration seam. The save/reload task (t_59bca09b) owns the *persistent* RunState
> snapshot contract; the integration task (t_fae6db36) owns reconciling field naming across the six
> parallel branches. See "Integration notes" below.

## Gameplay

Turn-based, deliberately (the task allows "movement/positioning **or** turn actions"; a
deterministic turn model is reliably playable start-to-finish and unit-testable in a headless
environment, which is the point of the find-the-fun greybox gate). Three actions:

- **ATTACK** — deal `AttackPower`.
- **DEFEND** — raise guard; the next incoming hit is reduced by `DefendReduction`.
- **SPECIAL** — deal `SpecialPower`; costs `SpecialManaCost` mana and starts a
  `SpecialCooldownTurns` cooldown. A special that is on cooldown or lacks mana **does not consume
  the turn** (the player may re-choose).

The opponent acts after every player turn, driven by a seeded RNG (`OpponentSpecialChance`,
`OpponentDefendChance`). Win at opponent 0 HP; lose at champion 0 HP.

## Tuning parameters (for the integration/tuning pass)

All knobs are public `[SerializeField]` fields on `GreyboxCombatEncounter` via
`CombatEncounterConfig`:

- Champion economy: `SpecialManaCost`, `SpecialCooldownTurns`, `ManaRegenPerTurn`, `DefendReduction`.
- Opponent (hardcoded, tunable in place): `Opponent` (`MaxHealth`, `MaxMana`, `AttackPower`,
  `SpecialPower`, `DefendMitigation`, `SpecialManaCost`).
- Opponent AI: `OpponentSpecialChance`, `OpponentDefendChance`, `OpponentManaRegenPerTurn`.

Champion stats themselves live in `SliceChampionProfile` (populated by character creation).

## Verification

`unity/Assets/AL/Tests/EditMode/GreyboxCombatEncounterTests.cs` exercises the sim end-to-end:
win path, lose path, determinism (same seed → same result), defend reduction (full + partial),
special mana/cooldown gating (including no-turn-consumed failures), run-state round-trip, and
default-value sanity. Run via Unity Test Framework (EditMode).

## Integration notes & known gaps

- **Turn-based vs action:** real-time action combat (WASD + skills) exists in
  `ChampionMode/Control/ChampionController` but is coupled to realm authority and physics. The
  integration pass may choose to swap this sim for the real-time controller later; the champion /
  result contracts stay valid either way.
- **No persistence:** results are session-only by design; the save/reload task owns durability.
- **No rewards:** a win does not yet grant credits/loot; the kingdom-build task consumes
  `LastCombatResult` for its reward budget.
- **Shared-file hotspot:** `SliceRunState` / `SliceChampionProfile` / `SliceCombatResult` are the
  integration seam; reconcile with the save/reload snapshot contract during t_fae6db36.
