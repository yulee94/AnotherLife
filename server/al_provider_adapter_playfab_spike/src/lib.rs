//! Disposable non-production Microsoft PlayFab Multiplayer Servers adapter spike.
//!
//! Provider resource identities, errors, and transport objects stay in this crate.
//! The authoritative core sees only the provider-neutral contracts. This spike
//! deliberately grants no gameplay, economy, social, identity, or realm authority.

#![forbid(unsafe_code)]

use al_server_core::provider_contracts::{
    AdapterBoundary, AdapterError, AdapterFailureClass, AdapterObservation, AdapterObservationKind,
    AdapterObserver, AdapterRequestContext, AllocationId, ArtifactFingerprint,
    AuthorizationContextId, CapacityObservation, CompatibilityFingerprint, ContractId,
    DeploymentAdapter, EndpointHandle, LeaseEpoch, LifecycleReceipt, LifecycleStatus,
    OperationsAdapter, PlacementAdapter, PlacementReceipt, PlacementRequest, PlacementStatus,
    ProcessLifecycleRequest, RegionId,
};

/// Sanitized PlayFab API failure retained only inside the disposable adapter.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PlayFabApiError {
    /// HTTP status observed from the service or transport wrapper.
    pub http_status: u16,
    /// Optional numeric PlayFab error code. Messages are intentionally excluded.
    pub playfab_error_code: Option<u32>,
}

/// Maps PlayFab transport/service failures into the stable provider-neutral vocabulary.
#[must_use]
pub fn map_playfab_failure(http_status: u16, playfab_error_code: Option<u32>) -> AdapterError {
    let class = if http_status == 0 {
        AdapterFailureClass::AmbiguousCompletion
    } else if http_status == 429 || playfab_error_code == Some(1199) {
        AdapterFailureClass::Throttled
    } else if matches!(http_status, 401 | 403) {
        AdapterFailureClass::Unauthorized
    } else if http_status == 409 {
        AdapterFailureClass::Conflict
    } else if matches!(http_status, 408 | 502 | 503 | 504) {
        AdapterFailureClass::Unavailable
    } else if playfab_error_code == Some(1609) {
        AdapterFailureClass::Unsupported
    } else if (400..500).contains(&http_status) {
        AdapterFailureClass::InvalidRequest
    } else {
        AdapterFailureClass::Internal
    };
    AdapterError::from_class(class)
}

impl PlayFabApiError {
    fn into_adapter_error(self) -> AdapterError {
        map_playfab_failure(self.http_status, self.playfab_error_code)
    }

    fn into_mutating_adapter_error(self) -> AdapterError {
        if self.http_status == 0
            || self.http_status == 408
            || (500..600).contains(&self.http_status)
        {
            AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion)
        } else {
            self.into_adapter_error()
        }
    }
}

/// Provider-private MPS server lifecycle states needed for translation.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum PlayFabServerState {
    /// Allocation request has not produced an active server.
    Pending,
    /// Server is allocated and may be exposed through a fenced neutral lease.
    Active,
    /// Provider accepted shutdown, but termination is not yet confirmed.
    Terminating,
    /// Provider confirms the server is fully terminated.
    Terminated,
    /// Provider returned a state this spike does not understand.
    Unknown,
}

/// Provider-private MPS server response.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PlayFabServer {
    /// Provider server identifier; never returned across the neutral boundary.
    pub server_id: String,
    /// Deterministic adapter-private session identifier derived from operation identity.
    pub session_id: String,
    /// Azure region reported by MPS.
    pub region: String,
    /// Sanitized lifecycle state.
    pub state: PlayFabServerState,
}

/// Operation-specific result of looking up one exact build/session/region identity.
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum PlayFabServerDetails {
    /// The exact scoped server was found.
    Found(PlayFabServer),
    /// The exact scoped server was authoritatively absent. A transport must not
    /// derive this result from HTTP status alone.
    Absent,
}

