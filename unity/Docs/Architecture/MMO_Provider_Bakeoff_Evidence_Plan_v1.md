# MMO Provider Bake-off Evidence Plan v1

Plan ID: `MMO-BAKEOFF-v1.0.0`

Status: approved common evidence definition; no candidate has been selected or measured by this plan

Consumes: `MMO-BL-20260831-001`, `MMO-CONTRACTS-v1.0.0`, and ADRs `0001` through `0004`

Candidate spikes: Amazon GameLift task `t_ff702849` and Microsoft PlayFab task `t_27759e01`

Decision owner: game owner for provider, managed/custom boundary, spend, commitment, exposure, residual risk, and selection or no-selection

Machine-readable authority: `MMO_Provider_Bakeoff_Scenarios_v1.json`

Run record starter: `Templates/MMO_Provider_Spike_Run_Record_v1.json`

Runbook starter: `Templates/MMO_Provider_Bakeoff_Runbooks_v1.md`

## 1. Scope and claim discipline

This package defines one workload, observation, failure, and rollback protocol for both candidate spikes. It does not select a winner, authorize production use, approve spend, or claim production capacity. Candidate-specific API calls remain inside disposable adapters; the provider-neutral contracts and test meaning remain fixed.

Every statement produced by a spike is classified as one of:

- `requirement`: an approved behavior or topology the candidate must support;
- `observed_sandbox_fact`: a result tied to one immutable run packet;
- `vendor_documented_limit`: a dated vendor statement retained with source and scope;
- `measured_limit`: a boundary directly reached by the common driver;
- `unknown_measurement_required`: a quota, regional property, failure behavior, or operational limit that has not been proved;
- `unproven_scale_target`: the 10,000 steady and 20,000 surge CCU targets from the foundation baseline, never an achieved result of a functional sandbox run.

A result without an artifact fingerprint, configuration hash, scenario ID, raw evidence handle, timestamp, and limitation is not comparison evidence. Marketing claims, quoted maximums, account-population requirements, and provider dashboards alone are not measured capacity.

## 2. Data classification and residency map

The owning domain decides authority. Hosting, placement, identity, telemetry, or control-plane convenience cannot change it.

| Class | Data and examples | Sole authority | Residency and replication | Candidate proof |
| --- | --- | --- | --- | --- |
| `D-GID-01` minimized global identity | canonical account ID; opaque platform-link, eligibility, restriction, consent, owning-region, and durable-realm references | identity domain | global only for the enumerated references; no gameplay, economy, social, communications, raw assertion, receipt, or secret payload | inspect stored/exported fields; deny adapter attempts to add regional aggregates; prove provider IDs remain opaque mappings |
| `D-ENT-01` minimized pre-grant entitlement reference | opaque evidence reference, owning region, reconciliation state | identity/platform reconciliation boundary; not value authority | global reference MAY route reconciliation; granted/reversed value and ledger history remain regional | reconcile an opaque reference into the regional economy ledger; prove duplicate evidence cannot grant twice and global loss cannot rewrite the grant |
| `D-SES-01` session and route | short-lived session, admission, route hint, fenced lease | regional gateway/session and placement controllers | regional and expiring; a minimized non-authoritative owning-region hint may be global | create, reconnect, expire, and reject a stale lease without changing realm membership |
| `D-PLY-01` authoritative gameplay | character, progression, inventory, kingdom, territory, objective, reward, active checkpoint and outcome | regional persistence plus the one active simulation owner | owning region only; asynchronous public projections are read-only and minimized | attempt cross-region write/placement; inspect provider stores and exports; verify one writer through outage and restart |
| `D-ECO-01` authoritative economy | currency, market, trade, inventory value, settlement, granted entitlements, outbox and dedupe | regional transactional economy ledger | owning region only, including backups and audit; no writable global mirror | duplicate, ambiguity, outage, and cross-region tests prove conservation and one commit |
| `D-SOC-01` authoritative social | guild/alliance membership, roles, moderation cases, sanctions, regional communication state | regional social domain | owning region only; transport receipts and privacy-approved projections are non-authoritative | provider outage may degrade transport but cannot change membership, sanctions, evidence, or realm boundaries |
| `D-BAK-01` backup and recovery | regional snapshots, logs needed for restore, operation/outbox position | regional recovery domain | same approved region boundary as source; no automatic cross-region copy | inventory backup locations and restore metadata; prove restored owner, schema, dedupe, and outbox position before write exposure |
| `D-AUD-01` security and value audit | actor, authorization reference, payload hash, versions, operation/correlation identity | producing regional domain and append-only audit boundary | region/privacy scoped; only approved minimized summaries leave region | verify append-only evidence, access scope, export/redaction, and no secrets or private payloads |
| `D-TEL-01` sanitized telemetry | low-cardinality metrics, traces, health, crash/evidence handles | producing service until accepted by bounded observation pipeline | region/privacy scoped by default; approved aggregate export only; never authority | drop, delay, duplicate, reorder, and export telemetry without blocking ticks or leaking high-cardinality identity |
| `D-PRV-01` provider-private metadata | allocation/resource IDs, SDK errors, endpoint and credential handles, provider receipts | disposable candidate adapter | adapter-private and environment/region scoped; exported only as sanitized evidence handles | source/dependency scan, log scan, teardown inventory, and adapter-removal proof |

