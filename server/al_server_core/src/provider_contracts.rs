//! Provider-neutral boundary contracts for external control planes.
//!
//! Authoritative gameplay does not depend on these traits. Implementations may
//! translate requests to a managed service, a self-operated control plane, or a
//! deterministic test double, but they may not decide combat, rewards, durable
//! realm membership, economy settlement, or social membership.

use std::fmt;
use std::num::NonZeroU64;

macro_rules! opaque_id {
    ($(#[$meta:meta])* $name:ident, $description:literal) => {
        $(#[$meta])*
        #[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
        pub struct $name(NonZeroU64);

        impl $name {
            #[doc = concat!("Creates ", $description, "; zero is reserved as invalid.")]
            #[must_use]
            pub const fn new(value: u64) -> Option<Self> {
                match NonZeroU64::new(value) {
                    Some(value) => Some(Self(value)),
                    None => None,
                }
            }

            #[doc = concat!("Returns the numeric value of ", $description, ".")]
            #[must_use]
            pub const fn get(self) -> u64 {
                self.0.get()
            }
        }

        impl fmt::Display for $name {
            fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                self.get().fmt(formatter)
            }
        }
    };
}

opaque_id!(
    /// Stable identity for one logical operation across retries.
    OperationId,
    "an operation identifier"
);
opaque_id!(
    /// Stable correlation identity propagated through logs, metrics, and traces.
    CorrelationId,
    "a correlation identifier"
);
opaque_id!(
    /// Canonical actor identity authorized outside the adapter boundary.
    ActorId,
    "an actor identifier"
);
opaque_id!(
    /// Least-privilege calling service identity authenticated outside the adapter.
    ServiceId,
    "a service identifier"
);
opaque_id!(
    /// Reference to an authorization decision made outside the adapter.
    AuthorizationContextId,
    "an authorization-context identifier"
);
opaque_id!(
    /// Version of the policy used by the external authorization decision.
    PolicyVersion,
    "a policy version"
);
opaque_id!(
    /// Version of the provider-neutral request and response schema.
    SchemaVersion,
    "a schema version"
);
opaque_id!(
    /// Fingerprint of the compatible build, catalog, protocol, and configuration tuple.
    CompatibilityFingerprint,
    "a compatibility fingerprint"
);
opaque_id!(
    /// Provider-neutral canonical account identity.
    AccountId,
    "an account identifier"
);
opaque_id!(
    /// Opaque region identity resolved from approved deployment configuration.
    RegionId,
    "a region identifier"
);
opaque_id!(
    /// Opaque durable realm identity.
    RealmId,
    "a realm identifier"
);
opaque_id!(
    /// Opaque authenticated session identity.
    SessionId,
    "a session identifier"
);
opaque_id!(
    /// Provider-neutral allocation identity.
    AllocationId,
    "an allocation identifier"
);
opaque_id!(
    /// Monotonic fencing generation for one placement lease.
    LeaseEpoch,
    "a lease epoch"
);
opaque_id!(
    /// Opaque connection target resolved only by the trusted gateway.
    EndpointHandle,
    "an endpoint handle"
);
opaque_id!(
    /// Opaque external identity assertion held outside the simulation core.
    AssertionHandle,
    "an assertion handle"
);
opaque_id!(
    /// Opaque platform evidence held outside the simulation core.
    PlatformEvidenceHandle,
    "a platform evidence handle"
);
opaque_id!(
    /// Stable fingerprint for an immutable server artifact and configuration tuple.
    ArtifactFingerprint,
    "an artifact fingerprint"
);

/// Provider-neutral contract identity carried by every adapter call.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub enum ContractId {
    /// `C-IDN-01` identity and session contract.
    Identity,
    /// `C-PLC-01` placement and topology contract.
    Placement,
    /// `C-PER-01` persistence and recovery contract.
    Persistence,
    /// `C-SIM-01` authoritative simulation contract.
    Simulation,
    /// `C-SOC-01` social and moderation contract.
    Social,
    /// `C-ECO-01` economy and commerce contract.
    Economy,
    /// `C-PLT-01` platform integration contract.
    Platform,
    /// `C-CAP-01` capacity and qualification contract.
    Capacity,
    /// `C-DEP-01` deployment and lifecycle contract.
    Deployment,
    /// `C-SEC-01` security assurance contract.
    Security,
    /// `C-OPS-01` operations and release contract.
    Operations,
}

