# Provider-Neutral MMO Architecture Contracts v1

Contract ID: `MMO-CONTRACTS-v1.0.0`

Status: approved technical baseline for reversible implementation; owner-reserved decisions remain unresolved where listed below

Baseline consumed: `MMO-BL-20260831-001`

Authority consumed: `GOV-G0-v1.0.0`, `RB-20260828-002`, and the immutable authority rows cited by each contract

## 1. Scope and normative language

This document defines the stable boundaries consumed by the identity, placement,
persistence, simulation, social, economy, platform, capacity, deployment,
security, and operations epics. `MUST`, `MUST NOT`, `SHOULD`, and `MAY` are
normative.

It does not select a hosting, identity, database, cache, event, observability,
content-delivery, or platform provider. It does not set a price, cost ceiling,
latency threshold, service objective, recovery objective, device tier, quota, or
release threshold. Those values remain at their approved evidence and owner gates.

The approved topology and population inputs remain planning requirements, not
capacity claims: one regional server models four realms, the active-account
population requirements remain those in `MMO-BL-20260831-001`, and the steady and
surge concurrency figures in that baseline remain unproven validation targets.

## 2. Invariants shared by every contract

1. The authoritative simulation and domain services own gameplay decisions.
   External providers supply capabilities; they never decide combat, movement,
   rewards, progression, economy settlement, realm membership, social membership,
   objectives, or game time.
2. A durable regional system of record owns transactional gameplay, economy, and
   social state. Active simulation owns hot state while active. A cache, queue,
   analytics store, object store, client, or provider control plane cannot become
   an accidental second authority.
3. Global state is minimized to canonical identity, platform links, eligibility,
   and entitlement references. Authoritative gameplay, economy, social, realm,
   character, and backup state stays in its owning region (`REG-02`).
4. Every mutating call carries a stable `operation_id`, authenticated actor and
   service identity, authorization context, region and realm scope where
   applicable, schema version, correlation identity, and an explicit compatibility
   fingerprint. Retries preserve `operation_id` and the canonical payload.
5. Reusing an `operation_id` with a different canonical payload is a conflict and
   fails closed. A duplicate with the same payload returns the original result or
   a reconciliation handle; it never repeats a value mutation.
6. An ambiguous timeout or lost response is not a failure proof. The caller
   reconciles by `operation_id` before retrying a value, ownership, or lifecycle
   mutation.
7. Queries may be retried when they are side-effect free. Mutations may be retried
   only when their contract states `same-operation retry`. Backoff duration is
   configuration or provider signal, never a gameplay constant.
8. Every boundary exposes sanitized logs, metrics, and trace correlation. No hook
   may emit credentials, assertions, receipts, private communications, raw payment
   data, or unnecessary personal data.
9. Queues are bounded. The producer sees acceptance, backpressure, expiry, or a
   stable rejection. No external dependency may grow unbounded tick debt or block
   an authoritative simulation tick.
10. Compatibility is explicit and expand-contract. An incompatible reader, writer,
    catalog, schema, protocol, or server artifact fails before mutation and leaves
    the last compatible state intact.
11. Provider errors are translated into the stable failure vocabulary below.
    Gameplay and domain code MUST NOT branch on provider error codes or SDK types.
12. Adapter removal restores the provider-neutral path without moving durable realm
    identity, rewriting gameplay state, or discarding audit history.

## 3. Stable request, result, and failure vocabulary

### 3.1 Common request envelope

| Field | Contract |
| --- | --- |
| `contract_id` | Typed identity from the contract register governing the call. |
| `operation_id` | Non-zero stable identity for one logical mutation; unchanged across retries. |
| `correlation_id` | Sanitized cross-service trace/log identity; not an idempotency key. |
| `actor_id` | Canonical account or approved operator identity; never a raw platform identity. |
| `service_id` | Authenticated least-privilege calling service identity. |
| `authorization_context_id` | Reference to an authorization decision made before adapter invocation; not an adapter grant. |
| `policy_version` | Version of the policy used by that authorization decision. |
| `region_id` | Mandatory approved regional data-plane scope for the adapter call. |
| `realm_id` | Durable realm scope where applicable; never inferred from a provider allocation. |
| `schema_version` | Version of the provider-neutral request/response contract. |
| `artifact_fingerprint` | Immutable server artifact and configuration tuple observed for the call. |
| `compatibility_fingerprint` | Immutable build, catalog, protocol, and configuration tuple required by the operation. |
| `attempt` | Diagnostic attempt count. It may increase; it never changes operation identity. |

