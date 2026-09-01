# Candidate Supersession — RC-20260828-001

**Candidate:** `RC-20260828-001`

**State:** `WITHDRAWN BEFORE OWNER REVIEW`

**Frozen content commit:** `ae7deb96027be93a6bb2a823dc8d01cace299165`

**Independent technical audit:** `AUD-G0-RC-20260828-001-002` passed the local frozen byte set.

**Superseded by:** `RC-20260828-002`

## Reason

After the RC-001 audit, the candidate owner found that four SHA-256 entries were calculated from Windows working-tree bytes containing repository line-ending conversions. Those values were reproducible in the audited worktree but not guaranteed portable across a fresh Git checkout. No owner decision had been requested or recorded.

RC-002 preserves the same roadmap, authorities, traceability, gate ordering, unresolved dispositions, rollback scope, and owner boundaries, but defines canonical UTF-8/LF hashes matching Git text content. RC-001 cannot be approved, promoted, or reused. Its files, commit, audit, and this supersession record remain immutable history.