impl ContractId {
    /// Returns the stable document contract identifier.
    #[must_use]
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::Identity => "C-IDN-01",
            Self::Placement => "C-PLC-01",
            Self::Persistence => "C-PER-01",
            Self::Simulation => "C-SIM-01",
            Self::Social => "C-SOC-01",
            Self::Economy => "C-ECO-01",
            Self::Platform => "C-PLT-01",
            Self::Capacity => "C-CAP-01",
            Self::Deployment => "C-DEP-01",
            Self::Security => "C-SEC-01",
            Self::Operations => "C-OPS-01",
        }
    }
}

/// Context that must remain stable across a retryable external operation.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct AdapterRequestContext {
    contract_id: ContractId,
    operation_id: OperationId,
    correlation_id: CorrelationId,
    actor_id: ActorId,
    service_id: ServiceId,
    authorization_context_id: AuthorizationContextId,
    policy_version: PolicyVersion,
    region_id: RegionId,
    realm_id: Option<RealmId>,
    schema_version: SchemaVersion,
    artifact_fingerprint: ArtifactFingerprint,
    compatibility_fingerprint: CompatibilityFingerprint,
    attempt: u32,
}

impl AdapterRequestContext {
    /// Creates context for an operation attempt authorized outside the adapter.
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub const fn new(
        contract_id: ContractId,
        operation_id: OperationId,
        correlation_id: CorrelationId,
        actor_id: ActorId,
        service_id: ServiceId,
        authorization_context_id: AuthorizationContextId,
        policy_version: PolicyVersion,
        region_id: RegionId,
        realm_id: Option<RealmId>,
        schema_version: SchemaVersion,
        artifact_fingerprint: ArtifactFingerprint,
        compatibility_fingerprint: CompatibilityFingerprint,
        attempt: u32,
    ) -> Self {
        Self {
            contract_id,
            operation_id,
            correlation_id,
            actor_id,
            service_id,
            authorization_context_id,
            policy_version,
            region_id,
            realm_id,
            schema_version,
            artifact_fingerprint,
            compatibility_fingerprint,
            attempt,
        }
    }

    /// Returns the provider-neutral contract governing this call.
    #[must_use]
    pub const fn contract_id(self) -> ContractId {
        self.contract_id
    }

    /// Returns the idempotency identity. Retries must preserve it.
    #[must_use]
    pub const fn operation_id(self) -> OperationId {
        self.operation_id
    }

    /// Returns the observability correlation identity.
    #[must_use]
    pub const fn correlation_id(self) -> CorrelationId {
        self.correlation_id
    }

    /// Returns the canonical actor authorized before adapter invocation.
    #[must_use]
    pub const fn actor_id(self) -> ActorId {
        self.actor_id
    }

    /// Returns the authenticated least-privilege calling service.
    #[must_use]
    pub const fn service_id(self) -> ServiceId {
        self.service_id
    }

    /// Returns the reference to the external authorization decision.
    #[must_use]
    pub const fn authorization_context_id(self) -> AuthorizationContextId {
        self.authorization_context_id
    }

    /// Returns the policy version used by the authorization decision.
    #[must_use]
    pub const fn policy_version(self) -> PolicyVersion {
        self.policy_version
    }

    /// Returns the mandatory owning region scope.
    #[must_use]
    pub const fn region_id(self) -> RegionId {
        self.region_id
    }

    /// Returns the durable realm scope for realm-scoped operations.
    #[must_use]
    pub const fn realm_id(self) -> Option<RealmId> {
        self.realm_id
    }

    /// Returns the provider-neutral contract schema version.
    #[must_use]
    pub const fn schema_version(self) -> SchemaVersion {
        self.schema_version
    }

    /// Returns the immutable server artifact and configuration fingerprint.
    #[must_use]
    pub const fn artifact_fingerprint(self) -> ArtifactFingerprint {
        self.artifact_fingerprint
    }

    /// Returns the required compatibility tuple fingerprint.
    #[must_use]
    pub const fn compatibility_fingerprint(self) -> CompatibilityFingerprint {
        self.compatibility_fingerprint
    }

    /// Returns the zero-based caller attempt number.
    #[must_use]
    pub const fn attempt(self) -> u32 {
        self.attempt
    }

    /// Produces the next retry attempt while preserving every other field.
    #[must_use]
    pub const fn next_attempt(self) -> Option<Self> {
        match self.attempt.checked_add(1) {
            Some(attempt) => Some(Self { attempt, ..self }),
            None => None,
        }
    }

