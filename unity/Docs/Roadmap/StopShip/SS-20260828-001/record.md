# Stop-Ship Record SS-20260828-001

**State:** `STOP_SHIP — OPEN`

**Recorded UTC date:** 2026-08-28

**Recorder:** `default` / `t_5306f093`

**Affected gate:** `GT-00` / `t_1ad7a8d5`

**Affected candidate:** `MISSING — FAIL-CLOSED`; the inspected artifact is
`unity/Docs/Roadmap/Gate0_Integrated_Delivery_DAG_v1.md` version `1.0.0`, dated
2026-08-28, but it has no required `RC-*` identity or parent `RB-*` baseline.

**Controlling authorities:** `GOV-02`, `GOV-03`, `GOV-04`; Gate 0 profile in
`unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md`.

## Source signals

1. The governance control requires `GOV-G0-v*`, `RC-*`, and `RB-*` identities,
   a canonical candidate change set, and a retained approved-baseline manifest.
   None exists in the audited package.
2. `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md:7` claims an
   owner-approved planning baseline while the other two Gate 0 artifacts remain
   candidates and final approval/lock task `t_a4c586ff` is `todo`.

Full evidence and the 44-authority plus 12-unresolved-requirement traceability
matrix are retained at
`unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v1.md`.

## Affected scope and containment

- Gate 0 approval, baseline lock, and every dependent implementation or exposure
  promotion remain blocked on this package.
- Preserve the previously approved board roadmap and all unaffected approved
  baselines. Do not treat the three candidate Markdown files, a merged PR, a
  completed task, or owner silence as approval.
- No runtime rollback or destructive state action is authorized or required. The
  unapproved candidate remains quarantined as documentation only.

## Owners and authority

- **Investigation/correction owner:** Gate 0 roadmap steward on `t_a4c586ff`, with
  traceability impact routing through `t_15f5019e`.
- **Independent re-audit owner:** a reviewer who did not prepare the corrected
  candidate package.
- **Resume and Gate 0 approval authority:** game owner only.

## Required correction and closure evidence

1. Declare the governance-control identity, roadmap candidate ID, parent approved
   baseline ID, canonical candidate change set, and retained baseline manifest.
2. Make all package statuses candidate-only until the exact final package receives
   explicit owner approval.
3. Re-run source, authority, edge, parity, numerical-reference, and prohibited-
   invention validation against the corrected immutable candidate.
4. Record an independent PASS, then obtain the explicit game-owner Gate 0 decision
   and create append-only approval and baseline records.

Closure of this record without all four steps is not a gate pass. Any changed
candidate, source ruling, dependency, artifact identity, authority assignment, or
new conflict reopens the stop-ship record and all affected evidence packets.

## Append-only remediation event — 2026-08-28T09:27:12Z

Candidate owner `default` / `t_a4c586ff` assigned `GOV-G0-v1.0.0`,
`RC-20260828-001`, parent `RB-20260828-001`, and proposed baseline
`RB-20260828-002`; normalized the three package statuses to candidate-only; and
prepared the canonical candidate, retained-baseline, gate, approval-package, and
rollback records. The candidate change set contains the frozen SHA-256 inventory.

This remediation does not close the incident. A corrected independent audit and
explicit game-owner decision on the exact candidate remain required. Gate 0 and
dependent dispatch stay fail-closed.

## Append-only independent-review event — 2026-08-28T09:38:34Z

Delegated read-only reviewer `deleg_514a5344` task `0`, operating in a separate
execution context from candidate preparation, inspected the corrected package and
ran `python tools/roadmap/validate_gate0_candidate.py .`. The validator exited `0`
with 44 matching authority rows, 12 fail-closed unresolved rows, and 8/8 frozen
hashes verified. Targeted identity, status, ordering, visual-separation,
numerical-reference, prohibited-invention, rollback, and reopen checks passed.

The corrected technical audit is
`unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v2.md`. This incident
remains open pending the game owner's explicit decision on exact candidate
`RC-20260828-001` and the resulting approval/baseline/gate/closure records.

## Append-only candidate-revision event — 2026-08-28T09:53:23Z

After the RC-001 technical PASS, the candidate owner found that raw working-tree
hashes were not portable across Git line-ending conversion. RC-001 was withdrawn
before owner review and is preserved at commit
`ae7deb96027be93a6bb2a823dc8d01cace299165` with a canonical supersession record.
It cannot be approved, promoted, or reused.

`RC-20260828-002` preserves identical roadmap meaning and uses canonical UTF-8/LF
SHA-256 values matching Git text content. Delegated read-only reviewer
`deleg_a8aba4b2` task `0` / `sa-0-f632fede` independently ran both validator modes;
both exited `0`, with 44 matching authority rows, 12 fail-closed unresolved rows,
and 8/8 canonical hashes verified. Targeted order, visual-separation,
numerical-reference, prohibited-invention, rollback, reopen, historical-isolation,
and approval-boundary checks passed.

The controlling technical audit is
`unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v3.md`. This incident
remains open pending an explicit game-owner decision naming exact RC-002 and the
resulting approval, promoted-baseline, Gate 0, and closure records.