/// Sanitized point-in-time MPS capacity counts.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PlayFabCapacity {
    /// Processes MPS reports ready for allocation.
    pub ready_processes: u64,
    /// Processes MPS reports allocated.
    pub allocated_processes: u64,
    /// Adapter-observed pending control-plane operations.
    pub pending_operations: u64,
}

/// Narrow provider-private API seam used by the disposable adapter.
///
/// A live implementation may use PlayFab REST, while tests use deterministic
/// responses. No PlayFab type appears in `al_server_core`.
pub trait PlayFabApi {
    /// Requests one server in the exact pre-authorized region.
    fn request_server(
        &mut self,
        title_id: &str,
        build_id: &str,
        session_id: &str,
        region: &str,
    ) -> Result<PlayFabServer, PlayFabApiError>;

    /// Reconciles a prior request by stable build/session/region identity.
    fn server_details(
        &mut self,
        title_id: &str,
        build_id: &str,
        session_id: &str,
        region: &str,
    ) -> Result<PlayFabServerDetails, PlayFabApiError>;

    /// Stops one previously reconciled synthetic server.
    fn shutdown_server(
        &mut self,
        title_id: &str,
        build_id: &str,
        session_id: &str,
        region: &str,
    ) -> Result<(), PlayFabApiError>;

    /// Returns whether the configured immutable build has ready capacity in-region.
    fn build_ready(
        &mut self,
        title_id: &str,
        build_id: &str,
        region: &str,
    ) -> Result<bool, PlayFabApiError>;

    /// Reads point-in-time provider counts without making them admission authority.
    fn capacity(
        &mut self,
        title_id: &str,
        build_id: &str,
        region: &str,
    ) -> Result<PlayFabCapacity, PlayFabApiError>;
}

/// Explicit non-production provider scope. Region and build are selected before MPS.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PlayFabScope {
    title_id: String,
    build_id: String,
    region_id: RegionId,
    authorization_context_id: AuthorizationContextId,
    home_region: String,
    forbidden_region: String,
    artifact: ArtifactFingerprint,
    compatibility: CompatibilityFingerprint,
}

impl PlayFabScope {
    /// Creates a fail-closed PlayFab scope with distinct home and forbidden regions.
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        title_id: impl Into<String>,
        build_id: impl Into<String>,
        region_id: RegionId,
        authorization_context_id: AuthorizationContextId,
        home_region: impl Into<String>,
        forbidden_region: impl Into<String>,
        artifact: ArtifactFingerprint,
        compatibility: CompatibilityFingerprint,
    ) -> Result<Self, &'static str> {
        let title_id = title_id.into();
        let build_id = build_id.into();
        let home_region = home_region.into();
        let forbidden_region = forbidden_region.into();
        let valid_identifier = |value: &str| {
            !value.is_empty()
                && value
                    .bytes()
                    .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'_' | b'.'))
        };
        if !valid_identifier(&title_id) || !valid_identifier(&build_id) {
            return Err("title and build identifiers must be nonblank opaque identifiers");
        }
        if home_region.trim().is_empty()
            || forbidden_region.trim().is_empty()
            || home_region.eq_ignore_ascii_case(&forbidden_region)
        {
            return Err("home and forbidden regions must be distinct nonblank values");
        }
        Ok(Self {
            title_id,
            build_id,
            region_id,
            authorization_context_id,
            home_region,
            forbidden_region,
            artifact,
            compatibility,
        })
    }

    /// Returns the opaque title identity for provider-private transport setup.
    #[must_use]
    pub fn title_id(&self) -> &str {
        &self.title_id
    }

    /// Returns the opaque configured MPS build identity.
    #[must_use]
    pub fn build_id(&self) -> &str {
        &self.build_id
    }

    /// Returns the configured provider region name.
    #[must_use]
    pub fn home_region(&self) -> &str {
        &self.home_region
    }

    /// Returns the region that the common workload must probe and reject.
    #[must_use]
    pub fn forbidden_region(&self) -> &str {
        &self.forbidden_region
    }

    fn approved_cleanup_region(&self, reported_region: &str) -> Option<&str> {
        if reported_region.eq_ignore_ascii_case(&self.home_region) {
            Some(&self.home_region)
        } else if reported_region.eq_ignore_ascii_case(&self.forbidden_region) {
            Some(&self.forbidden_region)
        } else {
            None
        }
    }

    fn context_matches(
        &self,
        context: AdapterRequestContext,
        expected_contract: ContractId,
        realm_required: bool,
    ) -> bool {
        context.contract_id() == expected_contract
            && context.region_id() == self.region_id
            && context.authorization_context_id() == self.authorization_context_id
            && context.artifact_fingerprint() == self.artifact
            && context.compatibility_fingerprint() == self.compatibility
            && context.realm_id().is_some() == realm_required
    }
}

