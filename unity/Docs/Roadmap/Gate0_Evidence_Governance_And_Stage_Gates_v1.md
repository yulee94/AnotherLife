# Gate 0 Evidence Governance and Stage-Gate Controls

**Control version:** `1.0.0`

**Effective baseline date:** 2026-08-28

**Status:** approved-roadmap governance candidate

**Owning gate:** `t_1ad7a8d5`

**Control task:** `t_7742b57d`

## 1. Authority and fail-closed rule

The Hermes Kanban is the durable authority for roadmap decisions, dependencies,
approvals, stop-ship state, and reopen events. Repository records are the
version-controlled index, templates, evidence manifests, and audit mirrors. A
repository mirror cannot create authority absent the required board decision.

This control consumes the Gate 0 authority register at
`unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v1.md`. That register and
its cited board sources control conflicts. In particular:

- exact numerical release, evidence-window, known-issue, recovery, rollback,
  restore, kill-switch, and related criteria are read from `t_4a5b066c`;
- exact numerical capacity, queue, SLO, battle-participation, vendor-bake-off,
  and measured-cost criteria are read from `t_7f6be100`;
- neither numerical package may be copied, rounded, reinterpreted, or changed in
  this control or in a gate record;
- 3D and 2.5D evidence are separate and non-substitutable under `t_3c74f0d3`;
- final release, creative, visual, balance, monetization, vendor, spend, scope,
  funding, exposure-resume, and other owner-reserved decisions remain with the
  game owner.

A missing source, inaccessible artifact, ambiguous value, conflicting approval,
unknown decision maker, incomplete field, stale candidate identity, or
unauthorized change automatically sets the affected gate to `FAIL-CLOSED`. It
never becomes an assumed pass, waiver, provisional default, or permission to
continue. Only the named authority may resolve the issue.

## 2. Record identities and versioning

Every governed record has an immutable identity. Mutable filenames such as
`latest`, `final`, or `approved-new` are prohibited as evidence identities.

| Record | Required identity | Version rule |
| --- | --- | --- |
| Governance control | `GOV-G0-vMAJOR.MINOR.PATCH` | Semantic version. Major changes alter authority or required fields; minor changes add compatible controls; patch changes clarify without changing meaning. |
| Approved roadmap baseline | `RB-YYYYMMDD-NNN` | Monotonic sequence. Approval creates a new baseline; an existing approved baseline is never edited in place. |
| Roadmap candidate | `RC-YYYYMMDD-NNN` | Binds exactly one parent approved baseline and one change set. A changed change set creates a new candidate revision. |
| Gate record | `GR-<gate-profile>-<candidate-id>-NNN` | New review, reopen, or changed evidence creates a new append-only record. |
| Evidence packet | `EV-<gate-profile>-<candidate-id>-NNN` | Packet contents are immutable after submission. Corrections supersede; they do not overwrite. |
| Approval record | `AP-<gate-record-id>-NNN` | One decision event per record. A later ruling supersedes by reference and preserves history. |
| Stop-ship incident | `SS-YYYYMMDD-NNN` | Append-only event and containment history. |
| Rollback record | `RR-<candidate-id>-NNN` | Binds the exact rejected or unapproved candidate and the retained baseline. |

All times use UTC in ISO 8601 form. Every record names its schema/control version,
board task or comment identifiers, repository commit, and the hashes needed to
identify its content. Hashes identify evidence; they do not prove approval.

## 3. Canonical artifact registry

Each instance must replace the role below with one named accountable owner: an AI
profile or agent for preparation/review records, and `game owner` where authority
is owner-exclusive. Unassigned ownership fails closed.

