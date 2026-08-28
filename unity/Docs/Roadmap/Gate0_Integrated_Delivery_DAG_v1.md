# Gate 0 Integrated Delivery DAG

**Roadmap integration version:** `1.0.0`

**Candidate date:** 2026-08-28

**Status:** Gate 0 integration candidate; approval is authoritative only on the
Hermes Kanban

**Owning gate:** `t_1ad7a8d5`

**Integration task:** `t_00f412e4`

## 1. Authority, scope, and non-substitution

The Hermes Kanban is the durable roadmap authority. This document is a
version-controlled integration map for its tasks, dependency edges, evidence, and
decision points. It cannot create a decision, approval, waiver, threshold, or
implementation authority that is absent from the board.

This candidate consumes, without weakening or restating them:

- `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md`;
- `unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md`;
- the controlling board integration and supersession record `t_93c953eb`;
- the explicit owner approval dependency `t_0648ce23`; and
- the exact numerical packages on `t_4a5b066c` and `t_7f6be100`, by source-card
  reference only.

The authority register controls the meaning of every authority ID used below. The
evidence-governance control defines record identities, canonical locations, stage
profiles, separate visual profiles, approval vocabulary, stop-ship handling, and
rollback. A missing or inaccessible source, ambiguous value, stale artifact,
unauthorized decision, or conflict is `FAIL-CLOSED`, never a local default.

Gate 0 (`t_1ad7a8d5`) is the roadmap-integration root. Its prerequisites are only
approval and governance/integration work (`t_0648ce23`, `t_180378d0`,
`t_7742b57d`, `t_00f412e4`, `t_5306f093`, and `t_a4c586ff`). No implementation
epic is a Gate 0 prerequisite. Implementation starts only downstream of the Gate
0 owner decision.

## 2. Integrated DAG

### 2.1 Stage profiles and board gates

A board gate can require more than one sequential governance profile; it cannot
merge their records or approvals.

| Board gate | Governance profile(s) | Transition controlled | Required owner decision |
| --- | --- | --- | --- |
| `t_1ad7a8d5` Gate 0 | `GT-00` | roadmap candidate to approved authority lock | Roadmap, authority matrix, ordering, and unresolved-ledger disposition |
| `t_2fb95b2e` Gate 1 | `GT-10` | pre-production exit to production | Production baseline plus separate Stonehold 3D and 2.5D decisions and authorization to proceed |
| `t_9e887232` Gate 2 | `GT-20` | production exit to Korea Windows internal alpha | Alpha exposure plus separate Eldergrove decisions and authorization to proceed |
| `t_4e087f76` Gate 3 | `GT-30` | internal-alpha exit to North America plus Korea Windows closed alpha | Closed-alpha exposure plus separate Crownlands decisions and authorization to proceed |
| `t_99b48b45` Gate 4 | `GT-40` | closed-alpha exit to cross-platform beta | Beta exposure, system-parity candidacy, separate Umbral decisions, and deferred-scale disposition |
| `t_c1b2323a` Gate 5 | `GT-50` | beta exit to simultaneous regional soft-launch entry | Soft-launch candidate, exact exposure, and all launch-blocking deferred decisions |
| `t_debd2042` Gate 6 | `GT-60`, then `GT-70` | soft-launch exit to approved-territory 1.0 entry and final GO | Separate soft-launch-exit acceptance followed by explicit final 1.0 GO on the unchanged candidate |
| `t_50726ad6` Gate 7 | `GT-80`, then `GT-90` | stabilization exit and live-service handoff exit | Separate stabilization acceptance followed by explicit sustainable-handoff acceptance |

`GT-60` cannot substitute for `GT-70`, and `GT-80` cannot substitute for
`GT-90`. Each profile gets its own immutable gate record and approval record.

### 2.2 Critical-path chains

The stage governance chain is:

```text
t_1ad7a8d5 -> t_2fb95b2e -> t_9e887232 -> t_4e087f76
             -> t_99b48b45 -> t_c1b2323a -> t_debd2042 -> t_50726ad6
```