/// Opaque proof that the exact synthetic PlayFab scope was authorized by the
/// process environment. No credential value is retained.
pub struct SyntheticSandboxAuthorization {
    scope: PlayFabScope,
}

impl SyntheticSandboxAuthorization {
    /// Validates explicit authorization and exact title/build/region bindings.
    pub fn from_environment(scope: &PlayFabScope) -> Result<Self, &'static str> {
        let exact =
            |name: &str, expected: &str| std::env::var(name).is_ok_and(|value| value == expected);
        let has_secret_reference =
            std::env::var("PLAYFAB_SECRET_KEY").is_ok_and(|value| !value.is_empty());
        if std::env::var("PLAYFAB_SPIKE_LIVE_AUTHORIZED").as_deref() != Ok("1")
            || !exact("PLAYFAB_TITLE_ID", &scope.title_id)
            || !exact("PLAYFAB_BUILD_ID", &scope.build_id)
            || !exact("PLAYFAB_HOME_REGION", &scope.home_region)
            || !exact("PLAYFAB_FORBIDDEN_REGION", &scope.forbidden_region)
            || !has_secret_reference
        {
            return Err("exact synthetic PlayFab scope authorization is required");
        }
        Ok(Self {
            scope: scope.clone(),
        })
    }
}

#[derive(Clone, Debug, Eq, PartialEq)]
struct PlacementRecord {
    context: AdapterRequestContext,
    request: PlacementRequest,
    receipt: PlacementReceipt,
    session_id: String,
    server_id: String,
    lease_epoch: LeaseEpoch,
    endpoint: EndpointHandle,
    cancelled: bool,
    cleanup_region: Option<String>,
    drifted: bool,
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

/// Opaque provider-private journal retained across adapter instance recreation.
///
/// Fields stay private so PlayFab resource identities cannot leak into the
/// provider-neutral core. The host owns retention inside the isolated candidate
/// boundary; this spike intentionally does not define a production store format.
#[derive(Clone, Eq, PartialEq)]
pub struct PlayFabJournal {
    scope: PlayFabScope,
    placements: Vec<PlacementRecord>,
    cancellations: Vec<CancellationRecord>,
    lifecycles: Vec<LifecycleRecord>,
    enabled: bool,
}

/// Disposable PlayFab adapter. It owns only provider mappings and operation journals.
pub struct PlayFabAdapter<A> {
    scope: PlayFabScope,
    api: A,
    placements: Vec<PlacementRecord>,
    cancellations: Vec<CancellationRecord>,
    lifecycles: Vec<LifecycleRecord>,
    enabled: bool,
}

impl<A> PlayFabAdapter<A> {
    /// Creates an enabled candidate only after explicit synthetic-sandbox authorization.
    pub fn new(
        scope: PlayFabScope,
        api: A,
        authorization: SyntheticSandboxAuthorization,
    ) -> Result<Self, &'static str> {
        if authorization.scope != scope {
            return Err("synthetic PlayFab authorization does not match adapter scope");
        }
        Ok(Self {
            scope,
            api,
            placements: Vec::new(),
            cancellations: Vec::new(),
            lifecycles: Vec::new(),
            enabled: true,
        })
    }

