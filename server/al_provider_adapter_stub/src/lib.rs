//! Disposable in-memory adapter for provider-boundary contract tests.
//!
//! This crate is deliberately outside `al_server_core`. It has no provider SDK,
//! network client, credentials, production configuration, or gameplay rules. A
//! real candidate adapter may replace this crate without changing authoritative
//! simulation code.

#![forbid(unsafe_code)]
#![deny(missing_docs)]

use al_server_core::provider_contracts::{
    AccountId, AdapterBoundary, AdapterError, AdapterFailureClass, AdapterObservation,
    AdapterObservationKind, AdapterObserver, AdapterRequestContext, AllocationId,
    ArtifactFingerprint, AssertionHandle, CapacityObservation, CompatibilityFingerprint,
    ContractId, DeploymentAdapter, ExternalIdentityAdapter, IdentityResolution, LifecycleReceipt,
    LifecycleStatus, OperationsAdapter, PlacementAdapter, PlacementReceipt, PlacementRequest,
    PlacementStatus, PlatformAdapter, PlatformEvidenceHandle, PlatformEvidenceResult,
    ProcessLifecycleRequest, RegionId,
};

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct PlacementRecord {
    context: AdapterRequestContext,
    request: PlacementRequest,
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

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct CancellationRecord {
    context: AdapterRequestContext,
    receipt: PlacementReceipt,
    status: PlacementStatus,
}

/// Explicit region and immutable-artifact scope accepted by the disposable stub.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct StubProviderScope {
    region_id: RegionId,
    artifact: ArtifactFingerprint,
    compatibility: CompatibilityFingerprint,
}

impl StubProviderScope {
    /// Creates an accepted provider-neutral scope.
    #[must_use]
    pub const fn new(
        region_id: RegionId,
        artifact: ArtifactFingerprint,
        compatibility: CompatibilityFingerprint,
    ) -> Self {
        Self {
            region_id,
            artifact,
            compatibility,
        }
    }
}

/// Disposable deterministic adapter used to verify dependency direction,
/// idempotency, reconciliation, and sanitized observations.
#[derive(Debug)]
pub struct StubProviderAdapter {
    scope: StubProviderScope,
    placements: Vec<PlacementRecord>,
    lifecycles: Vec<LifecycleRecord>,
    cancellations: Vec<CancellationRecord>,
}

impl StubProviderAdapter {
    /// Creates an empty disposable adapter with an explicit accepted scope.
    #[must_use]
    pub const fn new(scope: StubProviderScope) -> Self {
        Self {
            scope,
            placements: Vec::new(),
            lifecycles: Vec::new(),
            cancellations: Vec::new(),
        }
    }

    fn placement_index(&self, operation_id: u64) -> Option<usize> {
        self.placements
            .iter()
            .position(|entry| entry.receipt.operation_id().get() == operation_id)
    }

    fn lifecycle_index(&self, operation_id: u64) -> Option<usize> {
        self.lifecycles
            .iter()
            .position(|entry| entry.receipt.operation_id().get() == operation_id)
    }

    fn cancellation_index(&self, operation_id: u64) -> Option<usize> {
        self.cancellations
            .iter()
            .position(|entry| entry.context.operation_id().get() == operation_id)
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

    fn configured_scope_matches(&self, context: AdapterRequestContext) -> bool {
        context.region_id() == self.scope.region_id
            && context.artifact_fingerprint() == self.scope.artifact
            && context.compatibility_fingerprint() == self.scope.compatibility
    }

    fn placement_scope_matches(
        &self,
        context: AdapterRequestContext,
        request: PlacementRequest,
    ) -> bool {
        self.configured_scope_matches(context)
            && context.contract_id() == ContractId::Placement
            && context.region_id() == request.region_id()
            && context.realm_id() == Some(request.realm_id())
            && context.artifact_fingerprint() == request.artifact()
    }

    fn placement_record_scope_matches(
        &self,
        context: AdapterRequestContext,
        record: PlacementRecord,
    ) -> bool {
        self.configured_scope_matches(context)
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
        self.configured_scope_matches(context)
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
        self.configured_scope_matches(context)
            && context.contract_id() == ContractId::Deployment
            && context.region_id() == record.context.region_id()
            && context.realm_id() == record.context.realm_id()
            && context.artifact_fingerprint() == record.context.artifact_fingerprint()
            && context.compatibility_fingerprint() == record.context.compatibility_fingerprint()
    }
}

impl ExternalIdentityAdapter for StubProviderAdapter {
    fn verify_assertion(
        &mut self,
        context: AdapterRequestContext,
        assertion: AssertionHandle,
        observer: &mut dyn AdapterObserver,
    ) -> Result<IdentityResolution, AdapterError> {
        Self::observe(
            context,
            AdapterBoundary::Identity,
            AdapterObservationKind::Started,
            observer,
        );
        if context.contract_id() != ContractId::Identity
            || context.realm_id().is_some()
            || !self.configured_scope_matches(context)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Identity,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        let account_id = (assertion.get() != u64::MAX)
            .then(|| AccountId::new(assertion.get()))
            .flatten()
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Identity,
                    AdapterFailureClass::Internal,
                    observer,
                )
            })?;
        Self::observe(
            context,
            AdapterBoundary::Identity,
            AdapterObservationKind::Succeeded,
            observer,
        );
        Ok(IdentityResolution::new(account_id))
    }
}