The fixed realm-learning chain, including its exposure milestone before the next
stage gate, is:

```text
Stonehold t_5ca7c5f2 -> Gate 1 t_2fb95b2e
  -> Eldergrove t_3be4c45c -> Korea alpha t_64e836b0 -> Gate 2 t_9e887232
  -> Crownlands t_bd653156 -> closed alpha t_9cd91a98 -> Gate 3 t_4e087f76
  -> Umbral t_77448d81 -> cross-platform beta t_3cbf17ac -> Gate 4 t_99b48b45
```

Shared technology may prototype ahead where its own card permits. Full realm
production and qualification may not bypass the chain. Every slice requires
separate `VIS-3D` and `VIS-2_5D` manifests, reviews, owner decisions, rollback
targets, and reopen handling.

The public-exposure and operating chain is synchronized to the stage chain:

```text
Gate 4 t_99b48b45 -> Gate 5 t_c1b2323a
  -> soft launch operation t_587aeef4 -> Gate 6 t_debd2042
  -> 1.0 promotion t_d6105f7a -> stabilization t_2cefb02f
  -> live-service transition t_3d678dca -> Gate 7 t_50726ad6
```

The integration task added the three missing synchronization edges
`t_c1b2323a -> t_587aeef4`, `t_587aeef4 -> t_debd2042`, and
`t_debd2042 -> t_d6105f7a`. They bind actual exposure evidence to the governing
stage decisions without changing scope or criteria.

### 2.3 Foundation and assurance fan-in

| Lane | Governing epic(s) | First mandatory stage fan-in | Continuing fan-in |
| --- | --- | --- | --- |
| Roadmap governance | `t_15f5019e` | Gate 1 | every stage audit and post-incident reopen |
| QA/build/save/content foundation | `t_aefa8e9a` | Gate 1 | every changed build, schema, save, scene, content, or recovery contract |
| Production gameplay/content | `t_c6e9368a` | Stonehold and Gate 1 | every realm and product-maturity gate |
| Realm evidence | `t_cf3f900c` | Stonehold and Gate 1 | every realm plus every stage where `t_3c74f0d3` applies |
| Account/platform parity | `t_927d3dd9`, `t_4f3f4535`, `t_a69cf1b7` | Windows alpha evidence | Gate 4 and all later exposure gates |
| Multiplayer/regional authority/capacity | `t_a8960ec8`, `t_22898962`, `t_7d1036e8`, `t_d4d26ddf` | Gate 1 | all alpha, beta, launch, recovery, and live-operation gates |
| Security and player safety | `t_30eadba6` | Gate 2 | every expanded cohort and release gate |
| Commerce and economy | `t_00f0f879`, `t_4493492b` | Gate 3 | beta, soft launch, 1.0, stabilization, and live service |
| Compliance | `t_616edab5` | Gate 4 | every submission, soft launch, 1.0, and recurring live review |
| Release and incident operations | `t_28c37145`, `t_aa3849be` | Gate 5 | Gate 6, stabilization, and Gate 7 |
| Stabilization/live service | `t_2cefb02f`, `t_3d678dca` | after Gate 6 | `GT-80` and `GT-90` within Gate 7 |

A stage cannot pass because another lane is green. Every required incoming lane
must supply an admissible packet for the exact candidate.

## 3. Evidence ownership and artifact routing

The planning owner for the tasks below is the `default` profile named on each
card. At execution, each packet must repeat the exact named assignee and task ID;
a generic team name is invalid. The independent reviewer must not have
implemented the packet. `t_5306f093` performs the downstream integration audit
in a separate execution context but does not convert one GitHub identity into
cryptographic independence.