    /// Recreates an adapter instance with retained operations.
    pub fn resume(
        scope: PlayFabScope,
        api: A,
        authorization: SyntheticSandboxAuthorization,
        journal: PlayFabJournal,
    ) -> Result<Self, &'static str> {
        if authorization.scope != scope {
            return Err("synthetic PlayFab authorization does not match adapter scope");
        }
        if scope != journal.scope {
            return Err("retained PlayFab journal scope does not match adapter scope");
        }
        Ok(Self {
            scope,
            api,
            placements: journal.placements,
            cancellations: journal.cancellations,
            lifecycles: journal.lifecycles,
            enabled: journal.enabled,
        })
    }

    /// Consumes the adapter and returns its opaque provider-private journal.
    #[must_use]
    pub fn into_journal(self) -> PlayFabJournal {
        PlayFabJournal {
            scope: self.scope,
            placements: self.placements,
            cancellations: self.cancellations,
            lifecycles: self.lifecycles,
            enabled: self.enabled,
        }
    }

    /// Disables new provider work for rollback. Existing neutral state is untouched.
    pub fn disable(&mut self) {
        self.enabled = false;
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
        error: AdapterError,
        observer: &mut dyn AdapterObserver,
    ) -> AdapterError {
        Self::observe(
            context,
            boundary,
            AdapterObservationKind::Failed(error.class()),
            observer,
        );
        error
    }

    fn placement_index(&self, operation_id: u64) -> Option<usize> {
        self.placements
            .iter()
            .position(|record| record.context.operation_id().get() == operation_id)
    }

    fn lifecycle_index(&self, operation_id: u64) -> Option<usize> {
        self.lifecycles
            .iter()
            .position(|record| record.receipt.operation_id().get() == operation_id)
    }

    fn cancellation_index(&self, operation_id: u64) -> Option<usize> {
        self.cancellations
            .iter()
            .position(|record| record.context.operation_id().get() == operation_id)
    }
}

impl<A: PlayFabApi> PlayFabAdapter<A> {
    fn reconcile_cleanup(&mut self, placement_index: usize) -> Result<bool, AdapterError> {
        let record = self.placements[placement_index].clone();
        let cleanup_region = record
            .cleanup_region
            .ok_or_else(|| AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion))?;
        let response = match self.api.server_details(
            &self.scope.title_id,
            &self.scope.build_id,
            &record.session_id,
            &cleanup_region,
        ) {
            Err(error) => return Err(error.into_adapter_error()),
            Ok(PlayFabServerDetails::Absent) => return Ok(false),
            Ok(PlayFabServerDetails::Found(response)) => response,
        };
        if response.session_id != record.session_id
            || response.server_id != record.server_id
            || !response.region.eq_ignore_ascii_case(&cleanup_region)
        {
            return Err(AdapterError::from_class(AdapterFailureClass::Conflict));
        }
        if response.state == PlayFabServerState::Terminated {
            self.placements[placement_index].cancelled = true;
            Ok(true)
        } else {
            Ok(false)
        }
    }
}