### 2.1 Prohibited data paths

The following fail the candidate regardless of convenience or feature breadth:

1. global writable copies of `D-PLY-01`, `D-ECO-01`, `D-SOC-01`, `D-BAK-01`, or `D-AUD-01`;
2. provider account, allocation, realm, guild, inventory, or session records becoming canonical domain identity;
3. a placement or global control plane choosing durable region or realm;
4. synchronous global consensus, provider API, database, telemetry, or object storage in the movement/combat tick;
5. direct dual-writes to regional authority and provider storage;
6. credentials, raw assertions, receipts, private communications, or personal data in metrics, traces, evidence filenames, or provider-neutral errors;
7. backup, diagnostics, or support exports silently crossing the approved region boundary;
8. restoring or failing over into two writable regional owners.

### 2.2 Residency evidence procedure

For each candidate and scenario, the worker must capture:

- configured resource region and every discovered control-plane, data-plane, log, backup, support, export, and credential-processing location;
- provider documentation URL, retrieval UTC, quoted scope, and whether the statement is account-, service-, feature-, or region-specific;
- synthetic canary records unique to each data class, then storage/export searches showing every observed copy;
- denied cross-region writes and placements with stable failure translation;
- teardown inventory proving no provider copy is authoritative or required by the neutral path;
- `unknown_measurement_required` whenever location, replication, support access, deletion, retention, or restore behavior cannot be observed.

No real player, commerce, social, or production data may enter either spike.

## 3. Threat and failure model

Likelihood and business impact are intentionally not scored here; no owner-approved risk scale exists. Each threat is a hard evidence dimension.