| Artifact class | Canonical location | Accountable artifact owner | Required approver or authority |
| --- | --- | --- | --- |
| Immutable authority register | `unity/Docs/Roadmap/Gate0_Immutable_Authority_Register_v<MAJOR>.md` | Gate 0 roadmap steward | Game owner for authority changes |
| Evidence governance control | `unity/Docs/Roadmap/Gate0_Evidence_Governance_And_Stage_Gates_v<MAJOR>.md` | Gate 0 governance steward | Game owner for material governance or authority changes |
| Approved baseline manifest | `unity/Docs/Roadmap/Baselines/<baseline-id>/manifest.md` | Gate 0 roadmap steward | Game owner, mirrored from an explicit owner-authored Kanban decision |
| Candidate change set | `unity/Docs/Roadmap/Candidates/<candidate-id>/change-set.md` | Named roadmap candidate owner | Game owner when the candidate changes approved direction; technical review alone never approves it |
| Gate record | `unity/Docs/Roadmap/Gates/<gate-profile>/<candidate-id>/<gate-record-id>.md` | Named evidence owner for that gate | The approver named by the gate profile |
| Evidence manifest | `unity/Docs/Roadmap/Evidence/<gate-profile>/<candidate-id>/<evidence-packet-id>/manifest.md` | Named evidence owner | Named independent reviewer; owner approval remains separate when mandated |
| Small textual evidence retained in Git | Beside its evidence manifest under `artifacts/` | Named evidence owner | Named independent reviewer |
| Binary, large, device, capture, profiler, build, or raw-log evidence | Attachments on the exact Kanban gate/evidence card named by the manifest | Named evidence owner | Named independent reviewer; game owner additionally approves owner-gated presentation or release decisions |
| 3D visual evidence packet | `unity/Docs/Roadmap/Evidence/VIS-3D/<candidate-id>/<evidence-packet-id>/manifest.md` plus attachments on the named gate card | Named 3D evidence owner | Independent evidence reviewer, then separate game-owner 3D decision |
| 2.5D visual evidence packet | `unity/Docs/Roadmap/Evidence/VIS-2_5D/<candidate-id>/<evidence-packet-id>/manifest.md` plus attachments on the named gate card | Named 2.5D evidence owner | Independent evidence reviewer, then separate game-owner 2.5D decision |
| Approval record mirror | `unity/Docs/Roadmap/Approvals/<gate-profile>/<candidate-id>/<approval-record-id>.md` | Gate recorder | The explicit Kanban decision author is authoritative; game owner where the profile requires owner approval |
| Stop-ship record | `unity/Docs/Roadmap/StopShip/<stop-ship-id>/record.md` | Named incident commander or gate reviewer | Authority to halt and resume is read from `t_4a5b066c`; owner-only resume remains owner-only |
| Rollback record | `unity/Docs/Roadmap/Rollbacks/<rollback-record-id>/record.md` | Named rollback executor | Authority is read from `t_4a5b066c`; destructive state restore is never inferred |
| Unresolved ambiguity/conflict | Authority register unresolved ledger and a dedicated Kanban decision card linked by ID | Gate 0 roadmap steward | The owner authority named by the controlling source |

A link alone is not retained evidence. The manifest must give an attachment ID,
CI run and artifact ID, or approved durable repository path, plus hash, size,
format, collection time, retention, and access status. Gate-critical evidence that
will expire before the audit window must be copied to the gate card attachment
collection. If a required payload is too large for the available attachment path,
the gate remains fail-closed until the game owner approves a durable storage
location; agents may not silently choose a vendor or omit the payload.

## 4. Roles and separation of duties

- **Candidate owner:** prepares one roadmap or release candidate and its change
  set. The candidate owner cannot self-approve.
- **Evidence owner:** collects, signs, submits, and maintains accessibility of one
  packet. This is a named accountable role, not a team alias.
- **Independent reviewer:** did not implement the candidate evidence under review;
  verifies identity, completeness, reproducibility, source criteria, and declared
  limitations. The reviewer records `PASS`, `FAIL`, or `FAIL-CLOSED` only.
- **Gate recorder:** mirrors the authoritative Kanban decision into the repository
  without changing its meaning.
- **Game owner:** exercises every owner-exclusive decision, including final stage
  GO, release, resume where reserved, separate 3D and 2.5D visual disposition,
  and any other authority retained by source.
- **Incident commander or observing agent:** may take only the reversible,
  pre-approved exposure-reduction, containment, and rollback actions expressly
  authorized by `t_4a5b066c`. This role cannot expand exposure or exercise an
  owner-exclusive decision.

A single AI identity may prepare and record artifacts when necessary, but it must
not claim independent review of its own implementation. One GitHub identity is
not proof of independent approval. The gate record names the actual roles and
board evidence used.