impl<A: PlayFabApi> PlacementAdapter for PlayFabAdapter<A> {
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
            let record = self.placements[index].clone();
            if !record.context.has_same_retry_invariants(context) || record.request != request {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterError::from_class(AdapterFailureClass::Conflict),
                    observer,
                ));
            }
            if record.drifted {
                let error = match self.reconcile_cleanup(index) {
                    Ok(true) => AdapterError::from_class(AdapterFailureClass::Unauthorized),
                    Ok(false) => AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion),
                    Err(error) => error,
                };
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    error,
                    observer,
                ));
            }
            if record.cancelled {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterError::from_class(AdapterFailureClass::Unavailable),
                    observer,
                ));
            }
            if record.server_id.is_empty() {
                match self.api.server_details(
                    &self.scope.title_id,
                    &self.scope.build_id,
                    &record.session_id,
                    &self.scope.home_region,
                ) {
                    Ok(PlayFabServerDetails::Absent) => {
                        self.placements.remove(index);
                    }
                    Err(error) => {
                        return Err(Self::fail(
                            context,
                            AdapterBoundary::Placement,
                            error.into_adapter_error(),
                            observer,
                        ));
                    }
                    Ok(PlayFabServerDetails::Found(response)) => {
                        if response.session_id != record.session_id
                            || !response
                                .region
                                .eq_ignore_ascii_case(&self.scope.home_region)
                        {
                            return Err(Self::fail(
                                context,
                                AdapterBoundary::Placement,
                                AdapterError::from_class(AdapterFailureClass::Conflict),
                                observer,
                            ));
                        }
                        self.placements[index].server_id = response.server_id;
                        Self::observe(
                            context,
                            AdapterBoundary::Placement,
                            AdapterObservationKind::Duplicate,
                            observer,
                        );
                        return Ok(record.receipt);
                    }
                }
            } else {
                Self::observe(
                    context,
                    AdapterBoundary::Placement,
                    AdapterObservationKind::Duplicate,
                    observer,
                );
                return Ok(record.receipt);
            }
        }
        if !self.enabled {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterError::from_class(AdapterFailureClass::Unavailable),
                observer,
            ));
        }
        if !self
            .scope
            .context_matches(context, ContractId::Placement, true)
            || request.region_id() != self.scope.region_id
            || request.realm_id() != context.realm_id().expect("realm requirement checked")
            || request.artifact() != self.scope.artifact
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        let session_id = format!("al-bakeoff-op-{}", context.operation_id().get());
        let allocation = nonzero_hash(&format!("allocation:{session_id}"), AllocationId::new);
        let lease_epoch = nonzero_hash(&format!("lease:{session_id}"), LeaseEpoch::new);
        let endpoint = nonzero_hash(&format!("endpoint:{session_id}"), EndpointHandle::new);
        let receipt = PlacementReceipt::new(context.operation_id(), allocation);
        let response = match self.api.request_server(
            &self.scope.title_id,
            &self.scope.build_id,
            &session_id,
            &self.scope.home_region,
        ) {
            Ok(response) => response,
            Err(error) => {
                let adapter_error = error.into_mutating_adapter_error();
                if adapter_error.class() == AdapterFailureClass::AmbiguousCompletion {
                    self.placements.push(PlacementRecord {
                        context,
                        request,
                        receipt,
                        session_id,
                        server_id: String::new(),
                        lease_epoch,
                        endpoint,
                        cancelled: false,
                        cleanup_region: None,
                        drifted: false,
                    });
                }
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    adapter_error,
                    observer,
                ));
            }
        };
        if response.session_id != session_id
            || !response
                .region
                .eq_ignore_ascii_case(&self.scope.home_region)
        {
            let cleanup_region = if response.session_id == session_id {
                self.scope
                    .approved_cleanup_region(&response.region)
                    .map(str::to_owned)
            } else {
                None
            };
            self.placements.push(PlacementRecord {
                context,
                request,
                receipt,
                session_id: session_id.clone(),
                server_id: response.server_id,
                lease_epoch,
                endpoint,
                cancelled: false,
                cleanup_region: cleanup_region.clone(),
                drifted: true,
            });
            let error = if let Some(region) = cleanup_region {
                match self.api.shutdown_server(
                    &self.scope.title_id,
                    &self.scope.build_id,
                    &session_id,
                    &region,
                ) {
                    Ok(()) => AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion),
                    Err(error) => error.into_mutating_adapter_error(),
                }
            } else {
                AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion)
            };
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                error,
                observer,
            ));
        }
        self.placements.push(PlacementRecord {
            context,
            request,
            receipt,
            session_id,
            server_id: response.server_id,
            lease_epoch,
            endpoint,
            cancelled: false,
            cleanup_region: None,
            drifted: false,
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
        if !self.enabled
            || !self
                .scope
                .context_matches(context, ContractId::Placement, true)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        let index = self
            .placement_index(receipt.operation_id().get())
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterError::from_class(AdapterFailureClass::InvalidRequest),
                    observer,
                )
            })?;
        if self.placements[index].receipt != receipt
            || !has_same_resource_scope(self.placements[index].context, context)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        if self.placements[index].drifted {
            let error = match self.reconcile_cleanup(index) {
                Ok(true) => AdapterError::from_class(AdapterFailureClass::Unauthorized),
                Ok(false) => AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion),
                Err(error) => error,
            };
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                error,
                observer,
            ));
        }
        if self.placements[index].cancelled {
            Self::observe(
                context,
                AdapterBoundary::Placement,
                AdapterObservationKind::Succeeded,
                observer,
            );
            return Ok(PlacementStatus::Cancelled);
        }
        let response = self
            .api
            .server_details(
                &self.scope.title_id,
                &self.scope.build_id,
                &self.placements[index].session_id,
                &self.scope.home_region,
            )
            .map_err(|error| {
                Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    error.into_adapter_error(),
                    observer,
                )
            })?;
        let response = match response {
            PlayFabServerDetails::Found(response) => response,
            PlayFabServerDetails::Absent => {
                Self::observe(
                    context,
                    AdapterBoundary::Placement,
                    AdapterObservationKind::Failed(AdapterFailureClass::Unavailable),
                    observer,
                );
                return Ok(PlacementStatus::Failed(AdapterFailureClass::Unavailable));
            }
        };
        if response.session_id != self.placements[index].session_id
            || response.server_id != self.placements[index].server_id
            || !response
                .region
                .eq_ignore_ascii_case(&self.scope.home_region)
        {
            let cleanup_region = if response.session_id == self.placements[index].session_id
                && response.server_id == self.placements[index].server_id
            {
                self.scope
                    .approved_cleanup_region(&response.region)
                    .map(str::to_owned)
            } else {
                None
            };
            self.placements[index].drifted = true;
            self.placements[index].cleanup_region = cleanup_region.clone();
            let session_id = self.placements[index].session_id.clone();
            let error = if let Some(region) = cleanup_region {
                match self.api.shutdown_server(
                    &self.scope.title_id,
                    &self.scope.build_id,
                    &session_id,
                    &region,
                ) {
                    Ok(()) => AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion),
                    Err(error) => error.into_mutating_adapter_error(),
                }
            } else {
                AdapterError::from_class(AdapterFailureClass::AmbiguousCompletion)
            };
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                error,
                observer,
            ));
        }
        let status = match response.state {
            PlayFabServerState::Pending => PlacementStatus::Pending,
            PlayFabServerState::Active => PlacementStatus::Ready {
                allocation_id: receipt.allocation_id(),
                lease_epoch: self.placements[index].lease_epoch,
                endpoint: self.placements[index].endpoint,
            },
            PlayFabServerState::Terminating | PlayFabServerState::Terminated => {
                PlacementStatus::Failed(AdapterFailureClass::Unavailable)
            }
            PlayFabServerState::Unknown => PlacementStatus::Failed(AdapterFailureClass::Internal),
        };
        let kind = match status {
            PlacementStatus::Pending => AdapterObservationKind::Pending,
            PlacementStatus::Ready { .. } => AdapterObservationKind::Succeeded,
            PlacementStatus::Cancelled => AdapterObservationKind::Succeeded,
            PlacementStatus::Failed(class) => AdapterObservationKind::Failed(class),
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
            let record = self.cancellations[index];
            if !record.context.has_same_retry_invariants(context) || record.receipt != receipt {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterError::from_class(AdapterFailureClass::Conflict),
                    observer,
                ));
            }
            if record.status == PlacementStatus::Pending {
                let placement_index = self
                    .placement_index(receipt.operation_id().get())
                    .ok_or_else(|| {
                        Self::fail(
                            context,
                            AdapterBoundary::Placement,
                            AdapterError::from_class(AdapterFailureClass::InvalidRequest),
                            observer,
                        )
                    })?;
                match self.reconcile_cleanup(placement_index) {
                    Ok(true) => {
                        self.cancellations[index].status = PlacementStatus::Cancelled;
                    }
                    Ok(false) => {}
                    Err(error) => {
                        return Err(Self::fail(
                            context,
                            AdapterBoundary::Placement,
                            error,
                            observer,
                        ));
                    }
                }
            }
            Self::observe(
                context,
                AdapterBoundary::Placement,
                AdapterObservationKind::Duplicate,
                observer,
            );
            return Ok(self.cancellations[index].status);
        }
        if !self
            .scope
            .context_matches(context, ContractId::Placement, true)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        let index = self
            .placement_index(receipt.operation_id().get())
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Placement,
                    AdapterError::from_class(AdapterFailureClass::InvalidRequest),
                    observer,
                )
            })?;
        if self.placements[index].receipt != receipt
            || !has_same_resource_scope(self.placements[index].context, context)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        if self.placements[index].cancelled {
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
            return Ok(PlacementStatus::Cancelled);
        }
        if self.placements[index].drifted {
            let status = match self.reconcile_cleanup(index) {
                Ok(true) => PlacementStatus::Cancelled,
                Ok(false) => PlacementStatus::Pending,
                Err(error) => {
                    return Err(Self::fail(
                        context,
                        AdapterBoundary::Placement,
                        error,
                        observer,
                    ));
                }
            };
            self.cancellations.push(CancellationRecord {
                context,
                receipt,
                status,
            });
            Self::observe(
                context,
                AdapterBoundary::Placement,
                if status == PlacementStatus::Pending {
                    AdapterObservationKind::Pending
                } else {
                    AdapterObservationKind::Succeeded
                },
                observer,
            );
            return Ok(status);
        }
        self.placements[index].cleanup_region = Some(self.scope.home_region.clone());
        self.cancellations.push(CancellationRecord {
            context,
            receipt,
            status: PlacementStatus::Pending,
        });
        if let Err(error) = self.api.shutdown_server(
            &self.scope.title_id,
            &self.scope.build_id,
            &self.placements[index].session_id,
            &self.scope.home_region,
        ) {
            let adapter_error = error.into_mutating_adapter_error();
            if adapter_error.class() != AdapterFailureClass::AmbiguousCompletion {
                self.cancellations.pop();
                self.placements[index].cleanup_region = None;
            }
            return Err(Self::fail(
                context,
                AdapterBoundary::Placement,
                adapter_error,
                observer,
            ));
        }
        Self::observe(
            context,
            AdapterBoundary::Placement,
            AdapterObservationKind::Pending,
            observer,
        );
        Ok(PlacementStatus::Pending)
    }
}