    /// Returns whether two attempts preserve every retry-invariant field.
    #[must_use]
    pub fn has_same_retry_invariants(self, other: Self) -> bool {
        self.contract_id == other.contract_id
            && self.operation_id == other.operation_id
            && self.correlation_id == other.correlation_id
            && self.actor_id == other.actor_id
            && self.service_id == other.service_id
            && self.authorization_context_id == other.authorization_context_id
            && self.policy_version == other.policy_version
            && self.region_id == other.region_id
            && self.realm_id == other.realm_id
            && self.schema_version == other.schema_version
            && self.artifact_fingerprint == other.artifact_fingerprint
            && self.compatibility_fingerprint == other.compatibility_fingerprint
    }
}

/// Stable failure classes exposed by every external adapter.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum AdapterFailureClass {
    /// The provider-neutral request is invalid and must not be retried unchanged.
    InvalidRequest,
    /// Authentication or authorization failed.
    Unauthorized,
    /// Current fenced state conflicts with the request.
    Conflict,
    /// The external boundary refused work because a quota or rate guard fired.
    Throttled,
    /// The external dependency is temporarily unavailable.
    Unavailable,
    /// Completion is unknown because the caller lost the response.
    AmbiguousCompletion,
    /// The requested capability is not implemented by this adapter.
    Unsupported,
    /// An internal adapter translation or invariant failed.
    Internal,
}

/// Caller behavior permitted after an adapter failure.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum RetryDisposition {
    /// Do not retry the same request.
    Never,
    /// Retry only after an explicit health, capacity, or operator signal.
    AfterExplicitSignal,
    /// Reconcile status by operation identity before deciding whether to retry.
    ReconcileFirst,
}

/// Opaque sanitized diagnostic identity safe to expose across adapter boundaries.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct SanitizedDiagnosticCode(NonZeroU64);

impl SanitizedDiagnosticCode {
    const fn from_failure_class(class: AdapterFailureClass) -> Self {
        let value = match class {
            AdapterFailureClass::InvalidRequest => 1,
            AdapterFailureClass::Unauthorized => 2,
            AdapterFailureClass::Conflict => 3,
            AdapterFailureClass::Throttled => 4,
            AdapterFailureClass::Unavailable => 5,
            AdapterFailureClass::AmbiguousCompletion => 6,
            AdapterFailureClass::Unsupported => 7,
            AdapterFailureClass::Internal => 8,
        };
        match NonZeroU64::new(value) {
            Some(value) => Self(value),
            None => unreachable!(),
        }
    }

    /// Returns the opaque provider-neutral code value.
    #[must_use]
    pub const fn get(self) -> u64 {
        self.0.get()
    }
}

/// Provider-neutral adapter failure with no provider message or secret payload.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct AdapterError {
    class: AdapterFailureClass,
    retry: RetryDisposition,
    diagnostic_code: SanitizedDiagnosticCode,
}

impl AdapterError {
    /// Creates an adapter error with the canonical retry contract for its class.
    #[must_use]
    pub const fn from_class(class: AdapterFailureClass) -> Self {
        let retry = match class {
            AdapterFailureClass::Conflict | AdapterFailureClass::AmbiguousCompletion => {
                RetryDisposition::ReconcileFirst
            }
            AdapterFailureClass::InvalidRequest
            | AdapterFailureClass::Unauthorized
            | AdapterFailureClass::Unsupported => RetryDisposition::Never,
            AdapterFailureClass::Throttled
            | AdapterFailureClass::Unavailable
            | AdapterFailureClass::Internal => RetryDisposition::AfterExplicitSignal,
        };
        Self {
            class,
            retry,
            diagnostic_code: SanitizedDiagnosticCode::from_failure_class(class),
        }
    }

    /// Returns the stable failure class.
    #[must_use]
    pub const fn class(self) -> AdapterFailureClass {
        self.class
    }

    /// Returns the caller retry contract.
    #[must_use]
    pub const fn retry(self) -> RetryDisposition {
        self.retry
    }

    /// Returns an opaque sanitized code without a provider message or secret payload.
    #[must_use]
    pub const fn diagnostic_code(self) -> SanitizedDiagnosticCode {
        self.diagnostic_code
    }
}

impl fmt::Display for AdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{:?} ({:?})", self.class, self.retry)
    }
}

impl std::error::Error for AdapterError {}