impl PlacementAdapter for StubProviderAdapter {
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
        if let Some(index) = self.placement_index(context.operation_id().get()) {
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

        let allocation_id = AllocationId::new(context.operation_id().get()).ok_or_else(|| {
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
            status: PlacementStatus::Pending,
        });
        Self::observe(
            context,
            AdapterBoundary::Placement,
            AdapterObservationKind::Pending,
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
        Self::observe(
            context,
            AdapterBoundary::Placement,
            AdapterObservationKind::Started,
            observer,
        );
        let index = self
            .placement_index(receipt.operation_id().get())
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterFailureClass::InvalidRequest,
                    observer,
                )
            })?;
        let entry = self.placements[index];
        if entry.receipt != receipt {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Conflict,
                observer,
            ));
        }
        if !self.placement_record_scope_matches(context, entry) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        let status = entry.status;
        let kind = match status {
            PlacementStatus::Pending => AdapterObservationKind::Pending,
            PlacementStatus::Failed(class) => AdapterObservationKind::Failed(class),
            PlacementStatus::Ready { .. } | PlacementStatus::Cancelled => {
                AdapterObservationKind::Succeeded
            }
        };
        Self::observe(context, AdapterBoundary::Placement, kind, observer);
        Ok(status)
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
        if let Some(index) = self.cancellation_index(context.operation_id().get()) {
            let existing = self.cancellations[index];
            if !existing.context.has_same_retry_invariants(context) || existing.receipt != receipt {
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
            return Ok(existing.status);
        }

        let index = self
            .placement_index(receipt.operation_id().get())
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
        if !self.placement_record_scope_matches(context, self.placements[index]) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        self.placements[index].status = PlacementStatus::Cancelled;
        self.cancellations.push(CancellationRecord {
            context,
            receipt,
            status: PlacementStatus::Cancelled,
        });
        Self::observe(
            context,
            AdapterBoundary::Placement,
            AdapterObservationKind::Succeeded,
            observer,
        );
        Ok(PlacementStatus::Cancelled)
    }
}

impl DeploymentAdapter for StubProviderAdapter {
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
        if let Some(index) = self.lifecycle_index(context.operation_id().get()) {
            let existing = self.lifecycles[index];
            if !existing.context.has_same_retry_invariants(context) || existing.request != request {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    AdapterFailureClass::Conflict,
                    observer,
                ));
            }
            Self::observe(
                context,
                AdapterBoundary::Deployment,
                AdapterObservationKind::Duplicate,
                observer,
            );
            return Ok(existing.receipt);
        }

        if !self.lifecycle_scope_matches(context, request) {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }

        let receipt = LifecycleReceipt::new(context.operation_id());
        self.lifecycles.push(LifecycleRecord {
            context,
            request,
            receipt,
            status: LifecycleStatus::Pending,
        });
        Self::observe(
            context,
            AdapterBoundary::Deployment,
            AdapterObservationKind::Pending,
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
        Self::observe(
            context,
            AdapterBoundary::Deployment,
            AdapterObservationKind::Started,
            observer,
        );
        let index = self
            .lifecycle_index(receipt.operation_id().get())
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
        let status = record.status;
        let kind = match status {
            LifecycleStatus::Pending => AdapterObservationKind::Pending,
            LifecycleStatus::Complete => AdapterObservationKind::Succeeded,
            LifecycleStatus::Failed(class) => AdapterObservationKind::Failed(class),
        };
        Self::observe(context, AdapterBoundary::Deployment, kind, observer);
        Ok(status)
    }
}