External tokens, receipts, credentials, assertions, and platform payloads cross
only opaque adapter handles. Simulation and domain code receive normalized,
provider-neutral claims.

### 3.2 Stable failure classes

| Class | Meaning | Retry contract |
| --- | --- | --- |
| `invalid_request` | Provider-neutral request failed validation. | Never retry unchanged. |
| `unauthorized` | Authentication or authorization failed. | Never retry without new authorization evidence. |
| `conflict` | Fencing, version, ownership, or operation-payload conflict. | Reconcile current state; do not blind-retry. |
| `throttled` | External boundary rejected work due to a quota or rate guard. | After explicit provider/capacity signal. |
| `unavailable` | Dependency cannot currently serve the request. | After explicit health signal. |
| `ambiguous_completion` | Response was lost and commit status is unknown. | Reconcile by `operation_id` first. |
| `unsupported` | Adapter does not implement a required capability. | No retry; fail the capability gate or use another adapter. |
| `internal` | Adapter translation or invariant failed. | After explicit operator signal. |

Provider messages are retained only in access-controlled adapter diagnostics.
Stable callers receive the class, retry disposition, provider-neutral operation
identity, and a sanitized diagnostic code.

### 3.3 Observability hook

Every call emits a low-cardinality lifecycle observation:

`started | duplicate | pending | succeeded | failed(class)`

Required dimensions are contract ID, adapter boundary, operation/correlation
identity, region, realm where allowed, schema version, artifact fingerprint, and
result class. Metrics aggregate by stable dimensions. Account, character, item,
chat, receipt, assertion, and endpoint identities MUST NOT become metric labels.
Audit events add authenticated actor, authorization decision, canonical payload
hash, previous/new version, and evidence retention class where the domain requires
it.

## 4. State ownership matrix

| State | Sole authority while active | Durable owner | Forbidden authorities |
| --- | --- | --- | --- |
| Canonical account ID and platform-link graph | identity domain | minimized global identity store | platform identity, client, placement provider |
| Durable realm membership | identity/realm domain | region-linked durable store | placement result, session provider, client |
| Session authentication and admission | gateway/session domain | short-lived session record plus audit | client, cache-only record |
| Active placement route and lease | regional placement controller | fenced lease journal; reconstructible route projection | provider allocation alone, cache without fencing |
| Movement, combat, AI, objectives, active territory | one authoritative simulation owner and epoch | versioned checkpoint plus idempotent committed outcomes where required | client, ghost, provider, database tick loop |
| Character, progression, inventory, kingdom | region-local persistence domain | regional transactional store | client, cache, event bus, global identity store |
| Currency, market, trade, entitlements after grant | region-local economy ledger | regional transactional store and outbox | client, platform receipt, cache, analytics |
| Guild/alliance/social graph and moderation cases | region-local social domain | regional transactional store | global replica, cache-only projection, provider membership model |
| Presence, route hints, query projections | owning service | reconstructible cache/projection | sole copy of durable state |
| Build/catalog/release identity | deployment/release domain | signed immutable manifest and audit record | mutable provider label or client claim |
| Telemetry, replay, crash bundles | producing domain until accepted | bounded telemetry/object pipeline | gameplay authority or transactional ledger |

A write crossing owners uses a command plus idempotent outcome or an explicit
reservation/commit state machine. Direct dual-writes are prohibited.

## 5. Contract register and named consumers

