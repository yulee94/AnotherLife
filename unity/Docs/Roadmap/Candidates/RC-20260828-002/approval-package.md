# Gate 0 Owner Approval Package — RC-20260828-002

**Candidate ID:** `RC-20260828-002`

**Parent approved baseline:** `RB-20260828-001`

**Proposed promoted baseline:** `RB-20260828-002`

**Governance control:** `GOV-G0-v1.0.0`

**Gate:** `GT-00` / `t_1ad7a8d5`

**Approval-and-lock task:** `t_a4c586ff`

**State:** `OWNER APPROVAL REQUIRED — FAIL-CLOSED`

**Supersedes:** `RC-20260828-001`, which was withdrawn before owner review because its working-tree byte hashes were not portable across Git line-ending conversion. No roadmap meaning, authority, scope, order, or decision boundary changed.

## Recommendation

Approve only this exact immutable candidate after its corrected independent audit records `PASS`. Approval accepts the integrated roadmap, authority matrix, gate ordering, and unresolved/fail-closed dispositions together. Do not approve files or lanes in isolation.

## Exact package under decision

1. Integrated roadmap: `unity/Docs/Roadmap/Gate0_Integrated_Delivery_DAG_v1.md`.
2. Authority register: `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md`.
3. Governance and gates: `unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md`.
4. Historical audit v1: `unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v1.md` (`STOP_SHIP`, preserved).
5. Historical RC-001 audit v2: `unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v2.md` (technically passed RC-001 only; not reusable).
6. RC-002 corrected independent audit: canonical `unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v3.md`; absent qualifying record means fail-closed.
7. Candidate change set: `unity/Docs/Roadmap/Candidates/RC-20260828-002/change-set.md`.
8. Retained parent baseline: `unity/Docs/Roadmap/Baselines/RB-20260828-001/manifest.md`.
9. Gate records: `unity/Docs/Roadmap/Gates/GT-00/RC-20260828-002/`.
10. Rollback record: `unity/Docs/Roadmap/Rollbacks/RR-RC-20260828-002-001/record.md`.
11. Stop-ship history: `unity/Docs/Roadmap/StopShip/SS-20260828-001/record.md`.

The candidate change set binds frozen text using SHA-256 after canonical UTF-8/LF normalization, matching Git content across Windows and non-Windows checkouts. Any semantic or canonical-content change creates another candidate revision.

## Integrated roadmap and gate ordering

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

`GT-60` cannot substitute for `GT-70`; `GT-80` cannot substitute for `GT-90`. Realm production remains Stonehold -> Eldergrove -> Crownlands -> Umbral. Every required realm transition retains separate `VIS-3D` and `VIS-2_5D` evidence, review, owner decisions, rollback, and reopen handling.

## Authority register and traceability

The register has 44 active authority rows mapped exactly once in the integrated DAG and traceability matrix. Owner-exclusive release, creative, visual, balance, monetization, vendor, spend, scope, funding, exposure-resume, date-advance, destructive-restore, and major-expansion decisions remain with the game owner. Exact numerical release and capacity packages remain source references to `t_4a5b066c` and `t_7f6be100`; no gate value is copied or altered.

Controlling board sources are `t_93c953eb`, prior broad approval dependency `t_0648ce23`, and every exact source cited by the authority register. Repository records mirror but do not replace Kanban authority.

## Unresolved-ambiguity log

| ID | Fail-closed disposition |
| --- | --- |
| `U-01` | Alliance guild-count cap remains unselected pending closed-alpha evidence and owner balance decision. |
| `U-02` | Exact supported OS/device/GPU/RAM tiers remain unselected pending representative evidence and owner decision. |
| `U-03` | Exact latency, acknowledgement, jitter, and loss ceilings remain unselected pending evidence and owner decision. |
| `U-04` | Primary hosting vendor remains unselected pending controlled bake-off and owner decision. |
| `U-05` | Cost ceiling/unit-cost stop remains unselected; measured packages and owner approval control. |
| `U-06` | Exact subscription, price, refund, convenience, and monetization numbers remain unselected. |
| `U-07` | Live balance defaults and widened experimental envelopes remain unselected outside source-bounded experiments. |
| `U-08` | Exact Oathmark, repair, corridor, relocation, convenience, and other deferred values remain unselected. |
| `U-09` | Private-kingdom demolition and other explicit post-1.0 features remain deferred. |
| `U-10` | Realm compass ring-slot assignment remains neutral until owner creative/world approval. |
| `U-11` | Territory expansion beyond the United States, Canada, and South Korea remains post-1.0 and owner-gated. |
| `U-12` | Unverifiable legal/store applicability excludes affected scope; no legal conclusion is invented. |

The authority register records each exact source, decision maker, resolution trigger, and fail-closed behavior. None of these rows supplies a default.

## Rollback and prior-baseline retention

On `REVISE`, `REJECT`, audit failure, or identity drift, freeze only `RC-20260828-002`; preserve `RB-20260828-001`; close or revise the unmerged PR without rewriting history; append the decision and evidence; and keep Gate 0 descendants blocked. No runtime or destructive-state rollback is authorized by this documentation candidate.

On `APPROVE`, create immutable `RB-20260828-002` pointing to this exact candidate and parent; create an append-only approval mirror for the exact owner comment; close `SS-20260828-001` only after identity verification; and never edit `RB-20260828-001`.

## Reopen triggers

Reopen `t_0648ce23`, Gate 0, affected evidence, and invalidated descendants for any authority change, scope addition, numerical-gate revision on its source card, material DAG/gate/cost/capacity/platform/region/compliance/evidence/exposure change, candidate drift, inaccessible evidence, incident, unauthorized change, source conflict, or downstream-epic conflict.

The downstream epic stops affected scope, cites authority/source/candidate/evidence/first gate, routes impact through `t_15f5019e`, preserves unaffected approved baselines, and resumes only after corrected evidence, independent review, and authorized owner ruling.

## Required owner decision

A valid owner-authored comment on `t_a4c586ff` must name `RC-20260828-002` and record one of:

- `APPROVE` — approve the exact integrated roadmap, authority matrix, gate ordering, and unresolved/fail-closed ledger as `RB-20260828-002`;
- `REVISE` — name required changes; or
- `REJECT` — reject the candidate and retain `RB-20260828-001`.

Silence, task completion, PR merge, CI, the earlier broad approval on `t_0648ce23`, or partial approval is not approval of this exact candidate.