| Key | Evidence owner card | Canonical packet class | Required disposition |
| --- | --- | --- | --- |
| `E-GOV` | `default` / `t_15f5019e` | `Evidence/GT-00/<candidate>/<packet>/manifest.md` | independent audit, then game-owner Gate 0 decision |
| `E-QA` | `default` / `t_aefa8e9a` | stage-profile evidence directory for the exact candidate | independent build/save/content review; owner GO at stage |
| `E-PROD` | `default` / `t_c6e9368a` | stage and realm evidence directories | independent functional review; owner creative/balance/release decisions remain separate |
| `E-REALM-3D` | `default` / `t_cf3f900c` | `Evidence/VIS-3D/<candidate>/<packet>/manifest.md` | independent technical disposition, then separate owner 3D decision |
| `E-REALM-2_5D` | `default` / `t_cf3f900c` | `Evidence/VIS-2_5D/<candidate>/<packet>/manifest.md` | independent technical disposition, then separate owner 2.5D decision |
| `E-PLAT` | `default` / `t_a69cf1b7` | applicable stage-profile evidence directory | independent parity/accessibility/localization review; owner platform/release decision |
| `E-MP` | `default` / `t_d4d26ddf` | applicable stage-profile evidence directory | independent authority/recovery/capacity review; exact numerical criteria remain on `t_7f6be100` |
| `E-SEC` | `default` / `t_30eadba6` | applicable stage-profile evidence directory | independent security/safety review; owner residual-risk and release decision |
| `E-COM` | `default` / `t_4493492b` | applicable stage-profile evidence directory | independent ledger/economy/commerce review; owner balance/monetization decision |
| `E-COMP` | `default` / `t_616edab5` | applicable stage-profile evidence directory | official-source checklist review; owner applicability/scope/release decision |
| `E-REL` | `default` / `t_aa3849be` | applicable stage-profile evidence directory | independent release/runbook review; owner exposure, resume, vendor, spend, and GO decisions |
| `E-LIVE` | `default` / `t_2cefb02f`, `t_3d678dca` | `GT-80` and `GT-90` evidence directories | independent stabilization/handoff reviews followed by separate owner acceptances |

Canonical gate records, approval mirrors, stop-ship records, rollback records, and
large-evidence attachments use the exact locations and identities defined by the
evidence-governance control. Every matrix row below routes through one of these
keys. A key is not evidence by itself; the immutable manifest and retained
payload are required.

## 4. Immutable-authority traceability

The requirement text, authorized decision maker, status, and reopen trigger are
read from the authority register. The final column abbreviates that source-owned
reopen rule; it never transfers approval to the evidence owner.