| Threat | Failure mechanism | Required injection or inspection | Pass interpretation | Fail/rollback interpretation |
| --- | --- | --- | --- | --- |
| `TM-01` provider lock-in | SDK types, resource IDs, identity, lifecycle, data shape, or teardown constraints leak into the core | dependency/source scan; export inventory; adapter disable/delete; neutral-core test rerun | core compiles/runs without adapter and durable state needs no rewrite | contract violation; disable adapter, export evidence, revoke credentials, delete sandbox resources, record no-selection dimension |
| `TM-02` regional-data leakage | logs, backups, support/export paths, global indexes, or defaults copy regional authority | class canaries, region inventory, log/export scan, denied cross-region attempt | every copy is allowed by the map and authoritative classes remain regional | stop run, contain/delete synthetic data, revoke access, preserve sanitized evidence, mark hard failure or unknown |
| `TM-03` hidden global-state coupling | login, placement, tick, persistence, or recovery synchronously depends on a global plane | isolate global/control plane while established regional process continues | established authority remains single-writer and bounded; only approved new-session behavior blocks/degrades | stop exposure; restore neutral routing; reconcile operations and owner epochs |
| `TM-04` incompatible authority model | provider allocation/account/session becomes realm, identity, gameplay, economy, or social authority | duplicate owner, stale/future epoch, altered operation payload, provider-side state drift | neutral domain rejects drift and provider cannot commit a gameplay/value result | freeze affected mutation path; reconcile regional source of truth; remove adapter if seam cannot hold |
| `TM-05` quota surprise | undocumented/default/plan-specific rate, capacity, artifact, region, log, API, or account limit | preflight inventory; symmetric request ladder; forced throttle; recovery observation | limit and recovery signal are measured or explicitly unknown; stable `throttled` behavior and bounded admission occur | no extrapolation; stop ladder; drain load; preserve limit evidence; no-selection if required equivalence is impossible |
| `TM-06` failed-region control-plane dependence | region-local data plane cannot continue, reconcile, drain, or recover when provider control plane fails | approved fault seam or documented blocker; established-session and new-placement paths separated | one established owner remains; new work fails/degrades explicitly; no cross-region implicit placement | isolate region, stop new placement, preserve owner epoch, invoke outage runbook, record blocker if injection is unavailable |
| `TM-07` credential compromise or rotation | broad, long-lived, logged, stale, or unrecoverable credentials permit unintended actions | least-privilege inventory; log scan; rotate and revoke under activity; negative old-credential test | new credential works only in scope; old credential fails; gameplay state and operation identity remain unchanged | disable adapter, revoke all candidate credentials, isolate resources, inspect audit, restore neutral path |
| `TM-08` prototype assumption escapes | sandbox defaults, fixtures, local endpoints, permissive roles, manual steps, or fake scale become production contract/config | config/source scan; environment guard; evidence and claim review; teardown | adapter is explicitly non-production, disposable, reproducible, and carries no production claim/default | block promotion, delete resources, revert provider experiment, retain only sanitized evidence |

Threat closure requires raw evidence or an explicit blocker. `Not tested`, unavailable fault injection, unknown data location, or inaccessible quota information never counts as pass.

## 4. Equivalent workload protocol

The JSON manifest is normative for scenario IDs and exact functional-workload counts. Both workers must use the same versioned driver, server artifact, synthetic fixture seed, four-realm topology, request envelope, operation IDs, payload bytes, observation schema, repetitions, warm-up rule, fault schedule, and teardown checks.

### 4.1 Functional equivalence profile

`WF-FUNCTIONAL-v1` is deliberately small and is not scale evidence:

- four synthetic realms;
- two logical region slots: `home_region` and `forbidden_region`; all four realms are preassigned to `home_region`, and every realm is probed against `forbidden_region` without reassignment; each candidate maps those logical slots to its approved sandbox regions during `SCN-01`;
- eight synthetic accounts per realm;
- two placement/lifecycle cycles per account;
- one identity/session normalization cycle per account;
- one region-local gameplay, economy, and social canary per account through neutral test seams;
- three repetitions per scenario after one excluded warm-up where the scenario permits repetition;
- fixed deterministic seed `anotherlife-mmo-bakeoff-v1`.

Changing a count, payload, topology, fault point, or observation field creates a new workload ID and requires both candidates to rerun. Candidate convenience cannot redefine the common workload.

### 4.2 Quota/capacity discovery profile