## 5. Evidence submission convention

An evidence packet is admissible only when its manifest contains all fields below.
A blank, `TBD`, inaccessible, or ambiguous required field is a submission failure.

```text
Evidence packet ID:
Gate profile:
Candidate ID and parent approved baseline ID:
Evidence owner (named profile/agent and task ID):
Independent reviewer (named profile/agent and task ID):
Control version:
Controlling authority references:
Repository commit and PR:
Immutable client/server/config/schema/catalog/build identities as applicable:
Platforms, regions, languages, devices, quality tiers, inputs, networks, and cohorts:
Collection UTC range:
Exact commands, tools, versions, and exit states:
Artifact inventory (canonical location, attachment/artifact ID, hash, bytes, format):
Criteria mapping (source criterion -> artifact -> reviewer finding):
3D applicability and packet ID:
2.5D applicability and packet ID:
Accessibility evidence:
Security/data/economy/commerce/compliance/player-safety evidence:
Known limitations, exclusions, blocked checks, and expired evidence:
Retention/access verification date:
Submitter signature and UTC time:
Reviewer disposition, signature, and UTC time:
Supersedes/superseded-by links:
```

Submission rules:

1. Bind one packet to one immutable candidate. Pooled evidence from different
   candidates is inadmissible unless a controlling source expressly allows it.
2. Preserve raw outputs. A summary, screenshot collage, aggregate average, or
   dashboard without the underlying retained evidence cannot satisfy a hard gate.
3. Report failed, skipped, unavailable, cancelled, blocked, and not-applicable
   checks distinctly. None is a pass.
4. Map every claim to a source criterion and artifact. Unmapped evidence does not
   close a criterion.
5. Segment evidence by every cohort required by the controlling source. A pooled
   result cannot hide a failing cohort.
6. Mark 3D and 2.5D separately. `Not applicable` needs a cited scope reason and
   reviewer acceptance; it cannot be used when that mode is required.
7. Do not transcribe numerical criteria from `t_4a5b066c` or `t_7f6be100` into the
   packet. Record the exact source card and criterion label or section, then store
   the measured result and `PASS`/`FAIL` comparison.
8. Corrections create a new packet ID. Preserve the old packet and its disposition.
9. An evidence owner verifies every canonical location immediately before gate
   review. Missing or expired evidence changes the packet to `FAIL-CLOSED`.
10. No agent may convert a technical pass, playtester preference, benchmark
    comparison, schedule target, or owner silence into approval.

## 6. Gate states and transition rules

Allowed states are:

```text
DRAFT -> SUBMITTED -> INDEPENDENT_REVIEW -> OWNER_REVIEW -> APPROVED
                         |                       |
                         +-> FAILED              +-> REVISE or REJECT
Any state -> FAIL_CLOSED or STOP_SHIP
APPROVED -> REOPENED only through a recorded trigger
```

`OWNER_REVIEW` is omitted only when the profile explicitly names a non-owner final
approver and no owner-reserved decision is involved. `APPROVED` requires all entry
criteria, evidence, independent review, and the exact required approver decision.
Silence, elapsed time, a completed task, merged code, green CI, or partial approval
cannot advance a state.

## 7. Required gate-record template

Every stage and visual gate record uses this complete template. No field may be
deleted by a specialized profile.

```text
Gate record ID:
Gate profile and stage transition:
Candidate ID:
Parent approved baseline ID:
Record owner:
Entry criteria:
Controlling numerical criteria: t_4a5b066c and/or t_7f6be100 by reference only
Evidence packet IDs and canonical locations:
Independent reviewer and disposition:
Approver and required decision vocabulary:
Approval record / authoritative Kanban comment ID:
Stop-ship conditions:
Current stop-ship incidents:
Rollback action and exact rollback target:
Reopen triggers:
Unresolved ambiguities/conflicts and owning decision cards:
Final state:
Created/updated UTC:
Supersedes/superseded-by:
```

The record fails closed when any required field is missing, when a referenced
packet is not independently reviewed, when the approver lacks authority, or when
the candidate differs from the evidence identity.