| Authority | Responsible implementation or governance epic(s) | Stage gate(s) | Evidence key | Decision and conflict routing |
| --- | --- | --- | --- | --- |
| `GOV-01` | `t_15f5019e`, `t_3d678dca` | `GT-00`, `GT-90` | `E-GOV`, `E-LIVE` | Game owner; resourcing conflict returns to owner and blocks affected scope |
| `GOV-02` | `t_15f5019e`, `t_aa3849be` | `GT-00`, `GT-50`, `GT-70`, `GT-90` | `E-GOV`, `E-REL`, `E-LIVE` | All register-listed owner-exclusive decisions remain owner-only |
| `GOV-03` | `t_15f5019e` | `GT-00` | `E-GOV` | Board conflict or traceability gap is Gate 0 fail-closed |
| `GOV-04` | `t_15f5019e` | `GT-00` | `E-GOV` | Material change reopens `t_0648ce23`; silence never approves |
| `PROD-01` | `t_c6e9368a`, `t_3cbf17ac`, `t_3d678dca` | `GT-10`, `GT-40`, `GT-50`, `GT-70`, `GT-90` | `E-PROD`, `E-REL`, `E-LIVE` | Game owner controls platform and release sequence |
| `PROD-02` | `t_cf3f900c`, `t_5ca7c5f2`, `t_3be4c45c`, `t_bd653156`, `t_77448d81` | `GT-10`, `GT-20`, `GT-30`, `GT-40` | `E-REALM-3D`, `E-REALM-2_5D` | Failed slice blocks the next full-realm slice |
| `PROD-03` | `t_cf3f900c`, fixed realm chain in Section 2.2 | `GT-10`, `GT-20`, `GT-30`, `GT-40` | `E-REALM-3D`, `E-REALM-2_5D` | Only explicit owner revision or source trigger can change order |
| `PROD-04` | `t_5ca7c5f2`, `t_3be4c45c`, `t_bd653156`, `t_77448d81` | `GT-10`, `GT-20`, `GT-30`, `GT-40` | `E-PROD`, `E-REALM-3D`, `E-REALM-2_5D` | Owner creative/release review; parity failure blocks advancement |
| `VIS-01` | `t_cf3f900c` | `GT-10` through `GT-60`; `VIS-3D`, `VIS-2_5D` | `E-REALM-3D`, `E-REALM-2_5D` | Separate owner decisions; one mode cannot satisfy the other |
| `ACC-01` | `t_a69cf1b7`, `t_3cbf17ac` | `GT-40`, `GT-50`, `GT-60`, `GT-70` | `E-PLAT` | Critical failure blocks affected exposure; only owner can approve an allowed exception |
| `LOC-01` | `t_a69cf1b7`, `t_3cbf17ac` | `GT-40`, `GT-50`, `GT-60`, `GT-70` | `E-PLAT`, `E-REL` | Incomplete English/Korean parity blocks simultaneous launch |
| `PLAT-01` | `t_927d3dd9` | `GT-40`, `GT-50`, `GT-60` | `E-PLAT`, `E-SEC` | Owner controls account/platform/release changes |
| `PLAT-02` | `t_927d3dd9` | `GT-40`, `GT-50`, `GT-60`, `GT-70` | `E-PLAT`, `E-COM` | No 1.0 transfer default; post-launch need returns to owner |
| `PLAT-03` | `t_a69cf1b7`, `t_3cbf17ac` | `GT-40`, `GT-50` | `E-PLAT` | Incomplete system parity blocks shared scarce-state exposure |
| `PLAT-04` | `t_a69cf1b7`, `t_7d1036e8` | `GT-40`, `GT-50` | `E-PLAT`, `E-MP` | Fairness/accessibility evidence returns material changes to owner |
| `PLAT-05` | `t_4f3f4535`, `t_3cbf17ac` | `GT-40`, `GT-50`, `GT-60`, `GT-70` | `E-PLAT`, `E-COMP`, `E-REL` | Every public channel needs evidence and owner GO |
| `PLAT-06` | `t_00f0f879`, `t_4493492b` | `GT-40`, `GT-50`, `GT-60` | `E-COM`, `E-PLAT` | Contract/player-impact change requires owner disclosure and approval |
| `AUTO-01` | `t_7d1036e8`, `t_c6e9368a` | `GT-20`, `GT-30`, `GT-40`, `GT-50` | `E-PROD`, `E-MP`, `E-PLAT` | Material fairness, security, or accessibility evidence returns to owner |
| `REG-01` | `t_3cbf17ac`, `t_d6105f7a` | `GT-50`, `GT-60`, `GT-70` | `E-COMP`, `E-REL` | Territory expansion remains blocked under `t_d7595f9f` |
| `REG-02` | `t_a8960ec8`, `t_64e836b0`, `t_9cd91a98` | `GT-20`, `GT-30`, `GT-40`, `GT-50` | `E-MP`, `E-SEC`, `E-COMP` | Regional/data-residency failure blocks the affected exposure |
| `REG-03` | `t_a8960ec8`, `t_18ab6a1a` | `GT-30`, `GT-40`, `GT-50` | `E-MP` | Population evidence is not capacity evidence; owner controls scale changes |
| `CAP-01` | `t_d4d26ddf`, `t_18ab6a1a`, `t_d64db3b5` | `GT-20` through `GT-60` | `E-MP`, `E-REL` | Consume `t_7f6be100` only by reference; vendor/spend/exposure remain owner-only |
| `REL-01` | `t_aa3849be` | `GT-10` through `GT-90` | `E-REL`, `E-LIVE` | Consume `t_4a5b066c` only by reference; hard stop-ship cannot be pooled away |
| `GUILD-01` | `t_db8f937f` | `GT-30`, `GT-40`, `GT-50` | `E-PROD`, `E-SEC` | Social/safety change requires owner ruling |
| `GUILD-02` | `t_db8f937f` | `GT-30`, `GT-40`, `GT-50` | `E-PROD`, `E-MP` | The locked guild cap is tested; owner alone may revise it |
| `GUILD-03` | `t_db8f937f`, `t_904a275f` | `GT-30`, `GT-40`, `GT-50` | `E-PROD`, `E-MP` | Alliance cap is fail-closed until evidence and owner selection |
| `ECON-01` | `t_00f0f879`, `t_4493492b` | `GT-30`, `GT-40`, `GT-50`, `GT-60` | `E-COM`, `E-SEC` | Economy/fraud change returns to owner; ledger failure stops promotion |
| `ECON-02` | `t_00f0f879`, `t_4493492b` | `GT-40`, `GT-50`, `GT-60` | `E-COM`, `E-SEC` | Exact listing controls remain owner-gated |
| `MON-01` | `t_00f0f879`, `t_4493492b`, `t_904a275f` | `GT-40`, `GT-50`, `GT-60`, `GT-70` | `E-COM`, `E-COMP` | Exact offers remain blocked; prohibited monetization is stop-ship |
| `BAL-01` | `t_c6e9368a`, `t_4493492b`, `t_904a275f` | `GT-30`, `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Formula change or live widening requires owner approval |
| `BAL-02` | `t_c6e9368a`, `t_4493492b`, `t_904a275f` | `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Exact live defaults remain blocked pending owner ruling |
| `BAL-03` | `t_c6e9368a`, `t_4493492b`, `t_904a275f` | `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Exact amount/exceptions remain blocked pending owner ruling |
| `BAL-04` | `t_c6e9368a`, `t_4493492b`, `t_904a275f` | `GT-40`, `GT-50` | `E-PROD`, `E-COM`, `E-PLAT` | Exact rates/behavior remain blocked pending owner ruling |
| `BAL-05` | `t_c6e9368a`, `t_4493492b` | `GT-30`, `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Settlement/identity evidence returns changes to owner |
| `BAL-06` | `t_c6e9368a`, `t_4493492b`, `t_904a275f` | `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Exact buff/reactivation values remain blocked pending owner ruling |
| `PROG-01` | `t_c6e9368a` | `GT-10`, `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Progression/class-identity change requires owner ruling |
| `PROG-02` | `t_c6e9368a`, `t_904a275f` | `GT-10`, `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Exact ceiling remains blocked pending owner ruling |
| `PROG-03` | `t_c6e9368a`, `t_904a275f` | `GT-10`, `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Lifecycle change requires owner ruling |
| `PROG-04` | `t_c6e9368a`, realm-slice cards | `GT-10`, `GT-20`, `GT-30`, `GT-40` | `E-PROD`, `E-REALM-3D`, `E-REALM-2_5D` | Encounter-specific choices remain owner-gated |
| `PROG-05` | `t_c6e9368a`, `t_4493492b`, `t_904a275f` | `GT-30`, `GT-40`, `GT-50` | `E-PROD`, `E-COM` | Only source-bounded experiments are permitted; live defaults need owner approval |
| `WORLD-01` | `t_7d1036e8`, `t_c8ea885d`, `t_c6e9368a` | `GT-20`, `GT-30`, `GT-40` | `E-PROD`, `E-MP`, `E-SEC` | Deferred timing/eligibility/reward values remain blocked |
| `WORLD-02` | `t_c6e9368a`, `t_7d1036e8` | `GT-10`, `GT-40`, `GT-50` | `E-PROD`, `E-MP` | Topology/balance change requires owner ruling |
| `LIVE-01` | `t_aa3849be`, `t_2cefb02f`, `t_3d678dca` | `GT-70`, `GT-80`, `GT-90` | `E-REL`, `E-LIVE` | Calendar never overrides gates; owner accepts operating authority and handoff |
| `COMP-01` | `t_616edab5`, `t_3cbf17ac` | `GT-40`, `GT-50`, `GT-60`, `GT-70`, `GT-90` | `E-COMP`, `E-REL`, `E-LIVE` | Unverifiable applicability excludes affected scope or returns to owner |

