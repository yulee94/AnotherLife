//! Disposable Amazon GameLift Servers adapter spike.
//!
//! Provider-private request and response shapes live in this crate. The
//! authoritative core depends only on `al_server_core::provider_contracts` and
//! never imports this crate. The API transport is injected so contract behavior
//! can be exercised without credentials; a real sandbox transport may translate
//! these private values to GameLift API calls.

#![forbid(unsafe_code)]
#![deny(missing_docs)]

use al_server_core::provider_contracts::{
    AdapterBoundary, AdapterError, AdapterFailureClass, AdapterObservation, AdapterObservationKind,
    AdapterObserver, AdapterRequestContext, AllocationId, ArtifactFingerprint, CapacityObservation,
    CompatibilityFingerprint, ContractId, DeploymentAdapter, EndpointHandle, LeaseEpoch,
    LifecycleReceipt, LifecycleStatus, OperationId, OperationsAdapter, PlacementAdapter,
    PlacementReceipt, PlacementRequest, PlacementStatus, ProcessLifecycleRequest, RealmId,
    RegionId,
};

/// GameLift-private failure categories translated at the adapter boundary.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum GameLiftApiFailure {
    /// The candidate request was rejected as malformed.
    InvalidRequest,
    /// Candidate credentials or policy rejected the operation.
    Unauthorized,
    /// Candidate state conflicted with the request.
    Conflict,
    /// A GameLift quota or rate guard rejected the request.
    Throttled,
    /// The GameLift control plane was unavailable.
    Unavailable,
    /// The response was lost after completion became possible.
    AmbiguousCompletion,
    /// The candidate translation or response was invalid.
    Internal,
}

/// Sanitized GameLift-private API error used only inside this disposable crate.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GameLiftApiError {
    failure: GameLiftApiFailure,
}

impl GameLiftApiError {
    /// Creates a sanitized candidate error without retaining provider messages.
    #[must_use]
    pub const fn new(failure: GameLiftApiFailure) -> Self {
        Self { failure }
    }

    /// Returns the candidate-private failure category.
    #[must_use]
    pub const fn failure(self) -> GameLiftApiFailure {
        self.failure
    }
}

/// Candidate-private placement state returned by the injected transport.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum GameLiftPlacementState {
    /// The placement remains queued or provisioning.
    Pending,
    /// The placement is ready and has private lease and endpoint tokens.
    Ready {
        /// Monotonic lease token translated to the neutral fence.
        lease_epoch: u64,
        /// Private endpoint token translated to an opaque neutral handle.
        endpoint_token: u64,
    },
    /// The placement was cancelled.
    Cancelled,
    /// The placement failed with a sanitized candidate category.
    Failed(GameLiftApiFailure),
}

/// Candidate-private placement snapshot.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GameLiftPlacementSnapshot {
    placement_token: u64,
    allocation_token: u64,
    region_id: RegionId,
    realm_id: RealmId,
    artifact: ArtifactFingerprint,
    compatibility: CompatibilityFingerprint,
    state: GameLiftPlacementState,
}

impl GameLiftPlacementSnapshot {
    /// Creates a candidate-private placement snapshot.
    #[must_use]
    pub const fn new(
        placement_token: u64,
        allocation_token: u64,
        region_id: RegionId,
        realm_id: RealmId,
        artifact: ArtifactFingerprint,
        compatibility: CompatibilityFingerprint,
        state: GameLiftPlacementState,
    ) -> Self {
        Self {
            placement_token,
            allocation_token,
            region_id,
            realm_id,
            artifact,
            compatibility,
            state,
        }
    }
}

/// Candidate-private start-placement request.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GameLiftStartPlacement {
    placement_token: u64,
    queue_token: u64,
    region_id: RegionId,
    realm_id: RealmId,
    artifact: ArtifactFingerprint,
    compatibility: CompatibilityFingerprint,
}

impl GameLiftStartPlacement {
    /// Returns the deterministic candidate placement token.
    #[must_use]
    pub const fn placement_token(self) -> u64 {
        self.placement_token
    }

    /// Returns the pre-authorized provider-neutral region.
    #[must_use]
    pub const fn region_id(self) -> RegionId {
        self.region_id
    }

    /// Returns the preassigned durable realm.
    #[must_use]
    pub const fn realm_id(self) -> RealmId {
        self.realm_id
    }

