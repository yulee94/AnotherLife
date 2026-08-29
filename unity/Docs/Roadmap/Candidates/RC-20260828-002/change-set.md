# Roadmap Candidate Change Set RC-20260828-002

**Candidate ID:** `RC-20260828-002`

**Parent approved baseline:** `RB-20260828-001`

**Proposed promoted baseline:** `RB-20260828-002`

**Governance control:** `GOV-G0-v1.0.0`

**Candidate owner:** `default` / `t_a4c586ff`

**State:** `FROZEN FOR CANONICAL HASH INVENTORY — UNAPPROVED`

**Supersedes:** `RC-20260828-001`, withdrawn before owner review because raw working-tree hashes were line-ending dependent.

## Purpose and scope

This revision preserves the exact roadmap meaning of RC-001 while making artifact hashes portable. It corrects `SS-20260828-001` by assigning governed identities, consistent candidate-only statuses, canonical candidate/baseline/gate/rollback records, and an owner package. It changes no source authority, numerical gate, realm order, stage order, implementation scope, or owner decision boundary.

## Canonical content inventory

SHA-256 is computed over UTF-8 text with line endings normalized to LF, matching canonical Git text content across checkouts. A changed canonical byte invalidates this inventory and requires a new candidate revision.

| Artifact | Canonical SHA-256 |
| --- | --- |
| `unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v1.md` | `51fdf4431c70ac2fb77b4006c136b58735b67bed859a05b2cf6b681e52423c7e` |
| `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md` | `7d41b40e7496dffe858ab17c0bc9a7491ea971712521f575cccd0a46d4c1e8d6` |
| `unity/Docs/Roadmap/Gate0_Integrated_Delivery_DAG_v1.md` | `e94ac3820fa033b1a5e4a12821816650cd0cf9162172af6cff1ddcd5c1824343` |
| `unity/Docs/Roadmap/Gate0_Traceability_And_Authority_Audit_v1.md` | `9c71178c88b09b9535de2f6890de90a4ca15cb14b44d7f65cf43fc0da45210d3` |
| `unity/Docs/Roadmap/Baselines/RB-20260828-001/manifest.md` | `aa4a9decfd5ca4441b7ca9293b4766847adca87be23e4844aa4b717bcc843bb1` |
| `unity/Docs/Roadmap/Candidates/RC-20260828-002/approval-package.md` | `1d16afe56b1f0cf678b7b668ff2923eadc3e30ffd3afdbf7f123dfa9083662ad` |
| `unity/Docs/Roadmap/Gates/GT-00/RC-20260828-002/GR-GT-00-RC-20260828-002-001.md` | `e4a3642b2113efb75cd9a2fed9734a0b24f68296e0067b0fc983366e5a1cfdbb` |
| `unity/Docs/Roadmap/Rollbacks/RR-RC-20260828-002-001/record.md` | `74b7d9da2b1fcf3864d586a991ccf038b3922cabe2d6b7b38bc7a133ac220265` |

Stop-ship history, corrected audits, later gate records, approval mirrors, and the promoted baseline are append-only state/evidence about frozen candidate content; adding them without changing listed artifacts does not change this candidate.

## Source-card references

- Integration and supersession authority: `t_93c953eb`.
- Completed prior broad approval dependency: `t_0648ce23`.
- Gate 0: `t_1ad7a8d5`.
- Authority/governance/integration/audit tasks: `t_180378d0`, `t_7742b57d`, `t_00f412e4`, `t_5306f093`.
- Candidate approval-and-lock task: `t_a4c586ff`.
- Numerical release and capacity packages: `t_4a5b066c`, `t_7f6be100`, by reference only.
- Full 36-card inventory and exact comments: the authority register.

## Change boundary

Repository changes are limited to corrected identity/status headers, canonical records under `unity/Docs/Roadmap/`, and the candidate validator. RC-001 and its technical audit remain preserved as withdrawn history. Board integration edges already recorded by `t_00f412e4` remain unchanged. No implementation permission is added.

## Approval and rollback

This candidate remains fail-closed until its independent audit passes and the game owner explicitly decides exact `RC-20260828-002` on `t_a4c586ff`. `REVISE`, `REJECT`, audit failure, or drift isolates only this candidate under `RR-RC-20260828-002-001`; parent `RB-20260828-001` remains retained.