## 8. Stage-gate profiles

Each profile below supplies all required template controls. Exact measurable
thresholds, windows, volumes, issue tolerances, RPO/RTO, rollback authority,
capacity, queue, SLO, and cost evidence remain at their source cards and are not
restated here.

| Profile | Entry criteria | Required evidence | Approver | Stop-ship conditions | Rollback action | Reopen triggers |
| --- | --- | --- | --- | --- | --- | --- |
| `GT-00` Gate 0 roadmap authority lock | A complete candidate change set against one retained approved baseline; authority register and traceability complete; every ambiguity has an owner and blocked decision card; no implementation prerequisite is inserted ahead of Gate 0 | Candidate DAG and diff, authority register, governance control, traceability, unresolved ledger, source access proof, independent review, and explicit `t_0648ce23` decision when material | Game owner for roadmap approval | Omitted/contradicted authority; unauthorized decision maker; invented constraint; copied or altered source number; circular/missing prerequisite; inaccessible source; partial or silent approval | Reject or revert only the unapproved candidate change set; retain the parent approved baseline and its board history unchanged | Any material direction, scope, DAG, cost/capacity assumption, platform/region/compliance exposure, milestone gate, owner-authority change, source conflict, or explicit owner revision |
| `GT-10` pre-production exit / production entry | `GT-00` approved; prior dependencies complete; all exact entry criteria resolved from `t_4a5b066c`; applicable capacity/SLO planning resolved from `t_7f6be100`; no unresolved item is being used as a default | Stage-bound candidate packet, all source-required release evidence, authority and risk traceability, recovery/rollback proof, and applicable separate `VIS-3D` and `VIS-2_5D` packets | Independent gate pass plus explicit game-owner GO | Any source-defined hard failure; missing/ambiguous criterion; unauthorized waiver; unresolved critical risk; failed required visual mode; stale or mixed candidate evidence | Hold production entry; revert only the rejected candidate/config/change set to the last approved pre-production baseline using source-authorized reversible action | Any source-defined trigger on `t_4a5b066c` or `t_7f6be100`, failed slice/visual evidence, material scope/authority change, incident, or explicit owner revision |
| `GT-20` production exit / Korea Windows internal alpha entry | `GT-10` approved and all exact source criteria for this transition satisfied on one candidate | Source-mapped release packet, Windows candidate/build evidence, security/data/economy/recovery evidence, realm-slice evidence, and applicable separate visual packets | Independent gate pass plus explicit game-owner GO | Any applicable source-defined hard failure, missing required evidence, failed owner-gated visual mode, unauthorized scope/platform/realm substitution, or mixed candidate identity | Do not expose the candidate; restore the last approved production candidate/config through a source-authorized reversible rollback | Source-defined release/capacity triggers, failed operational or visual evidence, realm-slice change, incident, or explicit owner revision |
| `GT-30` Korea Windows internal alpha exit / North America plus Korea Windows closed alpha entry | `GT-20` approved; the separately applicable internal-alpha exit and closed-alpha entry criteria are explicitly resolvable from controlling sources; regional prerequisites complete | Internal-alpha cohort evidence, regional readiness, candidate compatibility, recovery/rollback rehearsal, source-required evidence, and applicable separate visual packets | Independent gate pass plus explicit game-owner GO | No separately resolvable criterion; failing cohort hidden by aggregate; regional/security/data/compliance failure; unauthorized owner decision; source-defined hard failure | Keep closed-alpha exposure disabled and revert only the rejected alpha candidate or reversible config to the retained approved candidate | Evidence-derived threshold change, regional/capacity/SLO breach, incident, failed visual/slice evidence, material scope change, or explicit owner revision |
| `GT-40` Windows closed alpha exit / Windows-Android-iOS cross-platform beta entry | `GT-30` approved; exact closed-alpha exit and beta entry criteria resolved; cross-platform, account, regional, accessibility, localization, and mixed-client prerequisites complete | Closed-alpha and cross-platform packets segmented as source requires; compatibility, migration, recovery, security, commerce, accessibility, language, device, and applicable separate visual evidence | Independent gate pass plus explicit game-owner GO | Incomplete system parity; unsupported invented device tier; inaccessible cohort evidence; failed accessibility/localization/security/data/economy/recovery criterion; source-defined hard failure | Keep beta exposure disabled; revert only the beta candidate/reversible configuration to the last approved closed-alpha baseline | Platform/device/region/language change, failed parity or cohort evidence, source-defined release/capacity trigger, incident, or explicit owner revision |
| `GT-50` cross-platform beta exit / simultaneous regional soft-launch entry | `GT-40` approved; all exact source criteria resolved; all fail-closed beta decisions needed for launch have explicit owner rulings | Full source-mapped release packet; Windows/Android/iOS parity; English/Korean parity; regional, store, account, commerce, moderation, accessibility, recovery, capacity/cost, security, and separate visual evidence | Independent gate pass plus explicit game-owner GO | Any unresolved launch value used as a default; missing platform/language/region evidence; owner-reserved decision made by another party; source-defined hard failure; either required visual mode not approved | Do not increase public exposure; return only the unapproved soft-launch candidate/config to the retained beta baseline using source-authorized reversible controls | Failed gate, material evidence/risk/cost/capacity/platform/region/compliance change, incident, source-defined trigger, or explicit owner revision |
| `GT-60` regional soft-launch exit / approved-territory global 1.0 entry | `GT-50` approved; exact source criteria and all territory/product-maturity requirements satisfied on the same immutable candidate contract | Soft-launch production evidence, source-required load/fault/recovery and operational evidence, complete product/channel/accessibility/localization/compliance/commerce/security packets, and separate owner-approved 3D and 2.5D evidence | Independent gate pass plus explicit game-owner GO | Calendar or pooled health used as waiver; any source-defined hard failure; territory expansion without approval; stale evidence; visual owner REVISE/REJECT; unapproved vendor/spend/exposure decision | Keep 1.0 promotion disabled; roll back only the unapproved release candidate or reversible exposure/config according to source authority; preserve the approved soft-launch baseline | Failed gate, material risk/evidence change, incident, milestone scope or territory change, capacity/vendor/cost/SLO change, visual trigger, or explicit owner revision |
| `GT-70` global 1.0 final GO | `GT-60` approved and the exact final-GO source criteria remain green on the exact immutable candidate/backend compatibility contract | Final release packet, independent review, runbook and recovery proof, all required owner decision records, retained platform/region/device/accessibility/language/visual evidence | Independent gate pass plus explicit game-owner final GO | Any source-defined hard failure, changed candidate after evidence, missing runbook/recovery proof, unapproved known issue, or absent explicit owner GO | Cancel promotion or reduce exposure only within source authority; restore the last approved compatible release/config without destructive state action unless expressly owner-approved | Any failed gate, incident, material candidate/risk/scope evidence change, source-defined trigger, or explicit owner revision |
| `GT-80` stabilization exit | `GT-70` approved; exact stabilization criteria and required operating evidence satisfied | Production, error-budget, incident, recovery, capacity, support, accessibility, platform, regional, economy, security, and source-required stabilization evidence | Independent gate pass plus explicit game-owner acceptance | Source-defined hard failure; unresolved incident; missing retained evidence; unsafe rollback/restore; unowned issue; owner-reserved waiver | Hold stabilization exit; reduce exposure or revert compatible candidate/config only within source authority; preserve authoritative state | Incident, failed operational/recovery evidence, source threshold breach, authority/resourcing change, or explicit owner revision |
| `GT-90` live-service handoff exit | `GT-80` approved; exact handoff criteria satisfied; every operational artifact and escalation has a named AI owner under the owner-plus-AI model | Source-required dashboards, alerts, runbooks, access/audit, exercises, reconciliation, support/communication, capacity/cost, and continuity evidence with owner burden/escalation records | Independent handoff pass plus explicit game-owner acceptance | Missing owner/escalation/runbook/evidence; failed exercise; unsafe authority delegation; source-defined hard failure; implied studio/vendor/human-team handoff | Keep the prior approved operating baseline and ownership model active; revert only the unapproved handoff revision or reversible config | Explicit resourcing/authority decision, failed operational or sustainability evidence, incident, source-defined breach, or explicit owner revision |

