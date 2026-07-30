# Territory 100-User Load Safety Contract

**Status date:** 2026-07-30
**Primary delivery mode:** Codex engineering
**Decision source:** user requested automatic visual degradation under heavy territory load and a 100-user stress test.

## Scope

This contract protects client rendering when up to 100 registered users are visible in one territory. It does not create a multiplayer transport, server authority, replication, spawning, streaming, or production Slagfall integration. Terrestrial source remains separate and keeps its own approval boundary.

The safety rule is:

> A territory may contain 100 visible users, but a client must not animate or render all 100 at maximum detail.

All 100 remain represented through progressively cheaper models. Users beyond the 100-user client contract are explicitly culled until a future networking and interest-management contract defines a wider limit.

## Degradation budgets

| Load | Full | Medium | Low/static | Impostor | Animated maximum | Decorative VFX | Decorative lights |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Normal | 24 | 32 | 32 | 12 | 56 | 100% | on |
| Elevated | 16 | 28 | 28 | 28 | 44 | 65% | on |
| Heavy | 12 | 20 | 20 | 48 | 32 | 25% | off |
| Critical | 8 | 12 | 16 | 64 | 20 | off | off |

Nearest registered participants receive the highest available tier. Registration fails closed unless every participant provides full, medium, low, and impostor representations. Runtime fallback still moves only toward cheaper tiers; the controller never promotes a missing low-cost model back to an expensive model.

## Load signals and recovery

The controller uses the worst of registered-visible-user pressure, externally reported territory population, and smoothed client frame pressure:

- 70 users enters at least `Elevated`.
- 100 users enters at least `Heavy`.
- More than 100 users enters `Critical`.
- Frame pressure enters `Elevated`, `Heavy`, or `Critical` at 1.10×, 1.35×, or 1.75× the target frame time.

Known user-count thresholds apply immediately, so a 100-user join burst cannot spend its first frames at normal detail. Frame-pressure degradation requires 0.5 seconds of sustained pressure. Recovery requires 3 seconds and moves one level at a time. This hysteresis prevents quality from flickering when frame time oscillates around a threshold.

## Runtime behavior

- Crowd objects are reused; tier changes only activate an existing representation.
- Batch registration assigns the burst once instead of sorting and reapplying after every join.
- Only full and medium representations keep Animators enabled.
- Low and impostor representations are static.
- Decorative particle emission is reduced before it is stopped and cleared.
- Decorative lights are disabled at heavy and critical load.
- Assigned environment LOD groups transition to cheaper levels earlier under pressure while retaining ordinary distance culling, then restore their authored thresholds on recovery.
- The controller changes only registered territory visuals. It does not mutate global `QualitySettings`, avoiding cross-scene restore races.
- Disabling the controller restores authored participant, particle, light, and LOD state.

## Automated evidence

EditMode tests prove deterministic budgets, monotonic degradation, validation, hysteresis, the exact 100-user heavy plan, and explicit culling beyond the contract.

PlayMode stress tests create 100 synthetic participants and prove:

- all 100 remain represented at heavy load;
- exact `12 / 20 / 20 / 48` heavy-tier distribution;
- nearest users retain higher detail;
- twelve repeated critical/heavy cycles reuse the same participant objects;
- the 100-user burst performs one plan application;
- synthetic Renderer and Animator counts follow the active tier caps;
- critical load removes decorative particle and light cost;
- authored visual state returns after recovery;
- the test fixture cleans itself up.

## Remaining acceptance work

Automated tests are structural and deterministic; they intentionally do not use machine-dependent FPS assertions. Production acceptance still requires a target-device profiler run with the real terrain, shaders, avatars, animations, effects, camera, and networking stack:

- 30-minute 100-user congregation;
- minimum-device frame, thermal, and memory measurements;
- target-PC frame and memory measurements;
- join burst, teleport stack, disconnect/rejoin, and territory transition;
- server tick and per-client bandwidth budgets after a transport is selected;
- visual readability and user playtest.