Historical `HIST-01` and `HIST-02` are audit-only. They map to no implementation
epic or stage permission; `AUTO-01` is controlling.

## 5. Soft-launch cut

Gate 5 remains `FAIL-CLOSED` until the exact soft-launch candidate has all of the
following, with no aggregate substitute:

1. Gate 4 approved and all four realm slices completed in the fixed order, with
   separate owner-approved 3D and 2.5D evidence for each applicable slice.
2. Windows, Android, and iOS system parity for gameplay authority, account,
   progression, inventory, social/economy participation, optional automation,
   inputs, accessibility, telemetry, recovery, and mixed-client behavior.
3. One canonical account, durable realm lock, and no version-1.0 realm transfer.
4. Complete English and Korean player-facing text and voice parity, including
   account, commerce, safety, support, incident, accessibility, and content paths.
5. Comprehensive platform-level accessibility evidence for every critical flow
   and prioritized real-time cue.
6. United States/Canada/Korea scope only, correct Korea/North America regional
   sequencing, and region-local authoritative gameplay, economy, social, and
   backup state.
7. Multiplayer, queue, recovery, capacity, and cost evidence evaluated directly
   against `t_7f6be100`, without a copied threshold, invented provider, arbitrary
   battle cap, or preset cost ceiling.
8. Security assurance for account, authorization, authoritative gameplay,
   payments, secrets, personal data, abuse, moderation, support access,
   penetration findings, and supply chain.
