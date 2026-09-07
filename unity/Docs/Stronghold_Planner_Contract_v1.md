# Stronghold v1 — inert source and planner handoff (#459)

## Admission boundary

This is a non-runtime contract, not a released stronghold feature. `AL.Strongholds`
resides in the existing `AL.GameDataCatalog` assembly (`noEngineReferences=true`).
There are no scene, service-registration, collision, server, economy, save, UI,
asset-provider, or `WarzoneService.CaptureTerritory` changes.

All definitions are `productionEligible=false`. All plans are permanently
`CanApplyProduction=false`. Only explicitly labeled `FixtureOnly` observations can
prepare candidates. The public observation booleans are simulation inputs, never
credentials or evidence of server authentication. The planner does not emit
`TerritoryCaptureAuthorization` or a production command-result token.

## Source decisions and compatibility

- `al_stronghold_catalog.json`, schema `al-stronghold.schema.json`, and mirrored
  valid/invalid fixtures define v1. Existing T1–T5 IDs are preserved. Only T1 and
  T4 receive compatibility profile references, matching their existing fortress
  flags; T2/T3/T5 explicitly retain null mappings. This is not approval of final
  warzone placement/count, native architecture, or castle-versus-fortress design.
- Each compatibility profile declares one continuous wall-ring ID, exactly one
  gate ID and one statue ID, plus command and upgrade interaction IDs. These are
  semantic source requirements, not a graph/collision proof or physical assets.
- Ten ordered level slots each have distinct typed visual, gate, roster, cost,
  stats, survivor-regeneration and reinforcement-timing references. L5 is
  `MajorGateMilestone`; L10 is `CapstoneMilestone`. Mage/full-gate/command-defeat
  requirements begin at L5. Low-tier command-defeat operations are unsupported;
  levels 1–4 need only a breach and valid statue interaction.
- Balance remains null. Costs, significant HP/stat improvements, guard counts,
  regeneration rates and reinforcement delays are NOT invented. The L10 references
  are distinct, but their magnitude and visual quality are not accepted by this PR.
- Upgrade-NPC role stays `Unresolved`. A future approved catalog must choose the
  Captain/General/Aristocrat role and prove actual inside/direct interaction.
- The v1 rare-cost profile maps the CURRENT owner: Stonehold → DeepOre,
  Eldergrove → WorldSap, Crownlands → RoyalSigil, Umbral → DarkCrystal.
  `QuoteUpgrade` returns an unresolved requirement quote, not a debit instruction.
  `NumericCostResolved` and `CanDebit` are always false. Fixture funding/permission
  assertions exercise the decision branch only; no wallet or transaction is used.

## Planner semantics

`Fresh` rejects unknown/unmapped territories, unknown realms and malformed instance
IDs; it returns an immutable level-1 candidate. `Plan` binds the operation ID,
operation kind, exact territory/instance/target, catalog-byte SHA-256 and complete
expected state hash. The state hash includes owner, level, revision, ownership
epoch, fencing generation, breach/NPC state, attempt, time and receipt fingerprints.
The catalog hash is computed from supplied bytes, not itself trusted admission;
future servers must compare it with a pinned approved catalog registry.

- Breach and L5+ command defeat require exact-target fixture combat observations.
  They never transfer ownership or start a timer by themselves.
- Eligible hostile statue interaction starts a realm-scoped 180,000 ms attempt.
  Same-realm repeat interaction preserves its deadline. Any other eligible realm,
  including the owner, cancels to idle without replacement. A later distinct
  interaction starts from the full duration; progress is never retained.
- Initiator departure/life state is deliberately absent from the active attempt.
  Completion is a timer command, not another player interaction, and rechecks the
  exact attempt, ownership epoch, fencing generation, breach and tier prerequisites.
- Completion is too early at 179,999 ms and possible at 180,000 ms. The candidate
  atomically changes owner, increments revision/epoch/generation, resets level to
  1, clears attempt and NPC defeat, and returns a closed/unbreached gate candidate.
  This conservative fixture reset grants no passage and chooses no live occupant,
  repair, siege-window or ejection policy.
- Reseal invalidates breach/NPC/quote work via the generation and state hash without
  silently changing an existing attempt's timer. Rebreaching does not resurrect
  the old attempt's generation authority. Valid cancellation then a distinct
  restart is needed; this is a fail-closed model, not a live recovery UI.
- Upgrade quotes bind the complete state, current owner and exact next-level cost
  profile; 9→10 adds the versioned current-owner rare requirement. Capture or any
  intervening state transition invalidates old quotes. Upgrade candidate generation
  requires same-owner, exact-NPC, direct-valid, permission and funding fixture
  assertions, no breach and no active attempt. No level beyond 10 exists.
- Exact operation replay returns the prior immutable receipt and NO candidate.
  Changed content under the same operation ID conflicts. A stale second command
  in a race cannot change the winner's candidate. These are serialized simulation
  checks, not a database CAS or durable exactly-once guarantee.
- Missing/untrusted observation, unavailable/backward time, time arithmetic overflow,
  catalog/state/target drift and receipt capacity fail closed with no candidate.
  The 1,024-receipt bound is a technical fixture limit, not a player admission cap;
  a durable ledger/retention policy is a separate server requirement.

## Reproduction and evidence

Python (requires jsonschema):

    python -m unittest discover -s unity/SharedContracts/Tests -p test_stronghold_contract.py -v

Engine-free C# (supply the actual installed paths; no Editor process is launched):

    python tools/strongholds/run_planner_tests.py --unity-data "PATH/Editor/Data" --nunit "PATH/net40/unity-custom/nunit.framework.dll" --sabotage

The runner compiles the real catalog assembly and a separate test executable with
Roslyn against Mono 4.8 API references, warnings as errors. It runs the repository's
parameterless NUnit tests, rejects unsupported setup/test-case/async lifecycles,
and stores output under `archive/stronghold-verification/`.

Verified locally: 15 NUnit methods PASS; 3 Python methods PASS; all 12 invalid
fixtures rejected by both C# and schema. Three separate copied-source sabotage
builds fail the intended tests: one-ms early expiry, cancellation auto-replacement,
and stale quote acceptance. Restored/current source returns 15/15 PASS. These are
fixture/engine-free results, not Unity Editor, IL2CPP, movement, networking or
physical-device evidence. The initial read-only review found legacy mapping and
profile cross-wire gaps; added RED tests reproduced them before correction.

The broad SharedContracts runner and hosted required checks are reported separately
in the PR/task evidence; focused green never implies whole-repository green.
Local PowerShell hygiene invocation was blocked by the headless approval guard.

## Remaining ownership and rollback

Issue #459 remains open. Existing successor `t_dcad2f4a` owns L1–10 presentation,
guard occupancy and durable gate health; `t_5a7bfda9` owns garrison NPC production.
Existing server successor `t_7d1036e8` owns authenticated/trusted-time issuance,
CAS/transactions/outbox, independent generations, combat/topology/passage authority,
anti-bypass and multiplayer/restart fault tests. Existing owner phase gates remain.
No duplicate self-assigned successor was created.

Rollback removes this unregistered catalog and engine-free types/tests. Existing
save formats, territory service state, owner/resource bonuses and runtime behavior
are unchanged; there is no save migration or live activation to roll back.