| Contract | Named consuming epic | Authority rows | Stable boundary |
| --- | --- | --- | --- |
| `C-IDN-01` Identity and session | `t_927d3dd9` | `PLAT-01`, `PLAT-02`, `REG-02` | `ExternalIdentityAdapter`, canonical account/session APIs |
| `C-PLC-01` Placement and topology | `t_a8960ec8` | `REG-02`, `REG-03`, `CAP-01`, `U-10` | `PlacementAdapter`, route/lease APIs |
| `C-PER-01` Persistence and recovery | `t_22898962` | `REG-02`, `GOV-02`, `REL-01` | aggregate repositories, outbox/inbox, recovery APIs |
| `C-SIM-01` Authoritative simulation | `t_7d1036e8` | `AUTO-01`, `WORLD-01`, `WORLD-02`, `BAL-01` through `BAL-06`, `PROG-01` through `PROG-05` | command/snapshot/outcome APIs; `al_server_core` |
| `C-SOC-01` Social and moderation | `t_db8f937f` | `GUILD-01`, `GUILD-02`, `GUILD-03`, `REG-02` | region-local social graph, channel, case, sanction APIs |
| `C-ECO-01` Economy and commerce | `t_00f0f879` | `ECON-01`, `ECON-02`, `MON-01`, applicable `BAL-*` | ledger, inventory, market, entitlement settlement APIs |
| `C-PLT-01` Platform integration | `t_4f3f4535` | `PLAT-01`, `PLAT-03` through `PLAT-06`, `ACC-01`, `LOC-01` | `PlatformAdapter`, client compatibility/content APIs |
| `C-CAP-01` Capacity and qualification | `t_d4d26ddf` | `CAP-01`, `REG-03`, `U-02` through `U-05` | measured workload/observation and admission evidence APIs |
| `C-DEP-01` Deployment and lifecycle | `t_28c37145` | `REL-01`, `GOV-02`, `REG-02` | `DeploymentAdapter`, signed artifact promotion APIs |
| `C-SEC-01` Security assurance | `t_30eadba6` | `GOV-02`, `PLAT-01`, `REG-02`, `COMP-01` | authentication, authorization, audit, containment APIs |
| `C-OPS-01` Operations and release | `t_28c37145`, `t_aa3849be` | `REL-01`, `LIVE-01`, `GOV-01`, `GOV-02` | `OperationsAdapter`, health/evidence/rollback APIs |

Shared foundation consumers `t_1cfdd495` and `t_c6e9368a` consume the entire
register for DTOs, schemas, migrations, client/server compatibility, and catalog-
driven gameplay. The provider spikes `t_ff702849` and `t_27759e01` implement only
adapter boundaries and must execute identical provider-neutral scenarios.

## 6. Domain contracts

### 6.1 `C-IDN-01` — identity and session

Stable operations:

- `VerifyExternalAssertion(opaque_assertion) -> canonical_account_id`
- `LinkPlatformIdentity(operation_id, canonical_account_id, opaque_assertion)`
- `ReconcilePlatformLink(operation_id)`
- `IssueSession(canonical_account_id, approved_region, client_fingerprint)`
- `RevokeSession(operation_id, session_id, reason_class)`
- `ResolveRealmMembership(canonical_account_id) -> durable_realm_id`

Ownership and behavior:

- Canonical account and durable realm membership are independent of platform and
  provider identities. Gameplay records store opaque canonical IDs.
- The global identity plane stores only the minimized fields approved by `REG-02`.
  It may route to an owning region but cannot read or mutate regional gameplay,
  economy, or social aggregates.
- Realm membership cannot be reassigned by login, placement, reconnect, provider
  failover, account linking, or platform reconciliation through version 1.0.
- Linking, unlinking, recovery, entitlement evidence, and revocation are audited
  and idempotent. Ambiguous link completion is reconciled before another mutation.
- Identity-provider outage blocks new verification or uses only an explicitly
  approved, still-valid session path. It never fabricates identity or changes realm.

### 6.2 `C-PLC-01` — placement, routing, and regional topology

Stable operations:

- `RequestPlacement(operation_id, account_id, session_id, region_id, realm_id, artifact_fingerprint) -> placement_receipt`
- `GetPlacement(placement_receipt) -> pending | ready(fenced_lease, endpoint_handle) | cancelled | failed`
- `CancelPlacement(operation_id, placement_receipt)`
- `ResolveSessionRoute(session_id, lease_epoch)`
- `BeginReconnect(operation_id, session_id, prior_lease_epoch)`
- `DrainPlacementGeneration(operation_id, region_id, artifact_fingerprint)`

Ownership and behavior:

- Regional placement owns only process/session routing and fenced leases. Durable
  account/realm identity remains in identity/persistence.
- A placement request includes the already-authoritative region and realm; an
  adapter cannot choose or migrate either. Neutral ring-slot identifiers remain
  unresolved under `U-10` and are never randomized by placement.
- Exactly one active session route and one authoritative simulation owner are
  writable for an allocation epoch. Stale/future epochs fail separately.
- Duplicate requests return the original receipt. Payload drift under one
  operation ID is a conflict. Lost responses reconcile by receipt/operation ID.
- Control-plane loss preserves established data-plane ownership until the bounded
  lease/recovery policy says otherwise; it does not create a second owner.
- Failed launch, over-capacity, stale lease, region isolation, and drain failure are
  explicit states. No adapter silently places across regions or invents a realm.

### 6.3 `C-PER-01` — region-local persistence, outbox, and recovery

Stable operations:

- `LoadAggregate(region, aggregate_id, accepted_schema_range) -> versioned_snapshot`
- `CommitMutation(operation_id, expected_version, mutation, outbox_events) -> commit_receipt`
- `GetOperationResult(operation_id) -> committed | rejected | unknown`
- `ConsumeInbox(event_id, canonical_event) -> first_apply | duplicate`
- `WriteCheckpoint(operation_id, owner_epoch, simulation_tick, payload_hash)`
- `BeginRecovery(recovery_record_id, immutable_backup_id, target_manifest)`
- `VerifyRecovery(recovery_record_id) -> evidence_record`

Ownership and behavior:

- One regional transactional authority owns each durable aggregate. The mutation
  and its outbox records commit atomically. Direct database-plus-broker dual-write
  is prohibited.
- Optimistic version, ownership epoch, region, schema compatibility, invariants,
  and operation-payload hash are checked before commit.
- Duplicate committed operations return the original receipt. Ambiguous commit
  status is queried before retry. Outbox/inbox delivery is at-least-once with
  domain-level deduplication.
- Simulation emits bounded persistence intents asynchronously. Backpressure leads
  to admission/load shedding or fail-closed value outcomes; SQL, cache, object
  storage, analytics, and global control planes never enter the movement/combat
  tick.
- Migrations are expand-contract, restartable, audited, and compatible with the
  declared reader/writer window. Failure preserves the prior compatible reader and
  last valid state. Destructive restore of owner-reserved state requires the owner.
- Backups and restore remain region-local and independently recoverable. A restore
  is not complete until compatibility, invariants, operation deduplication, outbox
  position, and authoritative ownership are verified.

### 6.4 `C-SIM-01` — authoritative simulation and outcomes

Stable operations:

- `SubmitCommand(session, entity, sequence, owner_epoch, target_tick, command)`
- `AcknowledgeInput(highest_processed_sequence, authoritative_tick)`
- `ReadSnapshot(baseline, interest_scope) -> public_page + private_overlay`
- `BeginOwnershipHandoff(handoff_id, source_epoch, destination, cutover_tick)`
- `CommitOutcome(operation_id, outcome_intent)`
- `ReconnectState(session, last_acknowledged_tick, lease_epoch)`

Ownership and behavior:

- One simulation owner and monotonic epoch write each active entity/objective.
  Ghosts and snapshots are read-only. Existing `al_server_core` ownership, handoff,
  wire, and microcell contracts are the reference primitives.
- Clients submit intent. The server validates movement, time, resources, targets,
  collision/navigation, cooldowns, inventory authority, automation policy,
  objectives, and rate/work bounds before deriving outcomes.
- Manual and optional automated control use the same command validation and
  outcome rules. The provider sees placement/lifecycle requests only.
- Duplicate command sequences are harmless, stale commands expire, and future or
  wrong-epoch commands fail closed. A command cannot be applied by two owners.