/// Observable lifecycle of one provider adapter call.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum AdapterObservationKind {
    /// The adapter accepted a call for processing.
    Started,
    /// The adapter returned the same result for a duplicate operation.
    Duplicate,
    /// The adapter accepted work that remains incomplete.
    Pending,
    /// The adapter completed successfully.
    Succeeded,
    /// The adapter failed with a stable classification.
    Failed(AdapterFailureClass),
}

/// Low-cardinality observation emitted for every adapter call.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct AdapterObservation {
    contract_id: ContractId,
    operation_id: OperationId,
    correlation_id: CorrelationId,
    region_id: RegionId,
    realm_id: Option<RealmId>,
    schema_version: SchemaVersion,
    artifact_fingerprint: ArtifactFingerprint,
    boundary: AdapterBoundary,
    kind: AdapterObservationKind,
}

impl AdapterObservation {
    /// Creates an adapter observation using mandatory dimensions from the call context.
    #[must_use]
    pub const fn new(
        context: AdapterRequestContext,
        boundary: AdapterBoundary,
        kind: AdapterObservationKind,
    ) -> Self {
        Self {
            contract_id: context.contract_id(),
            operation_id: context.operation_id(),
            correlation_id: context.correlation_id(),
            region_id: context.region_id(),
            realm_id: context.realm_id(),
            schema_version: context.schema_version(),
            artifact_fingerprint: context.artifact_fingerprint(),
            boundary,
            kind,
        }
    }

    /// Returns the provider-neutral contract governing the call.
    #[must_use]
    pub const fn contract_id(self) -> ContractId {
        self.contract_id
    }

    /// Returns the logical operation identity.
    #[must_use]
    pub const fn operation_id(self) -> OperationId {
        self.operation_id
    }

    /// Returns the trace/log correlation identity.
    #[must_use]
    pub const fn correlation_id(self) -> CorrelationId {
        self.correlation_id
    }

    /// Returns the mandatory region dimension.
    #[must_use]
    pub const fn region_id(self) -> RegionId {
        self.region_id
    }

    /// Returns the optional durable realm dimension.
    #[must_use]
    pub const fn realm_id(self) -> Option<RealmId> {
        self.realm_id
    }

    /// Returns the provider-neutral schema dimension.
    #[must_use]
    pub const fn schema_version(self) -> SchemaVersion {
        self.schema_version
    }

    /// Returns the immutable artifact dimension.
    #[must_use]
    pub const fn artifact_fingerprint(self) -> ArtifactFingerprint {
        self.artifact_fingerprint
    }

    /// Returns the external boundary that emitted the observation.
    #[must_use]
    pub const fn boundary(self) -> AdapterBoundary {
        self.boundary
    }

    /// Returns the observed lifecycle state.
    #[must_use]
    pub const fn kind(self) -> AdapterObservationKind {
        self.kind
    }

    /// Returns the stable failure class when this observation represents failure.
    #[must_use]
    pub const fn result_class(self) -> Option<AdapterFailureClass> {
        match self.kind {
            AdapterObservationKind::Failed(class) => Some(class),
            AdapterObservationKind::Started
            | AdapterObservationKind::Duplicate
            | AdapterObservationKind::Pending
            | AdapterObservationKind::Succeeded => None,
        }
    }
}

/// Provider boundaries that may have replaceable implementations.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum AdapterBoundary {
    /// External account assertion verification.
    Identity,
    /// Server/session placement control plane.
    Placement,
    /// Dedicated process lifecycle control plane.
    Deployment,
    /// Platform entitlement evidence verification.
    Platform,
    /// External health and capacity observation.
    Operations,
}

/// Sink for adapter metrics, logs, and trace correlation.
pub trait AdapterObserver {
    /// Records one sanitized, provider-neutral observation.
    fn record(&mut self, observation: AdapterObservation);
}

/// Provider-neutral result of translating an external identity assertion.
///
/// This adapter-constructible value is not an authorization grant or an
/// authoritative proof. Authentication and authorization remain outside adapters.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct IdentityResolution {
    account_id: AccountId,
}

impl IdentityResolution {
    /// Creates an adapter translation result for a canonical account identity.
    #[must_use]
    pub const fn new(account_id: AccountId) -> Self {
        Self { account_id }
    }

    /// Returns the canonical account identity.
    #[must_use]
    pub const fn account_id(self) -> AccountId {
        self.account_id
    }
}