    /// Returns the immutable server artifact requested by the caller.
    #[must_use]
    pub const fn artifact(self) -> ArtifactFingerprint {
        self.artifact
    }

    /// Returns the immutable compatibility fingerprint authorized by the caller.
    #[must_use]
    pub const fn compatibility(self) -> CompatibilityFingerprint {
        self.compatibility
    }

    /// Returns the adapter-private queue token.
    #[must_use]
    pub const fn queue_token(self) -> u64 {
        self.queue_token
    }
}

/// Candidate-private lifecycle action.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GameLiftLifecycleAction {
    fleet_token: u64,
    request: ProcessLifecycleRequest,
}

impl GameLiftLifecycleAction {
    /// Returns the adapter-private fleet token.
    #[must_use]
    pub const fn fleet_token(self) -> u64 {
        self.fleet_token
    }

    /// Returns the unchanged provider-neutral lifecycle request.
    #[must_use]
    pub const fn request(self) -> ProcessLifecycleRequest {
        self.request
    }
}

/// Candidate-private lifecycle result retaining the applied provider scope.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GameLiftLifecycleSnapshot {
    fleet_token: u64,
    request: ProcessLifecycleRequest,
    status: LifecycleStatus,
}

impl GameLiftLifecycleSnapshot {
    /// Creates a scoped lifecycle response from the injected transport.
    #[must_use]
    pub const fn new(
        fleet_token: u64,
        request: ProcessLifecycleRequest,
        status: LifecycleStatus,
    ) -> Self {
        Self {
            fleet_token,
            request,
            status,
        }
    }
}

/// Candidate-private capacity snapshot. Values are measurements, not promises.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GameLiftCapacitySnapshot {
    region_id: RegionId,
    artifact: ArtifactFingerprint,
    compatibility: CompatibilityFingerprint,
    ready_processes: u64,
    allocated_processes: u64,
    pending_operations: u64,
}

impl GameLiftCapacitySnapshot {
    /// Creates a point-in-time candidate capacity snapshot.
    #[must_use]
    pub const fn new(
        region_id: RegionId,
        artifact: ArtifactFingerprint,
        compatibility: CompatibilityFingerprint,
        ready_processes: u64,
        allocated_processes: u64,
        pending_operations: u64,
    ) -> Self {
        Self {
            region_id,
            artifact,
            compatibility,
            ready_processes,
            allocated_processes,
            pending_operations,
        }
    }
}

/// Injected GameLift API transport. Provider SDK types remain behind this trait.
pub trait GameLiftApi {
    /// Translates to `StartGameSessionPlacement` in a real sandbox transport.
    fn start_game_session_placement(
        &mut self,
        request: GameLiftStartPlacement,
    ) -> Result<GameLiftPlacementSnapshot, GameLiftApiError>;

    /// Translates to `DescribeGameSessionPlacement`.
    fn describe_game_session_placement(
        &mut self,
        placement_token: u64,
    ) -> Result<GameLiftPlacementSnapshot, GameLiftApiError>;

    /// Translates to `StopGameSessionPlacement`.
    fn stop_game_session_placement(
        &mut self,
        placement_token: u64,
    ) -> Result<GameLiftPlacementSnapshot, GameLiftApiError>;

    /// Applies the configured GameLift fleet lifecycle sequence.
    fn apply_lifecycle(
        &mut self,
        action: GameLiftLifecycleAction,
    ) -> Result<GameLiftLifecycleSnapshot, GameLiftApiError>;

    /// Describes the fleet lifecycle state after an ambiguous mutation result.
    fn describe_lifecycle(
        &mut self,
        action: GameLiftLifecycleAction,
    ) -> Result<GameLiftLifecycleSnapshot, GameLiftApiError>;

    /// Reads a point-in-time fleet-capacity measurement.
    fn describe_capacity(&mut self) -> Result<GameLiftCapacitySnapshot, GameLiftApiError>;
}

/// Explicit non-production candidate configuration.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GameLiftSpikeConfig {
    home_region: RegionId,
    artifact: ArtifactFingerprint,
    compatibility: CompatibilityFingerprint,
    queue_token: u64,
    fleet_token: u64,
}