- Durable value outcomes use idempotent intents and reservation/commit where needed.
  A cache, ghost, client prediction, or provider response cannot commit death,
  reward, loot, currency, progression, territory, fortress, Gem, or Wish state.
- Tick queues are bounded. Overload follows the documented degradation order and
  never slows authoritative game time or masks failure with an invented admission
  cap.

### 6.5 `C-SOC-01` — social, guild, alliance, voice, moderation, support

Stable operations:

- `ApplyMembershipMutation(operation_id, realm_id, expected_version, mutation)`
- `ResolveSocialPolicy(account_id, realm_id, channel_kind)`
- `PublishMessage(operation_id, authorized_channel, content_handle)`
- `OpenModerationCase(operation_id, evidence_handles)`
- `ApplySanction(operation_id, subject, policy_version, authorized_actor)`
- `ReconcileChannelProvider(operation_id, channel_receipt)`

Ownership and behavior:

- Social graph, guild/alliance membership, roles, channel authority, moderation
  cases, and sanctions remain region-local and same-realm under `GUILD-01`.
- The version-1.0 guild membership boundary is enforced by the social domain.
  Alliance capacity is configuration without a selected value and fails closed
  under `GUILD-03` until owner approval.
- Chat/voice providers may transport content and return opaque receipts. They do
  not own guild roles, sanctions, reports, identity, or durable evidence policy.
- Membership and sanctions use expected versions and idempotent operations.
  Message retry is allowed only when the channel contract can deduplicate the same
  operation; otherwise an ambiguous send is reconciled and surfaced without blind
  replay.
- Provider outage degrades or disables the affected channel while preserving
  membership, reports, sanctions, and evidence. It cannot open cross-realm paths.

### 6.6 `C-ECO-01` — economy, inventory, market, commerce

Stable operations:

- `PrepareValueMutation(operation_id, expected_versions, canonical_entries)`
- `CommitValueMutation(operation_id, reservation_receipt)`
- `GetValueMutation(operation_id)`
- `CreateMarketOrder(operation_id, item_version, eligibility_proof, terms)`
- `SettleMarketOrder(operation_id, expected_order_version)`
- `ReconcilePlatformEvidence(operation_id, opaque_evidence_handle)`
- `FreezeDomain(operation_id, authorized_scope, reason_class)`

Ownership and behavior:

- Region-local ledger/inventory aggregates and transactional constraints are the
  sole authority. Platform receipts and commerce adapters provide evidence only.
- Every grant, reversal, listing, settlement, transfer, reward, and refund is an
  idempotent operation with conservation, uniqueness, eligibility, provenance, and
  audit checks in one transactional boundary or explicit reservation/commit flow.
- `ECON-01`, `ECON-02`, `MON-01`, and the applicable balance/progression authority
  rows define allowed behavior. Exact prices, taxes, caps, products, and unresolved
  balance values are not part of this contract.
- Ambiguous completion is reconciled before retry. Duplicate evidence cannot mint
  value. Outage or uncertainty freezes affected mutations while reads and audit
  history remain available where safe.
- Provider or platform rollback removes/disables the evidence adapter; it never
  rewrites ledger history or treats a provider-side state as canonical settlement.

### 6.7 `C-PLT-01` — platform, client, content, and entitlement adapters

Stable operations:

- `VerifyPlatformEvidence(operation_id, account_id, opaque_evidence_handle)`
- `ReconcileEntitlement(operation_id, normalized_claim)`
- `GetCompatibility(client_fingerprint, server_fingerprint)`
- `ResolveSignedCatalog(environment, platform, last_known_good)`
- `ActivateCatalog(operation_id, verified_catalog_fingerprint)`
- `RollbackCatalog(operation_id, last_compatible_fingerprint)`

Ownership and behavior:

- Platform SDKs authenticate platform evidence and expose platform capabilities.
  They do not become canonical account, realm, gameplay, inventory, or economy
  authority.
- The client receives normalized identity/session, compatibility, and entitlement
  results. Store/platform error codes remain in the adapter.