`WF-QUOTA-LADDER-v1` begins only after both preflight inventories are complete. It tests concurrent neutral operations, placement requests, lifecycle requests, and administrative requests separately. Each dimension starts at one unit, doubles for at most 12 steps, uses a 60-second measurement window, and probes recovery every five seconds. The common ceiling is the lower of the two documented or measured authorized sandbox boundaries. A dimension with an unknown boundary stops after its functional baseline and is blocked without extrapolation. One increment beyond the last common accepted step is attempted only when both candidates are authorized. Each step records attempted, accepted, pending, succeeded, duplicate, reconciled, rejected, throttled, unavailable, ambiguous, and cancelled counts plus provider and external measurements.

The ladder stops on throttle, unavailable service, safety/budget guard, sandbox limit, or missing authorization. A candidate-specific maximum may be recorded as an observation but is not directly comparable unless the other candidate was allowed the same workload. An unknown or inaccessible boundary remains `unknown_measurement_required`. Neither ladder proves the baseline CCU targets.

### 4.3 Scenario set

The machine manifest defines these mandatory cases:

- `SCN-01` preflight, region, quota, credential, and resource inventory;
- `SCN-02` clean provision, artifact verification, readiness, placement, drain, and retire;
- `SCN-03` identity normalization, session, region, and durable-realm preservation;
- `SCN-04` duplicate operation with identical payload;
- `SCN-05` reused operation ID with changed payload or scope;
- `SCN-06` ambiguous completion and reconciliation before retry;
- `SCN-07` quota/throttle ladder and delayed recovery signal;
- `SCN-08` provider/control-plane outage with established regional ownership;
- `SCN-09` failed launch, partial lifecycle, stuck drain, and cancellation;
- `SCN-10` cross-region placement/write, data copy, backup, log, and export denial;
- `SCN-11` stale/future lease epoch and duplicate active owner rejection;
- `SCN-12` adapter restart with retained operation reconciliation;
- `SCN-13` credential rotation, old-credential rejection, and compromise containment;
- `SCN-14` telemetry loss, pressure, duplication, reorder, redaction, and export;
- `SCN-15` adapter disable/removal and neutral-path restoration;
- `SCN-16` teardown, data export/deletion, residual-resource, and lock-in inventory.

A scenario is `pass`, `fail`, `blocked`, or `not_run`; there is no partial pass. A blocker must name the missing access, quota, API, fault seam, evidence, or owner decision and its effect on comparability.

## 5. Instrumentation and evidence capture

### 5.1 Required independent observations

Provider dashboards are supporting evidence, not sole truth. The common driver must record:

- UTC and monotonic start/end timestamps and duration;
- scenario, attempt, operation, correlation, contract, region, realm, artifact, adapter, schema, configuration, and compatibility fingerprints;
- lifecycle transitions and stable result/failure classes;
- attempted, accepted, pending, succeeded, duplicate, reconciled, rejected, throttled, unavailable, ambiguous, and cancelled counts;
- established and attempted ownership epochs and route/lease state;
- process health, CPU, memory, network bytes, queue depth, and provider-reported capacity where obtainable;
- data-class canary and region-copy inventory;
- credential reference/version, never credential material;
- raw stdout/stderr, sanitized logs/traces/metrics, provider event/export handles, screenshots only when no machine export exists, and exact collection command;
- setup, operator actions, manual steps, wait time, retries, teardown actions, limitations, blockers, and contract violations.

Metrics must not use account, character, item, message, receipt, assertion, credential, endpoint, or provider resource IDs as labels. Raw restricted diagnostics remain access-controlled and are referenced by sanitized handles.

### 5.2 Packet layout

Each spike stores one immutable packet using this logical layout:

```text
evidence/<candidate>/<run-id>/
  run-record.json
  workload-manifest.json
  environment.txt
  commands.txt
  raw/
  logs/
  metrics/
  traces/
  provider-exports/
  residency-inventory.json
  quota-inventory.json
  teardown-inventory.json
  limitations.md
```

`run-record.json` uses the provided starter. Every file gets a SHA-256 entry in the run record after capture. Secrets and personal data are prohibited. If evidence cannot safely be retained, the scenario is blocked or failed rather than summarized from memory.