impl GameLiftSpikeConfig {
    /// Creates a scoped disposable configuration from opaque private tokens.
    #[must_use]
    pub const fn new(
        home_region: RegionId,
        artifact: ArtifactFingerprint,
        compatibility: CompatibilityFingerprint,
        queue_token: u64,
        fleet_token: u64,
    ) -> Self {
        Self {
            home_region,
            artifact,
            compatibility,
            queue_token,
            fleet_token,
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct PlacementRecord {
    context: AdapterRequestContext,
    request: PlacementRequest,
    receipt: PlacementReceipt,
    allocation_resolved: bool,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct CancellationRecord {
    context: AdapterRequestContext,
    receipt: PlacementReceipt,
    status: PlacementStatus,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct LifecycleRecord {
    context: AdapterRequestContext,
    request: ProcessLifecycleRequest,
    receipt: LifecycleReceipt,
    status: LifecycleStatus,
}

/// Disposable GameLift adapter implementing only provider-neutral boundaries.
#[derive(Debug)]
pub struct GameLiftSpikeAdapter<A> {
    config: GameLiftSpikeConfig,
    api: A,
    placements: Vec<PlacementRecord>,
    cancellations: Vec<CancellationRecord>,
    lifecycles: Vec<LifecycleRecord>,
}

impl<A> GameLiftSpikeAdapter<A> {
    /// Creates an empty disposable adapter around an injected transport.
    #[must_use]
    pub const fn new(config: GameLiftSpikeConfig, api: A) -> Self {
        Self {
            config,
            api,
            placements: Vec::new(),
            cancellations: Vec::new(),
            lifecycles: Vec::new(),
        }
    }

    /// Returns the injected transport for test and evidence inspection.
    #[must_use]
    pub const fn api(&self) -> &A {
        &self.api
    }

    fn observe(
        context: AdapterRequestContext,
        boundary: AdapterBoundary,
        kind: AdapterObservationKind,
        observer: &mut dyn AdapterObserver,
    ) {
        observer.record(AdapterObservation::new(context, boundary, kind));
    }

    fn fail(
        context: AdapterRequestContext,
        boundary: AdapterBoundary,
        class: AdapterFailureClass,
        observer: &mut dyn AdapterObserver,
    ) -> AdapterError {
        Self::observe(
            context,
            boundary,
            AdapterObservationKind::Failed(class),
            observer,
        );
        AdapterError::from_class(class)
    }

    fn map_failure(failure: GameLiftApiFailure) -> AdapterFailureClass {
        match failure {
            GameLiftApiFailure::InvalidRequest => AdapterFailureClass::InvalidRequest,
            GameLiftApiFailure::Unauthorized => AdapterFailureClass::Unauthorized,
            GameLiftApiFailure::Conflict => AdapterFailureClass::Conflict,
            GameLiftApiFailure::Throttled => AdapterFailureClass::Throttled,
            GameLiftApiFailure::Unavailable => AdapterFailureClass::Unavailable,
            GameLiftApiFailure::AmbiguousCompletion => AdapterFailureClass::AmbiguousCompletion,
            GameLiftApiFailure::Internal => AdapterFailureClass::Internal,
        }
    }

    fn scope_matches(&self, context: AdapterRequestContext) -> bool {
        context.region_id() == self.config.home_region
            && context.artifact_fingerprint() == self.config.artifact
            && context.compatibility_fingerprint() == self.config.compatibility
    }

    fn placement_scope_matches(
        &self,
        context: AdapterRequestContext,
        request: PlacementRequest,
    ) -> bool {
        self.scope_matches(context)
            && context.contract_id() == ContractId::Placement
            && context.region_id() == request.region_id()
            && context.realm_id() == Some(request.realm_id())
            && context.artifact_fingerprint() == request.artifact()
    }

    fn placement_index(&self, operation_id: OperationId) -> Option<usize> {
        self.placements
            .iter()
            .position(|entry| entry.receipt.operation_id() == operation_id)
    }

    fn cancellation_index(&self, operation_id: OperationId) -> Option<usize> {
        self.cancellations
            .iter()
            .position(|entry| entry.context.operation_id() == operation_id)
    }

    fn placement_resource_scope_matches(
        &self,
        context: AdapterRequestContext,
        record: PlacementRecord,
    ) -> bool {
        self.scope_matches(context)
            && context.contract_id() == ContractId::Placement
            && context.region_id() == record.context.region_id()
            && context.realm_id() == record.context.realm_id()
            && context.artifact_fingerprint() == record.context.artifact_fingerprint()
            && context.compatibility_fingerprint() == record.context.compatibility_fingerprint()
    }

    fn lifecycle_scope_matches(
        &self,
        context: AdapterRequestContext,
        request: ProcessLifecycleRequest,
    ) -> bool {
        let (region_id, artifact) = match request {
            ProcessLifecycleRequest::EnsureReady {
                region_id,
                artifact,
            }
            | ProcessLifecycleRequest::Drain {
                region_id,
                artifact,
            }
            | ProcessLifecycleRequest::Retire {
                region_id,
                artifact,
            } => (region_id, artifact),
        };
        self.scope_matches(context)
            && context.contract_id() == ContractId::Deployment
            && context.region_id() == region_id
            && context.realm_id().is_none()
            && context.artifact_fingerprint() == artifact
    }

    fn lifecycle_record_scope_matches(
        &self,
        context: AdapterRequestContext,
        record: LifecycleRecord,
    ) -> bool {
        self.scope_matches(context)
            && context.contract_id() == ContractId::Deployment
            && context.region_id() == record.context.region_id()
            && context.realm_id() == record.context.realm_id()
            && context.artifact_fingerprint() == record.context.artifact_fingerprint()
            && context.compatibility_fingerprint() == record.context.compatibility_fingerprint()
    }

    fn translate_snapshot(
        snapshot: GameLiftPlacementSnapshot,
    ) -> Result<PlacementStatus, AdapterFailureClass> {
        match snapshot.state {
            GameLiftPlacementState::Pending => Ok(PlacementStatus::Pending),
            GameLiftPlacementState::Ready {
                lease_epoch,
                endpoint_token,
            } => Ok(PlacementStatus::Ready {
                allocation_id: AllocationId::new(snapshot.allocation_token)
                    .ok_or(AdapterFailureClass::Internal)?,
                lease_epoch: LeaseEpoch::new(lease_epoch).ok_or(AdapterFailureClass::Internal)?,
                endpoint: EndpointHandle::new(endpoint_token)
                    .ok_or(AdapterFailureClass::Internal)?,
            }),
            GameLiftPlacementState::Cancelled => Ok(PlacementStatus::Cancelled),
            GameLiftPlacementState::Failed(failure) => {
                Ok(PlacementStatus::Failed(Self::map_failure(failure)))
            }
        }
    }

    fn snapshot_identity_matches(
        snapshot: GameLiftPlacementSnapshot,
        record: PlacementRecord,
    ) -> bool {
        snapshot.placement_token == record.receipt.operation_id().get()
            && (!record.allocation_resolved
                || snapshot.allocation_token == record.receipt.allocation_id().get())
            && snapshot.region_id == record.request.region_id()
            && snapshot.realm_id == record.request.realm_id()
            && snapshot.artifact == record.request.artifact()
            && snapshot.compatibility == record.context.compatibility_fingerprint()
    }
}

impl<A: GameLiftApi> GameLiftSpikeAdapter<A> {
    /// Reconciles an ambiguous placement by its original operation identity.
    pub fn reconcile_operation(
        &mut self,
        context: AdapterRequestContext,
        operation_id: OperationId,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlacementStatus, AdapterError> {
        Self::observe(
            context,
            AdapterBoundary::Placement,
            AdapterObservationKind::Started,
            observer,
        );
        let index = self.placement_index(operation_id).ok_or_else(|| {
            Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::InvalidRequest,
                observer,
            )
        })?;
        let record = self.placements[index];
        if !self.placement_resource_scope_matches(context, record) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        let snapshot = self
            .api
            .describe_game_session_placement(operation_id.get())
            .map_err(|error| {
                Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    Self::map_failure(error.failure()),
                    observer,
                )
            })?;
        if !Self::snapshot_identity_matches(snapshot, record) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        let status = Self::translate_snapshot(snapshot)
            .map_err(|class| Self::fail(context, AdapterBoundary::Placement, class, observer))?;
        if !record.allocation_resolved {
            if let PlacementStatus::Ready { allocation_id, .. } = status {
                self.placements[index].receipt = PlacementReceipt::new(operation_id, allocation_id);
                self.placements[index].allocation_resolved = true;
            }
        }
        Self::observe(
            context,
            AdapterBoundary::Placement,
            observation_kind_for_placement(status),
            observer,
        );
        Ok(status)
    }
}

impl<A: GameLiftApi> PlacementAdapter for GameLiftSpikeAdapter<A> {
    fn submit(
        &mut self,
        context: AdapterRequestContext,
        request: PlacementRequest,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlacementReceipt, AdapterError> {
        Self::observe(
            context,
            AdapterBoundary::Placement,
            AdapterObservationKind::Started,
            observer,
        );
        if let Some(index) = self.placement_index(context.operation_id()) {
            let existing = self.placements[index];
            if !existing.context.has_same_retry_invariants(context) || existing.request != request {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterFailureClass::Conflict,
                    observer,
                ));
            }
            Self::observe(
                context,
                AdapterBoundary::Placement,
                AdapterObservationKind::Duplicate,
                observer,
            );
            return Ok(existing.receipt);
        }
        if !self.placement_scope_matches(context, request) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }

        let provider_request = GameLiftStartPlacement {
            placement_token: context.operation_id().get(),
            queue_token: self.config.queue_token,
            region_id: request.region_id(),
            realm_id: request.realm_id(),
            artifact: request.artifact(),
            compatibility: context.compatibility_fingerprint(),
        };
        let provisional_receipt = PlacementReceipt::new(
            context.operation_id(),
            AllocationId::new(context.operation_id().get()).expect("operation identity is nonzero"),
        );
        let snapshot = match self.api.start_game_session_placement(provider_request) {
            Ok(snapshot) => snapshot,
            Err(error) => {
                if error.failure() == GameLiftApiFailure::AmbiguousCompletion {
                    self.placements.push(PlacementRecord {
                        context,
                        request,
                        receipt: provisional_receipt,
                        allocation_resolved: false,
                    });
                }
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    Self::map_failure(error.failure()),
                    observer,
                ));
            }
        };
        if snapshot.placement_token != context.operation_id().get()
            || snapshot.region_id != request.region_id()
            || snapshot.realm_id != request.realm_id()
            || snapshot.artifact != request.artifact()
            || snapshot.compatibility != context.compatibility_fingerprint()
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        let status = Self::translate_snapshot(snapshot)
            .map_err(|class| Self::fail(context, AdapterBoundary::Placement, class, observer))?;
        let allocation_id = AllocationId::new(snapshot.allocation_token).ok_or_else(|| {
            Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Internal,
                observer,
            )
        })?;
        let receipt = PlacementReceipt::new(context.operation_id(), allocation_id);
        self.placements.push(PlacementRecord {
            context,
            request,
            receipt,
            allocation_resolved: true,
        });
        Self::observe(
            context,
            AdapterBoundary::Placement,
            observation_kind_for_placement(status),
            observer,
        );
        Ok(receipt)
    }

    fn status(
        &mut self,
        context: AdapterRequestContext,
        receipt: PlacementReceipt,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlacementStatus, AdapterError> {
        let index = self
            .placement_index(receipt.operation_id())
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterFailureClass::InvalidRequest,
                    observer,
                )
            })?;
        if self.placements[index].receipt != receipt {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        self.reconcile_operation(context, receipt.operation_id(), observer)
    }

    fn cancel(
        &mut self,
        context: AdapterRequestContext,
        receipt: PlacementReceipt,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlacementStatus, AdapterError> {
        Self::observe(
            context,
            AdapterBoundary::Placement,
            AdapterObservationKind::Started,
            observer,
        );
        if let Some(index) = self.cancellation_index(context.operation_id()) {
            let existing = self.cancellations[index];
            if !existing.context.has_same_retry_invariants(context) || existing.receipt != receipt {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterFailureClass::Conflict,
                    observer,
                ));
            }
            let status = if matches!(
                existing.status,
                PlacementStatus::Pending
                    | PlacementStatus::Ready { .. }
                    | PlacementStatus::Failed(AdapterFailureClass::AmbiguousCompletion)
            ) {
                let placement = self
                    .placements
                    .iter()
                    .find(|entry| entry.receipt == receipt)
                    .copied()
                    .ok_or_else(|| {
                        Self::fail(
                            context,
                            AdapterBoundary::Placement,
                            AdapterFailureClass::InvalidRequest,
                            observer,
                        )
                    })?;
                let snapshot = self
                    .api
                    .describe_game_session_placement(receipt.operation_id().get())
                    .map_err(|error| {
                        Self::fail(
                            context,
                            AdapterBoundary::Placement,
                            Self::map_failure(error.failure()),
                            observer,
                        )
                    })?;
                if !Self::snapshot_identity_matches(snapshot, placement) {
                    return Err(Self::fail(
                        context,
                        AdapterBoundary::Placement,
                        AdapterFailureClass::Conflict,
                        observer,
                    ));
                }
                let reconciled = Self::translate_snapshot(snapshot).map_err(|class| {
                    Self::fail(context, AdapterBoundary::Placement, class, observer)
                })?;
                self.cancellations[index].status = reconciled;
                reconciled
            } else {
                existing.status
            };
            Self::observe(
                context,
                AdapterBoundary::Placement,
                AdapterObservationKind::Duplicate,
                observer,
            );
            return Ok(status);
        }
        let index = self
            .placement_index(receipt.operation_id())
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterFailureClass::InvalidRequest,
                    observer,
                )
            })?;
        let record = self.placements[index];
        if record.receipt != receipt || !self.placement_resource_scope_matches(context, record) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        let snapshot = match self
            .api
            .stop_game_session_placement(receipt.operation_id().get())
        {
            Ok(snapshot) => snapshot,
            Err(error) => {
                let class = Self::map_failure(error.failure());
                if class == AdapterFailureClass::AmbiguousCompletion {
                    self.cancellations.push(CancellationRecord {
                        context,
                        receipt,
                        status: PlacementStatus::Failed(class),
                    });
                }
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    class,
                    observer,
                ));
            }
        };
        if !Self::snapshot_identity_matches(snapshot, record) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        let status = Self::translate_snapshot(snapshot)
            .map_err(|class| Self::fail(context, AdapterBoundary::Placement, class, observer))?;
        if status != PlacementStatus::Cancelled {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        self.cancellations.push(CancellationRecord {
            context,
            receipt,
            status,
        });
        Self::observe(
            context,
            AdapterBoundary::Placement,
            observation_kind_for_placement(status),
            observer,
        );
        Ok(status)
    }
}