- Binary, catalog, scene, localization, accessibility, and protocol compatibility
  are signed and explicit. Interrupted update retains the last compatible binary/
  catalog/schema tuple.
- Presentation and input may differ by platform; gameplay authority, reward,
  progression, economy, automation, and mixed-client rules remain shared.
- Unsupported device/OS tiers and networking ceilings remain unresolved at their
  owner gate. This contract creates no tier or threshold.

### 6.8 `C-CAP-01` — capacity, admission evidence, and qualification

Stable operations:

- `ObserveCapacity(region, artifact_fingerprint) -> measured_snapshot`
- `PublishWorkloadDefinition(immutable_workload_id, manifest_hash)`
- `RecordScenarioResult(scenario_id, raw_evidence_handles, limitations)`
- `RequestAdmissionDecision(session, current_measured_state, approved_policy_id)`
- `RecordDegradationAction(operation_id, reason_class, before_after_measurement)`

Ownership and behavior:

- Capacity observations are measurements, not promises or gameplay state.
  Provider dashboards are evidence inputs, not sole truth.
- Workload definitions separate connected, represented, individually replicated,
  and causally interactive populations. Active-account planning requirements are
  not concurrency evidence.
- Admission consumes an owner-approved policy and current provider-neutral
  measurements. It cannot invent a player-facing battle cap, cost ceiling, service
  objective, or device tier.
- Identical bake-off scenarios, artifact fingerprints, region/data posture,
  failures, quotas, and observation fields are required for every candidate.
  Missing or incomparable evidence yields `no-selection`, not an inferred winner.
- Raw evidence and limitations are retained; sandbox results are never
  extrapolated into production capacity.

### 6.9 `C-DEP-01` — deployment and dedicated-process lifecycle

Stable operations:

- `EnsureReady(operation_id, region, artifact_fingerprint)`
- `GetLifecycle(operation_id)`
- `Drain(operation_id, region, artifact_fingerprint)`
- `Retire(operation_id, region, artifact_fingerprint)`
- `PromoteManifest(operation_id, source, destination, signed_manifest)`
- `RollbackManifest(operation_id, last_compatible_manifest)`

Ownership and behavior:

- The deployment domain owns immutable artifacts, environment configuration,
  promotion evidence, and process lifecycle. The provider schedules/starts/stops
  capacity behind an adapter; it does not own application compatibility, realm
  state, gameplay state, or release approval.
- Lifecycle operations are idempotent and reconciled by operation ID. A duplicate
  returns the same receipt. Drain stops new placements before retirement and
  preserves the approved reconnect/session policy.
- Artifact fingerprint, region, configuration, schema compatibility, provenance,
  and authorization are verified before promotion. Mutable provider labels are not
  evidence of artifact identity.
- Failed deploy preserves the last compatible manifest and does not roll back data
  destructively. Provider-specific experiment removal returns to the neutral
  lifecycle adapter and retained manifest.

### 6.10 `C-SEC-01` — authentication, authorization, audit, containment

Stable operations:

- `Authenticate(opaque_credential_handle) -> canonical_principal`
- `Authorize(principal, capability, resource_scope, policy_version)`
- `AppendSecurityEvent(operation_id, sanitized_event, evidence_handles)`
- `RevokeCredential(operation_id, credential_reference)`
- `Contain(operation_id, authorized_scope, action_class)`

Ownership and behavior:

- Authentication proves a principal; authorization decides each capability. A
  provider role, platform identity, network location, or successful SDK call is
  not gameplay/domain authorization.
- Service identities are short-lived and least privilege. Secrets never enter
  source, gameplay payloads, traces, metrics, or provider-neutral error objects.
- Security/audit events are append-only, region/privacy scoped, and linked to
  canonical operation/correlation identities. High-impact containment and
  destructive state action obey `GOV-02` owner boundaries.
- Replay, tamper, version, scope, sequence, rate/work, and fencing failures are
  explicit. Security-provider outage fails closed for new privileged operations
  and follows approved continuation rules for existing sessions.

### 6.11 `C-OPS-01` — health, telemetry, rollback, incidents, release