9. Protected Oathmark market and all locked economy/progression/commerce
   invariants, with no paid randomness, power, competitive advantage, or paid
   progression advantage.
10. Every beta-deferred launch value needed by the candidate resolved through
    `t_904a275f` and an explicit game-owner decision. A missing value is not zero,
    disabled, unlimited, or platform default.
11. Current official-source compliance/store/platform/payment evidence for the
    exact artifacts and flows, with unresolved applicability excluded or
    fail-closed.
12. Rehearsed artifact promotion, cohort halt, rollback, compatible recovery,
    reconciliation, incident, support, community, moderation, and bilingual
    communication paths owned under the owner-plus-AI model.
13. Release entry, stop-ship, known-issue, rollback, restore, kill-switch, and
    resume criteria evaluated directly against `t_4a5b066c`, followed by
    independent review and explicit game-owner soft-launch GO.

## 6. Blocked and deferred nodes

These nodes are visible dependencies, not implementation defaults.

| Blocked node | Owning card or ledger route | Must resolve by | Fail-closed behavior |
| --- | --- | --- | --- |
| `U-01` alliance cap | `t_904a275f`; authority-register ledger | Gate 5 if alliances are in the candidate | No cap is selected or promised |
| `U-02` supported device/OS tiers | `t_d64db3b5` | Gate 4 | No invented support tier or device promise |
| `U-03` networking ceilings | `t_d64db3b5` | Gate 4 | No invented latency, acknowledgement, jitter, or loss ceiling |
| `U-04` primary hosting vendor | `t_d64db3b5` | Gate 4 | Provider-neutral work may proceed; no vendor winner or commitment |
| `U-05` cost ceiling or unit-cost stop | `t_d64db3b5` | Gate 4 and every later scale-up | No preset ceiling; owner approves measured spend/exposure |
| `U-06` exact monetization package | `t_904a275f` | Gate 5 if commerce is exposed | No price, SKU, benefit, cap, refund window, or trigger default |
| `U-07` live balance defaults/widening | `t_904a275f` | Gate 5 for every exposed value | Source-bounded experiments do not become live defaults |
| `U-08` other source-deferred values | `t_904a275f`; authority-register ledger | First stage that would expose the value | Affected behavior remains unavailable or on the last approved baseline |
| `U-09` post-1.0 private-kingdom demolition | `t_d7595f9f`; authority-register ledger | No pre-1.0 deadline is authorized | No implementation, promise, placeholder authority, or launch dependency |
| `U-10` realm compass ring-slot assignment | `t_15f5019e` deferred register and an owner decision card when triggered | Before any candidate needs a non-neutral assignment | Retain neutral stable IDs; do not choose or randomize placement |
| `U-11` territory expansion | `t_d7595f9f` | Post-Gate-7 owner proposal only | No service, marketing, payment, or date promise outside approved territory |
| `U-12` unverifiable legal/store applicability | `t_616edab5` and an affected-scope decision card | First affected gate | Exclude the affected feature, territory, or payment path; no legal conclusion |