Validate the common package before a run with `python tools/architecture/validate_mmo_bakeoff_plan.py .`. After filling one copied record, validate its evidence contract with `python tools/architecture/validate_mmo_bakeoff_plan.py . --record evidence/<candidate>/<run-id>/run-record.json`. The `workload_manifest_hash` must identify exactly one retained JSON artifact whose keys exactly match `common_driver.required_equal_inputs`; the validator binds its driver commit, neutral server artifact, and fixture seed back to the run record. Validate a comparison by supplying `--record` twice, once per candidate; the validator then requires both candidate IDs and the same artifact-bound workload manifest plus equivalent plan, driver commit, neutral server artifact, fixture seed, and realm IDs. A `completed` run may contain only passed scenarios, no top-level blocker or contract violation, and a passed aggregate rollback. A `blocked` run must carry blocked scenarios, a top-level named blocker, and explicit rollback disposition. No filled record may retain `not_run` scenarios.

Every evidence handle is a nonblank relative path beneath the run packet and has exactly one `raw_evidence_manifest` entry with `path`, lowercase 64-character `sha256`, non-negative `bytes`, `content_type`, `classification` (`sanitized_common` or `restricted_adapter_diagnostic`), and covered `scenario_ids`. The completed-record validator rejects absolute/traversal paths, symlink or junction escapes after resolution, duplicate paths, missing files, byte/hash mismatches, blank handles, and handles not present in the manifest. Commit identities must be full Git object IDs; artifact, configuration, workload, and rollback fingerprints must be SHA-256. UTC timestamps must be RFC 3339 `Z` values with end not before start.

For a `pass`, operation and correlation identities, substantive stable result counts, measurement handles, raw evidence, and rollback evidence are mandatory; blockers and contract violations must be empty. A `fail` requires raw evidence plus a named limitation or contract violation. A `blocked` result requires at least one nonblank named blocker and cannot masquerade as a pass. Aggregate run status must agree with scenario statuses, and all inventory, secret-scan, rollback, and teardown handles must resolve through the evidence manifest. A completed rollback must restore identical neutral-configuration and regional-state SHA-256 values and report `core_tests: pass`; failed or blocked dispositions remain explicit rather than being promoted to completion.

### 5.3 Repeatability gate

A second worker must be able to start from a clean sandbox account/project, follow the recorded commands and runbooks, obtain the same artifact/configuration/workload fingerprints, and classify every scenario. Manual console actions require numbered steps and before/after export. Unrecorded console state is a blocker.

## 6. Pass/fail and comparison interpretation

Hard gates cannot be averaged away. A candidate fails a hard gate if it:

- violates provider-neutral gameplay/domain authority;
- creates a global writable gameplay, economy, social, backup, or audit copy;
- cannot prove one regional writer through the tested failure;
- requires secrets or provider SDK/resource types in the core;
- cannot reconcile ambiguous value or lifecycle completion;
- cannot rotate/revoke credentials without state rewrite;
- cannot be disabled/removed while restoring the neutral path;
- leaks real or prohibited data;
- requires production exposure, spend, commitment, or an owner decision not granted to the spike.

A scenario passes only when every manifest pass condition and rollback verification has evidence. Missing evidence is `blocked`, not pass. Different workload, artifact, region posture, observation schema, fault point, or sandbox entitlement makes that dimension incomparable until rerun.

Allowed final dispositions after both packets are independently reviewed:

1. `select_amazon_gamelift` — owner decision only, after GameLift passes every required hard gate and comparison evidence is complete;
2. `select_microsoft_playfab` — owner decision only, after PlayFab passes every required hard gate and comparison evidence is complete;
3. `no_selection` — valid when both fail, either violates a hard gate, evidence is materially incomparable, required regional/quota/exit behavior remains unknown, or neither offers acceptable owner-plus-AI operation/exit posture;
4. `blocked_pending_evidence` — temporary only when a named access, measurement, or owner gate can resolve the comparison without changing the contract.