Stable operations:

- `ReadHealth(service, region, artifact_fingerprint) -> measured_components`
- `EmitDomainObservation(sanitized_observation)`
- `OpenIncident(operation_id, evidence_manifest)`
- `ExecuteReversibleControl(operation_id, approved_control, scope)`
- `RecordRollback(operation_id, from_manifest, to_manifest, verification)`
- `PublishGateEvidence(immutable_candidate, evidence_manifest)`

Ownership and behavior:

- Operations observes and controls through explicit, audited capabilities. It does
  not directly edit gameplay, ledger, social, or identity records.
- Health is componentized; one pooled green signal cannot waive correctness,
  security, privacy, payment, save, economy, recovery, accessibility, localization,
  or regional isolation failure.
- Telemetry loss cannot block the simulation tick. Bounded buffers apply an
  explicit loss/backpressure policy and surface the gap. Audit/value paths fail
  closed when their required evidence cannot be retained.
- Automated actions are limited to pre-approved reversible controls. Vendor,
  spend, exposure, release, destructive restore, residual risk, and public
  communication remain owner decisions under `GOV-02`, `REL-01`, and `LIVE-01`.
- Rollback verifies artifact compatibility and authoritative state before exposure
  resumes. A provider outage or adapter removal follows the same neutral runbook.

## 7. Adapter isolation and dependency rule

The source dependency direction is:

```text
provider/platform SDK or API
        |
        v
candidate adapter crate/process
        |
        v
server/al_server_core::provider_contracts
        |
        v
regional orchestration and domain services
        |
        v
authoritative simulation/domain rules
```

The arrow is dependency on the provider-neutral contract. `al_server_core` MUST NOT
import an adapter crate, SDK, provider error, credential type, configuration
format, or provider resource identity. A candidate adapter MAY depend on
`al_server_core::provider_contracts`; the reverse dependency is forbidden.

`al_server_core::domain_contracts` exposes minimal executable persistence,
simulation, social, economy, security/abuse, and observability ports with typed
opaque handles, stable receipts, and explicit reconciliation. It provides no
implementation and grants no gameplay, authorization, settlement, or release
authority.

`server/al_provider_adapter_stub` is disposable proof of this direction. It has no
SDK or credentials and implements deterministic identity, placement, deployment,
platform, and operations seams. Its duplicate-request and payload-drift tests
exercise idempotency and fail-closed reconciliation. Deleting that crate leaves
`al_server_core` compilable and its authoritative tests unchanged.

Provider-specific identifiers remain adapter-private. Provider allocation becomes
`AllocationId`; network target becomes `EndpointHandle`; identity/entitlement payload becomes an opaque handle and an `IdentityResolution`
or `PlatformEvidenceResult`. These adapter translation results are not
authorization grants or authoritative proofs. Provider error becomes the stable failure class, canonical retry disposition, and
opaque sanitized diagnostic code. No adapter returns a gameplay result.

## 8. Managed service versus custom service gates

A capability MAY be managed, self-operated, or custom only after equivalent
scenario evidence. The default before evidence is undecided, not custom and not
managed.

| Capability | Stable seam | Evidence required before owner decision | Non-negotiable boundary |
| --- | --- | --- | --- |
| Identity verification | `ExternalIdentityAdapter` | link/recovery/revoke, outage, regional minimization, audit, exit | canonical account/realm identity remains provider-neutral |
| Placement/lifecycle | `PlacementAdapter`, `DeploymentAdapter` | launch, reconnect, fencing, drain, quota, regional isolation, rollback | provider never owns gameplay or durable realm membership |
| Durable persistence | repository/outbox contracts | transactions, migration, restore, failure, operation burden, exit | one regional transactional authority; no tick dependency |
| Cache/presence | reconstructible projection seam | eviction, loss, failover, rebuild, pressure | no durable value or sole correctness lock |
| Event pipeline | outbox relay/consumer seam | duplication, backlog, ordering, poison, outage, replay, exit | never synchronous in simulation tick; no exactly-once claim without domain idempotency |
| Social/chat/voice | social transport adapter | same-realm policy, moderation evidence, outage/degrade, evidence retention, exit | membership, sanctions, cases remain region-local authority |
| Commerce/platform | `PlatformAdapter` | receipt disagreement, replay, refund/revoke, outage, reconciliation, exit | platform evidence cannot mint value directly |
| Observability/incident | `OperationsAdapter` | telemetry loss, privacy, alert actionability, export/exit | no provider dashboard as sole gate evidence |
| Object/content storage | signed object API | integrity, corrupt object, origin loss, rollback, recovery, exit | no credentials to clients; no character/ledger authority |