If a profile cannot resolve its exact source criteria, it is `FAIL-CLOSED`; it does
not inherit criteria from another stage or create a local numerical substitute.

## 9. Separate visual gate profiles

Both profiles are required at every realm-slice transition and wherever
`t_3c74f0d3` requires them. They may share candidate identity and common raw device
runs, but they must have separate manifests, findings, decisions, and approval
records. A pass, high score, or owner approval in one mode cannot satisfy the
other.

| Profile | Entry criteria | Required evidence | Approver | Stop-ship conditions | Rollback action | Reopen triggers |
| --- | --- | --- | --- | --- | --- | --- |
| `VIS-3D` | Exact candidate and realm/scope identified; common objective package available; all 3D mandatory criteria read directly from `t_3c74f0d3`; preceding slice gate complete where applicable | Dedicated 3D manifest and captures/profiles; source-mapped objective device, performance, readability, accessibility, provenance, originality, materials, lighting, animation, UI, LOD/streaming, and reviewer findings required by `t_3c74f0d3` | Independent evidence reviewer records technical disposition; game owner separately records 3D `APPROVE`, `REVISE`, or `REJECT` | Missing/ambiguous/wrong-device evidence; any mandatory criterion below its source requirement; provenance/originality/accessibility failure; owner `REVISE`/`REJECT`; attempt to average with 2.5D | Reject or revert only the unapproved 3D candidate assets/configuration; retain the last owner-approved 3D baseline and do not alter 2.5D approval | Every source-defined visual reopen trigger, new representative content no longer matching the approved standard, failed slice gate, or explicit owner revision |
| `VIS-2_5D` | Exact candidate and realm/scope identified; common objective package available; all 2.5D mandatory criteria read directly from `t_3c74f0d3`; preceding slice gate complete where applicable | Dedicated 2.5D manifest and captures/profiles; source-mapped objective device, strategic readability, accessibility, provenance, originality, state hierarchy, UI, replay-authority, atlas/residency/streaming, and reviewer findings required by `t_3c74f0d3` | Independent evidence reviewer records technical disposition; game owner separately records 2.5D `APPROVE`, `REVISE`, or `REJECT` | Missing/ambiguous/wrong-device evidence; any mandatory criterion below its source requirement; provenance/originality/accessibility/authority failure; owner `REVISE`/`REJECT`; attempt to substitute 3D evidence | Reject or revert only the unapproved 2.5D candidate assets/configuration; retain the last owner-approved 2.5D baseline and do not alter 3D approval | Every source-defined visual reopen trigger, new representative content no longer matching the approved standard, failed slice gate, or explicit owner revision |