/// Replaceable external-identity verification boundary.
pub trait ExternalIdentityAdapter {
    /// Translates opaque identity evidence without granting authorization.
    fn verify_assertion(
        &mut self,
        context: AdapterRequestContext,
        assertion: AssertionHandle,
        observer: &mut dyn AdapterObserver,
    ) -> Result<IdentityResolution, AdapterError>;
}

/// Request for a region-local authoritative session placement.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PlacementRequest {
    account_id: AccountId,
    session_id: SessionId,
    region_id: RegionId,
    realm_id: RealmId,
    artifact: ArtifactFingerprint,
}

impl PlacementRequest {
    /// Creates a placement request from provider-neutral identities.
    #[must_use]
    pub const fn new(
        account_id: AccountId,
        session_id: SessionId,
        region_id: RegionId,
        realm_id: RealmId,
        artifact: ArtifactFingerprint,
    ) -> Self {
        Self {
            account_id,
            session_id,
            region_id,
            realm_id,
            artifact,
        }
    }

    /// Returns the canonical account identity.
    #[must_use]
    pub const fn account_id(self) -> AccountId {
        self.account_id
    }

    /// Returns the authenticated session identity.
    #[must_use]
    pub const fn session_id(self) -> SessionId {
        self.session_id
    }

    /// Returns the required deployment region.
    #[must_use]
    pub const fn region_id(self) -> RegionId {
        self.region_id
    }

    /// Returns the account's durable realm identity.
    #[must_use]
    pub const fn realm_id(self) -> RealmId {
        self.realm_id
    }

    /// Returns the required immutable artifact fingerprint.
    #[must_use]
    pub const fn artifact(self) -> ArtifactFingerprint {
        self.artifact
    }
}

/// Stable receipt returned when a placement operation is accepted.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PlacementReceipt {
    operation_id: OperationId,
    allocation_id: AllocationId,
}

impl PlacementReceipt {
    /// Creates a placement receipt.
    #[must_use]
    pub const fn new(operation_id: OperationId, allocation_id: AllocationId) -> Self {
        Self {
            operation_id,
            allocation_id,
        }
    }

    /// Returns the operation identity used for reconciliation.
    #[must_use]
    pub const fn operation_id(self) -> OperationId {
        self.operation_id
    }

    /// Returns the provider-neutral allocation identity.
    #[must_use]
    pub const fn allocation_id(self) -> AllocationId {
        self.allocation_id
    }
}

/// Reconciled state of a placement operation.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum PlacementStatus {
    /// Placement remains pending and the caller must poll without changing payload.
    Pending,
    /// Placement is ready behind a fenced lease and opaque gateway target.
    Ready {
        /// Provider-neutral allocation identity.
        allocation_id: AllocationId,
        /// Monotonic lease fencing generation.
        lease_epoch: LeaseEpoch,
        /// Opaque endpoint resolved only by a trusted gateway.
        endpoint: EndpointHandle,
    },
    /// Placement was cancelled without changing durable realm identity.
    Cancelled,
    /// Placement ended and must be resubmitted only as a new logical operation.
    Failed(AdapterFailureClass),
}

/// Replaceable server-placement control-plane boundary.
pub trait PlacementAdapter {
    /// Submits an idempotent placement operation.
    fn submit(
        &mut self,
        context: AdapterRequestContext,
        request: PlacementRequest,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlacementReceipt, AdapterError>;

    /// Reconciles current placement state by the original receipt.
    fn status(
        &mut self,
        context: AdapterRequestContext,
        receipt: PlacementReceipt,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlacementStatus, AdapterError>;

    /// Cancels an incomplete placement idempotently.
    fn cancel(
        &mut self,
        context: AdapterRequestContext,
        receipt: PlacementReceipt,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlacementStatus, AdapterError>;
}

/// Requested lifecycle state for one immutable server artifact.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ProcessLifecycleRequest {
    /// Make bounded ready capacity eligible for placement.
    EnsureReady {
        /// Region in which capacity must remain isolated.
        region_id: RegionId,
        /// Immutable server artifact and configuration tuple.
        artifact: ArtifactFingerprint,
    },
    /// Stop new placements while preserving existing session policy.
    Drain {
        /// Region whose matching processes should drain.
        region_id: RegionId,
        /// Immutable artifact generation to drain.
        artifact: ArtifactFingerprint,
    },
    /// Retire a drained artifact generation.
    Retire {
        /// Region whose drained processes may retire.
        region_id: RegionId,
        /// Immutable artifact generation to retire.
        artifact: ArtifactFingerprint,
    },
}

/// Stable receipt for an idempotent lifecycle operation.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct LifecycleReceipt {
    operation_id: OperationId,
}

impl LifecycleReceipt {
    /// Creates a lifecycle receipt.
    #[must_use]
    pub const fn new(operation_id: OperationId) -> Self {
        Self { operation_id }
    }