Owner decisions are listed in the ADRs. A trial that cannot satisfy the seam is a
contract violation; the contract is not reshaped around the candidate.

## 9. Failure and rollback scenarios required of every adapter

1. Duplicate request with identical operation ID and payload.
2. Reused operation ID with altered payload.
3. Response lost before caller learns whether mutation committed.
4. Throttle/quota refusal and delayed recovery signal.
5. Adapter process restart with retained operation reconciliation.
6. Provider/control-plane outage while established data-plane ownership continues.
7. Failed process launch, partial lifecycle transition, stuck drain, and cancellation.
8. Regional isolation and attempted cross-region placement/write.
9. Stale/future lease epoch and duplicate active session.
10. Telemetry unavailable, backpressured, duplicated, reordered, and redacted.
11. Credential rotation/revocation without gameplay-state rewrite.
12. Adapter disable/removal, restoration of the neutral path, and verification that
    durable account, realm, gameplay, economy, and social state is unchanged.

A scenario records artifact fingerprint, adapter version, configuration hash,
region/data class, operation/correlation IDs, stable observations, raw evidence
handles, result, limitation, rollback result, and contract violations. Secrets and
personal payloads are excluded.

## 10. Unresolved owner decisions

The following remain explicit gates; no default is authorized here:

- hosting/provider selection or `no-selection` disposition after equivalent spikes;
- managed versus custom identity, persistence, cache, event, social/voice,
  commerce, object, observability, deployment, secret, and backup components;
- exact placement policy, failover semantics, maintenance behavior, and regional
  isolation policy;
- exact consistency, retention, deletion, migration compatibility, backup,
  recovery, and restore objectives;
- exact simulation/update/prediction strategy and any third-party anti-cheat;
- exact social retention/recording/consent/escalation, guild permissions,
  sanctions, and the deferred alliance capacity;
- exact economy trade/listing/settlement, products, prices, refunds, caps, taxes,
  and fraud policy;
- content-delivery system, supported platforms/devices/OS tiers, networking
  ceilings, capacity/service objectives, quotas, commitment, and cost ceiling;
- observability, incident, secret, deployment, backup, release, exposure, vendor,
  spend, and destructive-restore decisions.

The controlling owner and reopen behavior are `GOV-02`, `CAP-01`, `REL-01`,
`LIVE-01`, and unresolved rows `U-01` through `U-12`. Missing evidence yields a
blocked or `no-selection` outcome.

## 11. Acceptance traceability

| Acceptance criterion | Evidence in this baseline |
| --- | --- |
| Every named consuming epic has an explicit contract. | Section 5 maps all eleven named domains to exact task IDs; Section 6 defines their operations, ownership, failure, retry, and observation behavior. |
| Authoritative gameplay is provider-independent. | Sections 2, 4, 6.4, and 7 prohibit provider gameplay authority; `al_server_core` depends on no adapter or SDK. |
| Provider adapters are replaceable. | Section 7 dependency rule; `al_provider_adapter_stub` one-way dependency and removal contract. |
| Major architecture and managed/custom boundaries have ADRs. | `unity/Docs/Architecture/ADRs/0001` through `0004`. |
| ADRs identify unresolved owner decisions. | ADR decision-gate sections and Section 10. |
| No provider, price, cost ceiling, latency threshold, or device tier is invented. | Sections 1 and 10 explicitly defer them; capacity figures are only cited as existing unproven baseline targets. |
| Failure, idempotency, retry, and observability are explicit. | Sections 2, 3, each domain contract, and Section 9. |