impl<A: GameLiftApi> DeploymentAdapter for GameLiftSpikeAdapter<A> {
    fn submit_lifecycle(
        &mut self,
        context: AdapterRequestContext,
        request: ProcessLifecycleRequest,
        observer: &mut dyn AdapterObserver,
    ) -> Result<LifecycleReceipt, AdapterError> {
        Self::observe(
            context,
            AdapterBoundary::Deployment,
            AdapterObservationKind::Started,
            observer,
        );
        if !self.lifecycle_scope_matches(context, request) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        if let Some(index) = self
            .lifecycles
            .iter()
            .position(|entry| entry.receipt.operation_id() == context.operation_id())
        {
            let existing = self.lifecycles[index];
            if !existing.context.has_same_retry_invariants(context) || existing.request != request {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    AdapterFailureClass::Conflict,
                    observer,
                ));
            }
            if existing.status == LifecycleStatus::Failed(AdapterFailureClass::AmbiguousCompletion)
            {
                let snapshot = self
                    .api
                    .describe_lifecycle(GameLiftLifecycleAction {
                        fleet_token: self.config.fleet_token,
                        request,
                    })
                    .map_err(|error| {
                        Self::fail(
                            context,
                            AdapterBoundary::Deployment,
                            Self::map_failure(error.failure()),
                            observer,
                        )
                    })?;
                if snapshot.fleet_token != self.config.fleet_token || snapshot.request != request {
                    return Err(Self::fail(
                        context,
                        AdapterBoundary::Deployment,
                        AdapterFailureClass::Conflict,
                        observer,
                    ));
                }
                self.lifecycles[index].status = snapshot.status;
            }
            Self::observe(
                context,
                AdapterBoundary::Deployment,
                AdapterObservationKind::Duplicate,
                observer,
            );
            return Ok(existing.receipt);
        }
        let receipt = LifecycleReceipt::new(context.operation_id());
        let snapshot = match self.api.apply_lifecycle(GameLiftLifecycleAction {
            fleet_token: self.config.fleet_token,
            request,
        }) {
            Ok(snapshot) => snapshot,
            Err(error) => {
                let class = Self::map_failure(error.failure());
                if class == AdapterFailureClass::AmbiguousCompletion {
                    self.lifecycles.push(LifecycleRecord {
                        context,
                        request,
                        receipt,
                        status: LifecycleStatus::Failed(class),
                    });
                }
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    class,
                    observer,
                ));
            }
        };
        if snapshot.fleet_token != self.config.fleet_token || snapshot.request != request {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        let status = snapshot.status;
        self.lifecycles.push(LifecycleRecord {
            context,
            request,
            receipt,
            status,
        });
        Self::observe(
            context,
            AdapterBoundary::Deployment,
            observation_kind_for_lifecycle(status),
            observer,
        );
        Ok(receipt)
    }

    fn lifecycle_status(
        &mut self,
        context: AdapterRequestContext,
        receipt: LifecycleReceipt,
        observer: &mut dyn AdapterObserver,
    ) -> Result<LifecycleStatus, AdapterError> {
        let index = self
            .lifecycles
            .iter()
            .position(|entry| entry.receipt == receipt)
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    AdapterFailureClass::InvalidRequest,
                    observer,
                )
            })?;
        let record = self.lifecycles[index];
        if !self.lifecycle_record_scope_matches(context, record) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        let status = if matches!(
            record.status,
            LifecycleStatus::Pending
                | LifecycleStatus::Failed(AdapterFailureClass::AmbiguousCompletion)
        ) {
            let snapshot = self
                .api
                .describe_lifecycle(GameLiftLifecycleAction {
                    fleet_token: self.config.fleet_token,
                    request: record.request,
                })
                .map_err(|error| {
                    Self::fail(
                        context,
                        AdapterBoundary::Deployment,
                        Self::map_failure(error.failure()),
                        observer,
                    )
                })?;
            if snapshot.fleet_token != self.config.fleet_token || snapshot.request != record.request
            {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    AdapterFailureClass::Conflict,
                    observer,
                ));
            }
            self.lifecycles[index].status = snapshot.status;
            snapshot.status
        } else {
            record.status
        };
        Self::observe(
            context,
            AdapterBoundary::Deployment,
            observation_kind_for_lifecycle(status),
            observer,
        );
        Ok(status)
    }
}

