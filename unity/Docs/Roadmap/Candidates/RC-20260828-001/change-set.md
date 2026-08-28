# Roadmap Candidate Change Set RC-20260828-001

**Candidate ID:** `RC-20260828-001`

**Parent approved baseline:** `RB-20260828-001`

**Proposed promoted baseline:** `RB-20260828-002`

**Governance control:** `GOV-G0-v1.0.0`

**Candidate owner:** `default` / `t_a4c586ff`

**State:** `FROZEN FOR HASH INVENTORY — UNAPPROVED`

## Purpose and scope

This candidate packages the already integrated board roadmap into the versioned Gate 0 governance form required by `GOV-G0-v1.0.0`. It corrects the two identity/status findings in `SS-20260828-001` without changing a source authority, numerical gate, realm order, stage order, implementation scope, or owner decision boundary.

The candidate:

1. assigns `GOV-G0-v1.0.0`, `RC-20260828-001`, `RB-20260828-001`, and proposed `RB-20260828-002` identities;
2. makes all three core Gate 0 documents candidate-only and unapproved;
3. retains the prior approved board roadmap in a canonical baseline manifest;
4. provides one owner approval package, Gate 0 record, rollback record, and exact hash inventory; and
5. preserves the first failed audit and open stop-ship record for append-only history pending corrected independent review and exact owner approval.

## Canonical content inventory

The SHA-256 values below bind the frozen candidate content. A changed byte in a
listed artifact invalidates the inventory and requires a new candidate revision.

| Artifact | SHA-256 |
| --- | --- |
| `unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md` | `daabaadff644502706774b265d9cb5bb9c83ac46f284f16f855d27d8badb32d5` |
| `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md` | `8a9ef0f72d97e9870f743a17525f030bf271d702e6a0d2a5e441f8e32a8f584d` |
| `unity/Docs/Roadmap/Gate0_Integrated_Delivery_DAG_v1.md` | `0f4f244735610bd4ebd97a5d35715ff7002df7d8b1ac695ed524f64f90e7ad19` |
| `unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v1.md` | `c9a5efe71aca93b36da7592eb07960807175a4668b6857705589521d99ceb91d` |
| `unity/Docs/Roadmap/Baselines/RB-20260828-001/manifest.md` | `aa4a9decfd5ca4441b7ca9293b4766847adca87be23e4844aa4b717bcc843bb1` |
| `unity/Docs/Roadmap/Candidates/RC-20260828-001/approval-package.md` | `7e2c4c23786ed2c73ea8ffa00d006f5fbb064075faa13053931bab22e123f425` |
| `unity/Docs/Roadmap/Gates/GT-00/RC-20260828-001/GR-GT-00-RC-20260828-001-001.md` | `950db784e0b1919887757c2f33cc39ac2e112eccf252addd67dcd930773d1686` |
| `unity/Docs/Roadmap/Rollbacks/RR-RC-20260828-001-001/record.md` | `78e95aa443224305d30064c72ed5274be5c0b23121f8cf071837f04d47a09a4b` |

The append-only stop-ship history, corrected independent audit, later gate-record
revisions, approval mirrors, and promoted-baseline manifest are state/evidence
records about this frozen content. They receive their own identities and hashes;
adding them without changing the artifacts above does not alter this candidate.

## Source-card references

- Board integration and controlling supersession index: `t_93c953eb`.
- Explicit prior broad-roadmap approval dependency: `t_0648ce23` (complete with owner-authored approval).
- Gate 0 root: `t_1ad7a8d5`.
- Authority register task: `t_180378d0`.
- Evidence-governance task: `t_7742b57d`.
- DAG integration task: `t_00f412e4`.
- Independent audit and stop-ship source: `t_5306f093`.
- Candidate approval-and-lock task: `t_a4c586ff`.
- Exact numerical release package: `t_4a5b066c`, by reference only.
- Exact numerical capacity/SLO/cost package: `t_7f6be100`, by reference only.
- Full 36-card source inventory and per-authority comment references: `Gate0_Immutable_Authority_Register_v1.md`.

## Board and repository change boundary

The integration task had already added three synchronization edges: `t_c1b2323a -> t_587aeef4`, `t_587aeef4 -> t_debd2042`, and `t_debd2042 -> t_d6105f7a`. This candidate records those approved integration inputs; it adds no implementation scope and grants no dispatch authority.

Repository changes are limited to the identity/status header corrections and new canonical manifests/records under `unity/Docs/Roadmap/`. The prior failed audit remains unmodified.

## Approval and rollback

The candidate remains fail-closed until a corrected independent audit passes and the game owner explicitly decides this exact candidate on `t_a4c586ff`. `REVISE`, `REJECT`, audit failure, or identity drift isolates only this candidate under `RR-RC-20260828-001-001`; `RB-20260828-001` remains retained and unchanged.