No candidate wins by feature count, quoted scale, lower unapproved price, more generous unknown quota, or the other candidate being blocked. The later comparison task records the owner decision; spike workers do not recommend a winner.

## 7. Rollback contract

Before a candidate is enabled, capture the neutral adapter configuration, artifact/configuration hashes, synthetic regional-state hash, active operation inventory, credential references, and provider resource inventory.

Every scenario rollback must:

1. stop new candidate placements/mutations;
2. preserve or drain the current single owner according to the scenario;
3. reconcile ambiguous operations and ownership epochs;
4. switch configuration to the neutral adapter/path without changing domain contracts;
5. rerun core authority, idempotency, regional-isolation, and state-hash checks;
6. rotate/revoke candidate credentials;
7. export allowed evidence and delete candidate resources/synthetic data;
8. capture residual-resource and deletion limitations;
9. leave no provider resource ID as canonical identity and no provider process as authority.

A rollback that requires gameplay-state rewrite, realm remap, ledger repair from provider state, or deletion of audit history fails `SCN-15` and the exit hard gate.

## 8. Runbook use

The dedicated template contains four required runbooks:

- `RB-OUTAGE-01` provider/control-plane outage;
- `RB-QUOTA-01` quota exhaustion;
- `RB-CREDENTIAL-01` credential compromise or planned rotation;
- `RB-REVERT-01` revert a provider-specific experiment.

The spike worker copies the template into its evidence packet, fills candidate-specific commands and resource scopes before fault injection, and records timestamps, actor, operation IDs, evidence handles, and outcome during execution. Placeholder or untested commands remain explicit blockers.

## 9. Unknown vendor limits to measure

Both candidates must inventory and attempt to measure, without guessing:

- enabled regions and feature-specific data/control/log/backup/support locations;
- account/project/fleet/build/title/server/queue/concurrent-process limits;
- placement, lifecycle, identity, telemetry, export, and administrative API rate limits;
- burst windows, retry hints, recovery/reset timing, queue limits, and backpressure visibility;
- artifact size/count/version, deployment, drain, shutdown, startup, and update limits;
- credential issuance, overlap, propagation, revocation, audit, and role-scope limits;
- log/metric/trace retention, delay, loss, cardinality, export, and deletion limits;
- sandbox/trial expiration, feature parity, billing activation, and fault-injection restrictions;
- data export, deletion, residual-resource, backup, restore, and account-closure behavior.

Each item is either `vendor_documented_limit`, `measured_limit`, or `unknown_measurement_required`, with source/run evidence and scope. Unknown does not become unlimited or zero.

## 10. Acceptance traceability

| Acceptance criterion | Evidence in this package |
| --- | --- |
| Materially equivalent candidate workloads and observations | Sections 4 and 5 plus the exact JSON workload, scenario, and evidence-field definitions |
| Region-local gameplay, economy, and social authority with minimized global identity/entitlement | Section 2 data map, prohibited paths, canary procedure, and `SCN-03`/`SCN-10` |
| Required threat/failure coverage | Section 3 and JSON threats cover lock-in, leakage, global coupling, authority mismatch, quotas, control-plane loss, credentials, and prototype escape |
| Requirements separated from scale claims | Section 1 claim classes and functional/quota profile boundaries |
| Unknown quotas and vendor limits measured rather than assumed | Section 9, `SCN-01`, `SCN-07`, and `unknown_measurement_required` |
| Repeatable evidence and rollback | Section 5 packet/repeatability gate, Section 7, run record, and four runbooks |
| Valid no-selection outcome | Section 6 hard gates and allowed dispositions |
| No winner preselected | Both candidates receive the same manifest; selection authority remains with the owner after both packets and comparison review |