    /// Returns the operation identity used for reconciliation.
    #[must_use]
    pub const fn operation_id(self) -> OperationId {
        self.operation_id
    }
}

/// Reconciled lifecycle-operation state.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum LifecycleStatus {
    /// The requested transition remains in progress.
    Pending,
    /// The requested transition completed.
    Complete,
    /// The requested transition failed with a stable class.
    Failed(AdapterFailureClass),
}

/// Replaceable dedicated-process lifecycle boundary.
pub trait DeploymentAdapter {
    /// Submits an idempotent process lifecycle request.
    fn submit_lifecycle(
        &mut self,
        context: AdapterRequestContext,
        request: ProcessLifecycleRequest,
        observer: &mut dyn AdapterObserver,
    ) -> Result<LifecycleReceipt, AdapterError>;

    /// Reconciles a prior lifecycle operation.
    fn lifecycle_status(
        &mut self,
        context: AdapterRequestContext,
        receipt: LifecycleReceipt,
        observer: &mut dyn AdapterObserver,
    ) -> Result<LifecycleStatus, AdapterError>;
}

/// Sanitized capacity observation. Values are measurements, never promises.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct CapacityObservation {
    region_id: RegionId,
    artifact: ArtifactFingerprint,
    ready_processes: u64,
    allocated_processes: u64,
    pending_operations: u64,
}

impl CapacityObservation {
    /// Creates a measured capacity observation.
    #[must_use]
    pub const fn new(
        region_id: RegionId,
        artifact: ArtifactFingerprint,
        ready_processes: u64,
        allocated_processes: u64,
        pending_operations: u64,
    ) -> Self {
        Self {
            region_id,
            artifact,
            ready_processes,
            allocated_processes,
            pending_operations,
        }
    }

    /// Returns the observed region.
    #[must_use]
    pub const fn region_id(self) -> RegionId {
        self.region_id
    }

    /// Returns the observed immutable artifact generation.
    #[must_use]
    pub const fn artifact(self) -> ArtifactFingerprint {
        self.artifact
    }

    /// Returns currently observed ready processes.
    #[must_use]
    pub const fn ready_processes(self) -> u64 {
        self.ready_processes
    }

    /// Returns currently observed allocated processes.
    #[must_use]
    pub const fn allocated_processes(self) -> u64 {
        self.allocated_processes
    }

    /// Returns currently observed pending control-plane operations.
    #[must_use]
    pub const fn pending_operations(self) -> u64 {
        self.pending_operations
    }
}

/// Replaceable read-only operations and capacity boundary.
pub trait OperationsAdapter {
    /// Reads a point-in-time measurement without converting it into admission truth.
    fn observe_capacity(
        &mut self,
        context: AdapterRequestContext,
        region_id: RegionId,
        artifact: ArtifactFingerprint,
        observer: &mut dyn AdapterObserver,
    ) -> Result<CapacityObservation, AdapterError>;
}

/// Provider-neutral result of translating external platform evidence.
///
/// This adapter-constructible result is neither an authoritative proof nor an
/// authorization or economy grant. Domain decisions remain outside adapters.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PlatformEvidenceResult {
    operation_id: OperationId,
    account_id: AccountId,
}

impl PlatformEvidenceResult {
    /// Creates an adapter translation result for platform evidence.
    #[must_use]
    pub const fn new(operation_id: OperationId, account_id: AccountId) -> Self {
        Self {
            operation_id,
            account_id,
        }
    }

    /// Returns the reconciliation operation identity.
    #[must_use]
    pub const fn operation_id(self) -> OperationId {
        self.operation_id
    }

    /// Returns the canonical account identity linked to the evidence.
    #[must_use]
    pub const fn account_id(self) -> AccountId {
        self.account_id
    }
}

/// Replaceable platform evidence verification boundary.
pub trait PlatformAdapter {
    /// Translates opaque external evidence without granting authorization or value.
    fn verify_evidence(
        &mut self,
        context: AdapterRequestContext,
        account_id: AccountId,
        evidence: PlatformEvidenceHandle,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlatformEvidenceResult, AdapterError>;
}

#[cfg(test)]
mod tests {
    use super::*;