impl<A: GameLiftApi> OperationsAdapter for GameLiftSpikeAdapter<A> {
    fn observe_capacity(
        &mut self,
        context: AdapterRequestContext,
        region_id: RegionId,
        artifact: ArtifactFingerprint,
        observer: &mut dyn AdapterObserver,
    ) -> Result<CapacityObservation, AdapterError> {
        Self::observe(
            context,
            AdapterBoundary::Operations,
            AdapterObservationKind::Started,
            observer,
        );
        if context.contract_id() != ContractId::Capacity
            || context.realm_id().is_some()
            || context.region_id() != region_id
            || context.artifact_fingerprint() != artifact
            || !self.scope_matches(context)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Operations,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        let snapshot = self.api.describe_capacity().map_err(|error| {
            Self::fail(
                context,
                AdapterBoundary::Operations,
                Self::map_failure(error.failure()),
                observer,
            )
        })?;
        if snapshot.region_id != region_id
            || snapshot.artifact != artifact
            || snapshot.compatibility != context.compatibility_fingerprint()
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Operations,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        Self::observe(
            context,
            AdapterBoundary::Operations,
            AdapterObservationKind::Succeeded,
            observer,
        );
        Ok(CapacityObservation::new(
            region_id,
            artifact,
            snapshot.ready_processes,
            snapshot.allocated_processes,
            snapshot.pending_operations,
        ))
    }
}

fn observation_kind_for_placement(status: PlacementStatus) -> AdapterObservationKind {
    match status {
        PlacementStatus::Pending => AdapterObservationKind::Pending,
        PlacementStatus::Ready { .. } | PlacementStatus::Cancelled => {
            AdapterObservationKind::Succeeded
        }
        PlacementStatus::Failed(class) => AdapterObservationKind::Failed(class),
    }
}

fn observation_kind_for_lifecycle(status: LifecycleStatus) -> AdapterObservationKind {
    match status {
        LifecycleStatus::Pending => AdapterObservationKind::Pending,
        LifecycleStatus::Complete => AdapterObservationKind::Succeeded,
        LifecycleStatus::Failed(class) => AdapterObservationKind::Failed(class),
    }
}