impl OperationsAdapter for StubProviderAdapter {
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
            || context.region_id() != region_id
            || context.artifact_fingerprint() != artifact
            || context.realm_id().is_some()
            || !self.configured_scope_matches(context)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Operations,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        let observation = CapacityObservation::new(
            region_id,
            artifact,
            0,
            0,
            self.placements
                .iter()
                .filter(|entry| {
                    matches!(entry.status, PlacementStatus::Pending)
                        && entry.request.region_id() == region_id
                        && entry.request.artifact() == artifact
                })
                .count() as u64,
        );
        Self::observe(
            context,
            AdapterBoundary::Operations,
            AdapterObservationKind::Succeeded,
            observer,
        );
        Ok(observation)
    }
}

impl PlatformAdapter for StubProviderAdapter {
    fn verify_evidence(
        &mut self,
        context: AdapterRequestContext,
        account_id: AccountId,
        _evidence: PlatformEvidenceHandle,
        observer: &mut dyn AdapterObserver,
    ) -> Result<PlatformEvidenceResult, AdapterError> {
        Self::observe(
            context,
            AdapterBoundary::Platform,
            AdapterObservationKind::Started,
            observer,
        );
        if context.contract_id() != ContractId::Platform
            || context.realm_id().is_some()
            || !self.configured_scope_matches(context)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Platform,
                AdapterFailureClass::Unauthorized,
                observer,
            ));
        }
        Self::observe(
            context,
            AdapterBoundary::Platform,
            AdapterObservationKind::Succeeded,
            observer,
        );
        Ok(PlatformEvidenceResult::new(
            context.operation_id(),
            account_id,
        ))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use al_server_core::provider_contracts::{
        ActorId, AuthorizationContextId, CompatibilityFingerprint, ContractId, CorrelationId,
        OperationId, PolicyVersion, RealmId, RetryDisposition, SchemaVersion, ServiceId, SessionId,
    };

    #[derive(Debug, Default)]
    struct RecordingObserver(Vec<AdapterObservation>);

    impl AdapterObserver for RecordingObserver {
        fn record(&mut self, observation: AdapterObservation) {
            self.0.push(observation);
        }
    }

    fn required<T>(value: u64, constructor: fn(u64) -> Option<T>) -> T {
        constructor(value).expect("test identity must be nonzero")
    }

    fn stub_adapter() -> StubProviderAdapter {
        StubProviderAdapter::new(StubProviderScope::new(
            required(3, RegionId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
        ))
    }

    fn context_with_authorization(
        contract_id: ContractId,
        operation_id: u64,
        attempt: u32,
        authorization_context_id: u64,
    ) -> AdapterRequestContext {
        let realm_id = if contract_id == ContractId::Placement {
            Some(required(4, RealmId::new))
        } else {
            None
        };
        context_with_scope(
            contract_id,
            operation_id,
            attempt,
            authorization_context_id,
            required(3, RegionId::new),
            realm_id,
            required(5, ArtifactFingerprint::new),
        )
    }

    #[allow(clippy::too_many_arguments)]
    fn context_with_scope(
        contract_id: ContractId,
        operation_id: u64,
        attempt: u32,
        authorization_context_id: u64,
        region_id: RegionId,
        realm_id: Option<RealmId>,
        artifact_fingerprint: ArtifactFingerprint,
    ) -> AdapterRequestContext {
        AdapterRequestContext::new(
            contract_id,
            required(operation_id, OperationId::new),
            required(12, CorrelationId::new),
            required(13, ActorId::new),
            required(14, ServiceId::new),
            required(authorization_context_id, AuthorizationContextId::new),
            required(16, PolicyVersion::new),
            region_id,
            realm_id,
            required(17, SchemaVersion::new),
            artifact_fingerprint,
            required(19, CompatibilityFingerprint::new),
            attempt,
        )
    }

    fn context_for_contract(
        contract_id: ContractId,
        operation_id: u64,
        attempt: u32,
    ) -> AdapterRequestContext {
        context_with_authorization(contract_id, operation_id, attempt, 15)
    }

    fn context_for(operation_id: u64, attempt: u32) -> AdapterRequestContext {
        context_for_contract(ContractId::Placement, operation_id, attempt)
    }

    fn context(attempt: u32) -> AdapterRequestContext {
        context_for(11, attempt)
    }

    fn request() -> PlacementRequest {
        PlacementRequest::new(
            required(1, AccountId::new),
            required(2, SessionId::new),
            required(3, RegionId::new),
            required(4, RealmId::new),
            required(5, ArtifactFingerprint::new),
        )
    }

    #[test]
    fn duplicate_placement_returns_same_receipt() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();

        let first = adapter
            .submit(context(0), request(), &mut observer)
            .expect("first submit must succeed");
        let duplicate = adapter
            .submit(context(1), request(), &mut observer)
            .expect("same operation retry must deduplicate");

        assert_eq!(first, duplicate);
        assert!(observer.0.iter().any(|event| {
            event.kind() == AdapterObservationKind::Duplicate
                && event.boundary() == AdapterBoundary::Placement
        }));
    }

    #[test]
    fn cross_region_placement_fails_closed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let mismatched_context = context_with_scope(
            ContractId::Placement,
            20,
            0,
            15,
            required(30, RegionId::new),
            Some(required(4, RealmId::new)),
            required(5, ArtifactFingerprint::new),
        );

        let error = adapter
            .submit(mismatched_context, request(), &mut observer)
            .expect_err("cross-region placement must fail closed");

        assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::Unauthorized
            ))
        );
    }

    #[test]
    fn reused_operation_with_different_payload_fails_closed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        adapter
            .submit(context(0), request(), &mut observer)
            .expect("first submit must succeed");
        let changed = PlacementRequest::new(
            required(1, AccountId::new),
            required(2, SessionId::new),
            required(3, RegionId::new),
            required(9, RealmId::new),
            required(5, ArtifactFingerprint::new),
        );

        let error = adapter
            .submit(context(1), changed, &mut observer)
            .expect_err("payload drift must conflict");

        assert_eq!(error.class(), AdapterFailureClass::Conflict);
        assert_eq!(error.retry(), RetryDisposition::ReconcileFirst);
    }

    #[test]
    fn reused_operation_with_authorization_context_drift_fails_closed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        adapter
            .submit(context(0), request(), &mut observer)
            .expect("first submit must succeed");

        let error = adapter
            .submit(
                context_with_authorization(ContractId::Placement, 11, 1, 115),
                request(),
                &mut observer,
            )
            .expect_err("authorization-context drift must conflict");

        assert_eq!(error.class(), AdapterFailureClass::Conflict);
        assert_eq!(error.retry(), RetryDisposition::ReconcileFirst);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::Conflict
            ))
        );
    }

    #[test]
    fn lifecycle_retry_invariant_drift_conflicts_and_emits_failed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let request = ProcessLifecycleRequest::EnsureReady {
            region_id: required(3, RegionId::new),
            artifact: required(5, ArtifactFingerprint::new),
        };
        adapter
            .submit_lifecycle(
                context_for_contract(ContractId::Deployment, 81, 0),
                request,
                &mut observer,
            )
            .expect("first lifecycle submit must succeed");

        let error = adapter
            .submit_lifecycle(
                context_with_authorization(ContractId::Deployment, 81, 1, 115),
                request,
                &mut observer,
            )
            .expect_err("lifecycle authorization-context drift must conflict");

        assert_eq!(error.class(), AdapterFailureClass::Conflict);
        assert_eq!(error.retry(), RetryDisposition::ReconcileFirst);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::Conflict
            ))
        );
    }

    #[test]
    fn cancellation_retry_invariant_drift_conflicts_and_emits_failed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let receipt = adapter
            .submit(context(0), request(), &mut observer)
            .expect("placement submit must succeed");
        adapter
            .cancel(context_for(82, 0), receipt, &mut observer)
            .expect("first cancellation must succeed");

        let error = adapter
            .cancel(
                context_with_authorization(ContractId::Placement, 82, 1, 115),
                receipt,
                &mut observer,
            )
            .expect_err("cancellation authorization-context drift must conflict");

        assert_eq!(error.class(), AdapterFailureClass::Conflict);
        assert_eq!(error.retry(), RetryDisposition::ReconcileFirst);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::Conflict
            ))
        );
    }

    #[test]
    fn cancellation_is_idempotent_and_does_not_mutate_realm_identity() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let receipt = adapter
            .submit(context(0), request(), &mut observer)
            .expect("submit must succeed");

        let first = adapter
            .cancel(context_for(21, 0), receipt, &mut observer)
            .expect("cancel must succeed");
        let duplicate = adapter
            .cancel(context_for(21, 1), receipt, &mut observer)
            .expect("duplicate cancel must succeed");

        assert_eq!(first, PlacementStatus::Cancelled);
        assert_eq!(duplicate, PlacementStatus::Cancelled);
        assert_eq!(request().realm_id(), required(4, RealmId::new));
        assert_eq!(adapter.cancellations.len(), 1);
        assert!(observer.0.iter().any(|event| {
            event.kind() == AdapterObservationKind::Duplicate
                && event.operation_id() == required(21, OperationId::new)
        }));
    }

    #[test]
    fn placement_status_rejects_mismatched_allocation_and_emits_failed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let receipt = adapter
            .submit(context(0), request(), &mut observer)
            .expect("submit must succeed");
        let mismatched =
            PlacementReceipt::new(receipt.operation_id(), required(999, AllocationId::new));

        let error = adapter
            .status(context_for(31, 0), mismatched, &mut observer)
            .expect_err("allocation mismatch must fail closed");

        assert_eq!(error.class(), AdapterFailureClass::Conflict);
        assert_eq!(error.retry(), RetryDisposition::ReconcileFirst);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::Conflict
            ))
        );
    }

    #[test]
    fn placement_status_and_cancel_reject_resource_scope_mismatch() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let receipt = adapter
            .submit(context(0), request(), &mut observer)
            .expect("submit must succeed");

        for operation_id in [32, 33] {
            let mismatched_context = context_with_scope(
                ContractId::Placement,
                operation_id,
                0,
                15,
                required(30, RegionId::new),
                Some(required(4, RealmId::new)),
                required(5, ArtifactFingerprint::new),
            );
            let error = if operation_id == 32 {
                adapter
                    .status(mismatched_context, receipt, &mut observer)
                    .expect_err("cross-region status must fail closed")
            } else {
                adapter
                    .cancel(mismatched_context, receipt, &mut observer)
                    .expect_err("cross-region cancellation must fail closed")
            };
            assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
        }
    }

    #[test]
    fn lifecycle_status_rejects_resource_scope_mismatch() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let request = ProcessLifecycleRequest::EnsureReady {
            region_id: required(3, RegionId::new),
            artifact: required(5, ArtifactFingerprint::new),
        };
        let receipt = adapter
            .submit_lifecycle(
                context_for_contract(ContractId::Deployment, 34, 0),
                request,
                &mut observer,
            )
            .expect("lifecycle submit must succeed");
        let mismatched_contexts = [
            context_with_scope(
                ContractId::Deployment,
                35,
                0,
                15,
                required(30, RegionId::new),
                None,
                required(5, ArtifactFingerprint::new),
            ),
            context_with_scope(
                ContractId::Deployment,
                36,
                0,
                15,
                required(3, RegionId::new),
                Some(required(4, RealmId::new)),
                required(5, ArtifactFingerprint::new),
            ),
        ];

        for mismatched_context in mismatched_contexts {
            let error = adapter
                .lifecycle_status(mismatched_context, receipt, &mut observer)
                .expect_err("lifecycle resource-scope mismatch must fail closed");
            assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
        }
    }

    #[test]
    fn cancellation_operation_payload_drift_conflicts() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let receipt = adapter
            .submit(context(0), request(), &mut observer)
            .expect("submit must succeed");
        adapter
            .cancel(context_for(41, 0), receipt, &mut observer)
            .expect("first cancellation must succeed");
        let changed_receipt =
            PlacementReceipt::new(receipt.operation_id(), required(998, AllocationId::new));

        let error = adapter
            .cancel(context_for(41, 1), changed_receipt, &mut observer)
            .expect_err("reused cancel operation with another receipt must conflict");

        assert_eq!(error.class(), AdapterFailureClass::Conflict);
        assert_eq!(error.retry(), RetryDisposition::ReconcileFirst);
        assert_eq!(adapter.cancellations.len(), 1);
    }

    #[test]
    fn unknown_placement_and_lifecycle_receipts_emit_failed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let unknown_placement = PlacementReceipt::new(
            required(901, OperationId::new),
            required(902, AllocationId::new),
        );

        let placement_error = adapter
            .status(context_for(51, 0), unknown_placement, &mut observer)
            .expect_err("unknown placement receipt must fail");
        assert_eq!(placement_error.class(), AdapterFailureClass::InvalidRequest);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::InvalidRequest
            ))
        );

        let lifecycle_error = adapter
            .lifecycle_status(
                context_for_contract(ContractId::Deployment, 52, 0),
                LifecycleReceipt::new(required(903, OperationId::new)),
                &mut observer,
            )
            .expect_err("unknown lifecycle receipt must fail");
        assert_eq!(lifecycle_error.class(), AdapterFailureClass::InvalidRequest);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::InvalidRequest
            ))
        );
    }

    #[test]
    fn identity_conversion_failure_emits_failed() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();

        let error = adapter
            .verify_assertion(
                context_for_contract(ContractId::Identity, 61, 0),
                required(u64::MAX, AssertionHandle::new),
                &mut observer,
            )
            .expect_err("untranslatable stub assertion must fail");

        assert_eq!(error.class(), AdapterFailureClass::Internal);
        assert_eq!(
            observer.0.last().map(|event| event.kind()),
            Some(AdapterObservationKind::Failed(
                AdapterFailureClass::Internal
            ))
        );
    }

    #[test]
    fn stateless_boundaries_reject_unconfigured_scope() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let wrong_compatibility = AdapterRequestContext::new(
            ContractId::Identity,
            required(62, OperationId::new),
            required(12, CorrelationId::new),
            required(13, ActorId::new),
            required(14, ServiceId::new),
            required(15, AuthorizationContextId::new),
            required(16, PolicyVersion::new),
            required(3, RegionId::new),
            None,
            required(17, SchemaVersion::new),
            required(5, ArtifactFingerprint::new),
            required(99, CompatibilityFingerprint::new),
            0,
        );

        let identity_error = adapter
            .verify_assertion(
                wrong_compatibility,
                required(1, AssertionHandle::new),
                &mut observer,
            )
            .expect_err("identity compatibility mismatch must fail closed");
        assert_eq!(identity_error.class(), AdapterFailureClass::Unauthorized);

        let platform_error = adapter
            .verify_evidence(
                context_with_scope(
                    ContractId::Platform,
                    63,
                    0,
                    15,
                    required(3, RegionId::new),
                    Some(required(4, RealmId::new)),
                    required(5, ArtifactFingerprint::new),
                ),
                required(1, AccountId::new),
                required(2, PlatformEvidenceHandle::new),
                &mut observer,
            )
            .expect_err("realm-scoped platform verification must fail closed");
        assert_eq!(platform_error.class(), AdapterFailureClass::Unauthorized);

        let capacity_error = adapter
            .observe_capacity(
                context_with_scope(
                    ContractId::Capacity,
                    64,
                    0,
                    15,
                    required(3, RegionId::new),
                    Some(required(4, RealmId::new)),
                    required(5, ArtifactFingerprint::new),
                ),
                required(3, RegionId::new),
                required(5, ArtifactFingerprint::new),
                &mut observer,
            )
            .expect_err("realm-scoped capacity query must fail closed");
        assert_eq!(capacity_error.class(), AdapterFailureClass::Unauthorized);
    }

    #[test]
    fn capacity_counts_only_matching_pending_region_and_artifact() {
        let mut adapter = stub_adapter();
        let mut observer = RecordingObserver::default();
        let region = required(3, RegionId::new);
        let other_region = required(30, RegionId::new);
        let artifact = required(5, ArtifactFingerprint::new);
        let other_artifact = required(50, ArtifactFingerprint::new);

        let matching = PlacementRequest::new(
            required(1, AccountId::new),
            required(71, SessionId::new),
            region,
            required(4, RealmId::new),
            artifact,
        );
        adapter
            .submit(context_for(71, 0), matching, &mut observer)
            .expect("matching placement fixture must succeed");

        for (operation, request_region, request_artifact) in
            [(72, other_region, artifact), (73, region, other_artifact)]
        {
            let placement = PlacementRequest::new(
                required(1, AccountId::new),
                required(operation, SessionId::new),
                request_region,
                required(4, RealmId::new),
                request_artifact,
            );
            adapter.placements.push(PlacementRecord {
                context: context_with_scope(
                    ContractId::Placement,
                    operation,
                    0,
                    15,
                    request_region,
                    Some(required(4, RealmId::new)),
                    request_artifact,
                ),
                request: placement,
                receipt: PlacementReceipt::new(
                    required(operation, OperationId::new),
                    required(operation, AllocationId::new),
                ),
                status: PlacementStatus::Pending,
            });
        }

        let capacity = adapter
            .observe_capacity(
                context_for_contract(ContractId::Capacity, 74, 0),
                region,
                artifact,
                &mut observer,
            )
            .expect("capacity observation must succeed");

        assert_eq!(capacity.pending_operations(), 1);
    }
}