## 10. Approval record convention

The authoritative approval is an explicit decision on the exact Kanban gate card
by the authorized approver. The repository mirror must preserve, without
paraphrasing away conditions:

```text
Approval record ID:
Gate record ID and profile:
Candidate ID and parent approved baseline ID:
Decision: APPROVE | REVISE | REJECT | REOPEN
Authorized approver:
Authoritative Kanban task and comment/event ID:
Verbatim decision or immutable attachment reference:
Conditions and non-waivers:
3D decision (when applicable):
2.5D decision (when applicable):
Owner-exclusive decisions preserved:
Evidence packet IDs reviewed:
Decision UTC time:
Recorder and mirror commit:
Supersedes/superseded-by:
```

Approval applies only to the named candidate, profile, evidence packets, and
scope. Editing a candidate, evidence packet, dependency, or controlling source
after approval invalidates reuse and triggers review. Approval may not be inferred
from a task status, PR merge, reaction, agent-authored summary, or silence.

## 11. Stop-ship handling

1. Any observing reviewer or authorized operational agent records the source
   signal and immediately applies only the exposure-reduction or containment
   authority granted by `t_4a5b066c`.
2. Create a `STOP_SHIP` gate state and `SS-*` record linked to the exact candidate,
   affected cohorts, raw evidence, board card, and source criterion.
3. Freeze promotion and dependent dispatch. Calendar, aggregate health, unrelated
   green checks, sunk cost, or partial owner approval cannot waive the stop.
