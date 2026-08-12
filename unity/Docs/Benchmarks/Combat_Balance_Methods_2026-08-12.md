# AnotherLife Combat-Balance Evaluation Methods

**Status date:** 2026-08-12

**Primary delivery mode:** Codex coordination/review

**Upstream:** [#471](https://github.com/yulee94/AnotherLife/issues/471)
**Source context:** `Cross_Genre_Benchmark_Source_Manifest_2026-08-12.json`

## 1. Authority and hypothesis boundary

This document defines an original, inspectable way to evaluate AnotherLife combat across PvE, small-team PvP, capped realm war, open-world war, and bosses. It does not reproduce a comparator's proprietary formula. External sources motivate questions and test categories; AnotherLife owns the equations and parameter names below.

Every number, coefficient, clamp, cap, target, weight, sample count, and example in this document is a **HYPOTHESIS / PROPOSED TARGET**. Nothing becomes production balance until it is:

1. implemented behind a versioned balance profile;
2. checked by deterministic tests and simulation;
3. measured with privacy-safe telemetry;
4. played by representative players, devices, roles, and input methods;
5. dispositioned by A1; and
6. explicitly accepted for balance by the user.

A statistically significant result cannot replace the user's balance decision, accessibility validation, design-source fidelity, or integrated playtest. Patch-sensitive community material may suggest scenarios, but it is never AnotherLife authority. `[COMMUNITY-GW2-DAMAGE-001]` `[COMMUNITY-ALBION-PRESSURE-001]` `[COMMUNITY-ALBION-FOCUS-001]`

## 2. Units, conventions, and reproducibility

| Symbol | Unit | Meaning |
| --- | --- | --- |
| `HP` | health points | Current or maximum health. |
| `Power`, `Armor`, ratings | rating points | Profile-versioned combat stats. |
| `BaseDamage`, `BaseHeal` | health points | Pre-modifier effect at reference power. |
| `SkillCoeff`, multipliers | unitless | Bounded scaling factors. |
| `Duration`, `TTK` | seconds | Simulation uses seconds; runtime logging records timestamps. |
| `Distance`, `Radius` | world metres | One world-space convention must be declared by the runtime profile. |
| probability | `[0,1]` | Stored and reported separately from percentages. |
| damage/healing rate | health points/second | Window and population must accompany the value. |

Use double precision in offline analysis and deterministic, documented runtime precision. Resolve rounding once, at the authoritative result boundary. Random trials require a stored seed, profile version, content version, mode, map, team composition, latency/loss condition, and simulator commit.

The proposed neutral reference profile is:

| Parameter | Proposed starting hypothesis |
| --- | ---: |
| `RefHP` | `10,000 HP` |
| `RefPower` | `1,000 rating` |
| `RefHealPower` | `1,000 rating` |
| `RefShieldPower` | `1,000 rating` |
| `ArmorK` | `1,500 rating` |
| `CritK` | `1,000 rating` |
| `PvPDamageScalar` | `0.80` |
| `PvPHealingScalar` | `0.70` |
| `ArmorDRCap` | `0.65` |
| `PersistentMitigationCap` | `0.75` |
| `GlanceDamageFactor` | `0.25` |

These values exist only to make examples reproducible. They are not implied by a reference game and are not accepted tuning.

All reference stats, `ArmorK`, `CritK`, `MaxHP`, item-power references, queue-rating scale, and other formula denominators must be finite and strictly positive. Ordinary ratings, base amounts, durations, counts, and coefficients must satisfy their field-specific domains; no equation is evaluated with an invalid denominator or fractional actor count.

## 3. Mode-separated profiles

Never tune one universal profile and assume it is fair everywhere. The proposed profile schema contains at least:

| Mode | Intended evaluation | Required separation |
| --- | --- | --- |
| `PvE` | ordinary creatures and encounters | PvE coefficients, threat, encounter mechanics, recovery |
| `RankedSmallTeam` | bounded, symmetric competitive team play | PvP damage/heal, normalization, dampening, objective score |
| `CappedRealmWar` | scheduled or capacity-bounded war | crowd/pressure controls, objective score, respawn, siege |
| `OpenWorldWar` | variable population and arrival | population ratio, reinforcement, route, disengage, recovery |
| `BossPvE` | break bars and coordinated mechanics | break damage, immunity, phase timing, enrage, role envelopes |

Each effect declares allowed modes and mode-specific coefficients. A profile loader rejects missing, duplicated, negative, non-finite, or out-of-bound values rather than silently inheriting a PvE value into PvP. Separating PvE/PvP coefficients and auditing stat limits is a general, evidenced practice; the exact AnotherLife values remain original hypotheses. `[MMO-BDO-COMBAT-001]` `[MMO-AION2-COMBAT-001]`

## 4. Parameterized damage and mitigation

### 4.1 Proposed baseline equations

For a single damaging event:

```text
PowerFactor = clamp(0.80, 1.20,
                    1 + 0.50 × (Power - RefPower) / RefPower)

RawDamage = BaseDamage × SkillCoeff × PowerFactor

EffectiveArmor = max(0,
                     Armor × (1 - PercentPenetration) - FlatPenetration)

ArmorDR = min(ArmorDRCap,
              EffectiveArmor / (EffectiveArmor + ArmorK))

PFullHit = clamp(0.25, 0.98,
                 0.85 + (Accuracy - Evasion) / 1000)

PGlance = 1 - PFullHit

PCrit = clamp(0.05, 0.35,
              0.05
              + CritRating / (CritRating + CritK)
              - CritResist / (CritResist + CritK))

CritBonus = max(0.10, 0.50 - CritResist / 2000)

ExpectedHitFactor = PFullHit × (1 + PCrit × CritBonus)
                    + PGlance × GlanceDamageFactor

PersistentMitigationMultiplier =
    max(1 - PersistentMitigationCap,
        (1 - ArmorDR) × (1 - GuardDR) × (1 - BuffDR))

PrePressureAmplifier =
    min(1.65,
        SkillAmplifier × ExternalAmplifier × VulnerabilityAmplifier)

ExpectedDamage = RawDamage
                 × ModeDamageScalar
                 × ExpectedHitFactor
                 × PersistentMitigationMultiplier
                 × PrePressureAmplifier
                 × AoEMultiplier
                 × ZergDamageMultiplier
                 × FocusPressureMultiplier
```

Proposed bounds are `0 ≤ SkillAmplifier ≤ 1.25`, `0 ≤ ExternalAmplifier ≤ 1.20`, `0 ≤ VulnerabilityAmplifier ≤ 1.20`, and their explicitly clamped combined pre-pressure multiplier `≤ 1.65`. Pressure controls are defined later. Transient active defenses may exceed the persistent mitigation cap only when clearly telegraphed, short, dispellable/counterable where appropriate, and separately logged.

### 4.2 Worked example — proposed inputs only

Given `BaseDamage=1,000`, `SkillCoeff=1.0`, `Power=1,000`, `Armor=1,500`, no penetration, `Accuracy=Evasion`, `CritRating=250`, `CritResist=250`, no guard/buff reduction, and all optional multipliers `1.0`:

```text
PowerFactor = 1.0
ArmorDR = 1500 / (1500 + 1500) = 0.50
PFullHit = 0.85
PCrit = 0.05 + 250/1250 - 250/1250 = 0.05
CritBonus = 0.50 - 250/2000 = 0.375
ExpectedHitFactor = 0.85 × (1 + 0.05 × 0.375) + 0.15 × 0.25
                  = 0.9034375
ExpectedDamage = 1000 × 0.80 × 0.9034375 × 0.50
               = 361.375 HP
Continuous expected-event approximation to defeat RefHP
    = 10000 / 361.375 ≈ 27.67 events
```

This is an arithmetic fixture, not a desired TTK or a claim that a discrete combat sequence can contain a fractional event. A fixed-size-event interpretation would require `28` such events. A second fixture with positive amplifiers/penetration should be stored beside it to detect cap-order regressions. All intermediate components must be available in diagnostic telemetry so an unexpected result can be explained.

### 4.3 Distribution, not only expectation

Expected damage hides burst tails. This proposed baseline has mutually exclusive glance/full outcomes and a conditional critical result on a full hit; it has no separate miss outcome. For every profile, run seeded event simulations and report damage per event/window p05/p10/p25/p50/p75/p90/p95/p99, glance/full/critical proportions, largest one-source share, and deaths inside `0.5 s`, `1 s`, `2.5 s`, `5 s`, and `10 s` windows. Verify `PFullHit + PGlance = 1`, `0 ≤ PCrit ≤ 1`, and `PCritObserved ≤ PFullHitObserved`. Compare observed probabilities against the configured values with confidence intervals; fail the pipeline for impossible states, cap bypasses, non-finite values, or non-determinism under the same seed.

## 5. Healing, shields, and sustain

Proposed healing model:

```text
HealPowerFactor = clamp(0.80, 1.20,
                        1 + 0.50 × (HealPower - RefHealPower) / RefHealPower)

RawHeal = BaseHeal × HealCoeff × HealPowerFactor

ShieldPowerFactor = clamp(0.80, 1.20,
                          1 + 0.50 × (ShieldPower - RefShieldPower)
                                      / RefShieldPower)

RawShield = BaseShield × ShieldCoeff × ShieldPowerFactor

MultiHealerMultiplier = 1 / sqrt(max(1, ConcurrentEffectiveHealers))

EffectiveHeal = RawHeal
                × ModeHealingScalar
                × MultiHealerMultiplier
                × (1 - AntiHeal)
                × ReceivedHealingMultiplier

UncappedShield = RawShield × ModeShieldScalar

EffectiveShield = min(0.25 × TargetMaxHP, UncappedShield)
```

`BaseShield`, `BaseHeal`, `TargetMaxHP`, all coefficients, and power inputs must be finite and nonnegative; `TargetMaxHP` must be positive. Proposed starting bounds: `AntiHeal ≤ 0.30` for persistent effects and `≤ 0.50` for short, explicit windows; the mandatory post-calculation clamp keeps an ordinary shield `≤ 0.25 × TargetMaxHP` at once; multi-healer diminishing return applies only to healers whose healing was effective within the declared rolling window. Self-heal, leech, regeneration, shield, cleanse, and external healing remain separate telemetry categories.

Evaluate sustain with incoming-pressure traces, not target-dummy HPS. Report effective healing, overheal, prevented damage, cleanse value, time at full health, healer concurrency, mana/resource exhaustion, and survival conditional on received support. Verify that anti-heal has non-color-only feedback and that audio-off players receive equal state information.

## 6. Crowd control, Resolve, and break bars

### 6.1 Player Resolve — proposed model

Every control effect declares category, base duration, severity `[0,1]`, breakability, immunity interactions, and diminishing-return family.

```text
ResolveDurationFactor = max(0.25, 1 - Resolve / 100)

AppliedDurationBeforePopulation =
    if IsHardControl and HardControlImmunityActive: 0
    else: BaseDuration × ResolveDurationFactor

AppliedDuration = AppliedDurationBeforePopulation × ZergCCMultiplier

ResolveGain = 35 Resolve-points/second × Severity × AppliedDuration

ResolveAfter = clamp(0, 100, ResolveBefore + ResolveGain)
```

Proposed starting constraints: ordinary hard-control base duration `≤ 2.5 s`; crossing to `100 Resolve` starts an explicit `2.5 s` hard-control-immunity state, so the hard-control branch returns zero before any duration multiplier while that state is active; after it expires, the ordinary Resolve factor applies even if Resolve has not yet decayed. Resolve begins decaying only after `3 s` without a qualifying control and decays at `15 Resolve-points/s`. Population pressure is applied after the immunity/Resolve branch and cannot turn an immune result nonzero. Displacements, roots, slows, silence, disarm, fear, stun, knockdown, and loss-of-control cinematics require separate classification. Immunity feedback uses icon/shape/text timing, not only color. Consecutive immunity handling is a patch-sensitive comparator topic, not an imported rule. `[MMO-AION2-COMBAT-001]`

### 6.2 Boss break bars — proposed model

```text
BreakDamage = BreakUnit × BaseControlDuration × Severity × BreakSkillModifier
```

`BreakUnit` is an independently tuned, profile-defined positive value measured in break-points/second; no starting numeric value is proposed here. `BaseControlDuration` is seconds, `Severity` is `[0,1]`, and `BreakSkillModifier` is a finite nonnegative unitless value. Bosses consume break damage instead of receiving ordinary control while the bar is active. The profile declares bar capacity, recovery, vulnerability window, phase resets, immunity, and contribution credit. Tests must prevent double application of both control and break damage and verify that each contributing role receives clear bar feedback. Community break-bar documentation is scenario inspiration only. `[COMMUNITY-GW2-BREAKBAR-001]`

## 7. Bounded area effects, focus fire, and zerg pressure

These controls protect readability and counterplay; they must not invisibly decide a battle.

### 7.1 Area effects

Proposed effective-target caps are `8` for ordinary damage, `5` for hard control, and `5` for burst healing. Exceptions require an explicit profile entry, telegraph, budget, and test.

```text
ClumpMultiplier = 1 + 0.06 × min(5, max(0, AffectedTargets - 3))
AoEMultiplier = min(1.30, ClumpMultiplier)
```

If more actors overlap than the target cap, selection is deterministic and declared: priority, distance, stable entity ID, or seeded choice. Never let render culling alter authoritative targeting. Always expose target count, eligible count, selected IDs, and cap reason in diagnostics. Comparator AoE escalation is a maintained-community reference only; this equation is AnotherLife's proposed original model. `[COMMUNITY-ALBION-AOE-001]`

### 7.2 Population pressure

For positive eligible counts, define `R = EligibleAllies / EligibleEnemies`. With at least `12` nearby eligible allies:

```text
if EligibleAllies < 12 or R <= 1.25:
    ZergDamageMultiplier = 1
    ZergCCMultiplier = 1
else:
    ZergDamageMultiplier = max(0.76, 1 - 0.08 × log2(R / 1.25))
    ZergCCMultiplier = max(0.70, 1 - 0.10 × log2(R / 1.25))
```

If either eligible count is zero or negative, profile validation fails rather than evaluating a ratio. The sampling radius/window, actor eligibility, grouping rules, and update cadence must be declared and must resist boundary dancing. Show an understandable status to affected players. Do not apply the multiplier to siege/objective damage without separate evidence. Test equal forces, both sides of the `1.25` boundary, reinforcement arrival, split groups, pets/summons, disconnects, and extreme ratios.

### 7.3 Focus pressure

For independent attackers who damaged the same target inside the proposed focus window:

```text
FocusPressureMultiplier =
    max(0.55, 1 / (1 + 0.12 × (Attackers - 1)^0.75))
```

`Attackers` must be an integer `≥ 1`; zero or a negative count is rejected. An attacker counts once regardless of hit frequency. Damage-over-time ownership, pets, vehicles, reflected damage, and siege require explicit attribution. Report both raw and pressure-adjusted damage. Use the mechanism only after simulation demonstrates that coordinated focus remains valuable while instant, unreadable deletion falls. Comparator disarray and focus-fire systems motivate the test category, not this formula. `[COMMUNITY-ALBION-PRESSURE-001]` `[COMMUNITY-ALBION-FOCUS-001]`

## 8. Gear normalization and role envelopes

### 8.1 Proposed competitive normalization

```text
NormalizedStat =
    if RawStat = 0: 0
    else if RawStat <= SoftCap: max(0.85 × SoftCap, RawStat)
    else: SoftCap + 0.15 × (RawStat - SoftCap)

ItemPowerMultiplier = clamp(0.90, 1.10,
                            (ItemPower / ReferenceItemPower)^0.35)
```

The formula applies only to eligible, present, nonnegative stat channels; absent/zero channels remain zero. `SoftCap` and `ReferenceItemPower` must be positive, and `ItemPower` must be finite and nonnegative. The floor, soft-cap rate, exponent, and hard bounds are proposed hypotheses. UI must show when a competitive profile changes an item and must not present a misleading pre-normalization `Power` score as predicted performance. Evaluate starter, median, high, and extreme gear; every role; premade and solo teams; and progression satisfaction outside competitive normalization.

### 8.2 Proposed role envelopes

| Role envelope | Damage pressure | Durability | Control/support | Evaluation risk |
| --- | --- | --- | --- | --- |
| Frontline | low–medium | high | peel/initiation | unkillable stack or mandatory monopoly |
| Striker | high, bounded burst | low–medium | limited | deletion before readable response |
| Sustained damage | medium–high over time | medium | limited | oppressive uptime with no disengage |
| Controller | low–medium | medium | high, Resolve-bound | chained loss of control |
| Support/healer | low | medium | high sustain/utility | immortal stacking or target denial |
| Siege/operator | objective-high | situational | route/structure utility | farming players outside intended role |

These are envelopes, not classes or lore. Narrative and creative authority remain untouched. Test each role's contribution, counters, failure recovery, input complexity, visual load, and accessibility—not only win rate.

## 9. TTK and burst distributions

Proposed starting targets for structured tests:

| Scenario | Proposed distribution target |
| --- | --- |
| Equal-profile 1v1 with active response | median TTK `12–20 s` |
| 5v5 focused target with timely response | median TTK `4–8 s` |
| 5v5 focused target without response | median TTK `2.5–4.5 s` |
| Mass-war ordinary player | TTK p10 `≥ 2.5 s` after becoming targetable/readable |
| Any ordinary single source | `≤ 35% MaxHP` in rolling `1 s`, except separately approved major telegraph |

All are hypotheses. Segment by role, gear band, skill band, premade status, latency, frame-rate tier, input method, team-size ratio, healer access, and control state. Survival analysis must include censored disengagements rather than treating every non-death as infinite TTK. Review death recaps to determine whether the player had perceivable warning and a legal response, not merely whether elapsed time passed the target.

## 10. Objective-weighted scoring and anti-snowball controls

Proposed structured-PvP score allocation:

- objectives and objective enabling: `70–85%` of attainable score;
- kills/assists: no more than `10–15%` directly;
- remaining share: defense, escort, recovery, scouting, siege, and support where measurable without encouraging abuse.

Proposed repeat-kill value for the same victim:

```text
RepeatKillMultiplier(n) = max(0.125, 0.5^n)
```

where `n` is a nonnegative integer and `n=0` for the first eligible defeat in the rolling window. Proposed underdog and comeback reward hypotheses:

```text
UnderdogRewardMultiplier =
    min(1.50,
        max(1.00, sqrt(EligibleEnemies / EligibleAllies)))

ComebackCaptureMultiplier =
    min(1.20,
        1 + 0.05 × ObjectiveDeficitTier)
```

`EligibleAllies` and `EligibleEnemies` must be positive, and `ObjectiveDeficitTier` must be a nonnegative integer derived from a declared objective-state table. Rewards should encourage continued participation without amplifying combat power. Anti-snowball mechanisms must never hide population truth, manufacture a false result, reward intentional losing, or remove earned strategic advantage. Simulate spawn trapping, late joins, disconnect waves, premade stacking, objective trading, kill feeding, and AFK contribution.

## 11. Matchmaking and team construction

Use a versioned rating adapter; no one named algorithm is mandated. A proposed Bayesian record contains skill mean `mu`, uncertainty `sigma`, mode/role cohort, party size, recency, and provisional state. Public rank need not expose the full internal estimate.

Proposed win estimate and match cost:

```text
P(TeamA wins) = 1 / (1 + exp(-(MuA - MuB) / Scale))

MatchCost(t) = wSkill(t) × abs(P - 0.5)
               + wUncertainty(t) × UncertaintyImbalance
               + wParty(t) × PartySizeImbalance
               + wRole(t) × RoleCoveragePenalty
               + wLatency(t) × LatencyPenalty
```

Here `P = P(TeamA wins)`, `Scale` is a positive value in the same rating units as `MuA` and `MuB`, `t` is nonnegative elapsed queue time, and every penalty is normalized to `[0,1]` before weighting. Every weight has an explicit nonnegative floor and a bounded, nonincreasing widening schedule; latency and gross-mismatch safety bounds never relax past their approved floors. As `t` increases, only approved weights decrease toward those floors, so waiting cannot add a separate positive cost. All weights and widening schedules are hypotheses. Queue UI tells the truth about mode, region, estimated wait, widening, backfill, and party constraints. Never infer or use protected personal traits. Test new accounts, returning players, smurfs, parties, sparse regions, high latency, role scarcity, rematches, abandonment, and adversarial queue timing. Published TrueSkill and Glicko-2 material can inform evaluation, but selecting or tuning a system requires independent AnotherLife evidence. `[RESEARCH-TRUESKILL-001]` `[RESEARCH-TRUESKILL2-001]` `[RESEARCH-GLICKO2-001]`

## 12. Simulation and scenario matrix

### 12.1 Deterministic checks

- identity and neutral-stat fixtures;
- every min/max clamp and one value on either side;
- field-specific domain validation: reject non-finite values everywhere, preserve semantically valid zero values, and reject negative or zero values only where each field's declared domain requires it;
- armor and penetration monotonicity;
- probability sum and RNG reproducibility;
- stack/cap ordering and persistent-mitigation cap;
- Resolve immunity/decay and break-bar exclusivity;
- AoE deterministic selection and render independence;
- focus/zerg ownership, pets, disconnects, and boundary cases;
- objective score conservation and repeat-kill floor;
- profile migration, rollback, and old replay compatibility.

### 12.2 Monte Carlo and sensitivity

For each declared profile and scenario, vary one parameter at a time across its full bound, then vary correlated sets using seeded Latin-hypercube or stratified samples. Report outcome elasticity:

```text
Elasticity(Y, X) = (ΔY / Y) / (ΔX / X)
```

Use this relative elasticity only when baseline `X` and `Y` are nonzero and the perturbation remains inside the parameter domain. At or near zero, report a finite-difference slope `ΔY / ΔX` with the exact interval instead. Rank parameters by effects on win probability, TTK quantiles, burst deaths, objective control, healing efficiency, queue quality, and role representation. Proposed minimum simulation counts must be chosen from precision/power analysis, not a universal magic number. Store confidence intervals and convergence plots; increase trials when tails remain unstable.

### 12.3 Required scenario dimensions

- `1v1`, `3v3`, `5v5`, `20v20`, proposed capped-war population, and uneven open-world ratios;
- mirrored and counter-composition teams;
- starter/median/high/extreme gear and normalization on/off;
- no healer/one healer/multiple healer;
- stationary, moving, obstructed, elevated, narrow-route, open-field, and objective cluster;
- 30/60/120 FPS where supported, CPU/GPU-bound frames, latency/loss/jitter/reconnect;
- mouse/keyboard, controller, touch, reduced motion, VFX reduction, audio off, and color-vision simulations;
- novice/intermediate/expert behavior models, then human validation.

Bots validate math and state transitions; they do not certify fun, comprehension, fairness, or human strategy.

## 13. Telemetry and statistical evaluation

### 13.1 Privacy-safe event contract

Use rotating/pseudonymous match and actor identifiers with declared retention and access. Never log names, chat contents, precise real-world location, advertising identity, or unnecessary device identifiers.

Minimum versioned fields:

- build, content, balance-profile, mode, map, ruleset, region bucket;
- match/encounter timestamp, duration, population and team-size bands;
- pseudonymous actor, role envelope, gear/normalization band, party-size band, input/device/performance tier;
- source/target/effect stable IDs, event category, raw amount, mitigated/prevented/effective amount;
- every named damage/heal/pressure component and cap reason;
- control category, proposed/applied duration, Resolve before/after, break damage;
- objective state/contribution, death/respawn/disengage, match result;
- frame-time/latency/loss bands and missing-event diagnostics.

### 13.2 Analysis requirements

Report distributions and uncertainty, not leaderboards of averages. Pre-register the primary metric, guardrails, exposure unit, exclusion policy, and stopping rule for an experiment. Check sample-ratio mismatch before interpreting an A/B result. Use confidence intervals suited to the statistic, survival methods for TTK, calibration curves for matchmaking, and multiplicity controls for many comparisons. `[RESEARCH-SRM-001]` `[RESEARCH-WILSON-001]` `[RESEARCH-ALWAYSVALID-001]`

Segment results without exposing small or identifiable cohorts. A role can have a 50% aggregate win rate while failing at different skill, party, device, or latency bands. Qualitative observation and player reports remain evidence alongside telemetry. `[RESEARCH-GAMEBALANCE-001]`

## 14. Failure, rollback, and exploit evaluation

Before a profile can advance:

- keep the previous accepted profile and a one-operation rollback path;
- reject partial or mismatched profile versions across client/server;
- preserve replay/result interpretation by profile version;
- define kill switches for an effect, item, queue, or mode without save corruption;
- simulate cap bypass, integer overflow, stacking cycles, reflection loops, pet attribution, disconnect/rejoin, time manipulation, duplicated events, and reward farming;
- compare rollback behavior during a live encounter and at the next safe boundary;
- communicate maintenance/correction truthfully and do not silently rewrite earned results.

Rollback readiness does not authorize live tuning or waive user approval.

## 15. Fairness, accessibility, and human gates

Balance is invalid when a legal response is technically present but cannot be perceived or performed on supported settings. Every competitive test records:

- telegraph recognition time and occlusion;
- input-to-visible and input-to-authoritative response;
- color-independent allegiance/control/immune states;
- captions and audio-off parity for critical cues;
- reduced shake/motion/flash/VFX settings without semantic loss;
- UI scale, touch target, focus/rebinding, hold/toggle, and motor-load considerations;
- effect and nameplate degradation under crowd load;
- device/frame/thermal state and network conditions.

Human gates ask whether players understood why they won or died, perceived a response window, could locate the objective, could contribute outside kills, and trusted the result. The user retains the final balance gate even if every machine metric passes.

## 16. Advancement checklist and non-claims

### Machine-verifiable evidence

- schema and bound validation;
- deterministic fixtures and seeds;
- simulation inputs, commit, profile, distributions, and confidence intervals;
- cap/stack/order invariants;
- performance/network/input measurements;
- privacy and event-completeness audit;
- rollback/exploit test results.

### Design and human evidence

- co-developer source/fidelity review where terrestrial design is involved;
- A1 architecture/integration disposition;
- representative accessibility/readability sessions;
- player comprehension, fairness, and enjoyment playtests;
- explicit user balance approval.

This document approves no formula, coefficient, target, mode, class, item, matchmaking policy, population, monetization, runtime integration, catalog/save/network change, player acceptance, milestone, or release. It is a proposed evaluation framework and source of testable hypotheses only.