impl<A: PlayFabApi> DeploymentAdapter for PlayFabAdapter<A> {
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
        if !self.enabled
            || !self
                .scope
                .context_matches(context, ContractId::Deployment, false)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        if let Some(index) = self.lifecycle_index(context.operation_id().get()) {
            let record = self.lifecycles[index];
            if !record.context.has_same_retry_invariants(context) || record.request != request {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    AdapterError::from_class(AdapterFailureClass::Conflict),
                    observer,
                ));
            }
            Self::observe(
                context,
                AdapterBoundary::Deployment,
                AdapterObservationKind::Duplicate,
                observer,
            );
            return Ok(record.receipt);
        }
        let (region_id, artifact) = lifecycle_scope(request);
        if region_id != self.scope.region_id || artifact != self.scope.artifact {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        let status = match request {
            ProcessLifecycleRequest::EnsureReady { .. } => {
                if self
                    .api
                    .build_ready(
                        &self.scope.title_id,
                        &self.scope.build_id,
                        &self.scope.home_region,
                    )
                    .map_err(|error| {
                        Self::fail(
                            context,
                            AdapterBoundary::Deployment,
                            error.into_adapter_error(),
                            observer,
                        )
                    })?
                {
                    LifecycleStatus::Complete
                } else {
                    LifecycleStatus::Pending
                }
            }
            ProcessLifecycleRequest::Drain { .. } | ProcessLifecycleRequest::Retire { .. } => {
                return Err(Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    AdapterError::from_class(AdapterFailureClass::Unsupported),
                    observer,
                ));
            }
        };
        let receipt = LifecycleReceipt::new(context.operation_id());
        self.lifecycles.push(LifecycleRecord {
            context,
            request,
            receipt,
            status,
        });
        let kind = match status {
            LifecycleStatus::Pending => AdapterObservationKind::Pending,
            LifecycleStatus::Complete => AdapterObservationKind::Succeeded,
            LifecycleStatus::Failed(class) => AdapterObservationKind::Failed(class),
        };
        Self::observe(context, AdapterBoundary::Deployment, kind, observer);
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
        if !self.enabled
            || !self
                .scope
                .context_matches(context, ContractId::Deployment, false)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        let index = self
            .lifecycle_index(receipt.operation_id().get())
            .ok_or_else(|| {
                Self::fail(
                    context,
                    AdapterBoundary::Deployment,
                    AdapterError::from_class(AdapterFailureClass::InvalidRequest),
                    observer,
                )
            })?;
        if self.lifecycles[index].receipt != receipt
            || !has_same_resource_scope(self.lifecycles[index].context, context)
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Deployment,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        if self.lifecycles[index].status == LifecycleStatus::Pending
            && matches!(
                self.lifecycles[index].request,
                ProcessLifecycleRequest::EnsureReady { .. }
            )
        {
            let ready = self
                .api
                .build_ready(
                    &self.scope.title_id,
                    &self.scope.build_id,
                    &self.scope.home_region,
                )
                .map_err(|error| {
                    Self::fail(
                        context,
                        AdapterBoundary::Deployment,
                        error.into_adapter_error(),
                        observer,
                    )
                })?;
            if ready {
                self.lifecycles[index].status = LifecycleStatus::Complete;
            }
        }
        let status = self.lifecycles[index].status;
        let kind = match status {
            LifecycleStatus::Pending => AdapterObservationKind::Pending,
            LifecycleStatus::Complete => AdapterObservationKind::Succeeded,
            LifecycleStatus::Failed(class) => AdapterObservationKind::Failed(class),
        };
        Self::observe(context, AdapterBoundary::Deployment, kind, observer);
        Ok(status)
    }
}