    fn id<T>(value: u64, constructor: fn(u64) -> Option<T>) -> T {
        constructor(value).expect("test identity must be nonzero")
    }

    #[test]
    fn retry_context_preserves_every_field_except_attempt() {
        let context = AdapterRequestContext::new(
            ContractId::Placement,
            id(7, OperationId::new),
            id(9, CorrelationId::new),
            id(10, ActorId::new),
            id(11, ServiceId::new),
            id(12, AuthorizationContextId::new),
            id(13, PolicyVersion::new),
            id(14, RegionId::new),
            Some(id(15, RealmId::new)),
            id(16, SchemaVersion::new),
            id(17, ArtifactFingerprint::new),
            id(18, CompatibilityFingerprint::new),
            0,
        );
        let retry = context.next_attempt().expect("attempt must increment");

        assert_eq!(retry.contract_id(), context.contract_id());
        assert_eq!(retry.operation_id(), context.operation_id());
        assert_eq!(retry.correlation_id(), context.correlation_id());
        assert_eq!(retry.actor_id(), context.actor_id());
        assert_eq!(retry.service_id(), context.service_id());
        assert_eq!(
            retry.authorization_context_id(),
            context.authorization_context_id()
        );
        assert_eq!(retry.policy_version(), context.policy_version());
        assert_eq!(retry.region_id(), context.region_id());
        assert_eq!(retry.realm_id(), context.realm_id());
        assert_eq!(retry.schema_version(), context.schema_version());
        assert_eq!(retry.artifact_fingerprint(), context.artifact_fingerprint());
        assert_eq!(
            retry.compatibility_fingerprint(),
            context.compatibility_fingerprint()
        );
        assert_eq!(retry.attempt(), 1);
        assert!(context.has_same_retry_invariants(retry));
    }

    #[test]
    fn retry_invariant_comparison_detects_authorization_context_drift() {
        let original = AdapterRequestContext::new(
            ContractId::Placement,
            id(21, OperationId::new),
            id(22, CorrelationId::new),
            id(23, ActorId::new),
            id(24, ServiceId::new),
            id(25, AuthorizationContextId::new),
            id(26, PolicyVersion::new),
            id(27, RegionId::new),
            Some(id(28, RealmId::new)),
            id(29, SchemaVersion::new),
            id(30, ArtifactFingerprint::new),
            id(31, CompatibilityFingerprint::new),
            0,
        );
        let drifted = AdapterRequestContext::new(
            ContractId::Placement,
            original.operation_id(),
            original.correlation_id(),
            original.actor_id(),
            original.service_id(),
            id(125, AuthorizationContextId::new),
            original.policy_version(),
            original.region_id(),
            original.realm_id(),
            original.schema_version(),
            original.artifact_fingerprint(),
            original.compatibility_fingerprint(),
            1,
        );

        assert!(!original.has_same_retry_invariants(drifted));
    }

    #[test]
    fn retry_invariant_comparison_covers_every_context_dimension() {
        let original = AdapterRequestContext::new(
            ContractId::Placement,
            id(61, OperationId::new),
            id(62, CorrelationId::new),
            id(63, ActorId::new),
            id(64, ServiceId::new),
            id(65, AuthorizationContextId::new),
            id(66, PolicyVersion::new),
            id(67, RegionId::new),
            Some(id(68, RealmId::new)),
            id(69, SchemaVersion::new),
            id(70, ArtifactFingerprint::new),
            id(71, CompatibilityFingerprint::new),
            0,
        );
        let drifted = [
            AdapterRequestContext {
                contract_id: ContractId::Deployment,
                ..original
            },
            AdapterRequestContext {
                operation_id: id(161, OperationId::new),
                ..original
            },
            AdapterRequestContext {
                correlation_id: id(162, CorrelationId::new),
                ..original
            },
            AdapterRequestContext {
                actor_id: id(163, ActorId::new),
                ..original
            },
            AdapterRequestContext {
                service_id: id(164, ServiceId::new),
                ..original
            },
            AdapterRequestContext {
                authorization_context_id: id(165, AuthorizationContextId::new),
                ..original
            },
            AdapterRequestContext {
                policy_version: id(166, PolicyVersion::new),
                ..original
            },
            AdapterRequestContext {
                region_id: id(167, RegionId::new),
                ..original
            },
            AdapterRequestContext {
                realm_id: Some(id(168, RealmId::new)),
                ..original
            },
            AdapterRequestContext {
                schema_version: id(169, SchemaVersion::new),
                ..original
            },
            AdapterRequestContext {
                artifact_fingerprint: id(170, ArtifactFingerprint::new),
                ..original
            },
            AdapterRequestContext {
                compatibility_fingerprint: id(171, CompatibilityFingerprint::new),
                ..original
            },
        ];

        assert!(drifted
            .into_iter()
            .all(|candidate| !original.has_same_retry_invariants(candidate)));
        assert!(original.has_same_retry_invariants(AdapterRequestContext {
            attempt: 99,
            ..original
        }));
    }