4. Preserve evidence and authoritative state. Do not destructively restore or
   rewrite financial, economy, inventory, progression, Realm Gem, Wish, or other
   owner-reserved state.
5. Name containment owner, investigation owner, evidence owner, rollback executor,
   communication owner, and owner escalation. Unknown ownership fails closed.
6. Resume only after repair, new evidence, independent review, all reopen criteria,
   and the exact source-required owner decision. A closed incident alone is not a
   gate pass.

An unauthorized roadmap or artifact change is itself stop-ship for the affected
scope. The recorder quarantines the candidate, logs the attempted change and
source conflict, and routes it to the game owner without incorporating it into the
approved baseline.

## 12. Baseline retention and rollback

### 12.1 Promotion is append-only

An approved baseline remains immutable and addressable by baseline ID, Git commit,
board approval event, DAG export/hash when available, authority-register version,
and governance-control version. A candidate never overwrites the current approved
baseline. Promotion occurs only after the candidate receives its required explicit
approval; then a new baseline manifest points to both the new approved content and
its parent baseline.

### 12.2 Reverting an unapproved revision

Rollback scope is the exact unapproved candidate change set only:

1. Freeze the candidate and record `REJECT`, `REVISE`, `FAIL-CLOSED`, or
   `STOP_SHIP` with the cause.
2. Identify its parent approved baseline and prove that baseline's approval record
   and evidence remain valid.
3. For repository changes, use a scoped revert PR for only the candidate commits or
   abandon/close the unmerged candidate PR. Never force-reset `main`, rewrite
   history, or revert unrelated approved work.
4. For Kanban changes, preserve append-only history; record inverse dependency or
   status corrections as new events and link them to the rollback record. Never
   delete the prior owner decision or mislabel the rejected candidate as approved.
5. For runtime/config/content exposure, perform only the reversible action
   authorized by `t_4a5b066c`. Store rollout pause is exposure containment, not
   proof of client or data rollback.
6. Verify the retained baseline identity, source access, gate state, candidate
   isolation, and evidence accessibility. Record the verification in `RR-*`.
7. Reopen downstream gates whose evidence depended on the rejected candidate.
   Unaffected approvals remain intact.

If the prior approved baseline is no longer safe, compatible, or verifiable, do not
call restoration successful. Keep exposure contained, mark the gate fail-closed,
and return to the source-defined incident and owner decision process.

## 13. Reopen and change control

A gate reopens when any profile trigger, controlling source trigger, expired or
inaccessible evidence, candidate drift, material dependency change, conflict,
incident, or explicit owner revision applies. Reopening:

1. creates a new gate-record revision;
2. preserves the prior approval as historical, not current permission;
3. identifies affected downstream gates and evidence packets;
4. blocks only affected promotion while preserving unaffected approved baselines;
5. requires new evidence and approval for the changed scope; and
6. reopens `t_0648ce23` for every material roadmap change covered by that gate.

Changes to this control require a version increment, source and authority review,
an impact list, and a migration note for open gate records. No editor may weaken a
mandatory field, convert owner authority to agent authority, merge 3D and 2.5D
approval, or introduce a numerical substitute for `t_4a5b066c` or `t_7f6be100`.
Such a change fails closed automatically.

## 14. Gate recorder checklist

- [ ] Every artifact instance has one named accountable owner and one canonical location.
- [ ] Candidate, baseline, gate, evidence, approval, incident, and rollback identities are exact.
- [ ] Entry criteria are source-resolved and complete.
- [ ] Numerical criteria point to `t_4a5b066c` or `t_7f6be100` without transcription or modification.
- [ ] Evidence is retained, hashed, accessible, reproducible, and mapped to criteria.
- [ ] Independent review is genuinely separate from implementation.
- [ ] Approver authority is verified from the controlling source.
- [ ] Required 3D and 2.5D packets and owner decisions are separate.
- [ ] Stop-ship conditions, rollback target/action, and reopen triggers are recorded.
- [ ] Owner-exclusive decisions and destructive-restore boundaries are preserved.
- [ ] The prior approved baseline is retained and only the unapproved candidate is reverted.
- [ ] Ambiguity, conflict, missing evidence, stale identity, or unauthorized change is `FAIL-CLOSED`.