impl<A: PlayFabApi> OperationsAdapter for PlayFabAdapter<A> {
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
        if !self.enabled
            || !self
                .scope
                .context_matches(context, ContractId::Capacity, false)
            || region_id != self.scope.region_id
            || artifact != self.scope.artifact
        {
            return Err(Self::fail(
                context,
                AdapterBoundary::Operations,
                AdapterError::from_class(AdapterFailureClass::Unauthorized),
                observer,
            ));
        }
        let capacity = self
            .api
            .capacity(
                &self.scope.title_id,
                &self.scope.build_id,
                &self.scope.home_region,
            )
            .map_err(|error| {
                Self::fail(
                    context,
                    AdapterBoundary::Operations,
                    error.into_adapter_error(),
                    observer,
                )
            })?;
        Self::observe(
            context,
            AdapterBoundary::Operations,
            AdapterObservationKind::Succeeded,
            observer,
        );
        Ok(CapacityObservation::new(
            region_id,
            artifact,
            capacity.ready_processes,
            capacity.allocated_processes,
            capacity.pending_operations,
        ))
    }
}

fn lifecycle_scope(request: ProcessLifecycleRequest) -> (RegionId, ArtifactFingerprint) {
    match request {
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
    }
}

fn has_same_resource_scope(
    original: AdapterRequestContext,
    current: AdapterRequestContext,
) -> bool {
    original.contract_id() == current.contract_id()
        && original.actor_id() == current.actor_id()
        && original.service_id() == current.service_id()
        && original.authorization_context_id() == current.authorization_context_id()
        && original.policy_version() == current.policy_version()
        && original.region_id() == current.region_id()
        && original.realm_id() == current.realm_id()
        && original.schema_version() == current.schema_version()
        && original.artifact_fingerprint() == current.artifact_fingerprint()
        && original.compatibility_fingerprint() == current.compatibility_fingerprint()
}

fn nonzero_hash<T>(value: &str, constructor: fn(u64) -> Option<T>) -> T {
    let mut hash = 0xcbf2_9ce4_8422_2325_u64;
    for byte in value.bytes() {
        hash ^= u64::from(byte);
        hash = hash.wrapping_mul(0x0000_0100_0000_01b3);
    }
    if hash == 0 {
        hash = 1;
    }
    constructor(hash).expect("nonzero hash")
}