    #[test]
    fn observation_preserves_mandatory_low_cardinality_dimensions() {
        let context = AdapterRequestContext::new(
            ContractId::Deployment,
            id(41, OperationId::new),
            id(42, CorrelationId::new),
            id(43, ActorId::new),
            id(44, ServiceId::new),
            id(45, AuthorizationContextId::new),
            id(46, PolicyVersion::new),
            id(47, RegionId::new),
            Some(id(48, RealmId::new)),
            id(49, SchemaVersion::new),
            id(50, ArtifactFingerprint::new),
            id(51, CompatibilityFingerprint::new),
            0,
        );
        let observation = AdapterObservation::new(
            context,
            AdapterBoundary::Deployment,
            AdapterObservationKind::Failed(AdapterFailureClass::Unavailable),
        );

        assert_eq!(observation.contract_id(), context.contract_id());
        assert_eq!(observation.boundary(), AdapterBoundary::Deployment);
        assert_eq!(observation.operation_id(), context.operation_id());
        assert_eq!(observation.correlation_id(), context.correlation_id());
        assert_eq!(observation.region_id(), context.region_id());
        assert_eq!(observation.realm_id(), context.realm_id());
        assert_eq!(observation.schema_version(), context.schema_version());
        assert_eq!(
            observation.artifact_fingerprint(),
            context.artifact_fingerprint()
        );
        assert_eq!(
            observation.kind(),
            AdapterObservationKind::Failed(AdapterFailureClass::Unavailable)
        );
        assert_eq!(
            observation.result_class(),
            Some(AdapterFailureClass::Unavailable)
        );
    }

    #[test]
    fn opaque_ids_reject_reserved_zero() {
        assert_eq!(OperationId::new(0), None);
        assert_eq!(AccountId::new(0), None);
        assert_eq!(RegionId::new(0), None);
        assert_eq!(RealmId::new(0), None);
        assert_eq!(LeaseEpoch::new(0), None);
    }

    #[test]
    fn placement_request_keeps_region_and_realm_separate() {
        let request = PlacementRequest::new(
            id(1, AccountId::new),
            id(2, SessionId::new),
            id(3, RegionId::new),
            id(4, RealmId::new),
            id(5, ArtifactFingerprint::new),
        );

        assert_ne!(request.region_id().get(), request.realm_id().get());
        assert_eq!(request.account_id().get(), 1);
        assert_eq!(request.session_id().get(), 2);
        assert_eq!(request.artifact().get(), 5);
    }

    #[test]
    fn failure_classes_have_one_canonical_retry_disposition() {
        let cases = [
            (AdapterFailureClass::InvalidRequest, RetryDisposition::Never),
            (AdapterFailureClass::Unauthorized, RetryDisposition::Never),
            (
                AdapterFailureClass::Conflict,
                RetryDisposition::ReconcileFirst,
            ),
            (
                AdapterFailureClass::Throttled,
                RetryDisposition::AfterExplicitSignal,
            ),
            (
                AdapterFailureClass::Unavailable,
                RetryDisposition::AfterExplicitSignal,
            ),
            (
                AdapterFailureClass::AmbiguousCompletion,
                RetryDisposition::ReconcileFirst,
            ),
            (AdapterFailureClass::Unsupported, RetryDisposition::Never),
            (
                AdapterFailureClass::Internal,
                RetryDisposition::AfterExplicitSignal,
            ),
        ];

        let mut diagnostic_codes = Vec::new();
        for (class, expected_retry) in cases {
            let error = AdapterError::from_class(class);
            assert_eq!(error.class(), class);
            assert_eq!(error.retry(), expected_retry);
            diagnostic_codes.push(error.diagnostic_code().get());
        }
        diagnostic_codes.sort_unstable();
        diagnostic_codes.dedup();
        assert_eq!(diagnostic_codes.len(), cases.len());
    }
}
