# Gate 0 Owner Approval Package — RC-20260828-001

**Candidate ID:** `RC-20260828-001`

**Parent approved baseline:** `RB-20260828-001`

**Proposed promoted baseline:** `RB-20260828-002`

**Governance control:** `GOV-G0-v1.0.0`

**Gate:** `GT-00` / `t_1ad7a8d5`

**Approval-and-lock task:** `t_a4c586ff`

**State:** `OWNER APPROVAL REQUIRED — FAIL-CLOSED`

## Recommendation

Approve only the exact immutable candidate after the corrected independent audit records `PASS` and `SS-20260828-001` has closure evidence. Approval means accepting the integrated roadmap, authority matrix, gate ordering, and unresolved/fail-closed dispositions together. Do not approve individual files or lanes in isolation.

## Exact package under decision

1. Integrated roadmap and delivery DAG: `unity/Docs/Roadmap/Gate0_Integrated_Delivery_DAG_v1.md`.
2. Authority register: `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md`.
3. Evidence governance and gate definitions: `unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md`.
4. Traceability matrix and first audit: `unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v1.md`; this preserved audit is a failed historical disposition and is not approval evidence.
5. Corrected independent audit: canonical `Gate0_Traceability_And_Authority_Audit_v2.md`; no qualifying record yet means approval remains fail-closed.
6. Candidate change set: `unity/Docs/Roadmap/Candidates/RC-20260828-001/change-set.md`.
7. Retained parent baseline: `unity/Docs/Roadmap/Baselines/RB-20260828-001/manifest.md`.
8. Gate record: `unity/Docs/Roadmap/Gates/GT-00/RC-20260828-001/GR-GT-00-RC-20260828-001-001.md`.
9. Rollback record: `unity/Docs/Roadmap/Rollbacks/RR-RC-20260828-001-001/record.md`.
10. Stop-ship record: `unity/Docs/Roadmap/StopShip/SS-20260828-001/record.md`.

The SHA-256 inventory in the candidate change set binds the exact content presented for audit and owner review. A content change creates a new candidate revision; approval of an earlier hash set is not reusable.

## Integrated roadmap and gate ordering

The stage order is immutable unless reopened by the owner:

```text
GT-00 roadmap authority lock
  -> GT-10 pre-production exit / production entry
  -> GT-20 production exit / Korea Windows internal alpha
  -> GT-30 internal-alpha exit / North America + Korea Windows closed alpha
  -> GT-40 closed-alpha exit / Windows-Android-iOS cross-platform beta
  -> GT-50 beta exit / simultaneous United States-Canada-Korea soft launch
  -> GT-60 soft-launch exit / approved-territory 1.0 entry
  -> GT-70 explicit final 1.0 GO
  -> GT-80 stabilization exit
  -> GT-90 sustainable live-service handoff exit
```

Board gates `t_debd2042` and `t_50726ad6` each retain their two sequential governance profiles; `GT-60` cannot substitute for `GT-70`, and `GT-80` cannot substitute for `GT-90`.

Realm production remains Stonehold -> Eldergrove -> Crownlands -> Umbral. Every required realm transition retains separate `VIS-3D` and `VIS-2_5D` evidence, independent review, owner decisions, rollback targets, and reopen handling.

## Authority register and traceability

The register contains 44 active authority rows mapped exactly once in the integrated DAG and traceability matrix. Owner-exclusive release, creative, visual, balance, monetization, vendor, spend, scope, funding, exposure-resume, date-advance, destructive-restore, and major-expansion decisions remain with the game owner. Numerical release and capacity packages remain source-card references to `t_4a5b066c` and `t_7f6be100`; this package does not copy, round, or alter their gates.

The complete source-card inventory and supersession rules are in the authority register. Controlling integration sources are `t_93c953eb`, explicit approval dependency `t_0648ce23`, and source tasks cited by each authority row. Repository Markdown is a versioned mirror; the Hermes Kanban remains authoritative.

## Unresolved-ambiguity log

All 12 entries are intentional fail-closed deferrals, not permission or defaults:

| ID | Disposition |
| --- | --- |
| `U-01` | Alliance guild-count cap remains unselected pending closed-alpha evidence and owner balance decision. |
| `U-02` | Exact supported OS/device/GPU/RAM tiers remain unselected pending representative Stonehold evidence and owner platform/visual/release decision. |
| `U-03` | Exact latency, acknowledgement, jitter, and loss ceilings remain unselected pending measured evidence and owner decision. |
| `U-04` | Primary hosting vendor remains unselected pending the controlled bake-off and owner vendor/spend/release decision. |
| `U-05` | Infrastructure cost ceiling/unit-cost stop remains unselected; measured packages and owner spend/exposure approval control. |
| `U-06` | Exact subscription, price, refund, convenience, and monetization numbers remain unselected pending beta evidence and owner decision. |
| `U-07` | Live balance defaults and widened experimental envelopes remain unselected outside source-bounded experiments. |
| `U-08` | Exact Oathmark, repair, corridor, relocation, convenience, and other source-deferred values remain unselected. |
| `U-09` | Private-kingdom demolition and other explicit post-1.0 features remain deferred without promise or placeholder authority. |
| `U-10` | Realm compass ring-slot assignment remains neutral and stable until owner creative/world approval. |
| `U-11` | Territory expansion beyond the United States, Canada, and South Korea remains post-1.0 and owner-gated. |
| `U-12` | Unverifiable legal/store applicability excludes affected scope; no legal conclusion is invented. |

The authority register records each exact source, responsible decision maker, trigger, and fail-closed behavior. Any new ambiguity or conflict is stop-ship for affected scope.

## Rollback and prior-baseline retention

If the owner records `REVISE` or `REJECT`, or if independent review fails, freeze only `RC-20260828-001`, preserve `RB-20260828-001`, close or revise the unmerged candidate PR without rewriting history, append the decision and evidence to the rollback/stop-ship records, and keep Gate 0 and descendants blocked. No runtime or destructive state rollback is authorized by this documentation candidate.

If approved, create `RB-20260828-002` as a new immutable manifest pointing to this candidate and `RB-20260828-001`; create an append-only approval mirror for the exact owner comment; close `SS-20260828-001` only after independent PASS and identity verification; never edit `RB-20260828-001`.

## Reopen triggers

Reopen `t_0648ce23`, Gate 0, affected evidence, and all invalidated descendants for:

- any authority or owner-decision-boundary change;
- any scope addition or material scope reduction;
- any numerical-gate revision on `t_4a5b066c` or `t_7f6be100`;
- any material DAG, milestone, cost/capacity, platform, region, compliance, evidence, or exposure change;
- candidate/evidence drift, inaccessible or expired evidence, incident, unauthorized change, or source conflict; or
- any conflict discovered by a downstream epic.

The downstream epic stops affected scope, cites the authority/source/candidate/evidence/first gate, routes the issue through `t_15f5019e`, preserves unaffected approved baselines, and resumes only after corrected evidence, independent review, and the authorized owner ruling.

## Required owner decision

A valid decision is an owner-authored comment on `t_a4c586ff` that names `RC-20260828-001` and records one of:

- `APPROVE` — approve the exact integrated roadmap, authority matrix, gate ordering, and unresolved/fail-closed ledger as proposed baseline `RB-20260828-002`;
- `REVISE` — name the required candidate changes; or
- `REJECT` — reject the candidate and retain `RB-20260828-001`.

Silence, task completion, PR merge, CI, the earlier broad roadmap approval on `t_0648ce23`, or partial approval is not approval of this exact corrected package.