If a future consumer cannot identify a dedicated decision card for a triggered
item, `t_15f5019e` creates and links one before dispatch. That clerical action does
not itself resolve the item or grant authority.

## 7. Owner decisions, stop-ship, and downstream conflict escalation

### 7.1 Owner decision points

The game owner alone records:

- Gate 0 roadmap/authority/order approval and any material reopen;
- separate 3D and 2.5D dispositions for every required visual scope;
- authorization to start each later full realm slice;
- platform/account/territory/vendor/spend/capacity/cost and deferred-value
  decisions where the source reserves them;
- beta, soft-launch, 1.0, exposure-resume, known-risk, destructive-restore, and
  stage/date-advance decisions; and
- stabilization acceptance, live-service handoff, sustainable workload limits,
  cadence, and post-1.0 expansion decisions.

A technical pass, agent recommendation, provider result, platform default,
calendar event, completed card, merged PR, or owner silence is not a decision.

### 7.2 Stop-ship path

1. Any observing reviewer records the source signal against the exact candidate
   and applies only containment expressly authorized by `t_4a5b066c`.
2. The gate becomes `STOP_SHIP` or `FAIL_CLOSED`; dependent dispatch and exposure
   promotion freeze.
3. The evidence owner creates the immutable stop-ship record and preserves raw
   evidence, candidate identity, authoritative state, and the last approved
   compatible baseline.
4. Only source-authorized reversible rollback/containment may run. Destructive
   financial, economy, inventory, progression, Realm Gem, Wish, or other
   owner-reserved state action requires the explicit owner decision.
5. Repair, new evidence, independent review, reopen criteria, and the exact
   source-required owner decision are all required before promotion resumes.

### 7.3 Conflict escalation

A downstream card that finds an omission, contradiction, supersession conflict,
unauthorized decision assignment, invented constraint, inaccessible source, or
candidate/evidence drift must:

1. stop only the affected scope and all descendants that consume it;
2. link the finding to the controlling authority ID, source card, candidate,
   evidence packet, and first affected gate;
3. record it in the authority-register unresolved/conflict ledger and on a linked
   decision card owned by the register-named authority;
4. have `t_15f5019e` compute and record the downstream impact set; unaffected
   approved baselines remain intact;
5. reopen every invalidated packet and gate, including `t_0648ce23` for a material
   roadmap change; and
6. resume only after the authorized owner ruling, register/version update,
   corrected DAG, fresh evidence, independent review, and required gate approval.

No downstream epic may resolve a conflict by picking the newer-looking text,
copying a number, averaging evidence, accepting a vendor default, or narrowing an
owner-reserved decision.

## 8. Change control and audit handoff

`t_5306f093` audits this candidate against every active authority row, source
reference, epic, gate, evidence key, blocked item, and board edge. Audit failure
leaves Gate 0 unapproved and records the failure as stop-ship. Audit success is a
technical disposition only; the game owner remains the Gate 0 approver.

A revision must increment this document version, identify its parent approved
baseline and candidate ID, preserve superseded history, update the board before
claiming the Markdown is current, rerun cycle/orphan/coverage validation, and
reopen every impacted gate. Numerical criteria owned by `t_4a5b066c` and
`t_7f6be100` remain source references only.
